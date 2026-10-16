using System;

namespace Arena.Core
{
    // Como a estocada se comporta em cada inimigo.
    public enum StabStyle
    {
        Skewer,   // espetar: a lâmina atravessa e o inimigo fica preso nela; golpe rápido arremessa (slime, goblin)
        Embed,    // cravar: a espada fica presa no monstro e sai da mão; segurar o gatilho para puxar (chefe, boneco)
    }

    // Fase 3 (refeita depois do teste no Quest): estocada com a ponta espeta ou crava.
    public static class StabRules
    {
        // Tolerante: no VR a lâmina fica ~40° inclinada em relação ao controle, e uma estocada natural segue o controle.
        public const float MinSpeed = 1.8f;            // m/s na ponta
        public const float MinAlignment = 0.6f;        // cos do ângulo entre a lâmina e o movimento (~53°)
        public const float TipZone = 0.35f;            // o contato precisa ser até 35 cm da ponta
        public const float DamageMultiplier = 1.2f;
        public const float BleedDps = 4f;
        public const float BleedTick = 1f;

        public static StabStyle StyleFor(EnemyKind kind) =>
            kind == EnemyKind.ReiGoblin || kind == EnemyKind.Boneco ? StabStyle.Embed : StabStyle.Skewer;

        public static bool CanStab(WeaponKind kind, float tipSpeed, float alignment, float contactDistanceFromTip) =>
            kind == WeaponKind.Espada && tipSpeed >= MinSpeed && alignment >= MinAlignment && contactDistanceFromTip <= TipZone;

        public static float StabDamage(float tipSpeed, WeaponDef weapon, bool sharpEdge, bool hasStamina) =>
            Combat.MeleeDamage(tipSpeed, weapon, sharpEdge, hasStamina, 1f) * DamageMultiplier;

        // Quanto a lâmina pode entrar: até perto da guarda.
        public static float MaxDepth(WeaponDef w) => Math.Max(0.2f, w.Length - 0.2f);

        // ---------- Espetar ----------
        public const float SkewerSlideSeconds = 0.12f;  // tempo para a lâmina atravessar o corpo
        // Ponto da lâmina (distância a partir da ponta) onde fica o centro do inimigo espetado: a ponta sai do outro lado.
        public static float SkewerCenterFromTip(WeaponDef w, float enemyRadius) => Math.Min(MaxDepth(w) - enemyRadius * 0.5f, enemyRadius + 0.08f);
        public const float SkewerWeightKgPerMass = 6f;  // peso que o inimigo espetado faz na mão (kg por unidade de "Mass")
        public const float FlingMinSpeed = 3.5f;        // golpe rápido (m/s na ponta) solta o inimigo voando
        public const float SlideOffBack = 0.15f;        // puxar a espada para trás 15 cm solta o inimigo no lugar

        public static float FlingMultiplier(EnemyKind kind) => kind switch
        {
            EnemyKind.Slime => 1.6f,                    // leve: voa longe
            EnemyKind.Goblin => 0.9f,
            _ => 0.5f,
        };

        // ---------- Inimigo arremessado ----------
        public const float ImpactDamagePerSpeed = 6f;   // dano ao bater (por m/s)
        public const float MinImpactSpeed = 2f;         // abaixo disso só cai, sem dano
        public static float ImpactDamage(float speed) => speed < MinImpactSpeed ? 0f : (speed - MinImpactSpeed * 0.5f) * ImpactDamagePerSpeed;

        // ---------- Cravar (chefe/boneco) ----------
        public static float PullSeconds(EnemyKind kind) => kind == EnemyKind.ReiGoblin ? 1.5f : 0.5f;
        public const float PullDecay = 1.5f;            // soltar o gatilho perde o progresso aos poucos
        public const float RipDamage = 15f;             // dano ao arrancar
    }
}
