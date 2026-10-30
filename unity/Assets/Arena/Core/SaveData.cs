using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    // Progresso salvo do personagem (classe, nível, XP, árvore, ouro e armas). Campos públicos para o JsonUtility da Unity.
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;   // 2 = mãos (direita/esquerda) + coldres

        public int version = CurrentVersion;
        public int classId;
        public int level = 1;
        public int xp;
        public int gold;
        public int potions = 1;
        public int activeSlot;
        public int deepestFloor;               // andar mais fundo alcançado na dungeon
        public int bossesDefeated;
        public int dungeonTier = 1;
        public int arenaTier = 1;
        public List<string> skills = new List<string>();
        public List<ItemSave> items = new List<ItemSave>();
        public List<ItemSave> holsters = new List<ItemSave>();      // ombro E, ombro D, cintura E, cintura D
        public List<ItemSave> lostItems = new List<ItemSave>();   // armas esquecidas (voltam no santuário)
        public List<ItemSave> stash = new List<ItemSave>();       // estante permanente do santuário
        public List<int> rescued = new List<int>();               // NPCs resgatados (moram no santuário)
        public int pet;
        public int extraPotions;
        public bool mercenary;

        public static SaveData From(PlayerState ps)
        {
            var d = new SaveData
            {
                classId = (int)ps.Class.Id,
                level = ps.Progression.Level,
                xp = ps.Progression.Xp,
                gold = ps.Gold,
                potions = ps.Potions,
                dungeonTier = ps.DungeonTier,
                arenaTier = ps.ArenaTier,
                activeSlot = ps.ActiveSlot,
                skills = ps.Tree.Unlocked.ToList(),
                rescued = ps.Rescued.Select(n => (int)n).ToList(),
                pet = (int)ps.Pet,
                extraPotions = ps.ExtraPotionSlots,
                mercenary = ps.MercenaryHired,
            };
            foreach (var it in ps.Loadout) d.items.Add(ItemSave.From(it));
            foreach (var it in ps.Holsters) d.holsters.Add(ItemSave.From(it));
            foreach (var it in ps.LostItems) d.lostItems.Add(ItemSave.From(it));
            foreach (var it in ps.Stash) d.stash.Add(ItemSave.From(it));
            return d;
        }

        // Cria o personagem a partir do salvo. Dados inválidos (arma ou habilidade desconhecida) são ignorados.
        public PlayerState ToPlayer(int maxLevel = Progression.MvpMaxLevel)
        {
            var cls = Enum.IsDefined(typeof(ClassId), classId) ? (ClassId)classId : ClassId.Lamina;
            var ps = new PlayerState(cls, maxLevel);
            ps.RestoreProgress(level, xp, skills ?? new List<string>());
            ps.SetGold(gold);
            if (rescued != null) foreach (var n in rescued) if (Enum.IsDefined(typeof(NpcId), n)) ps.Rescued.Add((NpcId)n);
            ps.Pet = Enum.IsDefined(typeof(PetKind), pet) ? (PetKind)pet : PetKind.Nenhum;
            ps.ExtraPotionSlots = Math.Clamp(extraPotions, 0, AllyRules.MaxExtraPotions);
            ps.MercenaryHired = mercenary;
            ps.SetPotions(potions);
            ps.DungeonTier = ArcadeRules.Clamp(dungeonTier);
            ps.ArenaTier = ArcadeRules.Clamp(arenaTier);
            if (version >= 2)
            {
                for (int i = 0; i < ps.Holsters.Length; i++) ps.Holsters[i] = null;
                if (items != null)
                    for (int i = 0; i < ps.Loadout.Length && i < items.Count; i++)
                    {
                        if (items[i] == null) continue;
                        var it = items[i].ToItem();
                        if (it != null || string.IsNullOrEmpty(items[i].weapon)) ps.Loadout[i] = it;   // vazio = mão vazia; desconhecida = fica a inicial
                    }
                if (holsters != null)
                    for (int i = 0; i < ps.Holsters.Length && i < holsters.Count; i++)
                    {
                        var it = holsters[i]?.ToItem();
                        if (it != null && !ps.StoreInHolster(i, it)) ps.AddLostItem(it);
                    }
            }
            else if (items != null && items.Count > 0)
            {
                // Salvo antigo: 2 armas (activeSlot = a da mão) + escudo fixo da Lâmina. Agora: a da mão vai para a
                // direita, a outra para um coldre, e a Lâmina recebe o escudo na esquerda.
                var a = items.Count > 0 ? items[0]?.ToItem() : null;
                var b = items.Count > 1 ? items[1]?.ToItem() : null;
                var hand = activeSlot == 1 && b != null ? b : a ?? b;
                var other = hand == a ? b : a;
                for (int i = 0; i < ps.Holsters.Length; i++) ps.Holsters[i] = null;
                ps.Loadout[0] = hand;
                ps.Loadout[1] = cls == ClassId.Lamina ? ItemInstance.Basic(WeaponDef.Escudo) : null;
                if (other != null) { int f = ps.FreeHolsterFor(other); if (f >= 0) ps.Holsters[f] = other; else ps.AddLostItem(other); }
            }
            if (lostItems != null)
                foreach (var s in lostItems) { var it = s?.ToItem(); if (it != null) ps.AddLostItem(it); }
            if (stash != null)
                foreach (var s in stash) { var it = s?.ToItem(); if (it != null) ps.AddToStash(it); }
            return ps;
        }

        public string Summary => $"{ClassProfile.Get(Enum.IsDefined(typeof(ClassId), classId) ? (ClassId)classId : ClassId.Lamina).Name} nv {level} · {gold} ouro";
    }

    [Serializable]
    public sealed class ItemSave
    {
        public string weapon = "";
        public int rarity;
        public int upgrade;
        public int passive;
        public List<AffixSave> affixes = new List<AffixSave>();

        public static ItemSave From(ItemInstance it)
        {
            if (it == null) return new ItemSave();
            return new ItemSave
            {
                weapon = it.Base.Id,
                rarity = (int)it.Rarity,
                upgrade = it.Upgrade,
                passive = (int)it.Passive,
                affixes = it.Affixes.Select(a => new AffixSave { kind = (int)a.Kind, value = a.Value }).ToList(),
            };
        }

        public ItemInstance ToItem()
        {
            var def = WeaponDef.All.FirstOrDefault(w => w.Id == weapon);
            if (def == null) return null;
            var r = Enum.IsDefined(typeof(Rarity), rarity) ? (Rarity)rarity : Rarity.Comum;
            var aff = (affixes ?? new List<AffixSave>())
                .Where(a => Enum.IsDefined(typeof(AffixKind), a.kind))
                .Take(ItemInstance.MaxAffixes(r))
                .Select(a => new Affix((AffixKind)a.kind, Math.Clamp(a.value, 0f, 1f)));
            var p = Enum.IsDefined(typeof(WeaponPassive), passive) ? (WeaponPassive)passive : WeaponPassive.Nenhuma;
            return new ItemInstance(def, r, aff, upgrade, p);
        }
    }

    [Serializable]
    public sealed class AffixSave
    {
        public int kind;
        public float value;
    }
}
