using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class DeathPenaltyTests
    {
        static PlayerState WithXpAndGold()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.Progression.GainXp(30);
            p.AddGold(101);
            return p;
        }

        [Test]
        public void Penalties_LoseWhatTheySay()
        {
            var none = WithXpAndGold(); none.Penalty = DeathPenalty.Nenhuma;
            none.Die();
            Assert.AreEqual(30, none.Progression.Xp); Assert.AreEqual(101, none.Gold);

            var xp = WithXpAndGold();   // padrão
            xp.Die();
            Assert.AreEqual(0, xp.Progression.Xp); Assert.AreEqual(101, xp.Gold);

            var gold = WithXpAndGold(); gold.Penalty = DeathPenalty.Ouro;
            gold.Die();
            Assert.AreEqual(0, gold.Progression.Xp); Assert.AreEqual(51, gold.Gold); Assert.AreEqual(50, gold.LastGoldLost);
            Assert.AreEqual(1, gold.Progression.Level);   // nunca cai de nível
        }

        [Test]
        public void OneLife_IsPermadeath_AndOptionCycles()
        {
            Assert.IsTrue(DeathRules.Permadeath(DeathPenalty.UmaVida));
            Assert.IsFalse(DeathRules.Permadeath(DeathPenalty.Ouro));
            var o = new GameOptions();
            Assert.AreEqual(DeathPenalty.Xp, o.DeathPenalty);
            for (int i = 0; i < 3; i++) o.CycleDeathPenalty();
            Assert.AreEqual(DeathPenalty.Nenhuma, o.DeathPenalty);
            o.deathPenalty = 99;
            Assert.AreEqual(DeathPenalty.Xp, o.Sanitized().DeathPenalty);
        }
    }
}
