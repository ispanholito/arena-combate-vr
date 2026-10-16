using System.Threading.Tasks;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Trilha ambiente (MusicSynth no Core): santuário/menu calmo, exploração escura e combate com tambor quando há
    // inimigos por perto. As faixas são geradas numa thread à parte na inicialização e trocadas com crossfade.
    public sealed class ArenaMusic : MonoBehaviour
    {
        const int Rate = 22050;
        const float BaseVolume = 0.35f, FadeSeconds = 1.5f, CombatRange = 14f;

        readonly AudioClip[] clips = new AudioClip[3];
        float[][] pending;
        Task generating;
        AudioSource a, b;   // a = tocando, b = entrando
        MusicMood current;
        bool started;

        void Start()
        {
            a = MakeSource(); b = MakeSource();
            generating = Task.Run(() =>
            {
                var data = new float[3][];
                for (int i = 0; i < 3; i++) data[i] = MusicSynth.Generate((MusicMood)i, Rate, 7 + i);
                pending = data;
            });
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        void Update()
        {
            if (pending != null && clips[0] == null)
            {
                for (int i = 0; i < 3; i++)
                {
                    clips[i] = AudioClip.Create("Musica" + (MusicMood)i, pending[i].Length, 1, Rate, false);
                    clips[i].SetData(pending[i], 0);
                }
                pending = null;
            }
            if (clips[0] == null) return;

            var o = SaveSystem.Options;
            float target = o.music ? BaseVolume * o.volume : 0f;
            var mood = Desired();
            if (!started || mood != current)
            {
                // Troca: a faixa nova entra em b; depois b vira a principal.
                (a, b) = (b, a);
                a.clip = clips[(int)mood];
                a.volume = 0f;
                a.Play();
                current = mood;
                started = true;
            }
            float step = Time.unscaledDeltaTime / FadeSeconds;
            a.volume = Mathf.MoveTowards(a.volume, target, step * BaseVolume);
            b.volume = Mathf.MoveTowards(b.volume, 0f, step * BaseVolume);
            if (b.volume <= 0f && b.isPlaying) b.Stop();
        }

        MusicMood Desired()
        {
            var g = ArenaGame.Instance;
            if (!g || g.Mode == ArenaGame.GameMode.Menu || g.Mode == ArenaGame.GameMode.Santuario || g.Mode == ArenaGame.GameMode.Treino)
                return MusicMood.Santuario;
            if (g.Mode == ArenaGame.GameMode.Dungeon && g.Dungeon.Active && g.Dungeon.Run.ActiveRoom != null) return MusicMood.Combate;
            var feet = g.Player.FeetPosition;
            foreach (var e in ArenaEnemy.All)
                if (e && !e.Dead && (e.transform.position - feet).sqrMagnitude < CombatRange * CombatRange) return MusicMood.Combate;
            return MusicMood.Exploracao;
        }
    }
}
