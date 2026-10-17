using System;

namespace Arena.Core
{
    public enum WeaponKind { Espada, Machado, Cajado, Maca, Lanca, Adaga }

    // Peso: com pouca Força a arma pesada "atrasa" na mão (lag no follow).
    public sealed class WeaponDef
    {
        public string Id { get; }
        public string Name { get; }
        public WeaponKind Kind { get; }
        public float Weight { get; }
        public float DamageMultiplier { get; }
        public float StaminaPerHit { get; }
        public float Length { get; }   // do cabo à ponta, em metros

        // Bônus do item (raridade, atributos, ferreiro, proficiência): o ItemInstance gera uma cópia ajustada da arma base.
        public float PostureBonus { get; private set; }     // multiplica o dano de postura (1 = normal)
        public float LifeSteal { get; private set; }        // fração do dano que vira vida
        public float ExecuteBonus { get; private set; }     // dano extra em inimigos com menos de 30% de vida
        public bool Proficient { get; private set; } = true; // a classe domina a arma: libera os bônus da árvore

        public WeaponDef(string id, string name, WeaponKind kind, float weight, float damageMultiplier, float staminaPerHit, float length)
        {
            Id = id; Name = name; Kind = kind; Weight = weight;
            DamageMultiplier = damageMultiplier; StaminaPerHit = staminaPerHit; Length = length;
            PostureBonus = 1f;
        }

        public WeaponDef With(string name, float weight, float damageMultiplier, float staminaPerHit,
                              float postureBonus, float lifeSteal, float executeBonus, bool proficient) =>
            new WeaponDef(Id, name, Kind, weight, damageMultiplier, staminaPerHit, Length)
            { PostureBonus = postureBonus, LifeSteal = lifeSteal, ExecuteBonus = executeBonus, Proficient = proficient };

        // Quão rápido a arma segue a mão (1/s). Força alta / arma leve = colada na mão.
        public float FollowRate(float strength) => Math.Clamp(60f * (float)Math.Pow(strength / Weight, 1.5), 5f, 45f);

        public static readonly WeaponDef EspadaLonga = new WeaponDef("espada", "Espada Longa", WeaponKind.Espada, 2.5f, 1.0f, 6f, 1.07f);
        public static readonly WeaponDef MachadoPesado = new WeaponDef("machado", "Machado Pesado", WeaponKind.Machado, 14f, 1.7f, 14f, 1.05f);
        public static readonly WeaponDef CajadoArcano = new WeaponDef("cajado", "Cajado Arcano", WeaponKind.Cajado, 2f, 0.5f, 5f, 1.14f);
        // Maça: contundente (não corta nem crava), quebra muita postura. Lança: longa, crava. Adaga: leve, rápida, crava.
        public static readonly WeaponDef MacaPesada = new WeaponDef("maca", "Maça", WeaponKind.Maca, 9f, 1.35f, 11f, 0.85f);
        public static readonly WeaponDef Lanca = new WeaponDef("lanca", "Lança", WeaponKind.Lanca, 5f, 1.1f, 8f, 1.9f);
        public static readonly WeaponDef Adaga = new WeaponDef("adaga", "Adaga", WeaponKind.Adaga, 1f, 0.7f, 3f, 0.45f);

        public static readonly WeaponDef[] All = { EspadaLonga, MachadoPesado, CajadoArcano, MacaPesada, Lanca, Adaga };

        // Arma contundente: o "fio" não importa e não corta partes do esqueleto.
        public bool Blunt => Kind == WeaponKind.Maca || Kind == WeaponKind.Cajado;
        // Fração do dano que vira postura no inimigo (maça quebra postura).
        public float PostureFactor => (Kind == WeaponKind.Maca ? 1.15f : 0.7f) * PostureBonus;
        public bool CanStab => Kind == WeaponKind.Espada || Kind == WeaponKind.Lanca || Kind == WeaponKind.Adaga;

        public static WeaponDef[] LoadoutFor(ClassId cls) =>
            cls == ClassId.Lamina ? new[] { EspadaLonga, MachadoPesado } : new[] { CajadoArcano, Adaga };
    }
}

namespace Arena.Core
{
    // Física da arma na mão (Fase 1): a arma é um corpo físico preso a um "pulso" invisível por uma mola.
    // Técnica do Blade & Sorcery (spring + damper) e do guia de Evan Fletcher (mola de posição mais forte que a de
    // rotação: ao bater, a arma gira e desliza no obstáculo em vez de empurrar a mão).
    // Rigidez = Força: a frequência natural da mola é o FollowRate (Força alta / arma leve = firme na mão).
    public static class WeaponPhysics
    {
        public const float DampingRatio = 0.9f;        // < 1 balança um pouco; 1 = sem passar do ponto
        public const float AngularRatio = 0.6f;        // rotação mais "mole" que a posição
        public const float MaxAcceleration = 600f;     // m/s²: limite da mola (evita explodir contra paredes)
        public const float SnapDistance = 1.2f;        // presa longe da mão por muito tempo (e sem parede no meio) = volta para a mão
        public const float SnapSeconds = 1f;
        public const float ThrowMinHandSpeed = 2f;     // soltar o grip com a mão mais rápida que isso arremessa; mais devagar, só larga
        public const float GhostHandDistance = 0.08f;  // a partir daqui mostra a mão fantasma

