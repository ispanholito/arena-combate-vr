using System;

namespace Arena.Core
{
    // Objetos que quebram (inspiração Soul Knight): caixas e barris soltam um pouco de ouro ou uma poção;
    // o barril vermelho explode (fere inimigos e o jogador perto) e explode outros barris em cadeia.
    public enum BreakableKind { Caixa, Barril, BarrilExplosivo }

    public static class BreakableRules
    {
        public const float BreakSpeed = 2.5f;          // golpe da arma (m/s) que quebra
        public const float BodyBreakSpeed = 4.5f;      // inimigo arremessado contra o objeto
        public const float ExplosionRadius = 3.5f, ExplosionDamage = 45f, ExplosionPlayerDamage = 14f;
        public const float ChainDelay = 0.15f;         // barril vizinho explode um instante depois (fica bonito)
        public const float GoldChance = 0.45f, PotionChance = 0.05f;
        public const int GoldMin = 1, GoldMax = 4;
        public const float ArenaRespawnSeconds = 25f;

        // Ouro e poção de um objeto comum (o explosivo não solta nada).
        public static (int gold, bool potion) RollLoot(BreakableKind kind, Random rng)
        {
            if (kind == BreakableKind.BarrilExplosivo) return (0, false);
            int gold = rng.NextDouble() < GoldChance ? rng.Next(GoldMin, GoldMax + 1) : 0;
            bool potion = rng.NextDouble() < PotionChance;
            return (gold, potion);
        }

        // Quantos objetos numa sala de combate: 2 a 4, e 40% de chance de um deles ser explosivo.
        public static int CountForRoom(Random rng) => 2 + rng.Next(3);
        public static bool RoomHasExplosive(Random rng) => rng.NextDouble() < 0.4;
    }
}
