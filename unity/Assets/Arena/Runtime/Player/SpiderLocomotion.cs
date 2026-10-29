using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Andar sem controle (Pablo, 2026-10-28), com qualquer mão LIVRE (sem controle nela):
    //   - TEIA (refeita depois do teste): há pontos de gancho no alto das pilastras, paredes e teto (GrapplePoint).
    //     O ponto para onde você OLHA acende. Aponte a mão FECHADA para ele e ABRA: a teia prende nele (2026-10-29:
    //     com o ponto aceso e a mão mirando nele, abrir lança a teia e NUNCA a bola de fogo). Feche a mão de novo e
    //     puxe em direção ao corpo: você ganha velocidade ao longo da teia. Abriu a mão de novo: solta e continua
    //     com o EMBALO, caindo em arco com a gravidade (ArenaPlayer.AirVelocity).
    //   - Sinal do Homem-Aranha (indicador + mindinho) duas vezes seguidas = dash para trás.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SpiderLocomotion : MonoBehaviour
    {
        public static SpiderLocomotion Instance { get; private set; }

        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        readonly SpiderSignDetector[] detectors = { new SpiderSignDetector(), new SpiderSignDetector() };
        readonly HandShape[] lastShape = new HandShape[2];

        // Teia
        bool ropeActive, grabbedOnce;
        int ropeSide;
        Vector3 anchor, lastHandLocal;
        float ropeT, openT, sincePull = 99f;
        LineRenderer line;
        GrapplePoint target;   // ponto aceso (para onde o jogador olha)

        public GrapplePoint Target => target;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (line) Destroy(line.gameObject); }

        void Update()
        {
            bool anyFree = player.IsVr && (player.Input.LeftHandFree || player.Input.RightHandFree);
            if (ArenaGame.Blocked || !player.IsVr || player.TreeOpen) { if (ropeActive) Release(); SetTarget(null); return; }
            // O ponto que acende: o mais perto do centro do olhar (com uma mão livre para usar).
            SetTarget(anyFree && !ropeActive ? PickGazeTarget() : null);
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
                if (!free || !hand.isTracked) { detectors[side].Update(false, 0f, dt); lastShape[side] = HandShape.Nenhuma; if (ropeActive && ropeSide == side) Release(); continue; }
                var shape = Detect(hand);
                bool openFromFist = shape == HandShape.Aberta && lastShape[side] == HandShape.Punho;
                if (shape != HandShape.Nenhuma) lastShape[side] = shape;
                if (ropeActive && ropeSide == side) { HandLock.Hold(side, "teia"); UpdateRope(hand, shape, dt); continue; }
                // Sinal do Homem-Aranha 2× = dash para trás (a teia por sinal foi trocada pelos pontos de gancho).
                if (detectors[side].Update(shape == HandShape.Chifres, 0f, dt) == SpiderAction.DashBack)
                {
                    player.DashBack();
                    ArenaFx.FloatText("DASH", player.HeadPosition + player.HeadForward * 0.8f, new Color(0.8f, 0.9f, 1f), 0.5f);
                }
                // Mão fechada mirando o ponto aceso + abrir = lança a teia.
                if (openFromFist && !ropeActive && target && !Muralha.Active && HandLock.Free(side, "teia") && World(hand, XRHandJointID.Palm, out var palm) && AimsAt(palm, target.Position))
                    Shoot(side, palm, target.Position);
            }
        }

        // ---------- Pontos de gancho ----------
        GrapplePoint PickGazeTarget()
        {
            GrapplePoint best = null; float bestAngle = HandMagicRules.GrappleGazeAngle;
            var eye = player.HeadPosition; var look = player.Head.forward;
            foreach (var g in GrapplePoint.All)
            {
                if (!g || !g.Usable) continue;
                var to = g.Position - eye;
                float d = to.magnitude;
                if (d > HandMagicRules.GrappleRange || d < 0.8f) continue;
                float a = Vector3.Angle(look, to);
                if (a < bestAngle) { bestAngle = a; best = g; }
            }
            return best;
        }

        void SetTarget(GrapplePoint g)
        {
            if (target == g) return;
            if (target) target.SetLit(false);
            target = g;
            if (target) target.SetLit(true);
        }

        // Braço esticado e a mão apontando para o ponto (a partir da cabeça).
        // A mão está mirando o ponto aceso? (a bola de fogo da mão não arma nesse caso: a teia tem prioridade)
        public bool AimsAtLit(Vector3 palm) => target && !ropeActive && AimsAt(palm, target.Position);

        bool AimsAt(Vector3 palm, Vector3 point)
        {
            var eye = player.HeadPosition;
            var arm = palm - eye;
            if (arm.magnitude < HandMagicRules.GrappleExtend) return false;
            return Vector3.Angle(arm, point - eye) <= HandMagicRules.GrappleHandAngle;
        }

        // ---------- Teia ----------
        public void Shoot(int side, Vector3 handPos, Vector3 point)
        {
            anchor = point;
            TutorialEvents.Raise("teia");
            ropeActive = true; ropeSide = side; ropeT = 0f; openT = 0f; grabbedOnce = false; sincePull = 99f;   // lança com a mão aberta: falta fechar
            lastHandLocal = player.trackingSpace.InverseTransformPoint(handPos);
            if (!line)
            {
                line = new GameObject("Teia").AddComponent<LineRenderer>();
                line.material = ArenaUtil.Unlit(new Color(0.95f, 0.95f, 0.95f));
                line.widthMultiplier = 0.014f;
                line.positionCount = 2;
                line.numCapVertices = 2;
            }
            line.gameObject.SetActive(true);
            line.SetPosition(0, handPos); line.SetPosition(1, anchor);
            SetTarget(null);
            ArenaAudio.Play(Sfx.Whoosh, handPos, 0.8f, 1.6f);
            ArenaFx.Burst(anchor, new Color(0.95f, 0.95f, 0.95f), 10, 1.5f, 0.6f);
        }

        // Debug (editor): lança a teia e simula um puxão de 'speed' m/s da mão.
        public void ShootDebug(Vector3 point) => Shoot(0, player.HeadPosition, point);
        public void PullDebug(float handSpeed) { if (ropeActive) ApplyPull(handSpeed); }
        public void ReleaseDebug() => Release();

        void UpdateRope(XRHand hand, HandShape shape, float dt)
        {
            ropeT += dt; sincePull += dt;
            var handPos = World(hand, XRHandJointID.Palm, out var p) ? p : player.HeadPosition;
            if (line) { line.SetPosition(0, handPos); line.SetPosition(1, anchor); }
            if (ropeT > RopeRules.MaxSeconds) { Release(); return; }
            var local = player.trackingSpace.InverseTransformPoint(handPos);
            if (shape == HandShape.Punho)
            {
                grabbedOnce = true;
                openT = 0f;
                // Velocidade da mão em relação ao corpo, na direção oposta à teia = puxão.
                var handVel = player.trackingSpace.TransformVector(local - lastHandLocal) / Mathf.Max(dt, 1e-4f);
                var ropeDir = (anchor - player.HeadPosition).normalized;
                float pullSpeed = Vector3.Dot(-handVel, ropeDir);
                if (pullSpeed > 0.15f) ApplyPull(pullSpeed);
                player.Hanging = true;
                player.Coasting = sincePull < HandMagicRules.HangCoast;
            }
            else if (grabbedOnce)
            {
                openT += dt;
                if (openT > 0.08f) { Release(); return; }   // abriu a mão de novo: solta e segue com o embalo
            }
            else if (ropeT > RopeRules.GrabTimeout) { Release(); return; }   // lançou e não fechou a mão a tempo
            lastHandLocal = local;
        }

        void ApplyPull(float handSpeed)
        {
            var toAnchor = anchor - player.HeadPosition;
            float dist = toAnchor.magnitude;
            if (sincePull > 0.5f) TutorialEvents.Raise("puxar");
            sincePull = 0f;
            player.Hanging = true; player.Coasting = true;
            if (dist < RopeRules.ArriveDistance) { player.AirVelocity = Vector3.zero; return; }   // chegou: fica pendurado
            var dir = toAnchor / dist;
            var want = dir * Mathf.Min(handSpeed * HandMagicRules.PullGain, HandMagicRules.MaxAirSpeed);
            // Acelera ao longo da teia sem perder o embalo que já tinha.
            var cur = player.AirVelocity;
            player.AirVelocity = Vector3.Dot(cur, dir) >= want.magnitude ? cur : cur - dir * Vector3.Dot(cur, dir) + want;
        }

        void Release()
        {
            ropeActive = false;
            player.Hanging = false; player.Coasting = false;
            if (line) line.gameObject.SetActive(false);
        }

        // ---------- Mão ----------
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
