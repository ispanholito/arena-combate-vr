using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class SkillTreeTests
    {
        [Test]
        public void StartingSkill_IsFree()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.IsTrue(p.Has(SkillIds.Investida));
            Assert.AreEqual(0, p.Progression.UnspentPoints);
            Assert.AreEqual(0, p.Tree.SpentPoints);
        }

        [Test]
        public void Unlock_NeedsPointLevelAndRequirement()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.IsFalse(p.Tree.CanUnlock(SkillIds.Contra), "sem ponto");
            p.Progression.GainXp(70);                         // nível 2, 1 ponto
            Assert.IsTrue(p.Tree.Unlock(SkillIds.Contra));
            Assert.IsFalse(p.Tree.CanUnlock(SkillIds.VentoLamina), "sem ponto e nível 4");
            p.Progression.GainXp(100 + 130);                  // nível 4, 2 pontos
            Assert.IsFalse(p.Tree.CanUnlock(SkillIds.InvestidaCadeia), "requer Passo Leve e nível 5");
            Assert.IsTrue(p.Tree.Unlock(SkillIds.VentoLamina));
        }

        [Test]
        public void Respec_RefundsSpentButKeepsStartingSkill()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.Progression.GainXp(70 + 100);
            p.Tree.Unlock(SkillIds.FioAfiado);
            p.Tree.Unlock(SkillIds.Contra);
            Assert.AreEqual(0, p.Progression.UnspentPoints);
            Assert.AreEqual(2, p.Tree.Respec());
            Assert.AreEqual(2, p.Progression.UnspentPoints);
            Assert.IsTrue(p.Has(SkillIds.Investida));
            Assert.IsFalse(p.Has(SkillIds.FioAfiado));
        }

        [Test]
        public void Elementalista_StartsWithFireball()
        {
            var p = new PlayerState(ClassId.Elementalista);
            Assert.IsTrue(p.Has(SkillIds.BolaFogo));
            Assert.IsFalse(p.Has(SkillIds.Investida));
        }
    }

    public class PlayerStateTests
    {
        [Test]
        public void Cast_SpendsResourceAndStartsCooldown()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.AreEqual(SkillCastResult.Ok, p.TryCast(SkillIds.Investida));
            Assert.AreEqual(75f, p.Stamina.Current, 1e-3);
            Assert.AreEqual(SkillCastResult.Cooldown, p.TryCast(SkillIds.Investida));
            p.Tick(5.01f);
            Assert.AreEqual(SkillCastResult.Ok, p.CanCast(SkillIds.Investida));
        }

        [Test]
        public void Cast_LockedSkillFails()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.AreEqual(SkillCastResult.Locked, p.TryCast(SkillIds.Contra));
            Assert.AreEqual(SkillCastResult.Unknown, p.TryCast(SkillIds.FioAfiado));
        }

        [Test]
        public void InvestidaCadeia_HalvesCooldown()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.Progression.GainXp(10000);
            p.Tree.Unlock(SkillIds.FioAfiado); p.Tree.Unlock(SkillIds.PassoLeve); p.Tree.Unlock(SkillIds.InvestidaCadeia);
            p.TryCast(SkillIds.Investida);
            Assert.AreEqual(2.5f, p.CooldownLeft(SkillIds.Investida), 1e-3);
        }

        [Test]
        public void Contra_OpensCounterStanceForTwoSeconds()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.Progression.GainXp(70);
            p.Tree.Unlock(SkillIds.Contra);
            p.TryCast(SkillIds.Contra);
            Assert.Greater(p.CounterStance, 0f);
            p.Tick(2.1f);
            Assert.IsFalse(p.ConsumeCounterStance());
        }

        [Test]
        public void Death_RefillsAndLosesLevelXp()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.AddKill(80);                       // nível 2 + 10 XP
            int lost = -1;
            p.Died += x => lost = x;
            p.TakeDamage(9999f);
            Assert.AreEqual(10, lost);
            Assert.AreEqual(2, p.Progression.Level);
            Assert.AreEqual(p.Hp.Max, p.Hp.Current);
        }

        [Test]
        public void HurtCooldown_PreventsDoubleHit()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.IsTrue(p.TakeDamage(10f));
            Assert.IsFalse(p.TakeDamage(10f));
            p.Tick(0.5f);
            Assert.IsTrue(p.TakeDamage(10f));
        }

        [Test]
        public void Dash_CostsLessWithPassoLeve()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.TryDash();
            Assert.AreEqual(75f, p.Stamina.Current, 1e-3);
            Assert.Greater(p.Invulnerable, 0f);
            Assert.AreEqual(15f, Combat.DashCost(true));
        }

        [Test]
        public void LevelUp_RaisesMaxHp()
        {
            var p = new PlayerState(ClassId.Lamina);
            float before = p.Hp.Max;
            p.AddKill(70);
            Assert.Greater(p.Hp.Max, before);
        }
    }
}
