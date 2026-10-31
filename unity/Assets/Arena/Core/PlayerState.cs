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
        // Regeneração mais rápida (2026-10-28, mais dinâmico): estamina 45/s (era 30), mana 12/s (era 7).
        public const float StaminaRegen = 45f, ManaRegen = 12f, ManaRegenFonte = 24f, SwingStaminaScale = 0.7f;
        public ResourcePool Stamina { get; } = new ResourcePool(ClassProfile.BaseStamina, StaminaRegen);
        public ResourcePool Mana { get; } = new ResourcePool(ClassProfile.BaseMana, ManaRegen);

        public float Invulnerable { get; private set; }
        public float HurtCooldown { get; private set; }
        // Postura de Contra saiu (2026-10-30): virou a Aura Vermelha (RedAuraRules). Fica 0 para quem ainda consulta.
        public float CounterStance => 0f;
        public float RedAura { get; private set; }
        public bool AuraActive => RedAura > 0f;
        public int WindCharges { get; private set; }
        public int Kills { get; private set; }
        public int Gold { get; private set; }
        // Modo arcade: profundidade atual da dungeon e da arena (sobe ao vencer, volta a 1 ao morrer).
        public int DungeonTier { get; set; } = 1;
        public int ArenaTier { get; set; } = 1;
        // Mãos (2026-10-30, estilo Blade & Sorcery): Loadout[0] = mão DIREITA, Loadout[1] = mão ESQUERDA (escudo ou arma).
        // Qualquer uma pode estar vazia. O resto fica nos COLDRES (Holsters): 2 nos ombros (qualquer tamanho) e 2 na
        // cintura (pequenas e médias).
        public ItemInstance[] Loadout { get; } = new ItemInstance[2];
        public int ActiveSlot { get => 0; set { } }   // antes: qual das 2 armas estava na mão; agora a direita é sempre a 0
        public ItemInstance ActiveItem => Loadout[0];
        public ItemInstance LeftItem => Loadout[1];
        public const int OmbroE = 0, OmbroD = 1, CinturaE = 2, CinturaD = 3;
        public ItemInstance[] Holsters { get; } = new ItemInstance[4];
        public static bool IsShoulder(int slot) => slot == OmbroE || slot == OmbroD;
        public static bool Fits(int slot, ItemInstance item) => item != null && (IsShoulder(slot) || !item.Base.IsLarge);
        // Coldre livre onde o item cabe (pequenas preferem a cintura, deixando os ombros para as grandes); -1 = nenhum.
        public int FreeHolsterFor(ItemInstance item)
        {
            if (item == null) return -1;
            int[] order = item.Base.IsLarge ? new[] { OmbroD, OmbroE } : new[] { CinturaD, CinturaE, OmbroD, OmbroE };
            foreach (int s in order) if (Holsters[s] == null) return s;
            return -1;
        }
        public bool StoreInHolster(int slot, ItemInstance item)
        {
            if (slot < 0 || slot >= Holsters.Length || Holsters[slot] != null || !Fits(slot, item)) return false;
            Holsters[slot] = item;
            return true;
        }
        public ItemInstance TakeFromHolster(int slot)
        {
            if (slot < 0 || slot >= Holsters.Length) return null;
            var it = Holsters[slot];
            Holsters[slot] = null;
            return it;
        }
        // Tem alguma arma além da que está na mão direita (esquerda ou coldres)?
        public bool HasOtherWeapon()
        {
            if (Loadout[1] != null) return true;
            foreach (var h in Holsters) if (h != null) return true;
            return false;
        }

        readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        // Bênçãos da partida (altar depois das salas). Não vão no salvo.
        public RunBlessings Blessings { get; } = new RunBlessings();
        public event Action SecondChance;

        public void ChooseBlessing(BlessingId id)
        {
            if (!Blessings.Add(id)) return;
            if (id == BlessingId.CoracaoForte) { ApplyLevelStats(fill: false); Hp.Restore(RunBlessings.HpBonus); }
            if (id == BlessingId.PocaoExtra) { AddPotion(); Hp.Fill(); }
        }

        // Fim da partida (saiu da dungeon, morreu na arena): perde as bênçãos.
        public void ClearBlessings()
        {
            if (Blessings.Count == 0) return;
            Blessings.Clear();
            ApplyLevelStats(fill: false);
        }

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

        // Demo: começa sem nada (a espada vem do cadáver, o escudo do lado dele).
        public void ClearAllItems()
        {
            Loadout[0] = Loadout[1] = null;
            for (int i = 0; i < Holsters.Length; i++) Holsters[i] = null;
        }
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
            if (Loadout[0] == null && Loadout[1] == null && !HasOtherWeapon()) { GiveStartingWeapons(); return; }
            var main = WeaponDef.LoadoutFor(Class.Id)[0];
            if (Loadout[0] != null && Loadout[0].Base.Kind == main.Kind) return;
            for (int i = 0; i < Holsters.Length; i++)
                if (Holsters[i] != null && Holsters[i].Base.Kind == main.Kind && !Holsters[i].Base.IsShield)
                {
                    var h = Holsters[i]; Holsters[i] = Loadout[0]; Loadout[0] = h;   // troca a da mão pela do coldre
                    if (Holsters[i] != null && !Fits(i, Holsters[i])) { var x = Holsters[i]; Holsters[i] = null; int f = FreeHolsterFor(x); if (f >= 0) Holsters[f] = x; else AddLostItem(x); }
                    return;
                }
            var old = Loadout[0];
            Loadout[0] = ItemInstance.Basic(main);
            if (old != null) { int f = FreeHolsterFor(old); if (f >= 0) Holsters[f] = old; else AddLostItem(old); }
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
            for (int i = 0; i < Loadout.Length; i++) Loadout[i] = i < defs.Length && defs[i] != null ? ItemInstance.Basic(defs[i]) : null;
            for (int i = 0; i < Holsters.Length; i++) Holsters[i] = null;
            for (int i = Loadout.Length; i < defs.Length; i++)
            {
                if (defs[i] == null) continue;
                var it = ItemInstance.Basic(defs[i]);
                int f = FreeHolsterFor(it);
                if (f >= 0) Holsters[f] = it;
            }
        }

        // Pegou uma arma com a mão (0 = direita, 1 = esquerda): ela vai para essa mão. A que estava nela vai para um
        // coldre livre onde caiba; senão sai (cai no chão / fica no gancho). Devolve a que saiu (ou null).
        public ItemInstance TakeItem(ItemInstance item, out bool oldStored, int hand = 0)
        {
            var old = Loadout[hand];
            Loadout[hand] = item;
            oldStored = false;
            if (old == null) return null;
            int f = FreeHolsterFor(old);
            if (f >= 0) { Holsters[f] = old; oldStored = true; return null; }
            return old;
        }

        // Tira a arma da mão DIREITA (vender/guardar). Precisa ter outra arma (na esquerda ou num coldre); a primeira do
        // coldre vai para a mão direita.
        public ItemInstance RemoveHandWeapon()
        {
            var item = Loadout[0];
            if (item == null || !HasOtherWeapon()) return null;
            Loadout[0] = null;
            for (int i = 0; i < Holsters.Length; i++)
                if (Holsters[i] != null && !Holsters[i].Base.IsShield) { Loadout[0] = Holsters[i]; Holsters[i] = null; break; }
            return item;
        }

        // Troca a arma da mão direita pela nova; devolve a antiga (que cai no chão).
        public ItemInstance EquipToActive(ItemInstance item)
        {
            var old = Loadout[0];
            Loadout[0] = item;
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

        // Vende a arma da MÃO (pedido do Pablo, 2026-10-29); a 2ª vai para a mão. Precisa ter outra arma.
        public int SellHandWeapon()
        {
            var item = RemoveHandWeapon();
            if (item == null) return 0;
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
            cooldowns[WeaponAbilities.CooldownId] = WeaponAbilities.CooldownFor(item) * Blessings.CooldownMultiplier;
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
            Hp.SetMax(Class.MaxHp(l) + Blessings.BonusHp, fill);
            Stamina.SetMax(Class.MaxStamina(l), fill);
            Mana.SetMax(Class.MaxMana(l), fill);
        }

        // ---------- Dano / morte ----------
        public bool TakeDamage(float amount)
        {
            if (Invulnerable > 0f || HurtCooldown > 0f || amount <= 0f) return false;
            Hp.Drain(amount * Blessings.DamageTakenMultiplier);
            HurtCooldown = 0.4f;
            Stamina.DelayRegen(1.5f);   // levar dano trava a estamina menos tempo (era 3 s)
            Hurt?.Invoke(amount);
            if (Hp.IsEmpty)
            {
                if (Blessings.TryCheatDeath()) { Hp.Restore(Hp.Max * RunBlessings.SecondChanceHp); SetInvulnerable(2f); SecondChance?.Invoke(); }
                else Die();
            }
            return true;
        }

        // Poção de cura: até 3; cura 45% da vida na hora (1 s entre goles). Começa com 1.
        public const int MaxPotions = 3;
        public const float PotionHeal = 0.45f, PotionCooldown = 1f;
        public int Potions { get; private set; } = 1;
        float potionCd;
        public int PotionCap => MaxPotions + ExtraPotionSlots;   // elixir da Alquimista: +1 por compra
        public bool AddPotion() { if (Potions >= PotionCap) return false; Potions++; return true; }
        public void SetPotions(int n) => Potions = Math.Clamp(n, 0, PotionCap);

        // Aliados: NPCs resgatados (moram no santuário), pet escolhido, elixires e mercenário contratado (só a próxima ida).
        public HashSet<NpcId> Rescued { get; } = new HashSet<NpcId>();
        public PetKind Pet { get; set; }
        public int ExtraPotionSlots { get; set; }
        public bool MercenaryHired { get; set; }
        public bool TryDrinkPotion()
        {
            if (Potions <= 0 || potionCd > 0f || Hp.Current >= Hp.Max) return false;
            Potions--;
            potionCd = PotionCooldown;
            Hp.Restore(Hp.Max * PotionHeal);
            return true;
        }
        public void TickPotion(float dt) => potionCd = Math.Max(0f, potionCd - dt);

        public DeathPenalty Penalty { get; set; } = DeathPenalty.Xp;
        public int LastGoldLost { get; private set; }

        public void Die()
        {
            int lost = DeathRules.LosesXp(Penalty) ? Progression.OnDeath() : 0;
            LastGoldLost = DeathRules.GoldLost(Penalty, Gold);
            Gold -= LastGoldLost;
            Hp.Fill(); Stamina.Fill(); Mana.Fill();
            RedAura = 0f; WindCharges = 0; Kills = 0;
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
            cooldowns[id] = cd * Blessings.CooldownMultiplier;
            if (id == SkillIds.Contra) RedAura = RedAuraRules.Duration;
            if (id == SkillIds.VentoLamina) WindCharges = 2;
            return r;
        }

        public bool ConsumeCounterStance() => false;

        public bool ConsumeWindCharge()
        {
            if (WindCharges <= 0) return false;
            WindCharges--;
            return true;
        }

        // Poderes da mão: soco e corte da palma (recarga + mana). Cada mão tem a SUA recarga (pedido do Pablo, 2026-10-17):
        // hand 0 = esquerda, 1 = direita. A mana é a mesma.
        public static string HandCooldownKey(string id, int hand) => hand == 1 ? id + "_dir" : id;
        public float HandCooldownLeft(string id, int hand) => CooldownLeft(HandCooldownKey(id, hand));

        public SkillCastResult TryHandPower(string id, int hand = 0)
        {
            var (cd, cost) = HandPowers.Cost(id);
            var key = HandCooldownKey(id, hand);
            if (CooldownLeft(key) > 0f) return SkillCastResult.Cooldown;
            if (!Mana.TrySpend(cost, 0.6f)) return SkillCastResult.NoMana;
            cooldowns[key] = cd * Blessings.CooldownMultiplier;
            return SkillCastResult.Ok;
        }

        // Raio do Freeza: um raio gasta mana. Retorna false sem mana.
        public bool TryBeam() => Mana.TrySpend(HandPowers.BeamManaPerShot, 0.3f);

        // Esquiva = dash. Retorna false se não houver estamina.
        public bool TryDash()
        {
            if (!Stamina.TrySpend(Combat.DashCost(Has(SkillIds.PassoLeve)) * Blessings.DashCostMultiplier, 0.8f)) return false;
            SetInvulnerable(0.3f);
            return true;
        }

        // Estamina gasta por golpe acertado. Retorna se havia estamina (golpe cansado causa menos dano).
        public bool SpendSwingStamina(WeaponDef w) => Stamina.TrySpend(w.StaminaPerHit * SwingStaminaScale, 0.4f);   // golpe 30% mais barato

        public void Tick(float dt)
        {
            TickPotion(dt);
            Invulnerable = Math.Max(0f, Invulnerable - dt);
            HurtCooldown = Math.Max(0f, HurtCooldown - dt);
            RedAura = Math.Max(0f, RedAura - dt);
            BackstabReady = Math.Max(0f, BackstabReady - dt);
            Mana.RegenPerSecond = (Has(SkillIds.FonteArcana) ? ManaRegenFonte : ManaRegen) * Blessings.ManaRegenMultiplier;
            Stamina.RegenPerSecond = StaminaRegen * Blessings.StaminaRegenMultiplier;
            float heal = Blessings.TakeHeal();
            if (heal > 0f && !Hp.IsEmpty) Hp.Restore(heal);
            Hp.Tick(dt); Stamina.Tick(dt); Mana.Tick(dt);
            if (cooldowns.Count == 0) return;
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys) cooldowns[k] = Math.Max(0f, cooldowns[k] - dt);
        }
    }

    public enum SkillCastResult { Ok, Unknown, Locked, Cooldown, NoStamina, NoMana }
}
