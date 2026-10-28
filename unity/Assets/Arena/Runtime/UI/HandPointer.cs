using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Ponteiro dos menus sem controle (pedido do Pablo, 2026-10-28): com um menu aberto, cada mão LIVRE ganha um raio
    // que sai do nó do indicador (direção = do ombro estimado até a mão, mais estável que o dedo). Encostar o indicador no
    // polegar (pinça) clica na linha mirada. Depois que um menu abre é preciso soltar a pinça antes do 1º clique (o gesto
    // de menu da Meta também é uma pinça e não pode clicar em "Continuar" sozinho).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class HandPointer : MonoBehaviour
    {
        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();

        const float PinchOn = 0.02f, PinchOff = 0.04f, ReleaseToArm = 0.15f, IdleLength = 1.2f;

        sealed class Ptr
        {
            public LineRenderer Line; public Transform Dot;
            public bool Pinching, Ready; public float ReleasedT;
        }
        readonly Ptr[] ptr = { new Ptr(), new Ptr() };
        int seenSerial = -1;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Update()
        {
            bool any = player.IsVr && MenuPanel.Visible.Count > 0;
            if (!any) { Hide(0); Hide(1); return; }
            if (MenuPanel.OpenSerial != seenSerial)
            {
                seenSerial = MenuPanel.OpenSerial;
                foreach (var p in ptr) { p.Ready = false; p.ReleasedT = 0f; }
            }
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            foreach (var panel in MenuPanel.Visible) panel.Hover = -1;
            if (hands == null) { Hide(0); Hide(1); return; }
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                var hand = side == 0 ? hands.leftHand : hands.rightHand;
                if (!free || !hand.isTracked
                    || !World(hand, XRHandJointID.IndexProximal, out var knuckle)
                    || !World(hand, XRHandJointID.IndexTip, out var indexTip)
                    || !World(hand, XRHandJointID.ThumbTip, out var thumbTip)) { Hide(side); continue; }
                Point(side, knuckle, Vector3.Distance(indexTip, thumbTip));
            }
        }

        void Point(int side, Vector3 knuckle, float pinchDist)
        {
            var p = ptr[side];
            var head = player.HeadPosition;
            var right = Vector3.Cross(Vector3.up, player.HeadForward).normalized;
            var shoulder = head - Vector3.up * 0.18f + right * (side == 0 ? -0.17f : 0.17f);
            var dir = (knuckle - shoulder).normalized;
            var ray = new Ray(knuckle, dir);

            MenuPanel best = null; int bestRow = -1; float bestDist = float.MaxValue; Vector3 hit = default;
            foreach (var panel in MenuPanel.Visible)
                if (panel.Raycast(ray, out float d, out int row, out var pt) && d < bestDist) { best = panel; bestRow = row; bestDist = d; hit = pt; }

            bool wasPinching = p.Pinching;
            if (p.Pinching) { if (pinchDist > PinchOff) p.Pinching = false; }
            else if (pinchDist < PinchOn) p.Pinching = true;
            if (!p.Pinching) { p.ReleasedT += Time.unscaledDeltaTime; if (p.ReleasedT >= ReleaseToArm) p.Ready = true; }
            else p.ReleasedT = 0f;

            if (best != null && bestRow >= 0)
            {
                best.Hover = bestRow;
                if (p.Pinching && !wasPinching && p.Ready)
                {
                    best.Clicked = bestRow;
                    ArenaAudio.Play2D(Sfx.Click, 0.7f, 1.3f);
                }
            }
            Draw(p, knuckle, best != null ? hit : knuckle + dir * IdleLength, best != null, p.Pinching);
        }

        void Draw(Ptr p, Vector3 from, Vector3 to, bool onPanel, bool pinching)
        {
            if (!p.Line)
            {
                p.Line = new GameObject("Ponteiro").AddComponent<LineRenderer>();
                p.Line.material = ArenaUtil.Unlit(new Color(0.6f, 0.85f, 1f, 0.6f));
                p.Line.positionCount = 2; p.Line.widthMultiplier = 0.004f; p.Line.numCapVertices = 2;
                p.Dot = ArenaUtil.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.018f, ArenaUtil.Unlit(new Color(0.8f, 0.95f, 1f))).transform;
                foreach (var c in p.Dot.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            p.Line.gameObject.SetActive(true);
            p.Line.SetPosition(0, from); p.Line.SetPosition(1, to);
            p.Dot.gameObject.SetActive(onPanel);
            p.Dot.position = to;
            p.Dot.localScale = Vector3.one * (pinching ? 0.012f : 0.018f);
        }

        void Hide(int side)
        {
            var p = ptr[side];
            if (p.Line) p.Line.gameObject.SetActive(false);
            if (p.Dot) p.Dot.gameObject.SetActive(false);
            p.Pinching = false;
        }

        bool World(XRHand hand, XRHandJointID id, out Vector3 world)
        {
            world = default;
            if (!hand.GetJoint(id).TryGetPose(out UnityEngine.Pose pose)) return false;
            world = player.trackingSpace.TransformPoint(pose.position);
            return true;
        }
    }
}
