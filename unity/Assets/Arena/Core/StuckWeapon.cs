using System;

namespace Arena.Core
{
    // Arma cravada num corpo (Pablo, 2026-10-30: "a espada precisa ir saindo, não só desgrudar"). Segurar o cabo e
    // puxar ao longo da lâmina faz ela DESLIZAR para fora aos poucos: a lâmina anda menos que a mão (atrito, mais
    // preso no começo), nunca volta para dentro e solta quando a ponta sai. Mão longe do cabo = escorregou.
    public static class ExtractRules
    {
        public const float Depth = 0.32f;          // quanto da lâmina está dentro (m)
        public const float TightZone = 0.06f;      // primeiros cm: bem preso
        public const float MaxSpeed = 0.7f;        // m/s: mesmo puxando rápido, sai no máximo nisso
        public const float SlipDistance = 0.3f;    // mão a mais disso do cabo = soltou
        public const float GrabRange = 0.18f;      // mão até o cabo para pegar
        public const float DesktopSpeed = 0.22f;   // desktop: segurando E, sai sozinho nessa velocidade

        // Fração do puxão que vira deslize (o resto é a mão escorregando no cabo/corpo cedendo).
        public static float Yield(float extracted) => extracted < TightZone ? 0.4f : 0.8f;

        // Avança a extração pelo quanto a mão passou do ponto mais longe já puxado (catraca: puxar, voltar e puxar de
        // novo continua de onde parou). pull = avanço da mão ao longo do eixo de saída desde o último máximo (m).
        public static float Step(float extracted, float pull, float dt)
        {
            if (pull <= 0f || extracted >= Depth) return extracted;
            float move = Math.Min(pull * Yield(extracted), MaxSpeed * dt);
            return Math.Min(Depth, extracted + move);
        }

        public static bool IsFree(float extracted) => extracted >= Depth - 1e-4f;
    }
}
