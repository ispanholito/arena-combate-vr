using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class TrainingTests
    {
        [Test]
        public void Dummy_NeverAttacksAndHealsFull()
        {
            var e = new EnemyState(EnemyDef.Boneco);
            Assert.AreEqual(0f, EnemyDef.Boneco.AttackDamage);
            e.TakeDamage(500f);
            Assert.Less(e.Hp, EnemyDef.Boneco.MaxHp);
            e.HealFull();
            Assert.AreEqual(EnemyDef.Boneco.MaxHp, e.Hp);
            Assert.IsTrue(e.Posture.Enabled, "dá para treinar quebra de postura");
        }

        [Test]
        public void DpsMeter_AveragesOverWindow()
        {
            var m = new DpsMeter(5f);
            for (int i = 0; i < 5; i++) { m.Add(10f); m.Tick(1f); }
            Assert.AreEqual(50f, m.Total, 1e-3);
            Assert.AreEqual(10f, m.Dps, 1e-3);
        }

        [Test]
        public void DpsMeter_ForgetsOldHits_AndStartsNewSequence()
        {
            var m = new DpsMeter(5f);
            m.Add(30f);
            m.Tick(6f);
            Assert.AreEqual(0f, m.Dps);
            m.Add(10f);
            Assert.AreEqual(10f, m.Total, 1e-3, "sequência nova zera o total");
        }
    }
}
