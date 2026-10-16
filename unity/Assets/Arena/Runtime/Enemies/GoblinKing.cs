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
    public sealed class GoblinKing : GoblinEnemy
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

        protected override void OnHit(DamageEvent ev)
        {
            if (Phase == 1 && State.HpFraction < EnemyDef.BossPhase2Fraction && !ev.Killed)
            {
                Phase = 2;
                windup = EnemyDef.BossPhase2Windup;
                skinMat.color = new Color(0.45f, 0.3f, 0.15f);
                ArenaGame.Instance?.OnBossPhase2();
                ArenaAudio.Play(Sfx.Roar, Center, 1f, 0.85f);
            }
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
