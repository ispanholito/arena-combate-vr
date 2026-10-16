using System;

namespace Arena.Core
{
    public enum TurnMode { Rapido30, Rapido45, Suave }
    public enum Difficulty { Facil, Normal, Dificil }

    // Dificuldade: escala o dano que o jogador toma e a vida dos inimigos (o XP e o loot não mudam).
    public static class DifficultyRules
    {
        public static float DamageTaken(Difficulty d) => d switch { Difficulty.Facil => 0.6f, Difficulty.Dificil => 1.4f, _ => 1f };
        public static float EnemyHp(Difficulty d) => d switch { Difficulty.Facil => 0.75f, Difficulty.Dificil => 1.3f, _ => 1f };
        public static string Label(Difficulty d) => d switch { Difficulty.Facil => "Fácil", Difficulty.Dificil => "Difícil", _ => "Normal" };
    }

    // Opções de conforto e som (menu da pausa). Campos públicos para o JsonUtility; ficam num arquivo à parte do salvo.
    [Serializable]
    public sealed class GameOptions
    {
        public float volume = 0.8f;
        public int turn = (int)TurnMode.Rapido30;
        public bool moveVignette = true;     // escurece as bordas ao andar/girar com o analógico (menos enjoo)
        public bool seated = false;          // jogar sentado: sobe a câmera
        public bool wristHud = false;        // HUD no pulso esquerdo (como um relógio) em vez de preso à cabeça
        public bool showFps = false;         // mostra quadros por segundo e nº de inimigos (medir desempenho no Quest)
        public int difficulty = (int)Difficulty.Normal;

        public Difficulty Difficulty => Enum.IsDefined(typeof(Difficulty), difficulty) ? (Difficulty)difficulty : Difficulty.Normal;
        public void CycleDifficulty() => difficulty = ((int)Difficulty + 1) % 3;

        public const float SeatedBoost = 0.45f;     // metros a mais na altura dos olhos
        public const float SmoothTurnSpeed = 100f;  // graus/s
        public const float MoveVignetteLevel = 0.45f;

        public TurnMode Turn => Enum.IsDefined(typeof(TurnMode), turn) ? (TurnMode)turn : TurnMode.Rapido30;
        public float SnapDegrees => Turn == TurnMode.Rapido45 ? 45f : 30f;

        public void CycleVolume() => volume = volume >= 0.99f ? 0f : (float)Math.Round(Math.Min(1f, volume + 0.2f), 2);
        public void CycleTurn() => turn = ((int)Turn + 1) % 3;

        public string TurnLabel => Turn switch
        {
            TurnMode.Rapido45 => "Rápido 45°",
            TurnMode.Suave => "Suave",
            _ => "Rápido 30°",
        };

        public GameOptions Sanitized()
        {
            volume = Math.Clamp(float.IsNaN(volume) ? 0.8f : volume, 0f, 1f);
            turn = (int)Turn;
            difficulty = (int)Difficulty;
            return this;
        }
    }
}
