using System;

namespace Arena.Core
{
    // Regras do inimigo com corpo físico (ragdoll ativo, Fase A do plano — docs/plano-objetivo-final.md).
    // Ossos com juntas; "músculos" = molas que puxam a pose da animação; a bacia é presa à posição da IA.
    public enum RagBone { Bacia, Tronco, Cabeca, BracoE, AntebracoE, BracoD, AntebracoD, CoxaE, CanelaE, CoxaD, CanelaD }

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

        // Perfurar osso (B&S): a lâmina entra pelo próprio movimento, freada por um amortecedor ao longo dela.
        // Freio ao longo da lâmina diferente para entrar e para sair (como os "damper in/out" do B&S): a estocada afunda,
        // mas o corpo não escorrega sozinho para fora — só sai puxando com vontade.
        public const float StabDamperIn = 500f, StabDamperOut = 2500f;   // N·s/m
        public const float StabDamper = StabDamperIn;
        public const float PullOutSpeed = 0.3f;          // m/s da arma recuando ao longo da lâmina = "puxando"
        public const float StuckStandStrength = 0.35f;   // espetado: a "perna" amolece (dá para arrastar pelo cabo)
        public const float StabMinSpeed = 1.3f, StabMinAlignment = 0.5f, StabTipZone = 0.3f;
        public const float StabGuard = 0.15f;              // para antes da guarda
        public const float BleedDps = 4f;

        // Desmembrar: corte de fio rápido num membro.
        public const float SeverMinSpeed = 5.5f, SeverMinEdge = 0.6f;

        public static bool CanSever(RagBone b) => b != RagBone.Bacia && b != RagBone.Tronco;
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
}
