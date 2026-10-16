using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    // Efeitos simples em código: números de dano, faíscas e anéis no chão.
    public sealed class ArenaFx : MonoBehaviour
    {
        static ArenaFx inst;
        static ArenaFx I
        {
            get
            {
                if (inst == null) inst = new GameObject("ArenaFx").AddComponent<ArenaFx>();
                return inst;
            }
        }

        struct Floater { public TextMesh t; public float life, max; public Vector3 vel; }
        struct Spark { public Transform t; public Vector3 vel; public float life; }
        struct RingFx { public Transform t; public float life, max, radius; public Material m; }

        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<RingFx> rings = new List<RingFx>();
        Font font;

        void Awake() { inst = this; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }

        public static TextMesh MakeText(Transform parent, string text, float charSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = I.font;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = I.font.material;
            mr.sortingOrder = 10;   // texto sempre por cima do fundo dos painéis
            tm.fontSize = 64; tm.characterSize = charSize; tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.color = color; tm.text = text; tm.richText = true;
            return tm;
        }

        public static void FloatText(string text, Vector3 pos, Color color, float size = 1f)
        {
            var tm = MakeText(I.transform, text, 0.012f * size, color);
            tm.transform.position = pos;
            I.floaters.Add(new Floater { t = tm, life = 0.9f, max = 0.9f, vel = new Vector3(0, 0.8f, 0) });
        }

        static Mesh sparkMesh; static readonly Dictionary<Color, Material> sparkMats = new Dictionary<Color, Material>();

        public static void Burst(Vector3 pos, Color color, int n = 10, float speed = 3f, float size = 1f)
        {
            if (!sparkMats.TryGetValue(color, out var mat)) sparkMats[color] = mat = ArenaUtil.Unlit(color);
            for (int i = 0; i < n; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.transform.SetParent(I.transform, false);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.05f * size;
                var v = Random.onUnitSphere * speed * Random.Range(0.4f, 1f); v.y = Mathf.Abs(v.y) + 1f;
                I.sparks.Add(new Spark { t = go.transform, vel = v, life = Random.Range(0.3f, 0.6f) });
            }
        }

        public static void Ring(Vector3 pos, Color color, float radius, float duration = 0.35f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            var m = ArenaUtil.Unlit(color);
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.transform.SetParent(I.transform, false);
            go.transform.position = new Vector3(pos.x, 0.03f, pos.z);
            go.transform.localScale = new Vector3(0.2f, 0.005f, 0.2f);
            I.rings.Add(new RingFx { t = go.transform, life = duration, max = duration, radius = radius, m = m });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i]; f.life -= dt;
                if (f.life <= 0f || f.t == null) { if (f.t) Destroy(f.t.gameObject); floaters.RemoveAt(i); continue; }
                f.t.transform.position += f.vel * dt;
                if (cam) f.t.transform.rotation = Quaternion.LookRotation(f.t.transform.position - cam.transform.position);
                var c = f.t.color; c.a = Mathf.Clamp01(f.life / f.max * 2f); f.t.color = c;
                floaters[i] = f;
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i]; s.life -= dt;
                if (s.life <= 0f || s.t == null) { if (s.t) Destroy(s.t.gameObject); sparks.RemoveAt(i); continue; }
                s.vel.y -= 9f * dt; s.t.position += s.vel * dt;
                sparks[i] = s;
            }
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i]; r.life -= dt;
                if (r.life <= 0f || r.t == null) { if (r.t) Destroy(r.t.gameObject); rings.RemoveAt(i); continue; }
                float k = 1f - r.life / r.max;
                float d = Mathf.Lerp(0.2f, r.radius * 2f, k);
                r.t.localScale = new Vector3(d, 0.005f, d);
                var c = r.m.color; c.a = 1f - k; r.m.color = c;
                rings[i] = r;
            }
        }
    }
}
