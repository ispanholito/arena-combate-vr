namespace Arena.Core
{
    // Partes do corpo (pela altura do acerto no eixo do corpo): cabeça forte, tronco normal, pernas fracas.
    // Inspirado no estudo do Blade & Sorcery (lá: cabeça ×3, membros ×0,5) com números mais suaves, porque os nossos
    // inimigos têm mais vida e o golpe é medido no eixo do corpo (não por osso).
    public static class WeakPoints
    {
        public const float HeadMultiplier = 2f;   // era 1,5
        public const float LegMultiplier = 0.6f;
        public const float LegZone = 0.3f;        // abaixo de 30% da altura = pernas

        // Fração da altura do corpo (0 = base, 1 = topo) a partir da qual o golpe conta como cabeça; negativo = não tem cabeça.
        public static float HeadZone(EnemyKind kind) => kind switch
        {
            EnemyKind.Slime => -1f,
            EnemyKind.Mimico => -1f,
            EnemyKind.Boneco => 0.9f,     // cabeça de palha bem no alto
            _ => 0.7f,                    // humanoides
        };

        public static bool HasLegs(EnemyKind kind) => kind != EnemyKind.Slime && kind != EnemyKind.Mimico && kind != EnemyKind.Boneco;

        public static bool IsHead(EnemyKind kind, float heightFraction)
        {
            float z = HeadZone(kind);
            return z >= 0f && heightFraction >= z;
        }

        public static bool IsLegs(EnemyKind kind, float heightFraction) => HasLegs(kind) && heightFraction < LegZone;

        public static float Multiplier(EnemyKind kind, float heightFraction) =>
            IsHead(kind, heightFraction) ? HeadMultiplier : IsLegs(kind, heightFraction) ? LegMultiplier : 1f;
    }
}
