using System;

namespace Arena.Core
{
    // Armadilha de espinhos: ciclo parado → aviso (placa brilha) → espinhos para cima (machuca quem está em cima,
    // uma vez por ciclo) → parado. Machuca inimigos também: dá para atrair goblins para cima dela.
    public sealed class SpikeTrapCycle
    {
        public enum Phase { Idle, Warning, Up }
        public const float IdleSeconds = 2.4f, WarningSeconds = 0.7f, UpSeconds = 0.8f;
        public const float PlayerDamage = 12f, EnemyDamage = 22f, HalfSize = 0.65f;

        public Phase Current { get; private set; }
        float t;

        public SpikeTrapCycle(float offset = 0f) { t = offset % (IdleSeconds + WarningSeconds + UpSeconds); Advance(0f); }

        // Avança o tempo; devolve true no quadro em que os espinhos sobem.
        public bool Advance(float dt)
        {
            var before = Current;
            t = (t + dt) % (IdleSeconds + WarningSeconds + UpSeconds);
            Current = t < IdleSeconds ? Phase.Idle : t < IdleSeconds + WarningSeconds ? Phase.Warning : Phase.Up;
            return Current == Phase.Up && before != Phase.Up;
        }

        public static bool Inside(float trapX, float trapZ, float x, float z, float radius = 0f) =>
            Math.Abs(x - trapX) <= HalfSize + radius && Math.Abs(z - trapZ) <= HalfSize + radius;
    }

    // Mímico: parte dos baús da dungeon é um monstro disfarçado.
    public static class MimicRules
    {
        public static float Chance(int floor) => floor <= 1 ? 0.12f : 0.22f;
        public const float BiteRange = 2.6f;      // começa o bote a esta distância
        public const float LungeSpeed = 6f, LungeSeconds = 0.35f, RecoverSeconds = 1.1f;
    }

    // Flechas do goblin arqueiro.
    public static class ArrowRules
    {
        public const float Speed = 14f;
        public const float PreferredMin = 5f, PreferredMax = 9f;   // distância que o arqueiro tenta manter
        public const float Cooldown = 2.2f;
        public const float DeflectSpeed = 3f;   // ponta da arma a pelo menos 3 m/s encostando na flecha = rebate
        public const float DeflectRadius = 0.18f;
        public const float ReflectedDamage = 25f;

        // Velocidade inicial para acertar um alvo a (dx horizontal, dy vertical) com gravidade g, mirando reto
        // e compensando a queda: devolve o ângulo de elevação extra (rad) para a velocidade dada.
        public static float LobAngle(float horizontal, float speed, float g = 9.81f)
        {
            if (horizontal <= 0f) return 0f;
            float s = g * horizontal / (speed * speed);
            return s >= 1f ? (float)(Math.PI / 4) : 0.5f * (float)Math.Asin(s);
        }
    }
}
