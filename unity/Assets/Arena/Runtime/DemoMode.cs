using System.Collections;
using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // O que a demo deixa usar (Demo.OnlyDemo). Fora da demo tudo vale. As habilidades vêm na ordem do roteiro
    // (docs/roteiro-demo.md): Bola de Fogo (estátua) → Lâmina de Vento (orc) → Aura Vermelha (sala do chefe).
    public static class DemoGate
    {
        public static bool InDemo => ArenaGame.Instance && ArenaGame.Instance.Mode == ArenaGame.GameMode.Demo;
        public static bool Fire = false;
        // Poderes da mão que a demo esconde (raio, soco, palma, raízes, onda, Muralha, raio do céu) e Investida/árvore.
        public static bool Hidden => InDemo;
        public static bool FireAllowed => !InDemo || Fire;
    }

    // DEMO (2026-10-31, roteiro do Pablo): caverna linear montada à mão, 1ª versão BRUTA (primitivas + KayKit), do
    // acordar amarrado até a saída. Salas em linha no eixo +z, ligadas por corredores de 3 m:
    //   0 cela (amarrado) → 1 cadáver com a espada cravada + escudo → 2 esqueleto enfraquecido → 3 estátua do fogo
    //   (fogueira) → 4 três goblins → 5 orc com escudo + arqueiro + xamã (dá a Lâmina de Vento) → 6 fogueira →
    //   porta pesada (duas mãos) → 7 sala do chefe: aura vermelha no altar → o Rei Goblin entra se curvando, pega a
    //   porta como escudo, a estante cai e fecha a saída → luta → coroa → queimar a estante com fogo → 8 saída = fim.
    // A luta do chefe ainda é a do Rei Goblin atual (escudo de porta só visual) — a sequência completa vem depois.
    public sealed class DemoMode : MonoBehaviour
    {
        public bool Active { get; private set; }
        ArenaGame game;
        Transform root;
        Material stone, wall, wood, iron, fire, gold, rope;

        sealed class Room
        {
            public string Name; public Rect2 Rect;
            public List<EnemyDef> Enemies = new List<EnemyDef>();
            public bool Triggered, Cleared;
            public System.Action OnClear;
            public System.Action<ArenaEnemy> OnSpawned;
        }
        readonly List<Room> rooms = new List<Room>();
        readonly List<Rect2> corridors = new List<Rect2>();
        Room activeRoom;
        readonly List<GameObject> bars = new List<GameObject>();
        float checkCd;

        const float CorW = 3f;
        Rect2 R(int i) => rooms[i].Rect;

        void Awake() => game = GetComponent<ArenaGame>();

        void Mats()
        {
            if (stone) return;
            stone = ArenaUtil.Lit(new Color(0.3f, 0.28f, 0.31f));
            wall = ArenaUtil.Lit(new Color(0.24f, 0.22f, 0.27f));
            wood = ArenaUtil.Lit(new Color(0.42f, 0.27f, 0.13f));
            iron = ArenaUtil.Lit(new Color(0.35f, 0.37f, 0.42f), new Color(0.03f, 0.03f, 0.04f));
            fire = ArenaUtil.Lit(new Color(1f, 0.48f, 0.1f), new Color(1.5f, 0.6f, 0.1f));
            gold = ArenaUtil.Lit(new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.24f, 0.02f));
            rope = ArenaUtil.Lit(new Color(0.65f, 0.52f, 0.32f));
        }

        // ---------- Montagem ----------
        public void Begin()
        {
            End();
            Mats();
            Active = true;
            DemoGate.Fire = false;
            root = new GameObject("Demo").transform;
            rooms.Clear(); corridors.Clear(); activeRoom = null; bossStage = 0; crownT = -1f; ended = false;
            float z = 0f;
            void AddRoom(string name, float w, float d)
            {
                if (rooms.Count > 0) { corridors.Add(new Rect2(-CorW / 2f, z, CorW / 2f, z + 4f)); z += 4f; }
                rooms.Add(new Room { Name = name, Rect = new Rect2(-w / 2f, z, w / 2f, z + d) });
                z += d;
            }
            AddRoom("Cela", 6f, 6f);              // 0
            AddRoom("Cadáver", 8f, 8f);           // 1
            AddRoom("Esqueleto", 12f, 12f);       // 2
            AddRoom("Estátua", 10f, 10f);         // 3
            AddRoom("Goblins", 16f, 16f);         // 4
            AddRoom("Orc", 18f, 18f);             // 5
            AddRoom("Fogueira", 8f, 8f);          // 6
            AddRoom("Chefe", 20f, 20f);           // 7
            AddRoom("Saída", 8f, 8f);             // 8

            BuildGeometry();
            // Encontros
            rooms[2].Enemies.Add(EnemyDef.Esqueleto.WithHp(0.6f));   // enfraquecido: o 1º inimigo
            for (int i = 0; i < 3; i++) rooms[4].Enemies.Add(EnemyDef.Goblin);
            rooms[5].Enemies.Add(EnemyDef.Orc); rooms[5].Enemies.Add(EnemyDef.Arqueiro); rooms[5].Enemies.Add(EnemyDef.Xama);
            rooms[5].OnSpawned = e => { if (e is PhysicalGoblinEnemy pg && e.Def.Kind == EnemyKind.Orc) StartCoroutine(GiveShield(pg)); };
            rooms[2].OnClear = () => Sign(Center(3) + new Vector3(0f, 2.6f, -R(3).Depth / 2f + 0.6f), "Uma estátua... ela estende a mão.", 0.007f);
            rooms[5].OnClear = () => SpawnOrb(Center(5) + Vector3.forward * 2f, "LÂMINA DE VENTO", new Color(0.6f, 0.95f, 1f), () =>
            {
                game.Player.State.Tree.Grant(SkillIds.VentoLamina);
                game.Message("LÂMINA DE VENTO: aperte B — os próximos 2 golpes rápidos soltam cortes de vento.", 6f);
            });

            // Checkpoint inicial e o jogador amarrado na cela
            SetCheckpoint(checkpoints[0]);
            var start = Center(0) + new Vector3(0f, 0f, -1.2f);
            game.Player.TeleportTo(start, start + Vector3.forward * 3f);
            Bind();
            UpdateArea();
        }

        public void End()
        {
            Active = false;
            DemoGate.Fire = false;
            StopAllCoroutines();
            if (root) Destroy(root.gameObject);
            root = null;
            bars.Clear(); checkpoints.Clear();
            if (game && game.Player) game.Player.Bound = false;
            ArenaUtil.Area = null;
        }

        Vector3 Center(int i) => new Vector3(R(i).CenterX, 0f, R(i).CenterZ);

        readonly List<Transform> checkpoints = new List<Transform>();

        void BuildGeometry()
        {
            var all = new List<Rect2>();
            foreach (var r in rooms) all.Add(r.Rect);
            all.AddRange(corridors);
            var area = new WalkArea(all);
            bool art = KayKitArt.Model("Dungeon/wall");
            var artRoot = new GameObject("Arte").transform; artRoot.SetParent(root, false);
            var rng = new System.Random(31);
            foreach (var r in area.Rects)
            {
                var floor = Box("Chao", new Vector3(r.CenterX, -0.05f, r.CenterZ), new Vector3(r.Width, 0.1f, r.Depth), stone);
                if (art) { floor.GetComponent<Renderer>().enabled = false; DungeonMode.FloorTiles(artRoot, r.MinX, r.MinZ, r.MaxX, r.MaxZ, rng); }
            }
            foreach (var (x0, z0, x1, z1) in area.WallSegments())
            {
                bool alongX = Mathf.Approximately(z0, z1);
                float len = alongX ? x1 - x0 : z1 - z0;
                var center = new Vector3((x0 + x1) * 0.5f, 2f, (z0 + z1) * 0.5f) + DungeonMode.OutsideNormal(area, x0, z0, x1, z1) * 0.15f;
                var wb = Box("Parede", center, alongX ? new Vector3(len + 0.3f, 4f, 0.3f) : new Vector3(0.3f, 4f, len + 0.3f), wall);
                if (art) { wb.GetComponent<Renderer>().enabled = false; DungeonMode.WallPieces(artRoot, area, x0, z0, x1, z1, alongX, len, rng); }
            }
            // Luz: uma por sala (orçamento do Quest), mais fraca nas pequenas.
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = R(i);
                var l = new GameObject("Luz " + rooms[i].Name).AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = Center(i) + Vector3.up * 3.4f;
                l.type = LightType.Point;
                l.range = Mathf.Max(r.Width, r.Depth) * 0.95f;
                l.intensity = i == 7 ? 2.6f : 2.2f;
                l.color = i == 7 ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.8f, 0.6f);
                if (art && i != 7 && i != 0) DungeonMode.CornerProps(artRoot, r, rng);
            }
            if (art) StaticBatchingUtility.Combine(artRoot.gameObject);

            // Checkpoints: cela (sem fogo), estátua e antes da porta (fogueiras de verdade: curam).
            checkpoints.Add(Marker(Center(0) + new Vector3(0f, 0f, -2f)));
            checkpoints.Add(Bonfire(Center(3) + new Vector3(-3f, 0f, -2.5f)));
            checkpoints.Add(Bonfire(Center(6) + new Vector3(-2.2f, 0f, -1.5f)));

            // Cena 1: cadáver com a espada cravada e o escudo ao lado
            var c1 = Center(1) + new Vector3(0f, 0f, 1.5f);
            StuckSword.Spawn(new ItemInstance(WeaponDef.EspadaLonga), c1, Vector3.back, root);
            WeaponPickup.Spawn(new ItemInstance(WeaponDef.Escudo), c1 + new Vector3(0.9f, 0.2f, -0.3f));
            Sign(c1 + new Vector3(0f, 2.4f, 1.6f), "Segure o cabo da espada e PUXE devagar.\n<size=40>O escudo está do lado (grip perto dele).</size>", 0.006f);

            // Cena 0: placa na cela
            Sign(Center(0) + new Vector3(0f, 2.3f, 2.6f), "Você está amarrado.\n<size=40>Puxe os braços com força para se soltar.</size>", 0.006f);

            // Cena 3: estátua do fogo
            BuildFireStatue(Center(3) + new Vector3(0f, 0f, 2.8f));

            // Cena 6/7: porta pesada entre a fogueira e a sala do chefe
            BuildHeavyDoor(new Vector3(0f, 0f, R(7).MinZ));
            Sign(new Vector3(0f, 3.2f, R(7).MinZ - 0.4f), "Pesada demais para uma mão.\n<size=40>Guarde as armas e empurre com as DUAS mãos.</size>", 0.006f);

            // Cena 7: altar com a aura e a estante na saída
            BuildAltar(Center(7) + new Vector3(0f, 0f, 4.5f));
            BuildShelf(new Vector3(0f, 0f, R(7).MaxZ - 0.6f));

            // Cena 8: saída
            var exitLight = new GameObject("Luz do fim").AddComponent<Light>();
            exitLight.transform.SetParent(root, false);
            exitLight.transform.position = Center(8) + Vector3.up * 2.5f;
            exitLight.type = LightType.Point; exitLight.color = new Color(1f, 0.95f, 0.8f); exitLight.range = 9f; exitLight.intensity = 3f;
        }

        GameObject Box(string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = ArenaUtil.Prim(PrimitiveType.Cube, root, pos, size, m, keepCollider: true);
            go.name = name;
            return go;
        }

        Transform Marker(Vector3 pos)
        {
            var t = new GameObject("Checkpoint").transform;
            t.SetParent(root, false);
            t.position = pos;
            t.rotation = Quaternion.LookRotation(Vector3.forward);
            return t;
        }

        Transform Bonfire(Vector3 pos)
        {
            var t = Marker(pos);
            t.name = "Fogueira";
            t.rotation = Quaternion.LookRotation(Vector3.right);
            for (int i = 0; i < 3; i++)
                ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.08f, 0), new Vector3(0.1f, 0.35f, 0.1f), wood).transform.localRotation = Quaternion.Euler(90, i * 60, 0);
            ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(0, 0.4f, 0), Vector3.one * 0.3f, fire);
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0, 0.9f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.48f, 0.16f); l.range = 6f; l.intensity = 2.2f;
            return t;
        }

        void Sign(Vector3 pos, string text, float size)
        {
            var t = ArenaFx.MakeText(root, text, size, new Color(1f, 0.9f, 0.65f));
            t.transform.position = pos;
            t.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // lida por quem vem de -z
        }

        void SetCheckpoint(Transform t) { if (t) game.bonfire = t; }

        // ---------- Cena 0: amarrado ----------
        Transform ropeVis;
        float struggle;

        void Bind()
        {
            game.Player.Bound = true;
            struggle = 0f;
            ropeVis = new GameObject("Corda").transform;
            ropeVis.SetParent(root, false);
            ArenaUtil.Prim(PrimitiveType.Cylinder, ropeVis, Vector3.zero, new Vector3(0.03f, 0.5f, 0.03f), rope);
        }

        void UpdateBound(float dt)
        {
            var pl = game.Player;
            if (!pl.Bound) return;
            var a = pl.leftHand.position; var b = pl.rightHand.position;
            if (!pl.IsVr) { a = pl.HeadPosition + pl.HeadForward * 0.4f + Vector3.down * 0.5f + Vector3.left * 0.12f; b = a + Vector3.right * 0.24f; }
            if (ropeVis)
            {
                var mid = (a + b) * 0.5f; var d = b - a;
                ropeVis.position = mid;
                ropeVis.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.right);
                ropeVis.GetChild(0).localScale = new Vector3(0.03f, Mathf.Max(0.05f, d.magnitude * 0.5f), 0.03f);
            }
            // Força: as mãos se afastando rápido (ou balançando) somam; desktop: segurar E.
            float sep = Vector3.Distance(a, b);
            float gain = pl.IsVr ? Mathf.Max(0f, (sep - lastSep) / Mathf.Max(dt, 1e-4f) - 0.4f) * dt : 0f;
            lastSep = sep;
            if (!pl.IsVr && UnityEngine.InputSystem.Keyboard.current?.eKey.isPressed == true) gain = dt * 0.6f;
            if (gain > 0f)
            {
                struggle += gain;
                if (Random.value < 0.15f) ArenaInput.Haptic(0.4f, 0.03f);
                if (Random.value < 0.05f) ArenaAudio.Play(Sfx.Rattle, (a + b) * 0.5f, 0.4f, 1.4f);
            }
            if (struggle >= 1.2f)
            {
                pl.Bound = false;
                ArenaFx.Burst((a + b) * 0.5f, new Color(0.65f, 0.52f, 0.32f), 18, 3f);
                ArenaAudio.Play(Sfx.Broken, (a + b) * 0.5f, 0.8f, 1.3f);
                ArenaInput.Haptic(0.8f, 0.12f); ArenaInput.Haptic(0.8f, 0.12f, left: true);
                if (ropeVis) Destroy(ropeVis.gameObject);
                game.Message("Livre! Siga pelo corredor.", 2.5f);
                UpdateArea();
            }
        }
        float lastSep;

        // ---------- Cena 3: estátua do fogo ----------
        Transform statueHand;
        bool statueUsed;

        void BuildFireStatue(Vector3 at)
        {
            statueUsed = false;
            var s = new GameObject("Estatua do fogo").transform;
            s.SetParent(root, false);
            s.position = at;
            s.rotation = Quaternion.LookRotation(Vector3.back);   // olha para quem chega
            var rock = ArenaUtil.Lit(new Color(0.55f, 0.52f, 0.5f));
            ArenaUtil.Prim(PrimitiveType.Cube, s, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f), rock, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Capsule, s, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 0.9f, 0.5f), rock);
            ArenaUtil.Prim(PrimitiveType.Sphere, s, new Vector3(0f, 2.55f, 0f), Vector3.one * 0.5f, rock);
            var arm = ArenaUtil.Prim(PrimitiveType.Cylinder, s, new Vector3(0.25f, 1.6f, 0.35f), new Vector3(0.14f, 0.35f, 0.14f), rock).transform;
            arm.localRotation = Quaternion.Euler(70f, 0f, 0f);
            statueHand = ArenaUtil.Prim(PrimitiveType.Sphere, s, new Vector3(0.25f, 1.45f, 0.72f), Vector3.one * 0.16f, fire).transform;
            var l = new GameObject("Brasa").AddComponent<Light>();
            l.transform.SetParent(statueHand, false);
            l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.15f); l.range = 3f; l.intensity = 1.5f;
            Sign(at + new Vector3(0f, 3.3f, -0.4f), "Ponha a mão na mão da estátua.", 0.006f);
        }

        void UpdateStatue()
        {
            if (statueUsed || !statueHand) return;
            var pl = game.Player;
            var p = statueHand.position;
            bool touch = pl.IsVr
                ? Vector3.Distance(pl.leftHand.position, p) < 0.2f || Vector3.Distance(pl.rightHand.position, p) < 0.2f
                : Vector3.Distance(pl.FeetPosition.Flat(), p.Flat()) < 1.6f && UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true;
            if (!touch) return;
            statueUsed = true;
            DemoGate.Fire = true;
            ArenaFx.Burst(p, new Color(1f, 0.5f, 0.15f), 40, 4f, 1.4f);
            ArenaAudio.Play(Sfx.Skill, p, 1f, 0.7f);
            ArenaInput.Haptic(1f, 0.3f); ArenaInput.Haptic(1f, 0.3f, left: true);
            game.Message("BOLA DE FOGO! Com o controle: SEGURE o clique do analógico ESQUERDO (a bola cresce) e jogue a mão. Sem controle: punho esticado, ABRA a mão e jogue. Mergulhe a espada na bola para ela pegar fogo.", 10f);
            SetCheckpoint(checkpoints[1]);
        }

        // ---------- Orbes de habilidade (Lâmina de Vento, Aura) ----------
        void SpawnOrb(Vector3 at, string name, Color c, System.Action onTake)
        {
            var o = new GameObject("Orbe " + name).transform;
            o.SetParent(root, false);
            o.position = at + Vector3.up * 1.2f;
            ArenaUtil.Prim(PrimitiveType.Sphere, o, Vector3.zero, Vector3.one * 0.3f, ArenaUtil.Lit(c, c * 1.5f));
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(o, false);
            l.type = LightType.Point; l.color = c; l.range = 4f; l.intensity = 2f;
            var t = ArenaFx.MakeText(o, name + "\n<size=40>toque com a mão</size>", 0.005f, c);
            t.transform.localPosition = Vector3.up * 0.5f;
            orbs.Add((o, onTake));
            ArenaAudio.Play(Sfx.Pickup, o.position, 0.8f, 0.8f);
        }
        readonly List<(Transform o, System.Action take)> orbs = new List<(Transform, System.Action)>();

        void UpdateOrbs()
        {
            var pl = game.Player;
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                var (o, take) = orbs[i];
                if (!o) { orbs.RemoveAt(i); continue; }
                o.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
                var lbl = o.GetComponentInChildren<TextMesh>();
                if (lbl) lbl.transform.rotation = Quaternion.LookRotation(lbl.transform.position - pl.HeadPosition);
                bool touch = Vector3.Distance(pl.leftHand.position, o.position) < 0.3f || Vector3.Distance(pl.rightHand.position, o.position) < 0.3f
                             || (!pl.IsVr && Vector3.Distance(pl.FeetPosition.Flat(), o.position.Flat()) < 1f);
                if (!touch) continue;
                ArenaFx.Burst(o.position, Color.white, 30, 4f, 1.2f);
                ArenaAudio.Play2D(Sfx.Level, 1f, 1.1f);
                ArenaInput.Haptic(1f, 0.25f); ArenaInput.Haptic(1f, 0.25f, left: true);
                Destroy(o.gameObject);
                orbs.RemoveAt(i);
                take();
            }
        }

        // Orc da sala 5 com escudo: o corpo físico nasce no Start do representante — espera ele existir.
        IEnumerator GiveShield(PhysicalGoblinEnemy pg)
        {
            for (int i = 0; i < 10 && pg && !pg.Body; i++) yield return null;
            if (pg && pg.Body) pg.Body.ForceShield();
        }

        // ---------- Cena 6/7: porta pesada ----------
        Transform door;
        float doorAngle;
        bool doorOpen;
        const float DoorW = 3.2f, DoorH = 2.7f, DoorOpenAngle = 72f;

        void BuildHeavyDoor(Vector3 at)
        {
            doorAngle = 0f; doorOpen = false;
            door = new GameObject("Porta pesada").transform;
            door.SetParent(root, false);
            door.position = at + Vector3.left * (DoorW / 2f);   // dobradiça à esquerda
            BuildDoorBoards(door, new Vector3(DoorW / 2f, DoorH / 2f, 0f), collider: true);
            // Batente e verga (a porta é mais baixa que o Rei: ele vai precisar se curvar)
            Box("Verga", at + new Vector3(0f, DoorH + 0.65f, 0f), new Vector3(CorW + 0.4f, 1.3f, 0.4f), wall);
        }

        // Tábuas grossas com chapas de ferro (o fogo não queima: só escurece). Usado na porta e no escudo do Rei.
        void BuildDoorBoards(Transform parent, Vector3 center, bool collider)
        {
            for (int i = 0; i < 4; i++)
                ArenaUtil.Prim(PrimitiveType.Cube, parent, center + new Vector3((i - 1.5f) * (DoorW / 4f), 0f, 0f), new Vector3(DoorW / 4f - 0.02f, DoorH, 0.14f), wood, keepCollider: collider);
            foreach (float y in new[] { -0.9f, 0f, 0.9f })
                ArenaUtil.Prim(PrimitiveType.Cube, parent, center + new Vector3(0f, y, -0.08f), new Vector3(DoorW - 0.05f, 0.22f, 0.03f), iron);
            for (int i = 0; i < 6; i++)
                ArenaUtil.Prim(PrimitiveType.Sphere, parent, center + new Vector3(-DoorW / 2f + 0.25f + i * (DoorW - 0.5f) / 5f, 0.9f, -0.1f), Vector3.one * 0.07f, iron);
        }

        void UpdateDoor(float dt)
        {
            if (!door || doorOpen) return;
            var pl = game.Player;
            if ((pl.FeetPosition - door.position).Flat().magnitude > 4.5f) return;
            bool pushing; float push;
            if (pl.IsVr)
            {
                float p0 = HandPush(pl.leftHand.position, 0, dt), p1 = HandPush(pl.rightHand.position, 1, dt);
                bool armed = pl.Weapon.Held || pl.State.LeftItem != null;
                bool touching = handOnDoor[0] || handOnDoor[1];
                pushing = p0 > 0.05f && p1 > 0.05f && !armed;
                push = Mathf.Min(p0, p1);
                if (touching && hintCd <= 0f && !pushing)
                {
                    hintCd = 2f;
                    game.Message(armed ? "Guarde as armas nos coldres: a porta só cede com as DUAS mãos." : "Use as DUAS mãos e empurre.", 1.8f);
                }
            }
            else
            {
                pushing = (pl.FeetPosition - (door.position + Vector3.right * DoorW / 2f)).Flat().magnitude < 1.8f && UnityEngine.InputSystem.Keyboard.current?.eKey.isPressed == true;
                push = 0.6f;
            }
            hintCd -= dt;
            if (!pushing) return;
            // Pesada: ~1/4 da velocidade da mão vira giro.
            doorAngle = Mathf.Min(DoorOpenAngle, doorAngle + push * 28f * dt);
            door.rotation = Quaternion.Euler(0f, -doorAngle, 0f);
            creakT -= dt;
            if (creakT <= 0f) { creakT = 0.45f; ArenaAudio.Play(Sfx.Rattle, door.position + Vector3.right * 1.2f + Vector3.up, 0.5f, 0.55f); ArenaInput.Haptic(0.5f, 0.06f); ArenaInput.Haptic(0.5f, 0.06f, left: true); }
            if (doorAngle >= DoorOpenAngle - 0.5f)
            {
                doorOpen = true;
                ArenaAudio.Play(Sfx.Slam, door.position, 0.8f, 0.6f);
                game.Message("A porta abriu.", 1.5f);
                UpdateArea();
            }
        }
        readonly bool[] handOnDoor = new bool[2];
        readonly Vector3[] handPrev = new Vector3[2];
        float hintCd, creakT;

        // Velocidade com que a mão empurra a face da porta (0 = não está encostada ou não empurra).
        float HandPush(Vector3 hand, int side, float dt)
        {
            var local = door.InverseTransformPoint(hand);
            var vel = door.InverseTransformDirection((hand - handPrev[side]) / Mathf.Max(dt, 1e-4f));
            handPrev[side] = hand;
            handOnDoor[side] = local.x > 0.05f && local.x < DoorW - 0.05f && local.y > 0.2f && local.y < DoorH && local.z > -0.32f && local.z < 0.05f;
            return handOnDoor[side] ? Mathf.Max(0f, vel.z) : 0f;
        }

        // ---------- Cena 7: altar, entrada do Rei, estante ----------
        Transform altarOrbSpot, shelf;
        int bossStage;   // 0 antes da aura, 1 cena de entrada, 2 luta, 3 morto
        ArenaEnemy boss;
        Transform bossShield;
        bool shelfBurnt;
        float shelfFire = -1f;

        void BuildAltar(Vector3 at)
        {
            var a = new GameObject("Altar").transform;
            a.SetParent(root, false);
            a.position = at;
            ArenaUtil.Prim(PrimitiveType.Cylinder, a, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.45f, 0.9f), ArenaUtil.Lit(new Color(0.4f, 0.36f, 0.38f)), keepCollider: true);
            altarOrbSpot = a;
            SpawnOrb(at + Vector3.down * 0.15f, "AURA VERMELHA", new Color(1f, 0.25f, 0.2f), () =>
            {
                game.Player.State.Tree.Grant(SkillIds.Contra);
                game.Message("AURA VERMELHA: aperte o GATILHO direito — por 8 s a arma quebra defesas e escudos.", 5f);
                StartCoroutine(BossEntrance(full: true));
            });
        }

        void BuildShelf(Vector3 at)
        {
            shelfBurnt = false; shelfFire = -1f;
            shelf = new GameObject("Estante").transform;
            shelf.SetParent(root, false);
            shelf.position = at;
            shelf.rotation = Quaternion.identity;
            // Em pé encostada na parede, ao lado da saída (cai por cima dela).
            shelf.position = at + Vector3.right * 2.6f;
            var w = ArenaUtil.Lit(new Color(0.48f, 0.3f, 0.15f));
            ArenaUtil.Prim(PrimitiveType.Cube, shelf, new Vector3(0f, 1.4f, 0f), new Vector3(1.8f, 2.8f, 0.5f), w, keepCollider: true);
            for (int i = 0; i < 4; i++)
                ArenaUtil.Prim(PrimitiveType.Cube, shelf, new Vector3(0f, 0.45f + i * 0.65f, -0.27f), new Vector3(1.7f, 0.06f, 0.06f), ArenaUtil.Lit(new Color(0.3f, 0.18f, 0.08f)));
            for (int i = 0; i < 6; i++)   // livros
                ArenaUtil.Prim(PrimitiveType.Cube, shelf, new Vector3(-0.7f + i * 0.28f, 0.7f + (i % 3) * 0.65f, -0.15f), new Vector3(0.1f, 0.3f, 0.22f), ArenaUtil.Lit(Color.HSVToRGB(i * 0.15f % 1f, 0.6f, 0.7f)));
        }

        IEnumerator BossEntrance(bool full)
        {
            bossStage = 1;
            UpdateArea();
            var pl = game.Player;
            var doorway = new Vector3(0f, 0f, R(7).MinZ);
            if (full)
            {
                ArenaAudio.Play2D(Sfx.Slam, 0.9f, 0.5f);
                game.Message("...passos pesados atrás de você.", 2.5f);
                yield return new WaitForSeconds(2f);
            }
            // O Rei nasce no corredor, parado (sem IA), e entra curvado pela porta.
            var def = EnemyDef.ReiGoblin();
            boss = game.Spawn(def, doorway + Vector3.back * 3.5f, false);
            boss.Showcase = true;
            boss.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            game.SetBoss(boss);
            float t = 0f, walk = full ? 3.2f : 1.4f;
            var from = boss.transform.position; var to = doorway + Vector3.forward * 3f;
            while (t < walk && boss)
            {
                t += Time.deltaTime;
                float k = t / walk;
                boss.transform.position = Vector3.Lerp(from, to, k);
                // Curva-se para passar na porta (mais forte no meio do caminho).
                float bend = Mathf.Clamp01(1f - Mathf.Abs(boss.transform.position.z - doorway.z) / 1.6f) * 40f;
                boss.transform.rotation = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(bend, 0f, 0f);
                if (Random.value < 0.04f) ArenaAudio.Play(Sfx.Step, boss.transform.position, 0.9f, 0.5f);
                yield return null;
            }
            if (!boss) yield break;
            boss.transform.rotation = Quaternion.LookRotation((pl.FeetPosition - boss.transform.position).Flat().normalized);
            if (full)
            {
                // Arranca a porta e usa como escudo (só visual nesta versão).
                yield return new WaitForSeconds(0.6f);
                if (door) { ArenaAudio.Play(Sfx.Broken, door.position + Vector3.up, 1f, 0.6f); ArenaFx.Burst(door.position + Vector3.up * 1.4f + Vector3.right, new Color(0.5f, 0.35f, 0.2f), 30, 4f, 1.3f); Destroy(door.gameObject); door = null; }
            }
            AttachDoorShield();
            if (full)
            {
                yield return new WaitForSeconds(0.8f);
                // Ruge e a estante tomba sobre a saída.
                ArenaAudio.Play2D(Sfx.Roar, 1f, 0.8f);
                yield return StartCoroutine(TopplShelf());
            }
            yield return new WaitForSeconds(full ? 0.6f : 0.3f);
            if (!boss) yield break;
            boss.Showcase = false;
            bossStage = 2;
            game.Message("REI GOBLIN", 2f);
        }

        void AttachDoorShield()
        {
            if (!boss) return;
            if (bossShield) Destroy(bossShield.gameObject);
            bossShield = new GameObject("Escudo de porta").transform;
            bossShield.SetParent(boss.transform, false);
            bossShield.localPosition = new Vector3(-0.75f, 1.3f, 0.55f);
            bossShield.localRotation = Quaternion.Euler(0f, 25f, 0f);
            bossShield.localScale = Vector3.one * 0.55f;
            BuildDoorBoards(bossShield, Vector3.zero, collider: false);   // sem colisor: o bloqueio é do GoblinKing (BlockMultiplier/AbsorbDamage)
            if (boss is GoblinKing king) king.GiveDoorShield(bossShield);
        }

        IEnumerator TopplShelf()
        {
            if (!shelf || shelfToppled) yield break;
            shelfToppled = true;
            var start = shelf.rotation; var pivot = shelf.position;
            var startPos = shelf.position;
            float t = 0f;
            while (t < 0.8f && shelf)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.8f); k *= k;
                shelf.rotation = start * Quaternion.Euler(0f, 0f, 90f * k);   // tomba para o lado da saída (-x)
                shelf.position = startPos + new Vector3(-1.2f * k, 0.9f * k, 0f);   // deitada atravessando a saída
                yield return null;
            }
            if (!shelf) yield break;
            ArenaAudio.Play(Sfx.Slam, shelf.position, 1f, 0.7f);
            ArenaFx.Burst(shelf.position + Vector3.up * 0.4f, new Color(0.55f, 0.45f, 0.35f), 30, 3f, 1.4f);
            ArenaInput.Haptic(0.9f, 0.2f);
            UpdateArea();
        }
        bool shelfToppled;

        void UpdateShelf(float dt)
        {
            if (!shelf || shelfBurnt || !shelfToppled) return;
            if (shelfFire >= 0f)
            {
                shelfFire += dt;
                if (Random.value < 0.6f) ArenaFx.Burst(shelf.position + new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.1f, 0.9f), Random.Range(-0.3f, 0.3f)), new Color(1f, 0.45f, 0.1f), 2, 1.5f, 0.8f);
                if (shelfFire > 2.5f)
                {
                    shelfBurnt = true;
                    ArenaFx.Burst(shelf.position + Vector3.up * 0.4f, new Color(0.2f, 0.18f, 0.16f), 40, 3f, 1.6f);
                    ArenaAudio.Play(Sfx.Broken, shelf.position, 1f, 0.8f);
                    Destroy(shelf.gameObject); shelf = null;
                    game.Message("O caminho está livre.", 2f);
                    UpdateArea();
                }
                return;
            }
            if (bossStage != 3) return;
            // Pega fogo com a espada em chamas encostando ou uma explosão de fogo perto.
            var pl = game.Player;
            var w = pl.Weapon;
            bool touch = FlameBlade.Burning && w && w.Held && SegmentNear(w.BasePoint, w.TipPoint, shelf.position + Vector3.up * 0.4f, 1.7f);
            if (touch || lastFireBlast > Time.time - 0.1f && Vector3.Distance(lastFireBlastPos, shelf.position) < 3f)
            {
                shelfFire = 0f;
                ArenaAudio.Play(Sfx.Explosion, shelf.position, 0.6f, 1.2f);
            }
            else if (hintCd2 <= 0f && (pl.FeetPosition - shelf.position).Flat().magnitude < 2.5f)
            {
                hintCd2 = 4f;
                game.Message("Madeira velha... ponha FOGO nela (espada em chamas ou bola de fogo).", 2.5f);
            }
            hintCd2 -= dt;
        }
        float hintCd2;
        static float lastFireBlast = -10f; static Vector3 lastFireBlastPos;
        public static void OnFireExplosion(Vector3 at) { lastFireBlast = Time.time; lastFireBlastPos = at; }

        static bool SegmentNear(Vector3 a, Vector3 b, Vector3 p, float r)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            var c = a + ab * t;
            return new Vector2(c.x - p.x, c.z - p.z).magnitude < r && Mathf.Abs(c.y - p.y) < 1f;
        }

        // ---------- Fim: coroa e saída ----------
        Transform crown;
        int crownHand = -1;
        float crownT;
        bool crowned, ended;

        public void OnBossKilled(Vector3 at)
        {
            bossStage = 3;
            boss = null;
            UpdateArea();
            crown = new GameObject("Coroa").transform;
            crown.SetParent(root, false);
            crown.position = new Vector3(at.x, 0.15f, at.z);
            ArenaUtil.Prim(PrimitiveType.Cylinder, crown, Vector3.zero, new Vector3(0.28f, 0.06f, 0.28f), gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                ArenaUtil.Prim(PrimitiveType.Cube, crown, new Vector3(Mathf.Sin(a) * 0.12f, 0.1f, Mathf.Cos(a) * 0.12f), new Vector3(0.05f, 0.12f, 0.05f), gold);
            }
            var lbl = ArenaFx.MakeText(crown, "COROA\n<size=40>pegue e ponha na cabeça</size>", 0.004f, new Color(1f, 0.85f, 0.35f));
            lbl.transform.localPosition = Vector3.up * 0.45f;
            game.Message("O Rei caiu! A coroa é sua. Depois queime a estante para sair.", 4f);
        }

        void UpdateCrown()
        {
            if (!crown || crowned) return;
            var pl = game.Player;
            var lbl = crown.GetComponentInChildren<TextMesh>();
            if (lbl) lbl.transform.rotation = Quaternion.LookRotation(lbl.transform.position - pl.HeadPosition);
            if (crownHand < 0)
            {
                for (int s = 0; s < 2; s++)
                {
                    var h = s == 0 ? pl.leftHand : pl.rightHand;
                    bool grip = s == 0 ? pl.Input.LeftGripHeld || HandPoses.Closed(pl, 0) : pl.Input.GripHeld || HandPoses.Closed(pl, 1);
                    if (pl.IsVr && Vector3.Distance(h.position, crown.position) < 0.3f && grip) { crownHand = s; ArenaInput.Haptic(0.5f, 0.05f, left: s == 0); break; }
                }
                if (!pl.IsVr && Vector3.Distance(pl.FeetPosition.Flat(), crown.position.Flat()) < 1.2f && UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true) Crown();
                return;
            }
            var hand = crownHand == 0 ? pl.leftHand : pl.rightHand;
            crown.position = hand.position + Vector3.up * 0.05f;
            bool still = crownHand == 0 ? pl.Input.LeftGripHeld || HandPoses.Closed(pl, 0) : pl.Input.GripHeld || HandPoses.Closed(pl, 1);
            if (Vector3.Distance(hand.position, pl.HeadPosition + Vector3.up * 0.1f) < 0.25f) { Crown(); return; }
            if (!still) { crownHand = -1; crown.position = new Vector3(crown.position.x, 0.15f, crown.position.z); }
        }

        void Crown()
        {
            crowned = true;
            var pl = game.Player;
            if (crown) Destroy(crown.gameObject);
            ArenaFx.Burst(pl.HeadPosition + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.3f), 40, 3f, 1.2f);
            ArenaAudio.Play2D(Sfx.Level, 1f, 0.9f);
            game.Message("VIDA LONGA AO NOVO REI!", 3f);
        }

        void UpdateEnd(float dt)
        {
            if (ended || bossStage != 3 || !shelfBurnt) return;
            var p = game.Player.FeetPosition;
            if (!R(8).Contains(p.x, p.z, -1.5f)) return;
            ended = true;
            var head = game.Player.Head;
            var t = ArenaFx.MakeText(root, "FIM DA DEMO\n<size=50>Obrigado por jogar!</size>\n<size=34>Arena VR — em desenvolvimento</size>", 0.012f, new Color(1f, 0.9f, 0.6f));
            t.transform.position = game.Player.HeadPosition + game.Player.HeadForward.Flat().normalized * 3f;
            t.transform.rotation = Quaternion.LookRotation(game.Player.HeadForward.Flat().normalized);
            ArenaAudio.Play2D(Sfx.Level, 1f, 0.7f);
            StartCoroutine(RestartLater());
        }

        IEnumerator RestartLater()
        {
            yield return new WaitForSeconds(12f);
            game.StartDemo();
        }

        // ---------- Área de andar e encontros ----------
        // Barra o caminho além do ponto atual: amarrado = só a cela; porta fechada = até a fogueira; estante = até o chefe.
        void UpdateArea()
        {
            var pl = game.Player;
            int last = rooms.Count - 1;
            if (pl.Bound) last = 0;
            else if (!doorOpen) last = 6;
            else if (!shelfBurnt) last = 7;
            if (activeRoom != null) { ArenaUtil.Area = new WalkArea(new[] { activeRoom.Rect }); return; }
            var list = new List<Rect2>();
            for (int i = 0; i <= last; i++) { list.Add(R(i)); if (i < last) list.Add(corridors[i]); }
            if (!pl.Bound && !doorOpen) list.Add(corridors[6]);   // o corredor até a porta (ela fica no fim dele)
            ArenaUtil.Area = new WalkArea(list);
        }

        void UpdateRooms(float dt)
        {
            var p = game.Player.FeetPosition;
            if (activeRoom == null)
            {
                foreach (var r in rooms)
                    if (!r.Triggered && r.Enemies.Count > 0 && r.Rect.Contains(p.x, p.z, -1.2f)) { Trigger(r); break; }
                // Cena 7 depois de morrer: entrou de novo na sala do chefe = o Rei volta (cena curta).
                if (bossStage == 2 && boss == null && doorOpen && R(7).Contains(p.x, p.z, -2.5f)) StartCoroutine(BossEntrance(full: false));
                return;
            }
            checkCd -= dt;
            if (checkCd > 0f) return;
            checkCd = 0.25f;
            foreach (var e in ArenaEnemy.All) if (e && !e.Dead) return;
            var cleared = activeRoom;
            cleared.Cleared = true;
            activeRoom = null;
            foreach (var b in bars) if (b) Destroy(b);
            bars.Clear();
            ArenaAudio.Play2D(Sfx.Rattle, 0.6f, 0.7f);
            UpdateArea();
            cleared.OnClear?.Invoke();
        }

        void Trigger(Room r)
        {
            r.Triggered = true;
            activeRoom = r;
            var c = new Vector3(r.Rect.CenterX, 0f, r.Rect.CenterZ);
            var rr = r.Rect.Shrink(1.8f);
            for (int i = 0; i < r.Enemies.Count; i++)
            {
                // Na metade de longe da entrada (a entrada é sempre em -z).
                var pos = new Vector3(Mathf.Lerp(rr.MinX, rr.MaxX, (i + 0.5f) / r.Enemies.Count), 0f, Mathf.Lerp(c.z, rr.MaxZ, 0.6f));
                var e = game.Spawn(r.Enemies[i], pos, false);
                r.OnSpawned?.Invoke(e);
                ArenaFx.Burst(pos + Vector3.up * 0.5f, new Color(0.6f, 0.42f, 1f), 14, 2.5f);
            }
            ArenaAudio.Play2D(Sfx.Windup, 0.7f, 0.6f);
            // Grade nas duas aberturas (entrada e saída)
            var mat = ArenaUtil.Lit(new Color(0.25f, 0.22f, 0.2f), new Color(0.35f, 0.08f, 0.04f));
            foreach (float z in new[] { r.Rect.MinZ, r.Rect.MaxZ })
            {
                var g = new GameObject("Grade"); g.transform.SetParent(root, false);
                for (float x = -CorW / 2f + 0.25f; x < CorW / 2f; x += 0.5f)
                    ArenaUtil.Prim(PrimitiveType.Cylinder, g.transform, new Vector3(x, 1.5f, z), new Vector3(0.08f, 1.5f, 0.08f), mat, keepCollider: true);
                bars.Add(g);
            }
            ArenaAudio.Play(Sfx.Slam, c, 0.7f, 1.3f);
            UpdateArea();
        }

        public void OnPlayerDied()
        {
            // A sala em combate volta ao começo (os inimigos já foram apagados pelo ArenaGame).
            if (activeRoom != null) { activeRoom.Triggered = false; activeRoom = null; }
            foreach (var b in bars) if (b) Destroy(b);
            bars.Clear();
            if (bossStage == 1 || bossStage == 2) { StopAllCoroutines(); bossStage = 2; boss = null; }
            UpdateArea();
        }

        void Update()
        {
            if (!Active || ArenaGame.Blocked) return;
            float dt = Time.deltaTime;
            UpdateBound(dt);
            UpdateStatue();
            UpdateOrbs();
            UpdateDoor(dt);
            UpdateRooms(dt);
            UpdateShelf(dt);
            UpdateCrown();
            UpdateEnd(dt);
            // Passou da estátua / da fogueira: checkpoint.
            var p = game.Player.FeetPosition;
            if (R(6).Contains(p.x, p.z) && game.bonfire != checkpoints[2]) SetCheckpoint(checkpoints[2]);
        }
    }
}
