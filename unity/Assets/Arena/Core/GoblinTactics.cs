using System;

namespace Arena.Core
{
    // Táticas simples de grupo para goblins/esqueletos: cada um ocupa uma "vaga" num arco à frente do jogador
    // (em vez de todos virem pelo mesmo lado), e goblin muito ferido às vezes recua um pouco antes de voltar.
    public static class GoblinTactics
    {
        public const float SlotSpacingDeg = 55f, MaxSlotDeg = 140f;
        public const float RetreatHpFraction = 0.3f, RetreatChance = 0.45f, RetreatSeconds = 1.4f;
        public const float EngageDistance = 3.5f;   // mais longe que isso: só se aproxima

        // Ângulo (graus, em volta do jogador, 0 = na frente dele) da vaga do membro `index` num grupo de `count`.
        public static float SlotAngle(int index, int count)
        {
            if (count <= 1) return 0f;
            float a = (index - (count - 1) / 2f) * SlotSpacingDeg;
            return Math.Clamp(a, -MaxSlotDeg, MaxSlotDeg);
        }

        // Recua? Só goblin comum, uma vez, abaixo de 30% de vida, com sorte (roll 0..1).
        public static bool ShouldRetreat(EnemyKind kind, float hpFraction, bool alreadyRetreated, double roll) =>
            kind == EnemyKind.Goblin && !alreadyRetreated && hpFraction > 0f && hpFraction < RetreatHpFraction && roll < RetreatChance;
    }
}
