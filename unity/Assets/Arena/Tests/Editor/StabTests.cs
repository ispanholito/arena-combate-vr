using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class StabTests
    {
        [Test]
        public void Thrust_WithSwordTip_Stabs()
        {
            Assert.IsTrue(StabRules.CanStab(WeaponKind.Espada, 3f, 0.95f, 0.1f));
        }

        [Test]
        public void VrThrust_WithBladeTiltedFromController_StillStabs()
        {
            // Lâmina 40° inclinada em relação ao movimento: cos(40°) ≈ 0,77 (antes exigia 0,8 e nunca cravava no Quest).
            Assert.IsTrue(StabRules.CanStab(WeaponKind.Espada, 2.5f, 0.77f, 0.1f));
        }

        [Test]
        public void SideSlash_SlowPoke_FarFromTip_OrAxe_DoNotStab()
        {
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 6f, 0.2f, 0.1f), "corte lateral");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 1f, 0.95f, 0.1f), "lento demais");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 3f, 0.95f, 0.6f), "acertou com o meio da lâmina");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Machado, 3f, 0.95f, 0.1f), "machado não crava");
        }

        [Test]
        public void SmallEnemies_AreSkewered_BigOnesEmbed()
        {
            Assert.AreEqual(StabStyle.Skewer, StabRules.StyleFor(EnemyKind.Slime));
            Assert.AreEqual(StabStyle.Skewer, StabRules.StyleFor(EnemyKind.Goblin));
            Assert.AreEqual(StabStyle.Embed, StabRules.StyleFor(EnemyKind.ReiGoblin));
            Assert.AreEqual(StabStyle.Embed, StabRules.StyleFor(EnemyKind.Boneco));
        }

        [Test]
        public void Slime_FliesFartherThanGoblin_BossIsHardestToPull()
        {
            Assert.Greater(StabRules.FlingMultiplier(EnemyKind.Slime), StabRules.FlingMultiplier(EnemyKind.Goblin));
            Assert.Greater(StabRules.PullSeconds(EnemyKind.ReiGoblin), StabRules.PullSeconds(EnemyKind.Boneco));
        }

        [Test]
        public void Skewer_BladeComesOutTheOtherSide()
        {
            // Centro do goblin a (raio + 8 cm) da ponta: a ponta passa 8 cm do outro lado.
            float r = EnemyDef.Goblin.Radius;
            Assert.AreEqual(r + 0.08f, StabRules.SkewerCenterFromTip(WeaponDef.EspadaLonga, r), 1e-4);
        }

        [Test]
        public void ImpactDamage_GrowsWithSpeed_SlowLandingIsFree()
        {
            Assert.AreEqual(0f, StabRules.ImpactDamage(1f));
            Assert.Greater(StabRules.ImpactDamage(10f), StabRules.ImpactDamage(5f));
            Assert.GreaterOrEqual(StabRules.ImpactDamage(8f), EnemyDef.Slime().MaxHp, "slime arremessado forte explode ao bater");
        }

        [Test]
        public void StabDamage_IsBonusOverThrust()
        {
            Assert.AreEqual(Combat.MeleeDamage(4f, WeaponDef.EspadaLonga, false, true, 1f) * StabRules.DamageMultiplier,
                            StabRules.StabDamage(4f, WeaponDef.EspadaLonga, false, true), 1e-3);
        }

        [Test]
        public void Sword_CanGoInAlmostToTheGuard()
        {
            Assert.Greater(StabRules.MaxDepth(WeaponDef.EspadaLonga), 0.8f);
        }
    }
}
