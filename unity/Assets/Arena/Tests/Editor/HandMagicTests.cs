using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HandMagicTests
    {
        [Test]
        public void Poses_RootsAndKamehameha()
        {
            Assert.IsTrue(HandMagicRules.RootsPose(0.2f, 0.1f, 0.3f));     // mãos juntas no chão
            Assert.IsFalse(HandMagicRules.RootsPose(0.6f, 0.1f, 0.2f));    // separadas
            Assert.IsFalse(HandMagicRules.RootsPose(0.2f, 0.9f, 1.1f));    // em pé (mãos no alto)
            Assert.IsTrue(HandMagicRules.KamePose(0.1f, 0.15f, 0.5f));     // pulsos juntos na altura do peito
            Assert.IsFalse(HandMagicRules.KamePose(0.4f, 0.15f, 0.5f));    // pulsos longe
            Assert.IsFalse(HandMagicRules.KamePose(0.1f, 0.15f, 1.4f));    // mãos baixas demais
        }

        [Test]
        public void Charge_GrowsAndCaps()
        {
            Assert.AreEqual(0f, HandMagicRules.FirePower(0.2f));
            Assert.Greater(HandMagicRules.FirePower(1f), HandMagicRules.FirePower(0.5f));
            Assert.AreEqual(1f, HandMagicRules.FirePower(5f), 1e-4);
            Assert.Greater(HandMagicRules.KameDamage(1f), HandMagicRules.KameDamage(0.3f));
        }

        [Test]
        public void Momentum_KeepsFlyingAfterRelease_AndStopsOnGround()
        {
            // Soltou a teia indo para frente a 6 m/s: no ar continua para frente (perde pouco) e cai.
            var (vx, vy, vz) = HandMagicRules.AirStep(0f, 2f, 6f, hanging: false, coasting: false, onGround: false, 0.1f);
            Assert.Greater(vz, 5.8f);
            Assert.Less(vy, 2f);
            // No chão o embalo some rápido.
            var g = HandMagicRules.AirStep(0f, 0f, 6f, false, false, true, 0.3f);
            Assert.Less(g.vz, 1f);
            // Segurando a teia sem puxar: mantém o embalo um instante (coast), depois freia.
            Assert.AreEqual(6f, HandMagicRules.AirStep(0f, 0f, 6f, true, true, false, 0.1f).vz, 1e-4);
            Assert.Less(HandMagicRules.AirStep(0f, 0f, 6f, true, false, false, 0.3f).vz, 3f);
        }
    }
}
