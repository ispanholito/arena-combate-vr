using System;
using System.Collections.Generic;
using System.Numerics;

namespace Arena.Core
{
    // Movimento dos inimigos em grupo (2026-10-28, "melhorar a IA"). Tudo no plano do chão (x, z):
    //   - VAGAS: quem está rondando (sem ficha de ataque) se espalha num arco NA FRENTE do jogador (no VR ninguém vê as
    //     costas; apanhar de quem não se vê é frustrante). A ordem atual em volta do jogador é mantida (ninguém cruza).
    //   - SEPARAÇÃO: inimigos a menos de 1 m se empurram (antes ficavam empilhados em cima do jogador).
    //   - DESVIO: pilares no caminho são contornados pelo lado mais curto (antes o corpo físico batia, era puxado e caía).
    //   - AMEAÇAS: projétil vindo na direção do inimigo → às vezes ele dá um passo para o lado (chance pela dificuldade);
    //     finta: às vezes ergue a arma e desiste (só normal/difícil).
    public static class CombatSteering
    {
        public const float SlotSpacingDeg = 45f, MaxArcDeg = 240f;
        public const float SlotRadius = 2.1f;
        public const float SeparationRadius = 1.0f, SeparationGain = 2.4f;
        public const float AvoidLookAhead = 1.8f, AvoidMargin = 0.4f, AvoidGain = 2.5f;
        public const float MaxSpeed = 2.2f;

        // Ângulos (graus, relativos à frente do jogador, -180..180) de quem está rondando → ângulo-alvo de cada um.
        // Mantém a ordem: o mais à esquerda fica com a vaga mais à esquerda.
        public static float[] AssignSlots(float[] anglesDeg)
        {
            int n = anglesDeg.Length;
            var result = new float[n];
            if (n == 0) return result;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => anglesDeg[a].CompareTo(anglesDeg[b]));
            float span = Math.Min(MaxArcDeg, SlotSpacingDeg * (n - 1));
            for (int k = 0; k < n; k++) result[order[k]] = n == 1 ? Clamp(anglesDeg[order[k]], -MaxArcDeg * 0.5f, MaxArcDeg * 0.5f) : -span * 0.5f + span * k / (n - 1);
            return result;
        }

        // Empurrão para longe dos vizinhos (mais forte quanto mais perto).
        public static Vector2 Separation(Vector2 self, IReadOnlyList<Vector2> others)
        {
            var push = Vector2.Zero;
            foreach (var o in others)
            {
                var d = self - o;
                float len = d.Length();
                if (len >= SeparationRadius) continue;
                var dir = len > 1e-3f ? d / len : new Vector2(1f, 0f);
                push += dir * (1f - len / SeparationRadius);
            }
            return push * SeparationGain;
        }

        // Desvio de obstáculos redondos (pilares) que estão no caminho da velocidade desejada.
        public static Vector2 Avoid(Vector2 pos, Vector2 desired, IReadOnlyList<(Vector2 c, float r)> circles, float bodyRadius)
        {
            var steer = Vector2.Zero;
            float speed = desired.Length();
            var fwd = speed > 1e-3f ? desired / speed : Vector2.Zero;
            foreach (var (c, r) in circles)
            {
                var to = c - pos;
                float clear = r + bodyRadius + AvoidMargin;
                float dist = to.Length();
                if (dist < clear)   // já encostado: sai para fora
                {
                    steer += (dist > 1e-3f ? -to / dist : new Vector2(1f, 0f)) * (1f - dist / clear) * AvoidGain;
                    continue;
                }
                if (speed < 1e-3f) continue;
                float along = Vector2.Dot(to, fwd);
                if (along <= 0f || along > AvoidLookAhead + clear) continue;
                var lateralVec = to - fwd * along;   // do caminho até o centro do pilar
                float lateral = lateralVec.Length();
                if (lateral >= clear) continue;
                // Vira para o lado oposto ao centro (pilar à esquerda → vai para a direita).
                var side = lateral > 1e-3f ? -lateralVec / lateral : new Vector2(-fwd.Y, fwd.X);
                steer += side * (1f - along / (AvoidLookAhead + clear)) * AvoidGain * Math.Max(1f, speed);
            }
            return steer;
        }

        public static Vector2 ClampSpeed(Vector2 v, float max)
        {
            float l = v.Length();
            return l > max ? v / l * max : v;
        }

        // Projétil (posição p, velocidade v) vai passar perto do inimigo (posição e) logo? Devolve o lado para fugir.
        public const float ThreatRadius = 0.9f, ThreatSeconds = 0.7f;
        public static bool ProjectileThreat(Vector2 e, Vector2 p, Vector2 v, out Vector2 dodgeDir)
        {
            dodgeDir = Vector2.Zero;
            float v2 = v.LengthSquared();
            if (v2 < 1f) return false;
            var rel = e - p;
            float t = Vector2.Dot(rel, v) / v2;
            if (t <= 0f || t > ThreatSeconds) return false;
            var miss = rel - v * t;   // onde ele estará em relação à linha do tiro
            if (miss.Length() > ThreatRadius) return false;
            var perp = new Vector2(-v.Y, v.X) / MathF.Sqrt(v2);
            dodgeDir = Vector2.Dot(miss, perp) >= 0f ? perp : -perp;   // para o lado em que já está
            return true;
        }

        // Esquiva na velocidade de ANDAR (Pablo, 2026-10-30: "a velocidade que ganham pra desviar é surreal"; era 3,6 m/s
        // por 0,35 s): um passo de ~1 m, acelerando e freando como o andar.
        public const float DodgeSeconds = 0.65f, DodgeSpeed = 1.5f;
        // Tempo de REAÇÃO humano antes de esquivar ou erguer a guarda (Pablo: "a reação é quase imediata"). Gente reage a
        // um estímulo visual em ~0,25 s e ainda leva um tempo para decidir; antes disso o inimigo não faz nada.
        public static float ReactionDelay(Difficulty d, double roll)
        {
            var (a, b) = d switch { Difficulty.Facil => (0.45f, 0.7f), Difficulty.Dificil => (0.26f, 0.4f), _ => (0.33f, 0.52f) };
            return a + (b - a) * Clamp((float)roll, 0f, 1f);
        }
        public static float DodgeChance(Difficulty d) => d switch { Difficulty.Facil => 0.15f, Difficulty.Dificil => 0.6f, _ => 0.35f };
        public static float FeintChance(Difficulty d) => d switch { Difficulty.Facil => 0f, Difficulty.Dificil => 0.2f, _ => 0.12f };
        public const float FeintAt = 0.55f;   // desiste no meio do aviso

        static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;
    }
}
