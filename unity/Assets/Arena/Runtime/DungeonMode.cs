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
        float icicleT = 4f;

        // Gelo: cristais nos cantos (só enfeite, dá para reconhecer o bioma).
        void IceCrystals(Rect2 r, System.Random rng)
        {
            var ice = ArenaUtil.Lit(new Color(0.7f, 0.9f, 1f), new Color(0.15f, 0.3f, 0.45f));
            foreach (var (x, z) in new[] { (r.MinX + 0.8f, r.MinZ + 0.8f), (r.MaxX - 0.8f, r.MinZ + 0.8f), (r.MinX + 0.8f, r.MaxZ - 0.8f), (r.MaxX - 0.8f, r.MaxZ - 0.8f) })
                for (int i = 0; i < 3; i++)
                {
                    var c = ArenaUtil.Prim(PrimitiveType.Capsule, root, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.8f, 0.4f, z + (float)(rng.NextDouble() - 0.5) * 0.8f),
                                           new Vector3(0.25f, 0.5f + (float)rng.NextDouble() * 0.4f, 0.25f), ice).transform;
                    c.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 40f, 0f, (float)(rng.NextDouble() - 0.5) * 40f);
                }
        }

        // Gelo: estalactites caem perto do jogador durante a luta.
        void TickIcicles(float dt)
        {
            if (Run.Floor.Biome != Biome.Gelo || Run.ActiveRoom == null) return;
            icicleT -= dt;
            if (icicleT > 0f) return;
            icicleT = Random.Range(BiomeRules.IcicleMinInterval, BiomeRules.IcicleMaxInterval);
            var p = game.Player.FeetPosition + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            Icicle.Spawn(p, root);
        }
        public Shop MerchantShop { get; } = new Shop();   // mercador perdido da dungeon
        DungeonRoom challengeRoom;
        float challengeT, challengeMsgT;
        int combatCleared;   // salas de combate limpas nesta partida (altar de bênçãos)

        public void Begin(int seed, int bossesDefeated)
        {
            Run = new DungeonRun(seed, bossesDefeated);
            combatCleared = 0;
            BuildFloor();
            game.Message("Andar 1: " + BiomeRules.Name(Run.Floor.Biome) + ". Desça pela escada; o chefe espera no andar 2.", 3.5f);
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
            foreach (var r in f.FullArea.Rects)
            {
                var floor = Box("Chao", new Vector3(r.CenterX, -0.05f, r.CenterZ), new Vector3(r.Width, 0.1f, r.Depth), stone);
                if (art) { floor.GetComponent<Renderer>().enabled = false; FloorTiles(artRoot, r.MinX, r.MinZ, r.MaxX, r.MaxZ, rng); }
            }
            foreach (var (x0, z0, x1, z1) in f.FullArea.WallSegments())
            {
                bool alongX = Mathf.Approximately(z0, z1);
                float len = alongX ? x1 - x0 : z1 - z0;
                // A parede fica do lado de fora da borda (0,15 m), para não roubar espaço de andar.
                var center = new Vector3((x0 + x1) * 0.5f, 2f, (z0 + z1) * 0.5f);
                center += OutsideNormal(f.FullArea, x0, z0, x1, z1) * 0.15f;
                var wallBox = Box("Parede", center, alongX ? new Vector3(len + 0.3f, 4f, 0.3f) : new Vector3(0.3f, 4f, len + 0.3f), wall);
                if (art) { wallBox.GetComponent<Renderer>().enabled = false; WallPieces(artRoot, f.FullArea, x0, z0, x1, z1, alongX, len, rng); }
                else Torches(f.FullArea, x0, z0, x1, z1, alongX, len);
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
                var (br, bg, bb) = BiomeRules.LightColor(f.Biome);   // cor do bioma
                l.color = room.Kind == RoomKind.Chefe ? new Color(1f, 0.35f, 0.25f) : new Color(br, bg, bb);
                if (f.Biome == Biome.Gelo && room.Kind == RoomKind.Combate) IceCrystals(room.Rect, rng);
                if (room.Kind == RoomKind.Inicio || room.Kind == RoomKind.Fogueira) bonfires.Add(Bonfire(c + Vector3.back * 1.5f, room.Index));
                if (room.Kind == RoomKind.Escada) SpawnPortal(c, home: false);
                // Pontos de gancho da teia: cantos no alto das paredes e o centro do "teto".
                var rr = room.Rect.Shrink(0.7f);
                foreach (var (gx, gz) in new[] { (rr.MinX, rr.MinZ), (rr.MaxX, rr.MinZ), (rr.MinX, rr.MaxZ), (rr.MaxX, rr.MaxZ) })
                    GrapplePoint.Spawn(root, new Vector3(gx, 3.6f, gz));
                GrapplePoint.Spawn(root, c + Vector3.up * 4.6f);
                // Em cada porta (2026-10-29, Pablo não conseguia trocar de sala): um ponto no alto da abertura, por dentro da sala.
                foreach (var (x0, z0, x1, z1) in f.Openings(room.Index))
                {
                    var door = new Vector3((x0 + x1) * 0.5f, 3.4f, (z0 + z1) * 0.5f);
                    GrapplePoint.Spawn(root, door + (c - door).Flat().normalized * 0.8f);
                }
                if (art) CornerProps(artRoot, room.Rect, rng);
                if (room.Kind == RoomKind.Combate) Breakables(room.Rect, rng);
                if (room.HasMerchant) BuildMerchant(c + Vector3.right * 2.5f);
                if (room.HasStatue) BlessingStatue.Spawn(new Vector3(room.Rect.MaxX - 2.4f, 0f, room.Rect.MaxZ - 2.4f), root, BlessingStatue.Price(f.Number + game.CurrentTier - 1, 0), repeat: false);
            }
            // Corredores: um ponto no meio (e mais um a cada ~6 m nos compridos).
            foreach (var cor in f.Corridors)
            {
                float len = Mathf.Max(cor.Width, cor.Depth);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 6f));
                bool alongX = cor.Width > cor.Depth;
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    var p = alongX ? new Vector3(Mathf.Lerp(cor.MinX, cor.MaxX, t), 3.6f, cor.CenterZ) : new Vector3(cor.CenterX, 3.6f, Mathf.Lerp(cor.MinZ, cor.MaxZ, t));
                    if (f.RoomAt(p.x, p.z, -0.5f) == null) GrapplePoint.Spawn(root, p);   // só a parte fora das salas
                }
            }
            if (f.Secret != null) BuildSecret(f);
            foreach (var lava in f.Lava) LavaPool.Spawn(lava, root);
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

        // ---------- Salas especiais ----------
        // Desafio: contagem regressiva na mensagem; acabou o tempo = perde o baú (a sala continua normal).
        void TickChallenge(float dt)
        {
            if (challengeRoom == null || Run.ActiveRoom != challengeRoom) return;
            challengeT -= dt;
            challengeMsgT -= dt;
            if (challengeT <= 0f)
            {
                challengeRoom = null;
                game.Message("Desafio perdido: o tempo acabou", 2f);
                ArenaAudio.Play2D(Sfx.Deny, 0.8f);
                return;
            }
            if (challengeMsgT <= 0f) { challengeMsgT = 0.25f; game.Message("DESAFIO: " + Mathf.CeilToInt(challengeT) + " s", 0.4f); }
        }

        // Mercador perdido: 2 armas (Incomum ou melhor) penduradas, compra com grip/E.
        void BuildMerchant(Vector3 at)
        {
            MerchantShop.Stock.Clear();
            var stand = new GameObject("Mercador").transform;
            stand.SetParent(root, false);
            stand.position = at;
            ArenaUtil.Prim(PrimitiveType.Cube, stand, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.6f), wood, keepCollider: true);
            // O mercador: um goblin encapuzado bem simples.
            var robe = ArenaUtil.Lit(new Color(0.35f, 0.2f, 0.45f));
            ArenaUtil.Prim(PrimitiveType.Capsule, stand, new Vector3(0f, 0.8f, 0.7f), new Vector3(0.5f, 0.8f, 0.5f), robe);
            ArenaUtil.Prim(PrimitiveType.Sphere, stand, new Vector3(0f, 1.65f, 0.7f), Vector3.one * 0.38f, ArenaUtil.Lit(new Color(0.45f, 0.62f, 0.3f)));
            for (int i = 0; i < SpecialRooms.MerchantItems; i++)
            {
                var item = game.Loot.RollItem(Rarity.Incomum, game.CurrentTier);
                MerchantShop.Stock.Add(item);
                var p = at + new Vector3(-0.4f + 0.8f * i, 1.1f, 0f);
                WeaponPickup.Spawn(item, p).PlaceOnRack(p, Quaternion.LookRotation(Vector3.back), RackKind.Loja).SetPriceTag(Merchant.ShopPrice(item));
            }
            var sign = ArenaFx.MakeText(stand, "MERCADOR PERDIDO\n<size=40>grip/E na arma para comprar</size>", 0.006f, new Color(1f, 0.85f, 0.4f));
            sign.transform.localPosition = new Vector3(0f, 2.4f, 0.7f);
            sign.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        // Sala secreta: parede rachada na porta (quebra com golpe forte ou explosão), baú garantido lá dentro.
        void BuildSecret(DungeonFloor f)
        {
            var s = f.Secret;
            var c = new Vector3(s.Rect.CenterX, 0f, s.Rect.CenterZ);
            var l = new GameObject("Luz secreta").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = c + Vector3.up * 3f;
            l.type = LightType.Point; l.range = 9f; l.intensity = 2f; l.color = new Color(1f, 0.85f, 0.4f);
            var host = f.Rooms[f.SecretHost];
            TreasureChest.Spawn(c, new Vector3(host.Rect.CenterX, 0f, host.Rect.CenterZ) - c, f.Number, root, false, rare: true);
            var (x0, z0, x1, z1) = f.SecretDoor;
            SecretWall.Spawn(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), root, () =>
            {
                f.OpenSecret();
                if (Run != null && Run.ActiveRoom == null) ArenaUtil.Area = f.Area;
                game.Message("SALA SECRETA!", 2.5f);
                ArenaAudio.Play2D(Sfx.Level, 1f, 1.4f);
            });
        }

        // Caixas e barris que quebram, encostados nas paredes (longe do meio delas, onde ficam as aberturas).
        void Breakables(Rect2 r, System.Random rng)
        {
            int n = BreakableRules.CountForRoom(rng);
            int explosive = BreakableRules.RoomHasExplosive(rng) || BiomeRules.ExtraExplosives(Run.Floor.Biome) ? rng.Next(n) : -1;
            for (int i = 0; i < n; i++)
            {
                int side = rng.Next(4);
                float t = rng.Next(2) == 0 ? (float)(0.12 + rng.NextDouble() * 0.22) : (float)(0.66 + rng.NextDouble() * 0.22);
                float x = side < 2 ? Mathf.Lerp(r.MinX, r.MaxX, t) : (side == 2 ? r.MinX + 1.1f : r.MaxX - 1.1f);
                float z = side >= 2 ? Mathf.Lerp(r.MinZ, r.MaxZ, t) : (side == 0 ? r.MinZ + 1.1f : r.MaxZ - 1.1f);
                bool fornalha = BiomeRules.ExtraExplosives(Run.Floor.Biome) && rng.NextDouble() < 0.35;   // Fornalha: mais barris vermelhos
                var kind = i == explosive || fornalha ? BreakableKind.BarrilExplosivo : (rng.Next(2) == 0 ? BreakableKind.Caixa : BreakableKind.Barril);
                Breakable.Spawn(kind, new Vector3(x, 0f, z), rng.Next(360), root);
            }
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
            TickChallenge(dt);
            TickIcicles(dt);
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
                    if (cleared != null && cleared == challengeRoom)
                    {
                        challengeRoom = null;
                        var cc = new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ) + Vector3.left * 1.6f;
                        TreasureChest.Spawn(cc, game.Player.FeetPosition - cc, Run.Floor.Number, root, false, rare: true);
                        game.Message("DESAFIO VENCIDO! Baú raro", 2.5f);
                        ArenaAudio.Play2D(Sfx.Level, 1f, 1.2f);
                    }
                    if (cleared != null && cleared.HasChest)
                        TreasureChest.Spawn(new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ), game.Player.FeetPosition - new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ), Run.Floor.Number, root, cleared.ChestIsMimic);
                    game.Message("Sala limpa", 1.2f);
                    if (cleared != null && cleared.HasCage)
                    {
                        var st = game.Player.State;
                        var who = AllyRules.NextToRescue(n => st.Rescued.Contains(n));
                        if (who.HasValue)
                        {
                            var cc = new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ);
                            var toPl = (game.Player.FeetPosition - cc).Flat();
                            toPl = toPl.sqrMagnitude > 0.01f ? toPl.normalized : Vector3.forward;
                            RescueCage.Spawn(cc - Vector3.Cross(Vector3.up, toPl) * 2.5f, who.Value, root);
                        }
                    }
                    if (cleared != null && cleared.Kind == RoomKind.Combate && RunBlessings.AltarAfterRoom(++combatCleared))
                    {
                        // Altar ao lado do centro (o baú fica no centro), do lado de onde o jogador está.
                        var c = new Vector3(cleared.Rect.CenterX, 0f, cleared.Rect.CenterZ);
                        var toPl = (game.Player.FeetPosition - c).Flat();
                        toPl = toPl.sqrMagnitude > 0.01f ? toPl.normalized : Vector3.forward;
                        game.OfferBlessings(c + toPl * 2.2f + Vector3.Cross(Vector3.up, toPl) * 1.5f, root);
                    }
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
                    game.Message("Andar " + Run.Floor.Number + ": " + BiomeRules.Name(Run.Floor.Biome) + ". A fogueira está acesa; o chefe está no fim.", 3f);
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
                if (EnemyDef.IsBoss(def.Kind)) pos = center + away * (r.Depth * 0.35f);
                else
                {
                    pos = new Vector3(Random.Range(r.MinX, r.MaxX), 0f, Random.Range(r.MinZ, r.MaxZ));
                    if (Vector3.Dot(pos - center, away) < 0f) pos -= 2f * Vector3.Dot(pos - center, away) * away;   // espelha para o lado de longe
                }
                var e = game.Spawn(def, pos, false);
                if (EnemyDef.IsBoss(def.Kind))
                {
                    game.SetBoss(e);
                    game.Message("O Rei Goblin!", 2.2f);
                }
                ArenaFx.Burst(pos + Vector3.up * 0.5f, new Color(0.6f, 0.42f, 1f), 14, 2.5f);
            }
            ArenaAudio.Play2D(Sfx.Windup, 0.7f, 0.6f);
            Lock(room);
            if (room.Kind != RoomKind.Chefe) game.Message("Emboscada! " + room.Enemies.Count + " inimigos", 1.4f);
            if (room.IsChallenge)
            {
                challengeRoom = room;
                challengeT = SpecialRooms.ChallengeTime(room.Enemies.Count);
                game.Message("DESAFIO! Limpe a sala em " + Mathf.CeilToInt(challengeT) + " s para ganhar um baú raro", 2.5f);
                challengeMsgT = 2.5f;
            }
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
            game.Message("Chefe derrotado! Pegue o loot e entre no portal.", 3f);
        }

        public void OnPlayerDied()
        {
            challengeRoom = null;
            Run.OnPlayerDied();
            Unlock();
            SetCheckpoint(Run.CheckpointRoom);
        }
    }
}
