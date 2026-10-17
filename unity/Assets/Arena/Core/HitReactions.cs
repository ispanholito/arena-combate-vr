using System;

namespace Arena.Core
{
    // Reação ao impacto físico da arma (ideia do HitReaction do Blade & Sorcery): força do impacto × o que o inimigo
    // está fazendo × parte do corpo. Acertar quem está no meio do ataque, ou na cabeça/pernas, derruba mais; em guarda
    // ele aguenta mais; golpes seguidos em quem já cambaleia derrubam.
    public enum EnemyActivity { Guarda, Atacando, Cambaleando }
    public enum HitReaction { Nenhuma, Leve, Media, Total, Derrubado }

    public static class HitReactions
    {
        // Nível do impacto (0-3) pela velocidade equivalente (impulso / massa do inimigo).
        public static int Level(float impactSpeed) => impactSpeed < 1.5f ? 0 : impactSpeed < 2.5f ? 1 : impactSpeed < 4f ? 2 : 3;

        static readonly HitReaction[,] Table =
        {
            //               Guarda               Atacando              (cabeça/pernas soma 1 nível)
            /* nível 0 */ { HitReaction.Nenhuma, HitReaction.Nenhuma },
            /* nível 1 */ { HitReaction.Leve,    HitReaction.Media },
            /* nível 2 */ { HitReaction.Media,   HitReaction.Total },
            /* nível 3 */ { HitReaction.Total,   HitReaction.Derrubado },
        };

        public static HitReaction Resolve(EnemyKind kind, int level, EnemyActivity activity, bool headOrLegs, bool postureBroken)
        {
            if (kind == EnemyKind.Boneco || kind == EnemyKind.Slime) return HitReaction.Nenhuma;
            // Chefe só reage com a postura quebrada; orc é pesado (um nível a menos).
            if (kind == EnemyKind.ReiGoblin && !postureBroken) return HitReaction.Nenhuma;
            if (kind == EnemyKind.Orc) level--;
            if (headOrLegs) level++;
            if (activity == EnemyActivity.Cambaleando) level++;
            if (level < 0) return HitReaction.Nenhuma;
            int col = activity == EnemyActivity.Guarda ? 0 : 1;
            int row = Math.Min(level, 3);
            var r = Table[row, col];
            // Acima do topo da tabela: derruba.
            if (level > 3 && r < HitReaction.Derrubado) r = (HitReaction)Math.Min((int)HitReaction.Derrubado, (int)r + (level - 3));
            return r;
        }

        // Duração do atordoamento de cada reação (s).
        public static float StunSeconds(HitReaction r) => r switch
        {
            HitReaction.Leve => 0.15f,
            HitReaction.Media => 0.35f,
            HitReaction.Total => 0.7f,
            HitReaction.Derrubado => 1.5f,
            _ => 0f,
        };

        public const float Cooldown = 0.8f;   // entre reações do mesmo inimigo (não trava ninguém)
    }
}
