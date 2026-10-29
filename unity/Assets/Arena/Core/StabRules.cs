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
        public const float MinSpeed = 1.3f;            // m/s na ponta (era 1,8: o Pablo achava difícil furar)
        public const float MinAlignment = 0.5f;        // cos do ângulo entre a lâmina e o movimento (~60°)
        public const float TipZone = 0.35f;            // o contato precisa ser até 35 cm da ponta
        public const float DamageMultiplier = 1.2f;
        public const float BleedDps = 4f;
        public const float BleedTick = 1f;

        public static StabStyle StyleFor(EnemyKind kind) =>
            kind == EnemyKind.ReiGoblin || kind == EnemyKind.Boneco || kind == EnemyKind.Mimico || kind == EnemyKind.Orc ? StabStyle.Embed : StabStyle.Skewer;

        public static bool CanPierce(WeaponKind kind) => kind == WeaponKind.Espada || kind == WeaponKind.Lanca || kind == WeaponKind.Adaga;

        public static bool CanStab(WeaponKind kind, float tipSpeed, float alignment, float contactDistanceFromTip) =>
            (kind == WeaponKind.Espada || kind == WeaponKind.Lanca || kind == WeaponKind.Adaga) && tipSpeed >= MinSpeed && alignment >= MinAlignment && contactDistanceFromTip <= TipZone;

        public static float StabDamage(float tipSpeed, WeaponDef weapon, bool sharpEdge, bool hasStamina) =>
            Combat.MeleeDamage(tipSpeed, weapon, sharpEdge, hasStamina, 1f) * DamageMultiplier;

        // Quanto a lâmina pode entrar: até perto da guarda.
        public static float MaxDepth(WeaponDef w) => Math.Max(0.2f, w.Length - 0.2f);

        // ---------- Espetar ----------
        public const float SkewerSlideSeconds = 0.12f;  // tempo para a lâmina atravessar o corpo
        // Ponto da lâmina (distância a partir da ponta) onde fica o centro do inimigo espetado: a ponta sai do outro lado.
        public static float SkewerCenterFromTip(WeaponDef w, float enemyRadius) => Math.Min(MaxDepth(w) - enemyRadius * 0.5f, enemyRadius + 0.08f);
        public const float SkewerWeightKgPerMass = 6f;  // peso que o inimigo espetado faz na mão (kg por unidade de "Mass")
        public const float FlingMinSpeed = 4.5f;        // sacudida rápida PARA O LADO (m/s da ponta, sem contar o eixo da lâmina) arremessa
        public const float FlingMinTime = 0.35f;        // logo depois de espetar não arremessa (o próprio fim da estocada não conta)

        // A lâmina entra e sai DESLIZANDO conforme o jogador empurra/puxa (antes o inimigo era puxado para o meio da
        // lâmina em 0,12 s, parecia teleporte). SlideFollow = quanto do movimento ao longo da lâmina vira deslize; o resto
        // arrasta o inimigo junto (atrito). Para os lados, o inimigo sempre vai com a lâmina.
        public const float SlideFollow = 0.6f, SlideFollowOut = 0.35f;
        public const float SlideDrag = 9f;              // freio (1/s) no movimento da arma ao longo da lâmina enquanto atravessa
        public const float GuardGap = 0.15f;            // o inimigo para antes da guarda

        // ---------- Penetração estilo Blade & Sorcery ----------
        // A velocidade do impacto continua empurrando a lâmina para dentro e um amortecedor freia (golpe forte entra fundo).
        public const float InitialVelocityMultiplier = 0.6f;
        public const float PenetrationDamper = 9f;      // 1/s
        // Pressão: ponta encostada empurrando devagar e alinhada também entra.
        public const float PressureMinAxialSpeed = 0.25f, PressureMinAlignment = 0.75f, PressureHoldSeconds = 0.2f;
        // Passar a ponta do centro do corpo = perfuração funda (dano extra uma vez); passar do outro lado = atravessou.
        public const float DeepDamageMultiplier = 0.6f;
        // Corpo morto pendurado na lâmina: mais leve que o ragdoll normal (senão a espada vai ao chão), freio ao deslizar e
        // a junta quebra numa sacudida de ~PinnedBreakG vezes o peso.
        public const float PinnedCorpseMassScale = 0.35f, PinnedSlideDamper = 25f, PinnedBreakG = 6f;

        public static float ClampZ(float z, float enemyRadius, float bladeLength) =>
            Math.Max(GuardGap + enemyRadius * 0.6f, Math.Min(z, bladeLength + enemyRadius));

        // Avança a penetração por um passo: devolve o novo z e a nova velocidade de penetração.
        public static (float z, float vp) Penetrate(float z, float vp, float dt, float enemyRadius, float bladeLength)
        {
            z = ClampZ(z - vp * dt, enemyRadius, bladeLength);
            vp *= (float)Math.Exp(-PenetrationDamper * dt);
            return (z, vp);
        }

        public static bool IsDeep(float z, float bladeLength) => z <= bladeLength;                       // a ponta passou do centro
        public static bool IsThrough(float z, float enemyRadius, float bladeLength) => z <= bladeLength - enemyRadius;   // saiu do outro lado

        // Nova posição (ao longo da lâmina, a partir da mão) do centro do inimigo espetado.
        // zPrev = onde estava; zFree = onde estaria se não se mexesse no mundo (a lâmina andou por baixo dele).
        public static float SlideZ(float zPrev, float zFree, float enemyRadius, float bladeLength)
        {
            // Puxar para fora desliza menos (o inimigo vem junto); empurrar entra mais fácil.
            float z = zPrev + (zFree - zPrev) * (zFree > zPrev ? SlideFollowOut : SlideFollow);
            return Math.Max(GuardGap + enemyRadius * 0.6f, Math.Min(z, bladeLength + enemyRadius));
        }

        // A ponta saiu do corpo: o inimigo solta no lugar (puxar a espada para trás de verdade).
        public static bool PulledOut(float z, float enemyRadius, float bladeLength) => z >= bladeLength + enemyRadius - 0.005f;
        // Logo ao espetar a ponta já está 6 cm dentro (senão o primeiro quadro já contaria como "saiu").
        public static float StartZ(float zNow, float enemyRadius, float bladeLength) => Math.Min(zNow, bladeLength + enemyRadius - 0.06f);
        public const float SlideOffBack = 0.15f;        // puxar a espada para trás 15 cm solta o inimigo no lugar

        public static float FlingMultiplier(EnemyKind kind) => kind switch
        {
            EnemyKind.Slime => 1.6f,                    // leve: voa longe
            EnemyKind.Goblin => 0.9f,
            EnemyKind.Arqueiro => 1f,
            EnemyKind.Xama => 1f,
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
