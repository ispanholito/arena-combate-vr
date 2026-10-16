using System;

namespace Arena.Core
{
    public enum EnemyKind { Slime, Goblin, ReiGoblin, Boneco, Esqueleto, Mimico, Arqueiro }

    public sealed class EnemyDef
    {
        public EnemyKind Kind { get; }
        public string Name { get; }
        public float MaxHp { get; }
        public float MaxPosture { get; }   // >= 900 = sem postura
        public int Xp { get; }
        public float AttackDamage { get; }
        public float Windup { get; }       // tempo do ataque anunciado
        public float Radius { get; }
        public float Height { get; }
        public float Mass { get; }
        public float MoveSpeed { get; }
        public float Size { get; }

        public EnemyDef(EnemyKind kind, string name, float hp, float posture, int xp, float dmg, float windup,
                        float radius, float height, float mass, float speed, float size = 1f)
        {
            Kind = kind; Name = name; MaxHp = hp; MaxPosture = posture; Xp = xp; AttackDamage = dmg; Windup = windup;
            Radius = radius; Height = height; Mass = mass; MoveSpeed = speed; Size = size;
        }

        // Slime: se divide ao morrer (filhos com 62% do tamanho) enquanto for maior que 0,45.
        public static EnemyDef Slime(float size = 1f) => new EnemyDef(EnemyKind.Slime, "Slime",
            (float)Math.Round(35f * size), 999f, (int)Math.Round(12f * size), (float)Math.Round(6f * size + 2f), 0f,
            0.35f * size, 0.56f * size, 0.6f * size + 0.3f, 2.4f, size);

        public const float SlimeSplitMinSize = 0.45f, SlimeChildScale = 0.62f;
        public static bool SlimeSplits(float size) => size > SlimeSplitMinSize;

        public static readonly EnemyDef Goblin = new EnemyDef(EnemyKind.Goblin, "Goblin", 70f, 100f, 35, 15f, 0.75f, 0.32f, 1.15f, 1.2f, 1.6f);

        // Esqueleto: espada, mais rápido e frágil que o goblin; desmembra e se remonta (SkeletonBody).
        public static readonly EnemyDef Esqueleto = new EnemyDef(EnemyKind.Esqueleto, "Esqueleto", 55f, 80f, 30, 12f, 0.6f, 0.28f, 1.35f, 0.9f, 1.9f);

        // Mímico: baú que morde. Ataque anunciado (a tampa abre e brilha) e bote curto; muita vida e postura.
        public static readonly EnemyDef Mimico = new EnemyDef(EnemyKind.Mimico, "Mímico", 110f, 120f, 50, 18f, 0.55f, 0.42f, 0.8f, 2.2f, 2.2f);

        // Goblin arqueiro: mantém distância e atira flechas (bloqueáveis com o escudo e rebatíveis com a arma).
        public static readonly EnemyDef Arqueiro = new EnemyDef(EnemyKind.Arqueiro, "Goblin Arqueiro", 45f, 70f, 30, 10f, 0.9f, 0.3f, 1.1f, 1.0f, 1.8f);

        // Boneco de treino (modo Treino): não ataca nem anda, vida enorme que volta cheia, postura de goblin forte.
        public static readonly EnemyDef Boneco = new EnemyDef(EnemyKind.Boneco, "Boneco de treino", 100000f, 150f, 0, 0f, 0f, 0.3f, 1.6f, 1000f, 0f);

        // Rei Goblin: fica mais forte a cada vitória.
        public static EnemyDef ReiGoblin(int timesDefeated = 0) => new EnemyDef(EnemyKind.ReiGoblin, "Rei Goblin",
            420f + 200f * timesDefeated, 220f, 220, 22f, 0.7f, 0.56f, 2.0f, 3.7f, 1.8f, 1.75f);

        public const float BossPhase2Fraction = 0.5f, BossPhase2Windup = 0.5f;
        public const float BossSlamDamage = 28f, BossSlamRadius = 2.2f, BossSlamWindup = 1.1f, BossSlamCooldown = 6f;
        public const int BossSummonCount = 2;
    }

    // Estado de vida + postura de um inimigo, independente da Unity.
    public sealed class EnemyState
    {
        public EnemyDef Def { get; }
        public float Hp { get; private set; }
        public Posture Posture { get; }
        public bool Dead => Hp <= 0f;
        public float Stun { get; private set; }
        public float Slow { get; private set; }
        public float Frozen { get; private set; }
        public float Rooted { get; private set; }

        public event Action<DamageEvent> Damaged;
        public event Action Died;

        public EnemyState(EnemyDef def)
        {
            Def = def;
            Hp = def.MaxHp;
            Posture = new Posture(def.MaxPosture);
            Posture.Broke += () => Stun = Math.Max(Stun, Posture.BrokenSeconds);
        }

        public float HpFraction => Def.MaxHp <= 0 ? 0 : Math.Max(0f, Hp / Def.MaxHp);
        public bool CanAct => !Dead && Stun <= 0f && Frozen <= 0f && Rooted <= 0f;
        public bool CanMove => !Dead && Stun <= 0f && Frozen <= 0f && Rooted <= 0f;
        public float SpeedMultiplier => Slow > 0f ? 0.45f : 1f;

        // postureFactor: fração do dano que vira postura (0,7 padrão). Retorna o dano aplicado.
        public DamageEvent TakeDamage(float amount, float postureFactor = 0.7f, bool canCrit = true, bool burn = false)
        {
            if (Dead) return default;
            bool crit = canCrit && Posture.IsBroken;
            if (crit) amount *= Combat.CritMultiplier;
            if (burn && Rooted > 0f) amount *= Combat.BurnMultiplier; else burn = false;
            float dmg = (float)Math.Max(1, Math.Round(amount));
            Hp -= dmg;
            bool broke = Posture.Add(dmg * postureFactor);
            var ev = new DamageEvent(dmg, crit, broke, burn, Dead);
            Damaged?.Invoke(ev);
            if (Dead) Died?.Invoke();
            return ev;
        }

        public bool AddPosture(float amount) => Posture.Add(amount);
        public void ApplyStun(float s) => Stun = Math.Max(Stun, s);
        public void ApplySlow(float s) => Slow = Math.Max(Slow, s);
        public void ApplyFreeze(float s) => Frozen = Math.Max(Frozen, s);
        public void ApplyRoot(float s) => Rooted = Math.Max(Rooted, s);
        public void HealFull() => Hp = Def.MaxHp;

        public void Tick(float dt)
        {
            Posture.Tick(dt);
            Stun = Math.Max(0f, Stun - dt);
            Slow = Math.Max(0f, Slow - dt);
            Frozen = Math.Max(0f, Frozen - dt);
            Rooted = Math.Max(0f, Rooted - dt);
        }
    }

    public readonly struct DamageEvent
    {
        public readonly float Amount;
        public readonly bool Crit, PostureBroke, Burn, Killed;
        public DamageEvent(float amount, bool crit, bool postureBroke, bool burn, bool killed)
        {
            Amount = amount; Crit = crit; PostureBroke = postureBroke; Burn = burn; Killed = killed;
        }
    }
}
