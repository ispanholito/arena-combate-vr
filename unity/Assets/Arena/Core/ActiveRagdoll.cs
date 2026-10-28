using System;
using System.Numerics;

namespace Arena.Core
{
    // Regras do inimigo com corpo físico (ragdoll ativo, Fase A do plano — docs/plano-objetivo-final.md).
    // Ossos com juntas; "músculos" = molas que puxam a pose da animação; a bacia é presa à posição da IA.
    public enum RagBone { Bacia, Tronco, Cabeca, BracoE, AntebracoE, BracoD, AntebracoD, CoxaE, CanelaE, CoxaD, CanelaD, Clava }

    public static class ActiveRagdollRules
    {
        // Força dos músculos (0..1): normal, atingido (amolece um instante), caído, morto.
        public const float MuscleNormal = 1f, MuscleHit = 0.3f, MuscleDown = 0.15f, MuscleDead = 0f;
        public const float HitFlopSeconds = 0.35f;
        public const float DownSeconds = 2.2f, StandUpSeconds = 0.8f;

        // Desequilíbrio: golpe forte no tronco/cabeça ou bacia arrastada para longe da posição da IA.
        public const float KnockdownSpeed = 7.5f;          // m/s do golpe (arma pesada: 6)
        public const float KnockdownSpeedHeavy = 6f;
        public const float DragOffDistance = 0.45f;        // bacia a mais de 45 cm do ponto da IA = perdeu o pé
        public const float DragOffDistanceHeld = 0.25f;    // agarrado/espetado: 25 cm já tira do chão

        // Perfurar osso (B&S): a lâmina entra pelo próprio movimento, freada por um amortecedor ao longo dela.
        // Freio ao longo da lâmina diferente para entrar e para sair (como os "damper in/out" do B&S): a estocada afunda,
        // mas o corpo não escorrega sozinho para fora — só sai puxando com vontade.
        public const float StabDamperIn = 500f, StabDamperOut = 20000f;   // saída parado ~ atrito (quase travado, sem trava rígida)   // N·s/m
        public const float StabDamper = StabDamperIn;
        public const float StabDamperPull = 300f;      // a mão puxando a espada para trás: sai fácil ("held damper out" do B&S)
        public const float PullOutSpeed = 0.12f;          // m/s da arma recuando ao longo da lâmina = "puxando"
        public const float StuckStandStrength = 0.35f;   // espetado: a "perna" amolece (dá para arrastar pelo cabo)
        public const float StabMinSpeed = 1.3f, StabMinAlignment = 0.5f, StabTipZone = 0.3f;
        public const float StabStartDepth = 0.15f, StabSettleSeconds = 0.3f;   // começa 15 cm dentro; 0,3 s para a junta se acomodar
        public const float StabGuard = 0.15f;              // para antes da guarda
        public const float BleedDps = 4f;

        // Desmembrar: corte de fio rápido num membro.
        public const float SeverMinSpeed = 5.5f, SeverMinEdge = 0.6f;

        public static bool CanSever(RagBone b) => b != RagBone.Bacia && b != RagBone.Tronco && b != RagBone.Clava;

        // ---------- IA física (passo A3) ----------
        // Desarmar: golpe forte no braço/antebraço da clava (ou na própria clava) faz soltar a arma, como no B&S.
        public const float DisarmSpeed = 4f;
        public static bool Disarms(RagBone b, float speed) =>
            speed >= DisarmSpeed && (b == RagBone.AntebracoD || b == RagBone.BracoD || b == RagBone.Clava);
        // Desarmado ataca com socos: mais perto e fraco.
        public const float PunchReach = 1.1f, PunchDamageFraction = 0.35f;

        // Defesa: quando a arma do jogador vem rápida perto da cabeça/tronco, às vezes ergue a clava na frente.
        public const float GuardTriggerSpeed = 3f, GuardTriggerDistance = 1.2f, GuardSeconds = 0.6f, GuardCooldown = 1.6f;
        public static float GuardChance(Difficulty d) => d switch { Difficulty.Facil => 0.15f, Difficulty.Dificil => 0.5f, _ => 0.3f };

        // Aparo (espada ou escudo empurrado contra o golpe): cambaleia — janela para punir.
        public const float ParryStunSeconds = 1.1f;
        // Com pouca vida às vezes recua um pouco (como o goblin antigo).
        public const float RetreatHpFraction = 0.3f, RetreatChance = 0.35f, RetreatSeconds = 1.4f;

        // Sem ficha de ataque: ronda o jogador a essa distância em vez de ficar parado na frente.
        public const float CircleRadius = 2.1f, CircleSpeed = 0.9f;
        public static bool IsLeg(RagBone b) => b == RagBone.CoxaE || b == RagBone.CanelaE || b == RagBone.CoxaD || b == RagBone.CanelaD;
        public static bool IsWeaponArm(RagBone b) => b == RagBone.BracoD || b == RagBone.AntebracoD;

        // Multiplicador de dano por osso (cabeça ×2, membros ×0,7).
        public static float DamageMultiplier(RagBone b) => b switch
        {
            RagBone.Cabeca => 2f,
            RagBone.Tronco or RagBone.Bacia => 1f,
            _ => 0.7f,
        };

        public static bool Knocksdown(RagBone b, float speed, bool heavyWeapon) =>
            (b == RagBone.Tronco || b == RagBone.Cabeca || b == RagBone.Bacia) && speed >= (heavyWeapon ? KnockdownSpeedHeavy : KnockdownSpeed);

