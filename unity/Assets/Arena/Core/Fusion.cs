using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    // Forja de fusão (inspiração Soul Knight): as DUAS armas (a da mão e a 2ª) viram UMA OUTRA arma (decisão do Pablo,
    // 2026-10-29: não é sacrificar uma para melhorar a outra). Cada par tem uma "receita" fixa: o mesmo par sempre dá a
    // mesma arma, diferente das duas. Mesma raridade = sobe uma raridade; diferentes = fica a maior. Os atributos se
    // somam (o melhor de cada tipo, até o limite da raridade), o ferreiro fica o maior e a passiva é herdada.
    public static class Fusion
    {
        public static Rarity ResultRarity(ItemInstance keep, ItemInstance food) =>
            keep.Rarity == food.Rarity ? (Rarity)Math.Min((int)Rarity.Lendario, (int)keep.Rarity + 1) : (Rarity)Math.Max((int)keep.Rarity, (int)food.Rarity);

        public static int Cost(ItemInstance keep, ItemInstance food)
        {
            int r = (int)ResultRarity(keep, food) + 1;
            return 25 * r * r;
        }

        public static bool CanFuse(ItemInstance keep, ItemInstance food) =>
            keep != null && food != null && !(keep.Rarity == Rarity.Lendario && food.Rarity == Rarity.Lendario && keep.Upgrade >= ItemInstance.MaxUpgrade);

        // Receita: arma resultante do par (não depende da ordem; nunca é uma das duas).
        public static WeaponDef ResultBase(WeaponDef a, WeaponDef b)
        {
            var options = new List<WeaponDef>();
            foreach (var w in WeaponDef.All) if (w != a && w != b) options.Add(w);
            uint h = Hash(a.Id) ^ Hash(b.Id);
            if (a == b) h = Hash(a.Id) * 31u;
            return options[(int)(h % (uint)options.Count)];
        }

        static uint Hash(string s)   // FNV-1a: estável entre execuções (string.GetHashCode não é)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }

        public static ItemInstance Fuse(ItemInstance keep, ItemInstance food, Random rng)
        {
            var rarity = ResultRarity(keep, food);
            var best = new Dictionary<AffixKind, float>();
            foreach (var a in keep.Affixes.Concat(food.Affixes))
                if (!best.TryGetValue(a.Kind, out var v) || a.Value > v) best[a.Kind] = a.Value;
            var affixes = best.Select(kv => new Affix(kv.Key, kv.Value)).OrderByDescending(a => a.Value)
                              .Take(ItemInstance.MaxAffixes(rarity)).ToList();
            var passive = keep.Passive != WeaponPassive.Nenhuma ? keep.Passive : food.Passive;
            if (rarity >= Rarity.Raro && passive == WeaponPassive.Nenhuma)
                passive = (WeaponPassive)rng.Next(1, Enum.GetValues(typeof(WeaponPassive)).Length);
            return new ItemInstance(ResultBase(keep.Base, food.Base), rarity, affixes, Math.Max(keep.Upgrade, food.Upgrade), passive);
        }

        // Funde no personagem: cobra o ouro, troca a arma da mão e esvazia a 2ª vaga.
        public static bool TryFuse(PlayerState ps, Random rng, out ItemInstance result)
        {
            result = null;
            var keep = ps.Loadout[0];   // mão direita
            var food = ps.Loadout[1];   // mão esquerda
            if (!CanFuse(keep, food)) return false;
            int cost = Cost(keep, food);
            if (ps.Gold < cost) return false;
            ps.SpendGold(cost);
            result = Fuse(keep, food, rng);
            ps.Loadout[0] = result;
            ps.Loadout[1] = null;
            return true;
        }
    }
}
