using System;
using System.Collections.Generic;

namespace Arena.Core
{
    // Biomas por andar (inspiração Soul Knight). Cada descida sorteia um bioma diferente para cada andar.
    // Diferença de jogo (a paleta/arte própria por bioma é o item 12, guardado): cor da luz, inimigos e perigos.
    //   Cripta:   esqueletos e xamãs mais comuns, luz verde-fria, espinhos.
    //   Gelo:     estalactites caem perto de você durante a luta (círculo de aviso), mais slimes e arqueiros, luz azul.
    //   Fornalha: poças de lava (queimam quem pisa, inimigos também), mais orcs e barris explosivos, luz laranja.
    public enum Biome { Nenhum, Cripta, Gelo, Fornalha }

    public static class BiomeRules
    {
        static readonly Biome[] All = { Biome.Cripta, Biome.Gelo, Biome.Fornalha };

        public static Biome For(int seed, int floor)
        {
            var order = new List<Biome>(All);
            var rng = new Random(seed * 131 + 7);
            for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            return order[(Math.Max(1, floor) - 1) % order.Count];
        }

        public static string Name(Biome b) => b switch
        {
            Biome.Cripta => "Cripta",
            Biome.Gelo => "Caverna Gelada",
            Biome.Fornalha => "Fornalha",
            _ => "Dungeon",
        };

        public static (float r, float g, float b) LightColor(Biome b) => b switch
        {
            Biome.Cripta => (0.55f, 1f, 0.7f),
            Biome.Gelo => (0.6f, 0.8f, 1f),
            Biome.Fornalha => (1f, 0.45f, 0.18f),
            _ => (1f, 0.6f, 0.3f),
        };

        // Troca parte dos inimigos pelos do bioma (o chefe não muda).
        public static EnemyDef Swap(EnemyDef e, Biome b, Random rng)
        {
            double r = rng.NextDouble();
            switch (b)
            {
                case Biome.Cripta:
                    if (e.Kind == EnemyKind.Goblin && r < 0.35) return EnemyDef.Esqueleto;
                    if (e.Kind == EnemyKind.Slime && r < 0.25) return EnemyDef.Xama;
                    break;
                case Biome.Gelo:
                    if (e.Kind == EnemyKind.Goblin && r < 0.25) return EnemyDef.Slime(1f);
                    if (e.Kind == EnemyKind.Esqueleto && r < 0.3) return EnemyDef.Arqueiro;
                    break;
                case Biome.Fornalha:
                    if (e.Kind == EnemyKind.Goblin && r < 0.2) return EnemyDef.Orc;
                    if (e.Kind == EnemyKind.Slime && r < 0.3) return EnemyDef.Goblin;
                    break;
            }
            return e;
        }

        // Fornalha: poças de lava.
        public const float LavaPlayerDps = 8f, LavaEnemyDps = 10f, LavaHeatPerSecond = 30f, LavaTick = 0.5f;
        public const float LavaSize = 2.6f;
        // Gelo: estalactites.
        public const float IcicleMinInterval = 3.5f, IcicleMaxInterval = 6f, IcicleWarn = 1.1f, IcicleRadius = 1.2f, IcicleDamage = 16f, IcicleEnemyDamage = 30f;
        // Fornalha: barris explosivos a mais por sala.
        public static bool ExtraExplosives(Biome b) => b == Biome.Fornalha;
    }
}
