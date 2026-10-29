using System.Linq;
using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    // 3ª leva Soul Knight: chefes sorteados, estátuas, biomas e desafios.
    public class SoulKnight3Tests
    {
        [Test]
        public void FourBossesAreBossesAndGetStronger()
        {
            foreach (var k in BossPool.All)
            {
                Assert.IsTrue(EnemyDef.IsBoss(k));
                Assert.AreEqual(k, EnemyDef.Boss(k).Kind);
                Assert.Greater(EnemyDef.Boss(k, 2).MaxHp, EnemyDef.Boss(k).MaxHp);
                Assert.AreEqual(60, LootTable.BaseGold(k));
            }
            Assert.IsFalse(EnemyDef.IsBoss(EnemyKind.Orc));
        }

        [Test]
        public void DungeonBossIsDrawnFromThePool()
        {
            var seen = new System.Collections.Generic.HashSet<EnemyKind>();
            for (int seed = 1; seed < 60; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 2);
                var boss = f.Rooms.Single(r => r.Kind == RoomKind.Chefe).Enemies.Single();
                Assert.IsTrue(EnemyDef.IsBoss(boss.Kind));
                seen.Add(boss.Kind);
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void ArenaBossRotatesAndHordeHasNone()
        {
            var w = new WaveDirector(5);
            var b = w.Tick(10f, 0, 0, 3, WaveDirector.KillsForBoss);
            Assert.IsTrue(EnemyDef.IsBoss(b.Kind));
            var h = new WaveDirector(5) { NoBoss = true };
            for (int i = 0; i < 50; i++) { var e = h.Tick(10f, 0, 0, 3, 999); if (e != null) Assert.IsFalse(EnemyDef.IsBoss(e.Kind)); }
        }

        [Test]
        public void EachFloorHasADifferentBiome()
        {
            for (int seed = 1; seed < 30; seed++)
            {
                var a = BiomeRules.For(seed, 1); var b = BiomeRules.For(seed, 2);
                Assert.AreNotEqual(a, b);
                Assert.AreNotEqual(Biome.Nenhum, a);
                var f = DungeonGenerator.Generate(seed, 1);
                Assert.AreEqual(a, f.Biome);
                if (f.Biome == Biome.Fornalha) Assert.Greater(f.Lava.Count, 0); else Assert.AreEqual(0, f.Lava.Count);
                Assert.AreEqual(1, f.Rooms.Count(r => r.HasStatue));
            }
        }

        [Test]
        public void ChallengesRules()
        {
            Assert.AreEqual(20261029, ChallengeRules.DailySeed(new System.DateTime(2026, 10, 29)));
            Assert.AreEqual(ChallengeRules.DailyBlessing(20261029), ChallengeRules.DailyBlessing(20261029));
            Assert.AreEqual(1, ChallengeRules.HordeWave(0));
            Assert.AreEqual(2, ChallengeRules.HordeWave(ChallengeRules.KillsPerWave));
            Assert.Greater(ChallengeRules.HordeHpMultiplier(5), ChallengeRules.HordeHpMultiplier(1));
            var order = ChallengeRules.BossRushOrder(new System.Random(3));
            CollectionAssert.AreEquivalent(BossPool.All, order);
            Assert.IsTrue(ChallengeRules.BetterTime(100f, 0f));
            Assert.IsFalse(ChallengeRules.BetterTime(100f, 90f));
            Assert.AreEqual("02:05", ChallengeRules.Time(125f));
        }
    }
}
