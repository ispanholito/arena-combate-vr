using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Rei Goblin: 2 fases. Na fúria ataca mais rápido, invoca slimes e faz golpe em área com círculo de aviso.
    public sealed class GoblinKing : GoblinEnemy
    {
        public int Phase { get; private set; } = 1;
        float slamCooldown = 5f;
        Transform marker;
        Material markerMat;
        bool summoned;

        protected override void OnHit(DamageEvent ev)
        {
            if (Phase == 1 && State.HpFraction < EnemyDef.BossPhase2Fraction && !ev.Killed)
            {
                Phase = 2;
                windup = EnemyDef.BossPhase2Windup;
                skinMat.color = new Color(0.45f, 0.3f, 0.15f);
                if (ArenaGame.Instance)
                {
                    ArenaGame.Instance.OnBossPhase2();
                    if (!summoned)
                    {
                        summoned = true;
                        for (int i = 0; i < EnemyDef.BossSummonCount; i++)
                            ArenaGame.Instance.Spawn(EnemyDef.Slime(1f), transform.position + transform.right * (i == 0 ? -1.5f : 1.5f), false);
                    }
                }
            }
        }

        protected override void OnStunned() => ClearMarker();

        // Chefe apanhando sem parar (sem a postura quebrada): onda de choque que empurra o jogador para trás.
        protected override void ComboBreak()
        {
            Player.Weapon.OnEnemyRemoved(this);
            ClearMarker();
            SpawnGrace = ComboBreaker.ImmuneSeconds + 0.1f;
            var away = (Player.FeetPosition - transform.position).Flat();
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();
            ArenaFx.Ring(transform.position, new Color(1f, 0.6f, 0.2f), 2.7f, 0.35f);
            ArenaFx.FloatText("RUGIDO!", Center + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.3f), 1.4f);
            if (Vector3.Distance(Player.FeetPosition.Flat(), transform.position.Flat()) < 2.5f)
            {
                Player.Push(away * 5f);
                Player.TakeDamage(6f, transform.position);
            }
            ArenaInput.Haptic(0.8f, 0.15f); ArenaInput.Haptic(0.8f, 0.15f, left: true);
            SetState(AiState.Recover);
        }

        protected override bool ExtraAI(float dt, float d)
        {
            if (Phase < 2) return false;
            slamCooldown -= dt;
            if (state == AiState.Slam)
            {
                LerpArm(-170f, 6f, dt);
                float t = stateT / EnemyDef.BossSlamWindup;
                if (marker)
                {
                    float s = EnemyDef.BossSlamRadius * 2f * (0.3f + 0.7f * Mathf.Min(1f, t));
                    marker.localScale = new Vector3(s, 0.005f, s);
                    var c = markerMat.color; c.a = 0.35f + 0.4f * Mathf.Min(1f, t); markerMat.color = c;
                }
                if (stateT > EnemyDef.BossSlamWindup)
                {
                    var center = marker ? marker.position : transform.position;
                    if (Vector3.Distance(Player.FeetPosition.Flat(), center.Flat()) < EnemyDef.BossSlamRadius)
                        Player.TakeDamage(EnemyDef.BossSlamDamage, transform.position);
                    ArenaFx.Ring(center, new Color(1f, 0.31f, 0.19f), EnemyDef.BossSlamRadius + 0.2f, 0.4f);
                    ArenaFx.Burst(center + Vector3.up * 0.2f, new Color(0.69f, 0.56f, 0.44f), 30, 4f, 1.5f);
                    ArenaInput.Haptic(1f, 0.15f); ArenaInput.Haptic(1f, 0.15f, left: true);
                    ClearMarker();
                    SetArm(75f);
                    SetState(AiState.Recover);
                }
                return true;
            }
            if (state == AiState.Approach && slamCooldown <= 0f && d < 6f)
            {
                slamCooldown = EnemyDef.BossSlamCooldown;
                SetState(AiState.Slam);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(go.GetComponent<Collider>());
                markerMat = ArenaUtil.Unlit(new Color(1f, 0.16f, 0.06f, 0.35f));
                go.GetComponent<Renderer>().sharedMaterial = markerMat;
                marker = go.transform;
                marker.position = new Vector3(Player.FeetPosition.x, 0.03f, Player.FeetPosition.z);
                if (ArenaGame.Instance) ArenaGame.Instance.Message("Saia do círculo!", 0.9f);
                return true;
            }
            return false;
        }

        void ClearMarker() { if (marker) Destroy(marker.gameObject); marker = null; }

        protected override void OnDestroy() { ClearMarker(); base.OnDestroy(); }
    }
}
