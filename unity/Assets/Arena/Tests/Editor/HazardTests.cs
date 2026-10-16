using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class HazardTests
    {
        [Test]
        public void SpikeCycle_WarnsThenRisesOncePerCycle()
        {
            var c = new SpikeTrapCycle();
            Assert.AreEqual(SpikeTrapCycle.Phase.Idle, c.Current);
            Assert.IsFalse(c.Advance(SpikeTrapCycle.IdleSeconds + 0.1f));
            Assert.AreEqual(SpikeTrapCycle.Phase.Warning, c.Current);
            Assert.IsTrue(c.Advance(SpikeTrapCycle.WarningSeconds));
            Assert.AreEqual(SpikeTrapCycle.Phase.Up, c.Current);
            Assert.IsFalse(c.Advance(0.1f));   // continua em cima: não conta de novo
            c.Advance(SpikeTrapCycle.UpSeconds);
            Assert.AreEqual(SpikeTrapCycle.Phase.Idle, c.Current);
            Assert.IsTrue(SpikeTrapCycle.Inside(0, 0, 0.6f, -0.6f));
            Assert.IsFalse(SpikeTrapCycle.Inside(0, 0, 0.8f, 0f));
            Assert.IsTrue(SpikeTrapCycle.Inside(0, 0, 0.8f, 0f, 0.2f));
        }

        [Test]
        public void Traps_AreWalkable_Spread_AndNotOnChestSpot()
        {
            int total = 0;
            for (int seed = 1; seed < 50; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 1 + seed % 2);
                total += f.Traps.Count;
                foreach (var (x, z) in f.Traps)
                {
                    Assert.IsTrue(f.Area.Contains(x, z), $"seed {seed}: espinho fora da área");
                    foreach (var room in f.Rooms.Where(r => r.Kind != RoomKind.Combate))
                        Assert.IsFalse(room.Rect.Contains(x, z), $"seed {seed}: espinho em sala segura");
                    foreach (var room in f.Rooms.Where(r => r.HasChest))
                        Assert.IsFalse(System.Math.Abs(x - room.Rect.CenterX) < 2f && System.Math.Abs(z - room.Rect.CenterZ) < 2f);
                }
                for (int i = 0; i < f.Traps.Count; i++)
                    for (int j = i + 1; j < f.Traps.Count; j++)
                        Assert.IsFalse(System.Math.Abs(f.Traps[i].x - f.Traps[j].x) < 2f && System.Math.Abs(f.Traps[i].z - f.Traps[j].z) < 2f);
            }
            Assert.Greater(total, 50);
        }

        [Test]
        public void Mimics_AppearOnlyOnChests_MoreOnFloorTwo()
        {
            int m1 = 0, m2 = 0;
            for (int seed = 1; seed < 300; seed++)
            {
                foreach (var r in DungeonGenerator.Generate(seed, 1).Rooms) { if (r.ChestIsMimic) { m1++; Assert.IsTrue(r.HasChest); } }
                foreach (var r in DungeonGenerator.Generate(seed, 2).Rooms) { if (r.ChestIsMimic) { m2++; Assert.IsTrue(r.HasChest); } }
            }
            Assert.Greater(m1, 0);
            Assert.Greater(m2, m1);
            var drop = new LootTable(4).RollDrop(EnemyKind.Mimico);
            Assert.NotNull(drop);
            Assert.GreaterOrEqual((int)drop.Rarity, (int)Rarity.Incomum);
        }

        [Test]
        public void Archers_ShowUpInDungeon()
        {
            var rng = new System.Random(3);
            int archers = 0;
            for (int i = 0; i < 200; i++) archers += DungeonGenerator.RollEnemies(rng, 1, 2).Count(e => e.Kind == EnemyKind.Arqueiro);
            Assert.Greater(archers, 20);
        }

        [Test]
        public void ArrowLob_GrowsWithDistance_AndCaps()
        {
            Assert.AreEqual(0f, ArrowRules.LobAngle(0f, ArrowRules.Speed));
            float near = ArrowRules.LobAngle(4f, ArrowRules.Speed), far = ArrowRules.LobAngle(10f, ArrowRules.Speed);
            Assert.Greater(far, near);
            Assert.Less(far, 0.3f);
            Assert.AreEqual(System.Math.PI / 4, ArrowRules.LobAngle(1000f, ArrowRules.Speed), 1e-4);
        }
    }
}
