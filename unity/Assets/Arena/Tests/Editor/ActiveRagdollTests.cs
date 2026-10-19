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

        [Test]
        public void A3_Disarm_Guard_AndAttackPoses()
        {
            Assert.IsTrue(ActiveRagdollRules.Disarms(RagBone.AntebracoD, 5f));
            Assert.IsTrue(ActiveRagdollRules.Disarms(RagBone.Clava, 4.5f));
            Assert.IsFalse(ActiveRagdollRules.Disarms(RagBone.AntebracoE, 9f));     // braço sem arma
            Assert.IsFalse(ActiveRagdollRules.Disarms(RagBone.BracoD, 2f));         // fraco
            Assert.IsFalse(ActiveRagdollRules.CanSever(RagBone.Clava));
            Assert.Less(ActiveRagdollRules.GuardChance(Difficulty.Facil), ActiveRagdollRules.GuardChance(Difficulty.Dificil));
            // Ataques laterais vêm de lados opostos e passam para o outro lado.
            var (rw, _) = ActiveRagdollPoses.Windup(EnemyAttack.DireitaEsquerda);
            var (rs, _) = ActiveRagdollPoses.Strike(EnemyAttack.DireitaEsquerda);
            var (lw, _) = ActiveRagdollPoses.Windup(EnemyAttack.EsquerdaDireita);
            var (ls, _) = ActiveRagdollPoses.Strike(EnemyAttack.EsquerdaDireita);
            Assert.Greater(rw.Z, 0f); Assert.Less(rs.Z, 0f);
            Assert.Less(lw.Z, 0f); Assert.Greater(ls.Z, 0f);
            Assert.Greater(ActiveRagdollPoses.Windup(EnemyAttack.Cima).arm.X, 90f);   // de cima: braço erguido
            var o = new GameOptions();
            Assert.AreEqual(1, o.TrainingGoblins);
            o.CycleTrainingGoblins(); Assert.AreEqual(3, o.TrainingGoblins);
            o.CycleTrainingGoblins(); Assert.AreEqual(6, o.TrainingGoblins);
            o.CycleTrainingGoblins(); Assert.AreEqual(1, o.TrainingGoblins);
        }
    }
}
