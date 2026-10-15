using System;
using System.Numerics;

namespace Arena.Core
{
    // Distâncias entre segmentos: lâmina (cabo→ponta) contra o eixo do corpo dos inimigos.
    public static class Geometry
    {
        public static float PointSegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float t = Math.Clamp(Vector3.Dot(p - a, ab) / Math.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
            return Vector3.Distance(a + ab * t, p);
        }

        public static float SegmentSegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) return Vector3.Distance(p1, p2);
            if (a <= 1e-6f) { s = 0f; t = Math.Clamp(f / e, 0f, 1f); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                else
                {
                    float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                    s = den > 1e-6f ? Math.Clamp((b * f - c * e) / den, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
                }
            }
            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }

        // A lâmina varreu o corpo entre dois frames? Testa sub-passos interpolados (golpes rápidos não "atravessam").
        public static bool SweptBladeHits(Vector3 basePrev, Vector3 tipPrev, Vector3 baseNow, Vector3 tipNow,
                                          Vector3 bodyA, Vector3 bodyB, float radius, int steps = 5)
        {
            for (int k = 1; k <= steps; k++)
            {
                float f = k / (float)steps;
                var b = Vector3.Lerp(basePrev, baseNow, f);
                var t = Vector3.Lerp(tipPrev, tipNow, f);
                if (SegmentSegmentDistance(b, t, bodyA, bodyB) < radius) return true;
            }
            return false;
        }

        // 1 = o golpe corta com o fio (movimento perpendicular à lâmina) ou é estocada; 0 = desliza ao longo dela.
        public static float EdgeAlignment(Vector3 bladeDir, Vector3 tipVelocity)
        {
            float speed = tipVelocity.Length();
            if (speed < 1e-4f || bladeDir.LengthSquared() < 1e-6f) return 0f;
            float along = Math.Abs(Vector3.Dot(Vector3.Normalize(bladeDir), tipVelocity / speed));
            // estocada (along≈1 indo para frente) também conta como limpa
            float thrust = Vector3.Dot(Vector3.Normalize(bladeDir), tipVelocity / speed);
            return thrust > 0.85f ? 1f : 1f - along;
        }
    }
}
