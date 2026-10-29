using System;

namespace Arena.Core
{
    // Correr balançando os braços (pedido do Pablo, 2026-10-29). Só com as DUAS mãos livres (nenhum controle).
    // Reconhece a corrida pelos braços em OPOSIÇÃO: um vai para frente enquanto o outro vai para trás, com ritmo.
    // Um corte de palma é uma mão só — por isso não confunde. Enquanto corre, os poderes das mãos ficam travados
    // (e o corte da palma também passou a exigir varrida para o LADO, para o primeiro balanço não disparar).
    public sealed class ArmSwingDetector
    {
        public const float MinHandSpeed = 0.5f;      // m/s de cada mão no eixo frente/trás (relativo ao corpo)
        public const float EnterSeconds = 0.35f;     // tempo acumulado em oposição para virar "correndo"
        public const float ExitSeconds = 0.4f;       // sem balanço esse tempo = parou
        public const float SpeedGain = 2.4f, SpeedOffset = 0.5f, MaxSpeed = 5.5f;   // m/s do jogador
        public const float SpeedSmoothing = 6f;

        float score, idle;
        public bool Running { get; private set; }
        public float Speed { get; private set; }     // velocidade atual do corpo (suavizada)
        public bool Swinging { get; private set; }   // este quadro tem balanço em oposição (trava os poderes já)

        // vLeft/vRight: velocidade de cada mão no eixo frente/trás do corpo (m/s); handsLow: as duas abaixo do peito.
        public void Update(float vLeft, float vRight, bool handsLow, float dt)
        {
            Swinging = handsLow && vLeft * vRight < 0f && Math.Abs(vLeft) > MinHandSpeed && Math.Abs(vRight) > MinHandSpeed;
            if (Swinging) { score = Math.Min(EnterSeconds * 2f, score + dt); idle = 0f; }
            else { score = Math.Max(0f, score - dt * 0.5f); idle += dt; }
            if (!Running && score >= EnterSeconds) Running = true;
            if (Running && idle >= ExitSeconds) { Running = false; score = 0f; }
            float target = 0f;
            if (Running && Swinging)
            {
                float avg = (Math.Abs(vLeft) + Math.Abs(vRight)) * 0.5f;
                target = Math.Min(MaxSpeed, Math.Max(0f, (avg - SpeedOffset) * SpeedGain));
            }
            else if (Running) target = Speed;   // entre um balanço e outro mantém o embalo
            Speed += (target - Speed) * Math.Min(1f, SpeedSmoothing * dt);
            if (!Running && Speed < 0.05f) Speed = 0f;
        }

        public void Reset() { score = 0f; idle = 0f; Running = false; Speed = 0f; Swinging = false; }
    }

    public static class PalmSweepRules
    {
        // Corte da palma só com varrida para o LADO: a parte lateral da velocidade tem que ser a maior parte dela.
        public const float MinLateralFraction = 0.6f;
        public static bool IsSweep(float lateralSpeed, float totalSpeed) => totalSpeed > 1e-3f && Math.Abs(lateralSpeed) / totalSpeed >= MinLateralFraction;
    }
}
