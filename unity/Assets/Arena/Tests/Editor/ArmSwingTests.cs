using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ArmSwingTests
    {
        // Simula os braços balançando em oposição (seno), dt fixo.
        static void Swing(ArmSwingDetector d, float seconds, float amp, bool opposite = true, bool low = true)
        {
            for (float t = 0f; t < seconds; t += 0.02f)
            {
                float v = amp * (float)System.Math.Sin(t * 9f);
                d.Update(v, opposite ? -v : v, low, 0.02f);
            }
        }

        [Test]
        public void AlternatingArmsRunAndStop()
        {
            var d = new ArmSwingDetector();
            Swing(d, 1.5f, 2.5f);
            Assert.IsTrue(d.Running);
            Assert.Greater(d.Speed, 1f);
            for (int i = 0; i < 40; i++) d.Update(0f, 0f, true, 0.02f);   // parou de balançar
            Assert.IsFalse(d.Running);
        }

        [Test]
        public void SameDirectionOrOneHandDoesNotRun()
        {
            var d = new ArmSwingDetector();
            Swing(d, 2f, 2.5f, opposite: false);   // as duas para o mesmo lado (palmas juntas, Kamehameha)
            Assert.IsFalse(d.Running);
            var e = new ArmSwingDetector();
            for (int i = 0; i < 100; i++) e.Update(3f, 0f, true, 0.02f);   // uma mão só (corte)
            Assert.IsFalse(e.Running);
            var f = new ArmSwingDetector();
            Swing(f, 2f, 2.5f, low: false);   // braços no alto não contam
            Assert.IsFalse(f.Running);
        }

        [Test]
        public void FasterSwingRunsFasterButCapped()
        {
            var slow = new ArmSwingDetector(); Swing(slow, 2f, 1.5f);
            var fast = new ArmSwingDetector(); Swing(fast, 2f, 4f);
            Assert.Greater(fast.Speed, slow.Speed);
            Assert.LessOrEqual(fast.Speed, ArmSwingDetector.MaxSpeed);
        }

        [Test]
        public void PalmCutNeedsArmExtendedInFront()
        {
            Assert.IsTrue(PalmSweepRules.IsExtendedInFront(0.5f, 0.2f));
            Assert.IsFalse(PalmSweepRules.IsExtendedInFront(0.15f, 0f));   // perto do corpo (corridinha) não corta
            Assert.IsFalse(PalmSweepRules.IsExtendedInFront(0.4f, 0.6f));  // do lado não corta
        }
    }
}
