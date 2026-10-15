using System;

namespace Arena.Core
{
    // Nível, XP e pontos de habilidade. Morte: perde só o XP do nível atual, nunca cai de nível.
    public sealed class Progression
    {
        public const int GameMaxLevel = 30;
        public const int MvpMaxLevel = 10;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int UnspentPoints { get; private set; }
        public int MaxLevel { get; }

        public event Action<int> LeveledUp;

        public Progression(int maxLevel = MvpMaxLevel)
        {
            MaxLevel = Math.Max(1, Math.Min(GameMaxLevel, maxLevel));
        }

        public static int XpToNext(int level) => 40 + level * 30;
        public int XpNeeded => Level >= MaxLevel ? 0 : XpToNext(Level);
        public bool IsMaxLevel => Level >= MaxLevel;

        // Retorna quantos níveis subiu.
        public int GainXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;
            Xp += amount;
            int gained = 0;
            while (!IsMaxLevel && Xp >= XpToNext(Level))
            {
                Xp -= XpToNext(Level);
                Level++;
                UnspentPoints++;
                gained++;
                LeveledUp?.Invoke(Level);
            }
            if (IsMaxLevel) Xp = 0;
            return gained;
        }

        // Retorna o XP perdido.
        public int OnDeath()
        {
            int lost = Xp;
            Xp = 0;
            return lost;
        }

        public bool TrySpendPoint()
        {
            if (UnspentPoints <= 0) return false;
            UnspentPoints--;
            return true;
        }

        public void RefundPoints(int count) => UnspentPoints += Math.Max(0, count);
    }
}
