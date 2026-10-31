using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Aura vermelha nas armas das DUAS mãos (substitui a Postura de Contra; regras em RedAuraRules). Enquanto
    // PlayerState.RedAura > 0: brilho vermelho envolvendo a lâmina, uma luz pequena e brasas; pisca no último segundo.
    // O dano extra e a quebra de defesa são aplicados nos acertos (RagdollGoblin, PlayerWeapon, GoblinEnemy).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class RedAura : MonoBehaviour
    {
        public static RedAura Instance { get; private set; }
        public static bool Active => Instance && Instance.player.State != null && Instance.player.State.AuraActive
                                     && (Held(Instance.player.Weapon) || Held(Instance.player.WeaponL));
        static bool Held(PlayerWeapon w) => w && w.Held;
        public static float DamageMultiplier => Active ? RedAuraRules.DamageMultiplier : 1f;

        ArenaPlayer player;
        // Uma casca por mão (0 = direita, 1 = esquerda).
        readonly Transform[] shell = new Transform[2];
        readonly Light[] glow = new Light[2];
        readonly Transform[] shownVisual = new Transform[2];
        Material shellMat;
        float emberT;

        void Awake() { player = GetComponent<ArenaPlayer>(); Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; foreach (var s in shell) if (s) Destroy(s.gameObject); }

        void Update()
        {
            bool active = Active;
            float left = player.State != null ? player.State.RedAura : 0f;
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 9f);
            if (left < 1f) pulse *= Mathf.Sin(Time.time * 30f) > 0f ? 1f : 0.2f;   // acabando: pisca
            if (shellMat) shellMat.color = new Color(1f, 0.12f, 0.08f, 0.35f * pulse + 0.1f);
            emberT -= Time.deltaTime;
            bool ember = emberT <= 0f;
            if (ember) emberT = 0.12f;
            for (int i = 0; i < 2; i++)
            {
                var w = i == 0 ? player.Weapon : player.WeaponL;
                if (!active || !Held(w) || !w.Visual) { if (shell[i]) shell[i].gameObject.SetActive(false); continue; }
                if (!shell[i] || shownVisual[i] != w.Visual) Build(i, w);
                shell[i].gameObject.SetActive(true);
                if (glow[i]) glow[i].intensity = 1.6f * pulse;
                if (ember) ArenaFx.Burst(Vector3.Lerp(w.BasePoint, w.TipPoint, Random.value), new Color(1f, 0.2f, 0.1f), 2, 0.8f, 0.4f);
            }
        }

        // Casca vermelha translúcida ao longo da arma (filha do modelo, acompanha a física).
        void Build(int i, PlayerWeapon w)
        {
            if (shell[i]) Destroy(shell[i].gameObject);
            shownVisual[i] = w.Visual;
            if (!shellMat) shellMat = ArenaUtil.Unlit(new Color(1f, 0.12f, 0.08f, 0.4f));
            var root = new GameObject("AuraVermelha").transform;
            shell[i] = root;
            root.SetParent(w.Visual, false);
            float L = w.Def.Length;
            var core = ArenaUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0f, 0f, L * 0.55f), new Vector3(0.09f, L * 0.5f, 0.09f), shellMat).transform;
            core.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var col in root.GetComponentsInChildren<Collider>()) Destroy(col);
            var lg = new GameObject("Luz").transform;
            lg.SetParent(root, false);
            lg.localPosition = new Vector3(0f, 0f, L * 0.6f);
            var gl = glow[i] = lg.gameObject.AddComponent<Light>();
            gl.type = LightType.Point; gl.color = new Color(1f, 0.2f, 0.1f); gl.range = 1.6f; gl.intensity = 1.2f;
            gl.shadows = LightShadows.None;
        }
    }
}
