using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void XpCurve_MatchesPrototype()
        {
            Assert.AreEqual(70, Progression.XpToNext(1));
            Assert.AreEqual(100, Progression.XpToNext(2));
        }

        [Test]
        public void GainXp_LevelsUpAndGivesPoints()
        {
            var p = new Progression();
            int gained = p.GainXp(70 + 100 + 5);
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, p.Level);
            Assert.AreEqual(2, p.UnspentPoints);
            Assert.AreEqual(5, p.Xp);
        }

        [Test]
        public void GainXp_StopsAtMaxLevel()
        {
            var p = new Progression(maxLevel: 3);
            p.GainXp(100000);
            Assert.AreEqual(3, p.Level);
            Assert.AreEqual(0, p.Xp);
            Assert.AreEqual(0, p.GainXp(50));
        }

        [Test]
        public void Death_LosesOnlyCurrentLevelXp()
        {
            var p = new Progression();
            p.GainXp(70 + 30);
            int lost = p.OnDeath();
            Assert.AreEqual(30, lost);
            Assert.AreEqual(2, p.Level, "nunca cai de nível");
            Assert.AreEqual(0, p.Xp);
        }

        [Test]
        public void MaxLevel_ClampedToGameMax()
        {
            Assert.AreEqual(Progression.GameMaxLevel, new Progression(99).MaxLevel);
        }
    }

    public class ClassProfileTests
    {
        [Test]
        public void Lamina_GetsMuchMoreStrengthThanElementalista()
        {
            Assert.Greater(ClassProfile.Lamina.Strength(10) - ClassProfile.Lamina.Strength(1),
                           2f * (ClassProfile.Elementalista.Strength(10) - ClassProfile.Elementalista.Strength(1)));
        }

        [Test]
        public void LevelRaisesHpStaminaMana()
        {
            foreach (var c in new[] { ClassProfile.Lamina, ClassProfile.Elementalista })
            {
                Assert.Greater(c.MaxHp(5), c.MaxHp(1));
                Assert.Greater(c.MaxStamina(5), c.MaxStamina(1));
                Assert.Greater(c.MaxMana(5), c.MaxMana(1));
                Assert.AreEqual(100f, c.MaxHp(1));
            }
        }
    }

    public class ResourcePoolTests
    {
        [Test]
        public void TrySpend_FailsWithoutEnough()
        {
            var r = new ResourcePool(10f);
            Assert.IsFalse(r.TrySpend(11f));
            Assert.AreEqual(10f, r.Current);
            Assert.IsTrue(r.TrySpend(4f));
            Assert.AreEqual(6f, r.Current);
        }

        [Test]
        public void Regen_WaitsForDelay()
        {
            var r = new ResourcePool(100f, 30f);
            r.TrySpend(50f, regenDelay: 1f);
            r.Tick(0.5f);
            Assert.AreEqual(50f, r.Current, 1e-3);
            r.Tick(0.5f);
            r.Tick(1f);
            Assert.AreEqual(80f, r.Current, 1e-3);
        }

        [Test]
        public void SetMax_KeepsCurrentUnlessFill()
        {
            var r = new ResourcePool(100f);
            r.Drain(40f);
            r.SetMax(120f, fill: false);
            Assert.AreEqual(60f, r.Current);
            r.SetMax(120f, fill: true);
            Assert.AreEqual(120f, r.Current);
        }
    }
}
