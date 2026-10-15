using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Poderes da mão esquerda livre (decisão do Pablo), para qualquer classe:
    //   apontar o indicador = laser contínuo (mana), punho golpeando para baixo = atordoa em 3 m,
    //   mão aberta em movimento rápido = corte de vento.
    // VR: lê a mão rastreada pelo XR Hands. Desktop: Z laser, X soco no chão, C corte da palma.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class LeftHandPowers : MonoBehaviour
    {
        public enum HandPose { Nenhuma, Aberta, Punho, Apontar, Outra }

        public HandPose CurrentPose { get; private set; }
        public bool HandTracked { get; private set; }

        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        LineRenderer laser;
        float poseT, laserAcc;
        Vector3 wristPrev, wristVel;
        bool hadWrist;

        void Awake()
        {
            player = GetComponent<ArenaPlayer>();
            laser = new GameObject("LaserDaMao").AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.startWidth = laser.endWidth = 0.015f;
            laser.material = ArenaUtil.Unlit(new Color(1f, 0.35f, 0.3f));
            laser.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            laser.enabled = false;
            if (player.TreeOpen) return;
            if (player.IsVr) VrHand(dt); else Desktop(dt);
        }

        void Desktop(float dt)
        {
            var cam = player.Head;
            if (player.Input.DesktopLaser)
                Laser(dt, cam.position + cam.right * -0.2f + cam.up * -0.25f, cam.forward);
            if (player.Input.DesktopSlam) Slam();
            if (player.Input.DesktopPalm) Palm(cam.position + player.HeadForward * 0.4f, cam.right * -4f);
        }

        void VrHand(float dt)
        {
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            HandTracked = hands != null && hands.leftHand.isTracked;
            var newPose = HandPose.Nenhuma;
            if (HandTracked)
            {
                var h = hands.leftHand;
                newPose = Detect(h);
                if (World(h, XRHandJointID.Wrist, out var wrist))
                {
                    if (hadWrist && dt > 0f) wristVel = (wrist - wristPrev) / dt;
                    wristPrev = wrist; hadWrist = true;
                }
            }
            else hadWrist = false;
            if (newPose != CurrentPose) { CurrentPose = newPose; poseT = 0f; } else poseT += dt;
            if (!HandTracked) return;

            var hand = hands.leftHand;
            if (CurrentPose == HandPose.Apontar && poseT > HandPowers.PoseHoldSeconds
                && World(hand, XRHandJointID.IndexTip, out var tip) && World(hand, XRHandJointID.IndexMetacarpal, out var baseJ))
                Laser(dt, tip, (tip - baseJ).normalized);
            if (CurrentPose == HandPose.Punho && wristVel.y < -HandPowers.SlamMinDownSpeed && wristPrev.y < HandPowers.SlamMaxHandHeight) Slam();
            if (CurrentPose == HandPose.Aberta && wristVel.magnitude > HandPowers.PalmMinSpeed && poseT > 0.1f) Palm(wristPrev, wristVel);
        }

        // ---------- Poderes ----------
        void Laser(float dt, Vector3 origin, Vector3 dir)
        {
            if (!player.State.DrainLaser(dt)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            float best = HandPowers.LaserRange; ArenaEnemy target = null;
            var end = origin + dir * HandPowers.LaserRange;
            foreach (var e in ArenaEnemy.All)
            {
                if (e == null || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.SegmentSegmentDistance(origin.ToN(), end.ToN(), a.ToN(), b.ToN()) < e.Radius + 0.05f)
                {
                    float t = Vector3.Dot(e.Center - origin, dir);
                    if (t > 0f && t < best) { best = t; target = e; }
                }
            }
            laser.enabled = true;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, origin + dir * best);
            if (target)
            {
                laserAcc += HandPowers.LaserDps * dt;
                if (laserAcc >= HandPowers.LaserTick)
                {
                    laserAcc -= HandPowers.LaserTick;
                    target.ApplyDamage(HandPowers.LaserTick, HandPowers.LaserPosture, null, 0f, new Color(1f, 0.5f, 0.5f));
                    ArenaInput.Haptic(0.3f, 0.03f, left: true);
                }
            }
        }

        void Slam()
        {
            if (player.State.TryHandPower(HandPowers.Slam) != SkillCastResult.Ok) return;
            var pp = player.FeetPosition;
            ArenaFx.Ring(pp, new Color(1f, 0.82f, 0.56f), HandPowers.SlamRadius + 0.2f, 0.4f);
            ArenaFx.Burst(pp + Vector3.up * 0.2f, new Color(0.69f, 0.56f, 0.44f), 30, 4f, 1.5f);
            ArenaInput.Haptic(1f, 0.16f, left: true);
            int n = 0;
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (e == null || e.Dead) continue;
                if (Vector3.Distance(e.transform.position.Flat(), pp.Flat()) < HandPowers.SlamRadius + e.Radius)
                {
                    e.State.ApplyStun(HandPowers.SlamStun);
                    e.ApplyDamage(HandPowers.SlamDamage, HandPowers.SlamPosture, null, 0f, new Color(1f, 0.82f, 0.56f));
                    n++;
                }
            }
            if (n > 0) ArenaGame.Instance?.Message($"Atordoou {n}!", 0.8f);
        }

        void Palm(Vector3 pos, Vector3 sweep)
        {
            if (player.State.TryHandPower(HandPowers.Palm) != SkillCastResult.Ok) return;
            SkillCaster.LaunchSlash(pos + player.HeadForward * 0.3f, player.HeadForward, sweep, HandPowers.PalmDamage, new Color(1f, 0.9f, 0.66f));
            ArenaInput.Haptic(0.6f, 0.06f, left: true);
        }

        // ---------- Detecção de pose (igual ao ArenaMultimodalTest) ----------
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

        HandPose Detect(XRHand h)
        {
            var i = Extended(h, XRHandJointID.IndexProximal, XRHandJointID.IndexTip);
            var m = Extended(h, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip);
            var r = Extended(h, XRHandJointID.RingProximal, XRHandJointID.RingTip);
            var l = Extended(h, XRHandJointID.LittleProximal, XRHandJointID.LittleTip);
            if (i == null || m == null || r == null || l == null) return HandPose.Nenhuma;
            if (i.Value && m.Value && r.Value && l.Value) return HandPose.Aberta;
            if (!i.Value && !m.Value && !r.Value && !l.Value) return HandPose.Punho;
            if (i.Value && !m.Value && !r.Value) return HandPose.Apontar;
            return HandPose.Outra;
        }
    }
}
