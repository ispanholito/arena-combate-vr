using System;
using System.Collections.Generic;
using System.Numerics;

namespace Arena.Core
{
    // MURALHA IMPENETRÁVEL (ideia do Pablo, 2026-10-28): mãos livres, bater o PUNHO de uma mão na PALMA aberta da outra
    // (qualquer uma) → aura vermelha por 20 s. Nela só sai SOCO, muito mais forte: inimigos pequenos voam, os grandes
    // atordoam bem mais rápido (postura ×3); as mãos defendem (bloqueio) e aparam (mão indo rápido contra o golpe);
    // PALMA batida = onda para frente; punho na palma de novo = onda em volta. Tudo dá dano.
    public static class MuralhaRules
    {
        public const float FistPalmDistance = 0.12f;   // punho a até 12 cm da palma aberta
        public const float FistPalmSpeed = 0.8f;       // m/s com que se aproximam
        public const float Seconds = 20f, Mana = 30f, Cooldown = 15f;

        public static bool IsFistPalm(bool aFist, bool aOpen, bool bFist, bool bOpen, float distance, float closingSpeed) =>
            ((aFist && bOpen) || (bFist && aOpen)) && distance <= FistPalmDistance && closingSpeed >= FistPalmSpeed;

        // Soco na Muralha.
        public const float PunchDamageMultiplier = 2.5f, PunchPostureMultiplier = 3f;
        public const float LaunchSpeed = 9f, LaunchUp = 3f;   // pequenos voam
        public static bool IsBig(EnemyKind k) => k == EnemyKind.Orc || EnemyDef.IsBoss(k) || k == EnemyKind.Mimico;

        // Defesa com as mãos: golpe chegando perto de uma mão = bloqueio; mão indo rápido contra ele = aparo.
        public const float GuardRadius = 0.4f, ParryHandSpeed = 2f, GuardStamina = 6f, ParryStun = 1.1f;
        public enum Guard { None, Block, Parry }
        public static Guard ResolveGuard(float handToHit, float handSpeedTowardHit) =>
            handToHit > GuardRadius ? Guard.None : handSpeedTowardHit >= ParryHandSpeed ? Guard.Parry : Guard.Block;

        // Ondas de choque.
        public const float WaveCooldown = 1.2f;
        public const float ClapRange = 5f, ClapConeDeg = 70f, ClapDamage = 25f, ClapKnock = 8f;   // palma: cone para frente
        public const float RingRadius = 4f, RingDamage = 20f, RingKnock = 7f;                      // punho na palma: em volta

        public static bool InCone(Vector2 toTarget, Vector2 forward, float coneDeg, float range)
        {
            float d = toTarget.Length();
            if (d > range || d < 1e-4f) return d < 1e-4f;
            float cos = Vector2.Dot(toTarget / d, Vector2.Normalize(forward));
            return cos >= MathF.Cos(coneDeg * 0.5f * MathF.PI / 180f);
        }
    }

    // RAIO DO CÉU (ideia do Pablo, 2026-10-28): olhando para cima, erga a mão acima da cabeça FECHADA e abra → um raio
    // desce e fica na mão; aponte a mão para frente → descarga num inimigo que pula para os próximos por perto.
    public static class SkyBoltRules
    {
        public const float LookUpMinDeg = 30f;         // cabeça olhando para cima
        public const float HandAboveHead = 0.12f;      // mão acima da cabeça
        public const float HeldSeconds = 6f;           // o raio espera na mão este tempo
        public const float Mana = 30f, Cooldown = 8f;
        public const float AimMinReach = 0.35f, AimMaxAngle = 30f, AimHold = 0.1f;   // braço esticado para frente
        public const float FirstConeDeg = 25f, Range = 25f;
        public const int MaxJumps = 4;
        public const float ChainRadius = 4f;
        public const float Damage = 35f, HopFalloff = 0.75f, Charge = 60f;   // carga elétrica: 2 descargas eletrocutam

        public static float HopDamage(int hop) => Damage * MathF.Pow(HopFalloff, hop);

        // Ordem dos atingidos: começa no primeiro e sempre pula para o mais perto ainda não atingido (até ChainRadius).
        public static List<int> ChainTargets(IReadOnlyList<Vector3> positions, int first)
        {
            var order = new List<int>();
            if (first < 0 || first >= positions.Count) return order;
            var used = new bool[positions.Count];
            int cur = first;
            order.Add(cur); used[cur] = true;
            while (order.Count <= MaxJumps)
            {
                int next = -1; float best = ChainRadius;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (used[i]) continue;
                    float d = Vector3.Distance(positions[cur], positions[i]);
                    if (d <= best) { best = d; next = i; }
                }
                if (next < 0) break;
                order.Add(next); used[next] = true; cur = next;
            }
            return order;
        }
    }
}
