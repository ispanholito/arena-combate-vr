using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Poderes da mão LIVRE (só com a mão rastreada, nunca com o controle na mão), qualquer classe, nas DUAS mãos
    // (desde 2026-10-17 a direita espelha a esquerda quando você larga o controle direito):
    //   apontar o indicador = raio do Freeza (como o Death Beam do anime: ponto vermelho carrega na ponta do dedo,
    //   raio fino e instantâneo na direção do dedo, atravessa 2; repete enquanto apontar),
    //   punho indo rápido para frente = soco (um inimigo um pouco além do braço, forte, recarga de 1 s),
    //   palma aberta em movimento rápido = corte da palma,
    //   fechar a mão perto de um inimigo = AGARRAR (EnemyGrabber); abrir a mão solta/arremessa.
    // Palma aberta parada ficou livre para um poder futuro.
    // O golpe no chão (atordoar em área) está guardado em GroundSlam() para uma habilidade futura.
    // Desktop (sem mãos): Z raio (segurar), X soco, C palma — só com o escudo nas costas (F).
    // (O nome da classe ficou "LeftHandPowers" para não quebrar as cenas.)
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class LeftHandPowers : MonoBehaviour
    {
        // Mão esquerda (compatibilidade com HUD e duas mãos).
        public HandShape CurrentShape => left.Shape;
        public bool HandTracked => left.Tracked;
        public HandShape RightShape => right.Shape;
        public bool RightTracked => right.Tracked;

        sealed class Hand
        {
            public bool IsRight;
            public bool Tracked;
            public HandShape Shape, LastShape;
            public float ShapeT;
            public Vector3 WristPrev, WristVel, WristLocalPrev;
            public bool HadWrist;
            public float BeamCharge, GunCooldown;
            public Transform ChargeDot;
            public bool WasFist;
            public float OpenT;
        }

        readonly Hand left = new Hand(), right = new Hand { IsRight = true };
        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();
        Material bulletMat;

        void Awake()
        {
            player = GetComponent<ArenaPlayer>();
            bulletMat = ArenaUtil.Unlit(new Color(1f, 0.82f, 0.78f));   // núcleo quase branco, brilho vermelho em volta
        }

        void Update()
        {
            if (ArenaGame.Blocked) return;
            float dt = Time.deltaTime;
            left.GunCooldown -= dt; right.GunCooldown -= dt;
            if (player.IsVr)
            {
                if (hands == null || !hands.running)
                {
                    SubsystemManager.GetSubsystems(buffer);
                    hands = null;
                    foreach (var s in buffer) if (s.running) { hands = s; break; }
                }
                Track(left, dt); Track(right, dt);
            }
            else { left.Tracked = right.Tracked = false; }
            if (player.TreeOpen) return;
            if (player.IsVr)
            {
                bool leftFree = player.Input.LeftHandFree && !(player.Weapon && player.Weapon.TwoHanded);
                Powers(left, leftFree, dt);
                Powers(right, player.Input.RightHandFree, dt);
            }
            else Desktop();
        }

        XRHand Get(Hand h) => h.IsRight ? hands.rightHand : hands.leftHand;

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
            if (player.Input.DesktopLaser) Fire(left, cam.position + cam.right * -0.2f + cam.up * -0.25f, cam.forward);
            if (player.Input.DesktopSlam) Punch(cam.position + cam.right * -0.2f + cam.up * -0.2f + player.HeadForward * 0.3f, player.HeadForward, false);
            if (player.Input.DesktopPalm) Palm(cam.position + player.HeadForward * 0.4f, cam.right * -4f);
        }

        void Track(Hand h, float dt)
        {
            h.Tracked = hands != null && Get(h).isTracked;
            var shape = HandShape.Nenhuma;
            if (h.Tracked)
            {
                var xh = Get(h);
                shape = Detect(xh);
                if (World(xh, XRHandJointID.Wrist, out var wrist))
                {
                    // Velocidade relativa ao corpo: andar, esquivar ou girar não conta como mover a mão.
                    var local = player.trackingSpace.InverseTransformPoint(wrist);
                    if (h.HadWrist && dt > 0f) h.WristVel = player.trackingSpace.TransformDirection((local - h.WristLocalPrev) / dt);
                    h.WristLocalPrev = local; h.WristPrev = wrist; h.HadWrist = true;
                }
            }
            else h.HadWrist = false;
            h.Shape = shape;
            if (h.Shape == h.LastShape) h.ShapeT += dt; else { h.LastShape = h.Shape; h.ShapeT = 0f; }
        }

        void Powers(Hand h, bool free, float dt)
        {
            var grabber = EnemyGrabber.Instance;
            int side = h.IsRight ? 1 : 0;
            if (!HandLock.Free(side, "mao")) { h.BeamCharge = 0f; ChargeDot(h, Vector3.zero, -1f); return; }   // mão ocupada com teia/magia (HandLock)
            if (!free || !h.Tracked)
            {
                h.BeamCharge = 0f; ChargeDot(h, Vector3.zero, -1f);
                if (grabber && grabber.HoldingWithTrackedHand(side)) grabber.Release(side, Vector3.zero);
                h.WasFist = false;
                return;
            }
            var xh = Get(h);
            bool fist = h.Shape == HandShape.Punho;
            bool hasPalm = World(xh, XRHandJointID.Palm, out var palm);

            // Agarrar: fechou a mão perto de um inimigo. Enquanto fechada segura; abriu (0,12 s) solta com a velocidade da mão.
            if (grabber && hasPalm && !Muralha.Active)   // na Muralha: só soco
            {
                if (grabber.Holding(side))
                {
                    if (fist) { h.OpenT = 0f; grabber.Hold(side, palm); }
                    else
                    {
                        h.OpenT += dt;
                        if (h.OpenT > 0.12f) grabber.Release(side, h.WristVel);
                    }
                    h.WasFist = fist;
                    HandLock.Hold(side, "mao");
                    return;   // segurando alguém: sem outros poderes nesta mão
                }
                if (fist && !h.WasFist && grabber.TryGrab(side, palm, tracked: true)) { h.WasFist = true; h.OpenT = 0f; return; }
            }
            h.WasFist = fist;
            if (DemoGate.Hidden) { h.BeamCharge = 0f; ChargeDot(h, Vector3.zero, -1f); return; }   // demo: só agarrar (raio, soco e palma escondidos)

            // Soco: punho fechado indo rápido para frente (na direção em que você olha, no plano do chão).
            var flatVel = h.WristVel.Flat();
            if (fist && flatVel.magnitude > HandPowers.PunchMinSpeed && Vector3.Dot(flatVel.normalized, player.HeadForward) > 0.5f)
            {
                Punch(h.WristPrev, h.WristVel.normalized, h.IsRight);
                HandLock.Hold(side, "mao", 0.4f);
            }
            if (Muralha.Active) { h.BeamCharge = 0f; ChargeDot(h, Vector3.zero, -1f); return; }   // na Muralha: só soco
            // Palma aberta rápida com o braço ESTICADO na frente = corte (2026-10-29: perto do corpo é a corrida; do lado não vale).
            var palmRel = h.WristPrev - player.HeadPosition;
            float palmFwd = Vector3.Dot(palmRel, player.HeadForward), palmSide = Vector3.Dot(palmRel, Vector3.Cross(Vector3.up, player.HeadForward).normalized);
            if (h.Shape == HandShape.Aberta && h.ShapeT > 0.1f && h.WristVel.magnitude > HandPowers.PalmMinSpeed && PalmSweepRules.IsExtendedInFront(palmFwd, palmSide))
            {
                Palm(h.WristPrev, h.WristVel, h.IsRight ? 1 : 0);
                HandLock.Hold(side, "mao", 0.4f);
            }
            // Apontar o indicador = raio do Freeza: carrega um ponto vermelho na ponta do dedo e dispara.
            if (h.Shape == HandShape.Apontar && IndexAim(xh, out var origin, out var dir))
            {
                h.BeamCharge += dt;
                HandLock.Hold(side, "mao");
                ChargeDot(h, origin, Mathf.Clamp01(h.BeamCharge / HandPowers.BeamChargeSeconds));
                if (h.BeamCharge >= HandPowers.BeamChargeSeconds && h.GunCooldown <= 0f) Fire(h, origin, dir);
            }
            else { h.BeamCharge = 0f; ChargeDot(h, Vector3.zero, -1f); }
        }

        // Ponto que carrega na ponta do dedo (k 0..1; negativo esconde).
        void ChargeDot(Hand h, Vector3 at, float k)
        {
            if (k < 0f) { if (h.ChargeDot) h.ChargeDot.gameObject.SetActive(false); return; }
            if (!h.ChargeDot)
            {
                h.ChargeDot = ArenaUtil.Prim(PrimitiveType.Sphere, null, at, Vector3.one * 0.02f, ArenaUtil.Unlit(new Color(1f, 0.85f, 0.8f))).transform;
                ArenaUtil.Prim(PrimitiveType.Sphere, h.ChargeDot, Vector3.zero, Vector3.one * 2.2f, ArenaUtil.Unlit(new Color(1f, 0.08f, 0.12f, 0.45f)));
                foreach (var c in h.ChargeDot.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            h.ChargeDot.gameObject.SetActive(true);
            h.ChargeDot.position = at;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 60f);
            h.ChargeDot.localScale = Vector3.one * Mathf.Lerp(0.006f, 0.022f, k) * pulse;
        }

        // Mira pelo indicador (da base até a ponta), saindo da ponta do dedo.
        bool IndexAim(XRHand hand, out Vector3 origin, out Vector3 dir)
        {
            origin = dir = default;
            if (!World(hand, XRHandJointID.IndexProximal, out var b) || !World(hand, XRHandJointID.IndexTip, out var tip)) return false;
            var d = tip - b;
            if (d.sqrMagnitude < 1e-4f) return false;
            dir = d.normalized;
            origin = tip;
            return true;
        }

        // ---------- Poderes ----------
        // Raio do Freeza: instantâneo, fino e brilhante; atravessa até 2 inimigos e para na parede.
        void Fire(Hand h, Vector3 origin, Vector3 dir)
        {
            if (h.GunCooldown > 0f) return;
            if (!player.State.TryBeam()) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            h.GunCooldown = HandPowers.BeamInterval;
            TutorialEvents.Raise("raio");
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
                hits[i].e.ApplyDamage(HandPowers.BeamDamage, HandPowers.BeamPosture, dir, 0.6f, new Color(1f, 0.25f, 0.25f));
                if (!hits[i].e.Dead) hits[i].e.State.Status.AddCharge(StatusRules.BeamCharge);
                ArenaFx.Burst(hits[i].e.Center, new Color(1f, 0.2f, 0.15f), 8, 2.5f);
                if (i == HandPowers.BeamPierce - 1) end = origin + dir * Mathf.Max(0.3f, hits[i].d);
            }
            BeamVisual(origin, end);
            ArenaAudio.Play(Sfx.Shot, origin, 0.6f, 1.8f);
            ArenaAudio.Play(Sfx.Ice, end, 0.4f, 1.6f);
        }

        void Fire(Vector3 origin, Vector3 dir) => Fire(left, origin, dir);

        void BeamVisual(Vector3 from, Vector3 to)
        {
            var len = Vector3.Distance(from, to);
            if (len < 0.05f) return;
            var core = ArenaUtil.Prim(PrimitiveType.Cylinder, null, (from + to) * 0.5f, new Vector3(0.018f, len * 0.5f, 0.018f), bulletMat);
            core.transform.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            // Fino como no anime: núcleo branco-quente, halo vermelho e um brilho fraco por fora.
            core.transform.localScale = new Vector3(0.012f, len * 0.5f, 0.012f);
            ArenaUtil.Prim(PrimitiveType.Cylinder, core.transform, Vector3.zero, new Vector3(3.2f, 1f, 3.2f), ArenaUtil.Unlit(new Color(1f, 0.06f, 0.08f, 0.55f)));
            ArenaUtil.Prim(PrimitiveType.Cylinder, core.transform, Vector3.zero, new Vector3(8f, 1f, 8f), ArenaUtil.Unlit(new Color(1f, 0.1f, 0.1f, 0.14f)));
            foreach (var c in core.GetComponentsInChildren<Collider>()) Destroy(c);
            ArenaFx.Burst(from, new Color(1f, 0.3f, 0.25f), 5, 1.2f, 0.5f);
            ArenaFx.Burst(to, new Color(1f, 0.15f, 0.1f), 10, 2.5f, 0.8f);
            ArenaFx.Ring(to, new Color(1f, 0.2f, 0.15f), 0.25f, 0.15f);
            Destroy(core, 0.12f);
        }

        // Soco: um inimigo à frente do punho, até um pouco além do braço. Forte e de recarga baixa.
        bool Punch(Vector3 hand, Vector3 dir, bool rightHand)
        {
            ArenaEnemy target = null; float best = float.MaxValue;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead || !HandPowers.PunchHits(hand.ToN(), dir.ToN(), e.Center.ToN(), e.Radius)) continue;
                float d = Vector3.Distance(hand, e.Center);
                if (d < best) { best = d; target = e; }
            }
            if (!target) return false;   // socar o ar não gasta nada
            if (player.State.TryHandPower(HandPowers.Punch, rightHand ? 1 : 0) != SkillCastResult.Ok) return false;
            TutorialEvents.Raise("soco");
            var at = target.Center - dir * target.Radius * 0.8f;
            float boost = Muralha.Active ? MuralhaRules.PunchDamageMultiplier : 1f;   // Muralha: soco muito mais forte
            target.ApplyDamage(HandPowers.PunchDamage * boost, HandPowers.PunchPosture, dir, HandPowers.PunchKnock, Muralha.Active ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.82f, 0.56f));
            target.PhysicalImpact(12f, dir);
            Muralha.EmpowerPunch(target, dir);
            ArenaFx.Burst(at, new Color(1f, 0.82f, 0.56f), 16, 3.5f, 1.2f);
            ArenaFx.Ring(at, new Color(1f, 0.85f, 0.6f), 0.5f, 0.2f);
            ArenaAudio.Play(Sfx.Impact, at, 1f, 1.2f);
            ArenaInput.Haptic(0.9f, 0.08f, left: !rightHand);
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

        bool Palm(Vector3 pos, Vector3 sweep, int hand = 0)
        {
            if (player.State.TryHandPower(HandPowers.Palm, hand) != SkillCastResult.Ok) return false;
            TutorialEvents.Raise("palma");
            SkillCaster.LaunchSlash(pos + player.HeadForward * 0.3f, player.HeadForward, sweep, HandPowers.PalmDamage, new Color(1f, 0.9f, 0.66f));
            return true;
        }

        // ---------- Detecção do formato da mão ----------
        // Centro da palma esquerda rastreada (para segurar a arma com as duas mãos).
        public bool TryGetPalm(out Vector3 pos)
        {
            pos = default;
            return left.Tracked && hands != null && World(hands.leftHand, XRHandJointID.Palm, out pos);
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
