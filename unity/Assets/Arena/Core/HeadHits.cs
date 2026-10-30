using System;

namespace Arena.Core
{
    public enum HeadZone { Cranio, Rosto, Pescoco }
    public enum HeadOutcome { Nada, Derruba, Atordoa, Mata, Decapita }

    // Golpe na cabeça por região e tipo de arma (Pablo, 2026-10-30: "qualquer arma na cabeça derruba na hora").
    //   Contundente (maça, martelo, frigideira...): inimigo pequeno cai em 85%; grande (orc) fica atordoado em 65%.
    //   Lâmina (tudo que tem fio):
    //     - crânio: não derruba; com capacete, 30% de atordoar;
    //     - rosto/boca: corta e dá bastante dano;
    //     - pescoço: decapita se o corte for bem dado e forte; senão pega a veia e mata (pequenos de carne).
    public static class HeadHitRules
    {
        public const float BluntKnockChanceSmall = 0.85f, BluntStunChanceBig = 0.65f, BladeHelmetStunChance = 0.3f;
        public const float MinSpeed = 3.5f;            // tapinha não derruba nem atordoa
        public const float StunSeconds = 1.4f;
        public const float JugularMinSpeed = 3.5f, JugularMinEdge = 0.4f;

        // up01: altura do ponto na cabeça (0 = junta do pescoço, 1 = topo); forward: quanto está para a frente da cara
        // (-1..1, relativo ao raio da cabeça).
        public static HeadZone Zone(float up01, float forward)
        {
            if (up01 < 0.22f) return HeadZone.Pescoco;
            if (up01 < 0.55f && forward > 0.35f) return HeadZone.Rosto;
            return HeadZone.Cranio;
        }

        public static float DamageMultiplier(HeadZone z, bool blunt, bool helmet) => z switch
        {
            HeadZone.Pescoco => 2f,
            HeadZone.Rosto => blunt ? 1.5f : helmet ? 1.6f : 2.2f,
            _ => blunt ? (helmet ? 1f : 1.6f) : (helmet ? 0.5f : 1.3f),
        };

        // big = inimigo grande (orc); bones = sem veia para cortar (esqueleto); decapitates = o corte passa na regra de
        // arrancar a cabeça daquele inimigo (DismemberRules). roll = sorteio 0..1.
        public static HeadOutcome Resolve(HeadZone z, bool blunt, bool helmet, bool big, bool bones, bool decapitates,
                                          float speed, float edge, double roll)
        {
            if (speed < MinSpeed) return HeadOutcome.Nada;
            if (blunt)
            {
                if (big) return roll < BluntStunChanceBig ? HeadOutcome.Atordoa : HeadOutcome.Nada;
                return roll < BluntKnockChanceSmall ? HeadOutcome.Derruba : HeadOutcome.Nada;
            }
            if (z == HeadZone.Pescoco)
            {
                if (decapitates) return HeadOutcome.Decapita;
                if (!bones && !big && edge >= JugularMinEdge && speed >= JugularMinSpeed) return HeadOutcome.Mata;
                return HeadOutcome.Nada;
            }
            if (z == HeadZone.Cranio && helmet && roll < BladeHelmetStunChance) return HeadOutcome.Atordoa;
            return HeadOutcome.Nada;
        }
    }
}
