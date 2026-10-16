using System;

namespace Arena.Core
{
    // Fase 4: segurar a arma com as duas mãos.
    public static class TwoHandRules
    {
        public const float StrengthMultiplier = 1.6f;    // a Força conta mais com as duas mãos (arma pesada fica controlável)
        public const float HeavyDamageBonus = 1.15f;     // armas pesadas (Weight > 5) causam mais dano
        public const float HeavyWeight = 5f;
        public const float GrabRadius = 0.12f;           // a mão precisa estar a até 12 cm do cabo
        public const float MaxStretch = 0.25f;           // mão a mais disso do ponto que segura = soltou (afastou as mãos)
        public const float StretchSeconds = 0.3f;
        public const float MainAngularScale = 0.35f;     // a rotação passa a vir da linha entre as mãos

        // Faixa do cabo (eixo da arma, em metros a partir da empunhadura) onde a segunda mão pode segurar.
        public static (float min, float max) GripRange(WeaponKind kind) => kind switch
        {
            WeaponKind.Espada => (-0.12f, 0.08f),     // cabo e pomo
            WeaponKind.Machado => (0.0f, 0.8f),       // cabo inteiro até a cabeça
            WeaponKind.Maca => (0.0f, 0.5f),
            WeaponKind.Lanca => (0.0f, 1.3f),          // haste longa: mãos bem afastadas
            WeaponKind.Adaga => (-0.06f, 0.0f),
            _ => (0.0f, 1.0f),                        // cajado
        };

        public static float ClampToGrip(WeaponKind kind, float z)
        {
            var (min, max) = GripRange(kind);
            return Math.Clamp(z, min, max);
        }

        public static float EffectiveStrength(float strength, bool twoHanded) => twoHanded ? strength * StrengthMultiplier : strength;

        public static float DamageMultiplier(WeaponDef w, bool twoHanded) => twoHanded && w.Weight > HeavyWeight ? HeavyDamageBonus : 1f;
    }
}
