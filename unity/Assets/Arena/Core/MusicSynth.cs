using System;

namespace Arena.Core
{
    public enum MusicMood { Santuario, Exploracao, Combate }

    // Música ambiente sintetizada por código (sem arquivos): drone grave + acordes suaves em 4 compassos, e no combate
    // um pulso de tambor. Sai em loop sem emenda (o fim é misturado no começo).
    public static class MusicSynth
    {
        const float Tau = (float)(Math.PI * 2);

        // Progressões (semitons a partir da tônica) — santuário em maior, dungeon em menor.
        static readonly int[][] CalmChords = { new[] { 0, 4, 7 }, new[] { 5, 9, 12 }, new[] { -3, 0, 4 }, new[] { 7, 11, 14 } };
        static readonly int[][] DarkChords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -5, -1, 2 } };

        public static float RootHz(MusicMood m) => m == MusicMood.Santuario ? 73.42f : 55f;   // ré1 / lá1
        public static float BarSeconds(MusicMood m) => m == MusicMood.Combate ? 3f : 4.5f;

        static float Note(float root, int semis) => root * (float)Math.Pow(2, semis / 12.0);

        public static float[] Generate(MusicMood mood, int rate, int seed = 1)
        {
            float bar = BarSeconds(mood);
            float length = bar * 4f;
            const float xfade = 1f;
            int n = (int)(length * rate), extra = (int)(xfade * rate);
            var buf = new float[n + extra];
            var rng = new Random(seed);
            var chords = mood == MusicMood.Santuario ? CalmChords : DarkChords;
            float root = RootHz(mood);
            float lp = 0f;

            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)rate;
                int barIdx = (int)(t / bar) % 4;
                float inBar = t % bar;
                // Drone: tônica + quinta, com respiração lenta.
                float breath = 0.75f + 0.25f * (float)Math.Sin(Tau * t / (bar * 2f));
                float s = 0.22f * breath * (float)Math.Sin(Tau * root * t) + 0.1f * (float)Math.Sin(Tau * root * 1.5f * t);
                // Acorde do compasso (oitava acima), ataque e queda suaves.
                float env = Math.Min(1f, inBar / 1.2f) * Math.Min(1f, (bar - inBar) / 0.8f);
                foreach (int semi in chords[barIdx])
                {
                    float f = Note(root * 4f, semi);
                    float ph = (f * t) % 1f;
                    s += 0.05f * env * (1f - 4f * Math.Abs(ph - 0.5f));   // triângulo
                }
                // Ruído de vento filtrado (ambiente de caverna).
                lp += 0.02f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                s += lp * (mood == MusicMood.Santuario ? 0.15f : 0.35f);
                // Combate: tambor grave a cada meio compasso/4.
                if (mood == MusicMood.Combate)
                {
                    float beat = bar / 4f;
                    float tb = t % beat;
                    float kick = (float)Math.Exp(-tb * 18f) * (float)Math.Sin(Tau * (50f + 70f * (float)Math.Exp(-tb * 30f)) * tb);
                    float accent = ((int)(t / beat) % 4 == 0) ? 1f : 0.6f;
                    s += 0.45f * accent * kick;
                }
                buf[i] = s;
            }
            // Loop sem emenda: o trecho depois do fim entra misturado no começo.
            var outBuf = new float[n];
            Array.Copy(buf, outBuf, n);
            for (int i = 0; i < extra; i++)
            {
                float k = i / (float)extra;
                outBuf[i] = outBuf[i] * k + buf[n + i] * (1f - k);
            }
            float peak = 0f;
            foreach (var v in outBuf) peak = Math.Max(peak, Math.Abs(v));
            if (peak > 0.9f) for (int i = 0; i < n; i++) outBuf[i] *= 0.9f / peak;
            return outBuf;
        }
    }
}
