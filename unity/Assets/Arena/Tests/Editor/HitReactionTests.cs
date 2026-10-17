using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HitReactionTests
    {
        [Test]
        public void HittingMidAttack_StaggersMore_ThanGuard()
        {
            int lv = HitReactions.Level(3f);
            Assert.AreEqual(2, lv);
            Assert.AreEqual(HitReaction.Media, HitReactions.Resolve(EnemyKind.Goblin, lv, EnemyActivity.Guarda, false, false));
            Assert.AreEqual(HitReaction.Total, HitReactions.Resolve(EnemyKind.Goblin, lv, EnemyActivity.Atacando, false, false));
        }

        [Test]
        public void HeadOrLegs_AndRepeatedHits_Knockdown()
        {
            Assert.AreEqual(HitReaction.Derrubado, HitReactions.Resolve(EnemyKind.Goblin, 2, EnemyActivity.Atacando, true, false));
            Assert.AreEqual(HitReaction.Derrubado, HitReactions.Resolve(EnemyKind.Goblin, 2, EnemyActivity.Cambaleando, true, false));
            Assert.AreEqual(HitReaction.Nenhuma, HitReactions.Resolve(EnemyKind.Goblin, 0, EnemyActivity.Guarda, false, false));
        }

        [Test]
        public void HeavyAndBoss_ResistMore()
        {
            Assert.Less(HitReactions.Resolve(EnemyKind.Orc, 2, EnemyActivity.Guarda, false, false),
                        HitReactions.Resolve(EnemyKind.Goblin, 2, EnemyActivity.Guarda, false, false));
            Assert.AreEqual(HitReaction.Nenhuma, HitReactions.Resolve(EnemyKind.ReiGoblin, 3, EnemyActivity.Atacando, true, false));
            Assert.AreNotEqual(HitReaction.Nenhuma, HitReactions.Resolve(EnemyKind.ReiGoblin, 3, EnemyActivity.Atacando, true, true));
            Assert.Greater(HitReactions.StunSeconds(HitReaction.Derrubado), HitReactions.StunSeconds(HitReaction.Total));
        }
    }
}
