using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class CombatTests
    {
        [Test]
        public void SlowSwing_DoesNoDamage()
        {
            Assert.AreEqual(0f, Combat.MeleeDamage(1f, WeaponDef.EspadaLonga, false, true));
        }

        [Test]
        public void FasterSwing_HitsHarder_UpToCap()
        {
            float slow = Combat.MeleeDamage(3f, WeaponDef.EspadaLonga, false, true);
            float fast = Combat.MeleeDamage(8f, WeaponDef.EspadaLonga, false, true);
            float capped = Combat.MeleeDamage(20f, WeaponDef.EspadaLonga, false, true);
            Assert.Greater(fast, slow);
            Assert.AreEqual(Combat.MeleeDamage(9f, WeaponDef.EspadaLonga, false, true), capped, 1e-3);
            Assert.AreEqual(30.5f, capped, 1e-3);
        }

        [Test]
        public void Axe_HitsHarderThanSword()
        {
            Assert.Greater(Combat.MeleeDamage(6f, WeaponDef.MachadoPesado, false, true),
                           Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, false, true));
        }

        [Test]
        public void SharpEdgeAndTiredness_Multiply()
        {
            float b = Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, false, true);
            Assert.AreEqual(b * 1.25f, Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, true, true), 1e-3);
            Assert.AreEqual(b * 0.4f, Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, false, false), 1e-3);
        }

        [Test]
        public void FlatHit_DoesLessThanEdgeHit()
        {
            Assert.Less(Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, false, true, 0f),
                        Combat.MeleeDamage(6f, WeaponDef.EspadaLonga, false, true, 1f));
        }

        [Test]
        public void HeavyWeapon_LagsWithLowStrength()
        {
            float str = ClassProfile.Elementalista.Strength(1);
            Assert.Less(WeaponDef.MachadoPesado.FollowRate(str), WeaponDef.EspadaLonga.FollowRate(str));
            Assert.Less(WeaponDef.MachadoPesado.FollowRate(str), WeaponDef.MachadoPesado.FollowRate(ClassProfile.Lamina.Strength(10)));
        }

        [Test]
        public void Fireball_FalloffAndBurn()
        {
            Assert.AreEqual(1f, Combat.ExplosionFalloff(0f, 2f), 1e-3);
            Assert.AreEqual(0.6f, Combat.ExplosionFalloff(2f, 2f), 1e-3);
            Assert.Greater(Combat.FireballRadius(1f, true), Combat.FireballRadius(1f, false));
        }
    }

    public class GuardTests
    {
        [Test]
        public void FastWeapon_Parries()
        {
            Assert.AreEqual(GuardResult.Parry, Guard.Resolve(3f, false, 0f));
        }

        [Test]
        public void StillWeapon_BlocksWithStamina_ElseBreaks()
        {
            Assert.AreEqual(GuardResult.Block, Guard.Resolve(0.5f, false, 50f));
            Assert.AreEqual(GuardResult.GuardBroken, Guard.Resolve(0.5f, false, 10f));
            Assert.AreEqual(4f, Guard.DamageToPlayer(GuardResult.Block, 15f));
            Assert.AreEqual(15f, Guard.DamageToPlayer(GuardResult.GuardBroken, 15f));
            Assert.AreEqual(0f, Guard.DamageToPlayer(GuardResult.Parry, 15f));
        }

        [Test]
        public void CounterStance_TurnsAnyContactIntoCounter()
        {
            Assert.AreEqual(GuardResult.Counter, Guard.Resolve(0f, true, 0f));
        }
    }

    public class PostureTests
    {
        [Test]
        public void Fills_Breaks_ThenResets()
        {
            var p = new Posture(100f);
            int broke = 0; p.Broke += () => broke++;
            Assert.IsFalse(p.Add(60f));
            Assert.IsTrue(p.Add(50f));
            Assert.AreEqual(1, broke);
            Assert.IsTrue(p.IsBroken);
            Assert.IsFalse(p.Add(10f), "não acumula quebrada");
            p.Tick(Posture.BrokenSeconds + 0.01f);
            Assert.IsFalse(p.IsBroken);
            Assert.AreEqual(0f, p.Value);
        }

        [Test]
        public void Regenerates_AfterDelay()
        {
            var p = new Posture(100f);
            p.Add(50f);
            p.Tick(1f);
            Assert.AreEqual(50f, p.Value, 1e-3);
            p.Tick(1f);
            p.Tick(1f);
            Assert.AreEqual(35f, p.Value, 1e-3);
        }

        [Test]
        public void Slime_HasNoPosture()
        {
            var p = new Posture(EnemyDef.Slime().MaxPosture);
            Assert.IsFalse(p.Enabled);
            Assert.IsFalse(p.Add(5000f));
        }
    }

    public class EnemyStateTests
    {
        [Test]
        public void BrokenPosture_GivesCritAndStun()
        {
            var e = new EnemyState(EnemyDef.Goblin);
            e.AddPosture(100f);
            Assert.Greater(e.Stun, 0f);
            var ev = e.TakeDamage(10f);
            Assert.IsTrue(ev.Crit);
            Assert.AreEqual(25f, ev.Amount);
        }

        [Test]
        public void Burn_OnlyWhenRooted()
        {
            var e = new EnemyState(EnemyDef.Goblin);
            Assert.IsFalse(e.TakeDamage(10f, burn: true).Burn);
            e.ApplyRoot(3f);
            var ev = e.TakeDamage(10f, burn: true);
            Assert.IsTrue(ev.Burn);
            Assert.AreEqual(16f, ev.Amount);
        }

        [Test]
        public void Dies_AndRaisesEvent()
        {
            var e = new EnemyState(EnemyDef.Slime());
            bool died = false; e.Died += () => died = true;
            var ev = e.TakeDamage(1000f);
            Assert.IsTrue(ev.Killed && died && e.Dead);
            Assert.AreEqual(0, e.TakeDamage(5f).Amount);
        }

        [Test]
        public void Slime_SplitsUntilSmall()
        {
            Assert.IsTrue(EnemyDef.SlimeSplits(1f));
            Assert.IsTrue(EnemyDef.SlimeSplits(0.62f));
            Assert.IsFalse(EnemyDef.SlimeSplits(0.62f * 0.62f));
        }

        [Test]
        public void Boss_GetsTougherEachVictory()
        {
            Assert.Greater(EnemyDef.ReiGoblin(1).MaxHp, EnemyDef.ReiGoblin(0).MaxHp);
        }
    }

    public class WaveDirectorTests
    {
        [Test]
        public void SpawnsUpToTarget_ThenBossAfterKills()
        {
            var d = new WaveDirector(seed: 42);
            Assert.IsNotNull(d.Tick(2f, 0, 0, 1, 0));
            Assert.IsNull(d.Tick(5f, d.TargetCount(1), 0, 1, 0));
            var boss = d.Tick(5f, 0, 0, 1, WaveDirector.KillsForBoss);
            Assert.AreEqual(EnemyKind.ReiGoblin, boss.Kind);
            Assert.AreEqual(0, d.TargetCount(1), "fase 1 do chefe: sozinho");
            d.BossPhase = 2;
            Assert.AreEqual(0, d.TargetCount(1), "fase 2 também sozinho (estilo Souls)");
            d.OnBossDefeated();
            Assert.AreEqual(1, d.BossesDefeated);
        }
    }
}

