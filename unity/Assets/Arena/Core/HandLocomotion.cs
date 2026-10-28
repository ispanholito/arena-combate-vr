namespace Arena.Core
{
    // Andar sem controle com o sinal do Homem-Aranha (indicador + mindinho esticados, médio e anelar dobrados —
    // o "Chifres" do HandSigns), ideia do Pablo (2026-10-28):
    //   - sinal duas vezes seguidas = dash para trás;
    //   - sinal uma vez apontando para cima = dispara uma teia; mão fechada puxando = o jogador é puxado pela teia.
    public enum SpiderAction { None, DashBack, Rope }

    public sealed class SpiderSignDetector
    {
        public const float DoubleWindow = 0.6f;   // segundo sinal até 0,6 s depois do primeiro = dash para trás
        public const float PointUpDot = 0.7f;     // dedos apontando para cima (cos ~45° da vertical)
        public const float PointUpHold = 0.12f;   // segurar apontado para cima esse tempo dispara a teia

        float sinceOnset = 99f, upT;
        bool wasSign, used;

        // sign: a mão está no sinal agora; upDot: quanto o indicador aponta para cima (−1..1).
        public SpiderAction Update(bool sign, float upDot, float dt)
        {
            sinceOnset += dt;
            var act = SpiderAction.None;
            if (sign && !wasSign)
            {
                if (sinceOnset <= DoubleWindow) { act = SpiderAction.DashBack; used = true; sinceOnset = 99f; }
                else { sinceOnset = 0f; used = false; }
                upT = 0f;
            }
            if (sign && !used)
            {
                upT = upDot >= PointUpDot ? upT + dt : 0f;
                if (upT >= PointUpHold) { act = SpiderAction.Rope; used = true; sinceOnset = 99f; }
            }
            wasSign = sign;
            return act;
        }
    }

    public static class RopeRules
    {
        public const float Range = 20f;                  // alcance da teia (teto, parede, pilar)
        public const float VirtualAnchorDistance = 12f;  // sem nada no caminho (céu aberto): prende no ar a 12 m
        public const float PullGain = 2.5f;              // cada 1 cm de puxão da mão = 2,5 cm de subida pela teia
        public const float MaxSeconds = 8f;              // a teia se desfaz depois disso
        public const float ReleaseOpenSeconds = 0.25f;   // mão aberta esse tempo (depois de segurar) = solta
        public const float GrabTimeout = 3f;             // se não fechar a mão em 3 s, a teia se desfaz
        public const float ArriveDistance = 0.6f;        // não puxa mais quando a mão chega perto do ponto
        public const float MaxHeight = 4.5f;             // não passa do teto das salas
        public const float Gravity = 9.81f;
    }
}
