using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Poderes da mão esquerda LIVRE (só com a mão rastreada, nunca com o controle na mão), qualquer classe:
    //   palma aberta parada = raio do Freeza (fino, instantâneo, na direção dos dedos, atravessa 2),
    //   punho indo rápido para frente = soco (um inimigo um pouco além do braço, forte, recarga de 1 s),
    //   palma aberta em movimento rápido = corte da palma. Apontar o indicador está livre para um poder futuro.
    // O golpe no chão (atordoar em área) está guardado em GroundSlam() para uma habilidade futura.
    // Sinais de mão (HandSignArming, no Core) existem mas não são usados aqui: ficam para habilidades futuras.
    // Desktop (sem mãos): Z raio (segurar), X soco, C palma — só com o escudo nas costas (F).
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
            bulletMat = ArenaUtil.Unlit(new Color(1f, 0.85f, 1f));   // núcleo quase branco, brilho rosa-violeta em volta
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
            if (player.Input.DesktopSlam) Punch(cam.position + cam.right * -0.2f + cam.up * -0.2f + player.HeadForward * 0.3f, player.HeadForward);
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
            // Soco: punho fechado indo rápido para frente (na direção em que você olha, no plano do chão).
            var flatVel = wristVel.Flat();
            if (CurrentShape == HandShape.Punho && flatVel.magnitude > HandPowers.PunchMinSpeed
                && Vector3.Dot(flatVel.normalized, player.HeadForward) > 0.5f)
            {
                Punch(wristPrev, wristVel.normalized);
            }
            // Palma aberta: rápida = corte; parada por 0,3 s = raio (nunca no meio de um corte).
            float handSpeed = wristVel.magnitude;
            if (handSpeed > HandPowers.BeamPalmMaxSpeed) stillT = 0f; else stillT += Time.deltaTime;
            palmLockout -= Time.deltaTime;
            if (CurrentShape == HandShape.Aberta && shapeT > 0.1f && handSpeed > HandPowers.PalmMinSpeed)
            {
                if (Palm(wristPrev, wristVel)) palmLockout = HandPowers.BeamLockoutAfterPalm;
            }
            else if (CurrentShape == HandShape.Aberta && shapeT > HandPowers.BeamPalmHoldSeconds
                     && stillT > HandPowers.BeamPalmHoldSeconds && palmLockout <= 0f
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
        // Raio do Freeza: instantâneo, fino e brilhante; atravessa até 2 inimigos e para na parede.
        void Fire(Vector3 origin, Vector3 dir)
        {
            if (gunCooldown > 0f) return;
            if (!player.State.TryBeam()) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            gunCooldown = HandPowers.BeamInterval;
            dir.Normalize();
            // Até onde vai: parede ou chão.
            float len = 0f;
            while (len < HandPowers.BeamRange)
            {
                var p = origin + dir * (len + 0.25f);
                if (p.y < 0f || ArenaUtil.IsOutside(p)) break;
                len += 0.25f;
            }
            var end = origin + dir * len;
            // Inimigos no caminho, do mais perto ao mais longe.
            var hits = new List<(float d, ArenaEnemy e)>();
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (!Geometry.SweptSegmentsHit(origin.ToN(), end.ToN(), origin.ToN(), end.ToN(), a.ToN(), b.ToN(), a.ToN(), b.ToN(), e.Radius + 0.05f)) continue;
                hits.Add((Vector3.Dot(e.Center - origin, dir), e));
            }
            hits.Sort((x, y) => x.d.CompareTo(y.d));
            for (int i = 0; i < hits.Count && i < HandPowers.BeamPierce; i++)
            {
                hits[i].e.ApplyDamage(HandPowers.BeamDamage, HandPowers.BeamPosture, dir, 0.6f, new Color(1f, 0.55f, 1f));
                if (!hits[i].e.Dead) hits[i].e.State.Status.AddCharge(StatusRules.BeamCharge);
                ArenaFx.Burst(hits[i].e.Center, new Color(1f, 0.6f, 1f), 6, 2f);
                if (i == HandPowers.BeamPierce - 1) end = origin + dir * Mathf.Max(0.3f, hits[i].d);
            }
            BeamVisual(origin, end);
            ArenaAudio.Play(Sfx.Shot, origin, 0.6f, 1.8f);
            ArenaAudio.Play(Sfx.Ice, end, 0.4f, 1.6f);
        }

        void BeamVisual(Vector3 from, Vector3 to)
        {
            var len = Vector3.Distance(from, to);
            if (len < 0.05f) return;
            var core = ArenaUtil.Prim(PrimitiveType.Cylinder, null, (from + to) * 0.5f, new Vector3(0.018f, len * 0.5f, 0.018f), bulletMat);
            core.transform.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            var glow = ArenaUtil.Prim(PrimitiveType.Cylinder, core.transform, Vector3.zero, new Vector3(3f, 1f, 3f), ArenaUtil.Unlit(new Color(0.9f, 0.4f, 1f, 0.3f)));
            ArenaFx.Burst(from, new Color(1f, 0.7f, 1f), 4, 1.5f, 0.6f);
            Destroy(core, 0.09f);
        }

        // Soco: um inimigo à frente do punho, até um pouco além do braço. Forte e de recarga baixa.
        bool Punch(Vector3 hand, Vector3 dir)
        {
            ArenaEnemy target = null; float best = float.MaxValue;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead || !HandPowers.PunchHits(hand.ToN(), dir.ToN(), e.Center.ToN(), e.Radius)) continue;
                float d = Vector3.Distance(hand, e.Center);
                if (d < best) { best = d; target = e; }
            }
            if (!target) return false;   // socar o ar não gasta nada
            if (player.State.TryHandPower(HandPowers.Punch) != SkillCastResult.Ok) return false;
            var at = target.Center - dir * target.Radius * 0.8f;
            target.ApplyDamage(HandPowers.PunchDamage, HandPowers.PunchPosture, dir, HandPowers.PunchKnock, new Color(1f, 0.82f, 0.56f));
            target.PhysicalImpact(12f, dir);
            ArenaFx.Burst(at, new Color(1f, 0.82f, 0.56f), 16, 3.5f, 1.2f);
            ArenaFx.Ring(at, new Color(1f, 0.85f, 0.6f), 0.5f, 0.2f);
            ArenaAudio.Play(Sfx.Impact, at, 1f, 1.2f);
            ArenaInput.Haptic(0.9f, 0.08f, left: true);
            return true;
        }

        // GUARDADO (pedido do Pablo): golpe no chão que atordoa em área. Fora do jogo até virar habilidade de alguma árvore.
        bool GroundSlam()
        {
            if (player.State.TryHandPower(HandPowers.Slam) != SkillCastResult.Ok) return false;   // (não chamado por enquanto)
            var pp = player.FeetPosition;
            ArenaFx.Ring(pp, new Color(1f, 0.82f, 0.56f), HandPowers.SlamRadius + 0.2f, 0.4f);
            ArenaAudio.Play(Sfx.Slam, pp, 1f);
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
