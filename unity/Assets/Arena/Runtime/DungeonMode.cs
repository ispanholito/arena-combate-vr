using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Dungeon de 2 andares gerada por código (DungeonGenerator no Core): salas ligadas por corredores, inimigos que
    // aparecem ao entrar em cada sala, fogueiras (checkpoint/cura), escada para o andar 2 e o Rei Goblin no fim.
    // Vencer abre um portal de volta ao santuário (menu). Geometria em primitivas, como o resto do protótipo.
    public sealed class DungeonMode : MonoBehaviour
    {
        public DungeonRun Run { get; private set; }
        public bool Active => Run != null;

        ArenaGame game;
        Transform root, portal;
        readonly List<Transform> bonfires = new List<Transform>();
        readonly List<GameObject> doors = new List<GameObject>();
        float portalCd, roomCheckCd;
        bool portalHome;
        Material stone, wall, wood, fire, portalMat;

        void Awake() => game = GetComponent<ArenaGame>();

        public void Begin(int seed, int bossesDefeated)
        {
            Run = new DungeonRun(seed, bossesDefeated);
            BuildFloor();
            game.Message("Dungeon — andar 1. Desça pela escada; o Rei Goblin espera no andar 2.", 3.5f);
        }

        public void End()
        {
            Run = null;
            if (root) Destroy(root.gameObject);
            root = null; portal = null;
            bonfires.Clear();
            ArenaUtil.Area = null;
        }

        void Mats()
        {
            if (stone) return;
            stone = ArenaUtil.Lit(new Color(0.3f, 0.28f, 0.31f));
            wall = ArenaUtil.Lit(new Color(0.24f, 0.22f, 0.27f));
            wood = ArenaUtil.Lit(new Color(0.3f, 0.19f, 0.1f));
            fire = ArenaUtil.Lit(new Color(1f, 0.48f, 0.1f), new Color(1.5f, 0.6f, 0.1f));
            portalMat = ArenaUtil.Lit(new Color(0.45f, 0.3f, 1f), new Color(0.6f, 0.35f, 1.4f));
        }

        // ---------- Geometria ----------
        void BuildFloor()
        {
            Mats();
            if (root) Destroy(root.gameObject);
            bonfires.Clear();
            root = new GameObject("Dungeon andar " + Run.Floor.Number).transform;
            var f = Run.Floor;
            ArenaUtil.Area = f.Area;

            // Arte do KayKit por cima das caixas simples (que continuam como colisão, invisíveis). Sem a arte, fica como era.
            bool art = KayKitArt.Model("Dungeon/wall");
            var artRoot = new GameObject("Arte").transform; artRoot.SetParent(root, false);
            var rng = new System.Random(Run.Floor.Number * 7919 + f.Rooms.Count);
            foreach (var r in f.Area.Rects)
            {
                var floor = Box("Chao", new Vector3(r.CenterX, -0.05f, r.CenterZ), new Vector3(r.Width, 0.1f, r.Depth), stone);
                if (art) { floor.GetComponent<Renderer>().enabled = false; FloorTiles(artRoot, r.MinX, r.MinZ, r.MaxX, r.MaxZ, rng); }
            }
            foreach (var (x0, z0, x1, z1) in f.Area.WallSegments())
            {
                bool alongX = Mathf.Approximately(z0, z1);
                float len = alongX ? x1 - x0 : z1 - z0;
                // A parede fica do lado de fora da borda (0,15 m), para não roubar espaço de andar.
                var center = new Vector3((x0 + x1) * 0.5f, 2f, (z0 + z1) * 0.5f);
                center += OutsideNormal(f.Area, x0, z0, x1, z1) * 0.15f;
                var wallBox = Box("Parede", center, alongX ? new Vector3(len + 0.3f, 4f, 0.3f) : new Vector3(0.3f, 4f, len + 0.3f), wall);
                if (art) { wallBox.GetComponent<Renderer>().enabled = false; WallPieces(artRoot, f.Area, x0, z0, x1, z1, alongX, len, rng); }
                else Torches(f.Area, x0, z0, x1, z1, alongX, len);
            }
            foreach (var room in f.Rooms)
            {
                var c = new Vector3(room.Rect.CenterX, 0f, room.Rect.CenterZ);
                // Luz da sala (tocha alta) — uma por sala para caber no orçamento do Quest.
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = c + Vector3.up * 3.4f;
                l.type = LightType.Point;
                l.range = Mathf.Max(room.Rect.Width, room.Rect.Depth) * 0.95f;
                l.intensity = room.Kind == RoomKind.Chefe ? 2.8f : 2.4f;
                l.color = room.Kind == RoomKind.Chefe ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.6f, 0.3f);
                if (room.Kind == RoomKind.Inicio || room.Kind == RoomKind.Fogueira) bonfires.Add(Bonfire(c + Vector3.back * 1.5f, room.Index));
                if (room.Kind == RoomKind.Escada) SpawnPortal(c, home: false);
                // Pontos de gancho da teia: cantos no alto das paredes e o centro do "teto".
                var rr = room.Rect.Shrink(0.7f);
                foreach (var (gx, gz) in new[] { (rr.MinX, rr.MinZ), (rr.MaxX, rr.MinZ), (rr.MinX, rr.MaxZ), (rr.MaxX, rr.MaxZ) })
                    GrapplePoint.Spawn(root, new Vector3(gx, 3.6f, gz));
                GrapplePoint.Spawn(root, c + Vector3.up * 4.6f);
                if (art) CornerProps(artRoot, room.Rect, rng);
            }
            if (art) StaticBatchingUtility.Combine(artRoot.gameObject);
            for (int i = 0; i < f.Traps.Count; i++)
                SpikeTrap.Spawn(new Vector3(f.Traps[i].x, 0f, f.Traps[i].z), root, i * 0.9f);   // ciclos defasados
            SetCheckpoint(Run.CheckpointRoom);
            game.Player.TeleportTo(game.BonfireStandPoint, new Vector3(f.Start.Rect.CenterX, 0f, f.Start.Rect.CenterZ + 10f));
        }

        // ---------- Arte do KayKit (peças de 4 m esticadas para caber em cada tamanho) ----------
        static readonly string[] FloorPieces = { "floor_tile_large", "floor_tile_large", "floor_tile_large", "floor_tile_large_rocks" };
        // Sem janela nem parede quebrada: atrás da parede da dungeon é o vazio.
        static readonly string[] WallVariants = { "wall", "wall", "wall_cracked", "wall" };

        static void FloorTiles(Transform parent, float x0, float z0, float x1, float z1, System.Random rng)
        {
            float w = x1 - x0, d = z1 - z0;
            int nx = Mathf.Max(1, Mathf.CeilToInt(w / 4f - 0.01f)), nz = Mathf.Max(1, Mathf.CeilToInt(d / 4f - 0.01f));
            float sx = w / (nx * 4f), sz = d / (nz * 4f);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector3(x0 + (i + 0.5f) * w / nx, 0f, z0 + (j + 0.5f) * d / nz);
                    // Gira só de 180 em 180 quando a peça é esticada (a escala é em x/z do mundo).
                    KayKitArt.Place(FloorPieces[rng.Next(FloorPieces.Length)], parent, p, rng.Next(2) * 180f, new Vector3(sx, 1f, sz));
                }
        }

        void WallPieces(Transform parent, WalkArea area, float x0, float z0, float x1, float z1, bool alongX, float len, System.Random rng)
        {
            var inward = -OutsideNormal(area, x0, z0, x1, z1);
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;   // a face decorada olha para dentro da sala
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
            float scale = len / (n * 4f);
            for (int i = 0; i < n; i++)
            {
                float s = (i + 0.5f) / n;
                var edge = new Vector3(Mathf.Lerp(x0, x1, s), 0f, Mathf.Lerp(z0, z1, s));
                string piece = len < 3f ? "wall" : WallVariants[rng.Next(WallVariants.Length)];
                KayKitArt.Place(piece, parent, edge - inward * 0.5f, yaw, new Vector3(scale, 1f, 1f));
                // Tocha de parede a cada duas peças (só a chama; a luz de verdade é 1 por sala).
                if (len >= 5f && i % 2 == 0) KayKitArt.Place("torch_mounted", parent, edge + Vector3.up * 1.7f, yaw);
            }
        }

        static readonly string[] Props = { "barrel_large", "barrel_small_stack", "crates_stacked", "box_large", "barrel_large_decorated", "box_stacked", "keg", "trunk_medium_A" };

        static void CornerProps(Transform parent, Rect2 r, System.Random rng)
        {
            // 2 cantos sorteados com um objeto (longe das aberturas, que ficam no meio das paredes).
            var corners = new[] { (r.MinX + 1f, r.MinZ + 1f), (r.MaxX - 1f, r.MinZ + 1f), (r.MinX + 1f, r.MaxZ - 1f), (r.MaxX - 1f, r.MaxZ - 1f) };
            int a = rng.Next(4), b = (a + 1 + rng.Next(3)) % 4;
            foreach (int k in new[] { a, b })
                KayKitArt.Place(Props[rng.Next(Props.Length)], parent, new Vector3(corners[k].Item1, 0f, corners[k].Item2), rng.Next(360));
        }

        // Tochas nas paredes a cada ~7 m (só a chama emissiva; luz de verdade é 1 por sala, pelo desempenho no Quest).
        void Torches(WalkArea area, float x0, float z0, float x1, float z1, bool alongX, float len)
        {
            if (len < 5f) return;
            var inward = -OutsideNormal(area, x0, z0, x1, z1);
            int n = Mathf.Max(1, Mathf.FloorToInt(len / 7f));
            for (int i = 0; i < n; i++)
            {
                float s = (i + 0.5f) / n;
                var p = new Vector3(Mathf.Lerp(x0, x1, s), 2.3f, Mathf.Lerp(z0, z1, s)) + inward * 0.12f;
                var stick = ArenaUtil.Prim(PrimitiveType.Cylinder, root, p - Vector3.up * 0.15f, new Vector3(0.05f, 0.18f, 0.05f), wood).transform;
                stick.rotation = Quaternion.AngleAxis(25f, Vector3.Cross(Vector3.up, inward)) ;
                ArenaUtil.Prim(PrimitiveType.Sphere, root, p + Vector3.up * 0.05f + inward * 0.05f, Vector3.one * 0.14f, fire);
            }
        }

        Vector3 OutsideNormal(WalkArea area, float x0, float z0, float x1, float z1)
        {
            var mid = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            var n = Mathf.Approximately(z0, z1) ? Vector3.forward : Vector3.right;
            return area.Contains(mid.x + n.x * 0.5f, mid.z + n.z * 0.5f) ? -n : n;
        }

        GameObject Box(string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = ArenaUtil.Prim(PrimitiveType.Cube, root, pos, size, m, keepCollider: true);
            go.name = name;
            return go;
        }

        Transform Bonfire(Vector3 pos, int roomIndex)
        {
            var t = new GameObject("Fogueira " + roomIndex).transform;
            t.SetParent(root, false);
            t.position = pos;
            t.rotation = Quaternion.LookRotation(Vector3.forward);
            for (int i = 0; i < 3; i++)
            {
                var log = ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.08f, 0), new Vector3(0.1f, 0.35f, 0.1f), wood).transform;
                log.localRotation = Quaternion.Euler(90, i * 60, 0);
            }
            ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(0, 0.4f, 0), Vector3.one * 0.3f, fire);
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0, 0.9f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.48f, 0.16f); l.range = 7f; l.intensity = 2.5f;
            return t;
        }

        void SpawnPortal(Vector3 pos, bool home)
        {
            if (portal) Destroy(portal.gameObject);
            portalHome = home;
            portal = new GameObject(home ? "Portal de volta" : "Escada").transform;
            portal.SetParent(root, false);
            portal.position = pos;
            ArenaUtil.Prim(PrimitiveType.Cylinder, portal, new Vector3(0, 0.03f, 0), new Vector3(2.2f, 0.03f, 2.2f), portalMat);
            ArenaUtil.Prim(PrimitiveType.Cylinder, portal, new Vector3(0, 1.4f, 0), new Vector3(0.08f, 1.4f, 0.08f), portalMat);
            var label = ArenaFx.MakeText(portal, home ? "PORTAL: voltar ao santuário" : "ESCADA: descer ao andar 2", 0.008f, new Color(0.8f, 0.7f, 1f));
            label.transform.localPosition = new Vector3(0, 3f, 0);
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(portal, false);
            l.transform.localPosition = Vector3.up * 1.5f;
            l.type = LightType.Point; l.color = new Color(0.6f, 0.4f, 1f); l.range = 6f; l.intensity = 2f;
            portalCd = 1.5f;
        }

        void SetCheckpoint(DungeonRoom room)
        {
            foreach (var b in bonfires)
                if (b && b.name == "Fogueira " + room.Index) { game.bonfire = b; return; }
        }

        // ---------- Loop ----------
        void Update()
        {
            if (Run == null || ArenaGame.Blocked) return;
            float dt = Time.deltaTime;
            portalCd -= dt;
            var p = game.Player.FeetPosition;
            var before = Run.CheckpointRoom;
            var room = Run.OnPlayerAt(p.x, p.z);
            if (Run.CheckpointRoom != before) { SetCheckpoint(Run.CheckpointRoom); game.RequestSave(); }
            if (room != null) SpawnRoom(room);

            // Sala em combate sem ninguém vivo: limpa (checa a cada 0,25 s; slimes se dividem ao morrer).
            roomCheckCd -= dt;
            if (Run.ActiveRoom != null && roomCheckCd <= 0f)
            {
                roomCheckCd = 0.25f;
                bool anyAlive = false;
                foreach (var e in ArenaEnemy.All) if (e && !e.Dead) { anyAlive = true; break; }
                if (!anyAlive)
                {
                    var cleared = Run.ActiveRoom;
                    Run.OnRoomEmpty();
                    Unlock();
                    if (cleared != null && cleared.HasChest)
                        TreasureChest.Spawn(new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ), game.Player.FeetPosition - new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ), Run.Floor.Number, root, cleared.ChestIsMimic);
                    game.Message("Sala limpa", 1.2f);
                    ArenaAudio.Play2D(Sfx.Level, 0.4f, 0.8f);
                    game.RequestSave();
                }
            }

            if (portal && portalCd <= 0f && (portal.position - p).Flat().magnitude < 1.1f)
            {
                if (portalHome) { game.RequestSave(); game.GoToSanctuary(); return; }
                if (Run.CanDescend)
                {
                    Run.GoToFloor(Run.Floor.Number + 1);
                    ClearLeftovers();
                    BuildFloor();
                    game.SaveDeepestFloor(Run.Floor.Number);
                    game.Message("Andar " + Run.Floor.Number + ": a fogueira está acesa. O chefe está no fim.", 3f);
                    ArenaAudio.Play2D(Sfx.Skill, 0.8f, 0.6f);
                }
            }
        }

        void ClearLeftovers()
        {
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            ArenaEnemy.All.Clear();
            RagdollGoblin.ClearAll();
            WeaponPickup.ClearAll(game.Player.State);
        }

        void SpawnRoom(DungeonRoom room)
        {
            var r = room.Rect.Shrink(1.6f);
            // Nascem na metade da sala mais longe de onde o jogador entrou.
            var from = room.EnteredFrom >= 0 ? Run.Floor.Rooms[room.EnteredFrom].Rect : room.Rect;
            var away = new Vector3(room.Rect.CenterX - from.CenterX, 0f, room.Rect.CenterZ - from.CenterZ);
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away.Normalize();
            var center = new Vector3(room.Rect.CenterX, 0f, room.Rect.CenterZ);
            for (int i = 0; i < room.Enemies.Count; i++)
            {
                var def = room.Enemies[i];
                Vector3 pos;
                if (def.Kind == EnemyKind.ReiGoblin) pos = center + away * (r.Depth * 0.35f);
                else
                {
                    pos = new Vector3(Random.Range(r.MinX, r.MaxX), 0f, Random.Range(r.MinZ, r.MaxZ));
                    if (Vector3.Dot(pos - center, away) < 0f) pos -= 2f * Vector3.Dot(pos - center, away) * away;   // espelha para o lado de longe
                }
                var e = game.Spawn(def, pos, false);
                if (def.Kind == EnemyKind.ReiGoblin)
                {
                    game.SetBoss(e as GoblinKing);
                    game.Message("O Rei Goblin!", 2.2f);
                }
                ArenaFx.Burst(pos + Vector3.up * 0.5f, new Color(0.6f, 0.42f, 1f), 14, 2.5f);
            }
            ArenaAudio.Play2D(Sfx.Windup, 0.7f, 0.6f);
            Lock(room);
            if (room.Kind != RoomKind.Chefe) game.Message("Emboscada! " + room.Enemies.Count + " inimigos", 1.4f);
        }

        // Emboscada: portas de barras nas aberturas da sala e a área de andar fica só a sala (ninguém sai até limpar).
        void Lock(DungeonRoom room)
        {
            ArenaUtil.Area = new WalkArea(new[] { room.Rect });
            var mat = ArenaUtil.Lit(new Color(0.25f, 0.22f, 0.2f), new Color(0.35f, 0.08f, 0.04f));
            foreach (var (x0, z0, x1, z1) in Run.Floor.Openings(room.Index))
            {
                var door = new GameObject("Porta");
                door.transform.SetParent(root, false);
                bool alongX = Mathf.Approximately(z0, z1);
                float len = alongX ? x1 - x0 : z1 - z0;
                for (float s = 0.25f; s < len; s += 0.5f)   // grade de barras
                {
                    var p = alongX ? new Vector3(x0 + s, 1.5f, z0) : new Vector3(x0, 1.5f, z0 + s);
                    ArenaUtil.Prim(PrimitiveType.Cylinder, door.transform, p, new Vector3(0.08f, 1.5f, 0.08f), mat, keepCollider: true);
                }
                var bar = alongX ? new Vector3(len + 0.2f, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, len + 0.2f);
                ArenaUtil.Prim(PrimitiveType.Cube, door.transform, new Vector3((x0 + x1) * 0.5f, 2.2f, (z0 + z1) * 0.5f), bar, mat, keepCollider: true);
                doors.Add(door);
                ArenaAudio.Play(Sfx.Slam, new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), 0.7f, 1.3f);
            }
        }

        void Unlock()
        {
            foreach (var d in doors) if (d) Destroy(d);
            if (doors.Count > 0) ArenaAudio.Play2D(Sfx.Rattle, 0.6f, 0.7f);
            doors.Clear();
            if (Run != null) ArenaUtil.Area = Run.Floor.Area;
        }

        // ---------- Eventos do jogo ----------
        public void OnBossKilled(Vector3 at)
        {
            Run.OnBossKilled();
            Unlock();
            var room = Run.Floor.Last;
            // Portal no lado da sala longe do jogador (antes nascia debaixo dele e o mandava embora antes do loot).
            var c = new Vector3(room.Rect.CenterX, 0f, room.Rect.CenterZ);
            var away = (c - game.Player.FeetPosition).Flat();
            away = away.sqrMagnitude < 0.25f ? Vector3.forward : away.normalized;
            SpawnPortal(c + away * Mathf.Min(room.Rect.Width, room.Rect.Depth) * 0.3f, home: true);
            portalCd = 3f;
            game.Message("Rei Goblin derrotado! Pegue o loot e entre no portal.", 3f);
        }

        public void OnPlayerDied()
        {
            Run.OnPlayerDied();
            Unlock();
            SetCheckpoint(Run.CheckpointRoom);
        }
    }
}
