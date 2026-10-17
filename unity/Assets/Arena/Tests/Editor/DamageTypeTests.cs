using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class DamageTypeTests
    {
        [Test]
        public void Classify_ByWeaponAndMotion()
        {
            Assert.AreEqual(DamageType.Corte, DamageTypes.Classify(WeaponKind.Espada, 0.9f, 0.1f));
            Assert.AreEqual(DamageType.Perfuracao, DamageTypes.Classify(WeaponKind.Espada, 0.2f, 0.9f));
            Assert.AreEqual(DamageType.Contusao, DamageTypes.Classify(WeaponKind.Espada, 0.1f, 0.1f));   // de chapa
            Assert.IsTrue(DamageTypes.IsFlatHit(WeaponKind.Espada, DamageType.Contusao));
            Assert.AreEqual(DamageType.CorteMachado, DamageTypes.Classify(WeaponKind.Machado, 0.8f, 0.9f));
            Assert.AreEqual(DamageType.Contusao, DamageTypes.Classify(WeaponKind.Maca, 1f, 1f));
            Assert.IsFalse(DamageTypes.IsFlatHit(WeaponKind.Maca, DamageType.Contusao));
            Assert.AreEqual(DamageType.Perfuracao, DamageTypes.Classify(WeaponKind.Lanca, 0f, 0.95f));
        }

        [Test]
        public void EachWeapon_HasAJob()
        {
            var bone = DamageTypes.MaterialOf(EnemyKind.Esqueleto);
            var plate = DamageTypes.MaterialOf(EnemyKind.Orc);
            Assert.Greater(DamageTypes.Multiplier(bone, DamageType.Contusao), DamageTypes.Multiplier(bone, DamageType.Corte));      // maça no esqueleto
            Assert.Greater(DamageTypes.Multiplier(plate, DamageType.CorteMachado), DamageTypes.Multiplier(plate, DamageType.Corte)); // machado no orc
            Assert.Greater(DamageTypes.Multiplier(plate, DamageType.Perfuracao), DamageTypes.Multiplier(plate, DamageType.Corte));  // ponta nas frestas
            Assert.AreEqual(1f, DamageTypes.Multiplier(DamageTypes.MaterialOf(EnemyKind.Goblin), DamageType.Corte));               // goblin sem armadura
        }

        [Test]
        public void BodyParts_HeadStrong_LegsWeak()
        {
            Assert.AreEqual(2f, WeakPoints.Multiplier(EnemyKind.Goblin, 0.85f));
            Assert.AreEqual(1f, WeakPoints.Multiplier(EnemyKind.Goblin, 0.5f));
            Assert.AreEqual(WeakPoints.LegMultiplier, WeakPoints.Multiplier(EnemyKind.Goblin, 0.1f));
            Assert.AreEqual(1f, WeakPoints.Multiplier(EnemyKind.Slime, 0.1f));    // slime não tem pernas nem cabeça
        }

        [Test]
        public void Knockout_And_FatalStab_Rules()
        {
            Assert.IsTrue(DamageTypes.Knocksout(EnemyKind.Goblin, DamageType.Contusao, true, 5f));
            Assert.IsFalse(DamageTypes.Knocksout(EnemyKind.Goblin, DamageType.Contusao, false, 9f));   // só na cabeça
            Assert.IsFalse(DamageTypes.Knocksout(EnemyKind.Goblin, DamageType.Corte, true, 9f));
            Assert.IsFalse(DamageTypes.Knocksout(EnemyKind.ReiGoblin, DamageType.Contusao, true, 9f)); // chefe não
            Assert.IsTrue(DamageTypes.IsFatalStab(EnemyKind.Goblin, true, 4f));
            Assert.IsFalse(DamageTypes.IsFatalStab(EnemyKind.Goblin, true, 1f));
            Assert.IsFalse(DamageTypes.IsFatalStab(EnemyKind.Orc, true, 9f));
            Assert.IsFalse(DamageTypes.IsFatalStab(EnemyKind.ReiGoblin, true, 9f));
        }
    }
}
