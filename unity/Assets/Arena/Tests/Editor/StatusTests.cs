using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class StatusTests
    {
        static void Run(EnemyState s, float seconds) { for (float t = 0; t < seconds; t += 0.05f) s.Tick(0.05f); }

        [Test]
        public void Heat_BuildsUp_ThenBurns_ThenDecays()
        {
            var s = new EnemyState(EnemyDef.Goblin);
            s.Status.AddHeat(StatusRules.FireballHeat);
            s.Status.AddHeat(StatusRules.FireballHeat);
            Assert.AreEqual(0f, s.Status.Burning);                     // 2 bolas ainda não acendem
            s.Status.AddHeat(StatusRules.FireballHeat);
            Assert.Greater(s.Status.Burning, 0f);                      // a 3ª acende
            float hp = s.Hp;
            Run(s, StatusRules.BurnSeconds + 0.5f);
            Assert.AreEqual(StatusRules.BurnDps * StatusRules.BurnSeconds, hp - s.Hp, 3.1f);
            Assert.AreEqual(0f, s.Status.Burning);

            var c = new StatusEffects(EnemyMaterial.Carne);
            c.AddHeat(50f); c.Tick(2f);
            Assert.Less(c.Heat, 50f);                                  // esfria sozinho
        }

        [Test]
        public void Ice_PutsOutFire_AndBurnDoesNotBreakPosture()
        {
            var s = new EnemyState(EnemyDef.Goblin);
            s.Status.Ignite();
            Run(s, 1f);
            Assert.AreEqual(0f, s.Posture.Value, 1e-3);
            s.ApplyFreeze(1f);
            Assert.AreEqual(0f, s.Status.Burning);
        }

        [Test]
        public void Electrocute_StopsActing_LongerOnMetal_AndHasCooldown()
        {
            var g = new EnemyState(EnemyDef.Goblin);
            for (int i = 0; i < 5; i++) g.Status.AddCharge(StatusRules.BeamCharge);
            Assert.IsFalse(g.CanAct);
            Assert.Less(g.SpeedMultiplier, 0.3f);
            var orc = new EnemyState(EnemyDef.Orc);
            for (int i = 0; i < 5; i++) orc.Status.AddCharge(StatusRules.BeamCharge);
            Assert.Greater(orc.Status.Electrocuted, g.Status.Electrocuted);
            Run(g, StatusRules.ElectrocuteSeconds + 0.1f);
            Assert.IsTrue(g.CanAct);
            for (int i = 0; i < 5; i++) g.Status.AddCharge(StatusRules.BeamCharge);
            Assert.AreEqual(0f, g.Status.Electrocuted);                // recarga: não trava para sempre
        }

        [Test]
        public void Boss_ResistsStatuses()
        {
            var k = new EnemyState(EnemyDef.ReiGoblin());
            for (int i = 0; i < 5; i++) k.Status.AddCharge(StatusRules.BeamCharge);
            Assert.AreEqual(0f, k.Status.Electrocuted);
        }
    }
}