namespace Arena.Core.Tests
{
    public class WeaponPhysicsTests
    {
        [Test]
        public void StrongerWielder_HasStifferSpring()
        {
            var axe = WeaponDef.MachadoPesado;
            float weak = WeaponPhysics.Spring(WeaponPhysics.Mass(axe), WeaponPhysics.LinearOmega(axe, ClassProfile.Elementalista.Strength(1)));
            float strong = WeaponPhysics.Spring(WeaponPhysics.Mass(axe), WeaponPhysics.LinearOmega(axe, ClassProfile.Lamina.Strength(10)));
            Assert.Greater(strong, weak);
        }

        [Test]
        public void HeavyAxe_IsSlowerThanSword_ForSameWielder()
        {
            float str = ClassProfile.Lamina.Strength(1);
            Assert.Less(WeaponPhysics.LinearOmega(WeaponDef.MachadoPesado, str), WeaponPhysics.LinearOmega(WeaponDef.EspadaLonga, str));
        }

        [Test]
        public void Rotation_IsSofterThanPosition()
        {
            float str = ClassProfile.Lamina.Strength(5);
            Assert.Less(WeaponPhysics.AngularOmega(WeaponDef.EspadaLonga, str), WeaponPhysics.LinearOmega(WeaponDef.EspadaLonga, str));
        }

