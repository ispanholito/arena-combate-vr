using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HandCooldownTests
    {
        [Test]
        public void EachHand_HasItsOwnCooldown()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.AreEqual(SkillCastResult.Ok, p.TryHandPower(HandPowers.Punch, 0));
            Assert.AreEqual(SkillCastResult.Cooldown, p.TryHandPower(HandPowers.Punch, 0));   // esquerda em recarga
            Assert.AreEqual(SkillCastResult.Ok, p.TryHandPower(HandPowers.Punch, 1));         // direita livre
            Assert.AreEqual(SkillCastResult.Cooldown, p.TryHandPower(HandPowers.Punch, 1));
            Assert.Greater(p.HandCooldownLeft(HandPowers.Punch, 0), 0f);
        }
    }
}
