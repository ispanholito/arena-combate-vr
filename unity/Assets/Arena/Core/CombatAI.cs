using System;
using System.Collections.Generic;

namespace Arena.Core
{
    // IA de combate inspirada no estudo do Blade & Sorcery (docs/estudo-blade-and-sorcery.md): a dificuldade muda a
    // AGRESSIVIDADE (quantos atacam juntos, intervalo entre ataques, rapidez do revide, quanto dano interrompe), não só
    // vida e dano. Números nossos, ajustar jogando.
    public static class CombatAI
    {
        // Fichas de ataque: quantos inimigos corpo a corpo podem atacar ao mesmo tempo.
        // 2026-10-18 (Pablo): menos gente rondando, mais atacando — fácil 2, normal 3, difícil 5 ao mesmo tempo.
        public static int AttackTokens(Difficulty d) => d switch { Difficulty.Facil => 2, Difficulty.Dificil => 5, _ => 3 };

        // Intervalo extra (s) antes de começar um ataque, sorteado entre min e max.
        public static (float min, float max) AttackDelay(Difficulty d) => d switch
        {
            Difficulty.Facil => (1.5f, 2.5f),
            Difficulty.Dificil => (0f, 0.4f),
            _ => (0.4f, 1.0f),
        };

        // Revide depois de bloquear o jogador: espera (s) antes do contra-ataque, que tem aviso mais curto.
        public static (float min, float max) RiposteDelay(Difficulty d) => d switch
        {
            Difficulty.Facil => (1.2f, 1.8f),
            Difficulty.Dificil => (0.3f, 0.7f),
            _ => (0.5f, 1.1f),
        };
        public const float RiposteWindupMultiplier = 0.6f;
        public static float RiposteChance(Difficulty d) => d switch { Difficulty.Facil => 0.35f, Difficulty.Dificil => 0.85f, _ => 0.6f };

        // Dano (fração da vida máxima) que cancela o ataque anunciado do inimigo.
        public static float InterruptFraction(Difficulty d) => d switch { Difficulty.Facil => 0.05f, Difficulty.Dificil => 0.12f, _ => 0.08f };
        public static bool Interrupts(float damage, float maxHp, Difficulty d) => maxHp > 0f && damage >= maxHp * InterruptFraction(d);

        // Janela de combo: logo depois de levar dano o inimigo não consegue bloquear.
        public const float ComboWindow = 0.75f;
        public static bool CanBlockAfterHit(float secondsSinceDamaged) => secondsSinceDamaged >= ComboWindow;

        public static float Pick((float min, float max) r, double roll) => r.min + (r.max - r.min) * (float)Math.Clamp(roll, 0, 1);
    }

    // Fichas de ataque compartilhadas: um inimigo pega uma antes de atacar e devolve quando o ataque acaba.
    public sealed class AttackTokenPool
    {
        readonly HashSet<int> holders = new HashSet<int>();
        public int Capacity { get; set; } = 2;
        public int InUse => holders.Count;

        public bool Has(int id) => holders.Contains(id);

        // Pega (ou já tinha). Falha se todas estão em uso.
        public bool TryTake(int id)
        {
            if (holders.Contains(id)) return true;
            if (holders.Count >= Math.Max(1, Capacity)) return false;
            holders.Add(id);
            return true;
        }

        public void Release(int id) => holders.Remove(id);
        public void Clear() => holders.Clear();
    }
}
