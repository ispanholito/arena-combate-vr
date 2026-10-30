using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class EnemyMotionTests
    {
        [Test]
        public void ApproachStraightAndRoamOnlyWhenClose()
        {
            float a = EnemyMotion.PickApproachAngle(0.9);
            Assert.That(System.Math.Abs(a), Is.InRange(20f, 45f));
            Assert.AreEqual(0f, EnemyMotion.ApproachAngle(a, 10f), 1e-4);  // longe: vem RETO (2026-10-30)
            Assert.AreEqual(0f, EnemyMotion.Weave(1f, 0.3f, 8f));          // longe não ziguezagueia
            Assert.AreNotEqual(0f, EnemyMotion.Pace(0.5f, 0.3f, 2.5f));     // perto vai e volta
            Assert.IsTrue(EnemyMotion.Engaged(3f)); Assert.IsFalse(EnemyMotion.Engaged(6f));
            Assert.Less(EnemyMotion.PickApproachAngle(0.1), 0f);           // lados alternados
            Assert.Greater(EnemyMotion.SpeedFactor(8f), EnemyMotion.SpeedFactor(3f));
            Assert.AreEqual(0f, EnemyMotion.Weave(1f, 0f, 1.2f));          // colado não ziguezagueia
        }

        [Test]
        public void BiggerHitsFlinchLongerAndHarder()
        {
            Assert.Less(EnemyMotion.FlinchSeconds(0.03f), EnemyMotion.FlinchSeconds(0.2f));
            Assert.Less(EnemyMotion.FlinchTiltDeg(0.03f), EnemyMotion.FlinchTiltDeg(0.2f));
            Assert.LessOrEqual(EnemyMotion.FlinchTiltDeg(1f), 38f);
            Assert.Less(EnemyMotion.MeleeDodgeChance(Difficulty.Facil), EnemyMotion.MeleeDodgeChance(Difficulty.Dificil));
        }
    }
}
