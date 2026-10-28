using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class SpiderLocomotionTests
    {
        const float Dt = 1f / 72f;

        static SpiderAction Run(SpiderSignDetector d, bool sign, float up, float seconds)
        {
            var last = SpiderAction.None;
            for (float t = 0; t < seconds; t += Dt) { var a = d.Update(sign, up, Dt); if (a != SpiderAction.None) last = a; }
            return last;
        }

        [Test]
        public void SignTwice_IsDashBack()
        {
            var d = new SpiderSignDetector();
            Assert.AreEqual(SpiderAction.None, Run(d, true, 0f, 0.15f));     // 1º sinal, mão para frente
            Assert.AreEqual(SpiderAction.None, Run(d, false, 0f, 0.15f));
            Assert.AreEqual(SpiderAction.DashBack, Run(d, true, 0f, 0.1f));  // 2º sinal dentro de 0,6 s
        }

        [Test]
        public void SignTwice_TooSlow_IsNothing()
        {
            var d = new SpiderSignDetector();
            Run(d, true, 0f, 0.15f);
            Run(d, false, 0f, 0.8f);
            Assert.AreEqual(SpiderAction.None, Run(d, true, 0f, 0.1f));
        }

        [Test]
        public void SignPointingUp_ShootsRope_Once()
        {
            var d = new SpiderSignDetector();
            Assert.AreEqual(SpiderAction.Rope, Run(d, true, 0.9f, 0.3f));
            Assert.AreEqual(SpiderAction.None, Run(d, true, 0.9f, 0.5f));    // segurando: não dispara de novo
            Run(d, false, 0f, 0.2f);
            Assert.AreEqual(SpiderAction.None, Run(d, true, 0f, 0.1f));      // depois da teia, o próximo sinal não vira dash
            Assert.AreEqual(SpiderAction.None, Run(d, true, 0.3f, 0.3f));    // de lado não dispara
        }
    }
}
