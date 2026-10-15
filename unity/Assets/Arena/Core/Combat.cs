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

        public static float Knockback(float tipSpeed, WeaponDef weapon) => Math.Min(6f, tipSpeed) * 0.4f * weapon.DamageMultiplier;

        // Bola de Fogo: carga 0..1, cai 40% na borda, QUEIMA (+60%) em enraizados.
        public static float FireballDamage(float charge) => 22f + 18f * Math.Clamp(charge, 0f, 1f);
        public static float FireballRadius(float charge, bool combustao) => (1.4f + 0.6f * Math.Clamp(charge, 0f, 1f)) * (combustao ? 1.6f : 1f);
        public static float ExplosionFalloff(float distance, float radius) => 1f - 0.4f * Math.Min(1f, distance / Math.Max(0.01f, radius));
        public const float BurnMultiplier = 1.6f;

        public const float IceDamage = 18f, IcePosture = 0.9f, IceSlowSeconds = 2.5f, IceFreezeSeconds = 1.5f;
        public const int IcePierce = 3;
        public const float RootSeconds = 3f, RootRadius = 2.2f;
        public const float WindSlashDamage = 24f;
        public const float CounterDamage = 40f;

        public static float DashCost(bool passoLeve) => passoLeve ? 15f : 25f;
        public static float ChargeLength(bool cadeia) => cadeia ? 6.3f : 4.5f;
        public const float ChargeDamage = 30f;
    }

    public enum GuardResult { None, Parry, Block, GuardBroken, Counter }

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

        // Dano que o jogador recebe dado o resultado.
        public static float DamageToPlayer(GuardResult r, float attackDamage) => r switch
        {
            GuardResult.Block => (float)Math.Round(attackDamage * BlockChipFraction),
            GuardResult.GuardBroken => attackDamage,
            GuardResult.None => attackDamage,
            _ => 0f,
        };
    }
}
