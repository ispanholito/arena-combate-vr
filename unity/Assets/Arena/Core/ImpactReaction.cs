using System;

namespace Arena.Core
{
    // Fase 2: como o inimigo reage ao impacto físico da arma (impulso da colisão, em N·s).
    public static class ImpactReaction
    {
        public const float KnockScale = 0.6f;          // fração da velocidade "transferida" que vira empurrão
        public const float MaxKnockSpeed = 4f;         // m/s
        public const float LeanPerSpeed = 0.12f;       // rad de inclinação por m/s de impacto
        public const float MaxLean = 0.6f;             // ~35°
        public const float LeanStiffness = 60f;        // mola que endireita o corpo (1/s²)
        public const float LeanDamping = 9f;
        public const float StaggerSpeed = 2.5f;        // impacto acima disso (m/s equivalentes) faz cambalear
        public const float StaggerSeconds = 0.3f;
        public const float StaggerCooldown = 1.2f;     // não dá para travar o inimigo cambaleando sem parar

        // Velocidade equivalente do impacto: impulso dividido pela massa do inimigo.
        public static float ImpactSpeed(float impulse, float enemyMass) => impulse / Math.Max(0.1f, enemyMass);

        public static float KnockSpeed(float impulse, float enemyMass) => Math.Min(MaxKnockSpeed, ImpactSpeed(impulse, enemyMass) * KnockScale);
        public static float LeanKick(float impulse, float enemyMass) => Math.Min(MaxLean, ImpactSpeed(impulse, enemyMass) * LeanPerSpeed);
        public static bool Staggers(float impulse, float enemyMass) => ImpactSpeed(impulse, enemyMass) >= StaggerSpeed;

        // Ragdoll ao morrer
        public const float DeathImpulseBase = 2f;      // m/s mínimo na direção do golpe final
        public const float DeathImpulseUp = 1.2f;      // m/s para cima (o corpo "salta" um pouco)
        public const float CorpseSeconds = 5f;
        public const float CorpseFadeSeconds = 0.6f;
        public const int MaxCorpses = 6;               // limite para o Quest
        public const float PartDensity = 60f;          // kg/m³ pelo volume das primitivas (goblin ≈ 15 kg: a espada consegue empurrar o corpo)
        public const float MinPartMass = 0.15f;
    }
}
