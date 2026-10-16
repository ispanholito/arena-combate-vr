using System.Collections.Generic;

namespace Arena.Core
{
    // Soma o dano recebido numa janela de tempo (boneco de treino): DPS e total do último combo.
    public sealed class DpsMeter
    {
        public float Window { get; }
        public float Total { get; private set; }      // dano desde o primeiro golpe da sequência atual
        public float LastHit { get; private set; }
        public float IdleTime { get; private set; }   // segundos desde o último dano

        readonly Queue<(float time, float amount)> hits = new Queue<(float, float)>();
        float now, windowSum;

        public DpsMeter(float window = 5f) { Window = window; IdleTime = float.MaxValue; }

        public void Add(float amount)
        {
            if (IdleTime > Window) Total = 0f;      // nova sequência
            hits.Enqueue((now, amount));
            windowSum += amount;
            Total += amount;
            LastHit = amount;
            IdleTime = 0f;
        }

        public void Tick(float dt)
        {
            now += dt;
            if (IdleTime < float.MaxValue) IdleTime += dt;
            while (hits.Count > 0 && now - hits.Peek().time > Window) windowSum -= hits.Dequeue().amount;
            if (hits.Count == 0) windowSum = 0f;
        }

        // DPS na janela. Se a sequência é mais curta que a janela, divide pelo tempo real dela (mínimo 1 s).
        public float Dps
        {
            get
            {
                if (hits.Count == 0) return 0f;
                float span = now - hits.Peek().time;
                return windowSum / System.Math.Max(1f, System.Math.Min(Window, span));
            }
        }

        public void Reset() { hits.Clear(); windowSum = 0f; Total = 0f; LastHit = 0f; IdleTime = float.MaxValue; }
    }
}
