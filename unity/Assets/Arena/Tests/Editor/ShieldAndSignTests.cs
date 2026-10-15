using NUnit.Framework;
using System.Numerics;

namespace Arena.Core.Tests
{
    public class ShieldTests
    {
        [Test]
        public void StillShield_BlocksWithoutDamage()
        {
            Assert.AreEqual(GuardResult.ShieldBlock, Guard.ResolveShield(0.3f, false, 50f));
            Assert.AreEqual(0f, Guard.DamageToPlayer(GuardResult.ShieldBlock, 15f));
        }

        [Test]
        public void PushingShield_Parries()
        {
            Assert.AreEqual(GuardResult.Parry, Guard.ResolveShield(2.5f, false, 0f));
        }

        [Test]
        public void ShieldWithoutStamina_Breaks()
        {
            Assert.AreEqual(GuardResult.GuardBroken, Guard.ResolveShield(0.3f, false, 5f));
        }

        [Test]
        public void CounterStance_WorksWithShield()
        {
            Assert.AreEqual(GuardResult.Counter, Guard.ResolveShield(0f, true, 0f));
            Assert.Greater(Combat.CounterDamage, 2f * 40f, "contra agora é dano alto direto");
        }
    }

    public class HandSignTests
    {
        [Test]
        public void Classify_AllShapes()
        {
            Assert.AreEqual(HandShape.Aberta, HandSigns.Classify(true, true, true, true));
            Assert.AreEqual(HandShape.Punho, HandSigns.Classify(false, false, false, false));
            Assert.AreEqual(HandShape.Apontar, HandSigns.Classify(true, false, false, false));
            Assert.AreEqual(HandShape.V, HandSigns.Classify(true, true, false, false));
            Assert.AreEqual(HandShape.TresDedos, HandSigns.Classify(true, true, true, false));
            Assert.AreEqual(HandShape.Chifres, HandSigns.Classify(true, false, false, true));
            Assert.AreEqual(HandShape.Outra, HandSigns.Classify(false, true, false, false));
        }

        [Test]
        public void Sign_MustBeHeld_ToArm()
        {
            var a = new HandSignArming();
            for (int i = 0; i < 3; i++) a.Update(HandShape.V, 0.1f);   // 0,2 s segurado
            Assert.AreEqual(HandPower.Nenhum, a.Armed, "segurou pouco");
            a.Update(HandShape.V, 0.1f);                                 // 0,3 s
            Assert.AreEqual(HandPower.Metralhadora, a.Armed);
        }

        [Test]
        public void Armed_Expires()
        {
            var a = new HandSignArming();
            for (int i = 0; i < 4; i++) a.Update(HandShape.Chifres, 0.1f);
            Assert.AreEqual(HandPower.SocoNoChao, a.Armed);
            for (int i = 0; i < 30; i++) a.Update(HandShape.Outra, 0.1f);
            Assert.AreEqual(HandPower.Nenhum, a.Armed);
        }

        [Test]
        public void WithoutSign_GestureDoesNothing()
        {
            var a = new HandSignArming();
            a.Update(HandShape.Apontar, 1f);
            Assert.IsFalse(a.IsTriggering(HandPower.Metralhadora, HandShape.Apontar));
        }

        [Test]
        public void SignThenGesture_Triggers()
        {
            var a = new HandSignArming();
            for (int i = 0; i < 4; i++) a.Update(HandShape.TresDedos, 0.1f);
            a.Update(HandShape.Aberta, 0.05f);
            Assert.IsTrue(a.IsTriggering(HandPower.CorteDaPalma, HandShape.Aberta));
            a.Consume();
            Assert.IsFalse(a.IsTriggering(HandPower.CorteDaPalma, HandShape.Aberta));
        }
    }

    public class SweptTests
    {
        [Test]
        public void FastClub_CrossingStillSword_IsDetected()
        {
            // Espada parada na horizontal em z=0; a clava passa de z=+0,4 para z=-0,4 num único frame.
            var s1 = new Vector3(-0.5f, 1.2f, 0f); var s2 = new Vector3(0.5f, 1.2f, 0f);
            var cPrevA = new Vector3(0, 1.0f, 0.4f); var cPrevB = new Vector3(0, 1.6f, 0.4f);
            var cNowA = new Vector3(0, 1.0f, -0.4f); var cNowB = new Vector3(0, 1.6f, -0.4f);
            Assert.Greater(Geometry.SegmentSegmentDistance(s1, s2, cNowA, cNowB), 0.3f, "checagem pontual erra");
            Assert.IsTrue(Geometry.SweptSegmentsHit(s1, s2, s1, s2, cPrevA, cPrevB, cNowA, cNowB, 0.3f));
        }
    }

    public class ManaBalanceTests
    {
        [Test]
        public void Gun_IsLessEfficientThanBase()
        {
            Assert.Less(HandPowers.GunDamage / HandPowers.GunManaPerShot, 1.5f);
        }

        [Test]
        public void Fireball_FullCharge_IsBaseRate()
        {
            Assert.AreEqual(1.5f, Combat.FireballDamage(1f) / 20f, 1e-3);
        }
    }
}
