using System;

namespace Arena
{
    // Avisos de "o jogador acabou de fazer X" para o tutorial (e o que mais precisar ouvir). Ids: golpe, raio, soco,
    // palma, bola, palma_batida, raizes, kame, teia, puxar, dash_tras, pausa.
    public static class TutorialEvents
    {
        public static event Action<string> Fired;
        public static void Raise(string id) => Fired?.Invoke(id);
    }
}
