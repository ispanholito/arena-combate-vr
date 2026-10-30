using System;

namespace Arena.Core
{
    public enum WeaponKind { Espada, Machado, Cajado, Maca, Lanca, Adaga }
    // Visual da arma. O "Kind" decide como ela funciona (corta, crava, pancada, magia); o "Look" só muda o modelo,
    // a massa e o equilíbrio. Assim uma arma nova não precisa mexer em todo o combate.
    public enum WeaponLook { Padrao, Katana, Rapieira, Martelo, Machadinha, Bacalhau, Frigideira, Tocha, Vassoura, Pistola, Bacamarte, PistolaRepeticao, Escudo }

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
        public WeaponLook Look { get; private set; }
        public float HeatPerHit { get; private set; }       // tocha: todo golpe põe calor (acende)
        public float MassKg { get; private set; }           // 0 = massa padrão do tipo
        public bool Funny { get; private set; }             // arma engraçada (peixe, frigideira...): avisa ao acertar

        public WeaponDef(string id, string name, WeaponKind kind, float weight, float damageMultiplier, float staminaPerHit, float length)
        {
            Id = id; Name = name; Kind = kind; Weight = weight;
            DamageMultiplier = damageMultiplier; StaminaPerHit = staminaPerHit; Length = length;
            PostureBonus = 1f;
        }

        public WeaponDef With(string name, float weight, float damageMultiplier, float staminaPerHit,
                              float postureBonus, float lifeSteal, float executeBonus, bool proficient) =>
            new WeaponDef(Id, name, Kind, weight, damageMultiplier, staminaPerHit, Length)
            { PostureBonus = postureBonus, LifeSteal = lifeSteal, ExecuteBonus = executeBonus, Proficient = proficient,
              Look = Look, HeatPerHit = HeatPerHit, MassKg = MassKg, Funny = Funny };

        // Quão rápido a arma segue a mão (1/s). Força alta / arma leve = colada na mão.
        public float FollowRate(float strength) => Math.Clamp(60f * (float)Math.Pow(strength / Weight, 1.5), 5f, 45f);

        // Balanceamento (2026-10-30, Pablo: "o dano de todas as armas está desbalanceado"): no VR todo mundo balança a mão
        // mais ou menos igual, então o multiplicador quase vira o dano por segundo. Pesadas: +45-55% por golpe (não +70-90%)
        // e gastam mais estamina; Katana = Espada (era melhor em tudo); Adaga sobe (alcance curto = ponta lenta).
        public static readonly WeaponDef EspadaLonga = new WeaponDef("espada", "Espada Longa", WeaponKind.Espada, 2.5f, 1.0f, 6f, 1.07f);
        public static readonly WeaponDef MachadoPesado = new WeaponDef("machado", "Machado Pesado", WeaponKind.Machado, 14f, 1.45f, 18f, 1.05f);
        public static readonly WeaponDef CajadoArcano = new WeaponDef("cajado", "Cajado Arcano", WeaponKind.Cajado, 2f, 0.5f, 5f, 1.14f);
        // Maça: contundente (não corta nem crava), quebra muita postura. Lança: longa, crava. Adaga: leve, rápida, crava.
        public static readonly WeaponDef MacaPesada = new WeaponDef("maca", "Maça", WeaponKind.Maca, 9f, 1.25f, 13f, 0.85f);
        public static readonly WeaponDef Lanca = new WeaponDef("lanca", "Lança", WeaponKind.Lanca, 5f, 1.0f, 8f, 1.9f);
        public static readonly WeaponDef Adaga = new WeaponDef("adaga", "Adaga", WeaponKind.Adaga, 1f, 0.85f, 3f, 0.45f);

        // Armas novas (2026-10-29, inspiração Soul Knight): variações sérias e algumas engraçadas.
        public static readonly WeaponDef Katana = new WeaponDef("katana", "Katana", WeaponKind.Espada, 2f, 1.0f, 5f, 1.0f) { Look = WeaponLook.Katana, MassKg = 1.1f };
        public static readonly WeaponDef Rapieira = new WeaponDef("rapieira", "Rapieira", WeaponKind.Espada, 1.2f, 0.9f, 4f, 1.05f) { Look = WeaponLook.Rapieira, MassKg = 0.9f };
        public static readonly WeaponDef Martelo = new WeaponDef("martelo", "Martelo de Guerra", WeaponKind.Maca, 16f, 1.55f, 20f, 1.1f) { Look = WeaponLook.Martelo, MassKg = 4.5f };
        public static readonly WeaponDef Machadinha = new WeaponDef("machadinha", "Machadinha", WeaponKind.Machado, 4f, 1.05f, 6f, 0.55f) { Look = WeaponLook.Machadinha, MassKg = 1.0f };
        public static readonly WeaponDef Bacalhau = new WeaponDef("bacalhau", "Bacalhau Seco", WeaponKind.Maca, 3f, 0.85f, 4f, 0.8f) { Look = WeaponLook.Bacalhau, MassKg = 1.2f, Funny = true };
        public static readonly WeaponDef Frigideira = new WeaponDef("frigideira", "Frigideira de Ferro", WeaponKind.Maca, 5f, 1.0f, 6f, 0.62f) { Look = WeaponLook.Frigideira, MassKg = 1.8f, Funny = true };
        public static readonly WeaponDef Tocha = new WeaponDef("tocha", "Tocha", WeaponKind.Maca, 2.5f, 0.75f, 4f, 0.72f) { Look = WeaponLook.Tocha, MassKg = 1.0f, HeatPerHit = 35f };
        public static readonly WeaponDef Vassoura = new WeaponDef("vassoura", "Vassoura de Bruxa", WeaponKind.Cajado, 2f, 0.45f, 4f, 1.45f) { Look = WeaponLook.Vassoura, MassKg = 0.9f, Funny = true };

