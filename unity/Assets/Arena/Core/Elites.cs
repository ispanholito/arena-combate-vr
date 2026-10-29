using System;

namespace Arena.Core
{
    // Inimigos de elite (inspiração Soul Knight): a mesma criatura com aura colorida e um modificador.
    // Dá muita variedade sem arte nova. Valem XP e ouro ×3 e têm chance de deixar arma.
    public enum EliteKind { Nenhum, Furioso, Blindado, Explosivo, Regenerador }

    public static class EliteRules
    {
        public const float XpMultiplier = 3f, GoldMultiplier = 3f, WeaponDropChance = 0.25f;
        public const float FuriousSpeed = 1.35f, FuriousWindup = 0.7f;
        public const float ArmoredDamageTaken = 0.55f;
        public const float ExplodeDelay = 1f, ExplodeRadius = 3f, ExplodePlayerDamage = 20f, ExplodeEnemyDamage = 40f;
        public const float RegenPerSecond = 0.04f, RegenAfterHit = 2f;

        public static float HpMultiplier(EliteKind k) => k switch
        {
            EliteKind.Furioso => 1.4f,
            EliteKind.Blindado => 1.4f,
            EliteKind.Explosivo => 1.3f,
            EliteKind.Regenerador => 1.6f,
            _ => 1f,
        };
        public static float WindupMultiplier(EliteKind k) => k == EliteKind.Furioso ? FuriousWindup : 1f;

        public static string Name(EliteKind k) => k switch
        {
            EliteKind.Furioso => "Furioso",
            EliteKind.Blindado => "Blindado",
            EliteKind.Explosivo => "Explosivo",
            EliteKind.Regenerador => "Regenerador",
            _ => "",
        };

        // Cor da aura (r, g, b).
        public static (float r, float g, float b) Color(EliteKind k) => k switch
        {
            EliteKind.Furioso => (1f, 0.2f, 0.15f),
            EliteKind.Blindado => (0.55f, 0.7f, 1f),
            EliteKind.Explosivo => (1f, 0.55f, 0.05f),
            EliteKind.Regenerador => (0.3f, 1f, 0.35f),
            _ => (1f, 1f, 1f),
        };

        public static bool CanBeElite(EnemyKind k) =>
            k == EnemyKind.Goblin || k == EnemyKind.Esqueleto || k == EnemyKind.Orc || k == EnemyKind.Arqueiro || k == EnemyKind.Slime || k == EnemyKind.Xama;

        // Chance por inimigo: dungeon 10% no andar 1 e 18% no 2; arena 0 até 8 abates, depois 10%.
        public static float Chance(bool dungeon, int floorOrKills) =>
            dungeon ? (floorOrKills >= 2 ? 0.18f : 0.10f) : (floorOrKills < 8 ? 0f : 0.10f);

        public static EliteKind Roll(EnemyKind kind, float chance, Random rng)
        {
            if (!CanBeElite(kind) || rng.NextDouble() >= chance) return EliteKind.Nenhum;
            // Slime explosivo/regenerador faz sentido; slime "blindado" não.
            var opts = kind == EnemyKind.Slime
                ? new[] { EliteKind.Furioso, EliteKind.Explosivo, EliteKind.Regenerador }
                : new[] { EliteKind.Furioso, EliteKind.Blindado, EliteKind.Explosivo, EliteKind.Regenerador };
            return opts[rng.Next(opts.Length)];
        }
    }
}
