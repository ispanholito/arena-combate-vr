using System;

namespace Arena.Core
{
    // Fórmulas de combate (portadas do protótipo web v0.3).
    public static class Combat
    {
        public const float MinSwingSpeed = 2.2f;      // m/s na ponta para contar como golpe
        public const float ParrySpeed = 1.5f;         // m/s na ponta para o contato virar aparo
        public const float SpeedCap = 9f;
        public const float SharpEdgeBonus = 1.25f;    // Fio Afiado
        public const float TiredMultiplier = 0.4f;    // golpe sem estamina
        public const float CritMultiplier = 2.5f;     // golpe com postura quebrada
        public const float HitCooldown = 0.35f;       // por inimigo

        // Dano corpo a corpo. edgeAlignment: 1 = golpe de fio/estocada limpa, 0 = pancada de lado (cabo/chapa).
        public static float MeleeDamage(float tipSpeed, WeaponDef weapon, bool sharpEdge, bool hasStamina, float edgeAlignment = 1f)
        {
            if (tipSpeed < MinSwingSpeed) return 0f;
            float dmg = (8f + Math.Min(tipSpeed, SpeedCap) * 2.5f) * weapon.DamageMultiplier;
            dmg *= 0.6f + 0.4f * Math.Clamp(edgeAlignment, 0f, 1f);
            if (sharpEdge) dmg *= SharpEdgeBonus;
            if (!hasStamina) dmg *= TiredMultiplier;
            return dmg;
        }

        // Fio Afiado (árvore) só vale com arma que a classe domina.
        public static bool SharpEdge(bool hasFioAfiado, WeaponDef weapon) => hasFioAfiado && weapon.Proficient;
        // Atributo Execução: dano extra em inimigo com pouca vida.
        public static float ExecuteMultiplier(WeaponDef weapon, float enemyHpFraction) =>
            enemyHpFraction < ItemInstance.ExecuteThreshold ? 1f + weapon.ExecuteBonus : 1f;

        public static float Knockback(float tipSpeed, WeaponDef weapon) => Math.Min(6f, tipSpeed) * 0.4f * weapon.DamageMultiplier;

        // Bola de Fogo: carga 0..1, cai 40% na borda, QUEIMA (+60%) em enraizados.
        public static float FireballDamage(float charge) => 18f + 12f * Math.Clamp(charge, 0f, 1f);
        public static float FireballRadius(float charge, bool combustao) => (1.4f + 0.6f * Math.Clamp(charge, 0f, 1f)) * (combustao ? 1.6f : 1f);
        public static float ExplosionFalloff(float distance, float radius) => 1f - 0.4f * Math.Min(1f, distance / Math.Max(0.01f, radius));
        public const float BurnMultiplier = 1.6f;

        public const float IceDamage = 22f, IcePosture = 0.9f, IceSlowSeconds = 2.5f, IceFreezeSeconds = 1.5f;
        public const int IcePierce = 3;
        public const float RootSeconds = 3f, RootRadius = 2.2f;
        public const float RootMaxRange = 12f;

        // Mira no chão: distância horizontal até onde o raio (da altura h, direção com componente vertical dirY e horizontal
        // dirFlat) toca o chão, limitada ao alcance. Mirando para cima ou longe demais fica TRAVADA no alcance máximo
        // (antes ela pulava para 6 m na frente do jogador).
        public static float GroundAimReach(float originHeight, float dirY, float dirFlat, float maxRange)
        {
            if (dirY >= -1e-3f || dirFlat < 1e-4f) return maxRange;
            return Math.Min(maxRange, originHeight * dirFlat / -dirY);
        }
        public const float WindSlashDamage = 24f, WindSlashWidth = 2.2f, WindSlashHitRadius = 1.0f;   // corte quase 2x maior (era 1,2 m / 0,55)
        // Postura de Contra: o contra-ataque causa dano alto direto (não quebra mais a postura).
        public const float CounterDamage = 90f;

        public static float DashCost(bool passoLeve) => passoLeve ? 15f : 25f;
        public static float ChargeLength(bool cadeia) => cadeia ? 6.3f : 4.5f;
        public const float ChargeDamage = 40f, ChargeDuration = 0.24f, ChargeInvulnerable = 0.4f;
        public const float DashDuration = 0.16f, DashSpeed = 16f, MoveSpeed = 2.6f;
        public const float ArenaHalfSize = 8.6f;
    }

