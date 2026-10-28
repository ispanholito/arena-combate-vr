using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Menu inicial (o "santuário" por enquanto): escolher o personagem (continuar o salvo ou criar um novo) e o modo.
    // Arena = ondas e o Rei Goblin; Dungeon = 2 andares com salas e o chefe no fim; Treino = boneco parado, nível 10.
    // VR: analógico direito para cima/baixo, A confirma; sem controle: aponte e faça a pinça. Desktop: setas ou W/S, Enter ou E.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class StartMenu : MonoBehaviour
    {
        ArenaPlayer player;
        MenuPanel panel;
        int cursor = 1;
        // 0 = continuar o salvo, 1 = novo Lâmina, 2 = novo Elementalista
        int pick;
        SaveData save;
        bool confirmOverwrite;
        bool wasShown;
        readonly List<string> rows = new List<string>();

        const int Rows = 6;

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            panel = new MenuPanel("MenuInicial", 1.25f, 1.3f, new Color(0.06f, 0.05f, 0.08f, 0.94f));
            RefreshSave();
        }

        // Ao abrir o menu: relê o salvo e já deixa "Continuar" escolhido quando houver.
        void RefreshSave()
        {
            save = SaveSystem.Load();
            pick = save != null ? 0 : pick == 2 ? 2 : 1;
            confirmOverwrite = false;
        }

        void Update()
        {
            if (panel == null || !player || !ArenaGame.Instance) return;
            bool show = ArenaGame.InMenu;
            if (!show) { panel.Hide(); wasShown = false; return; }
            panel.Show(player);
            if (!wasShown) { RefreshSave(); wasShown = true; }
            var input = player.Input;
            if (input.MenuUp || input.MenuDown) { ArenaAudio.Play2D(Sfx.Click, 0.5f); confirmOverwrite = false; }
            if (input.MenuUp) cursor = (cursor + Rows - 1) % Rows;
            if (input.MenuDown) cursor = (cursor + 1) % Rows;
            if (panel.Hover >= 0 && panel.Hover != cursor) { cursor = panel.Hover; confirmOverwrite = false; }
            int click = panel.TakeClick();
            if (click >= 0) cursor = click;
            if (input.MenuConfirm || click >= 0)
            {
                if (click < 0) ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
                if (cursor == 0)
                {
                    pick = (pick + 1) % 3;
                    if (pick == 0 && save == null) pick = 1;
                    confirmOverwrite = false;
                }
                else if (cursor == 5) ArenaGame.Instance.StartTutorial(pick != 0 || save == null ? new PlayerState(pick == 2 ? ClassId.Elementalista : ClassId.Lamina) : save.ToPlayer());
                else Begin(cursor == 1 ? ArenaGame.GameMode.Santuario : cursor == 2 ? ArenaGame.GameMode.Arena : cursor == 3 ? ArenaGame.GameMode.Dungeon : ArenaGame.GameMode.Treino);
            }
            Draw();
        }

        void Begin(ArenaGame.GameMode mode)
        {
            bool fresh = pick != 0 || save == null;
            // Personagem novo por cima do salvo: pede para confirmar (o Treino não grava, então não pergunta).
            if (fresh && save != null && mode != ArenaGame.GameMode.Treino && !confirmOverwrite)
            {
                confirmOverwrite = true;
                ArenaAudio.Play2D(Sfx.Deny, 0.6f);
                return;
            }
            confirmOverwrite = false;
            var character = fresh ? new PlayerState(pick == 2 ? ClassId.Elementalista : ClassId.Lamina) : save.ToPlayer();
            ArenaGame.Instance.StartMode(mode, character);
        }

        void Draw()
        {
            rows.Clear();
            string who = pick == 0 && save != null ? "Continuar: <color=#ffd36b>" + save.Summary + "</color>"
                       : "Novo personagem: <color=#ffd36b>" + ClassProfile.Get(pick == 2 ? ClassId.Elementalista : ClassId.Lamina).Name + "</color>";
            string whoDesc = pick == 0 && save != null
                ? "progresso salvo (nível, árvore, armas, ouro)" + (save.bossesDefeated > 0 ? " · " + save.bossesDefeated + " chefe(s) vencido(s)" : "")
                : save != null ? "<color=#ff9090>começar de novo apaga o salvo</color>" : (pick == 2 ? "cajado + mão esquerda livre" : "espada + escudo");
            Row(0, who, whoDesc);
            Row(1, "Santuário", "o hub: fogueira, ferreiro e os portais para Arena, Dungeon e Treino");
            Row(2, "Arena", "ondas de inimigos e o Rei Goblin");
            Row(3, "Dungeon", "2 andares de salas, baús, fogueiras e o Rei Goblin no fim");
            Row(4, "Treino", "boneco parado com medidor de dano; nível 10, sem tomar dano (não salva)");
            Row(5, "Tutorial dos gestos", "aprenda a usar a arma, os poderes da mão, a teia e os menus (não salva)");
            string footer = (confirmOverwrite ? "<color=#ff9090>Confirme de novo para APAGAR o salvo e começar do zero</color>\n" : "")
                + "<size=40><color=#999>"
                + (player.IsVr ? "Sem controle: aponte com a mão e encoste o indicador no polegar · Controle: analógico direito + A" : "Setas ou W/S: cima/baixo · Enter ou E: escolher")
                + "\nNa 1ª linha troca o personagem · Na pausa: B (ou R) volta para este menu (o progresso é salvo)</color></size>";
            panel.Render("<b><size=90>ARENA VR</size></b>", rows, cursor, footer);
        }

        void Row(int i, string title, string desc)
        {
            bool sel = cursor == i;
            rows.Add((sel ? "<color=#9fd0ff>" : "") + title + (sel ? "</color>" : "") + "\n   <size=46><color=#bbb>" + desc + "</color></size>");
        }
    }
}
