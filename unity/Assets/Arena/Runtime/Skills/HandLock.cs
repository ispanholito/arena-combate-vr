using UnityEngine;

namespace Arena
{
    // Trava de mão (pedido do Pablo, 2026-10-28: "quando estiver usando uma coisa, não consigo usar outra"). Cada poder
    // da mão livre segura a mão enquanto está em uso (Hold a cada quadro) e por um instante depois de soltar (o
    // movimento de arremessar a bola de fogo ou empurrar o Kamehameha não pode virar corte da palma). Magias de duas
    // mãos seguram as duas. Uma mão ocupada não impede a outra: dá para segurar a teia numa e usar poder na outra.
    // Donos: "mao" (raio, soco, palma, agarrar), "fogo" (bola de fogo), "duas" (palma batida, raízes, Kamehameha),
    // "teia".
    public static class HandLock
    {
        static readonly string[] owner = new string[2];
        static readonly float[] until = new float[2];

        public const float AfterCast = 0.6f;   // depois de soltar um poder, a mão fica com ele mais este tempo

        // Mão livre para este dono (ninguém segurando, ou já é dele).
        public static bool Free(int side, string who) => owner[side] == null || owner[side] == who || Time.time > until[side];

        // Segura a mão por mais 'seconds' (chamado a cada quadro enquanto o poder está ativo).
        public static void Hold(int side, string who, float seconds = 0.15f)
        {
            if (owner[side] != who || Time.time > until[side]) { owner[side] = who; until[side] = Time.time + seconds; return; }
            until[side] = Mathf.Max(until[side], Time.time + seconds);
        }

        public static bool BothFree(string who) => Free(0, who) && Free(1, who);
        public static void HoldBoth(string who, float seconds = 0.15f) { Hold(0, who, seconds); Hold(1, who, seconds); }

        public static void Clear() { owner[0] = owner[1] = null; until[0] = until[1] = 0f; }
    }
}
