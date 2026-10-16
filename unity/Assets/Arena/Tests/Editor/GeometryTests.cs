using NUnit.Framework;
using System.Numerics;

namespace Arena.Core.Tests
{
    public class GeometryTests
    {
        [Test]
        public void CrossingSegments_HaveZeroDistance()
        {
            float d = Geometry.SegmentSegmentDistance(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 1, 0));
            Assert.AreEqual(0f, d, 1e-4);
        }

        [Test]
        public void ParallelSegments_Distance()
        {
            float d = Geometry.SegmentSegmentDistance(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 2, 0), new Vector3(1, 2, 0));
            Assert.AreEqual(2f, d, 1e-4);
        }

        [Test]
        public void PointSegment_ClampsToEnds()
        {
            Assert.AreEqual(1f, Geometry.PointSegmentDistance(Vector3.Zero, Vector3.UnitX, new Vector3(2, 0, 0)), 1e-4);
        }

        [Test]
        public void FastSwing_ThroughBody_IsCaughtBySweep()
        {
            // Lâmina vertical passa de x=-2 para x=+2; corpo em x=0. Sem sub-passos os dois frames erram.
            var bodyA = new Vector3(0, 0, 1); var bodyB = new Vector3(0, 2, 1);
            bool hit = Geometry.SweptBladeHits(new Vector3(-2, 0, 1), new Vector3(-2, 1, 1), new Vector3(2, 0, 1), new Vector3(2, 1, 1), bodyA, bodyB, 0.4f);
            Assert.IsTrue(hit);
            Assert.Greater(Geometry.SegmentSegmentDistance(new Vector3(2, 0, 1), new Vector3(2, 1, 1), bodyA, bodyB), 0.4f);
        }

        [Test]
        public void EdgeAlignment_SlashThrustAndSlide()
        {
            var blade = Vector3.UnitZ;
            Assert.AreEqual(1f, Geometry.EdgeAlignment(blade, new Vector3(5, 0, 0)), 1e-3, "corte");
            Assert.AreEqual(1f, Geometry.EdgeAlignment(blade, new Vector3(0, 0, 5)), 1e-3, "estocada");
            Assert.AreEqual(0f, Geometry.EdgeAlignment(blade, new Vector3(0, 0, -5)), 1e-3, "puxando para trás");
        }
    }
}

namespace Arena.Core.Tests
{
    public class WeakPointTests
    {
        [Test]
        public void ClosestParam_GivesHitHeight()
        {
            // Lâmina horizontal na altura 1,0 cruzando um corpo de 0 a 1,25 → 80% da altura.
            float t = Geometry.ClosestParamOnSecond(new System.Numerics.Vector3(-1, 1f, 0), new System.Numerics.Vector3(1, 1f, 0),
                                                    new System.Numerics.Vector3(0, 0, 0), new System.Numerics.Vector3(0, 1.25f, 0));
            Assert.AreEqual(0.8f, t, 1e-3);
        }

        [Test]
        public void Head_DoesMoreDamage_SlimeHasNoHead()
        {
            Assert.AreEqual(WeakPoints.HeadMultiplier, WeakPoints.Multiplier(EnemyKind.Goblin, 0.85f));
            Assert.AreEqual(1f, WeakPoints.Multiplier(EnemyKind.Goblin, 0.4f));
            Assert.AreEqual(1f, WeakPoints.Multiplier(EnemyKind.Slime, 1f));
        }
    }
}
