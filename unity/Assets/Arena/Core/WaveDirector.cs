using System;

namespace Arena.Core
{
    // Decide quantos e quais inimigos aparecem na arena e quando o chefe chega.
    public sealed class WaveDirector
    {
        public const int KillsForBoss = 18;   // mais inimigos por vez: o chefe vem com mais abates (era 12)
        public const int SkeletonMinLevel = 2;
        public const int ArcherMinLevel = 3;
        public const int OrcMinLevel = 5;
        public float SpawnInterval = 1.0f;   // 2026-10-28 (Pablo): jogo para adolescentes, mais dinâmico (era 1,6 s)

        readonly Random rng;
        float timer = 1f;

        public bool BossAlive { get; private set; }
        public int BossPhase { get; set; } = 1;
        public int BossesDefeated { get; private set; }
        public EnemyKind? LastBoss { get; private set; }   // chefe sorteado (evita repetir seguido)
        public bool NoBoss { get; set; }        // horda infinita: sem chefe
        public int ExtraTarget { get; set; }    // horda infinita: inimigos a mais por onda

        public WaveDirector(int seed = 0) { rng = seed == 0 ? new Random() : new Random(seed); }

        public int TargetCount(int playerLevel) =>
            BossAlive ? 0 : Math.Min(9, 4 + playerLevel / 2) + ExtraTarget;   // chefe estilo Souls: luta sozinho, sem ondas junto (era 2-6, agora 4-9)

        public int MaxGoblins(int playerLevel) => 2 + playerLevel / 2;

        // Chamado a cada frame. Retorna o que nascer agora (ou null).
        public EnemyDef Tick(float dt, int aliveCount, int aliveGoblins, int playerLevel, int playerKills)
        {
            timer -= dt;
            if (timer > 0f) return null;
            if (!NoBoss && !BossAlive && playerKills >= KillsForBoss)
            {
                BossAlive = true; BossPhase = 1; timer = SpawnInterval;
                LastBoss = BossPool.Pick(rng, LastBoss);
                return EnemyDef.Boss(LastBoss.Value, BossesDefeated);
            }
            if (aliveCount >= TargetCount(playerLevel)) { timer = 0.5f; return null; }
            timer = SpawnInterval;
            // Humanoides (goblin, esqueleto, arqueiro) têm limite pelo nível; esqueletos a partir do nível 2, arqueiros do 3.
            bool humanoid = aliveGoblins < MaxGoblins(playerLevel) && rng.NextDouble() < 0.55;
            if (!humanoid) return EnemyDef.Slime(1f);
            if (playerLevel >= OrcMinLevel && rng.NextDouble() < 0.12) return EnemyDef.Orc;
            if (playerLevel >= ArcherMinLevel + 1 && rng.NextDouble() < 0.1) return EnemyDef.Xama;
            if (playerLevel >= ArcherMinLevel && rng.NextDouble() < 0.2) return EnemyDef.Arqueiro;
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
