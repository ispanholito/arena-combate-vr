using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ActiveRagdollTests
    {
        [Test]
        public void Rules_MatchBladeAndSorceryIdeas()
        {
            Assert.IsTrue(ActiveRagdollRules.Severs(RagBone.Cabeca, 6f, 0.8f, false));
            Assert.IsFalse(ActiveRagdollRules.Severs(RagBone.Tronco, 9f, 0.9f, false));   // tronco não sai
            Assert.IsFalse(ActiveRagdollRules.Severs(RagBone.BracoD, 9f, 0.9f, true));    // maça não corta
            Assert.IsFalse(ActiveRagdollRules.Severs(RagBone.BracoD, 4f, 0.9f, false));   // devagar
            Assert.IsTrue(ActiveRagdollRules.Knocksdown(RagBone.Tronco, 8f, false));
            Assert.IsTrue(ActiveRagdollRules.Knocksdown(RagBone.Tronco, 6.5f, true));     // arma pesada derruba mais fácil
            Assert.IsFalse(ActiveRagdollRules.Knocksdown(RagBone.BracoE, 9f, true));
            Assert.Greater(ActiveRagdollRules.StabDamperOut, ActiveRagdollRules.StabDamperIn);   // sai mais difícil do que entra
            Assert.IsTrue(ActiveRagdollRules.CanStab(2f, 0.7f, 0.1f));
            Assert.IsFalse(ActiveRagdollRules.CanStab(2f, 0.2f, 0.1f));                   // de lado não fura
            Assert.AreEqual(0f, ActiveRagdollRules.MuscleSpring(3f, ActiveRagdollRules.MuscleDead));
        }
    }
}
