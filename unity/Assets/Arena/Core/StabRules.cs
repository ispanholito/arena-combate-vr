using System;

namespace Arena.Core
{
    // Fase 3: cravar a lâmina. Só estocadas (lâmina alinhada com o movimento) com a ponta entram e ficam presas.
    public static class StabRules
    {
        public const float MinSpeed = 2.0f;            // m/s na ponta
        public const float MinAlignment = 0.8f;        // cos do ângulo entre a lâmina e o movimento (~37°)
        public const float TipZone = 0.3f;             // o contato precisa ser até 30 cm da ponta
        // Quanto a lâmina pode entrar: até perto da guarda (a espada pode atravessar o goblin de lado a lado).
        public static float MaxDepth(WeaponDef w) => Math.Max(0.2f, w.Length - 0.2f);
        public const float Friction = 25f;             // amortecimento ao deslizar dentro do corpo (N·s/m); era 80 e só entrava a ponta
        public const float ExitDepth = -0.02f;         // voltou para fora da entrada = soltou
        public const float RipSeparation = 0.45f;      // mão a mais de 45 cm da arma presa = arrancou
        public const float DamageMultiplier = 1.2f;
        public const float BleedDps = 4f;
        public const float BleedTick = 1f;             // aplica o sangramento a cada 1 s
        public const float RipDamage = 8f;
        public const float DragStartSeparation = 0.12f;
        public const float DragGain = 4f;              // m/s de arrasto por metro de separação
        public const float MaxDragSpeed = 1.5f;

        public static bool CanStab(WeaponKind kind, float tipSpeed, float alignment, float contactDistanceFromTip) =>
            kind == WeaponKind.Espada && tipSpeed >= MinSpeed && alignment >= MinAlignment && contactDistanceFromTip <= TipZone;

        public static float StabDamage(float tipSpeed, WeaponDef weapon, bool sharpEdge, bool hasStamina) =>
            Combat.MeleeDamage(tipSpeed, weapon, sharpEdge, hasStamina, 1f) * DamageMultiplier;

        public static bool Exited(float depth) => depth < ExitDepth;
        public static bool Ripped(float handSeparation) => handSeparation > RipSeparation;

        public static float DragSpeed(float handSeparation) =>
            handSeparation <= DragStartSeparation ? 0f : Math.Min(MaxDragSpeed, (handSeparation - DragStartSeparation) * DragGain);
    }
}
