using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    // Progresso salvo do personagem (classe, nível, XP, árvore, ouro e armas). Campos públicos para o JsonUtility da Unity.
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int classId;
        public int level = 1;
        public int xp;
        public int gold;
        public int activeSlot;
        public int deepestFloor;               // andar mais fundo alcançado na dungeon
        public int bossesDefeated;
        public List<string> skills = new List<string>();
        public List<ItemSave> items = new List<ItemSave>();

        public static SaveData From(PlayerState ps)
        {
            var d = new SaveData
            {
                classId = (int)ps.Class.Id,
                level = ps.Progression.Level,
                xp = ps.Progression.Xp,
                gold = ps.Gold,
                activeSlot = ps.ActiveSlot,
                skills = ps.Tree.Unlocked.ToList(),
            };
            foreach (var it in ps.Loadout) d.items.Add(ItemSave.From(it));
            return d;
        }

        // Cria o personagem a partir do salvo. Dados inválidos (arma ou habilidade desconhecida) são ignorados.
        public PlayerState ToPlayer(int maxLevel = Progression.MvpMaxLevel)
        {
            var cls = Enum.IsDefined(typeof(ClassId), classId) ? (ClassId)classId : ClassId.Lamina;
            var ps = new PlayerState(cls, maxLevel);
            ps.RestoreProgress(level, xp, skills ?? new List<string>());
            ps.SetGold(gold);
            if (items != null)
                for (int i = 0; i < ps.Loadout.Length && i < items.Count; i++)
                {
                    var it = items[i]?.ToItem();
                    if (it != null) ps.Loadout[i] = it;
                }
            ps.ActiveSlot = Math.Clamp(activeSlot, 0, ps.Loadout.Length - 1);
            if (ps.Loadout[ps.ActiveSlot] == null) ps.ActiveSlot = 0;
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
        public List<AffixSave> affixes = new List<AffixSave>();

        public static ItemSave From(ItemInstance it)
        {
            if (it == null) return new ItemSave();
            return new ItemSave
            {
                weapon = it.Base.Id,
                rarity = (int)it.Rarity,
                upgrade = it.Upgrade,
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
            return new ItemInstance(def, r, aff, upgrade);
        }
    }

    [Serializable]
    public sealed class AffixSave
    {
        public int kind;
        public float value;
    }
}
