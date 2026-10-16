using UnityEngine;
using N = System.Numerics;

namespace Arena
{
    public static class ArenaUtil
    {
        public static N.Vector3 ToN(this Vector3 v) => new N.Vector3(v.x, v.y, v.z);
        public static Vector3 ToU(this N.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; }

        static Shader litShader, unlitShader;

        // Materiais simples criados em código (pipeline built-in).
        public static Material Lit(Color c, Color? emission = null)
        {
            if (litShader == null) litShader = Shader.Find("Standard");
            var m = new Material(litShader) { color = c };
            m.SetFloat("_Glossiness", 0.15f);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            return m;
        }

        public static Material Unlit(Color c)
        {
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            return new Material(unlitShader) { color = c };
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // Área onde se anda: null = a arena quadrada; na dungeon, salas + corredores do andar.
        public static Arena.Core.WalkArea Area;

        public static Vector3 ClampToArena(Vector3 p)
        {
            if (Area != null) { var (x, z) = Area.Clamp(p.x, p.z); p.x = x; p.z = z; return p; }
            float h = Arena.Core.Combat.ArenaHalfSize;
            p.x = Mathf.Clamp(p.x, -h, h); p.z = Mathf.Clamp(p.z, -h, h);
            return p;
        }

        // Fora da área (com folga): bateu na parede.
        public static bool IsOutside(Vector3 p, float margin = 0f)
        {
            if (Area != null) return !Area.Contains(p.x, p.z, -margin);
            float h = Arena.Core.Combat.ArenaHalfSize + margin;
            return Mathf.Abs(p.x) > h || Mathf.Abs(p.z) > h;
        }

        // Até onde se anda em linha reta (no chão) a partir de start, sem atravessar paredes.
        public static float RayReach(Vector3 start, Vector3 dirFlat, float maxReach)
        {
            if (Area != null) return Area.RayReach(start.x, start.z, dirFlat.x, dirFlat.z, maxReach);
            float h = Arena.Core.Combat.ArenaHalfSize, reach = maxReach;
            if (Mathf.Abs(dirFlat.x) > 1e-4f) reach = Mathf.Min(reach, ((dirFlat.x > 0 ? h : -h) - start.x) / dirFlat.x);
            if (Mathf.Abs(dirFlat.z) > 1e-4f) reach = Mathf.Min(reach, ((dirFlat.z > 0 ? h : -h) - start.z) / dirFlat.z);
            return Mathf.Max(0f, reach);
        }
    }
}
