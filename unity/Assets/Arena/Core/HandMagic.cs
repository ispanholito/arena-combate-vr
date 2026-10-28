using System;

namespace Arena.Core
{
    // Mago sem controle (pedido do Pablo, 2026-10-28): poderes de mão vazia com gestos das DUAS mãos ou de uma.
    //   - Tudo das duas mãos começa com uma PALMA BATIDA (2026-10-28, depois do 1º teste):
    //     - Raízes: bateu a palma, separa e joga as duas mãos rápido para baixo → raízes prendem os inimigos à frente.
    //     - Kamehameha: bateu a palma e mantém os pulsos juntos (carrega) → empurrar as duas mãos para frente.
    //   - Bola de fogo: punho fechado esticado para frente → abrir a mão carrega a bola na palma → jogar a mão para frente.
    public static class HandMagicRules
    {
        // ---------- Raízes ----------
        // ---------- Palma batida (abre a janela das magias de duas mãos) ----------
        public const float ClapDistance = 0.1f;        // palmas a até 10 cm
        public const float ClapCloseSpeed = 0.6f;      // m/s com que as palmas se aproximam
        public const float ClapWindow = 1.5f, ClapCooldown = 0.4f;

        public static bool IsClap(float palmsApart, float closingSpeed) => palmsApart <= ClapDistance && closingSpeed >= ClapCloseSpeed;

        // ---------- Raízes: depois da palma, separar e jogar as duas mãos para baixo ----------
        public const float RootsSplit = 0.2f;          // palmas a mais de 20 cm (separou)
        public const float RootsDownSpeed = 1.6f;      // m/s para baixo, as duas mãos (relativo ao corpo)
        public const float RootsDistance = 3.5f, RootsRadius = 4f, RootsSeconds = 3f, RootsMana = 25f, RootsCooldown = 6f;

        public static bool RootsSlam(float palmsApart, float leftVy, float rightVy) =>
            palmsApart >= RootsSplit && leftVy <= -RootsDownSpeed && rightVy <= -RootsDownSpeed;

        // ---------- Bola de fogo teleguiada: o alvo é o inimigo mais perto da DIREÇÃO do arremesso (não o mais perto de você) ----------
        public const float HomingCone = 30f;           // graus em volta da direção
        public const float HomingRange = 35f;
        public const float HomingTurn = 240f;          // graus/s que a bola consegue virar

        // Ângulos (graus) e distâncias de cada inimigo em relação ao arremesso; devolve o índice do alvo ou -1.
        // Menor ângulo vence; só em empate (menos de 4° de diferença) o mais perto ganha.
        public static int PickHomingTarget(float[] anglesDeg, float[] distances)
        {
            int best = -1;
            for (int i = 0; i < anglesDeg.Length; i++)
            {
                if (anglesDeg[i] > HomingCone || distances[i] > HomingRange) continue;
                if (best < 0) { best = i; continue; }
                float da = anglesDeg[i] - anglesDeg[best];
                if (da < -4f || (Math.Abs(da) <= 4f && distances[i] < distances[best])) best = i;
            }
            return best;
        }

        // ---------- Kamehameha ----------
        public const float KameWristsTogether = 0.16f, KamePalmsTogether = 0.25f;
        public const float KameMinCharge = 0.6f, KameFullCharge = 1.6f;
        public const float KamePushSpeed = 2f;        // m/s das mãos para frente (relativo ao corpo) para soltar
        public const float KameRange = 25f, KameRadius = 0.5f, KameMana = 35f, KameCooldown = 6f, KameKnock = 7f;
        public const float KameMinHeightBelowHead = 0.05f, KameMaxHeightBelowHead = 1f;   // mãos na altura do tronco

        public static bool KamePose(float wristsApart, float palmsApart, float belowHead) =>
            wristsApart <= KameWristsTogether && palmsApart <= KamePalmsTogether
            && belowHead >= KameMinHeightBelowHead && belowHead <= KameMaxHeightBelowHead;

        public static float Charge01(float seconds, float min, float full) =>
            seconds < min ? 0f : Math.Min(1f, (seconds - min) / Math.Max(0.01f, full - min) * 0.7f + 0.3f);

        public static float KameDamage(float charge01) => 40f + 45f * Math.Clamp(charge01, 0f, 1f);

        // ---------- Bola de fogo (uma mão) ----------
        public const float FireExtendForward = 0.35f;  // mão a 35 cm à frente da cabeça (braço esticado)
        public const float FireArmHold = 0.12f, FireOpenWindow = 1.2f;
        public const float FireMinCharge = 0.4f, FireFullCharge = 1.5f;
        public const float FireThrowSpeed = 2.5f;      // m/s da mão para soltar
        public const float FireMana = 18f, FireCooldown = 0.8f;

        // Potência 0..1 da bola de fogo pelo tempo carregando (0 = não carregou o mínimo).
        public static float FirePower(float seconds) => Charge01(seconds, FireMinCharge, FireFullCharge);

        // ---------- Teia por pontos de gancho ----------
        public const float GrappleGazeAngle = 12f;     // olhar a até 12° do ponto acende ele
        public const float GrappleRange = 18f;
        public const float GrappleHandAngle = 35f;     // a mão esticada tem que apontar para o ponto (até 35°)
        public const float GrappleExtend = 0.3f;       // braço esticado: mão a 30 cm da cabeça

        // ---------- Embalo (o jogador continua voando quando para de puxar/solta a teia) ----------
        public const float PullGain = 2.5f;            // velocidade = velocidade do puxão da mão × 2,5
        public const float MaxAirSpeed = 10f;
        public const float HangDamping = 4f;           // segurando parado na teia: freia (1/s) depois de HangCoast
        public const float HangCoast = 0.35f;          // até 0,35 s sem puxar ainda mantém o embalo
        public const float GroundFriction = 10f;       // no chão o embalo some rápido
        public const float AirDrag = 0.15f;

        // Integra a velocidade no ar por um passo. Retorna (velocidade, altura).
        public static (float vx, float vy, float vz) AirStep(float vx, float vy, float vz, bool hanging, bool coasting, bool onGround, float dt)
        {
            if (hanging)
            {
                if (!coasting) { float k = (float)Math.Exp(-HangDamping * dt); vx *= k; vy *= k; vz *= k; }
                return (vx, vy, vz);
            }
            vy -= RopeRules.Gravity * dt;
            float drag = (float)Math.Exp(-(onGround ? GroundFriction : AirDrag) * dt);
            vx *= drag; vz *= drag;
            return (vx, vy, vz);
        }
    }
}
