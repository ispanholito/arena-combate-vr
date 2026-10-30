using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Poses da mão LIVRE usadas para pegar coisas (poção, cabo de arma cravada...).
    public static class HandPoses
    {
        static XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();

        // Mão livre "pegou": punho fechado ou pinça (indicador + polegar). side 0 = esquerda, 1 = direita.
        public static bool Closed(ArenaPlayer player, int side)
        {
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
                if (hands == null) return false;
            }
            var h = side == 0 ? hands.leftHand : hands.rightHand;
            if (!h.isTracked) return false;
            if (World(player, h, XRHandJointID.IndexTip, out var it) && World(player, h, XRHandJointID.ThumbTip, out var tt) && Vector3.Distance(it, tt) < 0.025f) return true;
            if (!World(player, h, XRHandJointID.Wrist, out var w)) return false;
            int curled = 0;
            foreach (var (prox, tip) in new[] { (XRHandJointID.IndexProximal, XRHandJointID.IndexTip), (XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip),
                                                (XRHandJointID.RingProximal, XRHandJointID.RingTip) })
                if (World(player, h, prox, out var p) && World(player, h, tip, out var t) && Vector3.Distance(t, w) <= Vector3.Distance(p, w) + 0.045f) curled++;
            return curled >= 3;
        }

        static bool World(ArenaPlayer player, XRHand hand, XRHandJointID id, out Vector3 world)
        {
            world = default;
            if (!hand.GetJoint(id).TryGetPose(out UnityEngine.Pose p)) return false;
            world = player.trackingSpace.TransformPoint(p.position);
            return true;
        }
    }
}
