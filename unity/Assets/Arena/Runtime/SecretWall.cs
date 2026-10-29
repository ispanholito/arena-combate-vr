using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    // Parede rachada que esconde a sala secreta. Diferente das outras: rachaduras que brilham de leve e soltam poeira
    // de vez em quando. Golpe forte da arma (≥ 3 m/s) ou explosão perto = desaba.
    public sealed class SecretWall : MonoBehaviour
    {
        public static readonly List<SecretWall> All = new List<SecretWall>();
        const float BreakSpeed = 3f;
        System.Action onOpen;
        bool open;
        float dustT;
        Vector3 center;

        public static SecretWall Spawn(Vector3 a, Vector3 b, Transform parent, System.Action onOpen)
        {
            var go = new GameObject("ParedeRachada");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<SecretWall>();
            w.onOpen = onOpen;
            w.center = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(a.z - b.z) < 0.01f;
            float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            go.transform.position = w.center;
            go.transform.rotation = Quaternion.LookRotation(alongX ? Vector3.forward : Vector3.right);   // x local = ao longo da porta
            var stone = ArenaUtil.Lit(new Color(0.3f, 0.27f, 0.3f));
            var block = ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 2f, 0f), new Vector3(len + 0.3f, 4f, 0.35f), stone, keepCollider: true);
            block.transform.localRotation = Quaternion.identity;
            // Rachaduras que brilham (dica para o jogador).
            var glow = ArenaUtil.Lit(new Color(0.9f, 0.5f, 0.2f), new Color(0.9f, 0.4f, 0.1f));
            var rng = new System.Random(len.GetHashCode());
            for (int i = 0; i < 7; i++)
            {
                var crack = ArenaUtil.Prim(PrimitiveType.Cube, go.transform,
                                           new Vector3((float)(rng.NextDouble() - 0.5) * len * 0.8f, 0.6f + (float)rng.NextDouble() * 2.4f, 0f),
                                           new Vector3(0.05f, 0.4f + (float)rng.NextDouble() * 0.5f, 0.38f), glow).transform;
                crack.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 80f);
            }
            All.Add(w);
            return w;
        }

        void OnCollisionEnter(Collision c)
        {
            if (open || !c.rigidbody) return;
            var pl = ArenaGame.Instance ? ArenaGame.Instance.Player : null;
            if (pl && pl.Weapon && c.rigidbody == pl.Weapon.Body && c.relativeVelocity.magnitude >= BreakSpeed) Open();
        }

        void Update()
        {
            if (open) return;
            dustT -= Time.deltaTime;
            if (dustT <= 0f) { dustT = Random.Range(1.5f, 3f); ArenaFx.Burst(center + Vector3.up * Random.Range(0.5f, 3f), new Color(0.6f, 0.55f, 0.5f), 3, 0.5f); }
        }

        public void Open()
        {
            if (open) return;
            open = true;
            ArenaAudio.Play(Sfx.Slam, center + Vector3.up, 1f, 0.6f);
            ArenaInput.Haptic(1f, 0.25f);
            for (int i = 0; i < 12; i++)
            {
                var p = ArenaUtil.Prim(PrimitiveType.Cube, null, center + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.3f, 3.5f), 0f),
                                       Vector3.one * Random.Range(0.2f, 0.45f), ArenaUtil.Lit(new Color(0.3f, 0.27f, 0.3f)), keepCollider: true);
                var rb = p.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.insideUnitSphere * 2f;
                p.layer = 2;
                Destroy(p, 4f);
            }
            ArenaFx.Burst(center + Vector3.up * 1.5f, new Color(0.6f, 0.55f, 0.5f), 30, 3f, 2f);
            onOpen?.Invoke();
            Destroy(gameObject);
        }

        public static void OnExplosion(Vector3 pos, float radius)
        {
            foreach (var w in All.ToArray())
                if (w && !w.open && Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(w.center.x, 0f, w.center.z)) < radius + 1f) w.Open();
        }

        void OnDestroy() => All.Remove(this);
    }
}
