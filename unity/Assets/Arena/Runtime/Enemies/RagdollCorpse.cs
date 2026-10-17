using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Fase 2: ao morrer, o modelo do inimigo (primitivas) vira um ragdoll. Cada parte com malha ganha um corpo físico,
    // ligado à parte maior mais próxima por uma CharacterJoint (cabeça no corpo, orelhas na cabeça, clava no braço...).
    // Recebe o impulso do golpe final, pode ser empurrado pela arma e some depois de alguns segundos.
    public sealed class RagdollCorpse : MonoBehaviour
    {
        static readonly Queue<RagdollCorpse> alive = new Queue<RagdollCorpse>();

        float t;
        readonly List<Transform> parts = new List<Transform>();
        readonly List<Vector3> scales = new List<Vector3>();

        // Preso na espada: não some enquanto estiver pendurado.
        public bool Pinned;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        public float TotalMass { get { float m = 0f; foreach (var b in bodies) if (b) m += b.mass; return m; } }
        public void ScaleMass(float k) { foreach (var b in bodies) if (b) b.mass *= k; }
        public Rigidbody NearestBody(Vector3 p)
        {
            Rigidbody best = null; float bd = float.MaxValue;
            foreach (var b in bodies)
            {
                if (!b) continue;
                var col = b.GetComponent<Collider>();
                float d = col ? Vector3.Distance(col.ClosestPoint(p), p) : Vector3.Distance(b.position, p);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public static RagdollCorpse Create(Transform model, Vector3 hitDir, float hitSpeed, bool impulse = true)
        {
            // Limite de corpos para o Quest: o mais antigo some.
            while (alive.Count >= ImpactReaction.MaxCorpses)
            {
                var old = alive.Dequeue();
                if (old) Destroy(old.gameObject);
            }
            var corpse = model.gameObject.AddComponent<RagdollCorpse>();
            corpse.Build(hitDir, hitSpeed, impulse);
            alive.Enqueue(corpse);
            return corpse;
        }

        void Build(Vector3 hitDir, float hitSpeed, bool impulse)
        {
            // Textos (TextMesh) não viram partes.
            foreach (var tm in GetComponentsInChildren<TextMesh>()) Destroy(tm.gameObject);

            var filters = GetComponentsInChildren<MeshFilter>();
            var infos = new List<(Transform t, float vol)>();
            foreach (var f in filters)
            {
                if (f.GetComponent<TextMesh>()) continue;
                var b = f.sharedMesh.bounds;
                var s = f.transform.lossyScale;
                float vol = Mathf.Abs(b.size.x * s.x * b.size.y * s.y * b.size.z * s.z);
                infos.Add((f.transform, Mathf.Max(vol, 1e-5f)));
            }
            if (infos.Count == 0) { Destroy(gameObject, 0.1f); return; }

            // Solta todas as partes no mundo (mantendo a pose) antes de criar os corpos.
            foreach (var (pt, _) in infos) pt.SetParent(transform, true);

            infos.Sort((a, b) => b.vol.CompareTo(a.vol));
            var bodies = new Dictionary<Transform, Rigidbody>();
            foreach (var (pt, vol) in infos)
            {
                var col = pt.gameObject.AddComponent<BoxCollider>();   // ajusta sozinho à malha
                var rb = pt.gameObject.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(ImpactReaction.MinPartMass, vol * ImpactReaction.PartDensity);
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bodies[pt] = rb;
                this.bodies.Add(rb);
                parts.Add(pt); scales.Add(pt.localScale);
            }
            // Cada parte (menos a maior) se liga à parte maior mais próxima.
            for (int i = 1; i < infos.Count; i++)
            {
                var me = infos[i].t;
                Transform best = infos[0].t; float bestD = float.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    float d = Vector3.Distance(me.position, infos[j].t.position);
                    if (d < bestD) { bestD = d; best = infos[j].t; }
                }
                var cj = me.gameObject.AddComponent<CharacterJoint>();
                cj.connectedBody = bodies[best];
                cj.enableProjection = true;
                cj.lowTwistLimit = new SoftJointLimit { limit = -30f };
                cj.highTwistLimit = new SoftJointLimit { limit = 30f };
                cj.swing1Limit = new SoftJointLimit { limit = 40f };
                cj.swing2Limit = new SoftJointLimit { limit = 40f };
                // Partes que se encostam no próprio corpo não colidem entre si (evita tremedeira).
                Physics.IgnoreCollision(me.GetComponent<Collider>(), best.GetComponent<Collider>());
            }
            // Impulso do golpe final: na direção do golpe e um pouco para cima.
            if (!impulse) return;
            var v = hitDir.normalized * (ImpactReaction.DeathImpulseBase + hitSpeed) + Vector3.up * ImpactReaction.DeathImpulseUp;
            foreach (var rb in bodies.Values)
            {
                rb.linearVelocity = v;
                rb.angularVelocity = Random.insideUnitSphere * 3f;
            }
        }

        void Update()
        {
            if (!Pinned) t += Time.deltaTime;
            float fadeStart = ImpactReaction.CorpseSeconds - ImpactReaction.CorpseFadeSeconds;
            if (t > fadeStart)
            {
                float k = Mathf.Clamp01(1f - (t - fadeStart) / ImpactReaction.CorpseFadeSeconds);
                for (int i = 0; i < parts.Count; i++) if (parts[i]) parts[i].localScale = scales[i] * k;
            }
            if (t > ImpactReaction.CorpseSeconds) Destroy(gameObject);
        }
    }
}
