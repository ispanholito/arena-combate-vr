using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class BlessingTests
    {
        [TearDown] public void Reset() => RunBlessings.Current = null;

        [Test]
        public void ChoicesAreDistinctAndSkipOwned()
        {
            var b = new RunBlessings();
            b.Add(BlessingId.ForcaBruta);
            for (int s = 0; s < 50; s++)
            {
                var c = b.RollChoices(new System.Random(s));
                Assert.AreEqual(3, c.Count);
                Assert.AreEqual(3, new System.Collections.Generic.HashSet<BlessingId>(c).Count);
                Assert.IsFalse(c.Contains(BlessingId.ForcaBruta));
            }
        }

        [Test]
        public void DamageBonusAndExecutorApplyToEnemies()
        {
            var ps = new PlayerState(ClassId.Lamina);
            RunBlessings.Current = ps.Blessings;
            var e = new EnemyState(EnemyDef.Goblin);
            var plain = e.TakeDamage(10f, 0f, false).Amount;
            ps.ChooseBlessing(BlessingId.ForcaBruta);
            var e2 = new EnemyState(EnemyDef.Goblin);
            Assert.AreEqual(plain * 1.2f, e2.TakeDamage(10f, 0f, false).Amount, 1f);
            ps.ChooseBlessing(BlessingId.Sanguessuga);
            ps.Hp.Drain(50f);
            float hp0 = ps.Hp.Current;
            new EnemyState(EnemyDef.Goblin).TakeDamage(25f, 0f, false);
            ps.Tick(0f);
            Assert.Greater(ps.Hp.Current, hp0);
        }

        [Test]
        public void HeartRaisesMaxHpAndClearRemovesIt()
        {
            var ps = new PlayerState(ClassId.Lamina);
            float max = ps.Hp.Max;
            ps.ChooseBlessing(BlessingId.CoracaoForte);
            Assert.AreEqual(max + RunBlessings.HpBonus, ps.Hp.Max, 0.01f);
            ps.ClearBlessings();
            Assert.AreEqual(max, ps.Hp.Max, 0.01f);
        }

        [Test]
        public void SecondChanceSavesOnce()
        {
            var ps = new PlayerState(ClassId.Lamina);
            int deaths = 0, saves = 0;
            ps.Died += _ => deaths++;
            ps.SecondChance += () => saves++;
            ps.ChooseBlessing(BlessingId.SegundaChance);
            ps.TakeDamage(9999f);
            Assert.AreEqual(1, saves); Assert.AreEqual(0, deaths);
            Assert.AreEqual(ps.Hp.Max * 0.5f, ps.Hp.Current, 1f);
            ps.Tick(5f);
            ps.TakeDamage(9999f);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void StoneSkinCutsDamage()
        {
            var ps = new PlayerState(ClassId.Lamina);
            ps.ChooseBlessing(BlessingId.PeleDePedra);
            float hp = ps.Hp.Current;
            ps.TakeDamage(50f);
            Assert.AreEqual(hp - 40f, ps.Hp.Current, 0.01f);
        }

        [Test]
        public void AltarEveryOtherRoom()
        {
            Assert.IsTrue(RunBlessings.AltarAfterRoom(1));
            Assert.IsFalse(RunBlessings.AltarAfterRoom(2));
            Assert.IsTrue(RunBlessings.AltarAfterRoom(3));
        }
    }
}
