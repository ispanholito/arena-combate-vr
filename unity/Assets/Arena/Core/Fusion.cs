using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    // Forja de fusão (inspiração Soul Knight): junta a arma da mão (fica o tipo dela) com a 2ª arma (é consumida).
    // Mesma raridade = sobe uma raridade; diferentes = fica a maior. Os atributos das duas se somam (vale o melhor de
    // cada tipo, até o limite da raridade), o ferreiro fica o maior e a passiva é herdada.
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
            return new ItemInstance(keep.Base, rarity, affixes, Math.Max(keep.Upgrade, food.Upgrade), passive);
        }

        // Funde no personagem: cobra o ouro, troca a arma da mão e esvazia a 2ª vaga.
        public static bool TryFuse(PlayerState ps, Random rng, out ItemInstance result)
        {
            result = null;
            var keep = ps.Loadout[ps.ActiveSlot];
            var food = ps.Loadout[1 - ps.ActiveSlot];
            if (!CanFuse(keep, food)) return false;
            int cost = Cost(keep, food);
            if (ps.Gold < cost) return false;
            ps.SpendGold(cost);
            result = Fuse(keep, food, rng);
            ps.Loadout[ps.ActiveSlot] = result;
            ps.Loadout[1 - ps.ActiveSlot] = null;
            return true;
        }
    }
}
