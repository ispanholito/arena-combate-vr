using System;

namespace Arena.Core
{
    // Barra de postura dos inimigos: encheu = quebra (atordoado + janela de crítico).
    // O jogador NÃO tem postura.
    public sealed class Posture
    {
        public const float RegenDelaySeconds = 2f;
        public const float RegenPerSecond = 15f;
        public const float BrokenSeconds = 2.5f;

        public float Value { get; private set; }
        public float Max { get; }
        public float BrokenTime { get; private set; }
        float regenDelay;

        public bool Enabled => Max < 900f;   // slimes não têm postura
        public bool IsBroken => BrokenTime > 0f;
        public float Fraction => Max <= 0f ? 0f : Math.Min(1f, Value / Max);

        public event Action Broke;

        public Posture(float max) { Max = max; }

        // Retorna true se quebrou agora.
        public bool Add(float amount, float regenDelay = RegenDelaySeconds)
        {
            if (!Enabled || IsBroken || amount <= 0f) return false;
            Value += amount;
            this.regenDelay = Math.Max(this.regenDelay, regenDelay);
            if (Value >= Max) { Break(); return true; }
            return false;
        }

        public void Break()
        {
            if (!Enabled) return;
            Value = Max;
            BrokenTime = BrokenSeconds;
            Broke?.Invoke();
        }

        public void Tick(float dt)
        {
            if (BrokenTime > 0f)
            {
                BrokenTime -= dt;
                if (BrokenTime <= 0f) { BrokenTime = 0f; Value = 0f; }
                return;
            }
            if (regenDelay > 0f) { regenDelay -= dt; return; }
            if (Value > 0f) Value = Math.Max(0f, Value - RegenPerSecond * dt);
        }
    }
}
