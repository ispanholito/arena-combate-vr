using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ReflexTests
    {
        [Test]
        public void Ramp_StartsSlowReachesLimitAndStays()
        {
            Assert.AreEqual(0.5f, ReflexRules.Ramp(0.5f, 2f, 60f, 0f), 1e-4f);
            Assert.AreEqual(1.25f, ReflexRules.Ramp(0.5f, 2f, 60f, 30f), 1e-4f);
            Assert.AreEqual(2f, ReflexRules.Ramp(0.5f, 2f, 60f, 60f), 1e-4f);
            Assert.AreEqual(2f, ReflexRules.Ramp(0.5f, 2f, 60f, 600f), 1e-4f);
            Assert.AreEqual(1.5f, ReflexRules.RampPerMinute(0.5f, 2f, 60f), 1e-4f);
        }

        [Test]
        public void Settings_RampOverridesBothStations()
        {
            var s = new ReflexSettings { projectileSpeed = 7, machineSpeed = 0 };
            Assert.AreEqual(3f, s.ProjectileNow(10f), 1e-4f);
            Assert.AreEqual(0.5f, s.MachineNow(10f), 1e-4f);
            s.ramp = true;   // começa em 0,5×, limite 2,5×, 60 s
            Assert.AreEqual(0.5f, s.ProjectileNow(0f), 1e-4f);
            Assert.AreEqual(s.ProjectileNow(30f), s.MachineNow(30f), 1e-4f);
            Assert.AreEqual(2.5f, s.MachineNow(61f), 1e-4f);
        }

        [Test]
        public void Settings_CycleWrapsAndSurvivesBadIndex()
        {
            var s = new ReflexSettings { projectileSpeed = 99 };
            Assert.AreEqual(3f, s.ProjectileSpeed, 1e-4f);
            s.CycleProjectileSpeed();
            Assert.AreEqual(0.5f, s.ProjectileSpeed, 1e-4f);
            for (int i = 0; i < ReflexRules.RampTimes.Length; i++) s.CycleRampTime();
            Assert.AreEqual(ReflexRules.RampTimes[1], s.RampSeconds, 1e-4f);
        }

        [Test]
        public void Faster_MeansShorterTimesWithFloors()
        {
            Assert.Less(ReflexRules.ShotInterval(2f), ReflexRules.ShotInterval(1f));
            Assert.Less(ReflexRules.Windup(2f), ReflexRules.Windup(1f));
            Assert.AreEqual(ReflexRules.MinSwing, ReflexRules.Swing(100f), 1e-4f);
            Assert.AreEqual(ReflexRules.MinShotInterval, ReflexRules.ShotInterval(100f), 1e-4f);
            Assert.AreEqual(MagicOrbRules.Speed * 2f, ReflexRules.OrbSpeed(2f), 1e-4f);
            float slow = ReflexRules.ArrowSpeed(0.5f);
            Assert.GreaterOrEqual(slow * slow / 9.81f, 9.5f, "flecha lenta precisa alcançar o jogador a 9 m");
        }

        [Test]
        public void Score_StreakBreaksOnHit()
        {
            var sc = new ReflexScore();
            sc.Add(ReflexOutcome.Aparou); sc.Add(ReflexOutcome.Rebateu); sc.Add(ReflexOutcome.Desviou);
            Assert.AreEqual(3, sc.Streak);
            sc.Add(ReflexOutcome.Acertou);
            Assert.AreEqual(0, sc.Streak);
            Assert.AreEqual(3, sc.BestStreak);
            Assert.AreEqual(4, sc.Total);
            sc.Reset();
            Assert.AreEqual(0, sc.Total);
        }
        [Test]
        public void Totems_CountAndShotTypesCycleSafely()
        {
            var s = new ReflexSettings { totems = 0 };
            Assert.AreEqual(1, s.TotemCount);
            s.totems = 8; s.CycleTotems();
            Assert.AreEqual(1, s.TotemCount);
            s.CycleTotems();
            Assert.AreEqual(2, s.TotemCount);
            s.shots = new[] { 0 };   // salvo antigo/curto
            Assert.AreEqual(TotemShot.Orbe, s.ShotOf(0));
            Assert.AreEqual(TotemShot.Misto, s.ShotOf(5));
            s.CycleShot(5);
            Assert.AreEqual(TotemShot.Orbe, s.ShotOf(5));
            Assert.AreEqual(TotemShot.Orbe, s.ShotOf(0));
            s.SetAllShots(TotemShot.Flecha);
            Assert.AreEqual(TotemShot.Flecha, s.ShotOf(7));
        }

        [Test]
        public void TotemAngles_SpreadSymmetricAndCapped()
        {
            Assert.AreEqual(25f, ReflexRules.TotemAngle(0, 1), 1e-4f);
            Assert.AreEqual(-15f, ReflexRules.TotemAngle(0, 2), 1e-4f);
            Assert.AreEqual(15f, ReflexRules.TotemAngle(1, 2), 1e-4f);
            Assert.AreEqual(-75f, ReflexRules.TotemAngle(0, 8), 1e-4f);
            Assert.AreEqual(75f, ReflexRules.TotemAngle(7, 8), 1e-4f);
        }
    }
}
