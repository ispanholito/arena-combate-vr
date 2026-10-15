using System;

namespace Arena.Core
{
    // Vida, estamina ou mana: valor atual, máximo e regeneração com atraso depois de gastar.
    public sealed class ResourcePool
    {
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float RegenPerSecond { get; set; }
        public float RegenDelay { get; private set; }

        public float Fraction => Max <= 0f ? 0f : Current / Max;
        public bool IsEmpty => Current <= 0f;

        public ResourcePool(float max, float regenPerSecond = 0f)
        {
            Max = max;
            Current = max;
            RegenPerSecond = regenPerSecond;
        }

        public bool Has(float amount) => Current >= amount;

        // Gasta se houver o suficiente. Retorna false sem gastar caso contrário.
        public bool TrySpend(float amount, float regenDelay = 0f)
        {
            if (Current < amount) return false;
            Current -= amount;
            DelayRegen(regenDelay);
            return true;
        }

        // Tira até zerar (dano). Retorna quanto foi tirado de fato.
        public float Drain(float amount, float regenDelay = 0f)
        {
            float taken = Math.Min(Current, Math.Max(0f, amount));
            Current -= taken;
            DelayRegen(regenDelay);
            return taken;
        }

        public void Restore(float amount) => Current = Math.Min(Max, Current + Math.Max(0f, amount));
        public void Fill() => Current = Max;

        public void SetMax(float max, bool fill)
        {
            Max = max;
            Current = fill ? max : Math.Min(Current, max);
        }

        public void DelayRegen(float seconds) => RegenDelay = Math.Max(RegenDelay, seconds);

        public void Tick(float dt)
        {
            if (RegenDelay > 0f) { RegenDelay = Math.Max(0f, RegenDelay - dt); return; }
            if (RegenPerSecond > 0f) Restore(RegenPerSecond * dt);
        }
    }
}