        // Armas de fogo (Core/Guns.cs): batem como maça leve; o tiro vem do GunSpec.
        public static readonly WeaponDef Pistola = new WeaponDef("pistola", "Pistola de Pederneira", WeaponKind.Maca, 2f, 0.6f, 3f, 0.32f) { Look = WeaponLook.Pistola, MassKg = 1.1f };
        // Semiautomática (pedido do Pablo, 2026-10-29): 8 tiros, um por puxada do gatilho, bem rápida.
        public static readonly WeaponDef PistolaRepeticao = new WeaponDef("pistola_rep", "Pistola de Repetição", WeaponKind.Maca, 2.2f, 0.65f, 3f, 0.34f) { Look = WeaponLook.PistolaRepeticao, MassKg = 1.2f };
        public static readonly WeaponDef Bacamarte = new WeaponDef("bacamarte", "Bacamarte", WeaponKind.Maca, 5f, 0.8f, 5f, 0.66f) { Look = WeaponLook.Bacamarte, MassKg = 2.8f };

        // Escudo (2026-10-30): virou item como as armas (achado; vai em qualquer mão ou nas costas). Funciona como pancada
        // (Maça) se bater com ele; na mão esquerda bloqueia e apara (PlayerShield).
        public static readonly WeaponDef Escudo = new WeaponDef("escudo", "Escudo de Madeira", WeaponKind.Maca, 4f, 0.6f, 6f, 0.62f) { Look = WeaponLook.Escudo, MassKg = 2.5f };

        public static readonly WeaponDef[] All = { EspadaLonga, MachadoPesado, CajadoArcano, MacaPesada, Lanca, Adaga,
                                                   Katana, Rapieira, Martelo, Machadinha, Bacalhau, Frigideira, Tocha, Vassoura, Pistola, Bacamarte, PistolaRepeticao, Escudo };
        public bool IsShield => Look == WeaponLook.Escudo;
        // Arma GRANDE (só nos coldres dos ombros): escudo, lança, cajados, machado pesado, martelo, bacamarte.
        // Pequenas e médias também cabem na cintura.
        public bool IsLarge => IsShield || Kind == WeaponKind.Cajado || Length > 1.15f || Look == WeaponLook.Bacamarte || WeaponPhysics.Mass(this) >= 3f;
        public bool IsGun => Look == WeaponLook.Pistola || Look == WeaponLook.Bacamarte || Look == WeaponLook.PistolaRepeticao;

        // Arma contundente: o "fio" não importa e não corta partes do esqueleto.
        public bool Blunt => Kind == WeaponKind.Maca || Kind == WeaponKind.Cajado;
        // Fração do dano que vira postura no inimigo (maça quebra postura).
        public float PostureFactor => (Kind == WeaponKind.Maca ? 1.15f : 0.7f) * PostureBonus;
        public bool CanStab => Kind == WeaponKind.Espada || Kind == WeaponKind.Lanca || Kind == WeaponKind.Adaga;

        // Kit inicial: [0] mão direita, [1] mão esquerda (null = vazia), o resto vai para os coldres.
        public static WeaponDef[] LoadoutFor(ClassId cls) =>
            cls == ClassId.Lamina ? new[] { EspadaLonga, Escudo, MachadoPesado } : new[] { CajadoArcano, null, Adaga };
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
        // O cabo fica TRAVADO na mão (limite rígido com um pouco de mola): antes era só mola e, no balanço rápido, a força
        // centrífuga esticava 20-90 cm (a lança "saía da mão"). Encostando em parede/inimigo o limite solta por um instante
        // para a arma parar no obstáculo em vez de atravessar.
        public const float GripLimit = 0.03f, GripLimitSpring = 40000f, GripLimitDamper = 600f, ContactRelaxSeconds = 0.15f;
        public const float ThrowMinHandSpeed = 2f;     // soltar o grip com a mão mais rápida que isso arremessa; mais devagar, só larga
        public const float GhostHandDistance = 0.08f;  // a partir daqui mostra a mão fantasma

        // Massa em kg (peso real aproximado; o "Weight" do WeaponDef é a escala de jogo usada no FollowRate).
        public static float Mass(WeaponDef w) => w.MassKg > 0f ? w.MassKg : w.Kind switch
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

        // Martelo de Guerra (2026-10-30, "hitbox uma merda"): a cabeça tem 42 cm de largura e bate com as FACES (±x);
        // o segmento ao longo do cabo passava pelo meio dela e não pegava quem levava a pancada com a face.
        public static WeaponStrike For(WeaponDef w) => w.Look == WeaponLook.Martelo
            ? new WeaponStrike(new System.Numerics.Vector3(-0.23f, 0f, 0.98f), new System.Numerics.Vector3(0.23f, 0f, 0.98f), System.Numerics.Vector3.Zero)
            : KindStrike(w);

        static WeaponStrike KindStrike(WeaponDef w) => w.Kind switch
        {
            // Gume proporcional ao tamanho (o machado de 1,05 m fica como era; a machadinha tem a cabeça menor).
            WeaponKind.Machado => new WeaponStrike(new System.Numerics.Vector3(0f, -0.3f * Math.Min(1f, w.Length / 1.05f), w.Length - 0.25f),
                                                   new System.Numerics.Vector3(0f, -0.3f * Math.Min(1f, w.Length / 1.05f), w.Length - 0.01f),
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
