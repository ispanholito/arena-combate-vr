using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Poderes da mão esquerda LIVRE (só com a mão rastreada, nunca com o controle na mão), qualquer classe:
    //   palma aberta parada = metralhadora (reta, na direção dos dedos), punho golpeando para baixo = soco no chão (atordoa em 3 m),
    //   palma aberta em movimento rápido = corte da palma. Apontar o indicador está livre para um poder futuro.
    // Sinais de mão (HandSignArming, no Core) existem mas não são usados aqui: ficam para habilidades futuras.
    // Desktop (sem mãos): Z metralhadora (segurar), X soco, C palma — só com o escudo nas costas (F).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class LeftHandPowers : MonoBehaviour
    {
        public HandShape CurrentShape { get; private set; }
        public bool HandTracked { get; private set; }
        HandShape lastShape;
        float shapeT;

        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        float gunCooldown;
        Vector3 wristPrev, wristVel, wristLocalPrev;
        bool hadWrist;
        Material bulletMat;

        void Awake()
        {
            player = GetComponent<ArenaPlayer>();
            bulletMat = ArenaUtil.Unlit(new Color(1f, 0.45f, 0.35f));
        }

        void Update()
        {
            if (ArenaGame.Blocked) return;
            float dt = Time.deltaTime;
            gunCooldown -= dt;
            if (player.IsVr) TrackHand(dt); else HandTracked = false;
            if (player.TreeOpen) return;
            if (player.IsVr) VrPowers(); else Desktop();
        }

        bool ShieldInHand => player.Shield && player.Shield.Equipped;

        void Desktop()
        {
            if (ShieldInHand)
            {
                if (player.Input.DesktopLaser || player.Input.DesktopSlam || player.Input.DesktopPalm)
                    ArenaGame.Instance?.Message("Guarde o escudo (F) para usar a mão", 0.8f);
                return;
            }
            var cam = player.Head;
            if (player.Input.DesktopLaser) Fire(cam.position + cam.right * -0.2f + cam.up * -0.25f, cam.forward);
            if (player.Input.DesktopSlam) Slam();
            if (player.Input.DesktopPalm) Palm(cam.position + player.HeadForward * 0.4f, cam.right * -4f);
        }

        void TrackHand(float dt)
        {
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            HandTracked = hands != null && hands.leftHand.isTracked;
            CurrentShape = HandShape.Nenhuma;
            if (HandTracked)
            {
                var h = hands.leftHand;
                CurrentShape = Detect(h);
                if (World(h, XRHandJointID.Wrist, out var wrist))
                {
                    // Velocidade relativa ao corpo: andar, esquivar ou girar não conta como mover a mão.
                    var local = player.trackingSpace.InverseTransformPoint(wrist);
                    if (hadWrist && dt > 0f) wristVel = player.trackingSpace.TransformDirection((local - wristLocalPrev) / dt);
                    wristLocalPrev = local; wristPrev = wrist; hadWrist = true;
                }
            }
            else hadWrist = false;
            if (CurrentShape == lastShape) shapeT += dt; else { lastShape = CurrentShape; shapeT = 0f; }
        }

        void VrPowers()
        {
            if (player.Weapon && player.Weapon.TwoHanded) return;   // mão ocupada segurando a arma
            if (!HandTracked || !player.Input.LeftHandFree) return;
            var hand = hands.leftHand;
            if (CurrentShape == HandShape.Punho
                && wristVel.y < -HandPowers.SlamMinDownSpeed && wristPrev.y < HandPowers.SlamMaxHandHeight)
            {
                Slam();
            }
            // Palma aberta: rápida = corte; parada por 0,3 s = metralhadora (nunca no meio de um corte).
            float handSpeed = wristVel.magnitude;
            if (handSpeed > HandPowers.GunPalmMaxSpeed) stillT = 0f; else stillT += Time.deltaTime;
            palmLockout -= Time.deltaTime;
            if (CurrentShape == HandShape.Aberta && shapeT > 0.1f && handSpeed > HandPowers.PalmMinSpeed)
            {
                if (Palm(wristPrev, wristVel)) palmLockout = HandPowers.GunLockoutAfterPalm;
            }
            else if (CurrentShape == HandShape.Aberta && shapeT > HandPowers.GunPalmHoldSeconds
                     && stillT > HandPowers.GunPalmHoldSeconds && palmLockout <= 0f
                     && PalmAim(hand, out var origin, out var dir))
            {
                Fire(origin, dir);
            }
        }

        float stillT, palmLockout;

        // Mira reta na direção dos dedos (do pulso para a ponta do dedo médio), saindo da ponta dos dedos.
        bool PalmAim(XRHand hand, out Vector3 origin, out Vector3 dir)
        {
            origin = dir = default;
            if (!World(hand, XRHandJointID.Wrist, out var w) || !World(hand, XRHandJointID.MiddleTip, out var tip)) return false;
            var d = tip - w;
            if (d.sqrMagnitude < 1e-4f) return false;
            dir = d.normalized;
            origin = tip;
            return true;
        }

        // ---------- Poderes ----------
        void Fire(Vector3 origin, Vector3 dir)
        {
            if (gunCooldown > 0f) return;
            if (!player.State.TryGunShot()) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            gunCooldown = HandPowers.GunFireInterval;
            var vis = ArenaUtil.Prim(PrimitiveType.Sphere, null, origin, Vector3.one * 0.05f, bulletMat);
            var p = Projectile.Spawn(vis, origin + dir * 0.05f, dir * HandPowers.GunBulletSpeed);
            p.Life = HandPowers.GunRange / HandPowers.GunBulletSpeed; p.Radius = 0.06f; p.Pierce = 1;
            p.OnHitEnemy = (e, _) => e.ApplyDamage(HandPowers.GunDamage, HandPowers.GunPosture, dir, 0.3f, new Color(1f, 0.55f, 0.5f));
        }

        bool Slam()
        {
            if (player.State.TryHandPower(HandPowers.Slam) != SkillCastResult.Ok) return false;
            var pp = player.FeetPosition;
            ArenaFx.Ring(pp, new Color(1f, 0.82f, 0.56f), HandPowers.SlamRadius + 0.2f, 0.4f);
            ArenaFx.Burst(pp + Vector3.up * 0.2f, new Color(0.69f, 0.56f, 0.44f), 30, 4f, 1.5f);
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
            return true;
        }

        bool Palm(Vector3 pos, Vector3 sweep)
        {
            if (player.State.TryHandPower(HandPowers.Palm) != SkillCastResult.Ok) return false;
            SkillCaster.LaunchSlash(pos + player.HeadForward * 0.3f, player.HeadForward, sweep, HandPowers.PalmDamage, new Color(1f, 0.9f, 0.66f));
            return true;
        }

        // ---------- Detecção do formato da mão ----------
        // Centro da palma esquerda rastreada (para segurar a arma com as duas mãos).
        public bool TryGetPalm(out Vector3 pos)
        {
            pos = default;
            return HandTracked && hands != null && World(hands.leftHand, XRHandJointID.Palm, out pos);
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