        // Massa em kg (peso real aproximado; o "Weight" do WeaponDef é a escala de jogo usada no FollowRate).
        public static float Mass(WeaponDef w) => w.Kind switch
        {
            WeaponKind.Espada => 1.4f,
            WeaponKind.Machado => 3.5f,
            WeaponKind.Maca => 2.6f,
            WeaponKind.Lanca => 2.2f,
            WeaponKind.Adaga => 0.5f,
            _ => 1.2f,
        };

        // Frequência natural (rad/s) da mola de posição.
        public static float LinearOmega(WeaponDef w, float strength) => w.FollowRate(strength);
        public static float AngularOmega(WeaponDef w, float strength) => w.FollowRate(strength) * AngularRatio;

        // Mola e amortecedor para um corpo de massa (ou inércia) m com frequência natural ω.
        public static float Spring(float m, float omega) => m * omega * omega;
        public static float Damper(float m, float omega) => 2f * DampingRatio * m * omega;

        // Rigidez mínima para segurar o próprio peso parado: no máximo ~20° de queda e 6 cm de afundamento.
        // (Sem isso, com pouca Força, o machado despencava 72° e ficava pendurado.) O peso continua no atraso dos golpes.
        public const float Gravity = 9.81f, MaxRestDroopRad = 0.35f, MaxRestSag = 0.06f;
        public static float LinearSpringHolding(float mass, float omega) => Math.Max(Spring(mass, omega), mass * Gravity / MaxRestSag);
        // Calibração medida na Unity 6.3 (PhysX): a mola de rotação (slerp drive) da ConfigurableJoint segura ~4x menos
        // que a conta torque = k·ângulo (machado: k=78 caía 70°, k=300 caía 20°). A mola de posição bate com a conta.
        public const float AngularDriveCalibration = 4f;
        public static float AngularSpringHolding(float inertia, float omega, float mass, float comDistance) =>
            AngularDriveCalibration * Math.Max(Spring(inertia, omega), mass * Gravity * comDistance / MaxRestDroopRad);
        // Amortecedor para uma mola k já escolhida (mantém o mesmo fator de amortecimento).
        public static float DamperFor(float k, float m) => 2f * DampingRatio * (float)Math.Sqrt(k * m);

        // Força máxima da mola de posição (N).
        public static float MaxForce(WeaponDef w) => Mass(w) * MaxAcceleration;

        // Vibração no impacto (0..1) a partir do impulso da colisão (N·s), relativo à massa da arma.
        public static float HapticFromImpulse(float impulse, WeaponDef w) => Math.Clamp(impulse / (Mass(w) * 3f), 0f, 1f);
    }
}

namespace Arena.Core
{
    // Parte da arma que causa dano (no espaço local da arma: Z = cabo → ponta, empunhadura em Z=0).
    // Espada: a lâmina toda (dois gumes, qualquer corte de fio vale). Machado: só o GUME da cabeça, que fica
    // 30 cm ao lado do cabo e olha para -Y (antes o dano era medido pelo cabo e a cabeça batia antes de "chegar").
    public readonly struct WeaponStrike
    {
        public readonly System.Numerics.Vector3 A, B;     // segmento que acerta
        public readonly System.Numerics.Vector3 EdgeDir;  // direção para onde o gume corta (zero = não tem gume único)
        public bool HasSingleEdge => EdgeDir != System.Numerics.Vector3.Zero;

        public WeaponStrike(System.Numerics.Vector3 a, System.Numerics.Vector3 b, System.Numerics.Vector3 edge) { A = a; B = b; EdgeDir = edge; }

        public static WeaponStrike For(WeaponDef w) => w.Kind switch
        {
            WeaponKind.Machado => new WeaponStrike(new System.Numerics.Vector3(0f, -0.3f, 0.8f), new System.Numerics.Vector3(0f, -0.3f, 1.04f),
                                                   new System.Numerics.Vector3(0f, -1f, 0f)),
            // Maça: só a cabeça (últimos 25 cm). Lança: só a ponta (últimos 45 cm); a haste não fere.
            WeaponKind.Maca => new WeaponStrike(new System.Numerics.Vector3(0f, 0f, w.Length - 0.25f), new System.Numerics.Vector3(0f, 0f, w.Length), System.Numerics.Vector3.Zero),
            WeaponKind.Lanca => new WeaponStrike(new System.Numerics.Vector3(0f, 0f, w.Length - 0.45f), new System.Numerics.Vector3(0f, 0f, w.Length), System.Numerics.Vector3.Zero),
            _ => new WeaponStrike(new System.Numerics.Vector3(0f, 0f, 0.12f), new System.Numerics.Vector3(0f, 0f, w.Length), System.Numerics.Vector3.Zero),
        };

        // Qualidade do corte de um gume único: 1 = gume na frente do movimento, 0 = de lado ou de costas.
        public static float SingleEdgeAlignment(System.Numerics.Vector3 edgeWorld, System.Numerics.Vector3 velocity)
        {
            float speed = velocity.Length();
            if (speed < 1e-4f) return 0f;
            return Math.Clamp(System.Numerics.Vector3.Dot(System.Numerics.Vector3.Normalize(edgeWorld), velocity / speed), 0f, 1f);
        }
    }
}
