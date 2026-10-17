using System;

namespace Arena.Core
{
    // Status acumulados (ideia dos "status" do Blade & Sorcery): cada efeito enche uma barra e só dispara quando ela
    // passa do limite; a barra esvazia sozinha. Base para as magias futuras.
    //  - Calor → QUEIMANDO: dano por segundo por alguns segundos. Gelo apaga.
    //  - Carga elétrica → ELETROCUTADO: quase parado e sem atacar. Dura mais em quem usa armadura de metal (Placa).
    //  - Lento: já existia (gelo), continua no EnemyState.
    public static class StatusRules
    {
        public const float Threshold = 100f;
        public const float HeatDecay = 12f;          // por segundo, quando não está queimando
        public const float BurnSeconds = 4f;
        public const float BurnDps = 6f;
        public const float BurnTick = 0.5f;          // o dano sai em pedaços a cada 0,5 s
        public const float ChargeDecay = 30f;
        public const float ElectrocuteSeconds = 1.2f;
        public const float ElectrocuteSpeed = 0.15f; // velocidade de quem está eletrocutado
        public const float ElectrocuteCooldown = 2.5f;   // depois de passar, não volta logo (evita travar o inimigo para sempre)

        // Quanto calor/carga cada fonte põe.
        public const float FireballHeat = 45f;       // ~3 bolas de fogo para acender; enraizado acende na hora
        public const float BeamCharge = 20f;         // 5 raios seguidos eletrocutam (o raio atira 2x mais rápido desde 2026-10-17)

        public static float ElectrocuteDuration(EnemyMaterial m) => m switch
        {
            EnemyMaterial.Placa => ElectrocuteSeconds * 2f,   // metal conduz
            EnemyMaterial.Gosma => ElectrocuteSeconds * 1.3f,
            EnemyMaterial.Madeira => ElectrocuteSeconds * 0.5f,
            _ => ElectrocuteSeconds,
        };

        // Quem pega fogo mais fácil (madeira, gosma) ou mais difícil (osso, placa).
        public static float HeatMultiplier(EnemyMaterial m) => m switch
        {
            EnemyMaterial.Madeira => 1.6f,
            EnemyMaterial.Gosma => 1.3f,
            EnemyMaterial.Osso => 0.7f,
            EnemyMaterial.Placa => 0.6f,
            _ => 1f,
        };
    }

    public sealed class StatusEffects
    {
        readonly EnemyMaterial material;
        readonly bool resistant;   // chefe: metade do acúmulo
        float burnTickTimer;

        public float Heat { get; private set; }
        public float Burning { get; private set; }
        public float Charge { get; private set; }
        public float Electrocuted { get; private set; }
        float electroCd;

        public event Action Ignited, Shocked;

        public StatusEffects(EnemyMaterial material, bool resistant = false)
        {
            this.material = material;
            this.resistant = resistant;
        }

        float Scale(float v) => resistant ? v * 0.5f : v;

        public void AddHeat(float amount)
        {
            if (Burning > 0f) { Burning = Math.Max(Burning, StatusRules.BurnSeconds); return; }   // renova
            Heat += Scale(amount * StatusRules.HeatMultiplier(material));
            if (Heat >= StatusRules.Threshold) Ignite();
        }

        public void Ignite()
        {
            bool fresh = Burning <= 0f;
            Heat = 0f;
            Burning = StatusRules.BurnSeconds;
            if (fresh) { burnTickTimer = StatusRules.BurnTick; Ignited?.Invoke(); }
        }

        // Gelo apaga o fogo e esfria.
        public void Extinguish() { Burning = 0f; Heat = 0f; }

        public void AddCharge(float amount)
        {
            if (Electrocuted > 0f || electroCd > 0f) return;
            Charge += Scale(amount);
            if (Charge >= StatusRules.Threshold)
            {
                Charge = 0f;
                Electrocuted = StatusRules.ElectrocuteDuration(material);
                electroCd = Electrocuted + StatusRules.ElectrocuteCooldown;
                Shocked?.Invoke();
            }
        }

        // Avança o tempo; devolve o dano de queimadura deste passo (0 na maioria dos quadros).
        public float Tick(float dt)
        {
            float dmg = 0f;
            if (Burning > 0f)
            {
                Burning = Math.Max(0f, Burning - dt);
                burnTickTimer -= dt;
                while (burnTickTimer <= 0f)
                {
                    dmg += StatusRules.BurnDps * StatusRules.BurnTick;
                    burnTickTimer += StatusRules.BurnTick;
                }
            }
            else Heat = Math.Max(0f, Heat - StatusRules.HeatDecay * dt);
            Electrocuted = Math.Max(0f, Electrocuted - dt);
            electroCd = Math.Max(0f, electroCd - dt);
            if (Electrocuted <= 0f) Charge = Math.Max(0f, Charge - StatusRules.ChargeDecay * dt);
            return dmg;
        }
    }
}
