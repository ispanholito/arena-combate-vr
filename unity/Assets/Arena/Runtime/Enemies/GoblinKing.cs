using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Rei Goblin estilo Souls (pedido do Pablo, 2026-10-16): luta só contra ele, sem invocar ajudantes.
    // - Se defende: fora da postura quebrada leva só 55% do dano (BossRules). Aparar e quebrar a postura é o caminho.
    // - Pisão em quem fica colado (medidor de "colado"; aviso de 0,4 s com círculo em volta dele).
    // - Rugido quando apanha sem parar: enche o peito 0,5 s e a onda joga o jogador LONGE.
    // - Investida quando você foge para longe; se errar, tropeça (janela de ataque).
    // - Fase 2 (menos de 50%): golpes em sequência de 2-3 com pausa longa no fim + golpe em área no chão (círculo).
    // Todo chefe (Rei Goblin, Rainha Slime, Chefe de Guerra Orc, Lich): barra no HUD, fase 2, fora das fichas de ataque.
    public interface IBoss { int Phase { get; } }

    public sealed class GoblinKing : GoblinEnemy, IBoss
    {
        public int Phase { get; private set; } = 1;

        enum Act { None, Roar, Stomp, ChargeWindup, Charging, Rest }
        Act act;
        float actT, slamCooldown = 5f, stompCd, chargeCd = 3f, restFor;
        int comboLeft;
        bool comboing;
        AiState prevState;
        Vector3 chargeDir, chargeStart;
        readonly HugMeter hug = new HugMeter();
        Transform marker;
        Material markerMat;

        protected override float IncomingDamageMultiplier => BossRules.DamageTakenMultiplier(State.Posture.IsBroken);
        protected override float IncomingPostureMultiplier => BossRules.HitPostureMultiplier;
        protected override float WindupTime => comboing ? BossRules.ComboWindup : windup;

        // ---------- Visual de rei (chefe da demo, 2026-10-30; paleta da docs/biblia-de-arte.md) ----------
        // Silhueta própria: coroa com pontas e joia, capa, ombreiras douradas, barriga, clava com cravos; olhos que acendem.
        Material eyeMat, gemMat;
        protected override void BuildVisual()
        {
            base.BuildVisual();
            skinMat.color = new Color(0.36f, 0.66f, 0.27f);
            var red = ArenaUtil.Lit(new Color(0.76f, 0.27f, 0.18f));
            var cape = ArenaUtil.Lit(new Color(0.5f, 0.12f, 0.28f));
            var gold = ArenaUtil.Lit(new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0.02f));
            var iron = ArenaUtil.Lit(new Color(0.72f, 0.75f, 0.82f));
            eyeMat = ArenaUtil.Lit(new Color(1f, 0.9f, 0.3f), new Color(0.9f, 0.7f, 0.1f));
            gemMat = ArenaUtil.Lit(new Color(1f, 0.15f, 0.2f), new Color(0.6f, 0.05f, 0.08f));
            // Barriga e cinto
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 0.6f, 0.04f), new Vector3(0.62f, 0.55f, 0.56f), red);
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.04f, 0.6f), gold);
            // Capa
            var c = ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0f, 0.6f, -0.3f), new Vector3(0.62f, 0.78f, 0.04f), cape).transform;
            c.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            // Ombreiras
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(s * 0.27f, 0.86f, 0f), new Vector3(0.2f, 0.14f, 0.2f), gold);
            // Olhos grandes que acendem (troca o material dos olhos do goblin comum)
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                if (r.transform.localScale == Vector3.one * 0.07f) { r.sharedMaterial = eyeMat; r.transform.localScale = Vector3.one * 0.09f; }
            // Coroa: aro (já existe) + 5 pontas + joia na frente
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var spike = ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(Mathf.Sin(a) * 0.13f, 1.29f, Mathf.Cos(a) * 0.13f), new Vector3(0.05f, 0.13f, 0.05f), gold).transform;
                spike.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                headParts.Add(spike);
            }
            headParts.Add(ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.22f, 0.15f), Vector3.one * 0.06f, gemMat).transform);
            // Clava maior com cravos
            ArenaUtil.Prim(PrimitiveType.Sphere, arm, new Vector3(0f, 0.85f, 0f), new Vector3(0.24f, 0.3f, 0.24f), clubMat);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var sp = ArenaUtil.Prim(PrimitiveType.Cube, arm, new Vector3(Mathf.Sin(a) * 0.12f, 0.85f + (i % 2) * 0.08f, Mathf.Cos(a) * 0.12f), Vector3.one * 0.05f, iron).transform;
                sp.localRotation = Quaternion.Euler(45f, a * Mathf.Rad2Deg, 45f);
            }
        }

        // Entrada: ruge ao aparecer (nome do chefe na tela) — quem está perto é empurrado, sem dano.
        bool introduced;
        void Intro()
        {
            introduced = true;
            ArenaFx.FloatText("REI GOBLIN", Center + Vector3.up * 1.6f, new Color(1f, 0.78f, 0.25f), 2.2f);
            ArenaFx.Ring(transform.position, new Color(1f, 0.78f, 0.25f), 5f, 0.6f);
            ArenaAudio.Play(Sfx.Roar, Center, 1f, 0.8f);
            ArenaInput.Haptic(0.8f, 0.3f); ArenaInput.Haptic(0.8f, 0.3f, left: true);
            model.localScale = Vector3.one * k * 1.12f;
        }

        protected override void OnHit(DamageEvent ev)
        {
            if (Phase == 1 && State.HpFraction < EnemyDef.BossPhase2Fraction && !ev.Killed)
            {
                Phase = 2;
                windup = EnemyDef.BossPhase2Windup;
                skinMat.color = new Color(0.45f, 0.3f, 0.15f);
                // Fúria: olhos e joia vermelhos, ruge e joga você longe (marca bem a troca de fase).
                eyeMat.color = new Color(1f, 0.2f, 0.1f); eyeMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f));
                gemMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.2f));
                ArenaFx.FloatText("FÚRIA!", Center + Vector3.up * 1.4f, new Color(1f, 0.25f, 0.15f), 1.6f);
                ArenaGame.Instance?.OnBossPhase2();
                ClearMarker();
                SpawnGrace = BossRules.RoarWindup + 0.25f;
                StartAct(Act.Roar);
                ArenaAudio.Play(Sfx.Roar, Center, 1f, 0.85f);
            }
        }

        // Morte: câmera lenta curta + explosão de moedas (momento para vídeo).
        protected override void Die()
        {
            ArenaGame.Instance?.SlowMotion(0.3f, 1.1f);
            ArenaFx.Burst(Center + Vector3.up * 0.5f, new Color(1f, 0.78f, 0.25f), 40, 5f, 1.6f);
            ArenaFx.Ring(transform.position, new Color(1f, 0.78f, 0.25f), 4f, 0.6f);
            base.Die();
        }

        protected override void OnStunned() { ClearMarker(); act = Act.None; model.localScale = Vector3.one * k; }

        // Golpes seguidos sem quebrar a postura: em vez de travar no combo do jogador, ele ruge.
        protected override void ComboBreak()
        {
            Player.Weapon.OnEnemyRemoved(this);
            ClearMarker();
            SpawnGrace = BossRules.RoarWindup + 0.25f;   // imune enquanto enche o peito
            StartAct(Act.Roar);
            ArenaFx.FloatText("RUGIDO!", Center + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.3f), 1.4f);
            ArenaAudio.Play(Sfx.Roar, Center, 1f);
        }

        void StartAct(Act a)
        {
            act = a; actT = 0f;
            if (a == Act.Stomp || a == Act.ChargeWindup) { clubMat.color = new Color(0.9f, 0.15f, 0.05f); ArenaAudio.Play(Sfx.Windup, Center, 0.9f, 0.6f); }
            if (a == Act.Stomp) MakeMarker(transform.position, BossRules.StompRadius);
        }

        void EndAct(float rest = 0f)
        {
            act = Act.None;
            clubMat.color = clubColor;
            model.localScale = Vector3.one * k;
            model.localRotation = Quaternion.identity;
            ClearMarker();
            if (rest > 0f) { act = Act.Rest; actT = 0f; restFor = rest; }
            else SetState(AiState.Recover);
        }

        protected override bool ExtraAI(float dt, float d)
        {
            if (!introduced) { Intro(); StartAct(Act.Rest); restFor = 1.2f; return true; }
            stompCd -= dt; chargeCd -= dt; slamCooldown -= dt;
            if (act != Act.None) { UpdateAct(dt, d); return true; }

            // Começo de um ataque normal: decide quantos golpes a sequência terá.
            if (state == AiState.Windup && prevState == AiState.Approach && !comboing)
                comboLeft = BossRules.ComboLength(Phase, Random.value) - 1;
            prevState = state;
            // Fim de um golpe: emenda o próximo da sequência ou descansa (janela para atacar).
            if (state == AiState.Strike && stateT > 0.38f)
            {
                if (comboLeft > 0) { comboLeft--; comboing = true; SetState(AiState.Windup); prevState = AiState.Windup; return true; }
                if (comboing) { comboing = false; EndAct(BossRules.ComboEndRecover); return true; }
            }

            if (state == AiState.Approach)
            {
                if (hug.Tick(dt, d) && stompCd <= 0f) { stompCd = BossRules.StompCooldown; StartAct(Act.Stomp); return true; }
                if (d > BossRules.ChargeMinDistance && chargeCd <= 0f && State.CanAct)
                {
                    chargeCd = BossRules.ChargeCooldown;
                    chargeDir = (Player.FeetPosition - transform.position).Flat().normalized;
                    StartAct(Act.ChargeWindup);
                    ArenaGame.Instance?.Message("Investida!", 0.8f);
                    return true;
                }
                if (Phase >= 2 && slamCooldown <= 0f && d < 6f) return StartSlam();
            }
            if (state == AiState.Slam) return UpdateSlam();
            return false;
        }

        void UpdateAct(float dt, float d)
        {
            actT += dt;
            var away = (Player.FeetPosition - transform.position).Flat();
            away = away.sqrMagnitude < 1e-4f ? -transform.forward : away.normalized;
            switch (act)
            {
                case Act.Roar:
                {
                    // Enche o peito e solta a onda.
                    model.localScale = Vector3.one * k * (1f + 0.12f * Mathf.Min(1f, actT / BossRules.RoarWindup));
                    LerpArm(-120f, 6f, dt);
                    if (actT < BossRules.RoarWindup) return;
                    ArenaFx.Ring(transform.position, new Color(1f, 0.6f, 0.2f), BossRules.RoarRadius, 0.45f);
                    ArenaInput.Haptic(1f, 0.2f); ArenaInput.Haptic(1f, 0.2f, left: true);
                    if (d < BossRules.RoarRadius)
                    {
                        Player.Push(away * BossRules.RoarPush);
                        Player.Vignette(0.8f);
                        Player.TakeDamage(BossRules.RoarDamage, transform.position);
                    }
                    EndAct();
                    return;
                }
                case Act.Stomp:
                {
                    LerpArm(-60f, 8f, dt);
                    float t = actT / BossRules.StompWindup;
                    if (marker)
                    {
                        marker.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
                        var c = markerMat.color; c.a = 0.3f + 0.5f * Mathf.Min(1f, t); markerMat.color = c;
                    }
                    if (actT < BossRules.StompWindup) return;
                    ArenaFx.Ring(transform.position, new Color(1f, 0.4f, 0.2f), BossRules.StompRadius + 0.2f, 0.3f);
                    ArenaFx.Burst(transform.position + Vector3.up * 0.2f, new Color(0.69f, 0.56f, 0.44f), 24, 4f, 1.3f);
                    ArenaAudio.Play(Sfx.Slam, transform.position, 1f, 1.3f);
                    if (d < BossRules.StompRadius)
                    {
                        Player.Push(away * BossRules.StompPush);
                        Player.TakeDamage(BossRules.StompDamage, transform.position);
                    }
                    hug.Reset();
                    EndAct(0.6f);
                    return;
                }
                case Act.ChargeWindup:
                {
                    // Abaixa e mira (vai para onde você estava no fim do aviso).
                    model.localRotation = Quaternion.Euler(18f * Mathf.Min(1f, actT / BossRules.ChargeWindup), 0, 0);
                    LerpArm(-90f, 6f, dt);
                    chargeDir = Vector3.Slerp(chargeDir, away, dt * 4f).normalized;
                    if (actT < BossRules.ChargeWindup) return;
                    act = Act.Charging; actT = 0f; chargeStart = transform.position;
                    ArenaAudio.Play(Sfx.Roar, Center, 0.6f, 1.3f);
                    return;
                }
                case Act.Charging:
                {
                    transform.rotation = Quaternion.LookRotation(chargeDir);
                    transform.position = ArenaUtil.ClampToArena(transform.position + chargeDir * BossRules.ChargeSpeed * dt);
                    if (d < BossRules.ChargeHitRadius + Def.Radius)
                    {
                        Player.Push(chargeDir * BossRules.StompPush);
                        Player.TakeDamage(BossRules.ChargeDamage, transform.position);
                        ArenaAudio.Play(Sfx.Impact, Center, 1f, 0.8f);
                        EndAct(0.5f);
                        return;
                    }
                    bool wall = (transform.position - chargeStart).magnitude < BossRules.ChargeSpeed * actT * 0.5f;   // travou na parede
                    if (actT > BossRules.ChargeMaxSeconds || wall)
                    {
                        // Errou: tropeça — janela para atacar.
                        ArenaFx.FloatText("TROPEÇOU", Center + Vector3.up * 1f, new Color(1f, 0.9f, 0.6f), 1f);
                        State.ApplyStun(BossRules.ChargeMissStumble);
                        EndAct();
                    }
                    return;
                }
                case Act.Rest:
                    LerpArm(30f, 3f, dt);
                    if (actT >= restFor) EndAct();
                    return;
            }
        }

        // ---------- Golpe em área no chão (fase 2): círculo de aviso onde você está ----------
        bool StartSlam()
        {
            slamCooldown = EnemyDef.BossSlamCooldown;
            SetState(AiState.Slam);
            MakeMarker(Player.FeetPosition, EnemyDef.BossSlamRadius * 0.3f);
            ArenaGame.Instance?.Message("Saia do círculo!", 0.9f);
            return true;
        }

        bool UpdateSlam()
        {
            LerpArm(-170f, 6f, Time.deltaTime);
            float t = stateT / EnemyDef.BossSlamWindup;
            if (marker)
            {
                float s = EnemyDef.BossSlamRadius * 2f * (0.3f + 0.7f * Mathf.Min(1f, t));
                marker.localScale = new Vector3(s, 0.005f, s);
                var c = markerMat.color; c.a = 0.35f + 0.4f * Mathf.Min(1f, t); markerMat.color = c;
            }
            if (stateT <= EnemyDef.BossSlamWindup) return true;
            var center = marker ? marker.position : transform.position;
            if (Vector3.Distance(Player.FeetPosition.Flat(), center.Flat()) < EnemyDef.BossSlamRadius)
                Player.TakeDamage(EnemyDef.BossSlamDamage, transform.position);
            ArenaFx.Ring(center, new Color(1f, 0.31f, 0.19f), EnemyDef.BossSlamRadius + 0.2f, 0.4f);
            ArenaAudio.Play(Sfx.Slam, center, 1f);
            ArenaFx.Burst(center + Vector3.up * 0.2f, new Color(0.69f, 0.56f, 0.44f), 30, 4f, 1.5f);
            ArenaInput.Haptic(1f, 0.15f); ArenaInput.Haptic(1f, 0.15f, left: true);
            ClearMarker();
            SetArm(75f);
            EndAct(1f);   // depois do golpe pesado: janela para atacar
            return true;
        }

        void MakeMarker(Vector3 at, float radius)
        {
            ClearMarker();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            markerMat = ArenaUtil.Unlit(new Color(1f, 0.16f, 0.06f, 0.35f));
            go.GetComponent<Renderer>().sharedMaterial = markerMat;
            marker = go.transform;
            marker.position = new Vector3(at.x, 0.03f, at.z);
            marker.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
        }

        void ClearMarker() { if (marker) Destroy(marker.gameObject); marker = null; }

        protected override void OnDestroy() { ClearMarker(); base.OnDestroy(); }
    }
}
