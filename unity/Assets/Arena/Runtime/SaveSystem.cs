using System;
using System.IO;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Salva o personagem em JSON. No Quest: armazenamento do app (persistentDataPath). No editor: pasta Saves/ do
    // projeto (o disco C: do PC do Pablo vive cheio; o projeto fica no E:).
    // Grava num arquivo temporário e troca no fim, para um desligamento no meio não corromper o salvo.
    public static class SaveSystem
    {
        const string FileName = "arena_save.json";

        public static string Folder =>
            Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Saves")) : Application.persistentDataPath;

        public static string FilePath => Path.Combine(Folder, FileName);

        public static bool HasSave => File.Exists(FilePath);

        public static void Save(PlayerState ps, int deepestFloor = -1, int bossesDefeated = -1)
        {
            if (ps == null) return;
            try
            {
                var old = Load();
                var data = SaveData.From(ps);
                data.deepestFloor = deepestFloor >= 0 ? deepestFloor : old?.deepestFloor ?? 0;
                data.bossesDefeated = bossesDefeated >= 0 ? bossesDefeated : old?.bossesDefeated ?? 0;
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("[Save] Falha ao salvar: " + e.Message); }
        }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data != null && data.version >= 1 ? data : null;
            }
            catch (Exception e) { Debug.LogWarning("[Save] Salvo ilegível: " + e.Message); return null; }
        }

        // ---------- Opções (arquivo à parte: apagar o salvo não zera as opções) ----------
        static GameOptions options;
        public static GameOptions Options
        {
            get
            {
                if (options != null) return options;
                try
                {
                    var path = Path.Combine(Folder, "arena_options.json");
                    options = File.Exists(path) ? JsonUtility.FromJson<GameOptions>(File.ReadAllText(path)) : null;
                }
                catch (Exception e) { Debug.LogWarning("[Save] Opções ilegíveis: " + e.Message); }
                options = (options ?? new GameOptions()).Sanitized();
                ArenaAudio.MasterVolume = options.volume;
                return options;
            }
        }

        public static void SaveOptions()
        {
            ArenaAudio.MasterVolume = Options.volume;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "arena_options.json"), JsonUtility.ToJson(Options, true));
            }
            catch (Exception e) { Debug.LogWarning("[Save] Falha ao salvar opções: " + e.Message); }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("[Save] Falha ao apagar: " + e.Message); }
        }
    }
}
