using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Mago sem controle (pedido do Pablo, 2026-10-28), com as mãos LIVRES (rastreadas):
    //   - RAÍZES: as duas mãos juntas encostadas no chão (agachado) 0,3 s → raízes prendem os inimigos num círculo de
    //     4 m à frente por 3 s.
    //   - KAMEHAMEHA: pulsos juntos, mãos abertas na altura do tronco → carrega uma esfera azul entre as palmas;
    //     empurrar as duas mãos para frente com força solta um raio grosso que atravessa todos.
    //   - BOLA DE FOGO (uma mão): punho fechado esticado para frente → abrir a mão carrega a bola na palma (cheia em
    //     1,5 s) → jogar a mão para frente arremessa (antes de encher sai mais fraca).
    // Enquanto carrega, os poderes de uma mão daquela mão ficam desligados (o empurrão não vira corte da palma).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class HandMagic : MonoBehaviour
    {
        public static HandMagic Instance { get; private set; }

        ArenaPlayer player;
        XRHandSubsystem hands;
        static readonly List<XRHandSubsystem> buffer = new List<XRHandSubsystem>();

        sealed class HandData
        {
            public bool Tracked;
            public HandShape Shape;
            public Vector3 Palm, Wrist, PalmLocalPrev, Vel;   // Vel = velocidade da palma relativa ao corpo
            public bool HasPrev;
            // bola de fogo
            public float ArmT, OpenWindow, Charge;
            public bool Armed, Charging;
            public Transform Orb;
        }
        readonly HandData[] h = { new HandData(), new HandData() };

        float rootsT, rootsCd, kameT, kameCd, fireCd;
        bool kameCharging;
        Transform kameOrb;

        // Mão ocupada com magia (carregando bola de fogo ou Kamehameha): os poderes de uma mão ficam desligados.
        public static bool IsBusy(int side) => Instance && (Instance.kameCharging || Instance.h[side].Armed || Instance.h[side].Charging);

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            rootsCd -= dt; kameCd -= dt; fireCd -= dt;
            if (ArenaGame.Blocked || !player.IsVr || player.TreeOpen) { CancelAll(); return; }
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            if (hands == null) { CancelAll(); return; }
            for (int side = 0; side < 2; side++) Track(side, dt);
            bool leftFree = player.Input.LeftHandFree && h[0].Tracked && !SpiderLocomotion.IsBusy(0);
            bool rightFree = player.Input.RightHandFree && h[1].Tracked && !SpiderLocomotion.IsBusy(1);

            if (leftFree && rightFree && TwoHandMagic(dt)) { CancelFire(0); CancelFire(1); return; }
            if (!kameCharging) { HideKameOrb(); }
            if (leftFree) Fireball(0, dt); else CancelFire(0);
            if (rightFree) Fireball(1, dt); else CancelFire(1);
        }

        void Track(int side, float dt)
        {
            var d = h[side];
            var hand = side == 0 ? hands.leftHand : hands.rightHand;
            d.Tracked = hand.isTracked && World(hand, XRHandJointID.Palm, out d.Palm) && World(hand, XRHandJointID.Wrist, out d.Wrist);
            if (!d.Tracked) { d.HasPrev = false; d.Shape = HandShape.Nenhuma; return; }
            d.Shape = Detect(hand);
            var local = player.trackingSpace.InverseTransformPoint(d.Palm);
            if (d.HasPrev && dt > 0f) d.Vel = player.trackingSpace.TransformVector(local - d.PalmLocalPrev) / dt;
            d.PalmLocalPrev = local; d.HasPrev = true;
        }

        // ---------- Duas mãos: raízes e Kamehameha. Retorna true se as duas mãos estão ocupadas com isso. ----------
        bool TwoHandMagic(float dt)
        {
            var L = h[0]; var R = h[1];
            float palmsApart = Vector3.Distance(L.Palm, R.Palm), wristsApart = Vector3.Distance(L.Wrist, R.Wrist);
            float floor = player.FeetPosition.y;
            // Raízes: mãos juntas no chão.
            if (HandMagicRules.RootsPose(palmsApart, Mathf.Min(L.Palm.y, R.Palm.y) - floor, Mathf.Max(L.Palm.y, R.Palm.y) - floor))
            {
                rootsT += dt;
                if (rootsT >= HandMagicRules.RootsHold && rootsCd <= 0f) { CastRoots(); rootsT = -1f; }
                return true;
            }
            rootsT = 0f;
            // Kamehameha: pulsos juntos, mãos abertas na altura do tronco.
            bool openish = L.Shape != HandShape.Punho && R.Shape != HandShape.Punho && L.Shape != HandShape.Apontar && R.Shape != HandShape.Apontar;
            float belowHead = player.HeadPosition.y - (L.Palm.y + R.Palm.y) * 0.5f;
            bool pose = openish && HandMagicRules.KamePose(wristsApart, palmsApart, belowHead);
            var mid = (L.Palm + R.Palm) * 0.5f;
            if (pose && kameCd <= 0f)
            {
                kameCharging = true;
                kameT += dt;
                float c = HandMagicRules.Charge01(kameT, HandMagicRules.KameMinCharge, HandMagicRules.KameFullCharge);
                ShowKameOrb(mid, c);
                // Soltar: as duas mãos empurrando para frente.
                var push = (L.Vel + R.Vel) * 0.5f;
                if (c > 0f && Vector3.Dot(push, player.HeadForward) > HandMagicRules.KamePushSpeed)
                {
                    FireKamehameha(mid, push.normalized, c);
                    kameCharging = false; kameT = 0f;
                }
                return true;
            }
            // Ainda carregado e empurrando (as mãos se separam um pouco no empurrão): solta mesmo assim.
            if (kameCharging)
            {
                float c = HandMagicRules.Charge01(kameT, HandMagicRules.KameMinCharge, HandMagicRules.KameFullCharge);
                var push = (L.Vel + R.Vel) * 0.5f;
                if (c > 0f && Vector3.Dot(push, player.HeadForward) > HandMagicRules.KamePushSpeed && palmsApart < 0.5f)
                {
                    FireKamehameha(mid, push.normalized, c);
                }
                kameCharging = false; kameT = 0f; HideKameOrb();
                return c > 0f;
            }
            return false;
        }

        public void CastRoots()
        {
            var ps = player.State;
            if (!ps.Mana.TrySpend(HandMagicRules.RootsMana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            rootsCd = HandMagicRules.RootsCooldown;
            var center = player.FeetPosition + player.HeadForward * HandMagicRules.RootsDistance;
            center = ArenaUtil.ClampToArena(center); center.y = player.FeetPosition.y;
            ArenaFx.Ring(center, new Color(0.5f, 0.9f, 0.4f), HandMagicRules.RootsRadius, 0.6f);
            ArenaFx.Burst(center + Vector3.up * 0.1f, new Color(0.35f, 0.23f, 0.1f), 40, 4f, 1.6f);
            ArenaAudio.Play(Sfx.Impact, center, 0.8f, 0.8f); ArenaAudio.Play(Sfx.Wind, center, 0.5f, 0.6f);
            // Espinhos de raiz brotando em volta.
            var wood = ArenaUtil.Lit(new Color(0.32f, 0.22f, 0.1f));
            for (int i = 0; i < 14; i++)
            {
                var off = Random.insideUnitCircle * HandMagicRules.RootsRadius;
                var spike = ArenaUtil.Prim(PrimitiveType.Cylinder, null, center + new Vector3(off.x, 0.3f, off.y), new Vector3(0.08f, 0.35f, 0.08f), wood);
                spike.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
                foreach (var c in spike.GetComponentsInChildren<Collider>()) Destroy(c);
                Destroy(spike, HandMagicRules.RootsSeconds);
            }
            int n = 0;
            foreach (var e in ArenaEnemy.All)
            {
                if (e == null || e.Dead) continue;
                if ((e.transform.position - center).Flat().magnitude < HandMagicRules.RootsRadius + e.Radius) { e.State.ApplyRoot(HandMagicRules.RootsSeconds); n++; }
            }
            ArenaGame.Instance?.Message(n > 0 ? $"Raízes! Enraizou {n}" : "Raízes!", 0.8f);
        }

        void ShowKameOrb(Vector3 at, float c)
        {
            if (!kameOrb)
            {
                kameOrb = ArenaUtil.Prim(PrimitiveType.Sphere, null, at, Vector3.one * 0.05f, ArenaUtil.Unlit(new Color(0.75f, 0.95f, 1f))).transform;
                ArenaUtil.Prim(PrimitiveType.Sphere, kameOrb, Vector3.zero, Vector3.one * 1.8f, ArenaUtil.Unlit(new Color(0.2f, 0.6f, 1f, 0.4f)));
                foreach (var col in kameOrb.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            kameOrb.gameObject.SetActive(true);
            kameOrb.position = at;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 30f);
            kameOrb.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.18f, Mathf.Max(c, kameT / HandMagicRules.KameFullCharge * 0.3f)) * pulse;
        }

        void HideKameOrb() { if (kameOrb) kameOrb.gameObject.SetActive(false); }

        public void FireKamehameha(Vector3 origin, Vector3 dir, float charge)
        {
            HideKameOrb();
            var ps = player.State;
            if (!ps.Mana.TrySpend(HandMagicRules.KameMana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            kameCd = HandMagicRules.KameCooldown;
            // Sem desvio para cima/baixo demais: mistura a direção das mãos com o olhar.
            dir = (dir + player.Head.forward).normalized;
            float len = HandMagicRules.KameRange;
            for (float d = 0.5f; d < HandMagicRules.KameRange; d += 0.5f)
            {
                var p = origin + dir * d;
                if (p.y < 0f || ArenaUtil.IsOutside(p)) { len = d; break; }
            }
            var end = origin + dir * len;
            float dmg = HandMagicRules.KameDamage(charge);
            int n = 0;
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (!Geometry.SweptSegmentsHit(origin.ToN(), end.ToN(), origin.ToN(), end.ToN(), a.ToN(), b.ToN(), a.ToN(), b.ToN(), e.Radius + HandMagicRules.KameRadius)) continue;
                e.ApplyDamage(dmg, 1.5f, dir, HandMagicRules.KameKnock, new Color(0.5f, 0.85f, 1f));
                n++;
            }
            // Visual: raio grosso azul que some em meio segundo.
            var beam = new GameObject("Kamehameha");
            beam.transform.position = (origin + end) * 0.5f;
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - origin);
            var core = ArenaUtil.Prim(PrimitiveType.Cylinder, beam.transform, Vector3.zero, new Vector3(0.35f, len * 0.5f, 0.35f), ArenaUtil.Unlit(new Color(0.85f, 0.97f, 1f)));
            var glow = ArenaUtil.Prim(PrimitiveType.Cylinder, beam.transform, Vector3.zero, new Vector3(0.95f, len * 0.5f, 0.95f), ArenaUtil.Unlit(new Color(0.2f, 0.6f, 1f, 0.35f)));
            foreach (var col in beam.GetComponentsInChildren<Collider>()) Destroy(col);
            beam.AddComponent<BeamFade>().Init(0.6f, len);
            ArenaFx.Burst(origin, new Color(0.6f, 0.9f, 1f), 20, 3f, 1f);
            ArenaFx.Burst(end, new Color(0.4f, 0.8f, 1f), 30, 5f, 1.4f);
            ArenaAudio.Play(Sfx.Explosion, origin, 1f, 0.7f);
            ArenaAudio.Play(Sfx.Wind, end, 1f, 0.5f);
            player.Vignette(0.4f);
            ArenaGame.Instance?.Message(n > 0 ? $"KAMEHAMEHA! ({n})" : "KAMEHAMEHA!", 1f);
        }

        // ---------- Bola de fogo (uma mão) ----------
        void Fireball(int side, float dt)
        {
            var d = h[side];
            var eye = player.HeadPosition;
            var fwd = Vector3.Dot(d.Palm - eye, player.HeadForward);
            bool extended = fwd > HandMagicRules.FireExtendForward && Mathf.Abs(d.Palm.y - eye.y) < 0.6f;
            if (d.Charging)
            {
                if (d.Shape == HandShape.Punho || d.Shape == HandShape.Apontar) { CancelFire(side); return; }   // fechou: cancela
                d.Charge += dt;
                float power = HandMagicRules.FirePower(d.Charge);
                ShowOrb(d, power);
                // Arremesso: a mão vai rápido para frente.
                if (power > 0f && Vector3.Dot(d.Vel, player.HeadForward) > HandMagicRules.FireThrowSpeed)
                {
                    ThrowFireball(d.Palm, d.Vel, power);
                    CancelFire(side);
                }
                return;
            }
            if (d.Armed)
            {
                d.OpenWindow -= dt;
                if (d.Shape == HandShape.Aberta && fireCd <= 0f)
                {
                    if (player.State.Mana.Current < HandMagicRules.FireMana) { ArenaGame.Instance?.Message("Sem mana", 0.6f); CancelFire(side); return; }
                    d.Armed = false; d.Charging = true; d.Charge = 0f;
                    ArenaAudio.Play(Sfx.FireCast, d.Palm, 0.5f, 0.8f);
                }
                else if (d.OpenWindow <= 0f || !extended && d.Shape != HandShape.Punho) CancelFire(side);
                return;
            }
            // Punho fechado esticado para frente por um instante = pronto para abrir.
            if (d.Shape == HandShape.Punho && extended && d.Vel.magnitude < 2f) d.ArmT += dt; else d.ArmT = 0f;
            if (d.ArmT >= HandMagicRules.FireArmHold) { d.Armed = true; d.OpenWindow = HandMagicRules.FireOpenWindow; d.ArmT = 0f; }
        }

        void ShowOrb(HandData d, float power)
        {
            if (!d.Orb)
            {
                d.Orb = ArenaUtil.Prim(PrimitiveType.Sphere, null, d.Palm, Vector3.one * 0.05f, ArenaUtil.Unlit(new Color(1f, 0.55f, 0.15f))).transform;
                ArenaUtil.Prim(PrimitiveType.Sphere, d.Orb, Vector3.zero, Vector3.one * 1.7f, ArenaUtil.Unlit(new Color(1f, 0.3f, 0.05f, 0.35f)));
                foreach (var col in d.Orb.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            d.Orb.gameObject.SetActive(true);
            // Flutua um pouco acima da palma.
            d.Orb.position = d.Palm + Vector3.up * 0.08f;
            float grow = Mathf.Max(power, d.Charge / HandMagicRules.FireFullCharge * 0.4f);
            d.Orb.localScale = Vector3.one * Mathf.Lerp(0.04f, 0.2f, grow) * (1f + 0.1f * Mathf.Sin(Time.time * 25f));
        }

        public void ThrowFireball(Vector3 from, Vector3 handVel, float power)
        {
            var ps = player.State;
            if (!ps.Mana.TrySpend(HandMagicRules.FireMana, 0.6f)) return;
            fireCd = HandMagicRules.FireCooldown;
            var dir = handVel.sqrMagnitude > 0.01f ? handVel.normalized : player.Head.forward;
            var vel = dir * Mathf.Max(12f, handVel.magnitude * 2.2f);
            float size = 0.12f + 0.1f * power;
            var vis = ArenaUtil.Prim(PrimitiveType.Sphere, null, from, Vector3.one * size * 2f, ArenaUtil.Unlit(new Color(1f, 0.48f, 0.13f)));
            foreach (var col in vis.GetComponentsInChildren<Collider>()) Destroy(col);
            var p = Projectile.Spawn(vis, from, vel);
            p.Gravity = 3f; p.Life = 2.5f; p.Radius = size; p.Pierce = 1;
            float radius = Combat.FireballRadius(power, ps.Has(SkillIds.Combustao));
            float dmg = Combat.FireballDamage(power);
            p.OnEnd = pr => SkillCaster.Explode(new Vector3(pr.transform.position.x, Mathf.Max(0.3f, pr.transform.position.y), pr.transform.position.z), radius, dmg);
            ArenaAudio.Play(Sfx.FireCast, from, 0.9f);
        }

        void CancelFire(int side)
        {
            var d = h[side];
            d.Armed = d.Charging = false; d.ArmT = 0f; d.Charge = 0f;
            if (d.Orb) d.Orb.gameObject.SetActive(false);
        }

        void CancelAll()
        {
            CancelFire(0); CancelFire(1);
            kameCharging = false; kameT = 0f; rootsT = 0f;
            HideKameOrb();
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

        HandShape Detect(XRHand hd)
        {
            var i = Extended(hd, XRHandJointID.IndexProximal, XRHandJointID.IndexTip);
            var m = Extended(hd, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip);
            var r = Extended(hd, XRHandJointID.RingProximal, XRHandJointID.RingTip);
            var l = Extended(hd, XRHandJointID.LittleProximal, XRHandJointID.LittleTip);
            if (i == null || m == null || r == null || l == null) return HandShape.Nenhuma;
            return HandSigns.Classify(i.Value, m.Value, r.Value, l.Value);
        }
    }

    // Raio que encolhe e some.
    public sealed class BeamFade : MonoBehaviour
    {
        float t, life, len;
        public void Init(float seconds, float length) { life = seconds; len = length; }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(1f - t / life);
            foreach (Transform c in transform) { var s = c.localScale; c.localScale = new Vector3(s.x * (0.94f + 0.06f * k), len * 0.5f, s.z * (0.94f + 0.06f * k)); }
            if (t >= life) Destroy(gameObject);
        }
    }
}
