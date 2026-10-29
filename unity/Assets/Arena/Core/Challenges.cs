using System;
using System.Collections.Generic;

namespace Arena.Core
{
    // Modos de desafio (inspiração Soul Knight), escolhidos no quadro de desafios do santuário:
    //   Desafio diário: a mesma dungeon para todo mundo no dia (semente = data), começa com uma bênção sorteada
    //                   pela data; recorde de tempo até matar o chefe.
    //   Horda infinita: arena sem chefe, a cada 8 abates sobe uma onda (mais vida, mais inimigos, mais elites);
    //                   morreu = acabou; recorde de onda.
    //   Boss rush:      os 4 chefes em sequência (ordem sorteada), descanso curto com cura e altar entre eles;
    //                   recorde de tempo; vencer dá ouro e uma arma rara.
    public enum Challenge { Nenhum, Diario, Horda, BossRush }

    public static class ChallengeRules
    {
        public static int DailySeed(DateTime day) => day.Year * 10000 + day.Month * 100 + day.Day;

        // Bênção do dia (não repetível, igual para todos).
        public static BlessingId DailyBlessing(int seed)
        {
            var list = new List<BlessingId>();
            foreach (var b in RunBlessings.All) if (!b.Repeatable) list.Add(b.Id);
            return list[new Random(seed).Next(list.Count)];
        }

        // Horda
        public const int KillsPerWave = 8;
        public static int HordeWave(int kills) => 1 + Math.Max(0, kills) / KillsPerWave;
        public static float HordeHpMultiplier(int wave) => 1f + 0.12f * (wave - 1);
        public static int HordeExtraEnemies(int wave) => Math.Min(4, (wave - 1) / 2);
        public static float HordeEliteChance(int wave) => Math.Min(0.35f, 0.05f + 0.03f * wave);

        // Boss rush
        public const float BossRushRest = 7f, BossRushHeal = 0.5f, BossRushFirstDelay = 3f;
        public const int BossRushGold = 250;
        public static List<EnemyKind> BossRushOrder(Random rng)
        {
            var l = new List<EnemyKind>(BossPool.All);
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
            return l;
        }

        public static string Name(Challenge c) => c switch
        {
            Challenge.Diario => "Desafio diário",
            Challenge.Horda => "Horda infinita",
            Challenge.BossRush => "Boss rush",
            _ => "",
        };

        public static string Time(float seconds)
        {
            int s = (int)Math.Max(0, Math.Round(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // Recorde de tempo: menor é melhor (0 = sem recorde).
        public static bool BetterTime(float now, float best) => best <= 0f || now < best;
    }
}
