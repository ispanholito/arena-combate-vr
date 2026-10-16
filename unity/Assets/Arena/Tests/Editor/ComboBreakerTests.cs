using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ComboBreakerTests
    {
        [Test]
        public void SpammingTheBoss_TriggersReaction()
        {
            var c = new ComboBreaker(EnemyKind.ReiGoblin);
            for (int i = 0; i < 3; i++) { Assert.IsFalse(c.RegisterHit(false)); c.Tick(0.3f); }
            Assert.IsTrue(c.RegisterHit(false));
        }

        [Test]
        public void SlowHits_DoNotTrigger()
        {
            var c = new ComboBreaker(EnemyKind.Goblin);
            for (int i = 0; i < 10; i++) { Assert.IsFalse(c.RegisterHit(false)); c.Tick(1.0f); }
        }

        [Test]
        public void BrokenPosture_AllowsFreeCombo()
        {
            var c = new ComboBreaker(EnemyKind.ReiGoblin);
            for (int i = 0; i < 10; i++) Assert.IsFalse(c.RegisterHit(true));
        }

        [Test]
        public void Boss_OnlyStaggersWithBrokenPosture()
        {
            Assert.IsFalse(ComboBreaker.CanBeStaggered(EnemyKind.ReiGoblin, false));
            Assert.IsTrue(ComboBreaker.CanBeStaggered(EnemyKind.ReiGoblin, true));
            Assert.IsTrue(ComboBreaker.CanBeStaggered(EnemyKind.Goblin, false));
            Assert.AreEqual(0, ComboBreaker.HitsToBreak(EnemyKind.Boneco));
        }
    }
}
