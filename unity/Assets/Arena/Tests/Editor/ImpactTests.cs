using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ImpactTests
    {
        [Test]
        public void HeavierEnemy_IsPushedLess()
        {
            Assert.Greater(ImpactReaction.KnockSpeed(5f, EnemyDef.Goblin.Mass), ImpactReaction.KnockSpeed(5f, EnemyDef.ReiGoblin().Mass));
        }

        [Test]
        public void Knock_AndLean_AreCapped()
        {
            Assert.AreEqual(ImpactReaction.MaxKnockSpeed, ImpactReaction.KnockSpeed(1000f, 1f), 1e-3);
            Assert.AreEqual(ImpactReaction.MaxLean, ImpactReaction.LeanKick(1000f, 1f), 1e-3);
        }

        [Test]
        public void OnlyStrongHits_Stagger()
        {
            Assert.IsFalse(ImpactReaction.Staggers(1f, EnemyDef.Goblin.Mass));
            Assert.IsTrue(ImpactReaction.Staggers(8f, EnemyDef.Goblin.Mass));
            Assert.IsFalse(ImpactReaction.Staggers(8f, EnemyDef.ReiGoblin().Mass), "o chefe aguenta mais");
        }
    }
}
