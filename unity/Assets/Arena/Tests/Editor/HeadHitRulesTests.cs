using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HeadHitRulesTests
    {
        [Test]
        public void Zones_NeckFaceCranium()
        {
            Assert.AreEqual(HeadZone.Pescoco, HeadHitRules.Zone(0.1f, 0.8f));
            Assert.AreEqual(HeadZone.Rosto, HeadHitRules.Zone(0.4f, 0.8f));
            Assert.AreEqual(HeadZone.Cranio, HeadHitRules.Zone(0.4f, -0.5f));
            Assert.AreEqual(HeadZone.Cranio, HeadHitRules.Zone(0.9f, 0.8f));
        }

        [Test]
        public void Blunt_KnocksSmall85_StunsBig65()
        {
            Assert.AreEqual(HeadOutcome.Derruba, HeadHitRules.Resolve(HeadZone.Cranio, true, false, false, false, false, 6f, 0f, 0.84));
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Cranio, true, false, false, false, false, 6f, 0f, 0.86));
            Assert.AreEqual(HeadOutcome.Atordoa, HeadHitRules.Resolve(HeadZone.Cranio, true, true, true, false, false, 6f, 0f, 0.6));
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Cranio, true, true, true, false, false, 6f, 0f, 0.7));
            // Tapinha não conta.
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Cranio, true, false, false, false, false, 2f, 0f, 0.0));
        }

        [Test]
        public void Blade_NeverKnocksDown_NeckKills_HelmetStuns30()
        {
            // Crânio sem capacete: nunca derruba.
            for (double r = 0; r < 1; r += 0.1)
                Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Cranio, false, false, false, false, false, 8f, 1f, r));
            // Capacete: 30% de atordoar.
            Assert.AreEqual(HeadOutcome.Atordoa, HeadHitRules.Resolve(HeadZone.Cranio, false, true, false, false, false, 6f, 1f, 0.29));
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Cranio, false, true, false, false, false, 6f, 1f, 0.31));
            // Pescoço: decapita (corte forte) ou pega a veia; esqueleto não tem veia; orc não morre pela veia.
            Assert.AreEqual(HeadOutcome.Decapita, HeadHitRules.Resolve(HeadZone.Pescoco, false, false, false, false, true, 6f, 1f, 0.5));
            Assert.AreEqual(HeadOutcome.Mata, HeadHitRules.Resolve(HeadZone.Pescoco, false, false, false, false, false, 4f, 1f, 0.5));
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Pescoco, false, false, false, true, false, 4f, 1f, 0.5));
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Pescoco, false, false, true, false, false, 4f, 1f, 0.5));
            // Rosto: só dano (mais que o crânio).
            Assert.AreEqual(HeadOutcome.Nada, HeadHitRules.Resolve(HeadZone.Rosto, false, false, false, false, false, 8f, 1f, 0.0));
            Assert.Greater(HeadHitRules.DamageMultiplier(HeadZone.Rosto, false, false), HeadHitRules.DamageMultiplier(HeadZone.Cranio, false, false));
        }
    }
}
