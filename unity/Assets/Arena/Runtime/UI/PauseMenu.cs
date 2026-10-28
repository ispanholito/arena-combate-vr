using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Pausa com opções. Controle: cima/baixo escolhe, A/Enter confirma (ou alterna a opção), B/R volta ao santuário
    // (do santuário, ao menu inicial). Sem controle: aponte com a mão livre e faça a pinça (HandPointer).
    // Daqui também abrem o Livro de magias e a Árvore de habilidades (o caminho sem controle para os dois).
    // Opções guardadas em arena_options.json, separado do salvo.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class PauseMenu : MonoBehaviour
    {
        ArenaPlayer player;
        MenuPanel panel;
        int cursor;
        readonly List<string> rows = new List<string>();
        const int Rows = 15;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Start()
        {
            panel = new MenuPanel("MenuPausa", 0.98f, 1.0f, new Color(0.05f, 0.04f, 0.07f, 0.92f));
            _ = SaveSystem.Options;   // aplica o volume salvo
        }

        // Chamado pelo ArenaPlayer enquanto pausado.
        public void HandleInput()
        {
            if (panel == null || SpellBook.IsOpen || SpellBook.ClosedFrame == Time.frameCount) return;
            var game = ArenaGame.Instance;
            var input = player.Input;
            if (input.MenuUp || input.MenuDown) ArenaAudio.Play2D(Sfx.Click, 0.5f);
            if (input.MenuUp) cursor = (cursor + Rows - 1) % Rows;
            if (input.MenuDown) cursor = (cursor + 1) % Rows;
            if (panel.Hover >= 0) cursor = panel.Hover;
            if (input.MenuRespec) { cursor = 0; game.BackFromPause(); return; }
            int click = panel.TakeClick();
            if (click >= 0) cursor = click;
            else if (!input.MenuConfirm) return;
            else ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
            var o = SaveSystem.Options;
            switch (cursor)
            {
                case 0: game.SetPaused(false); return;
                case 1: SpellBook.Open(); return;
                case 2:
                    if (ArenaGame.InMenu) return;
                    game.SetPaused(false); player.TreeOpen = true; return;
                case 3: o.CycleVolume(); break;
                case 4: o.CycleTurn(); break;
                case 5: o.moveVignette = !o.moveVignette; break;
                case 6: o.seated = !o.seated; break;
                case 7: o.wristHud = !o.wristHud; break;
                case 8: o.CycleDifficulty(); break;
                case 9: o.showFps = !o.showFps; break;
                case 10: o.music = !o.music; break;
                case 11: o.CycleDeathPenalty(); if (player.State != null) player.State.Penalty = o.DeathPenalty; break;
                case 12: o.CycleTrainingGoblins(); break;
                case 13: o.physicalGoblins = !o.physicalGoblins; break;
                case 14: cursor = 0; game.BackFromPause(); return;
            }
            SaveSystem.SaveOptions();
        }

        void Update()
        {
            if (panel == null) return;
            bool show = ArenaGame.Paused && !SpellBook.IsOpen;
            if (!show) { panel.Hide(); if (!ArenaGame.Paused) cursor = 0; return; }
            panel.Show(player);
            var o = SaveSystem.Options;
            bool inHub = ArenaGame.Instance && ArenaGame.Instance.Mode == ArenaGame.GameMode.Santuario;
            const string Note = " <size=40><color=#999>";
            rows.Clear();
            rows.Add("Continuar");
            rows.Add("<color=#ffd36b>Livro de magias</color>" + Note + "(como fazer cada poder e habilidade)</color></size>");
            rows.Add("Árvore de habilidades");
            rows.Add("Volume: " + Mathf.RoundToInt(o.volume * 100f) + "%");
            rows.Add("Giro (com dois controles): " + o.TurnLabel);
            rows.Add("Vinheta ao andar: " + (o.moveVignette ? "ligada" : "desligada"));
            rows.Add("Modo sentado: " + (o.seated ? "ligado (+45 cm)" : "desligado"));
            rows.Add("HUD: " + (o.wristHud ? "no pulso esquerdo (olhe para o relógio)" : "na frente da visão"));
            rows.Add("Dificuldade: " + DifficultyRules.Label(o.Difficulty) + Note + "(dano recebido e vida dos inimigos)</color></size>");
            rows.Add("Mostrar FPS: " + (o.showFps ? "sim" : "não"));
            rows.Add("Música: " + (o.music ? "ligada" : "desligada"));
            rows.Add("Morte: " + DeathRules.Label(o.DeathPenalty));
            rows.Add("Inimigos físicos no Treino: " + o.TrainingGoblins + Note + "(teste de desempenho)</color></size>");
            rows.Add("Inimigos físicos na Arena/Dungeon (beta): " + (o.physicalGoblins ? "sim" : "não") + Note + "(goblin, esqueleto e orc)</color></size>");
            rows.Add(inHub ? "Menu inicial (trocar de personagem)" : "Voltar ao santuário");
            string footer = "<size=40><color=#999>Sem controle: aponte com a mão e encoste o indicador no polegar\nControle: cima/baixo escolhe · A/Enter confirma · B/R "
                + (inHub ? "menu inicial" : "santuário") + "\nContinuar também: botão Menu (☰) ou gesto de menu da mão esquerda · P/Esc</color></size>";
            panel.Render("<b>PAUSADO</b>", rows, cursor, footer);
        }
    }
}
