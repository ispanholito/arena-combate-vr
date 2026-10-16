using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public enum Sfx
    {
        Hit, HitHeavy, Parry, Block, Stab, Slime, Hurt, Dash, Level, Broken, Whoosh, Skill, FireCast, Explosion, Ice,
        Wind, Roar, Click, Deny, Impact, Bone, Pickup, Shot, Slam, Windup, Death, Rip, Rattle, Step,
    }

    // Efeitos sonoros sintetizados por código na inicialização (sem arquivos de áudio), com a mesma técnica do protótipo
    // web: camadas de tom (seno/triângulo/quadrada/serra com varredura de frequência) e ruído filtrado, com envelope.
    // Tocados em 3D na posição do evento, com pequena variação de tom para não cansar.
    public sealed class ArenaAudio : MonoBehaviour
    {
        public static float MasterVolume = 0.8f;
        const int Rate = 22050;

        enum Wave { Sine, Triangle, Square, Saw }

        readonly struct Layer
        {
            public readonly bool IsNoise; public readonly Wave Wave;
            public readonly float F, F2, Dur, Vol, Delay, Cutoff;
            public Layer(Wave w, float f, float dur, float vol, float f2 = 0f, float delay = 0f)
            { IsNoise = false; Wave = w; F = f; F2 = f2 > 0f ? f2 : f; Dur = dur; Vol = vol; Delay = delay; Cutoff = 0f; }
            public Layer(float dur, float vol, float cutoff, float delay = 0f)
            { IsNoise = true; Wave = Wave.Sine; F = F2 = 0f; Dur = dur; Vol = vol; Delay = delay; Cutoff = cutoff; }
        }

        static ArenaAudio inst;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;

        static ArenaAudio I
        {
            get
            {
                if (inst == null) inst = new GameObject("ArenaAudio").AddComponent<ArenaAudio>();
                return inst;
            }
        }

        void Awake()
        {
            inst = this;
            Build(Sfx.Hit, N(0.12f, 0.35f, 1200), T(Wave.Triangle, 140, 0.12f, 0.3f, 60));
            Build(Sfx.HitHeavy, N(0.18f, 0.45f, 700), T(Wave.Triangle, 90, 0.2f, 0.4f, 40));
            Build(Sfx.Parry, T(Wave.Square, 1250, 0.35f, 0.12f), T(Wave.Square, 1870, 0.3f, 0.08f), N(0.08f, 0.3f, 6000));
            Build(Sfx.Block, T(Wave.Square, 600, 0.15f, 0.1f), N(0.1f, 0.3f, 3500), T(Wave.Triangle, 220, 0.12f, 0.15f, 120));
            Build(Sfx.Stab, N(0.18f, 0.35f, 500), T(Wave.Sine, 180, 0.2f, 0.25f, 70));
            Build(Sfx.Slime, T(Wave.Sine, 420, 0.25f, 0.25f, 120));
            Build(Sfx.Hurt, T(Wave.Saw, 110, 0.3f, 0.25f, 50), N(0.15f, 0.25f, 500));
            Build(Sfx.Dash, N(0.18f, 0.15f, 2500));
            Build(Sfx.Level, T(Wave.Triangle, 523, 0.25f, 0.18f), T(Wave.Triangle, 659, 0.25f, 0.18f, 0, 0.09f),
                             T(Wave.Triangle, 784, 0.25f, 0.18f, 0, 0.18f), T(Wave.Triangle, 1046, 0.3f, 0.18f, 0, 0.27f));
            Build(Sfx.Broken, T(Wave.Saw, 300, 0.4f, 0.2f, 80), N(0.2f, 0.3f, 3000));
            Build(Sfx.Whoosh, N(0.15f, 0.1f, 1800));
            Build(Sfx.Skill, T(Wave.Triangle, 500, 0.2f, 0.15f, 1200), N(0.2f, 0.15f, 4000));
            Build(Sfx.FireCast, T(Wave.Saw, 300, 0.3f, 0.12f, 600), N(0.15f, 0.08f, 1800));
            Build(Sfx.Explosion, N(0.4f, 0.4f, 900), T(Wave.Saw, 90, 0.45f, 0.22f, 40));
            Build(Sfx.Ice, T(Wave.Sine, 1800, 0.25f, 0.12f, 900), N(0.1f, 0.15f, 7000));
            Build(Sfx.Wind, N(0.4f, 0.3f, 1500));
            Build(Sfx.Roar, T(Wave.Saw, 70, 1.2f, 0.3f, 40), T(Wave.Square, 105, 1.2f, 0.12f, 60), N(0.8f, 0.2f, 600));
            Build(Sfx.Click, T(Wave.Square, 900, 0.06f, 0.08f));
            Build(Sfx.Deny, T(Wave.Square, 160, 0.15f, 0.1f));
            Build(Sfx.Impact, N(0.25f, 0.4f, 350), T(Wave.Sine, 80, 0.3f, 0.3f, 40));
            Build(Sfx.Bone, N(0.06f, 0.35f, 3500), T(Wave.Square, 900, 0.05f, 0.08f, 600), T(Wave.Triangle, 300, 0.08f, 0.12f, 200));
            Build(Sfx.Pickup, T(Wave.Triangle, 660, 0.12f, 0.15f), T(Wave.Triangle, 990, 0.18f, 0.15f, 0, 0.07f));
            Build(Sfx.Shot, T(Wave.Square, 900, 0.07f, 0.06f, 300), N(0.04f, 0.08f, 4000));
            Build(Sfx.Slam, N(0.5f, 0.5f, 400), T(Wave.Saw, 55, 0.6f, 0.3f, 30));
            Build(Sfx.Windup, T(Wave.Saw, 220, 0.15f, 0.12f, 120));
            Build(Sfx.Death, T(Wave.Saw, 300, 0.45f, 0.18f, 90), N(0.25f, 0.15f, 800));
            Build(Sfx.Rip, N(0.25f, 0.35f, 900), T(Wave.Saw, 160, 0.25f, 0.15f, 60));
            Build(Sfx.Rattle, N(0.05f, 0.25f, 4000), N(0.05f, 0.25f, 4000, 0.07f), N(0.05f, 0.2f, 4000, 0.14f), N(0.05f, 0.15f, 4000, 0.21f));
            Build(Sfx.Step, N(0.07f, 0.25f, 600), T(Wave.Sine, 90, 0.08f, 0.15f, 60));
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("Som" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 1.5f; s.maxDistance = 30f;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
        }

        static Layer T(Wave w, float f, float d, float v, float f2 = 0f, float delay = 0f) => new Layer(w, f, d, v, f2, delay);
        static Layer N(float d, float v, float cutoff, float delay = 0f) => new Layer(d, v, cutoff, delay);

        void Build(Sfx id, params Layer[] layers)
        {
            float len = 0f;
            foreach (var l in layers) len = Mathf.Max(len, l.Delay + l.Dur);
            int n = Mathf.CeilToInt(len * Rate) + 1;
            var data = new float[n];
            var rng = new System.Random((int)id * 7919 + 1);
            foreach (var l in layers)
            {
                int start = Mathf.FloorToInt(l.Delay * Rate);
                int count = Mathf.CeilToInt(l.Dur * Rate);
                float phase = 0f, lp = 0f;
                float alpha = l.IsNoise ? 1f - Mathf.Exp(-2f * Mathf.PI * l.Cutoff / Rate) : 0f;
                for (int i = 0; i < count && start + i < n; i++)
                {
                    float t = i / (float)Rate, k = t / l.Dur;
                    float s;
                    if (l.IsNoise)
                    {
                        lp += alpha * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);   // passa-baixa de 1 polo
                        s = lp * 2f * (1f - k);
                    }
                    else
                    {
                        float f = l.F * Mathf.Pow(l.F2 / l.F, k);                         // varredura exponencial
                        phase += f / Rate;
                        float ph = phase - Mathf.Floor(phase);
                        s = l.Wave switch
                        {
                            Wave.Sine => Mathf.Sin(ph * 2f * Mathf.PI),
                            Wave.Triangle => 1f - 4f * Mathf.Abs(ph - 0.5f),
                            Wave.Square => ph < 0.5f ? 1f : -1f,
                            _ => 2f * ph - 1f,
                        };
                        s *= Mathf.Pow(0.001f, k);                                            // decaimento exponencial
                    }
                    data[start + i] += s * l.Vol;
                }
            }
            float peak = 0f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.95f) for (int i = 0; i < n; i++) data[i] *= 0.95f / peak;
            var clip = AudioClip.Create(id.ToString(), n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[id] = clip;
        }

        // Toca na posição (3D). volume 0..1, pitch base com variação aleatória.
        public static void Play(Sfx id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var a = I;
            if (!a.clips.TryGetValue(id, out var clip)) return;
            var s = a.pool[a.next];
            a.next = (a.next + 1) % a.pool.Count;
            s.transform.position = position;
            s.spatialBlend = 1f;
            s.pitch = pitch * Random.Range(0.92f, 1.08f);
            s.volume = Mathf.Clamp01(volume) * MasterVolume * 2.2f;
            s.clip = clip;
            s.Play();
        }

        // Toca sem posição (interface, sons do próprio jogador).
        public static void Play2D(Sfx id, float volume = 1f, float pitch = 1f)
        {
            var a = I;
            if (!a.clips.TryGetValue(id, out var clip)) return;
            var s = a.pool[a.next];
            a.next = (a.next + 1) % a.pool.Count;
            s.spatialBlend = 0f;
            s.pitch = pitch * Random.Range(0.96f, 1.04f);
            s.volume = Mathf.Clamp01(volume) * MasterVolume;
            s.clip = clip;
            s.Play();
        }
    }
}
