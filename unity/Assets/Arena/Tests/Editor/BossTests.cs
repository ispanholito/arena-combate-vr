using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class BossTests
    {
        [Test]
        public void Boss_GuardsUntilPostureBreaks()
        {
            Assert.Less(BossRules.DamageTakenMultiplier(false), 0.7f);
            Assert.AreEqual(1f, BossRules.DamageTakenMultiplier(true));
        }

        [Test]
        public void HugMeter_TriggersOnlyWhenStayingClose()
        {
            var h = new HugMeter();
            Assert.IsFalse(h.Tick(1f, 1f));
            Assert.IsTrue(h.Tick(0.3f, 1f));          // 1,3 s colado: pisão
            Assert.AreEqual(0f, h.Value);
            h.Tick(1f, 1f);
            Assert.IsFalse(h.Tick(2f, 4f));           // se afastou: o medidor cai
            Assert.Less(h.Value, 0.1f);
            Assert.IsFalse(new HugMeter().Tick(5f, 3f));   // longe nunca pisa
        }

        [Test]
        public void Combos_OnlyInPhaseTwo()
        {
            Assert.AreEqual(1, BossRules.ComboLength(1, 0.1));
            Assert.AreEqual(2, BossRules.ComboLength(2, 0.1));
            Assert.AreEqual(3, BossRules.ComboLength(2, 0.9));
        }

        [Test]
        public void BossFight_IsSoloInArena()
        {
            var d = new WaveDirector(seed: 1);
            d.Tick(5f, 0, 0, 1, WaveDirector.KillsForBoss);   // chefe chegou
            Assert.AreEqual(0, d.TargetCount(5));
            d.BossPhase = 2;
            Assert.AreEqual(0, d.TargetCount(5), "estilo Souls: sem ajudantes nem na fúria");
        }
    }
}
