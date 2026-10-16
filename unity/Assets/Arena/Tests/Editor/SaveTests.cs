using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class SaveTests
    {
        [Test]
        public void RoundTrip_KeepsLevelTreeGoldAndItems()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.Progression.GainXp(400);
            int level = ps.Progression.Level;
            Assert.Greater(level, 2);
            Assert.IsTrue(ps.Tree.Unlock(SkillIds.FioAfiado) || ps.Tree.Unlock(SkillIds.Contra));
            ps.AddGold(123);
            var item = new ItemInstance(WeaponDef.Lanca, Rarity.Epico, new[] { new Affix(AffixKind.Dano, 0.12f), new Affix(AffixKind.RouboDeVida, 0.04f) }, 3);
            ps.EquipToActive(item);
            ps.ActiveSlot = 0;

            var loaded = SaveData.From(ps).ToPlayer();
            Assert.AreEqual(ClassId.Lamina, loaded.Class.Id);
            Assert.AreEqual(level, loaded.Progression.Level);
            Assert.AreEqual(ps.Progression.Xp, loaded.Progression.Xp);
            Assert.AreEqual(ps.Progression.UnspentPoints, loaded.Progression.UnspentPoints);
            CollectionAssert.AreEquivalent(ps.Tree.Unlocked.ToList(), loaded.Tree.Unlocked.ToList());
            Assert.AreEqual(123, loaded.Gold);
            var li = loaded.ActiveItem;
            Assert.AreEqual(WeaponKind.Lanca, li.Base.Kind);
            Assert.AreEqual(Rarity.Epico, li.Rarity);
            Assert.AreEqual(3, li.Upgrade);
            Assert.AreEqual(item.DamageMultiplier, li.DamageMultiplier, 1e-4);
            Assert.AreEqual(0.04f, li.Sum(AffixKind.RouboDeVida), 1e-4);
            Assert.AreEqual(WeaponKind.Machado, loaded.Loadout[1].Base.Kind);
            Assert.AreEqual(loaded.Hp.Max, loaded.Hp.Current);
            Assert.Greater(loaded.Hp.Max, ClassProfile.BaseHp);
        }

        [Test]
        public void SkillsSavedOutOfOrder_StillUnlockWithPrerequisites()
        {
            var ps = new PlayerState(ClassId.Elementalista);
            ps.Progression.GainXp(5000);
            foreach (var n in ps.Tree.Nodes.OrderBy(n => n.MinLevel)) ps.Tree.Unlock(n.Id);
            var data = SaveData.From(ps);
            data.skills.Reverse();
            var loaded = data.ToPlayer();
            CollectionAssert.AreEquivalent(ps.Tree.Unlocked.ToList(), loaded.Tree.Unlocked.ToList());
        }

        [Test]
        public void LostItems_AreCapped_Deduped_AndSaved()
        {
            var ps = new PlayerState(ClassId.Lamina);
            var first = new ItemInstance(WeaponDef.Adaga, Rarity.Raro, null, 0, WeaponPassive.Impeto);
            ps.AddLostItem(first);
            ps.AddLostItem(first);
            Assert.AreEqual(1, ps.LostItems.Count);
            for (int i = 0; i < PlayerState.MaxLostItems; i++) ps.AddLostItem(ItemInstance.Basic(WeaponDef.MacaPesada));
            Assert.AreEqual(PlayerState.MaxLostItems, ps.LostItems.Count);
            Assert.IsFalse(ps.LostItems.Contains(first));   // a mais antiga sai

            var p2 = new PlayerState(ClassId.Lamina);
            p2.AddLostItem(first);
            var loaded = SaveData.From(p2).ToPlayer();
            Assert.AreEqual(1, loaded.LostItems.Count);
            Assert.AreEqual(WeaponKind.Adaga, loaded.LostItems[0].Base.Kind);
            Assert.AreEqual(WeaponPassive.Impeto, loaded.LostItems[0].Passive);
        }

        [Test]
        public void InvalidData_IsIgnored()
        {
            var data = new SaveData { classId = 99, level = 500, xp = -5, gold = -10, activeSlot = 7 };
            data.skills.Add("nao-existe");
            data.items.Add(new ItemSave { weapon = "bazuca" });
            data.items.Add(new ItemSave { weapon = "adaga", rarity = 42, upgrade = 99, affixes = { new AffixSave { kind = 77, value = 5f } } });
            var ps = data.ToPlayer();
            Assert.AreEqual(ClassId.Lamina, ps.Class.Id);
            Assert.AreEqual(Progression.MvpMaxLevel, ps.Progression.Level);
            Assert.AreEqual(0, ps.Gold);
            Assert.AreEqual(WeaponKind.Espada, ps.Loadout[0].Base.Kind);   // arma desconhecida: fica a inicial
            Assert.AreEqual(WeaponKind.Adaga, ps.Loadout[1].Base.Kind);
            Assert.AreEqual(Rarity.Comum, ps.Loadout[1].Rarity);
            Assert.AreEqual(ItemInstance.MaxUpgrade, ps.Loadout[1].Upgrade);
            Assert.AreEqual(0, ps.Loadout[1].Affixes.Count);
            Assert.That(ps.ActiveSlot, Is.InRange(0, 1));
        }
    }
}
