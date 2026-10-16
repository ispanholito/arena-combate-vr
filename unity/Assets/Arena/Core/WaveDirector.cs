using System;

namespace Arena.Core
{
    // Decide quantos e quais inimigos aparecem na arena e quando o chefe chega.
    public sealed class WaveDirector
    {
        public const int KillsForBoss = 12;
        public const int SkeletonMinLevel = 2;
        public float SpawnInterval = 1.6f;

        readonly Random rng;
        float timer = 1f;

        public bool BossAlive { get; private set; }
        public int BossPhase { get; set; } = 1;
        public int BossesDefeated { get; private set; }

        public WaveDirector(int seed = 0) { rng = seed == 0 ? new Random() : new Random(seed); }

        public int TargetCount(int playerLevel) =>
            BossAlive ? (BossPhase == 2 ? 2 : 0) : Math.Min(6, 2 + playerLevel / 2);

        public int MaxGoblins(int playerLevel) => 1 + playerLevel / 3;

        // Chamado a cada frame. Retorna o que nascer agora (ou null).
        public EnemyDef Tick(float dt, int aliveCount, int aliveGoblins, int playerLevel, int playerKills)
        {
            timer -= dt;
            if (timer > 0f) return null;
            if (!BossAlive && playerKills >= KillsForBoss)
            {
                BossAlive = true; BossPhase = 1; timer = SpawnInterval;
                return EnemyDef.ReiGoblin(BossesDefeated);
            }
            if (aliveCount >= TargetCount(playerLevel)) { timer = 0.5f; return null; }
            timer = SpawnInterval;
            // Humanoides (goblin e esqueleto) têm limite pelo nível; esqueletos a partir do nível 2.
            bool humanoid = aliveGoblins < MaxGoblins(playerLevel) && rng.NextDouble() < 0.55;
            if (!humanoid) return EnemyDef.Slime(1f);
            return playerLevel >= SkeletonMinLevel && rng.NextDouble() < 0.45 ? EnemyDef.Esqueleto : EnemyDef.Goblin;
        }

        public void OnBossDefeated()
        {
            BossAlive = false; BossPhase = 1; BossesDefeated++;
        }

        public void Reset(float delay = 3f)
        {
            BossAlive = false; BossPhase = 1; timer = delay;
        }
    }
}
