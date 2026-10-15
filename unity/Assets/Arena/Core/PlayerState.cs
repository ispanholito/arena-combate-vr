using System;
using System.Collections.Generic;

namespace Arena.Core
{
    // Tudo do jogador que não depende da Unity: classe, nível, recursos, árvore, recargas.
    public sealed class PlayerState
    {
        public ClassProfile Class { get; private set; }
        public Progression Progression { get; }
        public SkillTree Tree { get; private set; }
        public ResourcePool Hp { get; } = new ResourcePool(ClassProfile.BaseHp);
        public ResourcePool Stamina { get; } = new ResourcePool(ClassProfile.BaseStamina, 30f);
        public ResourcePool Mana { get; } = new ResourcePool(ClassProfile.BaseMana, 7f);

        public float Invulnerable { get; private set; }
        public float HurtCooldown { get; private set; }
        public float CounterStance { get; private set; }
        public int WindCharges { get; private set; }
        public int Kills { get; private set; }

        readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        public event Action<float> Hurt;
        public event Action<int> Died;          // XP perdido
        public event Action<int> LeveledUp;

        public PlayerState(ClassId cls, int maxLevel = Progression.MvpMaxLevel)
        {
            Progression = new Progression(maxLevel);
            Progression.LeveledUp += OnLevelUp;
            SetClass(cls);
        }

        public float Strength => Class.Strength(Progression.Level);
        public bool Has(string skillId) => Tree.Has(skillId);

        public void SetClass(ClassId cls)
        {
            int refunded = Tree?.SpentPoints ?? 0;
            Class = ClassProfile.Get(cls);
            Tree = new SkillTree(SkillTree.For(cls), Progression);
            Tree.Grant(SkillTree.StartingSkill(cls));
            Progression.RefundPoints(refunded);
            ApplyLevelStats(fill: true);
        }

        void OnLevelUp(int level)
        {
            ApplyLevelStats(fill: true);
            LeveledUp?.Invoke(level);
        }

        void ApplyLevelStats(bool fill)
        {
            int l = Progression.Level;
            Hp.SetMax(Class.MaxHp(l), fill);
            Stamina.SetMax(Class.MaxStamina(l), fill);
            Mana.SetMax(Class.MaxMana(l), fill);
        }

        // ---------- Dano / morte ----------
        public bool TakeDamage(float amount)
        {
            if (Invulnerable > 0f || HurtCooldown > 0f || amount <= 0f) return false;
            Hp.Drain(amount);
            HurtCooldown = 0.4f;
            Stamina.DelayRegen(3f);
            Hurt?.Invoke(amount);
            if (Hp.IsEmpty) Die();
            return true;
        }

        public void Die()
        {
            int lost = Progression.OnDeath();
            Hp.Fill(); Stamina.Fill(); Mana.Fill();
            CounterStance = 0f; WindCharges = 0; Kills = 0;
            cooldowns.Clear();
            Died?.Invoke(lost);
        }

        public void RestAtBonfire()
        {
            Hp.Fill(); Stamina.Fill(); Mana.Fill();
        }

        public void AddKill(int xp)
        {
            Kills++;
            Progression.GainXp(xp);
        }

        public void ResetKills() => Kills = 0;
        public void SetInvulnerable(float s) => Invulnerable = Math.Max(Invulnerable, s);

        // ---------- Habilidades ----------
        public float CooldownLeft(string id) => cooldowns.TryGetValue(id, out var t) ? t : 0f;

        public SkillCastResult CanCast(string id)
        {
            var node = Tree.Get(id);
            if (node == null || !node.IsActive) return SkillCastResult.Unknown;
            if (!Tree.Has(id)) return SkillCastResult.Locked;
            if (CooldownLeft(id) > 0f) return SkillCastResult.Cooldown;
            var pool = node.Resource == ResourceKind.Mana ? Mana : Stamina;
            return pool.Has(node.Cost) ? SkillCastResult.Ok : (node.Resource == ResourceKind.Mana ? SkillCastResult.NoMana : SkillCastResult.NoStamina);
        }

        public SkillCastResult TryCast(string id)
        {
            var r = CanCast(id);
            if (r != SkillCastResult.Ok) return r;
            var node = Tree.Get(id);
            (node.Resource == ResourceKind.Mana ? Mana : Stamina).TrySpend(node.Cost, 0.6f);
            float cd = node.Cooldown;
            if (id == SkillIds.Investida && Has(SkillIds.InvestidaCadeia)) cd *= 0.5f;
            cooldowns[id] = cd;
            if (id == SkillIds.Contra) CounterStance = 2f;
            if (id == SkillIds.VentoLamina) WindCharges = 2;
            return r;
        }

        public bool ConsumeCounterStance()
        {
            if (CounterStance <= 0f) return false;
            CounterStance = 0f;
            return true;
        }

        public bool ConsumeWindCharge()
        {
            if (WindCharges <= 0) return false;
            WindCharges--;
            return true;
        }

        // Esquiva = dash. Retorna false se não houver estamina.
        public bool TryDash()
        {
            if (!Stamina.TrySpend(Combat.DashCost(Has(SkillIds.PassoLeve)), 0.8f)) return false;
            SetInvulnerable(0.3f);
            return true;
        }

        // Estamina gasta por golpe acertado. Retorna se havia estamina (golpe cansado causa menos dano).
        public bool SpendSwingStamina(WeaponDef w) => Stamina.TrySpend(w.StaminaPerHit, 0.6f);

        public void Tick(float dt)
        {
            Invulnerable = Math.Max(0f, Invulnerable - dt);
            HurtCooldown = Math.Max(0f, HurtCooldown - dt);
            CounterStance = Math.Max(0f, CounterStance - dt);
            Mana.RegenPerSecond = Has(SkillIds.FonteArcana) ? 14f : 7f;
            Hp.Tick(dt); Stamina.Tick(dt); Mana.Tick(dt);
            if (cooldowns.Count == 0) return;
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys) cooldowns[k] = Math.Max(0f, cooldowns[k] - dt);
        }
    }

    public enum SkillCastResult { Ok, Unknown, Locked, Cooldown, NoStamina, NoMana }
}
