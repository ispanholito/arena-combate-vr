using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Aura do inimigo de elite: anel colorido nos pés, brasas subindo e o nome em cima. O Explosivo pisca e explode
    // um instante depois de morrer (dá tempo de se afastar).
    public sealed class EliteAura : MonoBehaviour
    {
        ArenaEnemy enemy;
        Transform ring, label;
        Color color;
        float t, emberT;

        public static void Attach(ArenaEnemy e)
        {
            var a = new GameObject("AuraElite").AddComponent<EliteAura>();
            a.transform.SetParent(e.transform, false);
            a.enemy = e;
            var (r, g, b) = EliteRules.Color(e.State.Elite);
            a.color = new Color(r, g, b);
            a.Build();
        }

        void Build()
        {
            var mat = ArenaUtil.Unlit(new Color(color.r, color.g, color.b, 0.55f));
            ring = ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.03f, 0f),
                                  new Vector3(enemy.Radius * 3.2f, 0.01f, enemy.Radius * 3.2f), mat).transform;
            label = new GameObject("Nome").transform;
            label.SetParent(transform, false);
            ArenaFx.MakeText(label, "ELITE " + EliteRules.Name(enemy.State.Elite).ToUpper(), 0.006f, color);
        }

        void LateUpdate()
        {
            if (!enemy || enemy.Dead) { Destroy(gameObject); return; }
            t += Time.deltaTime;
            // O representante do corpo físico fica nos pés do corpo: a aura segue o centro do inimigo.
            var c = enemy.Center;
            ring.position = new Vector3(c.x, 0.03f, c.z);
            float s = enemy.Radius * 3.2f * (1f + Mathf.Sin(t * 4f) * 0.08f);
            ring.localScale = new Vector3(s, 0.01f, s);
            var game = ArenaGame.Instance;
            label.position = c + Vector3.up * 1.1f;
            if (game) { var d = label.position - game.Player.HeadPosition; d.y = 0f; if (d.sqrMagnitude > 0.01f) label.rotation = Quaternion.LookRotation(d); }
            emberT -= Time.deltaTime;
            if (emberT <= 0f) { emberT = 0.25f; ArenaFx.Burst(c + Random.insideUnitSphere * 0.3f, color, 2, 0.6f); }
        }

        // Explosivo: pisca 1 s no lugar onde morreu e explode (fere o jogador e os outros inimigos).
        public static void ExplodeLater(Vector3 pos)
        {
            var go = new GameObject("ExplosaoElite");
            go.transform.position = pos;
            go.AddComponent<DelayedBlast>();
        }

        sealed class DelayedBlast : MonoBehaviour
        {
            float t;
            GameObject orb;
            void Start()
            {
                orb = ArenaUtil.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.4f, ArenaUtil.Unlit(new Color(1f, 0.55f, 0.05f)));
                ArenaFx.Ring(new Vector3(transform.position.x, 0.05f, transform.position.z), new Color(1f, 0.4f, 0.05f), EliteRules.ExplodeRadius);
                ArenaAudio.Play(Sfx.Windup, transform.position, 0.9f, 1.5f);
            }
            void Update()
            {
                t += Time.deltaTime;
                if (orb) orb.transform.localScale = Vector3.one * (0.4f + t * 0.5f) * (Mathf.Sin(t * 30f) > 0f ? 1f : 0.7f);
                if (t < EliteRules.ExplodeDelay) return;
                var pos = transform.position;
                SkillCaster.Explode(pos, EliteRules.ExplodeRadius, EliteRules.ExplodeEnemyDamage);
                var game = ArenaGame.Instance;
                if (game && Vector3.Distance(game.Player.FeetPosition + Vector3.up * 0.9f, pos) < EliteRules.ExplodeRadius)
                    game.Player.TakeDamage(EliteRules.ExplodePlayerDamage, pos);
                Destroy(gameObject);
            }
        }
    }
}
