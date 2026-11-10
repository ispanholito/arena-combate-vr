using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class PumpTests
    {
        [Test]
        public void FullPumpCompletesOnceAndHalfPumpDoesNot()
        {
            var p = PumpRules.Phase.Closed;
            Assert.IsFalse(PumpRules.Step(ref p, 0.03f));
            Assert.IsFalse(PumpRules.Step(ref p, 0.10f));
            Assert.AreEqual(PumpRules.Phase.Open, p);
            Assert.IsFalse(PumpRules.Step(ref p, 0.02f));
            Assert.IsTrue(PumpRules.Step(ref p, 0f));
            Assert.AreEqual(PumpRules.Phase.Closed, p);
            // Meia bombeada: puxa pouco e volta — não conta.
            Assert.IsFalse(PumpRules.Step(ref p, 0.05f));
            Assert.IsFalse(PumpRules.Step(ref p, 0f));
            Assert.AreEqual(PumpRules.Phase.Closed, p);
        }

        [Test]
        public void FasterPumpIsReadySooner()
        {
            Assert.AreEqual(0f, PumpRules.ReadyDelay(2f), 1e-4f);
            Assert.AreEqual(PumpRules.MaxReadyDelay, PumpRules.ReadyDelay(0.1f), 1e-4f);
            Assert.Less(PumpRules.ReadyDelay(1.2f), PumpRules.ReadyDelay(0.6f));
        }
    }
}