    // Poderes da mão esquerda livre (hand tracking). Gastam mana, qualquer classe.
    public static class HandPowers
    {
        public const string Slam = "mao_chao", Palm = "mao_palma", Punch = "mao_soco";
        // Raio do Freeza (substituiu a metralhadora, que dava 1,2 de dano por mana e "não compensava"): raio fino e
        // instantâneo da ponta dos dedos, atravessa 2 inimigos. 16 de dano por 6 de mana = 2,7 por mana (por alvo).
        public const float BeamManaPerShot = 6f, BeamDamage = 16f, BeamInterval = 0.4f, BeamRange = 25f, BeamPosture = 0.8f;
        public const int BeamPierce = 2;
        // Soco (substituiu o golpe no chão): punho fechado indo rápido para frente acerta UM inimigo um pouco além do
        // alcance do braço. Forte e de recarga baixa.
        public const float PunchCooldown = 1f, PunchCost = 10f, PunchDamage = 30f, PunchPosture = 1.2f, PunchKnock = 4f;
        public const float PunchMinSpeed = 2.5f, PunchReach = 1.1f, PunchWidth = 0.35f;
        // Golpe no chão (atordoar em área): GUARDADO para uma habilidade futura (pedido do Pablo) — fora do jogo por enquanto.
        public const float SlamCooldown = 6f, SlamCost = 20f, SlamRadius = 3f, SlamStun = 1.8f, SlamDamage = 12f, SlamPosture = 2.5f;

        // O soco acerta o inimigo (centro do corpo) que está à frente da mão, até PunchReach além dela?
        public static bool PunchHits(System.Numerics.Vector3 hand, System.Numerics.Vector3 dir, System.Numerics.Vector3 enemyCenter, float enemyRadius)
        {
            var d = System.Numerics.Vector3.Normalize(dir);
            var to = enemyCenter - hand;
            float along = System.Numerics.Vector3.Dot(to, d);
            if (along < -enemyRadius || along > PunchReach + enemyRadius) return false;
            float side = (to - d * along).Length();
            return side <= enemyRadius + PunchWidth;
        }

        public static (float cooldown, float cost) Cost(string id) => id switch
        {
            Slam => (SlamCooldown, SlamCost),
            Punch => (PunchCooldown, PunchCost),
            _ => (PalmCooldown, PalmCost),
        };
        public const float PalmCooldown = 1.2f, PalmCost = 15f, PalmDamage = 20f;
        // Gestos
        public const float SlamMinDownSpeed = 3f, SlamMaxHandHeight = 1.05f, PalmMinSpeed = 3.2f, PoseHoldSeconds = 0.12f;
        // O raio sai da palma aberta PARADA (a mesma pose do corte, que é a palma aberta RÁPIDA).
        public const float BeamPalmMaxSpeed = 1.2f, BeamPalmHoldSeconds = 0.3f, BeamLockoutAfterPalm = 0.5f;
    }

    public enum GuardResult { None, Parry, Block, GuardBroken, Counter, ShieldBlock }

    // O que acontece quando o ataque de um inimigo encontra a arma do jogador.
    public static class Guard
    {
        public const float BlockStaminaCost = 20f;
        public const float BlockChipFraction = 0.25f;
        public const float ParryPosture = 55f, BlockPosture = 12f, ParryStaminaRefund = 10f;

        public static GuardResult Resolve(float weaponTipSpeed, bool counterStance, float playerStamina)
        {
            if (counterStance) return GuardResult.Counter;
            if (weaponTipSpeed > Combat.ParrySpeed) return GuardResult.Parry;
            return playerStamina >= BlockStaminaCost ? GuardResult.Block : GuardResult.GuardBroken;
        }

        // Escudo (Lâmina): bloqueia sem tomar dano e gasta menos estamina; empurrar o escudo contra o golpe = aparo.
        public const float ShieldBlockStaminaCost = 12f, ShieldBlockPosture = 18f, ShieldParrySpeed = 1.8f;

        public static GuardResult ResolveShield(float shieldSpeed, bool counterStance, float playerStamina)
        {
            if (counterStance) return GuardResult.Counter;
            if (shieldSpeed > ShieldParrySpeed) return GuardResult.Parry;
            return playerStamina >= ShieldBlockStaminaCost ? GuardResult.ShieldBlock : GuardResult.GuardBroken;
        }

        // Dano que o jogador recebe dado o resultado.
        public static float DamageToPlayer(GuardResult r, float attackDamage) => r switch
        {
            GuardResult.Block => (float)Math.Round(attackDamage * BlockChipFraction),
            GuardResult.ShieldBlock => 0f,
            GuardResult.GuardBroken => attackDamage,
            GuardResult.None => attackDamage,
            _ => 0f,
        };
    }
}
