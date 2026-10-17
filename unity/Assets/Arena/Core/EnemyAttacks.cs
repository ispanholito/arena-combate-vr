using System;

namespace Arena.Core
{
    // Repertório de ataques dos inimigos corpo a corpo (ideia das "stances" do Blade & Sorcery): golpe de cima,
    // horizontais dos dois lados, estocada e golpe com passo à frente. Cada um vem de uma direção diferente — o aviso
    // (pose do braço) mostra de onde vem e o aparo precisa ir para lá. Estocada e passo só no normal/difícil.
    public enum EnemyAttack { Cima, DireitaEsquerda, EsquerdaDireita, Estocada, Passo }

    public readonly struct ArmPose
    {
        public readonly float Pitch, Yaw, Forward;   // graus (X, Y do braço) e avanço do ombro (m)
        public ArmPose(float pitch, float yaw, float forward = 0f) { Pitch = pitch; Yaw = yaw; Forward = forward; }
    }

    public static class EnemyAttacks
    {
        public const float NormalRange = 1.45f;   // × escala do inimigo
        public const float StepRange = 2.3f;      // passo à frente: começa mais longe
        public const float StepSpeed = 4.5f, ThrustLunge = 2.5f;

        public static (ArmPose windup, ArmPose end) Poses(EnemyAttack a) => a switch
        {
            EnemyAttack.DireitaEsquerda => (new ArmPose(85f, 110f), new ArmPose(85f, -75f)),
            EnemyAttack.EsquerdaDireita => (new ArmPose(85f, -105f), new ArmPose(85f, 70f)),
            EnemyAttack.Estocada => (new ArmPose(90f, -10f, -0.25f), new ArmPose(90f, -10f, 0.45f)),
            _ => (new ArmPose(-150f, -18f), new ArmPose(75f, -18f)),   // de cima (e passo à frente)
        };

        public static float Range(EnemyAttack a) => a == EnemyAttack.Passo ? StepRange : NormalRange;

        // Quais ataques cada inimigo conhece (clava não estoca; o orc é pesado demais para passo rápido).
        public static bool Knows(EnemyKind k, EnemyAttack a) => (k, a) switch
        {
            (EnemyKind.Goblin, EnemyAttack.Estocada) => false,
            (EnemyKind.Orc, EnemyAttack.Estocada) => false,
            (EnemyKind.Orc, EnemyAttack.Passo) => false,
            (EnemyKind.ReiGoblin, EnemyAttack.Estocada) => false,
            (EnemyKind.ReiGoblin, EnemyAttack.Passo) => false,
            _ => true,
        };

        // Fácil só usa os ataques simples (de cima e horizontais).
        public static bool AllowedIn(EnemyAttack a, Difficulty d) =>
            d != Difficulty.Facil || (a != EnemyAttack.Estocada && a != EnemyAttack.Passo);

        static readonly EnemyAttack[] All = (EnemyAttack[])Enum.GetValues(typeof(EnemyAttack));

        // Sorteia o próximo ataque (roll 0..1) entre os que o inimigo conhece e a dificuldade permite.
        public static EnemyAttack Pick(EnemyKind k, Difficulty d, double roll)
        {
            int n = 0;
            foreach (var a in All) if (Knows(k, a) && AllowedIn(a, d)) n++;
            int idx = Math.Min(n - 1, (int)(Math.Clamp(roll, 0, 0.9999) * n));
            foreach (var a in All)
                if (Knows(k, a) && AllowedIn(a, d)) { if (idx == 0) return a; idx--; }
            return EnemyAttack.Cima;
        }
    }
}
