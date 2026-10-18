using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class CombatAITests
    {
        [Test]
        public void Tokens_LimitSimultaneousAttackers()
        {
            var pool = new AttackTokenPool { Capacity = CombatAI.AttackTokens(Difficulty.Normal) };
            Assert.IsTrue(pool.TryTake(1));
            Assert.IsTrue(pool.TryTake(2));
            Assert.IsTrue(pool.TryTake(3));       // normal: 3 atacam juntos (2026-10-18)
            Assert.IsFalse(pool.TryTake(4));      // o 4º espera
            Assert.IsTrue(pool.TryTake(1));       // quem já tem continua tendo
            pool.Release(1);
            Assert.IsTrue(pool.TryTake(4));
            Assert.AreEqual(3, pool.InUse);
            pool.Capacity = 0;                    // nunca trava todo mundo
            pool.Clear();
            Assert.IsTrue(pool.TryTake(9));
        }

        [Test]
        public void Difficulty_ChangesAggression()
        {
            Assert.Less(CombatAI.AttackTokens(Difficulty.Facil), CombatAI.AttackTokens(Difficulty.Dificil));
            Assert.Greater(CombatAI.AttackDelay(Difficulty.Facil).min, CombatAI.AttackDelay(Difficulty.Dificil).max);
            Assert.Greater(CombatAI.RiposteDelay(Difficulty.Facil).min, CombatAI.RiposteDelay(Difficulty.Dificil).min);
            Assert.Less(CombatAI.RiposteChance(Difficulty.Facil), CombatAI.RiposteChance(Difficulty.Dificil));
            Assert.AreEqual(0.5f, CombatAI.Pick((0f, 1f), 0.5), 1e-4);
        }

        [Test]
        public void HeavyHit_InterruptsAttack()
        {
            Assert.IsTrue(CombatAI.Interrupts(8f, 70f, Difficulty.Normal));    // 11%
            Assert.IsFalse(CombatAI.Interrupts(4f, 70f, Difficulty.Normal));   // 6%
            Assert.IsTrue(CombatAI.Interrupts(4f, 70f, Difficulty.Facil));     // fácil interrompe mais fácil
            Assert.IsFalse(CombatAI.Interrupts(8f, 70f, Difficulty.Dificil));
        }

        [Test]
        public void ComboWindow_BlocksDefenseRightAfterHit()
        {
            Assert.IsFalse(CombatAI.CanBlockAfterHit(0.3f));
            Assert.IsTrue(CombatAI.CanBlockAfterHit(0.8f));
        }
    }
}
