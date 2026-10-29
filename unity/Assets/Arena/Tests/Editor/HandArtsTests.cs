using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HandArtsTests
    {
        [Test]
        public void Muralha_FistIntoPalm_EitherHand_NotClap()
        {
            Assert.IsTrue(MuralhaRules.IsFistPalm(true, false, false, true, 0.08f, 1.5f));    // punho esquerdo na palma direita
            Assert.IsTrue(MuralhaRules.IsFistPalm(false, true, true, false, 0.08f, 1.5f));    // ou o contrário
            Assert.IsFalse(MuralhaRules.IsFistPalm(false, true, false, true, 0.08f, 1.5f));   // duas abertas = palma batida
            Assert.IsFalse(MuralhaRules.IsFistPalm(true, false, false, true, 0.08f, 0.2f));   // encostou devagar
        }

        [Test]
        public void Muralha_GuardAndParry_ByHandDistanceAndSpeed()
        {
            Assert.AreEqual(MuralhaRules.Guard.None, MuralhaRules.ResolveGuard(0.8f, 3f));
            Assert.AreEqual(MuralhaRules.Guard.Block, MuralhaRules.ResolveGuard(0.2f, 0.5f));
            Assert.AreEqual(MuralhaRules.Guard.Parry, MuralhaRules.ResolveGuard(0.2f, 2.5f));
            Assert.IsTrue(MuralhaRules.IsBig(EnemyKind.Orc));
            Assert.IsFalse(MuralhaRules.IsBig(EnemyKind.Goblin));
        }

        [Test]
        public void Muralha_ClapCone()
        {
            var fwd = new Vector2(0f, 1f);
            Assert.IsTrue(MuralhaRules.InCone(new Vector2(0.5f, 3f), fwd, MuralhaRules.ClapConeDeg, MuralhaRules.ClapRange));
            Assert.IsFalse(MuralhaRules.InCone(new Vector2(3f, 0f), fwd, MuralhaRules.ClapConeDeg, MuralhaRules.ClapRange));   // do lado
            Assert.IsFalse(MuralhaRules.InCone(new Vector2(0f, 9f), fwd, MuralhaRules.ClapConeDeg, MuralhaRules.ClapRange));   // longe
        }

        [Test]
        public void SkyBolt_ChainsToNearest_WithinRadius_UpToMax()
        {
            var p = new List<Vector3> { new Vector3(0, 0, 10), new Vector3(2, 0, 10), new Vector3(4, 0, 10), new Vector3(20, 0, 10), new Vector3(1, 0, 13) };
            var order = SkyBoltRules.ChainTargets(p, 0);
            Assert.AreEqual(0, order[0]);
            Assert.AreEqual(1, order[1]);              // o mais perto do primeiro
            CollectionAssert.DoesNotContain(order, 3); // longe demais (> 4 m de todos)
            Assert.LessOrEqual(order.Count, SkyBoltRules.MaxJumps + 1);
            Assert.Greater(SkyBoltRules.HopDamage(0), SkyBoltRules.HopDamage(2));
        }
    }
}
