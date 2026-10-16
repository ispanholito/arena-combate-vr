using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ArcadeTests
    {
        [Test]
        public void DeeperTiers_AreHarder_AndRicher()
        {
            Assert.AreEqual(1f, ArcadeRules.HpMultiplier(1));
            Assert.AreEqual(1f, ArcadeRules.DamageMultiplier(1));
            Assert.Greater(ArcadeRules.HpMultiplier(4), ArcadeRules.HpMultiplier(2));
            Assert.Greater(ArcadeRules.DamageMultiplier(4), 1f);
            Assert.Greater(ArcadeRules.GoldMultiplier(3), 1f);
            Assert.AreEqual(0, ArcadeRules.ExtraEnemies(2));
            Assert.AreEqual(1, ArcadeRules.ExtraEnemies(3));
            Assert.AreEqual(1f, ArcadeRules.HpMultiplier(-5));   // nunca abaixo da 1
        }

        [Test]
        public void DeeperTiers_GiveRarerItems()
        {
            int Rare(int tier)
            {
                var loot = new LootTable(21);
                int n = 0;
                for (int i = 0; i < 4000; i++) if (loot.RollRarity(Rarity.Comum, tier) >= Rarity.Raro) n++;
                return n;
            }
            Assert.Greater(Rare(5), Rare(1) * 2);
            var l = new LootTable(2);
            for (int i = 0; i < 200; i++) Assert.GreaterOrEqual((int)l.RollRarity(Rarity.Incomum, 6), (int)Rarity.Incomum);
        }

        [Test]
        public void DeeperDungeon_HasMoreEnemies_AndTierIsSaved()
        {
            int Count(int tier) => Enumerable.Range(1, 30).Sum(seed => DungeonGenerator.Generate(seed, 2, tier).Rooms.Sum(r => r.Enemies.Count));
            Assert.Greater(Count(5), Count(1));
            Assert.AreEqual(4, new DungeonRun(1, 4).Tier);
            var ps = new PlayerState(ClassId.Lamina) { DungeonTier = 3, ArenaTier = 2 };
            var loaded = SaveData.From(ps).ToPlayer();
            Assert.AreEqual(3, loaded.DungeonTier);
            Assert.AreEqual(2, loaded.ArenaTier);
        }
    }
}
