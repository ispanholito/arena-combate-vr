using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Sala de Criação (pedido do Pablo, 2026-10-30): vitrine para AVALIAR o que fazemos sem precisar simular uma
    // batalha — dá para olhar até no PC, sem o headset. Roda dentro do Treino (sem dano, nível 10). Portal "CRIAÇÃO"
    // no santuário. Cada estação fica rodando sozinha:
    //   - Queda e levantar: um goblin físico cai a cada poucos segundos e levanta.
    //   - Andar: um goblin físico anda para frente, de lado, para trás e corre (passos e direção do corpo).
    //   - Capacetes: goblin, esqueleto e orc físicos parados, todos de capacete.
    //   - Rei Goblin: o chefe parado (imune) para ver o visual.
    //   - Inimigos: todos os tipos parados, na versão que o jogo está usando (físicos ou não, conforme a opção).
    //   - Armas: todas as armas no chão (objetos físicos; pegue para ver na mão).
    //   - Espada cravada: cadáver com a espada no peito; segure o cabo e puxe (ela vai saindo). Volta 3 s depois.
    //   - Paleta: as cores da docs/biblia-de-arte.md.
    // REGRA: todo design novo ganha uma estação aqui.
    public sealed class CreationRoom : MonoBehaviour
    {
        public static CreationRoom Instance { get; private set; }

        static readonly Vector3 Center = new Vector3(0f, 0f, 0f);
        static readonly Vector3 FallSpot = new Vector3(-5f, 0f, 3f);
        static readonly Vector3 WalkSpot = new Vector3(0f, 0f, 5.5f);
        static readonly Vector3 HelmetSpot = new Vector3(5f, 0f, 3f);
        static readonly Vector3 KingSpot = new Vector3(6f, 0f, -3f);
        static readonly Vector3 WeaponSpot = new Vector3(-6.5f, 0f, -3f);
        static readonly Vector3 StuckSpot = new Vector3(-2.6f, 0f, -3.6f);
        static readonly Vector3 LineStart = new Vector3(-4.8f, 0f, -6.5f);

        RagdollGoblin faller, walker;
        readonly RagdollGoblin[] helmets = new RagdollGoblin[3];
        static readonly EnemyDef[] HelmetKinds = { EnemyDef.Goblin, EnemyDef.Esqueleto, EnemyDef.Orc };
        ArenaEnemy king;
        readonly List<ArenaEnemy> line = new List<ArenaEnemy>();
        readonly List<WeaponPickup> weapons = new List<WeaponPickup>();
        StuckSword stuck;
        WeaponPickup stuckShield;   // o escudo do aventureiro caído, ao lado (roteiro: acha os dois juntos)
        Transform root;
        float fallT = 2f, respawnT, walkT, stuckT;

        void Awake()
        {
            Instance = this;
            root = new GameObject("SalaDeCriacao").transform;
            // O Treino coloca o boneco no centro: aqui ele sai.
            foreach (var e in ArenaEnemy.All.ToArray()) if (e is TrainingDummy) Destroy(e.gameObject);
            Label(FallSpot, "QUEDA E LEVANTAR", "cai sozinho, levanta e fica 2,5 s de pé");
            Label(WalkSpot, "ANDAR", "frente, lado, trás e correndo");
            Label(HelmetSpot, "CAPACETES", "goblin · esqueleto · orc");
            Label(KingSpot, "REI GOBLIN", "chefe da demo (imune)");
            Label(WeaponSpot, "ARMAS", "pegue para ver na mão");
            Label(StuckSpot, "ESPADA CRAVADA", "segure o cabo e puxe; o escudo está do lado");
            Label(LineStart + new Vector3(4.8f, 0f, 0f), "INIMIGOS", "como aparecem no jogo");
            Palette(new Vector3(-10.5f, 0f, 3.5f));
            SpawnWeapons();
            var game = ArenaGame.Instance;
            game.Player.TeleportTo(Center + new Vector3(0f, 0f, -1.5f), WalkSpot);
            game.Message("SALA DE CRIAÇÃO: olhe as estações em volta. Pausa → voltar ao santuário.", 4f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root) Destroy(root.gameObject);
            foreach (var g in new[] { faller, walker }) if (g) Destroy(g.gameObject);
            foreach (var g in helmets) if (g) Destroy(g.gameObject);
            foreach (var w in weapons) if (w) Destroy(w.gameObject);
            if (stuck) Destroy(stuck.gameObject);
            if (stuckShield) Destroy(stuckShield.gameObject);
        }

        void Update()
        {
            if (ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            respawnT -= dt;
            if (respawnT <= 0f) { respawnT = 1.5f; Respawn(); }
            // Espada cravada: depois de arrancada, outro cadáver com espada aparece em 3 s.
            if (!stuck || stuck.Freed) { stuckT -= dt; if (stuckT <= 0f) { if (stuck) Destroy(stuck.gameObject); stuck = StuckSword.Spawn(new ItemInstance(WeaponDef.EspadaLonga), StuckSpot, Vector3.forward, root); stuckT = 3f;
                if (!stuckShield) stuckShield = WeaponPickup.Spawn(new ItemInstance(WeaponDef.Escudo), StuckSpot + new Vector3(0.7f, 0.2f, 0.1f)); } }

            // Queda e levantar
            fallT -= dt;
            // Conta o tempo DE PÉ: fica 2,5 s em pé (dá para ver o fim do levantar) e cai de novo.
            if (faller && !faller.Standing) fallT = 2.5f;
            else if (faller && fallT <= 0f) faller.ForceKnockDown();

            // Andar: ciclo de 12 s olhando para o centro (frente, direita, trás, esquerda) + corrida ida e volta.
            if (walker)
            {
                walkT += dt;
                float c = walkT % 12f;
                var toCenter = (Center - WalkSpot).normalized;
                var right = Vector3.Cross(Vector3.up, toCenter);
                Vector3 v; bool run = false;
                if (c < 2f) v = toCenter;
                else if (c < 4f) v = right;
                else if (c < 6f) v = -toCenter;
                else if (c < 8f) v = -right;
                else if (c < 9.5f) { v = right * 2.2f; run = true; }
                else if (c < 11f) { v = -right * 2.2f; run = true; }
                else v = Vector3.zero;
                walker.ShowcaseVelocity = v;
                // Correndo olha para onde vai; andando olha para o centro (como na luta).
                walker.ShowcaseLook = run ? walker.FeetPosition + v : Center;
            }
            // Inimigos do jogo usando o corpo físico: o corpo fica parado também.
            foreach (var e in line) if (e is PhysicalGoblinEnemy pg && pg.Body) { pg.Body.Showcase = true; pg.Body.ShowcaseLook = Center; }
        }

        void Respawn()
        {
            if (!Alive(faller)) faller = Showcase(EnemyDef.Goblin, FallSpot);
            if (!Alive(walker)) walker = Showcase(EnemyDef.Goblin, WalkSpot);
            for (int i = 0; i < helmets.Length; i++)
                if (!Alive(helmets[i]))
                {
                    helmets[i] = Showcase(HelmetKinds[i], HelmetSpot + new Vector3((i - 1) * 1.4f, 0f, 0f));
                    helmets[i].ForceHelmet();
                }
            var game = ArenaGame.Instance;
            if (!king || king.Dead) king = Frozen(game.Spawn(EnemyDef.Boss(EnemyKind.ReiGoblin, 0), KingSpot, false));
            line.RemoveAll(e => !e || e.Dead);
            if (line.Count == 0)
            {
                var defs = new[] { EnemyDef.Slime(), EnemyDef.Goblin, EnemyDef.Esqueleto, EnemyDef.Orc, EnemyDef.Arqueiro, EnemyDef.Xama, EnemyDef.Mimico };
                for (int i = 0; i < defs.Length; i++) line.Add(Frozen(game.Spawn(defs[i], LineStart + new Vector3(i * 1.6f, 0f, 0f), false)));
            }
        }

        static bool Alive(RagdollGoblin g) => g && !g.State.Dead;

        RagdollGoblin Showcase(EnemyDef def, Vector3 at)
        {
            var g = RagdollGoblin.Spawn(at, Center, new EnemyState(def));
            g.Showcase = true;
            g.ShowcaseLook = Center;
            return g;
        }

        static ArenaEnemy Frozen(ArenaEnemy e)
        {
            if (!e) return e;
            e.Showcase = true;
            e.SpawnGrace = 1e6f;   // imune: é para olhar
            e.transform.rotation = Quaternion.LookRotation((Center - e.transform.position).Flat().normalized);
            return e;
        }

        void SpawnWeapons()
        {
            var all = WeaponDef.All;
            for (int i = 0; i < all.Length; i++)
            {
                var pos = WeaponSpot + new Vector3((i % 3 - 1) * 1.1f, 0.4f, (i / 3 - 2) * 0.9f);
                var w = WeaponPickup.Spawn(new ItemInstance(all[i]), pos);
                if (w) weapons.Add(w);
            }
        }

        void Label(Vector3 at, string title, string desc)
        {
            var t = ArenaFx.MakeText(root, title + "\n<size=40>" + desc + "</size>", 0.01f, new Color(1f, 0.9f, 0.6f));
            t.transform.position = at + Vector3.up * 2.7f;
            var away = (at - Center).Flat();
            t.transform.rotation = Quaternion.LookRotation(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
            ArenaUtil.Prim(PrimitiveType.Cylinder, root, at + Vector3.up * 0.02f, new Vector3(2.2f, 0.02f, 2.2f), ArenaUtil.Unlit(new Color(1f, 0.85f, 0.4f, 0.25f)));
        }

        // Paleta da bíblia de arte: um quadrado por cor com o nome.
        void Palette(Vector3 at)
        {
            var colors = new (string, Color)[]
            {
                ("Pedra", Hex(0x4B4F63)), ("Pedra clara", Hex(0x6B6F86)), ("Madeira", Hex(0x8A5A34)), ("Metal", Hex(0xC9CED8)), ("Bronze", Hex(0xE09A3D)),
                ("Goblin", Hex(0x6DBB4A)), ("Roupa goblin", Hex(0xC2452F)), ("Osso", Hex(0xEDE3C8)), ("Brasa", Hex(0xFF7A2A)), ("Orc", Hex(0x5E8F45)),
                ("Ferro orc", Hex(0x4A5168)), ("Dourado", Hex(0xFFC640)), ("Slime azul", Hex(0x4FC3F7)), ("Slime verde", Hex(0x9CE36B)), ("Perigo", new Color(1f, 0.16f, 0.06f)),
            };
            var board = new GameObject("Paleta").transform;
            board.SetParent(root, false);
            board.position = at;
            board.rotation = Quaternion.LookRotation((at - Center).Flat().normalized);
            for (int i = 0; i < colors.Length; i++)
            {
                var (name, c) = colors[i];
                var p = new Vector3((i % 5 - 2) * 0.7f, 2.2f - (i / 5) * 0.75f, 0f);
                ArenaUtil.Prim(PrimitiveType.Quad, board, p, new Vector3(0.55f, 0.45f, 1f), ArenaUtil.Unlit(c));
                var t = ArenaFx.MakeText(board, name, 0.004f, Color.white);
                t.transform.localPosition = p + new Vector3(0f, -0.3f, 0f);
                t.transform.localRotation = Quaternion.identity;
            }
            var title = ArenaFx.MakeText(board, "PALETA\n<size=40>docs/biblia-de-arte.md</size>", 0.01f, new Color(1f, 0.9f, 0.6f));
            title.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            title.transform.localRotation = Quaternion.identity;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
