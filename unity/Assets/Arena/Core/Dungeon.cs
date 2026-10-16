using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena.Core
{
    // Retângulo no chão (plano XZ), em metros.
    public readonly struct Rect2
    {
        public readonly float MinX, MinZ, MaxX, MaxZ;
        public Rect2(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = Math.Min(minX, maxX); MaxX = Math.Max(minX, maxX);
            MinZ = Math.Min(minZ, maxZ); MaxZ = Math.Max(minZ, maxZ);
        }
        public static Rect2 Centered(float cx, float cz, float w, float d) => new Rect2(cx - w / 2f, cz - d / 2f, cx + w / 2f, cz + d / 2f);
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public bool Contains(float x, float z, float margin = 0f) =>
            x >= MinX + margin && x <= MaxX - margin && z >= MinZ + margin && z <= MaxZ - margin;
        public (float x, float z) Closest(float x, float z) => (Math.Clamp(x, MinX, MaxX), Math.Clamp(z, MinZ, MaxZ));
        public Rect2 Shrink(float m) => new Rect2(MinX + m, MinZ + m, MaxX - m, MaxZ - m);
    }

    // Área onde dá para andar: união de retângulos (salas e corredores). Tudo que anda (jogador, inimigos, loot)
    // é preso nela; as paredes são geradas nas bordas.
    public sealed class WalkArea
    {
        public readonly List<Rect2> Rects = new List<Rect2>();
        public WalkArea(IEnumerable<Rect2> rects) { Rects.AddRange(rects); }

        public bool Contains(float x, float z, float margin = 0f)
        {
            foreach (var r in Rects) if (r.Contains(x, z, margin)) return true;
            return false;
        }

        // Ponto mais próximo dentro da área.
        public (float x, float z) Clamp(float x, float z)
        {
            if (Contains(x, z)) return (x, z);
            float best = float.MaxValue; (float x, float z) res = (x, z);
            foreach (var r in Rects)
            {
                var c = r.Closest(x, z);
                float d = (c.x - x) * (c.x - x) + (c.z - z) * (c.z - z);
                if (d < best) { best = d; res = c; }
            }
            return res;
        }

        // Até onde se anda em linha reta antes de sair da área (passos de 0,2 m).
        public float RayReach(float x, float z, float dirX, float dirZ, float maxReach)
        {
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            if (len < 1e-5f) return 0f;
            dirX /= len; dirZ /= len;
            const float step = 0.2f;
            float d = 0f;
            while (d + step <= maxReach && Contains(x + dirX * (d + step), z + dirZ * (d + step))) d += step;
            return d;
        }

        // Paredes: bordas das células de 1 m ocupadas que fazem divisa com células vazias. Segmentos alinhados
        // (x0, z0, x1, z1) já juntados em trechos contínuos. Os retângulos devem ter coordenadas inteiras.
        public List<(float x0, float z0, float x1, float z1)> WallSegments()
        {
            var cells = new HashSet<(int, int)>();
            foreach (var r in Rects)
                for (int x = (int)Math.Floor(r.MinX); x < (int)Math.Ceiling(r.MaxX); x++)
                    for (int z = (int)Math.Floor(r.MinZ); z < (int)Math.Ceiling(r.MaxZ); z++)
                        cells.Add((x, z));
            var horiz = new Dictionary<(int line, int side), List<int>>();   // linha z (borda sul/norte)
            var vert = new Dictionary<(int line, int side), List<int>>();    // linha x (borda oeste/leste)
            void Add(Dictionary<(int, int), List<int>> d, int line, int side, int pos)
            {
                if (!d.TryGetValue((line, side), out var l)) d[(line, side)] = l = new List<int>();
                l.Add(pos);
            }
            foreach (var (x, z) in cells)
            {
                if (!cells.Contains((x, z - 1))) Add(horiz, z, -1, x);
                if (!cells.Contains((x, z + 1))) Add(horiz, z + 1, 1, x);
                if (!cells.Contains((x - 1, z))) Add(vert, x, -1, z);
                if (!cells.Contains((x + 1, z))) Add(vert, x + 1, 1, z);
            }
            var segs = new List<(float, float, float, float)>();
            foreach (var kv in horiz)
                foreach (var (a, b) in Runs(kv.Value)) segs.Add((a, kv.Key.line, b, kv.Key.line));
            foreach (var kv in vert)
                foreach (var (a, b) in Runs(kv.Value)) segs.Add((kv.Key.line, a, kv.Key.line, b));
            return segs;
        }

        static IEnumerable<(int a, int b)> Runs(List<int> cells)
        {
            cells.Sort();
            int start = cells[0], prev = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                if (cells[i] == prev + 1) { prev = cells[i]; continue; }
                yield return (start, prev + 1);
                start = prev = cells[i];
            }
            yield return (start, prev + 1);
        }
    }

    public enum RoomKind { Inicio, Combate, Fogueira, Escada, Chefe }

    public sealed class DungeonRoom
    {
        public int Index;
        public RoomKind Kind;
        public Rect2 Rect;
        public List<EnemyDef> Enemies = new List<EnemyDef>();
        public bool Entered, Cleared;
        public bool Visited;           // o jogador já pisou (minimapa)
        public bool HasChest;          // baú aparece quando a sala é limpa
        public bool ChestIsMimic;      // o baú é um mímico
        public int EnteredFrom = -1;   // sala anterior (para os inimigos nascerem longe da entrada)
    }

    public sealed class DungeonFloor
    {
        public int Number;
        public List<DungeonRoom> Rooms = new List<DungeonRoom>();
        public List<Rect2> Corridors = new List<Rect2>();
        public List<(float x, float z)> Traps = new List<(float, float)>();   // armadilhas de espinhos (centro da placa)
        public WalkArea Area;
        public DungeonRoom Start => Rooms[0];
        public DungeonRoom Last => Rooms[Rooms.Count - 1];

        public DungeonRoom RoomAt(float x, float z, float margin = 0.5f) => Rooms.FirstOrDefault(r => r.Rect.Contains(x, z, margin));

        // Aberturas da sala para os corredores (segmentos na borda da sala), onde as portas trancam na emboscada.
        public List<(float x0, float z0, float x1, float z1)> Openings(int roomIndex)
        {
            var res = new List<(float, float, float, float)>();
            var r = Rooms[roomIndex].Rect;
            foreach (int n in new[] { roomIndex - 1, roomIndex + 1 })
            {
                if (n < 0 || n >= Rooms.Count) continue;
                var c = Corridors[Math.Min(roomIndex, n)];
                var o = Rooms[n].Rect;
                if (c.Width <= DungeonGenerator.CorridorWidth + 0.01f)   // corredor vertical (largura em X)
                {
                    float z = o.CenterZ > r.CenterZ ? r.MaxZ : r.MinZ;
                    res.Add((c.MinX, z, c.MaxX, z));
                }
                else
                {
                    float x = o.CenterX > r.CenterX ? r.MaxX : r.MinX;
                    res.Add((x, c.MinZ, x, c.MaxZ));
                }
            }
            return res;
        }
    }

    // Gera os andares: salas numa caminhada sem repetição por uma grade (células de 18 m), ligadas por corredores
    // retos de 4 m. Andar 1: início (fogueira) → combates → escada. Andar 2: combates → fogueira → chefe.
    public static class DungeonGenerator
    {
        public const float Cell = 18f, CorridorWidth = 4f;   // inteiro par: bordas em metros inteiros (paredes certas)
        public const int Floors = 2;

        public static DungeonFloor Generate(int seed, int floor, int tier = 1)
        {
            var rng = new Random(seed * 31 + floor * 7919);
            bool last = floor >= Floors;
            int combats = last ? 3 : 4;
            var kinds = new List<RoomKind> { last ? RoomKind.Fogueira : RoomKind.Inicio };
            for (int i = 0; i < combats; i++) kinds.Add(RoomKind.Combate);
            if (last) { kinds.Add(RoomKind.Fogueira); kinds.Add(RoomKind.Chefe); }
            else kinds.Add(RoomKind.Escada);

            var cells = Walk(rng, kinds.Count);
            var f = new DungeonFloor { Number = floor };
            for (int i = 0; i < kinds.Count; i++)
            {
                var k = kinds[i];
                int w = k == RoomKind.Chefe ? 16 : k == RoomKind.Combate ? 10 + 2 * rng.Next(0, 3) : 8;
                int d = k == RoomKind.Chefe ? 16 : k == RoomKind.Combate ? 10 + 2 * rng.Next(0, 3) : 8;
                var (cx, cz) = (cells[i].x * Cell, cells[i].z * Cell);
                var room = new DungeonRoom { Index = i, Kind = k, Rect = Rect2.Centered(cx, cz, w, d), EnteredFrom = i - 1 };
                if (k == RoomKind.Combate) room.Enemies = RollEnemies(rng, floor, i, tier);
                if (k == RoomKind.Combate) room.HasChest = rng.NextDouble() < 0.4;
                if (room.HasChest) room.ChestIsMimic = rng.NextDouble() < MimicRules.Chance(floor);
                if (k == RoomKind.Chefe) room.Enemies.Add(EnemyDef.ReiGoblin());   // a vida extra vem da profundidade (ArcadeRules)
                if (k == RoomKind.Inicio || k == RoomKind.Fogueira || k == RoomKind.Escada) room.Cleared = true;
                f.Rooms.Add(room);
            }
            // Pelo menos um baú por andar.
            if (!f.Rooms.Any(r => r.HasChest)) f.Rooms.Last(r => r.Kind == RoomKind.Combate).HasChest = true;
            for (int i = 1; i < f.Rooms.Count; i++)
            {
                var a = f.Rooms[i - 1].Rect; var b = f.Rooms[i].Rect;
                float h = CorridorWidth / 2f;
                f.Corridors.Add(Math.Abs(a.CenterX - b.CenterX) < 0.1f
                    ? new Rect2(a.CenterX - h, Math.Min(a.CenterZ, b.CenterZ), a.CenterX + h, Math.Max(a.CenterZ, b.CenterZ))
                    : new Rect2(Math.Min(a.CenterX, b.CenterX), a.CenterZ - h, Math.Max(a.CenterX, b.CenterX), a.CenterZ + h));
            }
            f.Area = new WalkArea(f.Rooms.Select(r => r.Rect).Concat(f.Corridors));
            PlaceTraps(f, rng, floor);
            return f;
        }

        // Espinhos: 0-1 por sala de combate no andar 1, 1-2 no andar 2, longe do centro (onde aparece o baú); e às
        // vezes um no meio do corredor.
        static void PlaceTraps(DungeonFloor f, Random rng, int floor)
        {
            foreach (var room in f.Rooms)
            {
                if (room.Kind != RoomKind.Combate) continue;
                int n = floor <= 1 ? rng.Next(0, 2) : rng.Next(1, 3);
                var r = room.Rect.Shrink(2f);
                for (int i = 0; i < n; i++)
                {
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        float x = (float)Math.Round(r.MinX + rng.NextDouble() * r.Width), z = (float)Math.Round(r.MinZ + rng.NextDouble() * r.Depth);
                        if (Math.Abs(x - r.CenterX) < 2f && Math.Abs(z - r.CenterZ) < 2f) continue;
                        if (f.Traps.Any(t => Math.Abs(t.x - x) < 2f && Math.Abs(t.z - z) < 2f)) continue;
                        f.Traps.Add((x, z));
                        break;
                    }
                }
            }
            for (int i = 1; i < f.Rooms.Count; i++)
            {
                if (rng.NextDouble() >= 0.35) continue;
                var a = f.Rooms[i - 1].Rect; var b = f.Rooms[i].Rect;
                // Meio do vão entre as duas salas (fora das salas, dentro do corredor).
                float x = (a.CenterX + b.CenterX) / 2f, z = (a.CenterZ + b.CenterZ) / 2f;
                if (!f.Rooms.Any(r => r.Rect.Contains(x, z, -1f))) f.Traps.Add((x, z));
            }
        }

        // Caminhada sem repetir célula, preferindo seguir em frente (corredores longos e salas bem separadas).
        static List<(int x, int z)> Walk(Random rng, int count)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var path = new List<(int x, int z)> { (0, 0) };
                var used = new HashSet<(int, int)> { (0, 0) };
                var dirs = new[] { (0, 1), (1, 0), (0, -1), (-1, 0) };
                int dir = 0;
                while (path.Count < count)
                {
                    var options = new List<int>();
                    for (int k = 0; k < 4; k++)
                    {
                        var (px, pz) = path[path.Count - 1];
                        var n = (px + dirs[k].Item1, pz + dirs[k].Item2);
                        if (used.Contains(n) || k == 2 && path.Count == 1) continue;   // a primeira sala segue para frente (+Z)
                        options.Add(k);
                    }
                    if (options.Count == 0) break;
                    int pick = options.Contains(dir) && rng.NextDouble() < 0.55 ? dir : options[rng.Next(options.Count)];
                    dir = pick;
                    var (lx, lz) = path[path.Count - 1];
                    var next = (lx + dirs[pick].Item1, lz + dirs[pick].Item2);
                    path.Add(next); used.Add(next);
                }
                if (path.Count == count) return path;
            }
            return Enumerable.Range(0, count).Select(i => (0, i)).ToList();   // linha reta (nunca deve acontecer)
        }

        // Andar 1: slimes, goblins e alguns arqueiros. Andar 2: goblins, esqueletos, arqueiros e poucos slimes. Salas mais fundas têm mais inimigos.
        public static List<EnemyDef> RollEnemies(Random rng, int floor, int roomIndex, int tier = 1)
        {
            int n = Math.Min(6, 2 + floor + roomIndex / 2 + rng.Next(0, 2)) + ArcadeRules.ExtraEnemies(tier);
            var list = new List<EnemyDef>();
            for (int i = 0; i < n; i++)
            {
                double r = rng.NextDouble();
                if (floor <= 1) list.Add(r < 0.45 ? EnemyDef.Slime(1f) : r < 0.85 ? EnemyDef.Goblin : EnemyDef.Arqueiro);
                else list.Add(r < 0.12 ? EnemyDef.Slime(1f) : r < 0.4 ? EnemyDef.Goblin : r < 0.7 ? EnemyDef.Esqueleto : r < 0.88 ? EnemyDef.Arqueiro : EnemyDef.Orc);
            }
            return list;
        }
    }

    // Estado da descida: andar atual, salas, entrada em sala (dispara os inimigos), sala limpa, morte e vitória.
    public sealed class DungeonRun
    {
        public int Seed { get; }
        public int Tier { get; }   // profundidade do modo arcade
        public DungeonFloor Floor { get; private set; }
        public DungeonRoom ActiveRoom { get; private set; }   // sala em combate (inimigos vivos)
        public DungeonRoom CheckpointRoom { get; private set; }
        public bool Victory { get; private set; }

        public DungeonRun(int seed, int tier = 1)
        {
            Seed = seed; Tier = ArcadeRules.Clamp(tier);
            GoToFloor(1);
        }

        public void GoToFloor(int n)
        {
            Floor = DungeonGenerator.Generate(Seed, n, Tier);
            ActiveRoom = null;
            CheckpointRoom = Floor.Start;
        }

        public bool CanDescend => Floor.Number < DungeonGenerator.Floors;

        // O jogador está em (x, z). Se entrou numa sala nova com inimigos, devolve a sala (para nascerem).
        public DungeonRoom OnPlayerAt(float x, float z)
        {
            var room = Floor.RoomAt(x, z, 1f);
            if (room == null) return null;
            room.Visited = true;
            if (room.Kind == RoomKind.Fogueira || room.Kind == RoomKind.Inicio) CheckpointRoom = room;
            if (room.Entered || room.Cleared || ActiveRoom != null) return null;
            room.Entered = true;
            if (room.Enemies.Count == 0) { room.Cleared = true; return null; }
            ActiveRoom = room;
            return room;
        }

        // A sala em combate ficou sem inimigos vivos (quem conta é o jogo: slimes se dividem, o chefe invoca).
        public void OnRoomEmpty()
        {
            if (ActiveRoom == null) return;
            ActiveRoom.Cleared = true;
            ActiveRoom = null;
        }

        public void OnBossKilled()
        {
            Victory = true;
            OnRoomEmpty();
        }

        // Morreu: a sala em combate volta a ficar "não visitada" (os inimigos voltam quando entrar de novo).
        public void OnPlayerDied()
        {
            if (ActiveRoom != null) ActiveRoom.Entered = false;
            ActiveRoom = null;
        }
    }
}

