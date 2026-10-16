using System;
using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Projétil genérico: bola de fogo, lança de gelo, corte de vento.
    public sealed class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Gravity, Life = 2f, Radius = 0.1f;
        public int Pierce = 1;
        public Action<ArenaEnemy, Projectile> OnHitEnemy;
        public Action<Projectile> OnEnd;

        readonly HashSet<ArenaEnemy> hit = new HashSet<ArenaEnemy>();
        float t;
        bool ended;

        public static Projectile Spawn(GameObject visual, Vector3 pos, Vector3 velocity)
        {
            visual.transform.position = pos;
            if (velocity.sqrMagnitude > 1e-4f) visual.transform.rotation = Quaternion.LookRotation(velocity);
            var p = visual.AddComponent<Projectile>();
            p.Velocity = velocity;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var prev = transform.position;
            Velocity.y -= Gravity * dt;
            transform.position += Velocity * dt;
            if (Velocity.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(Velocity);
            bool done = t > Life;
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (e == null || e.Dead || hit.Contains(e)) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.SegmentSegmentDistance(prev.ToN(), transform.position.ToN(), a.ToN(), b.ToN()) < e.Radius + Radius)
                {
                    hit.Add(e);
                    OnHitEnemy?.Invoke(e, this);
                    if (hit.Count >= Pierce) { done = true; break; }
                }
            }
            var pos = transform.position;
            bool outside = pos.y < 0.05f || ArenaUtil.IsOutside(pos, 0.3f);
            if (outside || done)
            {
                if (!ended) { ended = true; OnEnd?.Invoke(this); }
                Destroy(gameObject);
            }
        }
    }
}
