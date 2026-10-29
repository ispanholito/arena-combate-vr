using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Círculo de aviso no chão (vermelho), usado pelos chefes.
    public static class BossMarker
    {
        public static Transform Make(Vector3 at, float radius, Color? color = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = ArenaUtil.Unlit(color ?? new Color(1f, 0.16f, 0.06f, 0.4f));
            go.transform.position = new Vector3(at.x, 0.03f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
            return go.transform;
        }

        public static void Pulse(Transform m, float t)
        {
            if (!m) return;
            var mat = m.GetComponent<Renderer>().sharedMaterial;
            var c = mat.color; c.a = 0.25f + 0.5f * Mathf.Clamp01(t); mat.color = c;
        }
    }

    // ===================== Rainha Slime =====================
    // Pula e esmaga onde você estava (círculo), encostar dói, divide-se em 66% e 33% (3 slimes) e fica menor e
    // mais rápida. Depois de cair fica parada um instante: é a hora de bater.
    public sealed class SlimeQueen : ArenaEnemy, IBoss
    {
        public int Phase { get; private set; } = 1;
        enum Act { Hop, Windup, Air, Rest }
        Act act;
        float actT, jumpCd = 2f, contactCd, hopT, vy, y, scaleK = 1f, speedK = 1f;
        int splits;
        Vector3 jumpFrom, jumpTo;
        Transform body, marker;

        protected override float IncomingDamageMultiplier => BossRules.DamageTakenMultiplier(State.Posture.IsBroken);
        protected override float IncomingPostureMultiplier => BossRules.HitPostureMultiplier;
        protected override bool LeavesCorpse => false;

        protected override void BuildModel()
        {
            float r = Def.Radius;
            bodyBottom = r * 0.3f; bodyTop = r * 1.5f;
            bloodColor = new Color(0.4f, 1f, 0.55f);
            body = ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0, r * 0.8f, 0), Vector3.one * r * 2f,
                                  ArenaUtil.Lit(new Color(0.3f, 0.75f, 0.95f), new Color(0.05f, 0.15f, 0.25f))).transform;
            var eye = ArenaUtil.Lit(new Color(0.07f, 0.07f, 0.07f));
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, body, new Vector3(s * 0.17f, 0.12f, 0.43f), Vector3.one * 0.12f, eye);
            var gold = ArenaUtil.Lit(new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.25f, 0.05f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.5f, 0), new Vector3(0.36f, 0.06f, 0.36f), gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                ArenaUtil.Prim(PrimitiveType.Cube, body, new Vector3(Mathf.Cos(a) * 0.15f, 0.6f, Mathf.Sin(a) * 0.15f), new Vector3(0.05f, 0.12f, 0.05f), gold);
            }
        }

        protected override void OnHit(DamageEvent ev)
        {
            if (ev.Killed || splits >= SlimeQueenRules.SplitAt.Length || State.HpFraction > SlimeQueenRules.SplitAt[splits]) return;
            splits++;
            Phase = 2;
            if (splits == 1) ArenaGame.Instance?.OnBossPhase2();
            scaleK *= SlimeQueenRules.ShrinkPerSplit; speedK *= SlimeQueenRules.SpeedPerSplit;
            ArenaAudio.Play(Sfx.Slime, Center, 1f, 0.5f);
            ArenaFx.Burst(Center, bloodColor, 30, 5f, 1.5f);
            for (int i = 0; i < SlimeQueenRules.SplitSlimes && ArenaGame.Instance; i++)
            {
                float a = i * Mathf.PI * 2f / SlimeQueenRules.SplitSlimes;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var child = ArenaGame.Instance.Spawn(EnemyDef.Slime(1f), transform.position + dir * 1.2f, false) as SlimeEnemy;
                if (child) { child.Launch(dir * 3f, 3.5f); child.SpawnGrace = 0.4f; }
            }
        }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            actT += dt; jumpCd -= dt; contactCd -= dt;
            float r = Def.Radius;
            switch (act)
            {
                case Act.Hop:
                    FacePlayer(toPlayer, dt, 4f);
                    vy -= 14f * dt; y = Mathf.Max(0f, y + vy * dt);
                    if (y == 0f) vy = 0f;
                    if (State.CanMove && y == 0f && (hopT -= dt) < 0f)
                    {
                        hopT = Random.Range(0.8f, 1.3f) / speedK;
                        vy = 3.5f;
                        knock += toPlayer.normalized * Mathf.Min(d, Def.MoveSpeed * speedK) * State.SpeedMultiplier;
                    }
                    if (jumpCd <= 0f && State.CanAct && y == 0f)
                    {
                        act = Act.Windup; actT = 0f;
                        jumpTo = Player.FeetPosition;
                        marker = BossMarker.Make(jumpTo, SlimeQueenRules.JumpRadius);
                        ArenaAudio.Play(Sfx.Windup, Center, 0.9f, 0.5f);
                        ArenaGame.Instance?.Message("Saia do círculo!", 0.8f);
                    }
                    break;
                case Act.Windup:
                    // Agacha (achata) e mira; o círculo segue você até o fim do aviso.
                    jumpTo = Vector3.Lerp(jumpTo, Player.FeetPosition, dt * 2f);
                    if (marker) marker.position = new Vector3(jumpTo.x, 0.03f, jumpTo.z);
                    BossMarker.Pulse(marker, actT / SlimeQueenRules.JumpWindup);
                    if (actT >= SlimeQueenRules.JumpWindup / speedK) { act = Act.Air; actT = 0f; jumpFrom = transform.position; ArenaAudio.Play(Sfx.Whoosh, Center, 1f, 0.5f); }
                    break;
                case Act.Air:
                {
                    float t = Mathf.Clamp01(actT / SlimeQueenRules.JumpAir);
                    var p = Vector3.Lerp(jumpFrom, jumpTo, t);
                    y = Mathf.Sin(t * Mathf.PI) * 3f;
                    transform.position = ArenaUtil.ClampToArena(new Vector3(p.x, 0f, p.z));
                    if (t < 1f) break;
                    y = 0f;
                    if (marker) Destroy(marker.gameObject);
                    ArenaFx.Ring(transform.position, new Color(0.4f, 0.9f, 1f), SlimeQueenRules.JumpRadius + 0.2f, 0.35f);
                    ArenaFx.Burst(transform.position + Vector3.up * 0.3f, bloodColor, 30, 5f, 1.5f);
                    ArenaAudio.Play(Sfx.Slam, transform.position, 1f, 0.7f);
                    ArenaInput.Haptic(1f, 0.15f); ArenaInput.Haptic(1f, 0.15f, left: true);
                    if ((Player.FeetPosition - transform.position).Flat().magnitude < SlimeQueenRules.JumpRadius)
                    {
                        Player.Push((Player.FeetPosition - transform.position).Flat().normalized * SlimeQueenRules.JumpPush);
                        Player.TakeDamage(SlimeQueenRules.JumpDamage, transform.position);
                    }
                    act = Act.Rest; actT = 0f;
                    jumpCd = SlimeQueenRules.JumpCooldown / speedK;
                    break;
                }
                case Act.Rest:   // caiu: parada, tremendo (janela de ataque)
                    if (actT >= SlimeQueenRules.LandStun) { act = Act.Hop; actT = 0f; }
                    break;
            }
            var pos = transform.position; pos.y = y; transform.position = pos;
            float sq = act == Act.Windup ? 0.7f : act == Act.Air ? 1.15f : act == Act.Rest ? 0.85f + Mathf.Sin(Time.time * 30f) * 0.04f : 1f + Mathf.Sin(Time.time * 6f) * 0.05f;
            body.localScale = new Vector3(1f / Mathf.Sqrt(sq), sq, 1f / Mathf.Sqrt(sq)) * r * 2f * scaleK;
            body.localPosition = new Vector3(0, r * 0.8f * scaleK * sq, 0);
            // Encostar nela gruda e machuca.
            if (contactCd <= 0f && act != Act.Air && d < r * scaleK + 0.35f && State.CanAct)
            {
                contactCd = SlimeQueenRules.ContactInterval;
                Player.TakeDamage(SlimeQueenRules.ContactDamage, transform.position);
            }
        }

        protected override void OnDestroy() { if (marker) Destroy(marker.gameObject); base.OnDestroy(); }
    }

    // ===================== Chefe de Guerra Orc =====================
    // IA do orc (golpe anunciado e aparável) + investida em linha (errou = cai), giro em área e grito de guerra.
    public sealed class OrcWarlord : OrcEnemy, IBoss
    {
        public int Phase { get; private set; } = 1;
        enum Act { None, ChargeWindup, Charging, SpinWindup, Spinning, WarCry }
        Act act;
        float actT, chargeCd = 4f, spinCd = 5f, cryCd = 10f, warCryT, spinHitCd;
        Vector3 chargeDir, chargeStart;
        Transform marker;

        protected override float IncomingDamageMultiplier => BossRules.DamageTakenMultiplier(State.Posture.IsBroken);
        protected override float IncomingPostureMultiplier => BossRules.HitPostureMultiplier * (warCryT > 0f ? OrcWarlordRules.WarCryPostureMultiplier : 1f);

        protected override void BuildVisual()
        {
            base.BuildVisual();
            // Elmo com chifres e capa vermelha: dá para ver que é o chefe.
            var iron = ArenaUtil.Lit(new Color(0.3f, 0.3f, 0.33f), new Color(0.03f, 0.03f, 0.03f));
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 1.26f, 0.02f), new Vector3(0.38f, 0.22f, 0.38f), iron);
            var horn = ArenaUtil.Lit(new Color(0.9f, 0.85f, 0.7f));
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(s * 0.22f, 1.38f, 0f), new Vector3(0.05f, 0.22f, 0.05f), horn).transform.localRotation = Quaternion.Euler(0, 0, -s * 30f);
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.75f, -0.28f), new Vector3(0.6f, 0.75f, 0.03f), ArenaUtil.Lit(new Color(0.6f, 0.1f, 0.08f)));
        }

        protected override void OnHit(DamageEvent ev)
        {
            if (Phase == 1 && State.HpFraction < EnemyDef.BossPhase2Fraction && !ev.Killed)
            {
                Phase = 2;
                windup = EnemyDef.BossPhase2Windup;
                ArenaGame.Instance?.OnBossPhase2();
                ArenaAudio.Play(Sfx.Roar, Center, 1f, 0.7f);
            }
        }

        protected override void OnStunned() { Clear(); act = Act.None; model.localRotation = Quaternion.identity; }

        protected override bool ExtraAI(float dt, float d)
        {
            chargeCd -= dt; spinCd -= dt; cryCd -= dt; warCryT -= dt; spinHitCd -= dt;
            if (act != Act.None) { UpdateAct(dt, d); return true; }
            if (state != AiState.Approach || !State.CanAct) return false;
            if (cryCd <= 0f && State.HpFraction < 0.8f) { cryCd = OrcWarlordRules.WarCryCooldown; StartAct(Act.WarCry); return true; }
            if (d < OrcWarlordRules.SpinRadius * 0.8f && spinCd <= 0f) { spinCd = OrcWarlordRules.SpinCooldown / Phase; StartAct(Act.SpinWindup); return true; }
            if (d > OrcWarlordRules.ChargeMinDistance && chargeCd <= 0f)
            {
                chargeCd = OrcWarlordRules.ChargeCooldown;
                chargeDir = (Player.FeetPosition - transform.position).Flat().normalized;
                StartAct(Act.ChargeWindup);
                ArenaGame.Instance?.Message("Investida!", 0.8f);
                return true;
            }
            return false;
        }

        void StartAct(Act a)
        {
            act = a; actT = 0f;
            if (a == Act.SpinWindup) { marker = BossMarker.Make(transform.position, OrcWarlordRules.SpinRadius); ArenaAudio.Play(Sfx.Windup, Center, 1f, 0.5f); }
            if (a == Act.ChargeWindup) { clubMat.color = new Color(0.9f, 0.15f, 0.05f); ArenaAudio.Play(Sfx.Windup, Center, 0.9f, 0.6f); }
            if (a == Act.WarCry) { ArenaAudio.Play(Sfx.Roar, Center, 1f, 0.6f); ArenaFx.FloatText("GRITO DE GUERRA", Center + Vector3.up * 1.4f, new Color(1f, 0.4f, 0.3f), 1.3f); }
        }

        void End(float rest = 0f)
        {
            act = Act.None; Clear();
            clubMat.color = clubColor;
            model.localRotation = Quaternion.identity;
            SetState(AiState.Recover);
            if (rest > 0f) State.ApplyStun(rest);
        }

        void Clear() { if (marker) Destroy(marker.gameObject); marker = null; }

        void UpdateAct(float dt, float d)
        {
            actT += dt;
            var away = (Player.FeetPosition - transform.position).Flat();
            away = away.sqrMagnitude < 1e-4f ? -transform.forward : away.normalized;
            switch (act)
            {
                case Act.WarCry:
                    LerpArm(-150f, 6f, dt);
                    if (actT < 0.8f) return;
                    warCryT = OrcWarlordRules.WarCrySeconds;
                    State.Posture.Clear();   // limpa a postura
                    ArenaFx.Ring(transform.position, new Color(1f, 0.3f, 0.2f), 4f, 0.5f);
                    End();
                    return;
                case Act.SpinWindup:
                    LerpArm(-90f, 6f, dt);
                    if (marker) marker.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
                    BossMarker.Pulse(marker, actT / OrcWarlordRules.SpinWindup);
                    if (actT >= OrcWarlordRules.SpinWindup) { act = Act.Spinning; actT = 0f; ArenaAudio.Play(Sfx.Whoosh, Center, 1f, 0.6f); }
                    return;
                case Act.Spinning:
                    transform.Rotate(0f, 720f * dt, 0f);
                    if (marker) marker.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
                    if (d < OrcWarlordRules.SpinRadius && spinHitCd <= 0f)
                    {
                        spinHitCd = 0.6f;
                        Player.Push(away * OrcWarlordRules.SpinPush);
                        Player.TakeDamage(OrcWarlordRules.SpinDamage, transform.position);
                        ArenaAudio.Play(Sfx.HitHeavy, Center, 1f, 0.8f);
                    }
                    if (actT >= OrcWarlordRules.SpinSeconds) End(0.9f);   // tonto depois do giro: janela
                    return;
                case Act.ChargeWindup:
                    model.localRotation = Quaternion.Euler(18f * Mathf.Min(1f, actT / OrcWarlordRules.ChargeWindup), 0, 0);
                    chargeDir = Vector3.Slerp(chargeDir, away, dt * 3f).normalized;
                    transform.rotation = Quaternion.LookRotation(chargeDir);
                    if (actT >= OrcWarlordRules.ChargeWindup) { act = Act.Charging; actT = 0f; chargeStart = transform.position; ArenaAudio.Play(Sfx.Roar, Center, 0.7f, 1.1f); }
                    return;
                case Act.Charging:
                    transform.rotation = Quaternion.LookRotation(chargeDir);
                    transform.position = ArenaUtil.ClampToArena(transform.position + chargeDir * OrcWarlordRules.ChargeSpeed * dt);
                    if (d < OrcWarlordRules.ChargeHitRadius + Def.Radius)
                    {
                        Player.Push(chargeDir * BossRules.StompPush);
                        Player.TakeDamage(OrcWarlordRules.ChargeDamage, transform.position);
                        ArenaAudio.Play(Sfx.Impact, Center, 1f, 0.7f);
                        End(0.4f);
                        return;
                    }
                    bool wall = (transform.position - chargeStart).magnitude < OrcWarlordRules.ChargeSpeed * actT * 0.5f;
                    if (actT > OrcWarlordRules.ChargeMaxSeconds || wall)
                    {
                        ArenaFx.FloatText("CAIU!", Center + Vector3.up * 1f, new Color(1f, 0.9f, 0.6f), 1.1f);
                        ArenaAudio.Play(Sfx.Slam, Center, 1f, 0.6f);
                        End(OrcWarlordRules.ChargeMissStun);
                    }
                    return;
            }
        }

        protected override void OnDestroy() { Clear(); base.OnDestroy(); }
    }

    // ===================== Lich =====================
    // Flutua longe, solta orbes em leque (REBATA para machucar: rebatido no Lich dá dano dobrado), teleporta,
    // levanta esqueletos em 66% e 33% e, se você chegar perto, solta uma onda de gelo e some.
    public sealed class LichBoss : ArenaEnemy, IBoss
    {
        public int Phase { get; private set; } = 1;
        enum Act { Float, Volley, NovaWindup, FadeOut, FadeIn }
        Act act;
        float actT, volleyCd = 2f, teleportCd = LichRules.TeleportCooldown, bobT;
        int raises;
        Transform visual, staffGem, marker;
        Vector3 teleportTo;
        readonly List<ArenaEnemy> minions = new List<ArenaEnemy>();

        protected override float IncomingDamageMultiplier => BossRules.DamageTakenMultiplier(State.Posture.IsBroken);
        protected override float IncomingPostureMultiplier => BossRules.HitPostureMultiplier;

        protected override void BuildModel()
        {
            float k = Def.Size;
            bodyBottom = 0.3f * k; bodyTop = 1.55f * k;
            bloodColor = new Color(0.6f, 0.95f, 1f);
            visual = new GameObject("Lich").transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * k;
            var robe = ArenaUtil.Lit(new Color(0.18f, 0.1f, 0.25f));
            var bone = ArenaUtil.Lit(new Color(0.88f, 0.86f, 0.78f));
            var glow = ArenaUtil.Lit(new Color(0.4f, 1f, 0.9f), new Color(0.3f, 1f, 0.9f));
            ArenaUtil.Prim(PrimitiveType.Capsule, visual, new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.6f, 0.4f), robe);
            ArenaUtil.Prim(PrimitiveType.Cylinder, visual, new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.25f, 0.5f), robe);   // barra do manto
            ArenaUtil.Prim(PrimitiveType.Sphere, visual, new Vector3(0, 1.45f, 0.02f), new Vector3(0.3f, 0.32f, 0.3f), bone);
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, visual, new Vector3(s * 0.07f, 1.47f, 0.13f), Vector3.one * 0.06f, glow);
            var gold = ArenaUtil.Lit(new Color(0.85f, 0.7f, 0.2f), new Color(0.2f, 0.15f, 0.03f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, visual, new Vector3(0, 1.62f, 0.02f), new Vector3(0.28f, 0.05f, 0.28f), gold);
            ArenaUtil.Prim(PrimitiveType.Cylinder, visual, new Vector3(0.3f, 0.95f, 0.1f), new Vector3(0.04f, 0.8f, 0.04f), ArenaUtil.Lit(new Color(0.25f, 0.2f, 0.18f)));
            staffGem = ArenaUtil.Prim(PrimitiveType.Sphere, visual, new Vector3(0.3f, 1.8f, 0.1f), Vector3.one * 0.13f, glow).transform;
        }

        protected override void OnHit(DamageEvent ev)
        {
            if (ev.Killed || raises >= LichRules.RaiseAt.Length || State.HpFraction > LichRules.RaiseAt[raises]) return;
            raises++;
            if (raises == 1) { Phase = 2; ArenaGame.Instance?.OnBossPhase2(); }
            minions.RemoveAll(m => !m || m.Dead);
            ArenaAudio.Play(Sfx.Rattle, Center, 1f, 0.6f);
            ArenaGame.Instance?.Message("O Lich levanta os mortos!", 1.5f);
            for (int i = 0; i < LichRules.RaiseCount && minions.Count < LichRules.MaxMinions && ArenaGame.Instance; i++)
            {
                var p = transform.position + Quaternion.Euler(0, 90f + 180f * i, 0) * transform.forward * 2f;
                var m = ArenaGame.Instance.Spawn(EnemyDef.Esqueleto, p, false);
                if (m) { m.SpawnGrace = 0.8f; minions.Add(m); ArenaFx.Burst(p + Vector3.up * 0.5f, new Color(0.4f, 1f, 0.9f), 20, 3f); }
            }
        }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            actT += dt; volleyCd -= dt; teleportCd -= dt; bobT += dt;
            FacePlayer(toPlayer, dt, 5f);
            visual.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(bobT * 2f) * 0.08f, 0f);
            switch (act)
            {
                case Act.Float:
                {
                    // Mantém 5-8 m de distância.
                    var dir = d > 0.01f ? toPlayer / d : transform.forward;
                    if (State.CanMove) Move(d < 5f ? -dir * Def.MoveSpeed : d > 8f ? dir * Def.MoveSpeed : Vector3.zero, dt);
                    if (!State.CanAct) return;
                    if (d < LichRules.NovaRange) { Begin(Act.NovaWindup); marker = BossMarker.Make(transform.position, LichRules.NovaRadius, new Color(0.4f, 0.9f, 1f, 0.4f)); ArenaAudio.Play(Sfx.Ice, Center, 1f, 0.6f); return; }
                    if (teleportCd <= 0f) { StartTeleport(); return; }
                    if (volleyCd <= 0f) { Begin(Act.Volley); ArenaAudio.Play(Sfx.Windup, Center, 0.9f, 1.2f); }
                    return;
                }
                case Act.Volley:
                    staffGem.localScale = Vector3.one * (0.13f + 0.12f * Mathf.Min(1f, actT / LichRules.VolleyWindup));
                    if (actT < LichRules.VolleyWindup) return;
                    staffGem.localScale = Vector3.one * 0.13f;
                    var from = staffGem.position;
                    var aim = (Player.HeadPosition - Vector3.up * 0.3f - from).normalized;
                    int n = LichRules.VolleyOrbs + (Phase >= 2 ? 2 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        float a = (i - (n - 1) * 0.5f) * LichRules.VolleySpread;
                        MagicOrb.Spawn(from, Quaternion.AngleAxis(a, Vector3.up) * aim * MagicOrbRules.Speed, Def.AttackDamage, this);
                    }
                    ArenaAudio.Play(Sfx.Skill, from, 0.9f, 0.6f);
                    volleyCd = LichRules.VolleyCooldown / (Phase >= 2 ? 1.3f : 1f);
                    Begin(Act.Float);
                    return;
                case Act.NovaWindup:
                    BossMarker.Pulse(marker, actT / LichRules.NovaWindup);
                    if (actT < LichRules.NovaWindup) return;
                    if (marker) Destroy(marker.gameObject);
                    ArenaFx.Ring(transform.position, new Color(0.6f, 0.95f, 1f), LichRules.NovaRadius, 0.4f);
                    ArenaAudio.Play(Sfx.Ice, Center, 1f, 0.8f);
                    if (d < LichRules.NovaRadius)
                    {
                        Player.Push(toPlayer.normalized * LichRules.NovaPush);
                        Player.TakeDamage(LichRules.NovaDamage, transform.position);
                    }
                    StartTeleport();
                    return;
                case Act.FadeOut:
                    visual.localScale = Vector3.one * Def.Size * Mathf.Max(0.01f, 1f - actT / LichRules.FadeSeconds);
                    if (actT < LichRules.FadeSeconds) return;
                    transform.position = teleportTo;
                    ArenaFx.Burst(teleportTo + Vector3.up, new Color(0.4f, 1f, 0.9f), 16, 2f);
                    Begin(Act.FadeIn);
                    return;
                case Act.FadeIn:
                    visual.localScale = Vector3.one * Def.Size * Mathf.Min(1f, actT / LichRules.FadeSeconds);
                    if (actT >= LichRules.FadeSeconds) Begin(Act.Float);
                    return;
            }
        }

        void Begin(Act a) { act = a; actT = 0f; }

        void StartTeleport()
        {
            teleportCd = LichRules.TeleportCooldown;
            var feet = Player.FeetPosition;
            teleportTo = transform.position;
            for (int i = 0; i < 12; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var p = feet + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(LichRules.TeleportMin, LichRules.TeleportMax);
                if (!ArenaUtil.IsOutside(p + Vector3.up, 1f)) { teleportTo = p; break; }
            }
            ArenaFx.Burst(Center, new Color(0.4f, 1f, 0.9f), 16, 2f);
            ArenaAudio.Play(Sfx.Whoosh, Center, 0.8f, 0.6f);
            Begin(Act.FadeOut);
        }

        protected override void OnDestroy() { if (marker) Destroy(marker.gameObject); base.OnDestroy(); }
    }
}
