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

        public static Vector3 ClampToArena(Vector3 p)
        {
            float h = Arena.Core.Combat.ArenaHalfSize;
            p.x = Mathf.Clamp(p.x, -h, h); p.z = Mathf.Clamp(p.z, -h, h);
            return p;
        }
    }
}
