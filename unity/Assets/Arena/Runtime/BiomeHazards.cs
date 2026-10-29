using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Fornalha: poça de lava. Queima o jogador e os inimigos que pisam (dá para empurrar goblins nela).
    public sealed class LavaPool : MonoBehaviour
    {
        Rect2 rect;
        float tick;
        Material mat;

        public static LavaPool Spawn(Rect2 r, Transform parent)
        {
            var go = new GameObject("Lava");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<LavaPool>();
            p.rect = r;
            p.mat = ArenaUtil.Unlit(new Color(1f, 0.4f, 0.08f));
            ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(r.CenterX, 0.02f, r.CenterZ), new Vector3(r.Width, 0.02f, r.Depth), p.mat);
            ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(r.CenterX, 0.015f, r.CenterZ), new Vector3(r.Width + 0.3f, 0.02f, r.Depth + 0.3f), ArenaUtil.Lit(new Color(0.15f, 0.08f, 0.06f)));
            return p;
        }

        void Update()
        {
            float t = Time.time;
            mat.color = Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.65f, 0.15f), 0.5f + 0.5f * Mathf.Sin(t * 2.3f + rect.CenterX));
            if (Random.value < Time.deltaTime * 3f)
                ArenaFx.Burst(new Vector3(Random.Range(rect.MinX, rect.MaxX), 0.1f, Random.Range(rect.MinZ, rect.MaxZ)), new Color(1f, 0.5f, 0.1f), 2, 1f);
            var game = ArenaGame.Instance;
            if (!game || ArenaGame.Blocked) return;
            tick -= Time.deltaTime;
            if (tick > 0f) return;
            tick = BiomeRules.LavaTick;
            var f = game.Player.FeetPosition;
            if (rect.Contains(f.x, f.z) && f.y < 0.3f)
            {
                game.Player.TakeDamage(BiomeRules.LavaPlayerDps * BiomeRules.LavaTick, f);
                ArenaFx.FloatText("LAVA!", game.Player.HeadPosition + game.Player.HeadForward * 0.6f, new Color(1f, 0.5f, 0.1f), 0.8f);
            }
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead || EnemyDef.IsBoss(e.Def.Kind)) continue;
                var p = e.transform.position;
                if (!rect.Contains(p.x, p.z)) continue;
                e.ApplyDamage(BiomeRules.LavaEnemyDps * BiomeRules.LavaTick, 0f, null, 0f, new Color(1f, 0.5f, 0.1f), canCrit: false);
                if (!e.Dead) e.State.Status.AddHeat(BiomeRules.LavaHeatPerSecond * BiomeRules.LavaTick);
            }
        }
    }

    // Gelo: estalactite que cai do teto num ponto perto de você (círculo de aviso, depois o cone cai).
    public sealed class Icicle : MonoBehaviour
    {
        float t;
        Transform marker, cone;
        Vector3 at;

        public static void Spawn(Vector3 at, Transform parent)
        {
            var go = new GameObject("Estalactite");
            go.transform.SetParent(parent, false);
            var i = go.AddComponent<Icicle>();
            i.at = new Vector3(at.x, 0f, at.z);
            i.marker = BossMarker.Make(i.at, BiomeRules.IcicleRadius, new Color(0.5f, 0.85f, 1f, 0.35f));
            i.marker.SetParent(go.transform, true);
            i.cone = ArenaUtil.Prim(PrimitiveType.Capsule, go.transform, i.at + Vector3.up * 5f, new Vector3(0.35f, 0.7f, 0.35f),
                                    ArenaUtil.Lit(new Color(0.75f, 0.92f, 1f), new Color(0.2f, 0.35f, 0.45f))).transform;
        }

        void Update()
        {
            t += Time.deltaTime;
            BossMarker.Pulse(marker, t / BiomeRules.IcicleWarn);
            float fall = Mathf.Clamp01((t - BiomeRules.IcicleWarn * 0.6f) / (BiomeRules.IcicleWarn * 0.4f));
            cone.position = at + Vector3.up * Mathf.Lerp(5f, 0.6f, fall * fall);
            if (t < BiomeRules.IcicleWarn) return;
            ArenaFx.Burst(at + Vector3.up * 0.3f, new Color(0.75f, 0.92f, 1f), 20, 3.5f);
            ArenaAudio.Play(Sfx.Ice, at, 1f, 0.7f);
            var game = ArenaGame.Instance;
            if (game && (game.Player.FeetPosition - at).Flat().magnitude < BiomeRules.IcicleRadius) game.Player.TakeDamage(BiomeRules.IcicleDamage, at);
            foreach (var e in ArenaEnemy.All.ToArray())
                if (e && !e.Dead && (e.transform.position - at).Flat().magnitude < BiomeRules.IcicleRadius + e.Radius)
                {
                    e.ApplyDamage(BiomeRules.IcicleEnemyDamage, 0.6f, null, 0f, new Color(0.75f, 0.92f, 1f));
                    if (!e.Dead) e.State.ApplySlow(1.5f);
                }
            Destroy(gameObject);
        }
    }
}
