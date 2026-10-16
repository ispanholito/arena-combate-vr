using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class OptionsTests
    {
        [Test]
        public void Volume_CyclesInStepsAndWraps()
        {
            var o = new GameOptions { volume = 0.8f };
            o.CycleVolume(); Assert.AreEqual(1f, o.volume, 1e-4);
            o.CycleVolume(); Assert.AreEqual(0f, o.volume, 1e-4);
            o.CycleVolume(); Assert.AreEqual(0.2f, o.volume, 1e-4);
        }

        [Test]
        public void Turn_CyclesThroughModes()
        {
            var o = new GameOptions();
            Assert.AreEqual(30f, o.SnapDegrees);
            o.CycleTurn(); Assert.AreEqual(TurnMode.Rapido45, o.Turn); Assert.AreEqual(45f, o.SnapDegrees);
            o.CycleTurn(); Assert.AreEqual(TurnMode.Suave, o.Turn);
            o.CycleTurn(); Assert.AreEqual(TurnMode.Rapido30, o.Turn);
        }

        [Test]
        public void Difficulty_ScalesDamageAndEnemyHp()
        {
            var o = new GameOptions();
            Assert.AreEqual(Difficulty.Normal, o.Difficulty);
            o.CycleDifficulty(); Assert.AreEqual(Difficulty.Dificil, o.Difficulty);
            o.CycleDifficulty(); Assert.AreEqual(Difficulty.Facil, o.Difficulty);
            Assert.Less(DifficultyRules.DamageTaken(Difficulty.Facil), DifficultyRules.DamageTaken(Difficulty.Normal));
            Assert.Greater(DifficultyRules.EnemyHp(Difficulty.Dificil), 1f);
            var g = EnemyDef.Goblin.WithHp(1.3f);
            Assert.AreEqual(91f, g.MaxHp);
            Assert.AreEqual(EnemyDef.Goblin.AttackDamage, g.AttackDamage);
            Assert.AreEqual(EnemyKind.Goblin, g.Kind);
            Assert.AreSame(EnemyDef.Goblin, EnemyDef.Goblin.WithHp(1f));
            Assert.AreEqual(Difficulty.Normal, new GameOptions { difficulty = 42 }.Sanitized().Difficulty);
        }

        [Test]
        public void Sanitize_FixesBadValues()
        {
            var o = new GameOptions { volume = 7f, turn = 99 }.Sanitized();
            Assert.AreEqual(1f, o.volume);
            Assert.AreEqual(TurnMode.Rapido30, o.Turn);
            Assert.AreEqual(0.8f, new GameOptions { volume = float.NaN }.Sanitized().volume, 1e-4);
        }
    }
}