        public static bool Severs(RagBone b, float speed, float edge, bool bluntWeapon) =>
            CanSever(b) && !bluntWeapon && edge >= SeverMinEdge && speed >= SeverMinSpeed;

        public static bool CanStab(float speed, float alignment, float fromTip) =>
            speed >= StabMinSpeed && alignment >= StabMinAlignment && fromTip <= StabTipZone;

        // Força da mola do músculo para um osso de massa m (N·m/rad) e amortecedor.
        public static float MuscleSpring(float mass, float muscle) => 90f * mass * Math.Max(0f, muscle);
        public static float MuscleDamper(float mass) => 6f * mass;
    }

    // Poses dos ataques no corpo físico (graus do braço e antebraço da clava, relativos à pose de pé).
    // X = para frente/trás (negativo = braço para frente), Z = para o lado (positivo = para fora/direita).
    public static class ActiveRagdollPoses
    {
        public static (Vector3 arm, Vector3 fore) Windup(EnemyAttack a) => a switch
        {
            EnemyAttack.DireitaEsquerda => (new Vector3(-55f, 0f, 85f), new Vector3(-25f, 0f, 0f)),    // clava aberta à direita
            EnemyAttack.EsquerdaDireita => (new Vector3(-75f, 0f, -55f), new Vector3(-95f, 0f, 0f)),   // clava cruzada à esquerda
            _ => (new Vector3(150f, -15f, 0f), new Vector3(-70f, 0f, 0f)),                             // de cima (e passo à frente)
        };

        public static (Vector3 arm, Vector3 fore) Strike(EnemyAttack a) => a switch
        {
            EnemyAttack.DireitaEsquerda => (new Vector3(-85f, 0f, -45f), new Vector3(-15f, 0f, 0f)),   // passa da direita para a esquerda
            EnemyAttack.EsquerdaDireita => (new Vector3(-70f, 0f, 75f), new Vector3(-10f, 0f, 0f)),    // passa da esquerda para a direita
            _ => (new Vector3(-75f, 10f, 0f), new Vector3(-5f, 0f, 0f)),
        };

        // Guarda: clava atravessada na frente da cabeça.
        public static (Vector3 arm, Vector3 fore) Guard => (new Vector3(-100f, 0f, 25f), new Vector3(-85f, 0f, 0f));

        public const float StepLunge = 0.6f;   // passo à frente avança a bacia durante o golpe (m)
    }
}

namespace Arena.Core
{
    public enum RagWeapon { Clava, Espada, Machado }

    // Como é o corpo físico de cada tipo de inimigo (passo A4 do plano): tamanho, peso, cores, arma e regras próprias.
    public sealed class RagdollSpec
    {
        public float Scale = 1f, MassMultiplier = 1f, AnchorMultiplier = 1f;
        public float KnockdownMultiplier = 1f;   // × a velocidade para derrubar
        public float SeverMultiplier = 1f;       // × a velocidade para arrancar membro
        public float DisarmMultiplier = 1f;
        public float LimbMuscle = 1f;            // × força dos músculos de braços, pernas e cabeça (esqueleto: mais solto)
        public bool SeverNeedsHeavy;             // orc: só arma pesada arranca membro
        public bool Grabbable = true;
        public bool HeadKills = true;            // esqueleto continua sem cabeça
        public bool Reassembles;                 // esqueleto: peças voltam se não forem quebradas
        public RagWeapon Weapon = RagWeapon.Clava;
        public float WeaponLength = 0.5f, WeaponRadius = 0.06f, WeaponMass = 1.3f;
        public (float r, float g, float b) Skin = (0.37f, 0.54f, 0.23f), Cloth = (0.35f, 0.25f, 0.16f), Eye = (1f, 0.88f, 0.29f);
        public bool Ears = true;

        public const float ReassembleSeconds = 4f;   // como o SkeletonBody antigo
        public const float BreakSpeed = 2f;          // golpe numa peça caída (no chão) quebra a peça

        public static RagdollSpec For(EnemyKind k) => k switch
        {
            EnemyKind.Orc => new RagdollSpec
            {
                Scale = 1.35f, MassMultiplier = 2.5f, AnchorMultiplier = 1.6f, KnockdownMultiplier = 1.4f, SeverMultiplier = 1.25f,
                DisarmMultiplier = 1.5f, SeverNeedsHeavy = true, Grabbable = false, Weapon = RagWeapon.Machado,
                WeaponLength = 0.75f, WeaponRadius = 0.05f, WeaponMass = 3.5f,
                Skin = (0.3f, 0.42f, 0.25f), Cloth = (0.35f, 0.35f, 0.38f), Eye = (1f, 0.3f, 0.2f), Ears = false,
            },
            EnemyKind.Esqueleto => new RagdollSpec
            {
                Scale = 1.08f, MassMultiplier = 0.6f, KnockdownMultiplier = 0.9f, SeverMultiplier = 0.8f, HeadKills = false, Reassembles = true, LimbMuscle = 0.7f,
                Weapon = RagWeapon.Espada, WeaponLength = 0.8f, WeaponRadius = 0.035f, WeaponMass = 1.2f,
                Skin = (0.86f, 0.84f, 0.76f), Cloth = (0.78f, 0.76f, 0.68f), Eye = (1f, 0.25f, 0.2f), Ears = false,
            },
            _ => new RagdollSpec(),
        };

        // Tipos que já têm corpo físico na opção beta.
        public static bool HasPhysicalBody(EnemyKind k) => k == EnemyKind.Goblin || k == EnemyKind.Orc || k == EnemyKind.Esqueleto;
    }
}
