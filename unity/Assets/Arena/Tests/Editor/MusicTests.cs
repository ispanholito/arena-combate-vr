using System;
using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class MusicTests
    {
        [Test]
        public void Music_IsFinite_Bounded_AndLoopsWithoutClick()
        {
            const int rate = 8000;
            foreach (MusicMood m in Enum.GetValues(typeof(MusicMood)))
            {
                var s = MusicSynth.Generate(m, rate);
                Assert.AreEqual((int)(MusicSynth.BarSeconds(m) * 4f * rate), s.Length);
                float peak = 0f, energy = 0f;
                foreach (var v in s) { Assert.IsFalse(float.IsNaN(v)); peak = Math.Max(peak, Math.Abs(v)); energy += v * v; }
                Assert.LessOrEqual(peak, 0.9001f);
                Assert.Greater(energy / s.Length, 1e-4f);   // não é silêncio
                // Emenda do loop: o salto entre a última e a primeira amostra é pequeno.
                Assert.Less(Math.Abs(s[s.Length - 1] - s[0]), 0.2f, m.ToString());
            }
        }
    }
}