namespace Arena.Core
{
    // Minimapa: encaixa o andar inteiro num quadrado (lado 1, centrado em 0) mantendo a proporção.
    public readonly struct MapProjection
    {
        public readonly float CenterX, CenterZ, Scale;

        public MapProjection(DungeonFloor f, float margin = 0.06f)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var r in f.Area.Rects)
            {
                minX = Math.Min(minX, r.MinX); minZ = Math.Min(minZ, r.MinZ);
                maxX = Math.Max(maxX, r.MaxX); maxZ = Math.Max(maxZ, r.MaxZ);
            }
            CenterX = (minX + maxX) / 2f; CenterZ = (minZ + maxZ) / 2f;
            float span = Math.Max(maxX - minX, maxZ - minZ);
            Scale = (1f - 2f * margin) / Math.Max(span, 1f);
        }

        // Mundo (x, z) → mapa (u, v), ambos em [-0,5; 0,5].
        public (float u, float v) ToMap(float x, float z) => ((x - CenterX) * Scale, (z - CenterZ) * Scale);

        // Mostrar a sala no mapa? Visitadas e as vizinhas das visitadas (a "névoa" esconde o resto).
        public static bool Revealed(DungeonFloor f, int i) =>
            f.Rooms[i].Visited || (i > 0 && f.Rooms[i - 1].Visited) || (i + 1 < f.Rooms.Count && f.Rooms[i + 1].Visited);
    }
}
