using System.Text;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Pausa com opções. Cima/baixo escolhe, A/Enter confirma (ou alterna a opção), B/R volta ao santuário
    // (do santuário, ao menu inicial). Opções de conforto: volume, giro (rápido 30°/45° ou suave), vinheta ao andar,
    // modo sentado (sobe a câmera). Guardadas em arena_options.json, separado do salvo.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class PauseMenu : MonoBehaviour
    {
        ArenaPlayer player;
        Transform panel;
        TextMesh text;
        int cursor;
        readonly StringBuilder sb = new StringBuilder();
        const int Rows = 13;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Start()
        {
            panel = new GameObject("MenuPausa").transform;
            panel.SetParent(player.Head, false);
            panel.localPosition = new Vector3(0, 0, 1.0f);
            ArenaUtil.Prim(PrimitiveType.Quad, panel, new Vector3(0, 0, 0.01f), new Vector3(0.95f, 0.98f, 1f), ArenaUtil.Unlit(new Color(0.05f, 0.04f, 0.07f, 0.92f)));
            text = ArenaFx.MakeText(panel, "", 0.0055f, Color.white, TextAnchor.MiddleLeft);
            text.transform.localPosition = new Vector3(-0.43f, 0, 0);
            panel.gameObject.SetActive(false);
            _ = SaveSystem.Options;   // aplica o volume salvo
        }

        // Chamado pelo ArenaPlayer enquanto pausado.
        public void HandleInput()
        {
            var game = ArenaGame.Instance;
            var input = player.Input;
            var o = SaveSystem.Options;
            if (input.MenuUp || input.MenuDown) ArenaAudio.Play2D(Sfx.Click, 0.5f);
            if (input.MenuUp) cursor = (cursor + Rows - 1) % Rows;
            if (input.MenuDown) cursor = (cursor + 1) % Rows;
            if (input.MenuRespec) { cursor = 0; game.BackFromPause(); return; }
            if (!input.MenuConfirm) return;
            ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
            switch (cursor)
            {
                case 0: game.SetPaused(false); return;
                case 1: o.CycleVolume(); break;
                case 2: o.CycleTurn(); break;
                case 3: o.moveVignette = !o.moveVignette; break;
                case 4: o.seated = !o.seated; break;
                case 5: o.wristHud = !o.wristHud; break;
                case 6: o.CycleDifficulty(); break;
                case 7: o.showFps = !o.showFps; break;
                case 8: o.music = !o.music; break;
                case 9: o.CycleDeathPenalty(); if (player.State != null) player.State.Penalty = o.DeathPenalty; break;
                case 10: o.CycleTrainingGoblins(); break;
                case 11: o.physicalGoblins = !o.physicalGoblins; break;
                case 12: cursor = 0; game.BackFromPause(); return;
            }
            SaveSystem.SaveOptions();
        }

        void Update()
        {
            if (!panel) return;
            bool show = ArenaGame.Paused;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) { cursor = 0; return; }
            var o = SaveSystem.Options;
            bool inHub = ArenaGame.Instance && ArenaGame.Instance.Mode == ArenaGame.GameMode.Santuario;
            sb.Clear();
            sb.Append("<b>PAUSADO</b>\n\n");
            Row(0, "Continuar");
            Row(1, "Volume: " + Mathf.RoundToInt(o.volume * 100f) + "%");
            Row(2, "Giro (com dois controles): " + o.TurnLabel);
            Row(3, "Vinheta ao andar: " + (o.moveVignette ? "ligada" : "desligada"));
            Row(4, "Modo sentado: " + (o.seated ? "ligado (+45 cm)" : "desligado"));
            Row(5, "HUD: " + (o.wristHud ? "no pulso esquerdo (olhe para o relógio)" : "na frente da visão"));
            Row(6, "Dificuldade: " + DifficultyRules.Label(o.Difficulty) + " <size=40><color=#999>(dano recebido e vida dos inimigos)</color></size>");
            Row(7, "Mostrar FPS: " + (o.showFps ? "sim" : "não"));
            Row(8, "Música: " + (o.music ? "ligada" : "desligada"));
            Row(9, "Morte: " + DeathRules.Label(o.DeathPenalty));
            Row(10, "Goblins físicos no Treino: " + o.TrainingGoblins + " <size=40><color=#999>(teste de desempenho)</color></size>");
            Row(11, "Goblins físicos na Arena/Dungeon (beta): " + (o.physicalGoblins ? "sim" : "não") + " <size=40><color=#999>(vale para os próximos que nascerem)</color></size>");
            Row(12, inHub ? "Menu inicial (trocar de personagem)" : "Voltar ao santuário");
            sb.Append("\n<size=40><color=#999>Cima/baixo escolhe · A/Enter confirma · B/R ")
              .Append(inHub ? "menu inicial" : "santuário")
              .Append("\nContinuar também: botão Menu (☰) ou gesto de menu da mão esquerda · P/Esc</color></size>");
            text.text = sb.ToString();
        }

        void Row(int i, string label)
        {
            sb.Append(cursor == i ? "<color=#9fd0ff>> " : "   ").Append(label).Append(cursor == i ? "</color>" : "").Append('\n');
        }
    }
}
