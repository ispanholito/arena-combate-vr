using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using N = System.Numerics;

namespace Arena
{
    // MURALHA IMPENETRÁVEL (ideia do Pablo, 2026-10-28). Mãos livres: bater o PUNHO de uma mão na PALMA aberta da outra →
    // aura vermelha por 20 s (30 de mana, recarga 15 s). Nela:
    //   - só sai SOCO (raio, corte da palma, bola de fogo, raízes, onda, teia e agarrar ficam desligados), muito mais
    //     forte: pequenos voam longe, grandes (orc, chefe, mímico) atordoam bem mais rápido (postura ×3);
    //   - as mãos DEFENDEM: golpe que chega perto de uma mão = bloqueio (sem dano, um pouco de estamina); mão indo
    //     rápido contra o golpe = APARO (o atacante fica atordoado);
    //   - PALMA batida = onda de choque para frente; PUNHO na palma de novo = onda em volta de você. Tudo dá dano.
    // Regras e números em Core/HandArts.cs (MuralhaRules).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class Muralha : MonoBehaviour
    {
        public static Muralha Instance { get; private set; }
        public static bool Active => Instance && Instance.activeT > 0f;

        ArenaPlayer player;
        float activeT, cd, waveCd, prevDist = 1f, sparkT;
        Transform ring;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (ring) Destroy(ring.gameObject); HandVisual.Tint(null); }

        void Update()
        {
            float dt = Time.deltaTime;
            cd -= dt;
            if (DemoGate.Hidden) return;   // fora da demo waveCd -= dt;
            if (DemoGate.Hidden) return;   // fora da demo
            if (activeT > 0f) { activeT -= dt; if (activeT <= 0f) End(); else Aura(); }
            var hm = HandMagic.Instance;
            if (!player.IsVr || ArenaGame.Blocked || !hm || !player.Input.LeftHandFree || !player.Input.RightHandFree) return;
            if (!hm.GetHand(0, out var lp, out _, out var ls) || !hm.GetHand(1, out var rp, out _, out var rs)) return;
            float dist = Vector3.Distance(lp, rp);
            float closing = dt > 0f ? (prevDist - dist) / dt : 0f;
            prevDist = dist;
            if (!MuralhaRules.IsFistPalm(ls == HandShape.Punho, ls == HandShape.Aberta, rs == HandShape.Punho, rs == HandShape.Aberta, dist, closing)) return;
            if (Active) { if (waveCd <= 0f) RingWave(); return; }
            if (cd > 0f) { ArenaGame.Instance?.Message("Muralha em recarga", 0.6f); return; }
            Activate();
        }

