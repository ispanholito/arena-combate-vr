using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ExtractRulesTests
    {
        [Test]
        public void Extraction_SlidesGradually_NeverBack_AndFreesAtDepth()
        {
            float e = 0f;
            // Um puxão de 5 cm no começo (bem preso) tira menos que 5 cm.
            e = ExtractRules.Step(e, 0.05f, 1f);
            Assert.Greater(e, 0f); Assert.Less(e, 0.05f);
            // Empurrar de volta não enfia de novo.
            Assert.AreEqual(e, ExtractRules.Step(e, -0.1f, 1f));
            // Puxão gigante num quadro: limitado pela velocidade máxima (não sai de uma vez).
            float fast = ExtractRules.Step(e, 1f, 1f / 72f);
            Assert.LessOrEqual(fast - e, ExtractRules.MaxSpeed / 72f + 1e-5f);
            Assert.IsFalse(ExtractRules.IsFree(fast));
            // Puxando sem parar ~0,5 m de mão: sai.
            for (int i = 0; i < 200 && !ExtractRules.IsFree(e); i++) e = ExtractRules.Step(e, 0.005f, 1f / 72f);
            Assert.IsTrue(ExtractRules.IsFree(e));
            Assert.LessOrEqual(e, ExtractRules.Depth);
        }
    }
}
