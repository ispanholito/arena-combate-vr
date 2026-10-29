using System;

namespace Arena.Core
{
    // Movimento e reação a dano dos inimigos humanoides (pedido do Pablo, 2026-10-29: "só andam reto", "reação a
    // dano paia"). Antes: velocidade instantânea em linha reta até o jogador e uma inclinação pequena ao apanhar.
    //   - Aceleração/freada (não teleporta de velocidade), passos com balanço.
    //   - Aproximação em CURVA: cada um escolhe um ângulo de chegada (±20-45°) que some perto do jogador.
    //   - Ritmo: corre de longe, anda desconfiado na média distância (ziguezague), para e mede antes de atacar.
    //   - Esquiva: arma do jogador vindo rápido perto do corpo → passo para o lado/trás (chance pela dificuldade).
    //   - Tranco ao apanhar: o corpo joga para trás (inclina pelo tamanho do golpe), trava um instante e recua uns
    //     passos; golpe grande = cambaleia mais. Chefes não (têm as próprias regras).
    public static class EnemyMotion
    {
        public const float Accel = 9f, Decel = 14f;

        // Ângulo de chegada (graus) sorteado por inimigo: lados alternados, 20° a 45°.
        public static float PickApproachAngle(double roll) => (float)((roll < 0.5 ? -1 : 1) * (20 + 25 * Math.Abs(roll * 2 - 1)));
        // Some conforme chega perto (abaixo de 2,2 m vem de frente).
        public static float ApproachAngle(float pickedDeg, float distance) => pickedDeg * Clamp01((distance - 2.2f) / 5f);

        // Velocidade relativa: corre de longe, anda desconfiado na média distância.
        public static float SpeedFactor(float distance) => distance > 6f ? 1.3f : distance > 3.5f ? 1f : 0.8f;
        // Ziguezague lateral (0..±0,5 da velocidade) na média distância.
        public static float Weave(float time, float seed, float distance) =>
            distance > 2f && distance < 6.5f ? (float)Math.Sin(time * 2.2f + seed) * 0.5f : 0f;

        // Esquiva de golpe corpo a corpo.
        public const float DodgeSpeed = 4.5f, DodgeSeconds = 0.3f, DodgeCooldown = 2.2f, DodgeTipSpeed = 4f, DodgeRange = 1.4f;
        public static float MeleeDodgeChance(Difficulty d) => CombatSteering.DodgeChance(d) * 0.6f;   // 9 / 21 / 36%

        // Tranco ao apanhar (fração da vida máxima tirada pelo golpe).
        public static float FlinchSeconds(float damageFraction) => damageFraction < 0.06f ? 0.18f : damageFraction < 0.15f ? 0.3f : 0.55f;
        public static float FlinchTiltDeg(float damageFraction) => Math.Min(38f, 12f + damageFraction * 160f);
        public static float StumbleSpeed(float damageFraction) => damageFraction < 0.06f ? 0.5f : damageFraction < 0.15f ? 1.2f : 2f;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
