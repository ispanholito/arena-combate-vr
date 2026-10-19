using System;

namespace Arena.Core
{
    public enum TurnMode { Rapido30, Rapido45, Suave }
    public enum Difficulty { Facil, Normal, Dificil }
    public enum DeathPenalty { Nenhuma, Xp, Ouro, UmaVida }

    // Penalidade de morte (opção, ideia do Blade & Sorcery): nenhuma; só o XP do nível (padrão, pilar do jogo);
    // XP + metade do ouro; ou uma vida só (o personagem é apagado ao morrer fora do santuário/treino).
    public static class DeathRules
    {
        public static bool LosesXp(DeathPenalty p) => p != DeathPenalty.Nenhuma;
        public static int GoldLost(DeathPenalty p, int gold) => p == DeathPenalty.Ouro ? Math.Max(0, gold) / 2 : 0;
        public static bool Permadeath(DeathPenalty p) => p == DeathPenalty.UmaVida;
        public static string Label(DeathPenalty p) => p switch
        {
            DeathPenalty.Nenhuma => "Nenhuma",
            DeathPenalty.Ouro => "XP do nível + metade do ouro",
            DeathPenalty.UmaVida => "UMA VIDA (morreu, apaga o personagem)",
            _ => "XP do nível atual",
        };
    }

    // Dificuldade: escala o dano que o jogador toma e a vida dos inimigos (o XP e o loot não mudam).
    public static class DifficultyRules
    {
        public static float DamageTaken(Difficulty d) => d switch { Difficulty.Facil => 0.6f, Difficulty.Dificil => 1.4f, _ => 1f };
        public static float EnemyHp(Difficulty d) => d switch { Difficulty.Facil => 0.75f, Difficulty.Dificil => 1.3f, _ => 1f };
        // Agressividade: tempo de aviso dos ataques (fácil dá mais tempo para reagir).
        public static float WindupMultiplier(Difficulty d) => d switch { Difficulty.Facil => 1.25f, Difficulty.Dificil => 0.85f, _ => 1f };
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
        public bool music = true;            // trilha ambiente sintetizada
        public int deathPenalty = (int)DeathPenalty.Xp;
        // Quantos goblins físicos (ragdoll ativo) o Treino mantém vivos: 1, 3 ou 6 (medir o FPS no Quest).
        public int trainingGoblins = 1;
        public int TrainingGoblins => trainingGoblins >= 6 ? 6 : trainingGoblins >= 3 ? 3 : 1;
        public void CycleTrainingGoblins() => trainingGoblins = TrainingGoblins == 1 ? 3 : TrainingGoblins == 3 ? 6 : 1;

        public DeathPenalty DeathPenalty => Enum.IsDefined(typeof(DeathPenalty), deathPenalty) ? (DeathPenalty)deathPenalty : DeathPenalty.Xp;
        public void CycleDeathPenalty() => deathPenalty = ((int)DeathPenalty + 1) % 4;

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
            deathPenalty = (int)DeathPenalty;
            trainingGoblins = TrainingGoblins;
            return this;
        }
    }
}
