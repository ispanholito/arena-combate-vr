using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class AuraTests
    {
        static int HitsToBreak(bool aura, float speed = 5f)
        {
            var s = new EnemyShieldState();
            int n = 0;
            while (!s.Broken && n < 100) { s.Hit(speed, aura, false, false); n++; }
            return n;
        }

        [Test]
        public void Shield_BreaksIn3to4WithAura_MuchSlowerWithout()
        {
            int aura = HitsToBreak(true), plain = HitsToBreak(false);
            Assert.That(aura, Is.InRange(3, 4));
            Assert.GreaterOrEqual(plain, 10);
            Assert.Less(plain, 20);   // sem aura demora, mas não trava
            Assert.AreEqual(0f, EnemyShieldState.Chip(1f, true, false, false));   // encostar não conta
        }

        [Test]
        public void Shield_CracksInStages()
        {
            var s = new EnemyShieldState();
            Assert.AreEqual(0, s.Stage);
            s.Hit(5f, true, false, false);
            Assert.AreEqual(1, s.Stage);
            s.Hit(5f, true, false, false);
            Assert.AreEqual(2, s.Stage);
            s.Hit(5f, true, false, false);
            Assert.AreEqual(3, s.Stage);
            Assert.IsTrue(s.Hit(5f, true, false, false));   // o 4º quebra
            Assert.IsFalse(s.Hit(5f, true, false, false));  // já quebrado
        }

        [Test]
        public void RedAura_ReplacesCounter_LastsAndBoosts()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.Progression.GainXp(70);
            p.Tree.Unlock(SkillIds.Contra);
            Assert.AreEqual(SkillCastResult.Ok, p.TryCast(SkillIds.Contra));
            Assert.IsTrue(p.AuraActive);
            Assert.AreEqual(RedAuraRules.Duration, p.RedAura, 1e-3);
            Assert.AreEqual(0f, p.CounterStance);   // o contra-ataque antigo não existe mais
            p.Tick(RedAuraRules.Duration + 0.1f);
            Assert.IsFalse(p.AuraActive);
            Assert.Greater(RedAuraRules.DamageMultiplier, 1f);
        }
    }
}
