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

        // Chegada RETA (Pablo, 2026-10-30: "não precisam vir pelas laterais, podem vir reto"). Só dentro de EngageRange
        // eles rondam: ziguezague de lado, se afastam e se aproximam (Pace) e, em grupo, cercam (vagas).
        public const float EngageRange = 3.5f;
        public static bool Engaged(float distance) => distance <= EngageRange;
        // Mantidos para compatibilidade: o ângulo de chegada agora é sempre 0 (reto).
        public static float PickApproachAngle(double roll) => (float)((roll < 0.5 ? -1 : 1) * (20 + 25 * Math.Abs(roll * 2 - 1)));
        public static float ApproachAngle(float pickedDeg, float distance) => 0f;
        // Vai e volta (m/s, ao longo da linha até o jogador) enquanto ronda de perto: + = se aproxima.
        public static float Pace(float time, float seed, float distance) =>
            distance > 1.4f && distance <= EngageRange ? (float)Math.Sin(time * 1.3f + seed * 1.7f) * 0.6f : 0f;

        // Velocidade relativa: corre de longe, anda desconfiado na média distância.
        public static float SpeedFactor(float distance) => distance > 6f ? 1.3f : distance > 3.5f ? 1f : 0.8f;
        // Ziguezague lateral (0..±0,5 da velocidade) na média distância.
        public static float Weave(float time, float seed, float distance) =>
            distance > 1.4f && distance <= EngageRange ? (float)Math.Sin(time * 2.2f + seed) * 0.5f : 0f;

        // Esquiva de golpe corpo a corpo.
        public const float DodgeSpeed = 4.5f, DodgeSeconds = 0.3f, DodgeCooldown = 2.2f, DodgeTipSpeed = 4f, DodgeRange = 1.4f;
        public static float MeleeDodgeChance(Difficulty d) => CombatSteering.DodgeChance(d) * 0.6f;   // 9 / 21 / 36%

        // Tranco ao apanhar (fração da vida máxima tirada pelo golpe).
        public static float FlinchSeconds(float damageFraction) => damageFraction < 0.06f ? 0.18f : damageFraction < 0.15f ? 0.3f : 0.55f;
        public static float FlinchTiltDeg(float damageFraction) => Math.Min(38f, 12f + damageFraction * 160f);
        public static float StumbleSpeed(float damageFraction) => damageFraction < 0.06f ? 0.5f : damageFraction < 0.15f ? 1.2f : 2f;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // Ciclo de passo dos inimigos físicos (2026-10-30, roteiro biomecânico de andar/correr mandado pelo Pablo).
    // O tamanho e o RITMO do passo saem da velocidade, para o pé no chão andar para trás na mesma velocidade em que o
    // corpo vai para frente (sem escorregar). Um ciclo = 2 passos = 2π de fase.
    //   Andar: passo médio, joelho de apoio quase reto, o da perna no ar dobra ~55°, cotovelos levemente dobrados.
    //   Correr (Run → 1): passo maior até o limite da coxa, calcanhar sobe (joelho no ar ~105°), cotovelos ~85°,
    //   tronco inclina mais, a bacia afunda no apoio médio (não entre os passos, como no andar).
    public static class GaitRules
    {
        public const float LegLength = 0.46f;        // quadril → pé do goblin (× escala)
        public const float MaxThighDeg = 42f;        // amplitude máxima da coxa (para frente e para trás)
        public const float MinSpeed = 0.08f;

        public static float Run(float speed) => Clamp01((speed - 1.3f) / 1.1f);
        // Passo desejado (m): cresce com a velocidade; limitado pelo que a coxa alcança.
        public static float StepLength(float speed, float scale)
        {
            float want = (0.24f + 0.3f * speed) * scale;
            float max = 2f * LegLength * scale * (float)Math.Sin(MaxThighDeg * Math.PI / 180.0);
            return Math.Min(want, max);
        }
        // Velocidade da fase (rad/s): um passo (meio ciclo) a cada StepLength andado.
        public static float PhaseRate(float speed, float scale) =>
            speed < MinSpeed ? 0f : (float)Math.PI * speed / Math.Max(0.05f, StepLength(speed, scale));
        // Amplitude da coxa (graus) para o passo dar StepLength.
        public static float ThighAmplitudeDeg(float speed, float scale)
        {
            if (speed < MinSpeed) return 0f;
            double half = StepLength(speed, scale) * 0.5 / (LegLength * scale);
            return (float)(Math.Asin(Math.Min(0.95, half)) * 180.0 / Math.PI);
        }
        public static float SwingKneeDeg(float speed) => 50f + 55f * Run(speed);        // perna no ar
        public static float StanceKneeDeg(float speed) => 6f + 12f * Run(speed);        // perna de apoio (absorve)
        public static float ElbowDeg(float speed) => 25f + 60f * Run(speed);            // cotovelos
        public static float TorsoLeanDeg(float speed) => Math.Min(24f, 4f + 6f * speed + 6f * Run(speed));
        // Altura da bacia (m, negativo = desce) na fase: andar = mais baixa entre os passos; correr = no apoio médio.
        public static float PelvisBob(float phase, float speed, float scale)
        {
            if (speed < MinSpeed) return 0f;
            float c = Math.Abs((float)Math.Cos(phase));
            float run = Run(speed);
            float walk = -(1f - c) * 0.03f, runB = -c * 0.05f;
            return (walk + (runB - walk) * run) * scale * Clamp01(speed / 0.6f);
        }
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
