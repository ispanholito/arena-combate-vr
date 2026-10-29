namespace Arena.Core
{
    // Espada flamejante (ideia do Pablo, 2026-10-28): mergulhar a lâmina numa bola de fogo (a que carrega na mão livre
    // ou uma voando) acende a arma. Enquanto queima: +30% de dano e cada golpe põe calor no inimigo (pega fogo).
    // Vale para qualquer arma de golpe; o cajado não (ele já é a fonte da magia).
    public static class FlameBladeRules
    {
        public const float Seconds = 12f;
        public const float DamageMultiplier = 1.3f;
        public const float HeatPerHit = 50f;       // 2 golpes acendem um inimigo comum (limiar do calor = 100)
        public const float DipMargin = 0.06f;      // folga além do raio da bola para contar o mergulho

        public static bool CanIgnite(WeaponKind kind) => kind != WeaponKind.Cajado;

        // Chama acesa: renova até o máximo (mergulhar de novo não acumula além de Seconds).
        public static float Refresh(float remaining) => Seconds > remaining ? Seconds : remaining;
    }
}
