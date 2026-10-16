using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Armadilha de espinhos da dungeon (ciclo no Core: SpikeTrapCycle). A placa brilha vermelho antes de os espinhos
    // subirem; machuca o jogador e os inimigos que estiverem em cima (uma vez por ciclo cada).
    public sealed class SpikeTrap : MonoBehaviour
    {
        SpikeTrapCycle cycle;
        Transform spikes;
        Material plateMat;
        readonly HashSet<ArenaEnemy> hitEnemies = new HashSet<ArenaEnemy>();
        bool hitPlayer;
        SpikeTrapCycle.Phase last;

        static readonly Color PlateColor = new Color(0.22f, 0.2f, 0.2f);

        public static SpikeTrap Spawn(Vector3 pos, Transform parent, float offset)
        {
            var go = new GameObject("Espinhos");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<SpikeTrap>();
            t.Build(offset);
            return t;
        }

        void Build(float offset)
        {
            cycle = new SpikeTrapCycle(offset);
            last = cycle.Current;
            float s = SpikeTrapCycle.HalfSize * 2f;
            plateMat = ArenaUtil.Lit(PlateColor);
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0, 0.01f, 0), new Vector3(s, 0.03f, s), plateMat);
            spikes = new GameObject("Pontas").transform;
            spikes.SetParent(transform, false);
            var metal = ArenaUtil.Lit(new Color(0.65f, 0.66f, 0.7f), new Color(0.05f, 0.05f, 0.06f));
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    var p = new Vector3(-s / 2f + s * (i + 0.5f) / 4f, 0.2f, -s / 2f + s * (j + 0.5f) / 4f);
                    ArenaUtil.Prim(PrimitiveType.Cube, spikes, p, new Vector3(0.05f, 0.4f, 0.05f), metal).transform.localRotation = Quaternion.Euler(0, 45, 0);
                }
            spikes.localPosition = new Vector3(0, -0.42f, 0);
        }

        void Update()
        {
            if (ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            cycle.Advance(dt);
            var phase = cycle.Current;
            if (phase != last)
            {
                if (phase == SpikeTrapCycle.Phase.Warning) ArenaAudio.Play(Sfx.Click, transform.position, 0.6f, 0.6f);
                if (phase == SpikeTrapCycle.Phase.Up) { ArenaAudio.Play(Sfx.Stab, transform.position, 0.8f, 1.4f); hitPlayer = false; hitEnemies.Clear(); }
                last = phase;
            }
            // Visual: placa pisca no aviso; espinhos sobem rápido e descem devagar.
            plateMat.color = phase == SpikeTrapCycle.Phase.Warning
                ? Color.Lerp(PlateColor, new Color(0.9f, 0.15f, 0.05f), 0.5f + 0.5f * Mathf.Sin(Time.time * 25f)) : PlateColor;
            float targetY = phase == SpikeTrapCycle.Phase.Up ? 0f : -0.42f;
            spikes.localPosition = new Vector3(0, Mathf.MoveTowards(spikes.localPosition.y, targetY, dt * (phase == SpikeTrapCycle.Phase.Up ? 8f : 1.5f)), 0);

            if (phase != SpikeTrapCycle.Phase.Up) return;
            var pos = transform.position;
            var game = ArenaGame.Instance;
            if (game && !hitPlayer)
            {
                var feet = game.Player.FeetPosition;
                if (SpikeTrapCycle.Inside(pos.x, pos.z, feet.x, feet.z, 0.15f))
                {
                    hitPlayer = true;
                    game.Player.TakeDamage(SpikeTrapCycle.PlayerDamage, pos);
                }
            }
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead || hitEnemies.Contains(e) || e.Def.Kind == EnemyKind.ReiGoblin) continue;
                var p = e.transform.position;
                if (!SpikeTrapCycle.Inside(pos.x, pos.z, p.x, p.z, e.Radius * 0.5f)) continue;
                hitEnemies.Add(e);
                e.ApplyDamage(SpikeTrapCycle.EnemyDamage, 0.6f, null, 0f, new Color(1f, 0.6f, 0.5f), canCrit: false);
            }
        }
    }
}
