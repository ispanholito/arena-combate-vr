using System;
using System.Collections.Generic;

namespace Arena.Core
{
    // Bênçãos da partida (inspiração: Soul Knight / Hades). Ao limpar algumas salas da dungeon (e a cada N abates na
    // arena) aparece um altar com 3 bênçãos; tocar numa escolhe. Valem só até o fim da partida (sair da dungeon ou
    // morrer na arena) e não vão no salvo. Na dungeon, morrer volta à fogueira e mantém as bênçãos.
    public enum BlessingId
    {
        ForcaBruta,     // +20% de todo dano causado
        PeleDePedra,    // -20% de dano recebido
        CoracaoForte,   // +40 de vida máxima (e cura 40)
        Folego,         // estamina regenera +50%
        MenteClara,     // mana regenera +50%
        Sanguessuga,    // 4% do dano causado vira vida
        Ganancia,       // +50% de ouro
        PesLeves,       // andar +15% e esquiva custa metade
        RecargaRapida,  // recargas -25%
        Brasa,          // todo golpe põe calor (acende)
        Faisca,         // todo golpe põe carga elétrica (eletrocuta)
        Executor,       // +50% de dano em inimigo abaixo de 25% de vida
        SegundaChance,  // a primeira morte da partida levanta com 50% de vida
        PocaoExtra,     // +1 poção e cura toda a vida (pode repetir)
    }

    public sealed class BlessingInfo
    {
        public BlessingId Id; public string Name, Desc; public bool Repeatable;
        public float R, G, B;   // cor do orbe
        public BlessingInfo(BlessingId id, string name, string desc, float r, float g, float b, bool repeatable = false)
        { Id = id; Name = name; Desc = desc; R = r; G = g; B = b; Repeatable = repeatable; }
    }

    public sealed class RunBlessings
    {
        // Partida do jogador atual (o jogo atualiza todo quadro). Os inimigos leem daqui o dano extra.
        public static RunBlessings Current;

        public const int ChoiceCount = 3;
        public const int ArenaKillsPerAltar = 6;
        public const float DamageBonus = 0.2f, DamageTakenCut = 0.2f, HpBonus = 40f, RegenBonus = 0.5f, LifeSteal = 0.04f,
                           GoldBonus = 0.5f, MoveBonus = 0.15f, DashCostCut = 0.5f, CooldownCut = 0.25f, HeatPerHit = 22f,
                           ChargePerHit = 14f, ExecuteBelow = 0.25f, ExecuteBonus = 0.5f, SecondChanceHp = 0.5f;

        public static readonly BlessingInfo[] All =
        {
            new BlessingInfo(BlessingId.ForcaBruta, "Força Bruta", "+20% de dano", 1f, 0.35f, 0.25f),
            new BlessingInfo(BlessingId.PeleDePedra, "Pele de Pedra", "-20% de dano recebido", 0.65f, 0.6f, 0.55f),
            new BlessingInfo(BlessingId.CoracaoForte, "Coração Forte", "+40 de vida máxima", 1f, 0.3f, 0.45f),
            new BlessingInfo(BlessingId.Folego, "Fôlego", "Estamina volta 50% mais rápido", 0.4f, 1f, 0.45f),
            new BlessingInfo(BlessingId.MenteClara, "Mente Clara", "Mana volta 50% mais rápido", 0.35f, 0.55f, 1f),
            new BlessingInfo(BlessingId.Sanguessuga, "Sanguessuga", "4% do dano vira vida", 0.75f, 0.1f, 0.2f),
            new BlessingInfo(BlessingId.Ganancia, "Ganância", "+50% de ouro", 1f, 0.85f, 0.3f),
            new BlessingInfo(BlessingId.PesLeves, "Pés Leves", "Anda 15% mais rápido, esquiva pela metade", 0.6f, 1f, 0.9f),
            new BlessingInfo(BlessingId.RecargaRapida, "Recarga Rápida", "Recargas 25% menores", 0.8f, 0.5f, 1f),
            new BlessingInfo(BlessingId.Brasa, "Brasa", "Todo golpe põe fogo", 1f, 0.5f, 0.1f),
            new BlessingInfo(BlessingId.Faisca, "Faísca", "Todo golpe dá choque", 0.5f, 0.85f, 1f),
            new BlessingInfo(BlessingId.Executor, "Executor", "+50% de dano em quem tem pouca vida", 0.55f, 0.1f, 0.1f),
            new BlessingInfo(BlessingId.SegundaChance, "Segunda Chance", "A primeira morte levanta com metade da vida", 1f, 1f, 0.75f),
            new BlessingInfo(BlessingId.PocaoExtra, "Poção Extra", "+1 poção e cura tudo", 0.9f, 0.2f, 0.35f, repeatable: true),
        };

