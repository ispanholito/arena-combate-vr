using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Espada flamejante (ideia do Pablo, 2026-10-28): mergulhe a lâmina numa bola de fogo — a que está carregando na
    // mão livre (ela é consumida e gasta a mana dela) ou uma voando — e a arma pega fogo por 12 s: +30% de dano e cada
    // golpe põe calor no inimigo (FlameBladeRules). Chamas e faíscas ao longo da lâmina + uma luz pequena.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class FlameBlade : MonoBehaviour
    {
        public static FlameBlade Instance { get; private set; }

        ArenaPlayer player;
        float flameT, sparkT;
        readonly List<Transform> flames = new List<Transform>();
        Light glow;
        Material flameMat;
        const int FlameCount = 6;

        // A arma da mão está em chamas?
        public static bool Burning => Instance && Instance.flameT > 0f;
        public static float DamageMultiplier => Burning ? FlameBladeRules.DamageMultiplier : 1f;

        // Chamado nos acertos da arma: põe calor no inimigo atingido.
        public static void OnHit(EnemyState target)
        {
            if (Burning && target != null && !target.Dead) target.Status.AddHeat(FlameBladeRules.HeatPerHit);
        }

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; Clear(); }

        void Update()
        {
            var w = player.Weapon;
            bool usable = w && w.Def != null && w.Held && FlameBladeRules.CanIgnite(w.Def.Kind);
            if (!usable) { if (flameT > 0f) { flameT = 0f; Clear(); } return; }
            if (flameT > 0f) flameT -= Time.deltaTime;
            TryDip(w);
            if (flameT > 0f) Draw(w); else if (flames.Count > 0) { Clear(); ArenaAudio.Play(Sfx.Wind, w.TipPoint, 0.4f, 0.7f); }
        }

        // Lâmina passando por uma bola de fogo (na mão ou voando).
        void TryDip(PlayerWeapon w)
        {
            var a = w.BasePoint; var b = w.TipPoint;
            var hm = HandMagic.Instance;
            if (hm && hm.TryConsumeFireOrb(a, b, FlameBladeRules.DipMargin)) { Ignite(w); return; }
            foreach (var p in Projectile.All)
            {
                if (!p || !p.IsFire) continue;
                if (DistToSegment(p.transform.position, a, b) < p.Radius + FlameBladeRules.DipMargin) { Ignite(w); return; }
            }
        }

        public void Ignite(PlayerWeapon w)
        {
            bool was = flameT > 0f;
            flameT = FlameBladeRules.Refresh(flameT);
            if (was) return;
            ArenaAudio.Play(Sfx.FireCast, w.TipPoint, 1f, 0.8f);
            ArenaFx.Burst(w.TipPoint, new Color(1f, 0.5f, 0.1f), 20, 3f, 0.8f);
            ArenaInput.Haptic(0.7f, 0.15f);
            ArenaGame.Instance?.Message("Arma em chamas!", 1.2f);
            TutorialEvents.Raise("arma_fogo");
        }

        void Draw(PlayerWeapon w)
        {
            if (flames.Count == 0)
            {
                flameMat = ArenaUtil.Unlit(new Color(1f, 0.55f, 0.12f, 0.75f));
                for (int i = 0; i < FlameCount; i++)
                {
                    var f = ArenaUtil.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.05f, flameMat).transform;
                    foreach (var c in f.GetComponentsInChildren<Collider>()) Destroy(c);
                    flames.Add(f);
                }
                glow = new GameObject("LuzChama").AddComponent<Light>();
                glow.type = LightType.Point; glow.color = new Color(1f, 0.5f, 0.15f); glow.range = 2.5f;
            }
            var a = w.BasePoint; var b = w.TipPoint;
            float t = Time.time;
            for (int i = 0; i < flames.Count; i++)
            {
                float s = (i + 0.5f) / flames.Count;
                float flick = 0.75f + 0.35f * Mathf.Sin(t * 23f + i * 1.7f);
                flames[i].position = Vector3.Lerp(a, b, s) + Vector3.up * 0.03f * flick;
                flames[i].localScale = Vector3.one * (0.055f + 0.03f * (1f - s)) * flick;   // mais forte perto do cabo
            }
            glow.transform.position = Vector3.Lerp(a, b, 0.6f);
            glow.intensity = 1.1f + 0.3f * Mathf.Sin(t * 17f);
            // Últimos 2 s: a chama pisca para avisar que vai apagar.
            bool ending = flameT < 2f;
            foreach (var f in flames) f.gameObject.SetActive(!ending || Mathf.Sin(t * 20f) > -0.3f);
            sparkT -= Time.deltaTime;
            if (sparkT <= 0f)
            {
                sparkT = 0.12f;
                ArenaFx.Burst(Vector3.Lerp(a, b, Random.value), new Color(1f, 0.45f, 0.1f), 3, 1.2f, 0.35f);
            }
        }

        void Clear()
        {
            foreach (var f in flames) if (f) Destroy(f.gameObject);
            flames.Clear();
            if (glow) Destroy(glow.gameObject);
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
