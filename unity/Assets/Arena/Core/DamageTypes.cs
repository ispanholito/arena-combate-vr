using System;

namespace Arena.Core
{
    // Tipos de dano × material do inimigo (ideia do estudo do Blade & Sorcery, números nossos): cada arma tem um papel.
    // Espada corta carne; machado ignora armadura; lança/adaga furam; maça quebra osso e nocauteia.
    public enum DamageType { Corte, CorteMachado, Perfuracao, Contusao }
    public enum EnemyMaterial { Carne, Couro, Osso, Placa, Gosma, Madeira }

    public static class DamageTypes
    {
        public const float EdgeForCut = 0.35f;     // abaixo disso o golpe é "de chapa": vira pancada
        public const float ThrustAlignment = 0.7f; // ponta indo na direção da lâmina = estocada
        public const float FlatHitMultiplier = 0.8f;   // somado à perda de fio do MeleeDamage dá ~50%, como no B&S

        // Que tipo de dano o golpe causa, pela arma e pelo jeito do golpe.
        public static DamageType Classify(WeaponKind kind, float edgeAlignment, float thrustAlignment)
        {
            switch (kind)
            {
                case WeaponKind.Maca:
                case WeaponKind.Cajado:
                    return DamageType.Contusao;
                case WeaponKind.Machado:
                    return edgeAlignment >= EdgeForCut ? DamageType.CorteMachado : DamageType.Contusao;
                default:   // espada, adaga, lança
                    if (thrustAlignment >= ThrustAlignment) return DamageType.Perfuracao;
                    return edgeAlignment >= EdgeForCut ? DamageType.Corte : DamageType.Contusao;
            }
        }

        // Golpe de chapa com arma de fio: pancada fraca.
        public static bool IsFlatHit(WeaponKind kind, DamageType t) =>
            t == DamageType.Contusao && kind != WeaponKind.Maca && kind != WeaponKind.Cajado;

        public static EnemyMaterial MaterialOf(EnemyKind k) => k switch
        {
            EnemyKind.Esqueleto => EnemyMaterial.Osso,
            EnemyKind.Orc => EnemyMaterial.Placa,
            EnemyKind.ReiGoblin => EnemyMaterial.Couro,
            EnemyKind.Slime => EnemyMaterial.Gosma,
            EnemyKind.Mimico => EnemyMaterial.Madeira,
            _ => EnemyMaterial.Carne,
        };

        public static string MaterialName(EnemyMaterial m) => m switch
        {
            EnemyMaterial.Couro => "couro", EnemyMaterial.Osso => "osso", EnemyMaterial.Placa => "armadura",
            EnemyMaterial.Gosma => "gosma", EnemyMaterial.Madeira => "madeira", _ => "carne",
        };

        // Multiplicador de dano do tipo contra o material.
        public static float Multiplier(EnemyMaterial m, DamageType t) => (m, t) switch
        {
            (EnemyMaterial.Couro, DamageType.Corte) => 0.7f,
            (EnemyMaterial.Couro, DamageType.Contusao) => 0.85f,

            (EnemyMaterial.Osso, DamageType.Corte) => 0.6f,          // espada desliza no osso
            (EnemyMaterial.Osso, DamageType.CorteMachado) => 0.9f,
            (EnemyMaterial.Osso, DamageType.Perfuracao) => 0.4f,     // furar costela vazia não adianta
            (EnemyMaterial.Osso, DamageType.Contusao) => 1.5f,       // maça quebra esqueleto

            (EnemyMaterial.Placa, DamageType.Corte) => 0.4f,
            (EnemyMaterial.Placa, DamageType.CorteMachado) => 0.85f, // machado vence armadura
            (EnemyMaterial.Placa, DamageType.Perfuracao) => 0.75f,   // ponta acha as frestas
            (EnemyMaterial.Placa, DamageType.Contusao) => 0.9f,

            (EnemyMaterial.Gosma, DamageType.Corte) => 1.1f,
            (EnemyMaterial.Gosma, DamageType.Perfuracao) => 0.6f,
            (EnemyMaterial.Gosma, DamageType.Contusao) => 0.8f,

            (EnemyMaterial.Madeira, DamageType.Corte) => 0.75f,
            (EnemyMaterial.Madeira, DamageType.CorteMachado) => 1.25f,
            (EnemyMaterial.Madeira, DamageType.Perfuracao) => 0.6f,
            _ => 1f,
        };

        // Nocaute: pancada forte na cabeça atordoa (o chefe não; ele usa a postura).
        public const float KnockoutMinSpeed = 4.5f, KnockoutSeconds = 1.3f;
        public static bool Knocksout(EnemyKind k, DamageType t, bool head, float speed) =>
            t == DamageType.Contusao && head && speed >= KnockoutMinSpeed
            && k != EnemyKind.ReiGoblin && k != EnemyKind.Slime && k != EnemyKind.Boneco && k != EnemyKind.Mimico;

        // Golpe fatal: estocada funda (rápida) na cabeça mata inimigo comum na hora.
        public const float FatalStabMinSpeed = 3f;
        public static bool IsFatalStab(EnemyKind k, bool head, float tipSpeed) =>
            head && tipSpeed >= FatalStabMinSpeed && (k == EnemyKind.Goblin || k == EnemyKind.Arqueiro);
    }
}
