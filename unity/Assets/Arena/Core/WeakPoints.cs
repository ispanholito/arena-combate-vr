namespace Arena.Core
{
    // Pontos fracos: acertar a cabeça (parte de cima do eixo do corpo) causa mais dano.
    public static class WeakPoints
    {
        public const float HeadMultiplier = 1.5f;

        // Fração da altura do corpo (0 = base, 1 = topo) a partir da qual o golpe conta como cabeça; negativo = não tem cabeça.
        public static float HeadZone(EnemyKind kind) => kind switch
        {
            EnemyKind.Slime => -1f,
            EnemyKind.Boneco => 0.9f,     // cabeça de palha bem no alto
            _ => 0.7f,                    // goblin e Rei Goblin
        };

        public static bool IsHead(EnemyKind kind, float heightFraction)
        {
            float z = HeadZone(kind);
            return z >= 0f && heightFraction >= z;
        }

        public static float Multiplier(EnemyKind kind, float heightFraction) => IsHead(kind, heightFraction) ? HeadMultiplier : 1f;
    }
}
