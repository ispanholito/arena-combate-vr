using System.Collections.Generic;

namespace Arena.Core
{
    // Reação a golpes seguidos: se o inimigo apanha muitas vezes em pouco tempo SEM estar com a postura quebrada,
    // ele reage (goblin pula para trás; chefe solta uma onda de choque). Evita travar o inimigo só spamando golpes:
    // para combar de verdade é preciso quebrar a postura primeiro.
    public sealed class ComboBreaker
    {
        public const float Window = 2.5f;
        public const float ImmuneSeconds = 0.5f;          // depois de reagir, não leva dano por um instante

        public static int HitsToBreak(EnemyKind kind) => kind switch
        {
            EnemyKind.Goblin => 5,
            EnemyKind.Esqueleto => 5,
            EnemyKind.ReiGoblin => 4,
            _ => 0,                                       // slime e boneco não reagem
        };

        // O chefe só cambaleia com a postura quebrada (antes ficava travado em mini-atordoamentos).
        public static bool CanBeStaggered(EnemyKind kind, bool postureBroken) => kind != EnemyKind.ReiGoblin || postureBroken;

        readonly int threshold;
        readonly Queue<float> hits = new Queue<float>();
        float now;

        public ComboBreaker(EnemyKind kind) { threshold = HitsToBreak(kind); }

        public void Tick(float dt)
        {
            now += dt;
            while (hits.Count > 0 && now - hits.Peek() > Window) hits.Dequeue();
        }

        // Retorna true quando este golpe dispara a reação.
        public bool RegisterHit(bool postureBroken)
        {
            if (threshold <= 0 || postureBroken) return false;
            hits.Enqueue(now);
            if (hits.Count < threshold) return false;
            hits.Clear();
            return true;
        }
    }
}
