using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class TwoHandTests
    {
        [Test]
        public void TwoHands_MakeHeavyAxeMoreResponsive()
        {
            float str = ClassProfile.Lamina.Strength(1);
            var axe = WeaponDef.MachadoPesado;
            Assert.Greater(axe.FollowRate(TwoHandRules.EffectiveStrength(str, true)), axe.FollowRate(TwoHandRules.EffectiveStrength(str, false)));
        }

        [Test]
        public void DamageBonus_OnlyForHeavyTwoHanded()
        {
            Assert.AreEqual(TwoHandRules.HeavyDamageBonus, TwoHandRules.DamageMultiplier(WeaponDef.MachadoPesado, true));
            Assert.AreEqual(1f, TwoHandRules.DamageMultiplier(WeaponDef.MachadoPesado, false));
            Assert.AreEqual(1f, TwoHandRules.DamageMultiplier(WeaponDef.EspadaLonga, true));
        }

        [Test]
        public void Grip_IsClampedToHandle()
        {
            Assert.AreEqual(0.8f, TwoHandRules.ClampToGrip(WeaponKind.Machado, 1.5f), 1e-4);
            Assert.AreEqual(-0.12f, TwoHandRules.ClampToGrip(WeaponKind.Espada, -1f), 1e-4);
            Assert.AreEqual(0.4f, TwoHandRules.ClampToGrip(WeaponKind.Machado, 0.4f), 1e-4);
        }
    }
}
