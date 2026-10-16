using System.Linq;
using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Árvore de habilidades em texto, na frente do jogador.
    // Abrir: Tab (desktop) / Y ou segurar o analógico direito (VR). Navegar: cima/baixo. Confirmar: Enter/E ou A.
    // Respec (devolve os pontos): R ou B, só perto da fogueira. Linha 0 troca de classe (também só na fogueira).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SkillTreeMenu : MonoBehaviour
    {
        ArenaPlayer player;
        Transform panel;
        TextMesh text;
        int cursor;
        readonly StringBuilder sb = new StringBuilder();

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            panel = new GameObject("Arvore").transform;
            panel.SetParent(player.Head, false);
            panel.localPosition = new Vector3(0, 0, 1.2f);
            ArenaUtil.Prim(PrimitiveType.Quad, panel, new Vector3(0, 0, 0.01f), new Vector3(1.25f, 0.85f, 1f), ArenaUtil.Unlit(new Color(0.08f, 0.06f, 0.1f, 0.92f)));
            text = ArenaFx.MakeText(panel, "", 0.0055f, Color.white, TextAnchor.MiddleLeft);
            text.transform.localPosition = new Vector3(-0.58f, 0, 0);
            panel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!panel || !player || player.State == null) return;
            if (ArenaGame.Blocked) { if (panel) panel.gameObject.SetActive(false); return; }
            bool open = player.TreeOpen;
            if (panel.gameObject.activeSelf != open) panel.gameObject.SetActive(open);
            if (!open) return;
            var s = player.State;
            var nodes = s.Tree.Nodes.ToList();
            int count = nodes.Count + 1;
            var input = player.Input;
            if (input.MenuUp) cursor = (cursor + count - 1) % count;
            if (input.MenuDown) cursor = (cursor + 1) % count;
            bool atFire = ArenaGame.Instance && ArenaGame.Instance.NearBonfire;
            if (input.MenuConfirm)
            {
                if (cursor == 0)
                {
                    if (atFire) player.ChangeClass(s.Class.Id == ClassId.Lamina ? ClassId.Elementalista : ClassId.Lamina);
                    else ArenaGame.Instance?.Message("Troque de classe na fogueira", 1f);
                }
                else if (!s.Tree.Unlock(nodes[cursor - 1].Id)) { ArenaGame.Instance?.Message("Não dá para liberar ainda", 0.8f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); }
                else ArenaAudio.Play2D(Sfx.Level, 0.6f, 1.2f);
            }
            if (input.MenuRespec)
            {
                if (atFire) { int n = s.Tree.Respec(); ArenaGame.Instance?.Message($"Respec: {n} ponto(s) devolvidos", 1f); }
                else ArenaGame.Instance?.Message("Respec só na fogueira", 1f);
            }
            // Atalho de teste no desktop: L = +1 nível
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!player.IsVr && kb != null && kb.lKey.wasPressedThisFrame) s.Progression.GainXp(s.Progression.XpNeeded - s.Progression.Xp);
            Draw(nodes, atFire);
        }

        void Draw(System.Collections.Generic.List<SkillNode> nodes, bool atFire)
        {
            var s = player.State;
            sb.Clear();
            sb.Append("<b>ÁRVORE — ").Append(s.Class.Name.ToUpper()).Append("</b>   Nível ").Append(s.Progression.Level)
              .Append("   Pontos: <color=#ffd36b>").Append(s.Progression.UnspentPoints).Append("</color>\n\n");
            sb.Append(cursor == 0 ? "> " : "   ").Append(atFire ? "Trocar de classe" : "<color=#777>Trocar de classe (na fogueira)</color>").Append("\n\n");
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                bool own = s.Tree.Has(n.Id), can = s.Tree.CanUnlock(n.Id);
                string color = own ? "#ffd36b" : can ? "#9fd0ff" : "#8a8494";
                sb.Append(cursor == i + 1 ? "> " : "   ")
                  .Append("<color=").Append(color).Append(">").Append(n.Name).Append(n.IsActive ? " (ativa)" : " (passiva)").Append("</color>  ")
                  .Append(own ? "<color=#ffd36b>LIBERADA</color>" : can ? "<color=#9fd0ff>liberar</color>" : $"<color=#777>nível {n.MinLevel}</color>")
                  .Append("\n      <size=40><color=#bbb>").Append(n.Description).Append("</color></size>\n");
            }
            sb.Append("\n<size=40><color=#999>Cima/baixo navega · Enter/E/A confirma · R/B respec na fogueira · Tab/Y fecha</color></size>");
            text.text = sb.ToString();
        }
    }
}
