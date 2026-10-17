using System.Numerics;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class GrabAndDismemberTests
    {
        [Test]
        public void Grab_OnlyLightEnemies_AndThrowAmplifiesHand()
        {
            Assert.IsTrue(GrabRules.CanGrab(EnemyKind.Goblin));
            Assert.IsTrue(GrabRules.CanGrab(EnemyKind.Slime));
            Assert.IsFalse(GrabRules.CanGrab(EnemyKind.Orc));
            Assert.IsFalse(GrabRules.CanGrab(EnemyKind.ReiGoblin));
            var slow = new Vector3(0.5f, 0f, 0f);
            Assert.AreEqual(slow, GrabRules.ThrowVelocity(slow, EnemyKind.Goblin));             // largou devagar: só cai
            var fast = new Vector3(0f, 0f, 5f);
            Assert.Greater(GrabRules.ThrowVelocity(fast, EnemyKind.Slime).Length(), GrabRules.ThrowVelocity(fast, EnemyKind.Goblin).Length());
            Assert.LessOrEqual(GrabRules.ThrowVelocity(new Vector3(0, 0, 50f), EnemyKind.Slime).Length(), GrabRules.MaxThrowSpeed + 1e-3f);
        }

        [Test]
        public void Decapitation_NeedsFastEdgeOnTheNeck()
        {
            Assert.IsTrue(DismemberRules.Decapitates(EnemyKind.Goblin, DamageType.Corte, true, 6f, 0.9f));
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.Goblin, DamageType.Corte, false, 9f, 0.9f));   // não foi na cabeça
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.Goblin, DamageType.Corte, true, 4f, 0.9f));    // devagar
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.Goblin, DamageType.Contusao, true, 9f, 0.9f)); // maça não corta
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.Goblin, DamageType.Corte, true, 9f, 0.2f));    // de chapa
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.Orc, DamageType.Corte, true, 9f, 0.9f));       // orc: só machado
            Assert.IsTrue(DismemberRules.Decapitates(EnemyKind.Orc, DamageType.CorteMachado, true, 8f, 0.9f));
            Assert.IsFalse(DismemberRules.Decapitates(EnemyKind.ReiGoblin, DamageType.CorteMachado, true, 9f, 0.9f));
        }

        [Test]
        public void Penetration_FastThrustGoesDeeper_AndStops()
        {
            float r = EnemyDef.Goblin.Radius, L = WeaponDef.EspadaLonga.Length;
            float Run(float speed)
            {
                float z = StabRules.StartZ(L + r, r, L), vp = speed * StabRules.InitialVelocityMultiplier;
                for (int i = 0; i < 120; i++) (z, vp) = StabRules.Penetrate(z, vp, 1f / 72f, r, L);
                return z;
            }
            float weak = Run(1.5f), strong = Run(5f);
            Assert.Less(strong, weak);                                   // golpe forte entra mais
            Assert.IsTrue(StabRules.IsDeep(strong, L));
            Assert.GreaterOrEqual(Run(50f), StabRules.GuardGap);         // nunca passa da guarda
            Assert.IsTrue(DamageTypes.IsFatalStab(EnemyKind.Goblin, true, StabRules.MinSpeed + 0.4f));
        }
    }
}
