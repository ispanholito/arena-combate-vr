using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ItemTests
    {
        [Test]
        public void AllWeapons_WithUniqueIds()
        {
            Assert.AreEqual(18, WeaponDef.All.Length);   // 6 originais + 8 da leva Soul Knight + 3 armas de fogo + escudo
            Assert.AreEqual(WeaponDef.All.Length, WeaponDef.All.Select(w => w.Id).Distinct().Count());
        }

        [Test]
        public void StabAndSever_DependOnWeaponKind()
        {
            Assert.IsTrue(StabRules.CanStab(WeaponKind.Lanca, 3f, 1f, 0.1f));
            Assert.IsTrue(StabRules.CanStab(WeaponKind.Adaga, 3f, 1f, 0.1f));
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Maca, 3f, 1f, 0.1f));
            Assert.IsFalse(SkeletonBody.CanSever(WeaponKind.Maca, 9f, 1f));
            Assert.IsFalse(SkeletonBody.CanSever(WeaponKind.Lanca, 9f, 1f));
            Assert.IsTrue(SkeletonBody.CanSever(WeaponKind.Machado, 9f, 1f));
        }

        [Test]
        public void Mace_BreaksMorePosture_SpearHitsOnlyWithTip()
        {
            Assert.Greater(WeaponDef.MacaPesada.PostureFactor, WeaponDef.EspadaLonga.PostureFactor);
            var s = WeaponStrike.For(WeaponDef.Lanca);
            Assert.Greater(s.A.Z, 1.3f);
            Assert.AreEqual(WeaponDef.Lanca.Length, s.B.Z, 1e-4);
        }

        [Test]
        public void Rarity_Affixes_AndUpgrade_RaiseDamage()
        {
            var basic = new ItemInstance(WeaponDef.EspadaLonga);
            var rare = new ItemInstance(WeaponDef.EspadaLonga, Rarity.Raro, new[] { new Affix(AffixKind.Dano, 0.1f) }, upgrade: 2);
            Assert.AreEqual(1f, basic.DamageMultiplier, 1e-4);
            Assert.AreEqual(1.1f * 1.1f * 1.12f, rare.DamageMultiplier, 1e-4);
            Assert.Greater(rare.Resolve(ClassId.Lamina).DamageMultiplier, basic.Resolve(ClassId.Lamina).DamageMultiplier);
            Assert.AreEqual("Espada Longa Rara +2", rare.DisplayName);
        }

        [Test]
        public void Affixes_ChangeWeightStaminaPostureLifeSteal()
        {
            var item = new ItemInstance(WeaponDef.MachadoPesado, Rarity.Epico, new[]
            {
                new Affix(AffixKind.Leveza, 0.2f), new Affix(AffixKind.Folego, 0.25f), new Affix(AffixKind.Postura, 0.3f),
            });
            var w = item.Resolve(ClassId.Lamina);
            Assert.AreEqual(14f * 0.8f, w.Weight, 1e-4);
            Assert.AreEqual(WeaponDef.MachadoPesado.StaminaPerHit * 0.75f, w.StaminaPerHit, 1e-4);
            Assert.AreEqual(1.3f, w.PostureBonus, 1e-4);
            Assert.Greater(w.FollowRate(10f), WeaponDef.MachadoPesado.FollowRate(10f));   // mais leve = segue melhor a mão

            var vamp = new ItemInstance(WeaponDef.Adaga, Rarity.Incomum, new[] { new Affix(AffixKind.RouboDeVida, 0.1f) }).Resolve(ClassId.Lamina);
            var ps = new PlayerState(ClassId.Lamina);
            ps.Hp.Drain(50f);
            float before = ps.Hp.Current;
            ps.OnDealtDamage(30f, vamp);
            Assert.AreEqual(before + 3f, ps.Hp.Current, 1e-3);
        }

        [Test]
        public void Execute_OnlyBelowThreshold()
        {
            var w = new ItemInstance(WeaponDef.EspadaLonga, Rarity.Raro, new[] { new Affix(AffixKind.Execucao, 0.4f) }).Resolve(ClassId.Lamina);
            Assert.AreEqual(1f, Combat.ExecuteMultiplier(w, 0.5f), 1e-4);
            Assert.AreEqual(1.4f, Combat.ExecuteMultiplier(w, 0.2f), 1e-4);
        }

        [Test]
        public void Proficiency_PenalizesOffClass_AndBlocksTreeBonus()
        {
            var staff = ItemInstance.Basic(WeaponDef.CajadoArcano);
            var asMage = staff.Resolve(ClassId.Elementalista);
            var asBlade = staff.Resolve(ClassId.Lamina);
            Assert.IsTrue(asMage.Proficient);
            Assert.IsFalse(asBlade.Proficient);
            Assert.AreEqual(asMage.DamageMultiplier * ItemInstance.OffClassPenalty, asBlade.DamageMultiplier, 1e-4);
            Assert.IsFalse(Combat.SharpEdge(true, asBlade));
            Assert.IsTrue(Combat.SharpEdge(true, ItemInstance.Basic(WeaponDef.EspadaLonga).Resolve(ClassId.Lamina)));
            Assert.IsFalse(Proficiency.CanCastSpells(ClassId.Elementalista, WeaponKind.Adaga));
            Assert.IsTrue(Proficiency.CanCastSpells(ClassId.Elementalista, WeaponKind.Cajado));
            Assert.IsTrue(Proficiency.CanCastSpells(ClassId.Lamina, WeaponKind.Adaga));
        }

        [Test]
        public void StartingWeapons_AndSwap()
        {
            var ps = new PlayerState(ClassId.Lamina);
            Assert.AreEqual(WeaponKind.Espada, ps.Loadout[0].Base.Kind);
            Assert.IsTrue(ps.LeftItem.Base.IsShield);   // Lâmina: espada na direita, escudo na esquerda
            Assert.AreEqual(WeaponKind.Machado, ps.Holsters[PlayerState.OmbroD].Base.Kind);   // machado (grande) no ombro
            var mace = ItemInstance.Basic(WeaponDef.MacaPesada);
            var old = ps.EquipToActive(mace);
            Assert.AreEqual(WeaponKind.Espada, old.Base.Kind);
            Assert.AreSame(mace, ps.ActiveItem);

            var mage = new PlayerState(ClassId.Elementalista);
            Assert.AreEqual(WeaponKind.Cajado, mage.ActiveItem.Base.Kind);
            Assert.IsNull(mage.LeftItem);
            Assert.AreEqual(WeaponKind.Adaga, mage.Holsters[PlayerState.CinturaD].Base.Kind);   // adaga (pequena) na cintura
        }

        [Test]
        public void TakeItem_GoesToHand_OldToFreeHolster()
        {
            var ps = new PlayerState(ClassId.Lamina);
            var spear = ItemInstance.Basic(WeaponDef.Lanca);
            Assert.IsNull(ps.TakeItem(spear, out bool stored));   // a espada foi para um coldre da cintura
            Assert.IsTrue(stored);
            Assert.AreSame(spear, ps.ActiveItem);
            Assert.AreEqual(WeaponKind.Espada, ps.Holsters[PlayerState.CinturaD].Base.Kind);
            // Lança é grande: só cabe no ombro. O ombro E está livre (o D tem o machado).
            var mace = ItemInstance.Basic(WeaponDef.MacaPesada);
            Assert.IsNull(ps.TakeItem(mace, out stored));
            Assert.AreSame(spear, ps.Holsters[PlayerState.OmbroE]);
            // Mão esquerda pega a sua; o escudo que estava nela não cabe em lugar nenhum (ombros cheios): sai.
            var dagger = ItemInstance.Basic(WeaponDef.Adaga);
            var old = ps.TakeItem(dagger, out stored, hand: 1);
            Assert.IsFalse(stored);
            Assert.IsTrue(old.Base.IsShield);
            Assert.AreSame(dagger, ps.LeftItem);
        }

        [Test]
        public void Holsters_ShouldersTakeAnything_HipsOnlySmall()
        {
            var shield = ItemInstance.Basic(WeaponDef.Escudo);
            var sword = ItemInstance.Basic(WeaponDef.EspadaLonga);
            Assert.IsTrue(WeaponDef.Escudo.IsLarge && WeaponDef.Lanca.IsLarge && WeaponDef.MachadoPesado.IsLarge && WeaponDef.Martelo.IsLarge);
            Assert.IsFalse(WeaponDef.EspadaLonga.IsLarge || WeaponDef.Adaga.IsLarge || WeaponDef.Machadinha.IsLarge || WeaponDef.Pistola.IsLarge);
            Assert.IsTrue(PlayerState.Fits(PlayerState.OmbroE, shield));
            Assert.IsFalse(PlayerState.Fits(PlayerState.CinturaE, shield));
            Assert.IsTrue(PlayerState.Fits(PlayerState.CinturaE, sword));
            Assert.IsTrue(PlayerState.Fits(PlayerState.OmbroD, sword));
            var ps = new PlayerState(ClassId.Elementalista);
            Assert.IsFalse(ps.StoreInHolster(PlayerState.CinturaE, shield));
            Assert.IsTrue(ps.StoreInHolster(PlayerState.OmbroE, shield));
            Assert.IsFalse(ps.StoreInHolster(PlayerState.OmbroE, sword));   // ocupado
            Assert.AreSame(shield, ps.TakeFromHolster(PlayerState.OmbroE));
            Assert.IsNull(ps.Holsters[PlayerState.OmbroE]);
        }

        [Test]
        public void ChangingClass_KeepsItems_ButGuaranteesStaff()
        {
            var ps = new PlayerState(ClassId.Lamina);
            var rare = new ItemInstance(WeaponDef.Lanca, Rarity.Raro);
            ps.EquipToActive(rare);
            ps.SetClass(ClassId.Elementalista);
            Assert.AreEqual(WeaponKind.Cajado, ps.ActiveItem.Base.Kind);
            Assert.IsTrue(ps.Holsters.Contains(rare) || ps.LostItems.Contains(rare));   // a lança vai para um coldre (ou estante)
        }

        [Test]
        public void Smithy_SpendsGold_UpToPlusFive()
        {
            var ps = new PlayerState(ClassId.Lamina);
            var item = ps.ActiveItem;
            Assert.IsFalse(Smithy.TryUpgrade(item, ps));   // sem ouro
            ps.AddGold(10000);
            int spent = 0;
            for (int i = 0; i < 5; i++) { spent += item.UpgradeCost; Assert.IsTrue(Smithy.TryUpgrade(item, ps)); }
            Assert.AreEqual(5, item.Upgrade);
            Assert.IsFalse(Smithy.TryUpgrade(item, ps));
            Assert.AreEqual(10000 - spent, ps.Gold);
            Assert.AreEqual(40 * 1 * 1 + 40 * 2 + 40 * 3 + 40 * 4 + 40 * 5, spent);
        }

        [Test]
        public void Loot_RarityWeights_AndBossMinimum()
        {
            var loot = new LootTable(7);
            int[] counts = new int[5];
            for (int i = 0; i < 20000; i++) counts[(int)loot.RollRarity()]++;
            Assert.Greater(counts[0], counts[1]);
            Assert.Greater(counts[1], counts[2]);
            Assert.Greater(counts[2], counts[3]);
            Assert.AreEqual(0.6, counts[0] / 20000.0, 0.02);
            for (int i = 0; i < 200; i++)
            {
                var drop = loot.RollDrop(EnemyKind.ReiGoblin);
                Assert.NotNull(drop);
                Assert.GreaterOrEqual((int)drop.Rarity, (int)Rarity.Raro);
                Assert.LessOrEqual(drop.Affixes.Count, ItemInstance.MaxAffixes(drop.Rarity));
                Assert.AreEqual(drop.Affixes.Count, drop.Affixes.Select(a => a.Kind).Distinct().Count());
            }
            Assert.IsNull(new LootTable(1).RollDrop(EnemyKind.Boneco));
        }

        [Test]
        public void Gold_VariesAroundBase()
        {
            var loot = new LootTable(3);
            for (int i = 0; i < 100; i++)
            {
                int g = loot.RollGold(EnemyKind.Goblin);
                Assert.GreaterOrEqual(g, 4);
                Assert.LessOrEqual(g, 8);
            }
            Assert.AreEqual(0, loot.RollGold(EnemyKind.Boneco));
        }
    }
}