        public void Activate()
        {
            if (!player.State.Mana.TrySpend(MuralhaRules.Mana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            activeT = MuralhaRules.Seconds; cd = MuralhaRules.Cooldown + MuralhaRules.Seconds; waveCd = 0.6f;
            HandVisual.Tint(new Color(1f, 0.25f, 0.2f));
            ArenaAudio.Play(Sfx.Slam, player.FeetPosition, 1f, 0.8f);
            ArenaFx.Ring(player.FeetPosition, new Color(1f, 0.2f, 0.15f), 1.6f, 0.5f);
            ArenaFx.Burst(player.HeadPosition - Vector3.up * 0.5f, new Color(1f, 0.25f, 0.15f), 30, 3f, 1f);
            ArenaInput.Haptic(0.8f, 0.15f);
            player.Vignette(0.4f);
            ArenaGame.Instance?.Message("MURALHA IMPENETRÁVEL!", 1.5f);
            TutorialEvents.Raise("muralha");
        }

        void End()
        {
            activeT = 0f;
            if (ring) ring.gameObject.SetActive(false);
            HandVisual.Tint(null);
            ArenaGame.Instance?.Message("A Muralha se desfez", 1f);
        }

        // Aura: anel vermelho nos pés e brasas subindo em volta do corpo (não cobre a visão).
        void Aura()
        {
            if (!ring)
            {
                ring = ArenaUtil.Prim(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(1.4f, 0.01f, 1.4f), ArenaUtil.Unlit(new Color(1f, 0.15f, 0.1f, 0.35f))).transform;
                foreach (var c in ring.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            ring.gameObject.SetActive(true);
            ring.position = player.FeetPosition + Vector3.up * 0.02f;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
            ring.localScale = new Vector3(1.4f * pulse, 0.01f, 1.4f * pulse);
            sparkT -= Time.deltaTime;
            if (sparkT <= 0f)
            {
                sparkT = 0.12f;
                var off = Random.insideUnitCircle.normalized * 0.55f;
                ArenaFx.Burst(player.FeetPosition + new Vector3(off.x, Random.Range(0.2f, 1.4f), off.y), new Color(1f, 0.2f, 0.12f), 3, 0.8f, 0.5f);
            }
        }

        // Soco na Muralha (chamado pelo soco das mãos).
        public static void EmpowerPunch(ArenaEnemy target, Vector3 dir)
        {
            if (!Active || !target || target.Dead) return;
            if (MuralhaRules.IsBig(target.Def.Kind))
            {
                target.ApplyDamage(0f, HandPowers.PunchPosture * (MuralhaRules.PunchPostureMultiplier - 1f), dir, 0f, new Color(1f, 0.3f, 0.2f));
                return;
            }
            var launch = dir.Flat().normalized * MuralhaRules.LaunchSpeed + Vector3.up * MuralhaRules.LaunchUp;
            if (target is PhysicalGoblinEnemy pg && pg.Body) pg.Body.OnThrown(launch);
            else target.PhysicalImpact(MuralhaRules.LaunchSpeed * 3f, launch.normalized);
        }

        // Golpe chegando no jogador vindo de 'from': uma mão no caminho bloqueia; indo rápido contra ele, apara.
        public static bool TryGuard(Vector3 from)
        {
            if (!Active) return false;
            var me = Instance; var pl = me.player; var hm = HandMagic.Instance;
            if (!hm) return false;
            var chest = pl.HeadPosition - Vector3.up * 0.35f;
            var toHit = from - chest; toHit.y *= 0.5f;
            var dir = toHit.sqrMagnitude > 1e-4f ? toHit.normalized : pl.HeadForward;
            var hitPoint = chest + dir * 0.45f;   // onde o golpe chega na frente do corpo
            var best = MuralhaRules.Guard.None; Vector3 at = hitPoint;
            for (int side = 0; side < 2; side++)
            {
                if (!hm.GetHand(side, out var palm, out var vel, out _)) continue;
                var g = MuralhaRules.ResolveGuard(Vector3.Distance(palm, hitPoint), Vector3.Dot(vel, dir));
                if (g > best) { best = g; at = palm; }
            }
            if (best == MuralhaRules.Guard.None) return false;
            if (best == MuralhaRules.Guard.Parry)
            {
                ArenaEnemy attacker = null; float bd = 1.5f;
                foreach (var e in ArenaEnemy.All) if (e && !e.Dead) { float d = Vector3.Distance(e.Center, from); if (d < bd) { bd = d; attacker = e; } }
                if (attacker) attacker.State.ApplyStun(MuralhaRules.ParryStun);
                ArenaFx.FloatText("APARADO", at + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.4f), 0.9f);
                ArenaAudio.Play(Sfx.Parry, at, 1f);
            }
            else
            {
                pl.State.Stamina.TrySpend(MuralhaRules.GuardStamina, 1f);
                ArenaFx.FloatText("BLOQUEIO", at + Vector3.up * 0.2f, new Color(1f, 0.7f, 0.6f), 0.8f);
                ArenaAudio.Play(Sfx.Block, at, 0.9f);
            }
            ArenaFx.Burst(at, new Color(1f, 0.3f, 0.2f), 10, 2f, 0.6f);
            return true;
        }

        // Palma batida na Muralha: onda de choque para frente (cone).
        public void ClapWave(Vector3 at)
        {
            if (waveCd > 0f) return;
            waveCd = MuralhaRules.WaveCooldown;
            var fwd = player.HeadForward;
            var origin = player.FeetPosition;
            ArenaFx.Ring(origin + fwd * 2f, new Color(1f, 0.3f, 0.2f), 2.5f, 0.35f);
            ArenaFx.Burst(at, new Color(1f, 0.35f, 0.2f), 25, 6f, 0.8f);
            ArenaAudio.Play(Sfx.Slam, at, 1f, 1.2f);
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var to = (e.transform.position - origin).Flat();
                if (!MuralhaRules.InCone(new N.Vector2(to.x, to.z), new N.Vector2(fwd.x, fwd.z), MuralhaRules.ClapConeDeg, MuralhaRules.ClapRange)) continue;
                Push(e, to.normalized, MuralhaRules.ClapDamage, MuralhaRules.ClapKnock);
            }
            player.Vignette(0.3f);
        }

        // Punho na palma de novo: onda em volta.
        void RingWave()
        {
            waveCd = MuralhaRules.WaveCooldown;
            var origin = player.FeetPosition;
            ArenaFx.Ring(origin, new Color(1f, 0.25f, 0.15f), MuralhaRules.RingRadius, 0.45f);
            ArenaAudio.Play(Sfx.Slam, origin, 1f, 0.7f);
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var to = (e.transform.position - origin).Flat();
                if (to.magnitude > MuralhaRules.RingRadius + e.Radius) continue;
                Push(e, to.sqrMagnitude > 1e-4f ? to.normalized : player.HeadForward, MuralhaRules.RingDamage, MuralhaRules.RingKnock);
            }
            player.Vignette(0.35f);
            ArenaInput.Haptic(0.8f, 0.12f);
        }

        static void Push(ArenaEnemy e, Vector3 dir, float dmg, float knock)
        {
            e.ApplyDamage(dmg, 1.2f, dir, knock, new Color(1f, 0.35f, 0.25f));
            if (MuralhaRules.IsBig(e.Def.Kind)) return;
            var launch = dir * knock + Vector3.up * 2f;
            if (e is PhysicalGoblinEnemy pg && pg.Body) pg.Body.OnThrown(launch);
            else e.PhysicalImpact(knock * 2f, launch.normalized);
        }
    }

