using System;

namespace Arena.Core
{
    // Sorteio do chefe (inspiração Soul Knight): a sala do chefe e a arena escolhem um dos 4.
    public static class BossPool
    {
        public static readonly EnemyKind[] All = { EnemyKind.ReiGoblin, EnemyKind.RainhaSlime, EnemyKind.ChefeOrc, EnemyKind.Lich };

        // Sorteia; se der o mesmo da última vez, sorteia de novo uma vez (evita repetir seguido sem proibir).
        public static EnemyKind Pick(Random rng, EnemyKind? last = null)
        {
            var k = All[rng.Next(All.Length)];
            if (last.HasValue && k == last.Value) k = All[rng.Next(All.Length)];
            return k;
        }

        public static string Title(EnemyKind k, int phase) => k switch
        {
            EnemyKind.RainhaSlime => phase >= 2 ? "Rainha Slime — Furiosa" : "Rainha Slime",
            EnemyKind.ChefeOrc => phase >= 2 ? "Chefe de Guerra Orc — Fúria" : "Chefe de Guerra Orc",
            EnemyKind.Lich => phase >= 2 ? "Lich — Desperto" : "Lich",
            _ => phase >= 2 ? "Rei Goblin — Fúria" : "Rei Goblin",
        };
    }

    // Rainha Slime: pula e esmaga (círculo de aviso onde você estava), encosta = gosma, divide-se em 66% e 33%
    // (3 slimes) e fica menor e mais rápida.
    public static class SlimeQueenRules
    {
        public const float JumpWindup = 0.8f, JumpAir = 0.9f, JumpRadius = 2.8f, JumpDamage = 24f, JumpCooldown = 3.2f, JumpPush = 10f;
        public const float ContactDamage = 8f, ContactInterval = 1f;
        public const float LandStun = 0.9f;                    // depois de cair: parada (janela de ataque)
        public static readonly float[] SplitAt = { 0.66f, 0.33f };
        public const int SplitSlimes = 3;
        public const float ShrinkPerSplit = 0.82f, SpeedPerSplit = 1.25f;
    }

    // Chefe de Guerra Orc: golpe anunciado aparável (IA do goblin), investida em linha (cai = janela), giro em
    // área (2 voltas) e grito de guerra (postura fica dura por um tempo; ele também recupera um pouco).
    public static class OrcWarlordRules
    {
        public const float ChargeWindup = 0.9f, ChargeSpeed = 10f, ChargeMaxSeconds = 1.1f, ChargeDamage = 28f, ChargeHitRadius = 1.2f, ChargeCooldown = 6f;
        public const float ChargeMinDistance = 4.5f, ChargeMissStun = 1.4f;
        public const float SpinWindup = 0.7f, SpinSeconds = 1.2f, SpinRadius = 3f, SpinDamage = 18f, SpinCooldown = 7f, SpinPush = 9f;
        public const float WarCrySeconds = 6f, WarCryPostureMultiplier = 0.4f, WarCryCooldown = 15f, WarCryHeal = 0.05f;
    }

    // Lich: flutua longe, teleporta, solta 3 orbes rebatíveis em leque (o jeito de machucar ele é REBATER), levanta
    // esqueletos em 66% e 33% (no máximo 2 vivos) e, se você chegar perto, solta uma onda de gelo e some.
    public static class LichRules
    {
        public const float VolleyCooldown = 3.2f, VolleyWindup = 0.9f, VolleySpread = 18f;
        public const int VolleyOrbs = 3;
        public const float TeleportCooldown = 6f, TeleportMin = 6f, TeleportMax = 9f, FadeSeconds = 0.4f;
        public const float NovaRange = 2.5f, NovaWindup = 0.6f, NovaRadius = 3f, NovaDamage = 14f, NovaPush = 14f, NovaSlow = 1.5f;
        public static readonly float[] RaiseAt = { 0.66f, 0.33f };
        public const int RaiseCount = 2, MaxMinions = 2;
        public const float ReflectedOrbBonus = 2f;   // orbe rebatido no Lich: dano dobrado (a mecânica principal)
    }
}
