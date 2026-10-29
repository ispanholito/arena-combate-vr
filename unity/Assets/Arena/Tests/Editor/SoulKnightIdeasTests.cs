using System.Linq;
using Arena.Core;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    // Leva inspirada no Soul Knight: elites, destrutíveis, armas novas e salas especiais.
    public class SoulKnightIdeasTests
    {
        [Test]
        public void ArmoredEliteTakesLessDamage()
        {
            var a = new EnemyState(EnemyDef.Goblin);
            var b = new EnemyState(EnemyDef.Goblin) { Elite = EliteKind.Blindado };
            Assert.Less(b.TakeDamage(20f, 0f, false).Amount, a.TakeDamage(20f, 0f, false).Amount);
        }

        [Test]
        public void RegeneratorHealsAfterPause()
        {
            var e = new EnemyState(EnemyDef.Goblin) { Elite = EliteKind.Regenerador };
            e.TakeDamage(30f, 0f, false);
            float hp = e.Hp;
            e.Tick(1f);
            Assert.AreEqual(hp, e.Hp, 0.01f);   // ainda dentro dos 2 s
            e.Tick(1.5f);
            Assert.Greater(e.Hp, hp);
        }

        [Test]
        public void FuriousIsFasterAndBossNeverElite()
        {
            var e = new EnemyState(EnemyDef.Goblin) { Elite = EliteKind.Furioso };
            Assert.Greater(e.SpeedMultiplier, 1f);
            var rng = new System.Random(1);
            for (int i = 0; i < 100; i++) Assert.AreEqual(EliteKind.Nenhum, EliteRules.Roll(EnemyKind.ReiGoblin, 1f, rng));
            for (int i = 0; i < 100; i++) Assert.AreNotEqual(EliteKind.Blindado, EliteRules.Roll(EnemyKind.Slime, 1f, rng));
        }

        [Test]
        public void ExplosiveBarrelDropsNothing()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 50; i++) Assert.AreEqual((0, false), BreakableRules.RollLoot(BreakableKind.BarrilExplosivo, rng));
        }

        [Test]
        public void NewWeaponsHaveUniqueIdsAndKeepLookWhenResolved()
        {
            Assert.AreEqual(WeaponDef.All.Length, WeaponDef.All.Select(w => w.Id).Distinct().Count());
            var item = new ItemInstance(WeaponDef.Tocha, Rarity.Raro);
            var def = item.Resolve(ClassId.Lamina);
            Assert.AreEqual(WeaponLook.Tocha, def.Look);
            Assert.Greater(def.HeatPerHit, 0f);
            Assert.AreEqual(1.0f, WeaponPhysics.Mass(def), 0.01f);
        }

        [Test]
        public void SecretRoomIsHiddenUntilOpened()
        {
            int found = 0;
            for (int seed = 1; seed < 40; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 1);
                if (f.Secret == null) continue;
                found++;
                float cx = f.Secret.Rect.CenterX, cz = f.Secret.Rect.CenterZ;
                Assert.IsFalse(f.Area.Contains(cx, cz), "seed " + seed);
                Assert.IsNull(f.RoomAt(cx, cz));
                f.OpenSecret();
                Assert.IsTrue(f.Area.Contains(cx, cz), "seed " + seed);
                // Não sobrepõe nenhuma sala do caminho.
                foreach (var r in f.Rooms) Assert.IsFalse(r.Rect.Contains(cx, cz, 5f));
            }
            Assert.Greater(found, 10);
        }

        [Test]
        public void ChallengeNeverOnFirstCombatRoom()
        {
            for (int seed = 1; seed < 40; seed++)
            {
                var f = DungeonGenerator.Generate(seed, 2);
                var combats = f.Rooms.Where(r => r.Kind == RoomKind.Combate).ToList();
                Assert.IsFalse(combats[0].IsChallenge);
                Assert.LessOrEqual(combats.Count(r => r.IsChallenge), 1);
            }
        }
    }
}
