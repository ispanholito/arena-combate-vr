using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class WeaponAbilityTests
    {
        [Test]
        public void EveryWeaponKind_HasItsOwnAbility()
        {
            var kinds = WeaponDef.All.Select(w => WeaponAbilities.For(w.Kind).Kind).ToList();
            Assert.AreEqual(6, kinds.Distinct().Count());
            Assert.AreEqual(ResourceKind.Mana, WeaponAbilities.For(WeaponKind.Cajado).Resource);
            Assert.AreEqual(ResourceKind.Stamina, WeaponAbilities.For(WeaponKind.Machado).Resource);
        }

        [Test]
        public void Ability_SpendsResource_AndGoesOnCooldown()
        {
            var ps = new PlayerState(ClassId.Lamina);
            float st = ps.Stamina.Current;
            Assert.AreEqual(SkillCastResult.Ok, ps.TryWeaponAbility());
            Assert.AreEqual(st - WeaponAbilities.CorteAmplo.Cost, ps.Stamina.Current, 1e-3);
            Assert.AreEqual(SkillCastResult.Cooldown, ps.TryWeaponAbility());
            ps.Tick(WeaponAbilities.CorteAmplo.Cooldown + 0.1f);
            Assert.AreEqual(SkillCastResult.Ok, ps.CanUseWeaponAbility());
        }

        [Test]
        public void Legendary_AndQuickRecharge_ShortenCooldown()
        {
            var common = new ItemInstance(WeaponDef.Lanca);
            var legend = new ItemInstance(WeaponDef.Lanca, Rarity.Lendario);
            var quick = new ItemInstance(WeaponDef.Lanca, Rarity.Raro, null, 0, WeaponPassive.RecargaRapida);
            Assert.Less(WeaponAbilities.CooldownFor(legend), WeaponAbilities.CooldownFor(common));
            Assert.Less(WeaponAbilities.CooldownFor(quick), WeaponAbilities.CooldownFor(common));
            Assert.Greater(WeaponAbilities.Power(Rarity.Lendario), WeaponAbilities.Power(Rarity.Epico));
        }

        [Test]
        public void Passive_OnlyOnRareOrBetter_AndSurvivesSave()
        {
            Assert.AreEqual(WeaponPassive.Nenhuma, new ItemInstance(WeaponDef.Adaga, Rarity.Incomum, null, 0, WeaponPassive.Impeto).Passive);
            var loot = new LootTable(11);
            for (int i = 0; i < 300; i++)
            {
                var it = loot.RollItem();
                if (it.Rarity < Rarity.Raro) Assert.AreEqual(WeaponPassive.Nenhuma, it.Passive);
                else Assert.AreNotEqual(WeaponPassive.Nenhuma, it.Passive);
            }
            var ps = new PlayerState(ClassId.Lamina);
            ps.EquipToActive(new ItemInstance(WeaponDef.MacaPesada, Rarity.Epico, null, 1, WeaponPassive.SedeDeSangue));
            var loaded = SaveData.From(ps).ToPlayer();
            Assert.AreEqual(WeaponPassive.SedeDeSangue, loaded.ActiveItem.Passive);
        }

        [Test]
        public void Passives_RestoreResources()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.EquipToActive(new ItemInstance(WeaponDef.EspadaLonga, Rarity.Raro, null, 0, WeaponPassive.SedeDeSangue));
            ps.Hp.Drain(30f);
            float hp = ps.Hp.Current;
            ps.OnKillPassive();
            Assert.AreEqual(hp + WeaponAbilities.KillHeal, ps.Hp.Current, 1e-3);

            ps.EquipToActive(new ItemInstance(WeaponDef.EspadaLonga, Rarity.Raro, null, 0, WeaponPassive.Impeto));
            ps.Stamina.Drain(50f);
            float st = ps.Stamina.Current;
            ps.OnDealtDamage(10f, ps.ActiveWeapon);
            Assert.AreEqual(st + WeaponAbilities.HitStamina, ps.Stamina.Current, 1e-3);
        }

        [Test]
        public void Legendaries_HaveUniqueNames_AndEffects()
        {
            var names = WeaponDef.All.Select(w => new ItemInstance(w, Rarity.Lendario).DisplayName).ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count());
            Assert.AreEqual("Presa da Serpente", new ItemInstance(WeaponDef.Lanca, Rarity.Lendario).DisplayName);
            Assert.AreEqual("Presa da Serpente +2", new ItemInstance(WeaponDef.Lanca, Rarity.Lendario, null, 2).DisplayName);
            Assert.AreEqual(6, WeaponDef.All.Select(w => WeaponAbilities.LegendaryEffect(w.Kind)).Distinct().Count());   // um efeito por tipo
            var ps = new PlayerState(ClassId.Lamina);
            ps.ArmBackstab(WeaponAbilities.LegendaryBackstabMultiplier);
            Assert.AreEqual(3f, ps.BackstabMultiplier);
            ps.ArmBackstab();
            Assert.AreEqual(WeaponAbilities.BackstabMultiplier, ps.BackstabMultiplier);
        }

        [Test]
        public void Backstab_ArmsOnce_AndExpires()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.ArmBackstab();
            Assert.IsTrue(ps.ConsumeBackstab());
            Assert.IsFalse(ps.ConsumeBackstab());
            ps.ArmBackstab();
            ps.Tick(WeaponAbilities.BackstabWindow + 0.1f);
            Assert.IsFalse(ps.ConsumeBackstab());
        }

        [Test]
        public void Charge_RaiseThenSwingReleases_AndExpires()
        {
            var c = new WeaponCharge();
            Assert.AreEqual(WeaponCharge.Event.None, c.Tick(0.2f, true, 0f, true));
            Assert.AreEqual(WeaponCharge.Event.Charged, c.Tick(0.3f, true, 0f, true));
            Assert.AreEqual(WeaponCharge.Event.None, c.Tick(0.1f, true, 6f, true));   // ainda erguida: não solta
            Assert.AreEqual(WeaponCharge.Event.Released, c.Tick(0.05f, false, 6f, true));
            Assert.IsFalse(c.Charged);

            c.Tick(0.5f, true, 0f, true);
            Assert.IsTrue(c.Charged);
            Assert.AreEqual(WeaponCharge.Event.Expired, c.Tick(WeaponCharge.ChargedSeconds + 0.1f, false, 1f, true));

            Assert.AreEqual(WeaponCharge.Event.None, c.Tick(1f, true, 0f, false));   // não pode carregar (mirando magia)
            Assert.IsFalse(c.Charged);
        }
    }
}
