using System.Collections.Generic;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class EnemyAttackTests
    {
        static HashSet<EnemyAttack> Seen(EnemyKind k, Difficulty d)
        {
            var s = new HashSet<EnemyAttack>();
            for (int i = 0; i < 100; i++) s.Add(EnemyAttacks.Pick(k, d, i / 100.0));
            return s;
        }

        [Test]
        public void Repertoire_DependsOnEnemyAndDifficulty()
        {
            var sk = Seen(EnemyKind.Esqueleto, Difficulty.Normal);
            Assert.AreEqual(5, sk.Count);                                   // esqueleto: todos
            Assert.IsFalse(Seen(EnemyKind.Goblin, Difficulty.Normal).Contains(EnemyAttack.Estocada));   // clava não estoca
            var orc = Seen(EnemyKind.Orc, Difficulty.Dificil);
            Assert.IsFalse(orc.Contains(EnemyAttack.Passo));
            Assert.IsTrue(orc.Contains(EnemyAttack.DireitaEsquerda));
            var easy = Seen(EnemyKind.Esqueleto, Difficulty.Facil);
            Assert.IsFalse(easy.Contains(EnemyAttack.Estocada));
            Assert.IsFalse(easy.Contains(EnemyAttack.Passo));
            Assert.AreEqual(3, easy.Count);
        }

        [Test]
        public void Poses_ComeFromDifferentDirections()
        {
            var (rw, re) = EnemyAttacks.Poses(EnemyAttack.DireitaEsquerda);
            var (lw, le) = EnemyAttacks.Poses(EnemyAttack.EsquerdaDireita);
            Assert.Greater(rw.Yaw, 0f); Assert.Less(re.Yaw, 0f);    // da direita para a esquerda
            Assert.Less(lw.Yaw, 0f); Assert.Greater(le.Yaw, 0f);    // da esquerda para a direita
            var (tw, te) = EnemyAttacks.Poses(EnemyAttack.Estocada);
            Assert.Greater(te.Forward, tw.Forward);                 // estocada avança
            Assert.Greater(EnemyAttacks.Range(EnemyAttack.Passo), EnemyAttacks.Range(EnemyAttack.Cima));
        }
    }
}
