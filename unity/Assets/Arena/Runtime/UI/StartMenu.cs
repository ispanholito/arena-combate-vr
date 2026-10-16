using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Menu inicial (o "santuário" por enquanto): escolher o personagem (continuar o salvo ou criar um novo) e o modo.
    // Arena = ondas e o Rei Goblin; Dungeon = 2 andares com salas e o chefe no fim; Treino = boneco parado, nível 10.
    // VR: analógico direito para cima/baixo, A confirma. Desktop: setas ou W/S, Enter ou E confirma.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class StartMenu : MonoBehaviour
    {
        ArenaPlayer player;
        Transform panel;
        TextMesh text;
        int cursor = 1;
        // 0 = continuar o salvo, 1 = novo Lâmina, 2 = novo Elementalista
        int pick;
        SaveData save;
        bool confirmOverwrite;
        bool wasShown;
        readonly StringBuilder sb = new StringBuilder();

        const int Rows = 4;

        void Start()
        {
            player = GetComponent<ArenaPlayer>();
            panel = new GameObject("MenuInicial").transform;
            panel.SetParent(player.Head, false);
            panel.localPosition = new Vector3(0, 0, 1.3f);
            ArenaUtil.Prim(PrimitiveType.Quad, panel, new Vector3(0, 0, 0.01f), new Vector3(1.25f, 0.95f, 1f), ArenaUtil.Unlit(new Color(0.06f, 0.05f, 0.08f, 0.94f)));
            text = ArenaFx.MakeText(panel, "", 0.0056f, Color.white, TextAnchor.MiddleLeft);
            text.transform.localPosition = new Vector3(-0.57f, 0, 0);
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
            if (!panel || !player || !ArenaGame.Instance) return;
            bool show = ArenaGame.InMenu;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) { wasShown = false; return; }
            if (!wasShown) { RefreshSave(); wasShown = true; }
            var input = player.Input;
            if (input.MenuUp || input.MenuDown) { ArenaAudio.Play2D(Sfx.Click, 0.5f); confirmOverwrite = false; }
            if (input.MenuUp) cursor = (cursor + Rows - 1) % Rows;
            if (input.MenuDown) cursor = (cursor + 1) % Rows;
            if (input.MenuConfirm)
            {
                ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
                if (cursor == 0)
                {
                    pick = (pick + 1) % 3;
                    if (pick == 0 && save == null) pick = 1;
                    confirmOverwrite = false;
                }
                else Begin(cursor == 1 ? ArenaGame.GameMode.Arena : cursor == 2 ? ArenaGame.GameMode.Dungeon : ArenaGame.GameMode.Treino);
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
            sb.Clear();
            sb.Append("<b><size=90>ARENA VR</size></b>\n\n");
            string who = pick == 0 && save != null ? "Continuar: <color=#ffd36b>" + save.Summary + "</color>"
                       : "Novo personagem: <color=#ffd36b>" + ClassProfile.Get(pick == 2 ? ClassId.Elementalista : ClassId.Lamina).Name + "</color>";
            string whoDesc = pick == 0 && save != null
                ? "progresso salvo (nível, árvore, armas, ouro)" + (save.bossesDefeated > 0 ? " · " + save.bossesDefeated + " chefe(s) vencido(s)" : "")
                : save != null ? "<color=#ff9090>começar de novo apaga o salvo</color>" : (pick == 2 ? "cajado + mão esquerda livre" : "espada + escudo");
            Row(0, who, whoDesc);
            Row(1, "Arena", "ondas de inimigos e o Rei Goblin");
            Row(2, "Dungeon", "2 andares de salas, fogueiras, loot e o Rei Goblin no fim");
            Row(3, "Treino", "boneco parado com medidor de dano; nível 10, sem tomar dano (não salva)");
            if (confirmOverwrite) sb.Append("<color=#ff9090>Confirme de novo para APAGAR o salvo e começar do zero</color>\n");
            sb.Append("\n<size=40><color=#999>")
              .Append(player.IsVr ? "Analógico direito: cima/baixo · A: escolher (na 1ª linha troca o personagem)" : "Setas ou W/S: cima/baixo · Enter ou E: escolher (na 1ª linha troca o personagem)")
              .Append("\nNa pausa: B (ou R) volta para este menu (o progresso é salvo)</color></size>");
            text.text = sb.ToString();
        }

        void Row(int i, string title, string desc)
        {
            bool sel = cursor == i;
            sb.Append(sel ? "<color=#9fd0ff>> " : "   ").Append(title).Append(sel ? "</color>" : "")
              .Append("\n      <size=46><color=#bbb>").Append(desc).Append("</color></size>\n\n");
        }
    }
}
