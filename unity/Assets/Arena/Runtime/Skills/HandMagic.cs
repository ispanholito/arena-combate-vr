using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Arena
{
    // Mago sem controle (pedido do Pablo, 2026-10-28), com as mãos LIVRES (rastreadas):
    //   As magias de duas mãos começam com uma PALMA BATIDA (abre uma janela de 1,5 s):
    //   - RAÍZES: depois da palma, separe e jogue as duas mãos rápido para baixo → raízes prendem os inimigos num
    //     círculo de 4 m à frente por 3 s.
    //   - KAMEHAMEHA: depois da palma, mantenha os pulsos juntos na altura do tronco → carrega uma esfera azul;
    //     empurrar as duas mãos para frente com força solta um raio grosso que atravessa todos.
    //   - BOLA DE FOGO (uma mão): punho fechado esticado para frente → abrir a mão carrega a bola na palma (cheia em
    //     1,5 s) → jogar a mão para frente arremessa. É TELEGUIADA: vai no inimigo mais perto da direção do arremesso.
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

        float rootsCd, kameT, kameCd, fireCd, clapWindow, clapCd, prevPalmsApart = 1f;
        bool kameCharging;
        public bool KameCharging => kameCharging;   // a IA sai da frente
        Transform kameOrb;


        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            rootsCd -= dt; kameCd -= dt; fireCd -= dt;
            if (ArenaGame.Blocked || !player.IsVr || player.TreeOpen) { CancelAll(); ctrlCharging = false; return; }
            ControllerFire(dt);
            if (hands == null || !hands.running)
            {
                SubsystemManager.GetSubsystems(buffer);
                hands = null;
                foreach (var s in buffer) if (s.running) { hands = s; break; }
            }
            if (hands == null) { if (!ctrlCharging) CancelAll(); return; }   // sem rastreamento: a bola do controle continua
            for (int side = 0; side < 2; side++) Track(side, dt);
            bool leftFree = player.Input.LeftHandFree && h[0].Tracked;
            bool rightFree = player.Input.RightHandFree && h[1].Tracked;

            // Trava de mão (HandLock): duas mãos só com as duas livres; bola de fogo só na mão livre para ela.
            if (Muralha.Active) { MuralhaClap(dt); CancelFire(0); CancelFire(1); HideKameOrb(); return; }   // na Muralha: só soco e ondas
            if (!DemoGate.Hidden && leftFree && rightFree && HandLock.BothFree("duas") && TwoHandMagic(dt)) { CancelFire(0); CancelFire(1); return; }
            if (kameCharging) { kameCharging = false; kameT = 0f; }
            HideKameOrb();
            bool fireOk = DemoGate.FireAllowed;   // demo: só depois da estátua
            if (fireOk && leftFree && HandLock.Free(0, "fogo")) Fireball(0, dt); else if (!ctrlCharging) CancelFire(0);
            if (fireOk && rightFree && HandLock.Free(1, "fogo")) Fireball(1, dt); else CancelFire(1);
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

        // Mão rastreada para os outros poderes (Muralha, raio do céu): palma, velocidade relativa ao corpo e formato.
        public bool GetHand(int side, out Vector3 palm, out Vector3 vel, out HandShape shape)
        {
            var d = h[side];
            palm = d.Palm; vel = d.Vel; shape = d.Shape;
            return d.Tracked;
        }

        // Muralha: palma batida (as duas abertas) = onda de choque para frente.
        void MuralhaClap(float dt)
        {
            var L = h[0]; var R = h[1];
            if (!L.Tracked || !R.Tracked || !player.Input.LeftHandFree || !player.Input.RightHandFree) return;
            float palmsApart = Vector3.Distance(L.Palm, R.Palm);
            float closing = dt > 0f ? (prevPalmsApart - palmsApart) / dt : 0f;
            prevPalmsApart = palmsApart;
            clapCd -= dt;
            if (clapCd > 0f || L.Shape == HandShape.Punho || R.Shape == HandShape.Punho || !HandMagicRules.IsClap(palmsApart, closing)) return;
            clapCd = HandMagicRules.ClapCooldown;
            Muralha.Instance.ClapWave((L.Palm + R.Palm) * 0.5f);
        }

        // ---------- Duas mãos: raízes e Kamehameha. Retorna true se as duas mãos estão ocupadas com isso. ----------
        bool TwoHandMagic(float dt)
        {
            var L = h[0]; var R = h[1];
            float palmsApart = Vector3.Distance(L.Palm, R.Palm), wristsApart = Vector3.Distance(L.Wrist, R.Wrist);
            float closing = dt > 0f ? (prevPalmsApart - palmsApart) / dt : 0f;
            prevPalmsApart = palmsApart;
            clapWindow -= dt; clapCd -= dt;
            // Palma batida: abre a janela das magias de duas mãos.
            if (clapCd <= 0f && L.Shape != HandShape.Punho && R.Shape != HandShape.Punho && HandMagicRules.IsClap(palmsApart, closing))   // punho na palma é a Muralha
            {
                clapWindow = HandMagicRules.ClapWindow; clapCd = HandMagicRules.ClapCooldown;
                var at = (L.Palm + R.Palm) * 0.5f;
                ArenaAudio.Play(Sfx.Block, at, 0.5f, 1.8f);
                ArenaFx.Burst(at, new Color(0.8f, 0.95f, 1f), 8, 1.2f, 0.4f);
                ArenaInput.Haptic(0.3f, 0.05f);
                TutorialEvents.Raise("palma_batida");
            }
            if (clapWindow <= 0f && !kameCharging) return false;
            HandLock.HoldBoth("duas");   // palma batida: as duas mãos ficam com as magias de duas mãos
            // Raízes: separou e jogou as duas mãos para baixo.
            if (!kameCharging && HandMagicRules.RootsSlam(palmsApart, L.Vel.y, R.Vel.y))
            {
                if (rootsCd <= 0f) CastRoots(); else ArenaGame.Instance?.Message("Raízes em recarga", 0.6f);
                HandLock.HoldBoth("duas", HandLock.AfterCast);
                clapWindow = 0f;
                return true;
            }
            // Kamehameha: pulsos juntos, mãos abertas na altura do tronco.
            bool openish = L.Shape != HandShape.Punho && R.Shape != HandShape.Punho && L.Shape != HandShape.Apontar && R.Shape != HandShape.Apontar;
            float belowHead = player.HeadPosition.y - (L.Palm.y + R.Palm.y) * 0.5f;
            bool pose = openish && HandMagicRules.KamePose(wristsApart, palmsApart, belowHead);
            var mid = (L.Palm + R.Palm) * 0.5f;
            if (pose && kameCd <= 0f)
            {
                kameCharging = true;
                clapWindow = Mathf.Max(clapWindow, 0.3f);
                kameT += dt;
                float c = HandMagicRules.Charge01(kameT, HandMagicRules.KameMinCharge, HandMagicRules.KameFullCharge);
                ShowKameOrb(mid, c);
                // Soltar: as duas mãos empurrando para frente.
                var push = (L.Vel + R.Vel) * 0.5f;
                if (c > 0f && Vector3.Dot(push, player.HeadForward) > HandMagicRules.KamePushSpeed)
                {
                    FireKamehameha(mid, push.normalized, c);
                    HandLock.HoldBoth("duas", HandLock.AfterCast);
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
                    HandLock.HoldBoth("duas", HandLock.AfterCast);
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
            TutorialEvents.Raise("raizes");
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

        void ShowKameOrb(Vector3 at, float c, bool armed = false)
        {
            if (!kameOrb)
            {
                kameOrb = ArenaUtil.Prim(PrimitiveType.Sphere, null, at, Vector3.one * 0.05f, ArenaUtil.Unlit(new Color(0.75f, 0.95f, 1f))).transform;
                ArenaUtil.Prim(PrimitiveType.Sphere, kameOrb, Vector3.zero, Vector3.one * 1.8f, ArenaUtil.Unlit(new Color(0.2f, 0.6f, 1f, 0.4f)));
                foreach (var col in kameOrb.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            kameOrb.gameObject.SetActive(true);
            kameOrb.position = at;
            float pulse = 1f + (armed ? 0.25f : 0.12f) * Mathf.Sin(Time.time * (armed ? 45f : 30f));   // armado: pulsa forte
            kameOrb.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.18f, Mathf.Max(c, kameT / HandMagicRules.KameFullCharge * 0.3f)) * pulse;
        }

        void HideKameOrb() { if (kameOrb) kameOrb.gameObject.SetActive(false); }

        public void FireKamehameha(Vector3 origin, Vector3 dir, float charge)
        {
            HideKameOrb();
            var ps = player.State;
            if (!ps.Mana.TrySpend(HandMagicRules.KameMana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            TutorialEvents.Raise("kame");
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

        // ---------- Bola de fogo pelo CONTROLE esquerdo (Pablo, 2026-10-31) ----------
        // Segurar o clique do analógico esquerdo: a bola cresce na frente do controle (cheia em 1,5 s). Jogar a mão para
        // frente arremessa; soltar o botão também solta (com a mão parada, vai para onde o controle aponta). A espada pode
        // ser mergulhada nela como na da mão livre.
        bool ctrlCharging;
        Vector3 ctrlPrev;

        void ControllerFire(float dt)
        {
            var I = player.Input;
            var d = h[0];
            var hand = player.leftHand;
            var palm = hand.position + hand.forward * 0.07f + hand.up * 0.03f;
            var local = player.trackingSpace.InverseTransformPoint(palm);
            var vel = dt > 0f ? player.trackingSpace.TransformDirection((local - ctrlPrev) / dt) : Vector3.zero;
            ctrlPrev = local;
            if (!I.LeftControllerConnected || !DemoGate.FireAllowed) { if (ctrlCharging) { ctrlCharging = false; CancelFire(0); } return; }
            if (I.LeftStickHeld)
            {
                if (!ctrlCharging)
                {
                    if (fireCd > 0f) return;
                    if (player.State.Mana.Current < HandMagicRules.FireMana) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
                    ctrlCharging = true; d.Armed = false; d.Charging = true; d.Charge = 0f;
                    ArenaAudio.Play(Sfx.FireCast, palm, 0.5f, 0.8f);
                }
                if (!d.Charging) { ctrlCharging = false; return; }   // a espada consumiu a bola (mergulhou)
                d.Palm = palm;
                d.Charge += dt;
                float power = HandMagicRules.FirePower(d.Charge);
                ShowOrb(d, power);
                if (power > 0f && Vector3.Dot(vel, player.HeadForward) > HandMagicRules.FireThrowSpeed)
                {
                    ThrowFireball(palm, vel, power);
                    ctrlCharging = false; CancelFire(0);
                }
                return;
            }
            if (!ctrlCharging) return;
            float pw = HandMagicRules.FirePower(d.Charge);
            if (pw > 0f && d.Charging) ThrowFireball(palm, vel.magnitude > 1.5f ? vel : hand.forward * 6f, pw);
            ctrlCharging = false; CancelFire(0);
        }

        // ---------- Bola de fogo (uma mão) ----------
        void Fireball(int side, float dt)
        {
            var d = h[side];
            if (d.Armed || d.Charging) HandLock.Hold(side, "fogo");
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
                    HandLock.Hold(side, "fogo", HandLock.AfterCast);   // o arremesso não vira corte da palma
                    CancelFire(side);
                }
                return;
            }
            // Mirando um ponto de teia aceso: a mão é da teia, não da bola de fogo.
            bool aimingWeb = SpiderLocomotion.Instance && SpiderLocomotion.Instance.AimsAtLit(d.Palm);
            if (d.Armed)
            {
                d.OpenWindow -= dt;
                if (aimingWeb) { CancelFire(side); return; }
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
            if (d.Shape == HandShape.Punho && extended && d.Vel.magnitude < 2f && !aimingWeb) d.ArmT += dt; else d.ArmT = 0f;
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
            TutorialEvents.Raise("bola");
            fireCd = HandMagicRules.FireCooldown;
            var dir = handVel.sqrMagnitude > 0.01f ? handVel.normalized : player.Head.forward;
            var vel = dir * Mathf.Max(12f, handVel.magnitude * 2.2f);
            float size = 0.12f + 0.1f * power;
            var vis = ArenaUtil.Prim(PrimitiveType.Sphere, null, from, Vector3.one * size * 2f, ArenaUtil.Unlit(new Color(1f, 0.48f, 0.13f)));
            foreach (var col in vis.GetComponentsInChildren<Collider>()) Destroy(col);
            var p = Projectile.Spawn(vis, from, vel);
            p.Gravity = 3f; p.Life = 2.5f; p.Radius = size; p.Pierce = 1; p.IsFire = true;
            float radius = Combat.FireballRadius(power, ps.Has(SkillIds.Combustao));
            float dmg = Combat.FireballDamage(power);
            p.OnEnd = pr => SkillCaster.Explode(new Vector3(pr.transform.position.x, Mathf.Max(0.3f, pr.transform.position.y), pr.transform.position.z), radius, dmg);
            // Teleguiada: o inimigo mais perto da DIREÇÃO do arremesso (um arqueiro longe na mira vence o goblin do lado).
            var target = PickHomingTarget(from, dir);
            if (target)
            {
                p.Gravity = 0f; p.Life = 4f;
                vis.AddComponent<HomingProjectile>().Init(p, target);
            }
            ArenaAudio.Play(Sfx.FireCast, from, 0.9f);
        }

        public static ArenaEnemy PickHomingTarget(Vector3 from, Vector3 dir)
        {
            var list = new List<ArenaEnemy>();
            foreach (var e in ArenaEnemy.All) if (e && !e.Dead) list.Add(e);
            var angles = new float[list.Count]; var dists = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var to = HomingProjectile.AimPoint(list[i]) - from;
                angles[i] = Vector3.Angle(dir, to); dists[i] = to.magnitude;
            }
            int k = HandMagicRules.PickHomingTarget(angles, dists);
            return k >= 0 ? list[k] : null;
        }

        // Espada flamejante: a lâmina (a→b) passou pela bola que alguma mão está carregando? Consome a bola (gasta a mana
        // dela) e devolve true.
        public bool TryConsumeFireOrb(Vector3 a, Vector3 b, float margin)
        {
            for (int side = 0; side < 2; side++)
            {
                var d = h[side];
                if (!d.Charging || !d.Orb || !d.Orb.gameObject.activeSelf) continue;
                var ab = b - a; var o = d.Orb.position;
                float t = Mathf.Clamp01(Vector3.Dot(o - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                if (Vector3.Distance(o, a + ab * t) > d.Orb.lossyScale.x * 0.85f + margin) continue;
                if (!player.State.Mana.TrySpend(HandMagicRules.FireMana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return false; }
                CancelFire(side);
                return true;
            }
            return false;
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
            kameCharging = false; kameT = 0f; clapWindow = 0f;
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

    // Vira a velocidade do projétil em direção ao alvo (até HomingTurn graus/s). Alvo morto: segue reto.
    public sealed class HomingProjectile : MonoBehaviour
    {
        Projectile p; ArenaEnemy target;
        public void Init(Projectile projectile, ArenaEnemy enemy) { p = projectile; target = enemy; }
        public static Vector3 AimPoint(ArenaEnemy e) { e.BodyAxis(out var a, out var b); return (a + b) * 0.5f; }
        void Update()
        {
            if (!p || !target || target.Dead) { enabled = false; return; }
            var want = AimPoint(target) - transform.position;
            float speed = p.Velocity.magnitude;
            var dir = Vector3.RotateTowards(p.Velocity.normalized, want.normalized, HandMagicRules.HomingTurn * Mathf.Deg2Rad * Time.deltaTime, 0f);
            p.Velocity = dir * speed;
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
