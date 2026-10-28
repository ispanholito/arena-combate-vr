using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Andar sem controle (ideia do Pablo, 2026-10-28) com o sinal do Homem-Aranha (indicador + mindinho esticados), em
    // qualquer mão LIVRE (sem controle nela):
    //   - fazer o sinal duas vezes seguidas = dash para trás (gasta estamina como a esquiva);
    //   - fazer o sinal apontando para cima = dispara uma teia até o teto/parede que o dedo aponta (ou prende no ar);
    //     fechar a mão e puxar em direção ao corpo = você é puxado pela teia; abrir a mão = solta e cai.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SpiderLocomotion : MonoBehaviour
    {
        public static SpiderLocomotion Instance { get; private set; }

        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        readonly SpiderSignDetector[] detectors = { new SpiderSignDetector(), new SpiderSignDetector() };

        // Teia
        bool ropeActive, grabbing, grabbedOnce;
        int ropeSide;
        Vector3 anchor, lastHandLocal;
        float ropeT, openT;
        LineRenderer line;

        // A mão está segurando a teia (os poderes dessa mão ficam desligados: o punho não vira soco).
        public static bool IsBusy(int side) => Instance && Instance.ropeActive && Instance.ropeSide == side;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (line) Destroy(line.gameObject); }

        void Update()
        {
            if (ArenaGame.Blocked || !player.IsVr || player.TreeOpen) { if (ropeActive) Release(); return; }
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            if (hands == null) return;
            float dt = Time.deltaTime;
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                var hand = side == 0 ? hands.leftHand : hands.rightHand;
                if (!free || !hand.isTracked) { detectors[side].Update(false, 0f, dt); if (ropeActive && ropeSide == side) Release(); continue; }
                var shape = Detect(hand);
                bool sign = shape == HandShape.Chifres;
                float up = IndexAim(hand, out var tip, out var dir) ? dir.y : 0f;
                if (ropeActive && ropeSide == side) { UpdateRope(hand, shape, dt); continue; }
                switch (detectors[side].Update(sign, up, dt))
                {
                    case SpiderAction.DashBack:
                        player.DashBack();
                        ArenaFx.FloatText("DASH", player.HeadPosition + player.HeadForward * 0.8f, new Color(0.8f, 0.9f, 1f), 0.5f);
                        break;
                    case SpiderAction.Rope:
                        if (IndexAim(hand, out tip, out dir)) Shoot(side, tip, dir, hand);
                        break;
                }
            }
        }

        // ---------- Teia ----------
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public void Shoot(int side, Vector3 origin, Vector3 dir, XRHand hand)
        {
            dir.Normalize();
            anchor = origin + dir * RopeRules.VirtualAnchorDistance;   // céu aberto: prende no ar
            float best = float.MaxValue;
            int n = Physics.RaycastNonAlloc(origin, dir, hits, RopeRules.Range, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (player.Weapon && player.Weapon.Body && c.attachedRigidbody == player.Weapon.Body) continue;
                if (c.GetComponentInParent<RagdollBone>()) continue;   // inimigo não serve de âncora
                if (hits[i].distance < best) { best = hits[i].distance; anchor = hits[i].point; }
            }
            ropeActive = true; ropeSide = side; ropeT = 0f; openT = 0f; grabbing = false; grabbedOnce = false;
            lastHandLocal = player.trackingSpace.InverseTransformPoint(PalmOrTip(hand, origin));
            if (!line)
            {
                line = new GameObject("Teia").AddComponent<LineRenderer>();
                line.material = ArenaUtil.Unlit(new Color(0.95f, 0.95f, 0.95f));
                line.widthMultiplier = 0.012f;
                line.positionCount = 2;
                line.numCapVertices = 2;
            }
            line.gameObject.SetActive(true);
            ArenaAudio.Play(Sfx.Whoosh, origin, 0.8f, 1.6f);
            ArenaFx.Burst(anchor, new Color(0.95f, 0.95f, 0.95f), 8, 1.5f, 0.6f);
            ArenaGame.Instance?.Message("Teia! Feche a mão e puxe", 1f);
        }

        // Debug/desktop: dispara a teia sem mão rastreada (testes no editor).
        public void ShootDebug(Vector3 origin, Vector3 dir) => Shoot(0, origin, dir, default);
        public void PullDebug(float meters) { if (ropeActive) { player.Hanging = true; Pull(meters); } }

        void UpdateRope(XRHand hand, HandShape shape, float dt)
        {
            ropeT += dt;
            var handPos = PalmOrTip(hand, player.HeadPosition);
            if (line) { line.SetPosition(0, handPos); line.SetPosition(1, anchor); }
            if (ropeT > RopeRules.MaxSeconds || (!grabbedOnce && ropeT > RopeRules.GrabTimeout)) { Release(); return; }
            bool fist = shape == HandShape.Punho;
            var local = player.trackingSpace.InverseTransformPoint(handPos);
            if (fist)
            {
                grabbing = grabbedOnce = true; openT = 0f;
                player.Hanging = true;   // segurando a teia: sem gravidade
                // Puxar a mão em direção ao corpo (oposto à teia) sobe/avança pela teia.
                var handDelta = player.trackingSpace.TransformVector(local - lastHandLocal);
                var ropeDir = (anchor - handPos).normalized;
                float pulled = Vector3.Dot(-handDelta, ropeDir);
                if (pulled > 0f) Pull(pulled * RopeRules.PullGain);
            }
            else if (grabbedOnce)
            {
                player.Hanging = false;
                openT += dt;
                if (openT > RopeRules.ReleaseOpenSeconds) { Release(); return; }
            }
            lastHandLocal = local;
        }

        void Pull(float meters)
        {
            var handPos = player.HeadPosition;
            var toAnchor = anchor - handPos;
            float dist = toAnchor.magnitude;
            if (dist < RopeRules.ArriveDistance) return;
            player.MoveRig(toAnchor / dist * Mathf.Min(meters, dist - RopeRules.ArriveDistance));
        }

        void Release()
        {
            ropeActive = false; grabbing = false;
            player.Hanging = false;
            if (line) line.gameObject.SetActive(false);
        }

        // ---------- Mão ----------
        Vector3 PalmOrTip(XRHand hand, Vector3 fallback) =>
            hands != null && World(hand, XRHandJointID.Palm, out var p) ? p : fallback;

        bool IndexAim(XRHand hand, out Vector3 tip, out Vector3 dir)
        {
            tip = dir = default;
            if (!World(hand, XRHandJointID.IndexProximal, out var b) || !World(hand, XRHandJointID.IndexTip, out tip)) return false;
            var d = tip - b;
            if (d.sqrMagnitude < 1e-4f) return false;
            dir = d.normalized;
            return true;
        }

        bool World(XRHand hand, XRHandJointID id, out Vector3 world)
        {
            world = default;
            if (!hand.GetJoint(id).TryGetPose(out UnityEngine.Pose p)) return false;
            world = player.trackingSpace.TransformPoint(p.position);
            return true;
        }

        bool? Extended(XRHand hand, XRHandJointID proximal, XRHandJointID tipId)
        {
            if (!World(hand, XRHandJointID.Wrist, out var w) || !World(hand, proximal, out var p) || !World(hand, tipId, out var t)) return null;
            return Vector3.Distance(t, w) > Vector3.Distance(p, w) + 0.045f;
        }

        HandShape Detect(XRHand h)
        {
            var i = Extended(h, XRHandJointID.IndexProximal, XRHandJointID.IndexTip);
            var m = Extended(h, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip);
            var r = Extended(h, XRHandJointID.RingProximal, XRHandJointID.RingTip);
            var l = Extended(h, XRHandJointID.LittleProximal, XRHandJointID.LittleTip);
            if (i == null || m == null || r == null || l == null) return HandShape.Nenhuma;
            return HandSigns.Classify(i.Value, m.Value, r.Value, l.Value);
        }
    }
}
