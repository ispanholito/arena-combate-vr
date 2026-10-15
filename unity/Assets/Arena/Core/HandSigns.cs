using System;

namespace Arena.Core
{
    // Formato da mão esquerda livre, a partir de quais dedos (indicador, médio, anelar, mínimo) estão esticados.
    public enum HandShape { Nenhuma, Aberta, Punho, Apontar, V, TresDedos, Chifres, Outra }

    // Poderes da mão. Cada um precisa ser ARMADO com um sinal antes do gesto que dispara.
    public enum HandPower { Nenhum, Metralhadora, SocoNoChao, CorteDaPalma }

    public static class HandSigns
    {
        public static HandShape Classify(bool index, bool middle, bool ring, bool little)
        {
            int mask = (index ? 1 : 0) | (middle ? 2 : 0) | (ring ? 4 : 0) | (little ? 8 : 0);
            switch (mask)
            {
                case 0b1111: return HandShape.Aberta;
                case 0b0000: return HandShape.Punho;
                case 0b0001: return HandShape.Apontar;
                case 0b0011: return HandShape.V;
                case 0b0111: return HandShape.TresDedos;
                case 0b1001: return HandShape.Chifres;
                default: return HandShape.Outra;
            }
        }

        // Sinal que arma cada poder, e gesto que dispara depois:
        //   V (indicador + médio)          → Metralhadora, dispara apontando o indicador
        //   Chifres (indicador + mínimo)   → Soco no chão, dispara com o punho golpeando para baixo
        //   Três dedos (sem o mínimo)      → Corte da palma, dispara com a mão aberta em movimento rápido
        public static HandPower PowerForSign(HandShape s) => s switch
        {
            HandShape.V => HandPower.Metralhadora,
            HandShape.Chifres => HandPower.SocoNoChao,
            HandShape.TresDedos => HandPower.CorteDaPalma,
            _ => HandPower.Nenhum,
        };

        public static HandShape TriggerShape(HandPower p) => p switch
        {
            HandPower.Metralhadora => HandShape.Apontar,
            HandPower.SocoNoChao => HandShape.Punho,
            HandPower.CorteDaPalma => HandShape.Aberta,
            _ => HandShape.Nenhuma,
        };

        public static string Name(HandPower p) => p switch
        {
            HandPower.Metralhadora => "Metralhadora",
            HandPower.SocoNoChao => "Soco no chão",
            HandPower.CorteDaPalma => "Corte da palma",
            _ => "",
        };
    }

    // Máquina de estados do sinal: segurar o sinal por SignHoldSeconds arma o poder por ArmedSeconds.
    public sealed class HandSignArming
    {
        public const float SignHoldSeconds = 0.25f;
        public const float ArmedSeconds = 2.5f;

        public HandPower Armed { get; private set; }
        public float ArmedTimeLeft { get; private set; }
        HandShape lastShape;
        float shapeTime;

        public event Action<HandPower> PowerArmed;

        public void Update(HandShape shape, float dt)
        {
            if (shape != lastShape) { lastShape = shape; shapeTime = 0f; } else shapeTime += dt;

            var p = HandSigns.PowerForSign(shape);
            if (p != HandPower.Nenhum && shapeTime >= SignHoldSeconds && (Armed != p || ArmedTimeLeft < ArmedSeconds - 0.01f))
            {
                bool fresh = Armed != p;
                Armed = p;
                ArmedTimeLeft = ArmedSeconds;
                if (fresh) PowerArmed?.Invoke(p);
                return;
            }
            if (Armed != HandPower.Nenhum)
            {
                ArmedTimeLeft -= dt;
                if (ArmedTimeLeft <= 0f) { Armed = HandPower.Nenhum; ArmedTimeLeft = 0f; }
            }
        }

        // O gesto de disparo está sendo feito com o poder armado?
        public bool IsTriggering(HandPower p, HandShape shape) => Armed == p && shape == HandSigns.TriggerShape(p);

        // Mantém armado enquanto o poder contínuo (metralhadora) está sendo usado.
        public void Sustain() { if (Armed != HandPower.Nenhum) ArmedTimeLeft = Math.Max(ArmedTimeLeft, 0.4f); }

        public void Consume() { Armed = HandPower.Nenhum; ArmedTimeLeft = 0f; }
    }
}
