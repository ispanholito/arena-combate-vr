using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HandMagicTests
    {
        [Test]
        public void Poses_RootsAndKamehameha()
        {
            Assert.IsTrue(HandMagicRules.IsClap(0.05f, 1f));               // palmas batendo
            Assert.IsFalse(HandMagicRules.IsClap(0.05f, 0.1f));            // juntas devagar não é palma
            Assert.IsTrue(HandMagicRules.RootsSlam(0.4f, -2f, -2.2f));     // separou e jogou as duas para baixo
            Assert.IsFalse(HandMagicRules.RootsSlam(0.4f, -2f, 0f));       // só uma mão
            Assert.IsFalse(HandMagicRules.RootsSlam(0.05f, -2f, -2f));     // ainda juntas
            // Onda: começa com as mãos juntas puxadas para o lado; arma com a garra (um pouco abertas, pulsos juntos).
            Assert.IsTrue(HandMagicRules.KameStart(0.1f, 0.12f, 0.25f));     // juntas, ao lado direito
            Assert.IsTrue(HandMagicRules.KameStart(0.1f, 0.12f, -0.25f));    // ou ao esquerdo
            Assert.IsFalse(HandMagicRules.KameStart(0.1f, 0.12f, 0.05f));    // ainda na frente
            Assert.IsTrue(HandMagicRules.KameClaw(0.15f, 0.12f));
            Assert.IsFalse(HandMagicRules.KameClaw(0.02f, 0.12f));           // fechadas (palma colada)
            Assert.IsFalse(HandMagicRules.KameClaw(0.45f, 0.12f));           // abertas demais
        }

        [Test]
        public void Homing_PrefersDirectionOverDistance()
        {
            // Arqueiro longe na direção da mira (5°, 20 m) vence o goblin do lado (40°, 1,5 m) e o de 25° a 3 m.
            Assert.AreEqual(0, HandMagicRules.PickHomingTarget(new[] { 5f, 40f, 25f }, new[] { 20f, 1.5f, 3f }));
            // Quase o mesmo ângulo: o mais perto.
            Assert.AreEqual(1, HandMagicRules.PickHomingTarget(new[] { 6f, 4f }, new[] { 20f, 5f }));
            Assert.AreEqual(1, HandMagicRules.PickHomingTarget(new[] { 4f, 6f }, new[] { 20f, 5f }));
            // Ninguém no cone: sem alvo (vai reto).
            Assert.AreEqual(-1, HandMagicRules.PickHomingTarget(new[] { 50f }, new[] { 3f }));
        }

        [Test]
        public void FlameBlade_SwordsYes_StaffNo_RefreshCaps()
        {
            Assert.IsTrue(FlameBladeRules.CanIgnite(WeaponKind.Espada));
            Assert.IsTrue(FlameBladeRules.CanIgnite(WeaponKind.Machado));
            Assert.IsFalse(FlameBladeRules.CanIgnite(WeaponKind.Cajado));
            Assert.AreEqual(FlameBladeRules.Seconds, FlameBladeRules.Refresh(3f), 1e-4);
            Assert.Greater(FlameBladeRules.DamageMultiplier, 1f);
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
