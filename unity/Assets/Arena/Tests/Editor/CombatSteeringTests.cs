using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class CombatSteeringTests
    {
        [Test]
        public void Slots_SpreadInFront_KeepOrder_NeverBehind()
        {
            // Três rondando todos à direita: espalham num arco centrado na frente, mantendo a ordem.
            var s = CombatSteering.AssignSlots(new[] { 30f, 50f, 40f });
            Assert.Less(s[0], s[2]); Assert.Less(s[2], s[1]);
            Assert.AreEqual(-45f, s[0], 1e-3); Assert.AreEqual(45f, s[1], 1e-3);
            // Muitos: o arco não passa de 240° (ninguém nas costas).
            var many = CombatSteering.AssignSlots(new[] { -170f, -100f, -50f, 0f, 50f, 100f, 150f, 170f });
            foreach (var a in many) Assert.LessOrEqual(System.Math.Abs(a), 120f + 1e-3f);
        }

        [Test]
        public void Separation_PushesApart_OnlyWhenClose()
        {
            var push = CombatSteering.Separation(new Vector2(0f, 0f), new List<Vector2> { new Vector2(0.4f, 0f) });
            Assert.Less(push.X, 0f);
            Assert.AreEqual(Vector2.Zero, CombatSteering.Separation(new Vector2(0f, 0f), new List<Vector2> { new Vector2(2f, 0f) }));
        }

        [Test]
        public void Avoid_SteersAroundPillar_InThePath()
        {
            // Indo para +z com um pilar logo à frente, um pouco à esquerda (x < 0): desvia para a direita (+x).
            var pillars = new List<(Vector2, float)> { (new Vector2(-0.2f, 1.2f), 0.55f) };
            var steer = CombatSteering.Avoid(Vector2.Zero, new Vector2(0f, 1.5f), pillars, 0.25f);
            Assert.Greater(steer.X, 0.1f);
            // Pilar atrás: não mexe.
            var behind = new List<(Vector2, float)> { (new Vector2(0f, -1.5f), 0.55f) };
            Assert.AreEqual(Vector2.Zero, CombatSteering.Avoid(Vector2.Zero, new Vector2(0f, 1.5f), behind, 0.25f));
        }

        [Test]
        public void ProjectileThreat_OnlyWhenComingAtMe()
        {
            // Bola de fogo vindo reto a 10 m/s, 4 m de distância: ameaça (passa a 0,3 m); foge para o lado em que já está.
            Assert.IsTrue(CombatSteering.ProjectileThreat(new Vector2(0.3f, 4f), Vector2.Zero, new Vector2(0f, 10f), out var dodge));
            Assert.Greater(dodge.X, 0f);
            // Passando longe ou indo embora: não.
            Assert.IsFalse(CombatSteering.ProjectileThreat(new Vector2(2f, 4f), Vector2.Zero, new Vector2(0f, 10f), out _));
            Assert.IsFalse(CombatSteering.ProjectileThreat(new Vector2(0f, -4f), Vector2.Zero, new Vector2(0f, 10f), out _));
            // Ainda longe demais no tempo (> 0,7 s): espera.
            Assert.IsFalse(CombatSteering.ProjectileThreat(new Vector2(0f, 12f), Vector2.Zero, new Vector2(0f, 10f), out _));
        }

        [Test]
        public void DodgeAndFeint_ScaleWithDifficulty()
        {
            Assert.Less(CombatSteering.DodgeChance(Difficulty.Facil), CombatSteering.DodgeChance(Difficulty.Dificil));
            Assert.AreEqual(0f, CombatSteering.FeintChance(Difficulty.Facil));
        }
    }
}
