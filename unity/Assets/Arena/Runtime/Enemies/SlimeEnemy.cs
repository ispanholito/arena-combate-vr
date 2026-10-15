using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Slime: pula na direção do jogador, dano por contato, se divide ao morrer.
    public sealed class SlimeEnemy : ArenaEnemy
    {
        Transform body;
        float hopTimer, vy, y, attackCooldown;

        protected override void BuildModel()
        {
            float r = Def.Radius;
            bodyBottom = r * 0.4f; bodyTop = r * 1.6f;
            bloodColor = new Color(0.36f, 1f, 0.48f);
            body = ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0, r * 0.8f, 0), Vector3.one * r * 2f,
                                  ArenaUtil.Lit(new Color(0.27f, 0.85f, 0.42f), new Color(0.04f, 0.2f, 0.07f))).transform;
            var eye = ArenaUtil.Lit(new Color(0.07f, 0.07f, 0.07f));
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, body, new Vector3(s * 0.17f, 0.15f, 0.42f), Vector3.one * 0.14f, eye);
            hopTimer = Random.Range(0.5f, 1.5f);
        }

        public void Launch(Vector3 velocity, float upward) { knock += velocity; vy = upward; }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            FacePlayer(toPlayer, dt);
            vy -= 12f * dt; y += vy * dt;
            if (y <= 0f) { y = 0f; vy = 0f; }
            if (State.CanMove && y == 0f && (hopTimer -= dt) < 0f)
            {
                hopTimer = Random.Range(0.9f, 1.7f);
                vy = Random.Range(3.2f, 4.2f);
                knock += toPlayer.normalized * Mathf.Min(d, Def.MoveSpeed) * State.SpeedMultiplier;
            }
            var p = transform.position; p.y = y; transform.position = p;
            float sq = y == 0f ? 1f + Mathf.Sin(Time.time * 8f) * 0.05f : 1.12f;
            body.localScale = new Vector3(1f / Mathf.Sqrt(sq), sq, 1f / Mathf.Sqrt(sq)) * Def.Radius * 2f;

            attackCooldown -= dt;
            if (attackCooldown < 0f && d < Def.Radius + 0.35f && y < 0.6f && State.CanAct)
            {
                attackCooldown = 1.2f;
                Player.TakeDamage(Def.AttackDamage, transform.position);
            }
        }

        protected override void Die()
        {
            if (EnemyDef.SlimeSplits(Def.Size) && ArenaGame.Instance)
            {
                foreach (int s in new[] { -1, 1 })
                {
                    var child = ArenaGame.Instance.Spawn(EnemyDef.Slime(Def.Size * EnemyDef.SlimeChildScale), transform.position + transform.right * s * 0.4f, false) as SlimeEnemy;
                    if (child) { child.Launch(transform.right * s * 2.5f, 3f); child.SpawnGrace = 0.3f; }
                }
            }
            base.Die();
        }
    }
}