        [Test]
        public void Damper_GivesNearCriticalDamping()
        {
            float m = 2f, w = 10f;
            float k = WeaponPhysics.Spring(m, w), c = WeaponPhysics.Damper(m, w);
            Assert.AreEqual(WeaponPhysics.DampingRatio, c / (2f * (float)System.Math.Sqrt(k * m)), 1e-3);
        }

        [Test]
        public void Haptic_ScalesWithImpulse_AndClamps()
        {
            Assert.AreEqual(0f, WeaponPhysics.HapticFromImpulse(0f, WeaponDef.EspadaLonga));
            Assert.AreEqual(1f, WeaponPhysics.HapticFromImpulse(1000f, WeaponDef.EspadaLonga));
        }
    }
}

namespace Arena.Core.Tests
{
    public class WeaponHoldingTests
    {
        [Test]
        public void HeavyAxe_WeakWielder_DoesNotDroopMoreThanLimit()
        {
            var axe = WeaponDef.MachadoPesado;
            float m = WeaponPhysics.Mass(axe), d = 0.75f, inertia = 0.3f + m * d * d;
            float k = WeaponPhysics.AngularSpringHolding(inertia, WeaponPhysics.AngularOmega(axe, ClassProfile.Lamina.Strength(1)), m, d);
            float droop = WeaponPhysics.AngularDriveCalibration * m * WeaponPhysics.Gravity * d / k;   // queda parada (rad), com a calibração medida
            Assert.LessOrEqual(droop, WeaponPhysics.MaxRestDroopRad + 1e-4);
        }

        [Test]
        public void HoldingSpring_NeverWeakerThanDynamicSpring()
        {
            Assert.GreaterOrEqual(WeaponPhysics.LinearSpringHolding(1.4f, 45f), WeaponPhysics.Spring(1.4f, 45f));
        }
    }
}

namespace Arena.Core.Tests
{
    public class WeaponStrikeTests
    {
        [Test]
        public void Axe_StrikesWithTheEdgeOffTheHandle()
        {
            var s = WeaponStrike.For(WeaponDef.MachadoPesado);
            Assert.IsTrue(s.HasSingleEdge);
            Assert.Less(s.A.Y, -0.2f, "o gume fica ao lado do cabo");
        }

        [Test]
        public void EdgeFirst_BeatsFlatSide()
        {
            var edge = new System.Numerics.Vector3(0, -1, 0);
            Assert.AreEqual(1f, WeaponStrike.SingleEdgeAlignment(edge, new System.Numerics.Vector3(0, -5, 0)), 1e-3);
            Assert.AreEqual(0f, WeaponStrike.SingleEdgeAlignment(edge, new System.Numerics.Vector3(5, 0, 0)), 1e-3);
            Assert.Greater(Combat.MeleeDamage(6f, WeaponDef.MachadoPesado, false, true, 1f), Combat.MeleeDamage(6f, WeaponDef.MachadoPesado, false, true, 0f));
        }

        [Test]
        public void Sword_StrikesAlongTheWholeBlade()
        {
            var s = WeaponStrike.For(WeaponDef.EspadaLonga);
            Assert.IsFalse(s.HasSingleEdge);
            Assert.AreEqual(WeaponDef.EspadaLonga.Length, s.B.Z, 1e-4);
        }
    }
}

namespace Arena.Core.Tests
{
    public class GroundAimTests
    {
        [Test]
        public void AimingDown_HitsGroundAtRightDistance()
        {
            // Da altura 1,5 m, 45° para baixo → 1,5 m à frente.
            Assert.AreEqual(1.5f, Combat.GroundAimReach(1.5f, -0.7071f, 0.7071f, 12f), 1e-3);
        }

        [Test]
        public void AimingFarOrUp_StaysAtMaxRange()
        {
            Assert.AreEqual(12f, Combat.GroundAimReach(1.5f, -0.01f, 0.99f, 12f), 1e-3);
            Assert.AreEqual(12f, Combat.GroundAimReach(1.5f, 0.3f, 0.95f, 12f), 1e-3);
        }
    }
}
