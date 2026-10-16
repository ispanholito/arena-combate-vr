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
        public void SideSlash_SlowPoke_FarFromTip_OrAxe_DoNotStab()
        {
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 6f, 0.2f, 0.1f), "corte lateral");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 1f, 0.95f, 0.1f), "lento demais");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Espada, 3f, 0.95f, 0.6f), "acertou com o meio da lâmina");
            Assert.IsFalse(StabRules.CanStab(WeaponKind.Machado, 3f, 0.95f, 0.1f), "machado não crava");
        }

        [Test]
        public void StabDamage_BeatsSameSpeedThrust()
        {
            float stab = StabRules.StabDamage(4f, WeaponDef.EspadaLonga, false, true);
            Assert.AreEqual(Combat.MeleeDamage(4f, WeaponDef.EspadaLonga, false, true, 1f) * StabRules.DamageMultiplier, stab, 1e-3);
        }

        [Test]
        public void ExitRipAndDrag()
        {
            Assert.IsFalse(StabRules.Exited(0.1f));
            Assert.IsTrue(StabRules.Exited(-0.05f));
            Assert.IsFalse(StabRules.Ripped(0.3f));
            Assert.IsTrue(StabRules.Ripped(0.5f));
            Assert.AreEqual(0f, StabRules.DragSpeed(0.1f));
            Assert.Greater(StabRules.DragSpeed(0.3f), 0f);
            Assert.AreEqual(StabRules.MaxDragSpeed, StabRules.DragSpeed(5f), 1e-3);
        }
    }
}

namespace Arena.Core.Tests
{
    public class StabDepthTests
    {
        [Test]
        public void Sword_CanGoInAlmostToTheGuard()
        {
            Assert.Greater(StabRules.MaxDepth(WeaponDef.EspadaLonga), 0.8f, "dá para atravessar um goblin (0,64 m de diâmetro)");
        }
    }
}
