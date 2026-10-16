using System.Linq;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class DungeonTests
    {
        [Test]
        public void Floors_HaveExpectedRoomSequence()
        {
            for (int seed = 1; seed < 40; seed++)
            {
                var f1 = DungeonGenerator.Generate(seed, 1);
                Assert.AreEqual(RoomKind.Inicio, f1.Start.Kind);
                Assert.AreEqual(RoomKind.Escada, f1.Last.Kind);
                Assert.AreEqual(4, f1.Rooms.Count(r => r.Kind == RoomKind.Combate));
                var f2 = DungeonGenerator.Generate(seed, 2);
                Assert.AreEqual(RoomKind.Chefe, f2.Last.Kind);
                Assert.AreEqual(RoomKind.Fogueira, f2.Rooms[f2.Rooms.Count - 2].Kind);
                Assert.AreEqual(EnemyKind.ReiGoblin, f2.Last.Enemies.Single().Kind);
                Assert.IsFalse(f2.Rooms.Any(r => r.Kind == RoomKind.Combate && r.Enemies.Any(e => e.Kind == EnemyKind.ReiGoblin)));
            }
        }

        [Test]
        public void Rooms_DoNotOverlap_AndAreConnected()
        {
            for (int seed = 1; seed < 60; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 1 + seed % 2);
                for (int i = 0; i < f.Rooms.Count; i++)
                    for (int j = i + 1; j < f.Rooms.Count; j++)
                    {
                        var a = f.Rooms[i].Rect; var b = f.Rooms[j].Rect;
                        bool overlap = a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
                        Assert.IsFalse(overlap, $"seed {seed}: salas {i} e {j} se sobrepõem");
                    }
                // Andando em linha reta de uma sala à seguinte nunca sai da área.
                for (int i = 1; i < f.Rooms.Count; i++)
                {
                    var a = f.Rooms[i - 1].Rect; var b = f.Rooms[i].Rect;
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        float x = a.CenterX + (b.CenterX - a.CenterX) * t, z = a.CenterZ + (b.CenterZ - a.CenterZ) * t;
                        Assert.IsTrue(f.Area.Contains(x, z), $"seed {seed}: caminho {i - 1}->{i} fora da área");
                    }
                }
            }
        }

        [Test]
        public void Openings_SitOnRoomEdge_TowardCorridor_AndEveryFloorHasAChest()
        {
            for (int seed = 1; seed < 40; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 1 + seed % 2);
                Assert.IsTrue(f.Rooms.Any(r => r.HasChest));
                Assert.IsFalse(f.Rooms.Any(r => r.HasChest && r.Kind != RoomKind.Combate));
                for (int i = 0; i < f.Rooms.Count; i++)
                {
                    var r = f.Rooms[i].Rect;
                    var ops = f.Openings(i);
                    Assert.AreEqual(i == 0 || i == f.Rooms.Count - 1 ? 1 : 2, ops.Count);
                    foreach (var (x0, z0, x1, z1) in ops)
                    {
                        float mx = (x0 + x1) / 2f, mz = (z0 + z1) / 2f;
                        bool onEdge = mx == r.MinX || mx == r.MaxX || mz == r.MinZ || mz == r.MaxZ;
                        Assert.IsTrue(onEdge, $"seed {seed} sala {i}: abertura fora da borda");
                        Assert.AreEqual(DungeonGenerator.CorridorWidth, System.Math.Abs(x1 - x0) + System.Math.Abs(z1 - z0), 1e-3);
                        // Um passo para fora da sala pela abertura continua andável (é o corredor).
                        float ox = mx == r.MinX ? -0.5f : mx == r.MaxX ? 0.5f : 0f, oz = mz == r.MinZ ? -0.5f : mz == r.MaxZ ? 0.5f : 0f;
                        Assert.IsTrue(f.Area.Contains(mx + ox, mz + oz));
                    }
                }
            }
        }

        [Test]
        public void Chest_GivesGold_AndSometimesAWeapon()
        {
            var loot = new LootTable(9);
            int items = 0;
            for (int i = 0; i < 200; i++)
            {
                var (gold, item) = loot.RollChest(2);
                Assert.That(gold, Is.InRange(28, 42));
                if (item != null) { items++; Assert.GreaterOrEqual((int)item.Rarity, (int)Rarity.Incomum); }
            }
            Assert.That(items, Is.InRange(90, 150));
        }

        [Test]
        public void Map_FitsFloor_AndRevealsVisitedNeighbours()
        {
            for (int seed = 1; seed < 30; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 1);
                var p = new MapProjection(f);
                foreach (var r in f.Area.Rects)
                    foreach (var (x, z) in new[] { (r.MinX, r.MinZ), (r.MaxX, r.MaxZ) })
                    {
                        var (u, v) = p.ToMap(x, z);
                        Assert.That(u, Is.InRange(-0.5f, 0.5f));
                        Assert.That(v, Is.InRange(-0.5f, 0.5f));
                    }
            }
            var run = new DungeonRun(4);
            var fl = run.Floor;
            Assert.IsFalse(MapProjection.Revealed(fl, 0));
            run.OnPlayerAt(fl.Start.Rect.CenterX, fl.Start.Rect.CenterZ);
            Assert.IsTrue(fl.Start.Visited);
            Assert.IsTrue(MapProjection.Revealed(fl, 1));    // vizinha da visitada aparece (apagada)
            Assert.IsFalse(MapProjection.Revealed(fl, 2));   // o resto fica escondido
        }

        [Test]
        public void SameSeed_SameLayout()
        {
            var a = DungeonGenerator.Generate(5, 1);
            var b = DungeonGenerator.Generate(5, 1);
            Assert.AreEqual(a.Rooms.Select(r => (r.Rect.CenterX, r.Rect.CenterZ, r.Enemies.Count)),
                            b.Rooms.Select(r => (r.Rect.CenterX, r.Rect.CenterZ, r.Enemies.Count)));
        }

        [Test]
        public void WalkArea_ClampsAndReaches()
        {
            var area = new WalkArea(new[] { new Rect2(0, 0, 10, 10), new Rect2(10, 4, 20, 6) });
            Assert.AreEqual((5f, 5f), area.Clamp(5, 5));
            Assert.AreEqual((10f, 9.5f), area.Clamp(12, 9.5f));   // fora: vai para a borda mais próxima
            Assert.AreEqual((15f, 6f), area.Clamp(15, 8));     // no corredor
            Assert.AreEqual(5f, area.RayReach(5, 5, 0, 1, 20), 0.21f);
            Assert.AreEqual(15f, area.RayReach(5, 5, 1, 0, 30), 0.21f);
        }

        [Test]
        public void WallSegments_OutlineRoomWithOpening()
        {
            var area = new WalkArea(new[] { new Rect2(0, 0, 4, 4), new Rect2(4, 1, 8, 3) });
            var walls = area.WallSegments();
            float total = walls.Sum(w => System.Math.Abs(w.x1 - w.x0) + System.Math.Abs(w.z1 - w.z0));
            // Sala 4x4 (perímetro 16) menos a abertura de 2 m + corredor 4x2 (perímetro 12) menos a junção de 2 m.
            Assert.AreEqual(16f - 2f + 12f - 2f, total, 1e-3);
            Assert.IsFalse(walls.Any(w => w.x0 == 4 && w.x1 == 4 && w.z0 >= 1 && w.z1 <= 3), "parede na abertura do corredor");
        }

        [Test]
        public void Run_RoomTriggers_Clears_AndDeathResets()
        {
            var run = new DungeonRun(3);
            var start = run.Floor.Start.Rect;
            Assert.IsNull(run.OnPlayerAt(start.CenterX, start.CenterZ));
            var combat = run.Floor.Rooms[1];
            var spawned = run.OnPlayerAt(combat.Rect.CenterX, combat.Rect.CenterZ);
            Assert.AreSame(combat, spawned);
            Assert.IsNull(run.OnPlayerAt(combat.Rect.CenterX, combat.Rect.CenterZ));   // não dispara de novo
            run.OnPlayerDied();
            Assert.AreSame(combat, run.OnPlayerAt(combat.Rect.CenterX, combat.Rect.CenterZ));
            run.OnRoomEmpty();
            Assert.IsTrue(combat.Cleared);
            Assert.IsNull(run.ActiveRoom);
            Assert.IsTrue(run.CanDescend);
            run.GoToFloor(2);
            Assert.IsFalse(run.CanDescend);
            Assert.AreEqual(RoomKind.Fogueira, run.CheckpointRoom.Kind);
            var fire = run.Floor.Rooms[run.Floor.Rooms.Count - 2];
            run.OnPlayerAt(fire.Rect.CenterX, fire.Rect.CenterZ);
            Assert.AreSame(fire, run.CheckpointRoom);
            var boss = run.Floor.Last;
            Assert.AreSame(boss, run.OnPlayerAt(boss.Rect.CenterX, boss.Rect.CenterZ));
            run.OnBossKilled();
            Assert.IsTrue(run.Victory);
        }

        [Test]
        public void DeeperRooms_HaveMoreEnemies_AndFloorTwoHasSkeletons()
        {
            var rng = new System.Random(1);
            int early = 0, late = 0, skeletons = 0;
            for (int i = 0; i < 200; i++)
            {
                early += DungeonGenerator.RollEnemies(rng, 1, 1).Count;
                var l = DungeonGenerator.RollEnemies(rng, 2, 4);
                late += l.Count;
                skeletons += l.Count(e => e.Kind == EnemyKind.Esqueleto);
            }
            Assert.Greater(late, early);
            Assert.Greater(skeletons, 0);
            Assert.IsFalse(DungeonGenerator.RollEnemies(new System.Random(2), 1, 3).Any(e => e.Kind == EnemyKind.Esqueleto));
        }
    }
}
