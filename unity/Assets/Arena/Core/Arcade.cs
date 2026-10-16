using System;

namespace Arena.Core
{
    // Modo "arcade" (pedido do Pablo): cada vez que você vence a dungeon (ou o chefe da arena) a PROFUNDIDADE sobe —
    // inimigos com mais vida, mais dano, salas mais cheias, mais ouro e itens mais raros. Morrer volta à profundidade 1.
    // A dificuldade da pausa continua valendo por cima disso.
    public static class ArcadeRules
    {
        public const int MaxTier = 99;

        public static float HpMultiplier(int tier) => 1f + 0.15f * (Clamp(tier) - 1);
        public static float DamageMultiplier(int tier) => 1f + 0.1f * (Clamp(tier) - 1);
        public static float GoldMultiplier(int tier) => 1f + 0.2f * (Clamp(tier) - 1);
        public static int ExtraEnemies(int tier) => (Clamp(tier) - 1) / 2;   // +1 inimigo por sala a cada 2 profundidades

        // Pesos de raridade (Comum..Lendário): cada profundidade engorda as raras (a mais rara cresce mais rápido).
        public static double[] RarityWeights(int tier)
        {
            var w = new double[LootTable.RarityWeights.Length];
            double boost = 1.0 + 0.35 * (Clamp(tier) - 1);
            for (int i = 0; i < w.Length; i++) w[i] = LootTable.RarityWeights[i] * Math.Pow(boost, i);
            return w;
        }

        public static int Clamp(int tier) => Math.Clamp(tier, 1, MaxTier);
    }
}
