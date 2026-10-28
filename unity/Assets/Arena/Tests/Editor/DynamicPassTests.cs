using NUnit.Framework;

namespace Arena.Core.Tests
{
    // Passada de dinamismo (2026-10-28, público adolescente): mais espaço, inimigos, recursos e recargas curtas.
    public class DynamicPassTests
    {
        [Test]
        public void MoreEnemies_MoreResources_ShorterCooldowns()
        {
            var w = new WaveDirector(1);
            Assert.GreaterOrEqual(w.TargetCount(1), 4);
            Assert.AreEqual(9, w.TargetCount(30));
            Assert.Greater(ClassProfile.BaseStamina, 100f);
            Assert.Greater(ClassProfile.BaseMana, 100f);
            Assert.LessOrEqual(new PlayerState(ClassId.Lamina).Tree.Get(SkillIds.Investida).Cooldown, 3f);
            Assert.LessOrEqual(WeaponAbilities.For(WeaponKind.Maca).Cooldown, 7f);
            Assert.Greater(Combat.ArenaHalfSize, 12f);
            Assert.GreaterOrEqual(DungeonGenerator.Cell, 24f);
            var rng = new System.Random(3);
            for (int i = 0; i < 20; i++)
            {
                int n = DungeonGenerator.RollEnemies(rng, 1, 1).Count;
                Assert.GreaterOrEqual(n, 4); Assert.LessOrEqual(n, 9);
            }
        }
    }
}
