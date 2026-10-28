using System.Collections.Generic;
using System.Linq;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Árvore de habilidades na frente do jogador.
    // Abrir: Tab (desktop) / Y ou segurar o analógico direito (VR) / pausa → "Árvore de habilidades" (sem controle).
    // Navegar: cima/baixo ou apontar com a mão livre. Confirmar: Enter/E, A ou pinça.
    // Respec (devolve os pontos): R, B ou a linha "Respec", só perto da fogueira. Linha 0 troca de classe (também só na
    // fogueira). Última linha fecha (sem controle não há botão para fechar).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SkillTreeMenu : MonoBehaviour
    {
        ArenaPlayer player;
        MenuPanel panel;
        int cursor;
        readonly List<string> rows = new List<string>();
        const int Extra = 3;   // 0 = trocar de classe, 1 = ferreiro, 2 = respec; depois os nós; por último "Fechar"

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            panel = new MenuPanel("Arvore", 1.25f, 1.2f, new Color(0.08f, 0.06f, 0.1f, 0.92f));
        }

        void Update()
        {
            if (panel == null || !player || player.State == null) return;
            if (ArenaGame.Blocked || !player.TreeOpen) { panel.Hide(); return; }
            bool justOpened = !panel.IsVisible;   // o botão/pinça que abriu a árvore não conta aqui
            panel.Show(player);
            var s = player.State;
            var nodes = s.Tree.Nodes.ToList();
            int count = nodes.Count + Extra + 1;
            var input = player.Input;
            if (input.MenuUp) cursor = (cursor + count - 1) % count;
            if (input.MenuDown) cursor = (cursor + 1) % count;
            if (panel.Hover >= 0) cursor = panel.Hover;
            int click = panel.TakeClick();
            if (click >= 0) cursor = click;
            bool confirm = !justOpened && (input.MenuConfirm || click >= 0);
            bool atFire = ArenaGame.Instance && ArenaGame.Instance.NearBonfire;
            if (confirm)
            {
                if (cursor == 0)
                {
                    if (atFire) player.ChangeClass(s.Class.Id == ClassId.Lamina ? ClassId.Elementalista : ClassId.Lamina);
                    else ArenaGame.Instance?.Message("Troque de classe na fogueira", 1f);
                }
                else if (cursor == 1) Smith(atFire);
                else if (cursor == 2) Respec(atFire);
                else if (cursor == count - 1) { player.TreeOpen = false; panel.Hide(); return; }
                else if (!s.Tree.Unlock(nodes[cursor - Extra].Id)) { ArenaGame.Instance?.Message("Não dá para liberar ainda", 0.8f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); }
                else ArenaAudio.Play2D(Sfx.Level, 0.6f, 1.2f);
            }
            if (input.MenuRespec && !justOpened) Respec(atFire);
            // Atalho de teste no desktop: L = +1 nível
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!player.IsVr && kb != null && kb.lKey.wasPressedThisFrame) s.Progression.GainXp(s.Progression.XpNeeded - s.Progression.Xp);
            if (confirm || input.MenuRespec) ArenaGame.Instance?.RequestSave();
            Draw(nodes, atFire);
        }

        void Respec(bool atFire)
        {
            if (atFire) { int n = player.State.Tree.Respec(); ArenaGame.Instance?.Message($"Respec: {n} ponto(s) devolvidos", 1f); }
            else ArenaGame.Instance?.Message("Respec só na fogueira", 1f);
        }

        // Ferreiro (na fogueira): melhora a arma da mão de +0 até +5 com ouro.
        void Smith(bool atFire)
        {
            var s = player.State;
            var item = s.ActiveItem;
            if (!atFire) { ArenaGame.Instance?.Message("O ferreiro fica na fogueira", 1f); return; }
            if (item == null || !item.CanUpgrade) { ArenaGame.Instance?.Message("Arma no máximo (+5)", 1f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); return; }
            if (!Smithy.TryUpgrade(item, s)) { ArenaGame.Instance?.Message("Ouro insuficiente (" + item.UpgradeCost + ")", 1f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); return; }
            player.EquipActive();
            ArenaAudio.Play2D(Sfx.Parry, 0.8f, 0.8f);
            ArenaGame.Instance?.Message(item.DisplayName + "!", 1.2f);
        }

        void Draw(List<SkillNode> nodes, bool atFire)
        {
            var s = player.State;
            string header = "<b>ÁRVORE — " + s.Class.Name.ToUpper() + "</b>   Nível " + s.Progression.Level
                + "   Pontos: <color=#ffd36b>" + s.Progression.UnspentPoints + "</color>";
            rows.Clear();
            rows.Add(atFire ? "Trocar de classe" : "<color=#777>Trocar de classe (na fogueira)</color>");
            var item = s.ActiveItem;
            string smith = item == null ? "Ferreiro" : !item.CanUpgrade ? "Ferreiro: " + item.DisplayName + " (máximo)"
                : "Ferreiro: " + item.DisplayName + " → +" + (item.Upgrade + 1) + "  (" + item.UpgradeCost + " ouro, você tem " + s.Gold + ")";
            rows.Add(atFire ? "<color=#ffd34d>" + smith + "</color>" : "<color=#777>Ferreiro (na fogueira)</color>");
            rows.Add(atFire ? "Respec (devolve os pontos)" : "<color=#777>Respec (na fogueira)</color>");
            foreach (var n in nodes)
            {
                bool own = s.Tree.Has(n.Id), can = s.Tree.CanUnlock(n.Id);
                string color = own ? "#ffd36b" : can ? "#9fd0ff" : "#8a8494";
                rows.Add("<color=" + color + ">" + n.Name + (n.IsActive ? " (ativa)" : " (passiva)") + "</color>  "
                    + (own ? "<color=#ffd36b>LIBERADA</color>" : can ? "<color=#9fd0ff>liberar</color>" : $"<color=#777>nível {n.MinLevel}</color>")
                    + "\n   <size=40><color=#bbb>" + n.Description + "</color></size>");
            }
            rows.Add("<color=#ff9f9f>Fechar</color>");
            panel.Render(header, rows, cursor, "<size=40><color=#999>Aponte e faça a pinça · ou cima/baixo + Enter/E/A · R/B respec na fogueira · Tab/Y fecha</color></size>");
        }
    }
}
