using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Aura vermelha na arma da mão direita (substitui a Postura de Contra; regras em RedAuraRules). Enquanto
    // PlayerState.RedAura > 0: brilho vermelho envolvendo a lâmina, uma luz pequena e brasas; pisca no último segundo.
    // O dano extra e a quebra de defesa são aplicados nos acertos (RagdollGoblin, PlayerWeapon, GoblinEnemy).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class RedAura : MonoBehaviour
    {
        public static RedAura Instance { get; private set; }
        public static bool Active => Instance && Instance.player.State != null && Instance.player.State.AuraActive
                                     && Instance.player.Weapon && Instance.player.Weapon.Held;
        public static float DamageMultiplier => Active ? RedAuraRules.DamageMultiplier : 1f;

        ArenaPlayer player;
        Transform shell;
        Material shellMat;
        Light glow;
        float emberT;
        PlayerWeapon shownOn;
        Transform shownVisual;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (shell) Destroy(shell.gameObject); }

        void Update()
        {
            var w = player.Weapon;
            if (!Active) { if (shell) shell.gameObject.SetActive(false); return; }
            if (!shell || shownVisual != w.Visual) Build(w);
            shell.gameObject.SetActive(true);
            float left = player.State.RedAura;
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 9f);
            if (left < 1f) pulse *= Mathf.Sin(Time.time * 30f) > 0f ? 1f : 0.2f;   // acabando: pisca
            var c = new Color(1f, 0.12f, 0.08f, 0.35f * pulse + 0.1f);
            shellMat.color = c;
            if (glow) glow.intensity = 1.6f * pulse;
            emberT -= Time.deltaTime;
            if (emberT <= 0f)
            {
                emberT = 0.12f;
                ArenaFx.Burst(Vector3.Lerp(w.BasePoint, w.TipPoint, Random.value), new Color(1f, 0.2f, 0.1f), 2, 0.8f, 0.4f);
            }
        }

        // Casca vermelha translúcida ao longo da arma (filha do modelo, acompanha a física).
        void Build(PlayerWeapon w)
        {
            if (shell) Destroy(shell.gameObject);
            shownOn = w; shownVisual = w.Visual;
            shellMat = ArenaUtil.Unlit(new Color(1f, 0.12f, 0.08f, 0.4f));
            shell = new GameObject("AuraVermelha").transform;
            shell.SetParent(w.Visual, false);
            float L = w.Def.Length;
            var core = ArenaUtil.Prim(PrimitiveType.Capsule, shell, new Vector3(0f, 0f, L * 0.55f), new Vector3(0.09f, L * 0.5f, 0.09f), shellMat).transform;
            core.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var col in shell.GetComponentsInChildren<Collider>()) Destroy(col);
            var lg = new GameObject("Luz").transform;
            lg.SetParent(shell, false);
            lg.localPosition = new Vector3(0f, 0f, L * 0.6f);
            glow = lg.gameObject.AddComponent<Light>();
            glow.type = LightType.Point; glow.color = new Color(1f, 0.2f, 0.1f); glow.range = 1.6f; glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;
        }
    }
}
