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

        // ---------- Bloqueio com a arma ----------
        // Goblin (e esqueleto, menos) de frente para o golpe às vezes bloqueia com a clava: não toma vida, mas a postura
        // sobe ainda mais (vale a pena insistir e quebrar a postura). Golpe muito forte atravessa a defesa com metade do dano.
        public enum BlockOutcome { None, Blocked, BrokeThrough }
        public const float BlockCooldown = 1.6f, FacingForBlock = 0.5f;   // cos ~60°
        public const float BlockPostureFactor = 1.2f, BreakThroughSpeed = 7.5f, BreakThroughWeight = 8f;

        public static float BlockChance(EnemyKind kind) => kind switch
        {
            EnemyKind.Goblin => 0.3f,
            EnemyKind.Esqueleto => 0.2f,
            _ => 0f,
        };

        // facing = cos do ângulo entre a frente do inimigo e a direção de onde vem o golpe (1 = de frente).
        public static BlockOutcome ResolveBlock(EnemyKind kind, bool canBlock, float facing, double roll, float tipSpeed, float weaponWeight, bool twoHanded)
        {
            if (!canBlock || facing < FacingForBlock || roll >= BlockChance(kind)) return BlockOutcome.None;
            bool heavy = tipSpeed >= BreakThroughSpeed || weaponWeight >= BreakThroughWeight || twoHanded;
            return heavy ? BlockOutcome.BrokeThrough : BlockOutcome.Blocked;
        }

        // Recua? Só goblin comum, uma vez, abaixo de 30% de vida, com sorte (roll 0..1).
        public static bool ShouldRetreat(EnemyKind kind, float hpFraction, bool alreadyRetreated, double roll) =>
            kind == EnemyKind.Goblin && !alreadyRetreated && hpFraction > 0f && hpFraction < RetreatHpFraction && roll < RetreatChance;
    }
}
