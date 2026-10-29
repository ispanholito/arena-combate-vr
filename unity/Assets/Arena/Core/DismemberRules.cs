namespace Arena.Core
{
    // Desmembramento (ideia do Blade & Sorcery): corte forte com o fio no pescoço arranca a cabeça.
    // O esqueleto tem o próprio sistema (SkeletonBody: cabeça, braços e pernas que voltam).
    public static class DismemberRules
    {
        public const float DecapMinSpeed = 5.5f;        // m/s da lâmina
        public const float DecapMinSpeedHeavy = 7f;     // orc: pescoço grosso, só machado
        public const float DecapMinEdge = 0.6f;         // golpe de fio (não de chapa)

        public static bool Decapitates(EnemyKind k, DamageType type, bool head, float speed, float edge)
        {
            if (!head || edge < DecapMinEdge) return false;
            if (type != DamageType.Corte && type != DamageType.CorteMachado) return false;
            switch (k)
            {
                case EnemyKind.Goblin:
                case EnemyKind.Arqueiro:
                case EnemyKind.Xama:
                    return speed >= DecapMinSpeed;
                case EnemyKind.Orc:
                    return type == DamageType.CorteMachado && speed >= DecapMinSpeedHeavy;
                default:
                    return false;   // chefe, slime, mímico, boneco e esqueleto (tem o próprio) não
            }
        }
    }
}
