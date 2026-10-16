using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class TacticsTests
    {
        [Test]
        public void Slots_SpreadSymmetrically_AndClamp()
        {
            Assert.AreEqual(0f, GoblinTactics.SlotAngle(0, 1));
            Assert.AreEqual(-GoblinTactics.SlotAngle(0, 2), GoblinTactics.SlotAngle(1, 2), 1e-4);
            Assert.AreEqual(0f, GoblinTactics.SlotAngle(1, 3), 1e-4);
            Assert.AreEqual(GoblinTactics.SlotSpacingDeg, GoblinTactics.SlotAngle(2, 3), 1e-4);
            Assert.AreEqual(GoblinTactics.MaxSlotDeg, GoblinTactics.SlotAngle(9, 10), 1e-4);
        }

        [Test]
        public void Block_OnlyFromFront_WithLuck_AndHeavyHitsBreakThrough()
        {
            Assert.AreEqual(GoblinTactics.BlockOutcome.Blocked, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 1f, 0.1, 4f, 2.5f, false));
            Assert.AreEqual(GoblinTactics.BlockOutcome.None, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 0.2f, 0.1, 4f, 2.5f, false));   // de lado
            Assert.AreEqual(GoblinTactics.BlockOutcome.None, GoblinTactics.ResolveBlock(EnemyKind.Goblin, false, 1f, 0.1, 4f, 2.5f, false));
            Assert.AreEqual(GoblinTactics.BlockOutcome.None, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 1f, 0.9, 4f, 2.5f, false));
            Assert.AreEqual(GoblinTactics.BlockOutcome.BrokeThrough, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 1f, 0.1, 9f, 2.5f, false));
            Assert.AreEqual(GoblinTactics.BlockOutcome.BrokeThrough, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 1f, 0.1, 4f, 14f, false));   // machado
            Assert.AreEqual(GoblinTactics.BlockOutcome.BrokeThrough, GoblinTactics.ResolveBlock(EnemyKind.Goblin, true, 1f, 0.1, 4f, 2.5f, true));   // duas mãos
            Assert.AreEqual(GoblinTactics.BlockOutcome.None, GoblinTactics.ResolveBlock(EnemyKind.Slime, true, 1f, 0.0, 4f, 2.5f, false));
            Assert.Less(GoblinTactics.BlockChance(EnemyKind.Esqueleto), GoblinTactics.BlockChance(EnemyKind.Goblin));
        }

        [Test]
        public void Retreat_OnlyHurtGoblin_Once()
        {
            Assert.IsTrue(GoblinTactics.ShouldRetreat(EnemyKind.Goblin, 0.2f, false, 0.1));
            Assert.IsFalse(GoblinTactics.ShouldRetreat(EnemyKind.Goblin, 0.2f, true, 0.1));
            Assert.IsFalse(GoblinTactics.ShouldRetreat(EnemyKind.Goblin, 0.5f, false, 0.1));
            Assert.IsFalse(GoblinTactics.ShouldRetreat(EnemyKind.Goblin, 0.2f, false, 0.9));
            Assert.IsFalse(GoblinTactics.ShouldRetreat(EnemyKind.Esqueleto, 0.2f, false, 0.1));
            Assert.IsFalse(GoblinTactics.ShouldRetreat(EnemyKind.Goblin, 0f, false, 0.1));
        }
    }
}
