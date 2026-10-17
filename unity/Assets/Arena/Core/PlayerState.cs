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
        public int Gold { get; private set; }
        // Modo arcade: profundidade atual da dungeon e da arena (sobe ao vencer, volta a 1 ao morrer).
        public int DungeonTier { get; set; } = 1;
        public int ArenaTier { get; set; } = 1;
        // Duas armas equipadas (troca com o clique do analógico / Q). Pegar uma arma do chão troca a da mão.
        public ItemInstance[] Loadout { get; } = new ItemInstance[2];
        public int ActiveSlot { get; set; }
        public ItemInstance ActiveItem => Loadout[ActiveSlot] ?? Loadout[0];

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
            EnsureClassWeapon();
        }

        // Personagem novo recebe as armas iniciais. Trocando de classe, mantém o que achou, mas garante a arma
        // principal da classe (o Elementalista precisa do cajado para as magias).
        void EnsureClassWeapon()
        {
            if (Loadout[0] == null) { GiveStartingWeapons(); return; }
            var main = WeaponDef.LoadoutFor(Class.Id)[0];
            for (int i = 0; i < Loadout.Length; i++)
                if (Loadout[i] != null && Loadout[i].Base.Kind == main.Kind) { ActiveSlot = i; return; }
            Loadout[0] = ItemInstance.Basic(main);
            ActiveSlot = 0;
        }

        // Jogo salvo: nível/XP e libera de novo as habilidades salvas (gastando os pontos, respeitando pré-requisitos).
        public void RestoreProgress(int level, int xp, IEnumerable<string> skills)
        {
            Progression.Restore(level, xp);
            var pending = new List<string>(skills);
            bool progress = true;
            while (progress && pending.Count > 0)
            {
                progress = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (Tree.Has(pending[i])) { pending.RemoveAt(i); progress = true; continue; }
                    if (Tree.Unlock(pending[i])) { pending.RemoveAt(i); progress = true; }
                }
            }
            ApplyLevelStats(fill: true);
        }

        // Armas iniciais da classe (comuns).
        public void GiveStartingWeapons()
        {
            var defs = WeaponDef.LoadoutFor(Class.Id);
            for (int i = 0; i < Loadout.Length; i++) Loadout[i] = i < defs.Length ? ItemInstance.Basic(defs[i]) : null;
            ActiveSlot = 0;
        }

        // Pegou uma arma: se a 2ª vaga estiver vazia ela vai para lá (nada cai no chão); senão troca a da mão.
        // Devolve a arma que saiu (ou null) e se a nova foi para a 2ª vaga.
        public ItemInstance TakeItem(ItemInstance item, out bool wentToSecond)
        {
            int other = 1 - ActiveSlot;
            if (Loadout[other] == null && Loadout[ActiveSlot] != null)
            {
                Loadout[other] = item;
                wentToSecond = true;
                return null;
            }
            wentToSecond = false;
            return EquipToActive(item);
        }

        // Troca a arma do slot ativo pela nova; devolve a antiga (que cai no chão).
        public ItemInstance EquipToActive(ItemInstance item)
        {
            var old = Loadout[ActiveSlot];
            Loadout[ActiveSlot] = item;
            return old;
        }

        // Armas do jogador largadas e esquecidas (trocou e não pegou de volta antes de sair do andar):
        // reaparecem no santuário, perto da fogueira. Guarda as 6 mais recentes.
        public const int MaxLostItems = 12;   // = vagas do suporte de armas do santuário
        public List<ItemInstance> LostItems { get; } = new List<ItemInstance>();
        public void AddLostItem(ItemInstance item)
        {
            if (item == null || LostItems.Contains(item)) return;
            LostItems.Add(item);
            while (LostItems.Count > MaxLostItems) LostItems.RemoveAt(0);
        }

        // Estante PERMANENTE do santuário: o que o jogador escolheu guardar (não some). LostItems é a TEMPORÁRIA:
        // o que ficou para trás; some quando ele entra de novo num portal sem tirar de lá.
        public const int MaxStash = 12;
        public List<ItemInstance> Stash { get; } = new List<ItemInstance>();
        public bool AddToStash(ItemInstance item)
        {
            if (item == null || Stash.Contains(item) || Stash.Count >= MaxStash) return false;
            Stash.Add(item);
            return true;
        }

        // Some com a estante temporária (entrou num portal). Devolve quantas sumiram.
        public int DiscardLostItems()
        {
            int n = LostItems.Count;
            LostItems.Clear();
            return n;
        }

        // Vende a 2ª arma (a que não está na mão) ao mercador do santuário. Devolve o ouro ganho (0 = nada para vender).
        public int SellSecondWeapon()
        {
            int other = 1 - ActiveSlot;
            var item = Loadout[other];
            if (item == null || Loadout[ActiveSlot] == null) return 0;
            Loadout[other] = null;
            int price = Merchant.Price(item);
            AddGold(price);
            return price;
        }

        public void AddGold(int g) => Gold += Math.Max(0, g);
        public void SpendGold(int g) => Gold = Math.Max(0, Gold - Math.Max(0, g));
        public void SetGold(int g) => Gold = Math.Max(0, g);

        // Roubo de vida da arma e passivas de golpe (Ímpeto, Eco Arcano).
        public void OnDealtDamage(float amount, WeaponDef w)
        {
            if (w != null && w.LifeSteal > 0f && amount > 0f) Hp.Restore(amount * w.LifeSteal);
            if (amount <= 0f) return;
            var p = ActiveItem?.Passive ?? WeaponPassive.Nenhuma;
            if (p == WeaponPassive.Impeto) Stamina.Restore(WeaponAbilities.HitStamina);
            if (p == WeaponPassive.EcoArcano) Mana.Restore(WeaponAbilities.HitMana);
        }

        // Passiva Sede de Sangue: abate cura.
        public void OnKillPassive()
        {
            if (ActiveItem?.Passive == WeaponPassive.SedeDeSangue) Hp.Restore(WeaponAbilities.KillHeal);
        }

        // ---------- Habilidade da arma ----------
        public float WeaponAbilityCooldown => CooldownLeft(WeaponAbilities.CooldownId);

        public SkillCastResult CanUseWeaponAbility()
        {
            var item = ActiveItem;
            if (item == null) return SkillCastResult.Unknown;
            if (WeaponAbilityCooldown > 0f) return SkillCastResult.Cooldown;
            var a = item.Ability;
            var pool = a.Resource == ResourceKind.Mana ? Mana : Stamina;
            return pool.Has(a.Cost) ? SkillCastResult.Ok : (a.Resource == ResourceKind.Mana ? SkillCastResult.NoMana : SkillCastResult.NoStamina);
        }

        public SkillCastResult TryWeaponAbility()
        {
            var r = CanUseWeaponAbility();
            if (r != SkillCastResult.Ok) return r;
            var item = ActiveItem;
            (item.Ability.Resource == ResourceKind.Mana ? Mana : Stamina).TrySpend(item.Ability.Cost, 0.6f);
            cooldowns[WeaponAbilities.CooldownId] = WeaponAbilities.CooldownFor(item);
            return r;
        }

        // Passo Sombrio: o próximo golpe dentro da janela é crítico.
        public float BackstabReady { get; private set; }
        public float BackstabMultiplier { get; private set; } = WeaponAbilities.BackstabMultiplier;
        public void ArmBackstab(float multiplier = WeaponAbilities.BackstabMultiplier) { BackstabReady = WeaponAbilities.BackstabWindow; BackstabMultiplier = multiplier; }
        public bool ConsumeBackstab()
        {
            if (BackstabReady <= 0f) return false;
            BackstabReady = 0f;
            return true;
        }

        // Arma efetiva na mão (com raridade, atributos, ferreiro e proficiência).
        public WeaponDef ActiveWeapon => ActiveItem?.Resolve(Class.Id);

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

        public DeathPenalty Penalty { get; set; } = DeathPenalty.Xp;
        public int LastGoldLost { get; private set; }

        public void Die()
        {
            int lost = DeathRules.LosesXp(Penalty) ? Progression.OnDeath() : 0;
            LastGoldLost = DeathRules.GoldLost(Penalty, Gold);
            Gold -= LastGoldLost;
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

        // Poderes da mão: soco no chão e corte da palma (recarga + mana).
        public SkillCastResult TryHandPower(string id)
        {
            var (cd, cost) = HandPowers.Cost(id);
            if (CooldownLeft(id) > 0f) return SkillCastResult.Cooldown;
            if (!Mana.TrySpend(cost, 0.6f)) return SkillCastResult.NoMana;
            cooldowns[id] = cd;
            return SkillCastResult.Ok;
        }

        // Raio do Freeza: um raio gasta mana. Retorna false sem mana.
        public bool TryBeam() => Mana.TrySpend(HandPowers.BeamManaPerShot, 0.3f);

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
            BackstabReady = Math.Max(0f, BackstabReady - dt);
            Mana.RegenPerSecond = Has(SkillIds.FonteArcana) ? 14f : 7f;
            Hp.Tick(dt); Stamina.Tick(dt); Mana.Tick(dt);
            if (cooldowns.Count == 0) return;
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys) cooldowns[k] = Math.Max(0f, cooldowns[k] - dt);
        }
    }

    public enum SkillCastResult { Ok, Unknown, Locked, Cooldown, NoStamina, NoMana }
}
