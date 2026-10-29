using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Interagir sem controle (pedido do Pablo, 2026-10-29): com a mão LIVRE perto de um baú, jaula, estátua, arma no
    // chão/estante, pedestal dos desafios, NPC, bigorna, forja, mercador... faça uma PINÇA (indicador + polegar).
    // É o mesmo gesto do clique nos menus; com um menu aberto quem cuida da pinça é o HandPointer.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class HandInteract : MonoBehaviour
    {
        const float PinchOn = 0.02f, PinchOff = 0.045f, Cooldown = 0.6f;
        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        readonly bool[] pinching = new bool[2];
        float cd;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Update()
        {
            cd -= Time.deltaTime;
            if (!player.IsVr || ArenaGame.Blocked || player.TreeOpen) { pinching[0] = pinching[1] = false; return; }
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
                if (hands == null) return;
            }
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                var hand = side == 0 ? hands.leftHand : hands.rightHand;
                if (!free || !hand.isTracked || !World(hand, XRHandJointID.IndexTip, out var index) || !World(hand, XRHandJointID.ThumbTip, out var thumb))
                { pinching[side] = false; continue; }
                float dist = Vector3.Distance(index, thumb);
                bool was = pinching[side];
                if (was) { if (dist > PinchOff) pinching[side] = false; }
                else if (dist < PinchOn) pinching[side] = true;
                if (!pinching[side] || was || cd > 0f) continue;
                if (Interact(player, (index + thumb) * 0.5f)) { cd = Cooldown; ArenaInput.Haptic(0.4f, 0.05f, left: side == 0); }
            }
        }

        // Mesma lista do grip/gatilho (as distâncias também contam pelos pés, então a mão livre funciona).
        static bool Interact(ArenaPlayer p, Vector3 at)
        {
            var g = ArenaGame.Instance;
            return (g && g.Sanctuary && g.Sanctuary.TryInteract(p)) || TreasureChest.TryOpen(p) || RescueCage.TryOpen(p)
                   || BlessingStatue.TryUse(p) || WeaponPickup.TryPickup(p);
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