    // RAIO DO CÉU (ideia do Pablo, 2026-10-28): olhando para cima, erga uma mão acima da cabeça FECHADA e ABRA → um raio
    // desce do céu e fica na mão (6 s; 30 de mana, recarga 8 s). Estique o braço para frente → descarga no inimigo mais
    // perto da mira, que pula para os próximos por perto (até 4 saltos de 4 m). Põe carga elétrica (eletrocuta).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SkyBolt : MonoBehaviour
    {
        public static SkyBolt Instance { get; private set; }
        public static bool Holding(int side) => Instance && Instance.heldT > 0f && Instance.heldSide == side;

        ArenaPlayer player;
        float cd, heldT, aimT, sparkT;
        int heldSide = -1;
        readonly bool[] fistUp = new bool[2];

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            cd -= dt;
            if (DemoGate.Hidden) return;   // fora da demo
            var hm = HandMagic.Instance;
            if (!player.IsVr || ArenaGame.Blocked || !hm || Muralha.Active) { heldT = 0f; return; }
            if (heldT > 0f) { Held(hm, dt); return; }
            bool lookingUp = player.Head.forward.y >= Mathf.Sin(SkyBoltRules.LookUpMinDeg * Mathf.Deg2Rad);
            for (int side = 0; side < 2; side++)
            {
                bool free = side == 0 ? player.Input.LeftHandFree : player.Input.RightHandFree;
                if (!free || !hm.GetHand(side, out var palm, out _, out var shape)) { fistUp[side] = false; continue; }
                bool raised = lookingUp && palm.y > player.HeadPosition.y + SkyBoltRules.HandAboveHead;
                if (!raised) { fistUp[side] = false; continue; }
                if (shape == HandShape.Punho) fistUp[side] = true;
                else if (shape == HandShape.Aberta && fistUp[side] && HandLock.Free(side, "raio_ceu")) { fistUp[side] = false; Summon(side, palm); return; }
            }
        }

        void Summon(int side, Vector3 palm)
        {
            if (cd > 0f) { ArenaGame.Instance?.Message("Raio em recarga", 0.6f); return; }
            if (!player.State.Mana.TrySpend(SkyBoltRules.Mana, 0.6f)) { ArenaGame.Instance?.Message("Sem mana", 0.6f); return; }
            heldT = SkyBoltRules.HeldSeconds; heldSide = side; aimT = 0f;
            Bolt(palm + Vector3.up * 14f, palm, 0.3f, 0.08f);
            ArenaAudio.Play(Sfx.Explosion, palm, 0.9f, 1.6f);
            ArenaFx.Burst(palm, new Color(0.7f, 0.85f, 1f), 18, 3f, 0.6f);
            player.Vignette(0.3f);
            ArenaGame.Instance?.Message("Raio na mão! Aponte para frente", 1.2f);
            TutorialEvents.Raise("raio_ceu_mao");
        }

