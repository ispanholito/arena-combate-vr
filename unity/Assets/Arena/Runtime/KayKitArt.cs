using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    // Arte do KayKit (Kay Lousberg, CC0 — kaylousberg.com), 1º teste do visual (2026-10-28): a sala do Treino montada
    // com as peças da "Dungeon Remastered". (O esqueleto do KayKit foi testado e descartado: o Pablo não gostou do
    // design; o corte por osso continua em Editor/KayKitImport para um modelo nosso no futuro.) As peças da dungeon seguem uma grade de 4 m (parede 4×4×1, piso 4×4, pilar 1,5×4).
    public static class KayKitArt
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Model(string path)
        {
            if (!cache.TryGetValue(path, out var go)) cache[path] = go = Resources.Load<GameObject>("KayKit/" + path);
            return go;
        }

        // Instancia uma peça da dungeon (sem colisores: a colisão do jogo continua nas formas simples).
        public static GameObject Place(string name, Transform parent, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var prefab = Model("Dungeon/" + name);
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return go;
        }

        // Sala do Treino: piso de pedra, paredes com tochas e estandartes, pilares do KayKit no lugar dos pilares da
        // arena e alguns objetos de canto. Tudo é juntado em lote (poucas chamadas de desenho para o Quest 2).
        public static Transform BuildTrainingHall(Transform parent, float half, IEnumerable<Transform> pillars, IEnumerable<Vector3> torchLights)
        {
            if (!Model("Dungeon/wall")) return null;
            var root = new GameObject("SalaTreinoKayKit").transform;
            root.SetParent(parent, false);
            const float Tile = 4f;
            int n = Mathf.CeilToInt(half * 2f / Tile);           // 7 peças de 4 m cobrem 28 m
            float start = -(n - 1) * Tile * 0.5f;
            // Piso: pedra grande, com algumas variações quebradas/com mato.
            var rng = new System.Random(7);
            string[] floors = { "floor_tile_large", "floor_tile_large", "floor_tile_large", "floor_tile_large_rocks" };
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                    Place(floors[rng.Next(floors.Length)], root, new Vector3(start + x * Tile, 0f, start + z * Tile), 90f * rng.Next(4));
            // Paredes: face interna em ±half (a peça tem 1 m de espessura). Algumas rachadas/com janela gradeada.
            string[] walls = { "wall", "wall", "wall_cracked", "wall", "wall_archedwindow_gated", "wall", "wall_cracked" };
            for (int i = 0; i < n; i++)
            {
                float c = start + i * Tile;
                string w = walls[i % walls.Length];
                Place(w, root, new Vector3(c, 0f, -half - 0.5f), 0f);
                Place(w, root, new Vector3(c, 0f, half + 0.5f), 180f);
                Place(walls[(i + 3) % walls.Length], root, new Vector3(-half - 0.5f, 0f, c), 90f);
                Place(walls[(i + 3) % walls.Length], root, new Vector3(half + 0.5f, 0f, c), -90f);
            }
            foreach (var cx in new[] { -1f, 1f }) foreach (var cz in new[] { -1f, 1f })
                Place("wall_corner", root, new Vector3(cx * (half + 0.5f), 0f, cz * (half + 0.5f)), cx > 0 ? (cz > 0 ? 180f : -90f) : (cz > 0 ? 90f : 0f));
            // Segunda fileira de parede em cima (a arena tem 5 m de muro e a peça 4 m).
            for (int i = 0; i < n; i++)
            {
                float c = start + i * Tile;
                Place("wall_half", root, new Vector3(c, 4f, -half - 0.5f), 0f);
                Place("wall_half", root, new Vector3(c, 4f, half + 0.5f), 180f);
                Place("wall_half", root, new Vector3(-half - 0.5f, 4f, c), 90f);
                Place("wall_half", root, new Vector3(half + 0.5f, 4f, c), -90f);
            }
            // Pilares do KayKit no lugar dos cilindros (1,5 × 4 m → 1,1 × 5 m).
            foreach (var p in pillars)
                Place("pillar_decorated", root, new Vector3(p.position.x, 0f, p.position.z), 0f, new Vector3(0.8f, 1.25f, 0.8f));
            // Estandartes entre as tochas e tochas de parede nas luzes que já existem.
            string[] banners = { "banner_patternA_red", "banner_shield_red", "banner_triple_red", "banner_patternB_red" };
            int bi = 0;
            foreach (float c in new[] { -10f, -2f, 2f, 10f })
            {
                Place(banners[bi++ % banners.Length], root, new Vector3(c, 0.3f, -half + 0.05f), 0f);
                Place(banners[bi++ % banners.Length], root, new Vector3(c, 0.3f, half - 0.05f), 180f);
            }
            // Cantos: barris, caixas, armas encostadas; perto da fogueira, mesa com velas.
            Place("barrel_large", root, new Vector3(-half + 1.2f, 0f, -half + 1.2f), 20f);
            Place("barrel_small_stack", root, new Vector3(-half + 2.6f, 0f, -half + 1f), 0f);
            Place("crates_stacked", root, new Vector3(half - 1.4f, 0f, -half + 1.4f), 15f);
            Place("box_large", root, new Vector3(half - 1.2f, 0f, half - 1.2f), -30f);
            Place("barrel_large_decorated", root, new Vector3(-half + 1.2f, 0f, half - 1.2f), 0f);
            Place("sword_shield", root, new Vector3(-half + 0.6f, 0f, -3f), 90f);
            Place("sword_shield_broken", root, new Vector3(half - 0.6f, 0f, 3f), -90f);
            Place("table_medium_decorated_A", root, new Vector3(3f, 0f, 9.5f), 0f);
            Place("candle_triple", root, new Vector3(3f, 1f, 9.5f), 0f);
            Place("chest", root, new Vector3(-3f, 0f, 10f), 180f);
            Place("rubble_large", root, new Vector3(half - 2f, 0f, -6f), 40f);
            foreach (var t in torchLights) PlaceTorch(root, t, half);
            StaticBatchingUtility.Combine(root.gameObject);
            return root;
        }

        public static void PlaceTorch(Transform parent, Vector3 lightPos, float half)
        {
            // A tocha de parede encosta na parede mais próxima e olha para o centro.
            var flat = new Vector3(lightPos.x, 0f, lightPos.z);
            float yaw = Mathf.Abs(flat.x) > Mathf.Abs(flat.z) ? (flat.x > 0 ? -90f : 90f) : (flat.z > 0 ? 180f : 0f);
            var wallPos = flat;
            if (Mathf.Abs(flat.x) > Mathf.Abs(flat.z)) wallPos.x = Mathf.Sign(flat.x) * half; else wallPos.z = Mathf.Sign(flat.z) * half;
            Place("torch_mounted", parent, wallPos + Vector3.up * (lightPos.y - 0.6f), yaw);
        }
    }
}
