using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Caixa/barril que quebra com golpe da arma, explosão ou inimigo arremessado contra ele.
    // O barril vermelho explode (dano em área + cadeia). Regras em Core/Breakables.cs.
    public sealed class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();
        static readonly System.Random rng = new System.Random();

        public BreakableKind Kind { get; private set; }
        public System.Action<Breakable> OnBroken;   // a arena usa para renascer o objeto
        bool broken;
        float height;
        Color debris;

        public static Breakable Spawn(BreakableKind kind, Vector3 pos, float yaw, Transform parent)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, yaw, 0f));
            var b = go.AddComponent<Breakable>();
            b.Kind = kind;
            b.Build();
            All.Add(b);
            return b;
        }

        void Build()
        {
            string model = Kind == BreakableKind.Caixa ? "box_small" : "barrel_small";
            var art = KayKitArt.Place(model, transform, transform.position, transform.eulerAngles.y);
            Vector3 size = Kind == BreakableKind.Caixa ? new Vector3(0.7f, 0.7f, 0.7f) : new Vector3(0.6f, 0.85f, 0.6f);
            debris = Kind == BreakableKind.Caixa ? new Color(0.55f, 0.36f, 0.18f) : new Color(0.45f, 0.28f, 0.14f);
            if (art)
            {
                // Mede o modelo para a colisão caber nele.
                var bounds = new Bounds(transform.position, Vector3.zero);
                foreach (var r in art.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                if (bounds.size.y > 0.1f) size = new Vector3(Mathf.Max(bounds.size.x, 0.3f), bounds.size.y, Mathf.Max(bounds.size.z, 0.3f));
                if (Kind == BreakableKind.BarrilExplosivo)
                    foreach (var r in art.GetComponentsInChildren<Renderer>())
                    {
                        r.material.color = new Color(0.85f, 0.18f, 0.12f);
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", new Color(0.25f, 0.03f, 0.01f));
                    }
            }
            else
            {
                var c = Kind == BreakableKind.BarrilExplosivo ? new Color(0.85f, 0.18f, 0.12f) : debris;
                ArenaUtil.Prim(Kind == BreakableKind.Caixa ? PrimitiveType.Cube : PrimitiveType.Cylinder, transform,
                               new Vector3(0f, size.y * 0.5f, 0f), Kind == BreakableKind.Caixa ? size : new Vector3(size.x, size.y * 0.5f, size.z), ArenaUtil.Lit(c));
            }
            if (Kind == BreakableKind.BarrilExplosivo)
            {
                // Faixa amarela de perigo: dá para reconhecer de longe.
                ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, size.y * 0.55f, 0f), new Vector3(size.x * 1.04f, 0.04f, size.z * 1.04f),
                               ArenaUtil.Lit(new Color(1f, 0.85f, 0.1f), new Color(0.4f, 0.3f, 0f)));
                debris = new Color(0.85f, 0.18f, 0.12f);
            }
            height = size.y;
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
            col.size = size;
        }

        void OnCollisionEnter(Collision c)
        {
            if (broken || !c.rigidbody) return;
            float speed = c.relativeVelocity.magnitude;
            var pl = ArenaGame.Instance ? ArenaGame.Instance.Player : null;
            bool weapon = pl && pl.WeaponOf(c.rigidbody);
            if (weapon ? speed >= BreakableRules.BreakSpeed : speed >= BreakableRules.BodyBreakSpeed) Break();
        }

        public Vector3 Center => transform.position + Vector3.up * height * 0.5f;

        public void Break()
        {
            if (broken) return;
            broken = true;
            var c = Center;
            ArenaAudio.Play(Sfx.HitHeavy, c, 0.9f, 0.8f);
            ArenaFx.Burst(c, debris, 14, 3f);
            Debris(c);
            var game = ArenaGame.Instance;
            if (Kind == BreakableKind.BarrilExplosivo) Explode(c, game);
            else if (game && game.Mode != ArenaGame.GameMode.Treino)
            {
                var (gold, potion) = BreakableRules.RollLoot(Kind, rng);
                var st = game.Player.State;
                if (gold > 0)
                {
                    gold = st.Blessings.ScaleGold(gold);
                    st.AddGold(gold);
                    ArenaFx.FloatText("+" + gold + " ouro", c + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.3f), 0.8f);
                }
                if (potion && st.AddPotion()) ArenaFx.FloatText("+1 poção", c + Vector3.up * 0.7f, new Color(1f, 0.35f, 0.45f), 0.9f);
            }
            OnBroken?.Invoke(this);
            Destroy(gameObject);
        }

        static void Explode(Vector3 c, ArenaGame game)
        {
            SkillCaster.Explode(c, BreakableRules.ExplosionRadius, BreakableRules.ExplosionDamage);   // também quebra os vizinhos
            ArenaInput.Haptic(1f, 0.2f); ArenaInput.Haptic(1f, 0.2f, left: true);
            if (game && Vector3.Distance(game.Player.FeetPosition + Vector3.up * 0.9f, c) < BreakableRules.ExplosionRadius)
                game.Player.TakeDamage(BreakableRules.ExplosionPlayerDamage, c);
            // Inimigos físicos voam.
            foreach (var g in RagdollGoblin.All)
            {
                if (!g) continue;
                var d = g.Center - c;
                if (d.magnitude < BreakableRules.ExplosionRadius) g.OnThrown(((d.Flat().normalized * 6f) + Vector3.up * 4f));
            }
        }

        // Chamado por toda explosão (bola de fogo, barril): quebra o que estiver no raio (os explosivos em cadeia).
        public static void OnExplosion(Vector3 pos, float radius)
        {
            foreach (var b in All.ToArray())
            {
                if (!b || b.broken) continue;
                if (Vector3.Distance(b.Center, pos) > radius + 0.3f) continue;
                if (b.Kind == BreakableKind.BarrilExplosivo) b.Invoke(nameof(Break), BreakableRules.ChainDelay);
                else b.Break();
            }
        }

        // Pedaços que caem e somem.
        void Debris(Vector3 c)
        {
            var mat = ArenaUtil.Lit(debris);
            for (int i = 0; i < 6; i++)
            {
                var p = ArenaUtil.Prim(PrimitiveType.Cube, null, c + Random.insideUnitSphere * 0.2f,
                                       new Vector3(Random.Range(0.08f, 0.25f), 0.04f, Random.Range(0.06f, 0.14f)), mat, keepCollider: true);
                p.transform.rotation = Random.rotation;
                var rb = p.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.linearVelocity = Random.insideUnitSphere * 2.5f + Vector3.up * 2f;
                p.layer = 2;   // Ignore Raycast: não atrapalha a mira
                Destroy(p, 2.5f);
            }
        }

        void OnDestroy() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i]) Destroy(All[i].gameObject);
            All.Clear();
        }
    }
}
