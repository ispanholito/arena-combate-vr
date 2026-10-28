using System.Collections.Generic;
using UnityEngine;
using N = System.Numerics;

namespace Arena
{
    // Obstáculos redondos que a IA contorna (CombatSteering.Avoid): os pilares da arena (Arena e Treino). Na dungeon e no
    // santuário fica vazio (as salas são retângulos; as paredes já prendem pelo WalkArea).
    public static class NavObstacles
    {
        public static readonly List<(N.Vector2 c, float r)> Circles = new List<(N.Vector2, float)>();

        public static void SetFromArena(Transform arenaWorld, bool active)
        {
            Circles.Clear();
            if (!active || !arenaWorld) return;
            foreach (Transform t in arenaWorld)
                if (t.name == "Pilar")
                    Circles.Add((new N.Vector2(t.position.x, t.position.z), t.localScale.x * 0.5f));
        }
    }
}
