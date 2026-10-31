using System;

namespace Arena.Core
{
    // Aura vermelha (2026-10-30, roteiro da demo: substitui a Postura de Contra). Gatilho → a arma fica vermelha por
    // alguns segundos: mais dano e QUEBRA DEFESAS — bloqueio com arma não segura e escudo racha muito mais rápido.
    // Na demo é a recompensa do orc (sala 5); a lâmina em chamas fica com o dano ao longo do tempo.
    public static class RedAuraRules
    {
        public const float Duration = 8f;
        public const float DamageMultiplier = 1.25f;
        public const float PostureMultiplier = 1.5f;
        public const float GuardBreakStun = 0.9f;   // bater na guarda com a aura: "DEFESA QUEBRADA!" e atordoa
    }

    // Escudo de inimigo (orc da sala 5, Rei Goblin): bloqueia o golpe que bate nele, racha e QUEBRA de verdade.
    // Com a aura: 3-4 golpes; sem: bem mais (não pode travar quem não tem a aura).
    public sealed class EnemyShieldState
    {
        public const float MinSpeed = 2.5f;          // encostar não conta
        public const float AuraChip = 32f, PlainChip = 8f;
        public float MaxHp { get; }
        public float Hp { get; private set; }
        public bool Broken => Hp <= 0f;
        public float Fraction => MaxHp <= 0f ? 0f : Math.Max(0f, Hp / MaxHp);
        // Rachaduras: 0 = inteiro, 1 = trincado, 2 = rachado, 3 = quase quebrando.
        public int Stage => Broken ? 3 : Fraction > 0.75f ? 0 : Fraction > 0.45f ? 1 : Fraction > 0.2f ? 2 : 3;

        public EnemyShieldState(float hp = 100f) { MaxHp = hp; Hp = hp; }

        // Quanto o golpe tira do escudo: aura muito mais; arma pesada e pancada um pouco mais; golpe forte mais.
        public static float Chip(float speed, bool aura, bool heavy, bool blunt)
        {
            if (speed < MinSpeed) return 0f;
            float s = Math.Clamp(speed / 5f, 0.6f, 1.6f);
            return (aura ? AuraChip : PlainChip) * (heavy ? 1.5f : 1f) * (blunt ? 1.3f : 1f) * s;
        }

        // Devolve true se este golpe quebrou o escudo.
        public bool Hit(float speed, bool aura, bool heavy, bool blunt)
        {
            if (Broken) return false;
            Hp -= Chip(speed, aura, heavy, blunt);
            return Broken;
        }
    }
}
