using System;
using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Pausa com opções. Controle: cima/baixo escolhe, A/Enter confirma (ou alterna a opção), B/R volta ao santuário
    // (do santuário, ao menu inicial). Sem controle: aponte com a mão livre e faça a pinça (HandPointer).
    // Daqui também abrem o Livro de magias e a Árvore de habilidades (o caminho sem controle para os dois); no tutorial,
    // "Pular este passo" e "Sair do tutorial". Opções guardadas em arena_options.json, separado do salvo.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class PauseMenu : MonoBehaviour
    {
        ArenaPlayer player;
        MenuPanel panel;
        int cursor;
        // Linhas da pausa: texto e o que fazer ao confirmar (true = salvar as opções depois).
        readonly List<(string label, Func<bool> act)> rows = new List<(string, Func<bool>)>();
        readonly List<string> labels = new List<string>();

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Start()
        {
            panel = new MenuPanel("MenuPausa", 0.98f, 1.0f, new Color(0.05f, 0.04f, 0.07f, 0.92f));
            _ = SaveSystem.Options;   // aplica o volume salvo
        }

        void BuildRows()
        {
            var game = ArenaGame.Instance;
            var o = SaveSystem.Options;
            bool inHub = game && game.Mode == ArenaGame.GameMode.Santuario;
            bool tut = game && game.InTutorial;
            const string Note = " <size=40><color=#999>";
            rows.Clear();
            rows.Add(("Continuar", () => { game.SetPaused(false); return false; }));
            if (tut)
            {
                rows.Add(("<color=#ffd36b>Pular este passo do tutorial</color>", () => { TutorialMode.Instance?.Skip(); game.SetPaused(false); return false; }));
                rows.Add(("<color=#ffd36b>Sair do tutorial</color>" + Note + "(volta ao santuário; dá para rever pelo portal TUTORIAL)</color></size>", () => { game.FinishTutorial(); return false; }));
            }
            rows.Add(("<color=#ffd36b>Livro de magias</color>" + Note + "(como fazer cada poder e habilidade)</color></size>", () => { SpellBook.Open(); return false; }));
            rows.Add(("Árvore de habilidades", () => { if (!ArenaGame.InMenu) { game.SetPaused(false); player.TreeOpen = true; } return false; }));
            rows.Add(("Volume: " + Mathf.RoundToInt(o.volume * 100f) + "%", () => { o.CycleVolume(); return true; }));
            rows.Add(("Giro (com dois controles): " + o.TurnLabel, () => { o.CycleTurn(); return true; }));
            rows.Add(("Vinheta ao andar: " + (o.moveVignette ? "ligada" : "desligada"), () => { o.moveVignette = !o.moveVignette; return true; }));
            rows.Add(("Locomoção (analógico): " + (o.teleport ? "teletransporte" : "andar suave"), () => { o.teleport = !o.teleport; return true; }));
            rows.Add(("Correr balançando os braços (sem controle): " + (o.armSwingRun ? "ligado" : "desligado"), () => { o.armSwingRun = !o.armSwingRun; return true; }));
            rows.Add(("Modo sentado: " + (o.seated ? "ligado (+45 cm)" : "desligado"), () => { o.seated = !o.seated; return true; }));
            rows.Add(("HUD: " + (o.wristHud ? "no pulso esquerdo (olhe para o relógio)" : "na frente da visão"), () => { o.wristHud = !o.wristHud; return true; }));
            rows.Add(("Dificuldade: " + DifficultyRules.Label(o.Difficulty) + Note + "(dano recebido e vida dos inimigos)</color></size>", () => { o.CycleDifficulty(); return true; }));
            rows.Add(("Mostrar FPS: " + (o.showFps ? "sim" : "não"), () => { o.showFps = !o.showFps; return true; }));
            rows.Add(("Música: " + (o.music ? "ligada" : "desligada"), () => { o.music = !o.music; return true; }));
            rows.Add(("Morte: " + DeathRules.Label(o.DeathPenalty), () => { o.CycleDeathPenalty(); if (player.State != null) player.State.Penalty = o.DeathPenalty; return true; }));
            rows.Add(("Inimigos físicos no Treino: " + o.TrainingGoblins + Note + "(teste de desempenho)</color></size>", () => { o.CycleTrainingGoblins(); return true; }));
            rows.Add(("Inimigos físicos na Arena/Dungeon (beta): " + (o.physicalGoblins ? "sim" : "não") + Note + "(goblin, esqueleto e orc)</color></size>", () => { o.physicalGoblins = !o.physicalGoblins; return true; }));
            rows.Add((inHub ? "Menu inicial (trocar de personagem)" : "Voltar ao santuário", () => { cursor = 0; game.BackFromPause(); return false; }));
        }

        // Chamado pelo ArenaPlayer enquanto pausado.
        public void HandleInput()
        {
            if (panel == null || SpellBook.IsOpen || SpellBook.ClosedFrame == Time.frameCount) return;
            var game = ArenaGame.Instance;
            var input = player.Input;
            BuildRows();
            int n = rows.Count;
            if (input.MenuUp || input.MenuDown) ArenaAudio.Play2D(Sfx.Click, 0.5f);
            if (input.MenuUp) cursor = (cursor + n - 1) % n;
            if (input.MenuDown) cursor = (cursor + 1) % n;
            if (panel.Hover >= 0) cursor = panel.Hover;
            cursor = Mathf.Clamp(cursor, 0, n - 1);
            if (input.MenuRespec) { cursor = 0; game.BackFromPause(); return; }
            int click = panel.TakeClick();
            if (click >= 0) cursor = click;
            else if (!input.MenuConfirm) return;
            else ArenaAudio.Play2D(Sfx.Click, 0.8f, 1.3f);
            if (rows[cursor].act()) SaveSystem.SaveOptions();
        }

        void Update()
        {
            if (panel == null) return;
            bool show = ArenaGame.Paused && !SpellBook.IsOpen;
            if (!show) { panel.Hide(); if (!ArenaGame.Paused) cursor = 0; return; }
            panel.Show(player);
            BuildRows();
            bool inHub = ArenaGame.Instance && ArenaGame.Instance.Mode == ArenaGame.GameMode.Santuario;
            labels.Clear();
            foreach (var r in rows) labels.Add(r.label);
            cursor = Mathf.Clamp(cursor, 0, labels.Count - 1);
            string footer = "<size=40><color=#999>Sem controle: aponte com a mão e encoste o indicador no polegar\nControle: cima/baixo escolhe · A/Enter confirma · B/R "
                + (inHub ? "menu inicial" : "santuário") + "\nContinuar também: botão Menu (☰) ou gesto de menu da mão esquerda · P/Esc</color></size>";
            panel.Render("<b>PAUSADO</b>", labels, cursor, footer);
        }
    }
}
