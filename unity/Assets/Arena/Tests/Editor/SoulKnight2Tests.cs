using System.Linq;
using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    // 2ª leva Soul Knight: forja de fusão, armas de fogo, Xamã, NPCs/pets/mercenários.
    public class SoulKnight2Tests
    {
        [Test]
        public void FusionSameRarityGoesUpAndKeepsBestAffixes()
        {
            var keep = new ItemInstance(WeaponDef.Katana, Rarity.Incomum, new[] { new Affix(AffixKind.Dano, 0.1f) }, 1);
            var food = new ItemInstance(WeaponDef.Adaga, Rarity.Incomum, new[] { new Affix(AffixKind.Dano, 0.15f) }, 3);
            var r = Fusion.Fuse(keep, food, new System.Random(1));
            Assert.AreEqual(Rarity.Raro, r.Rarity);
            Assert.AreNotSame(WeaponDef.Katana, r.Base);   // as duas viram uma OUTRA arma
            Assert.AreNotSame(WeaponDef.Adaga, r.Base);
            Assert.AreSame(Fusion.ResultBase(WeaponDef.Katana, WeaponDef.Adaga), r.Base);
            Assert.AreSame(Fusion.ResultBase(WeaponDef.Adaga, WeaponDef.Katana), r.Base);   // receita não depende da ordem
            Assert.AreEqual(3, r.Upgrade);
            Assert.AreEqual(0.15f, r.Sum(AffixKind.Dano), 1e-4);
            Assert.AreNotEqual(WeaponPassive.Nenhuma, r.Passive);   // virou Rara: ganha passiva
        }

        [Test]
        public void FusionOnPlayerSpendsGoldAndEmptiesSecondSlot()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.SetGold(1000);
            var cost = Fusion.Cost(ps.Loadout[0], ps.Loadout[1]);
            Assert.IsTrue(Fusion.TryFuse(ps, new System.Random(2), out var res));
            Assert.AreEqual(1000 - cost, ps.Gold);
            Assert.IsNull(ps.Loadout[1 - ps.ActiveSlot]);
            Assert.AreSame(res, ps.Loadout[ps.ActiveSlot]);
            Assert.IsFalse(Fusion.TryFuse(ps, new System.Random(2), out _));   // sem 2ª arma
        }

        [Test]
        public void GunsShootHarderWithRarityAndAnyClassUsesThem()
        {
            Assert.IsTrue(WeaponDef.Pistola.IsGun && WeaponDef.Bacamarte.IsGun && !WeaponDef.Katana.IsGun);
            var comum = new ItemInstance(WeaponDef.Pistola).Resolve(ClassId.Elementalista);
            var epica = new ItemInstance(WeaponDef.Pistola, Rarity.Epico).Resolve(ClassId.Elementalista);
            Assert.IsTrue(comum.Proficient);
            Assert.AreEqual(GunRules.Pistola.Damage, GunRules.ShotDamage(GunRules.Pistola, comum), 0.01f);
            Assert.Greater(GunRules.ShotDamage(GunRules.Pistola, epica), GunRules.ShotDamage(GunRules.Pistola, comum));
            Assert.IsTrue(GunRules.IsReloadFlick(3f, 0.8f));
            Assert.IsFalse(GunRules.IsReloadFlick(3f, 0.1f));   // cano deitado não recarrega
            Assert.AreEqual(17, WeaponDef.All.Length);
        }

        [Test]
        public void ShamanIsAnEnemyLikeTheArcher()
        {
            Assert.AreEqual(EnemyKind.Xama, EnemyDef.Xama.Kind);
            Assert.IsTrue(EliteRules.CanBeElite(EnemyKind.Xama));
            Assert.Greater(LootTable.BaseGold(EnemyKind.Xama), 0);
        }

        [Test]
        public void AlliesAreSaved()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.Rescued.Add(NpcId.Domadora); ps.Rescued.Add(NpcId.Alquimista);
            ps.Pet = PetKind.FadaCurandeira;
            ps.ExtraPotionSlots = 2;
            ps.MercenaryHired = true;
            ps.SetPotions(5);
            var back = SaveData.From(ps).ToPlayer();
            Assert.IsTrue(back.Rescued.SetEquals(ps.Rescued));
            Assert.AreEqual(PetKind.FadaCurandeira, back.Pet);
            Assert.AreEqual(5, back.PotionCap);
            Assert.AreEqual(5, back.Potions);
            Assert.IsTrue(back.MercenaryHired);
            Assert.AreEqual(NpcId.Capitao, AllyRules.NextToRescue(n => back.Rescued.Contains(n)));
        }
    }
}