        public static BlessingInfo Info(BlessingId id) { foreach (var b in All) if (b.Id == id) return b; return null; }

        readonly HashSet<BlessingId> owned = new HashSet<BlessingId>();
        public IReadOnlyCollection<BlessingId> Owned => owned;
        public bool SecondChanceUsed { get; private set; }
        public int Count => owned.Count;

        public bool Has(BlessingId id) => owned.Contains(id);
        public void Clear() { owned.Clear(); SecondChanceUsed = false; }

        // Acrescenta (as repetíveis não ficam na lista). Os efeitos imediatos (vida, poção) o PlayerState aplica.
        public bool Add(BlessingId id)
        {
            var info = Info(id);
            if (info == null) return false;
            if (info.Repeatable) return true;
            return owned.Add(id);
        }

        // 3 opções diferentes que o jogador ainda não tem (repetíveis sempre podem sair).
        public List<BlessingId> RollChoices(Random rng, int count = ChoiceCount)
        {
            var pool = new List<BlessingId>();
            foreach (var b in All) if (b.Repeatable || !owned.Contains(b.Id)) pool.Add(b.Id);
            var pick = new List<BlessingId>();
            while (pick.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                pick.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return pick;
        }

        // ---------- Efeitos ----------
        public float DamageMultiplier(float targetHpFraction)
        {
            float m = 1f;
            if (Has(BlessingId.ForcaBruta)) m += DamageBonus;
            if (Has(BlessingId.Executor) && targetHpFraction < ExecuteBelow) m += ExecuteBonus;
            return m;
        }
        public float DamageTakenMultiplier => Has(BlessingId.PeleDePedra) ? 1f - DamageTakenCut : 1f;
        public float BonusHp => Has(BlessingId.CoracaoForte) ? HpBonus : 0f;
        public float StaminaRegenMultiplier => Has(BlessingId.Folego) ? 1f + RegenBonus : 1f;
        public float ManaRegenMultiplier => Has(BlessingId.MenteClara) ? 1f + RegenBonus : 1f;
        public float LifeStealFraction => Has(BlessingId.Sanguessuga) ? LifeSteal : 0f;
        public int ScaleGold(int g) => Has(BlessingId.Ganancia) ? (int)Math.Round(g * (1f + GoldBonus)) : g;
        public float MoveMultiplier => Has(BlessingId.PesLeves) ? 1f + MoveBonus : 1f;
        public float DashCostMultiplier => Has(BlessingId.PesLeves) ? 1f - DashCostCut : 1f;
        public float CooldownMultiplier => Has(BlessingId.RecargaRapida) ? 1f - CooldownCut : 1f;
        public float HeatPerHitAmount => Has(BlessingId.Brasa) ? HeatPerHit : 0f;
        public float ChargePerHitAmount => Has(BlessingId.Faisca) ? ChargePerHit : 0f;

        // Sanguessuga: os inimigos acumulam a cura aqui e o jogador recolhe no Tick.
        float pendingHeal;
        public void OnDamageDealt(float dmg) { if (dmg > 0f) pendingHeal += dmg * LifeStealFraction; }
        public float TakeHeal() { float h = pendingHeal; pendingHeal = 0f; return h; }

        // Segunda chance: true = a morte foi evitada (gasta a bênção).
        public bool TryCheatDeath()
        {
            if (!Has(BlessingId.SegundaChance) || SecondChanceUsed) return false;
            SecondChanceUsed = true;
            return true;
        }

        // Dungeon: altar depois de limpar a 1ª sala e depois a cada 2 salas de combate (≈ 2 por andar).
        public static bool AltarAfterRoom(int combatRoomsCleared) => combatRoomsCleared >= 1 && combatRoomsCleared % 2 == 1;
    }
}
