using System;

namespace Arena.Core
{
    public enum WeaponKind { Espada, Machado, Cajado }

    // Peso: com pouca Força a arma pesada "atrasa" na mão (lag no follow).
    public sealed class WeaponDef
    {
        public string Id { get; }
        public string Name { get; }
        public WeaponKind Kind { get; }
        public float Weight { get; }
        public float DamageMultiplier { get; }
        public float StaminaPerHit { get; }
        public float Length { get; }   // do cabo à ponta, em metros

        public WeaponDef(string id, string name, WeaponKind kind, float weight, float damageMultiplier, float staminaPerHit, float length)
        {
            Id = id; Name = name; Kind = kind; Weight = weight;
            DamageMultiplier = damageMultiplier; StaminaPerHit = staminaPerHit; Length = length;
        }

        // Quão rápido a arma segue a mão (1/s). Força alta / arma leve = colada na mão.
        public float FollowRate(float strength) => Math.Clamp(60f * (float)Math.Pow(strength / Weight, 1.5), 5f, 45f);

        public static readonly WeaponDef EspadaLonga = new WeaponDef("espada", "Espada Longa", WeaponKind.Espada, 2.5f, 1.0f, 6f, 1.07f);
        public static readonly WeaponDef MachadoPesado = new WeaponDef("machado", "Machado Pesado", WeaponKind.Machado, 14f, 1.7f, 14f, 1.05f);
        public static readonly WeaponDef CajadoArcano = new WeaponDef("cajado", "Cajado Arcano", WeaponKind.Cajado, 2f, 0.5f, 5f, 1.14f);

        public static WeaponDef[] LoadoutFor(ClassId cls) =>
            cls == ClassId.Lamina ? new[] { EspadaLonga, MachadoPesado } : new[] { CajadoArcano };
    }
}
