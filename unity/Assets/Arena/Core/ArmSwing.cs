using System;

namespace Arena.Core
{
    // Correr balançando os braços (pedido do Pablo, 2026-10-29). Só com as DUAS mãos livres (nenhum controle) e
    // ABERTAS (2ª versão, depois do teste). Reconhece a corrida pelos braços em OPOSIÇÃO no arco da corrida (para
    // frente-e-cima / trás-e-baixo), com ritmo. Enquanto corre, os poderes das mãos ficam travados; o corte da palma
    // só vale com o braço ESTICADO na frente (perto do corpo ou do lado = balanço da corrida, não corta).
    public sealed class ArmSwingDetector
    {
        public const float MinHandSpeed = 0.5f;      // m/s de cada mão no eixo frente/trás (relativo ao corpo)
        public const float EnterSeconds = 0.25f;     // tempo acumulado em oposição para virar "correndo" (2ª versão: responde mais rápido)
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
        // Corte da palma só com o braço ESTICADO à frente (2026-10-29, Pablo): a mão a ≥ 35 cm na frente da cabeça e
        // dentro de 45° do olhar. Mão perto do corpo (corridinha, para cima e para baixo) ou do lado não corta.
        public const float MinForward = 0.35f;
        public static bool IsExtendedInFront(float forward, float lateral) => forward >= MinForward && Math.Abs(lateral) <= forward;
    }
}
