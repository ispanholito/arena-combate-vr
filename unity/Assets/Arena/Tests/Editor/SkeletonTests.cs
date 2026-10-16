using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class SkeletonTests
    {
        [Test]
        public void HitZones_MapToLimbs()
        {
            Assert.AreEqual(Limb.Cabeca, SkeletonBody.LimbAt(0.9f, 0f));
            Assert.AreEqual(Limb.BracoDireito, SkeletonBody.LimbAt(0.6f, 0.2f));
            Assert.AreEqual(Limb.BracoEsquerdo, SkeletonBody.LimbAt(0.6f, -0.2f));
            Assert.AreEqual(Limb.PernaEsquerda, SkeletonBody.LimbAt(0.2f, -0.1f));
        }

        [Test]
        public void OnlyFastEdgeCutsSever_StaffNever()
        {
            Assert.IsTrue(SkeletonBody.CanSever(WeaponKind.Espada, 6f, 0.9f));
            Assert.IsFalse(SkeletonBody.CanSever(WeaponKind.Espada, 3f, 0.9f), "lento");
            Assert.IsFalse(SkeletonBody.CanSever(WeaponKind.Espada, 6f, 0.2f), "de lado");
            Assert.IsFalse(SkeletonBody.CanSever(WeaponKind.Cajado, 9f, 1f), "cajado não corta");
        }

        [Test]
        public void SeveredLimb_ReassemblesUnlessBroken()
        {
            var b = new SkeletonBody();
            Assert.IsTrue(b.Sever(Limb.BracoDireito));
            Assert.IsFalse(b.CanAttack);
            Assert.IsTrue(b.Sever(Limb.PernaDireita));
            Assert.IsTrue(b.Break(Limb.PernaDireita));
            var back = b.Tick(SkeletonBody.ReassembleSeconds + 0.1f);
            Assert.Contains(Limb.BracoDireito, back);
            Assert.IsTrue(b.CanAttack, "braço voltou");
            Assert.AreEqual(LimbState.Destruido, b[Limb.PernaDireita], "perna quebrada não volta");
            Assert.Less(b.SpeedMultiplier, 1f);
        }

        [Test]
        public void NoLegs_Crawls_NoHead_SlowToTurn()
        {
            var b = new SkeletonBody();
            b.Sever(Limb.PernaDireita); b.Sever(Limb.PernaEsquerda); b.Sever(Limb.Cabeca);
            Assert.AreEqual(0.25f, b.SpeedMultiplier, 1e-4);
            Assert.Less(b.TurnMultiplier, 1f);
            Assert.Greater(b.WindupMultiplier, 1f);
        }

        [Test]
        public void SkeletonsAppearFromLevel2()
        {
            var d = new WaveDirector(seed: 7);
            bool sawSkeleton = false;
            for (int i = 0; i < 200; i++)
            {
                var def = d.Tick(5f, 0, 0, 5, 0);
                if (def != null && def.Kind == EnemyKind.Esqueleto) sawSkeleton = true;
            }
            Assert.IsTrue(sawSkeleton);
            var d1 = new WaveDirector(seed: 7);
            for (int i = 0; i < 200; i++)
            {
                var def = d1.Tick(5f, 0, 0, 1, 0);
                Assert.IsTrue(def == null || def.Kind != EnemyKind.Esqueleto);
            }
        }
    }
}