        void Held(HandMagic hm, float dt)
        {
            heldT -= dt;
            HandLock.Hold(heldSide, "raio_ceu");
            if (!hm.GetHand(heldSide, out var palm, out _, out _)) return;
            sparkT -= dt;
            if (sparkT <= 0f) { sparkT = 0.08f; ArenaFx.Burst(palm, new Color(0.65f, 0.85f, 1f), 2, 1.2f, 0.25f); }
            if (heldT <= 0f) { ArenaFx.Burst(palm, new Color(0.6f, 0.7f, 1f), 10, 2f, 0.4f); ArenaGame.Instance?.Message("O raio se dissipou", 0.8f); return; }
            // Braço esticado para frente = solta.
            var arm = palm - player.HeadPosition;
            bool aimed = arm.magnitude >= SkyBoltRules.AimMinReach && Vector3.Angle(arm, player.Head.forward) <= SkyBoltRules.AimMaxAngle && palm.y < player.HeadPosition.y + 0.1f;
            aimT = aimed ? aimT + dt : 0f;
            if (aimT >= SkyBoltRules.AimHold) Fire(palm, arm.normalized);
        }

        void Fire(Vector3 palm, Vector3 dir)
        {
            heldT = 0f; cd = SkyBoltRules.Cooldown;
            HandLock.Hold(heldSide, "raio_ceu", HandLock.AfterCast);
            var list = new List<ArenaEnemy>();
            foreach (var e in ArenaEnemy.All) if (e && !e.Dead) list.Add(e);
            int first = -1; float bestAng = SkyBoltRules.FirstConeDeg;
            for (int i = 0; i < list.Count; i++)
            {
                var to = list[i].Center - palm;
                if (to.magnitude > SkyBoltRules.Range) continue;
                float a = Vector3.Angle(dir, to);
                if (a < bestAng) { bestAng = a; first = i; }
            }
            ArenaAudio.Play(Sfx.Explosion, palm, 1f, 1.8f);
            player.Vignette(0.3f);
            if (first < 0) { Bolt(palm, palm + dir * 12f, 0.25f, 0.05f); return; }
            var pos = new List<N.Vector3>();
            foreach (var e in list) pos.Add(e.Center.ToN());
            var order = SkyBoltRules.ChainTargets(pos, first);
            var from = palm;
            for (int hop = 0; hop < order.Count; hop++)
            {
                var e = list[order[hop]];
                Bolt(from, e.Center, 0.3f, hop == 0 ? 0.06f : 0.04f);
                e.ApplyDamage(SkyBoltRules.HopDamage(hop), 0.6f, (e.Center - from).normalized, 1.5f, new Color(0.6f, 0.85f, 1f));
                e.State.Status.AddCharge(SkyBoltRules.Charge);
                ArenaFx.Burst(e.Center, new Color(0.7f, 0.85f, 1f), 12, 2.5f, 0.5f);
                from = e.Center;
            }
            ArenaGame.Instance?.Message(order.Count > 1 ? $"RAIO EM CADEIA! ({order.Count})" : "RAIO!", 1f);
        }

        // Relâmpago em zigue-zague que some rápido.
        static void Bolt(Vector3 a, Vector3 b, float seconds, float width)
        {
            var lr = new GameObject("Relampago").AddComponent<LineRenderer>();
            lr.material = ArenaUtil.Unlit(new Color(0.85f, 0.93f, 1f));
            lr.widthMultiplier = width; lr.numCapVertices = 2;
            int n = 9; lr.positionCount = n;
            var side = Vector3.Cross(b - a, Vector3.up).normalized; if (side.sqrMagnitude < 0.1f) side = Vector3.right;
            float len = Vector3.Distance(a, b);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var jitter = (i == 0 || i == n - 1) ? Vector3.zero : (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * len * 0.05f;
                lr.SetPosition(i, Vector3.Lerp(a, b, t) + jitter);
            }
            Destroy(lr.gameObject, seconds);
        }
    }
}
