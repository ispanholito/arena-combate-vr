using System;
using System.Numerics;

namespace Arena.Core
{
    // Agarrar inimigos com a mão vazia (ideia do Blade & Sorcery, pedido do Pablo em 2026-10-17).
    public static class GrabRules
    {
        public const float Reach = 0.15f;          // a palma precisa estar a até 15 cm da superfície do corpo
        public const float MaxThrowSpeed = 14f;
        public const float MinThrowSpeed = 1.5f;   // abaixo disso só larga (cai)

        // Leves dá para agarrar; orc, chefe, mímico (baú) e boneco não.
        public static bool CanGrab(EnemyKind k) =>
            k == EnemyKind.Slime || k == EnemyKind.Goblin || k == EnemyKind.Arqueiro || k == EnemyKind.Esqueleto;

        // Quanto o arremesso amplifica a velocidade da mão (slime é leve e voa longe).
        public static float ThrowMultiplier(EnemyKind k) => k == EnemyKind.Slime ? 1.8f : k == EnemyKind.Esqueleto ? 1.4f : 1.3f;

        public static Vector3 ThrowVelocity(Vector3 handVelocity, EnemyKind k)
        {
            float s = handVelocity.Length();
            if (s < MinThrowSpeed) return handVelocity;   // largou devagar: cai perto
            var v = handVelocity * ThrowMultiplier(k);
            float vs = v.Length();
            return vs > MaxThrowSpeed ? v * (MaxThrowSpeed / vs) : v;
        }
    }
}
