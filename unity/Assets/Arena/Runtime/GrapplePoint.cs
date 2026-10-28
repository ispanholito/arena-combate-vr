using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    // Ponto de gancho para a teia (no alto das pilastras, das paredes e no "teto" das salas). Acende quando o jogador
    // olha para ele (SpiderLocomotion escolhe o mais perto do centro da visão).
    public sealed class GrapplePoint : MonoBehaviour
    {
        public static readonly List<GrapplePoint> All = new List<GrapplePoint>();

        Transform core, halo;
        Material coreMat, haloMat;
        bool lit;
        float pulse;

        static readonly Color Idle = new Color(0.55f, 0.6f, 0.75f), Lit = new Color(0.4f, 1f, 1f);

        public static GrapplePoint Spawn(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Gancho");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var g = go.AddComponent<GrapplePoint>();
            g.coreMat = ArenaUtil.Unlit(Idle);
            g.haloMat = ArenaUtil.Unlit(new Color(Idle.r, Idle.g, Idle.b, 0.25f));
            g.core = ArenaUtil.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.16f, g.coreMat).transform;
            g.halo = ArenaUtil.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.34f, g.haloMat).transform;
            return g;
        }

        public bool Usable => isActiveAndEnabled;
        public Vector3 Position => transform.position;

        public void SetLit(bool value)
        {
            if (lit == value) return;
            lit = value;
            coreMat.color = lit ? Lit : Idle;
            haloMat.color = lit ? new Color(Lit.r, Lit.g, Lit.b, 0.45f) : new Color(Idle.r, Idle.g, Idle.b, 0.25f);
        }

        void Update()
        {
            pulse += Time.deltaTime * (lit ? 8f : 2f);
            float s = lit ? 1.35f + 0.15f * Mathf.Sin(pulse) : 1f + 0.05f * Mathf.Sin(pulse);
            if (halo) halo.localScale = Vector3.one * 0.34f * s;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void OnDestroy() { All.Remove(this); }
    }
}
