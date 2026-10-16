using System;

namespace Arena.Core
{
    // Chefe estilo Souls (pedido do Pablo): luta só contra ele, difícil, padrões que se aprendem e janelas claras.
    // Ficar colado batendo não funciona mais: o chefe se defende (dano reduzido fora da postura quebrada), pisa em
    // quem fica colado e o rugido joga longe. O dano de verdade entra quando a postura quebra (aparo + golpes).
    public static class BossRules
    {
        public const float GuardDamageMultiplier = 0.55f;   // fora da postura quebrada
        public static float DamageTakenMultiplier(bool postureBroken) => postureBroken ? 1f : GuardDamageMultiplier;
        // Golpe comum enche só metade da postura do chefe: quem quebra a postura de verdade é o APARO (55 de uma vez).
        public const float HitPostureMultiplier = 0.5f;

        // Rugido (golpes seguidos sem quebrar a postura): aviso curto e onda que joga o jogador longe.
        public const float RoarWindup = 0.5f, RoarRadius = 4f, RoarPush = 24f, RoarDamage = 15f;   // empurrão de 24 m/s ≈ 4 m de voo

        // Pisão contra quem fica colado: depois de HugSeconds acumulados a menos de HugRange, pisão rápido em volta.
        public const float HugRange = 1.7f, HugSeconds = 1.2f, HugDecay = 0.6f;
        public const float StompWindup = 0.4f, StompRadius = 2.3f, StompDamage = 18f, StompPush = 12f, StompCooldown = 3f;

        // Investida quando o jogador foge para longe; errar deixa o chefe tropeçando (janela de ataque).
        public const float ChargeMinDistance = 5f, ChargeWindup = 0.7f, ChargeSpeed = 9f, ChargeMaxSeconds = 0.9f;
        public const float ChargeDamage = 22f, ChargeHitRadius = 1.1f, ChargeCooldown = 7f, ChargeMissStumble = 1.2f;

        // Combos: fase 1 golpes únicos; fase 2 sequências de 2-3 com aviso curto entre eles, e pausa longa no fim.
        public const float ComboWindup = 0.35f, ComboEndRecover = 1.3f;
        public static int ComboLength(int phase, double roll) => phase < 2 ? 1 : roll < 0.5 ? 2 : 3;
    }

    // Medidor de "colado": sobe enquanto o jogador está perto, desce devagar quando ele se afasta.
    public sealed class HugMeter
    {
        public float Value { get; private set; }

        // Devolve true quando passa do limite (e zera).
        public bool Tick(float dt, float distance)
        {
            Value = distance < BossRules.HugRange ? Value + dt : Math.Max(0f, Value - dt * BossRules.HugDecay);
            if (Value < BossRules.HugSeconds) return false;
            Value = 0f;
            return true;
        }

        public void Reset() => Value = 0f;
    }
}
