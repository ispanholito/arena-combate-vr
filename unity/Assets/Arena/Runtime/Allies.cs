using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Bonecos dos NPCs (primitivas com cor e chapéu de cada um). Regras em Core/Allies.cs.
    public static class NpcFigure
    {
        public static Transform Build(Transform parent, NpcId n)
        {
            var root = new GameObject(AllyRules.Name(n)).transform;
            root.SetParent(parent, false);
            var (robe, hat) = n switch
            {
                NpcId.Domadora => (new Color(0.3f, 0.6f, 0.3f), new Color(0.9f, 0.75f, 0.3f)),
                NpcId.Capitao => (new Color(0.2f, 0.3f, 0.6f), new Color(0.55f, 0.55f, 0.6f)),
                _ => (new Color(0.5f, 0.2f, 0.45f), new Color(0.3f, 0.9f, 0.8f)),
            };
            var skin = ArenaUtil.Lit(new Color(0.86f, 0.66f, 0.52f));
            ArenaUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.8f, 0.4f), ArenaUtil.Lit(robe));
            ArenaUtil.Prim(PrimitiveType.Sphere, root, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.32f, skin);
            var hm = ArenaUtil.Lit(hat, hat * 0.2f);
            switch (n)
            {
                case NpcId.Domadora:   // chapéu de aba larga com pena
                    ArenaUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 1.88f, 0f), new Vector3(0.6f, 0.02f, 0.6f), hm);
                    ArenaUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 1.95f, 0f), new Vector3(0.3f, 0.08f, 0.3f), hm);
                    ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(0.12f, 2.05f, 0f), new Vector3(0.03f, 0.2f, 0.02f), ArenaUtil.Lit(new Color(0.9f, 0.3f, 0.3f)));
                    break;
                case NpcId.Capitao:    // elmo e espada nas costas
                    ArenaUtil.Prim(PrimitiveType.Sphere, root, new Vector3(0f, 1.78f, 0f), new Vector3(0.36f, 0.26f, 0.36f), hm);
                    ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(0f, 1.0f, -0.26f), new Vector3(0.06f, 1.0f, 0.02f), hm).transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    break;
                default:               // chapéu pontudo e frasco brilhando
                    ArenaUtil.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 1.9f, 0f), new Vector3(0.36f, 0.04f, 0.36f), hm);
                    ArenaUtil.Prim(PrimitiveType.Capsule, root, new Vector3(0f, 2.1f, 0f), new Vector3(0.14f, 0.2f, 0.14f), hm);
                    ArenaUtil.Prim(PrimitiveType.Sphere, root, new Vector3(0.3f, 1.0f, 0.15f), Vector3.one * 0.12f, ArenaUtil.Lit(hat, hat));
                    break;
            }
            return root;
        }
    }

    // Jaula na sala de combate limpa: grip/E perto abre e o NPC vai morar no santuário.
    public sealed class RescueCage : MonoBehaviour
    {
        public static readonly List<RescueCage> All = new List<RescueCage>();
        const float OpenRange = 1.7f;
        NpcId npc;
        bool open;
        TextMesh label;

        public static RescueCage Spawn(Vector3 pos, NpcId npc, Transform parent)
        {
            var go = new GameObject("Jaula");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            var c = go.AddComponent<RescueCage>();
            c.npc = npc;
            NpcFigure.Build(go.transform, npc);
            var iron = ArenaUtil.Lit(new Color(0.25f, 0.24f, 0.24f), new Color(0.03f, 0.03f, 0.03f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                ArenaUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(Mathf.Cos(a) * 0.55f, 1.15f, Mathf.Sin(a) * 0.55f), new Vector3(0.04f, 1.15f, 0.04f), iron, keepCollider: true);
            }
            ArenaUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 2.32f, 0f), new Vector3(1.2f, 0.04f, 1.2f), iron);
            ArenaUtil.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.04f, 1.2f), iron);
            c.label = ArenaFx.MakeText(go.transform, "SOCORRO!\n<size=40>" + AllyRules.Name(npc) + " (" + AllyRules.Role(npc) + ")\ngrip / E para abrir</size>", 0.008f, new Color(1f, 0.9f, 0.5f));
            c.label.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            All.Add(c);
            ArenaAudio.Play(Sfx.Rattle, pos + Vector3.up, 0.8f, 1f);
            return c;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam && label) { var d = label.transform.position - cam.transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) label.transform.rotation = Quaternion.LookRotation(d); }
        }

        public static bool TryOpen(ArenaPlayer player)
        {
            foreach (var c in All)
            {
                if (!c || c.open) continue;
                float d = player.IsVr
                    ? Mathf.Min(Vector3.Distance(player.RightHand.position, c.transform.position + Vector3.up), (player.FeetPosition - c.transform.position).Flat().magnitude)
                    : (player.FeetPosition - c.transform.position).Flat().magnitude;
                if (d > OpenRange) continue;
                c.Open(player);
                return true;
            }
            return false;
        }

        void Open(ArenaPlayer player)
        {
            open = true;
            player.State.Rescued.Add(npc);
            var p = transform.position + Vector3.up;
            ArenaFx.Burst(p, new Color(1f, 0.9f, 0.5f), 30, 3f);
            ArenaAudio.Play2D(Sfx.Level, 1f, 1.1f);
            ArenaInput.Haptic(0.7f, 0.15f);
            ArenaGame.Instance?.Message("Resgatou " + AllyRules.Name(npc) + "! Agora mora no santuário (" + AllyRules.Role(npc) + ")", 3f);
            ArenaGame.Instance?.RequestSave();
            Destroy(gameObject);
        }

        void OnDestroy() => All.Remove(this);
    }

    // Pet: fada que voa perto do ombro esquerdo. Faísca = raio pequeno no inimigo mais perto; Curandeira = cura devagar.
    public sealed class PetCompanion : MonoBehaviour
    {
        ArenaPlayer player;
        Transform fairy;
        PetKind shown;
        float cd, t;
        Material glow;

        void Awake() => player = GetComponent<ArenaPlayer>();

        void Update()
        {
            var game = ArenaGame.Instance;
            var kind = player.State != null && game && game.Mode != ArenaGame.GameMode.Menu ? player.State.Pet : PetKind.Nenhum;
            if (kind != shown) Rebuild(kind);
            if (!fairy || ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            t += dt;
            // Flutua perto do ombro esquerdo.
            var head = player.Head;
            var target = head.position - head.right * 0.45f + Vector3.up * (0.05f + Mathf.Sin(t * 3f) * 0.06f) + head.forward * 0.2f;
            fairy.position = Vector3.Lerp(fairy.position, target, 1f - Mathf.Exp(-6f * dt));
            if (Random.value < dt * 4f) ArenaFx.Burst(fairy.position, glow.color, 1, 0.3f);
            if (ArenaGame.Blocked) return;
            if (kind == PetKind.FadaCurandeira) { if (player.State.Hp.Current < player.State.Hp.Max) player.State.Hp.Restore(AllyRules.HealerPerSecond * dt); return; }
            cd -= dt;
            if (cd > 0f) return;
            ArenaEnemy best = null; float bestD = AllyRules.FairyRange;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead || e.Def.Kind == EnemyKind.Boneco && game.Mode != ArenaGame.GameMode.Treino) continue;
                float d = Vector3.Distance(e.Center, player.HeadPosition);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (!best) return;
            cd = AllyRules.FairyInterval;
            best.ApplyDamage(AllyRules.FairyDamage, 0.3f, best.Center - fairy.position, 0.5f, new Color(1f, 0.95f, 0.5f));
            var go = new GameObject("RaioFada");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, fairy.position); lr.SetPosition(1, best.Center);
            lr.startWidth = 0.025f; lr.endWidth = 0.01f; lr.material = ArenaUtil.Unlit(new Color(1f, 0.95f, 0.5f));
            Destroy(go, 0.08f);
            ArenaAudio.Play(Sfx.Shot, fairy.position, 0.4f, 2.2f);
        }

        void Rebuild(PetKind kind)
        {
            shown = kind;
            if (fairy) Destroy(fairy.gameObject);
            if (kind == PetKind.Nenhum) return;
            var c = kind == PetKind.FadaFaisca ? new Color(1f, 0.9f, 0.4f) : new Color(0.45f, 1f, 0.6f);
            glow = ArenaUtil.Unlit(c);
            fairy = new GameObject(AllyRules.PetName(kind)).transform;
            fairy.position = player.HeadPosition;
            ArenaUtil.Prim(PrimitiveType.Sphere, fairy, Vector3.zero, Vector3.one * 0.07f, glow);
            var wing = ArenaUtil.Unlit(new Color(1f, 1f, 1f, 0.5f));
            ArenaUtil.Prim(PrimitiveType.Cube, fairy, new Vector3(0.05f, 0.02f, 0f), new Vector3(0.08f, 0.05f, 0.005f), wing);
            ArenaUtil.Prim(PrimitiveType.Cube, fairy, new Vector3(-0.05f, 0.02f, 0f), new Vector3(0.08f, 0.05f, 0.005f), wing);
            var l = fairy.gameObject.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 1.5f; l.intensity = 1f;
        }
    }

    // Mercenário contratado com o Capitão: segue você e ataca quem estiver perto. Dura uma ida (arena ou dungeon).
    // Simplificação: os inimigos não miram nele (não morre); ele é um reforço de dano, não um tanque.
    public sealed class Mercenary : MonoBehaviour
    {
        public static Mercenary Instance { get; private set; }
        ArenaPlayer player;
        Transform sword;
        float cd, swingT = -1f;
        ArenaEnemy target;

        public static void Spawn(ArenaPlayer pl)
        {
            Clear();
            var go = new GameObject("Mercenario");
            go.transform.position = pl.FeetPosition - pl.HeadForward.Flat().normalized * 1.5f + Vector3.Cross(Vector3.up, pl.HeadForward.Flat()).normalized * 1f;
            Instance = go.AddComponent<Mercenary>();
            Instance.player = pl;
            Instance.Build();
            ArenaGame.Instance?.Message("O mercenário do Capitão veio com você", 2f);
        }

        public static void Clear() { if (Instance) Destroy(Instance.gameObject); Instance = null; }

        void Build()
        {
            var armor = ArenaUtil.Lit(new Color(0.3f, 0.4f, 0.7f), new Color(0.03f, 0.05f, 0.1f));
            var steel = ArenaUtil.Lit(new Color(0.75f, 0.77f, 0.8f), new Color(0.06f, 0.06f, 0.07f));
            ArenaUtil.Prim(PrimitiveType.Capsule, transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.85f, 0.45f), armor);
            ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, 1.8f, 0f), Vector3.one * 0.34f, steel);
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 1.8f, 0.15f), new Vector3(0.22f, 0.04f, 0.06f), ArenaUtil.Unlit(new Color(0.1f, 0.1f, 0.12f)));
            ArenaUtil.Prim(PrimitiveType.Cube, transform, new Vector3(-0.35f, 1.0f, 0.15f), new Vector3(0.08f, 0.6f, 0.45f), armor);   // escudo
            sword = new GameObject("Espada").transform;
            sword.SetParent(transform, false);
            sword.localPosition = new Vector3(0.35f, 1.1f, 0.1f);
            ArenaUtil.Prim(PrimitiveType.Cube, sword, new Vector3(0f, 0.45f, 0f), new Vector3(0.05f, 0.8f, 0.015f), steel);
            var label = ArenaFx.MakeText(transform, "MERCENÁRIO", 0.005f, new Color(0.6f, 0.75f, 1f));
            label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        }

        void Update()
        {
            if (!player || ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            var feet = player.FeetPosition;
            // Longe demais (troca de sala, escada): reaparece atrás de você.
            if ((transform.position - feet).Flat().magnitude > AllyRules.MercLeash || ArenaUtil.IsOutside(transform.position + Vector3.up, 0.2f))
                transform.position = feet - player.HeadForward.Flat().normalized * 1.5f;
            cd -= dt;
            if (!target || target.Dead || (target.Center - feet).Flat().magnitude > AllyRules.MercAggro) target = PickTarget(feet);
            Vector3 goal = target ? target.transform.position : feet + Vector3.Cross(Vector3.up, player.HeadForward.Flat()).normalized * 1.2f - player.HeadForward.Flat().normalized * 0.8f;
            var to = (goal - transform.position).Flat();
            float stop = target ? AllyRules.MercReach * 0.8f : 0.5f;
            if (to.magnitude > stop)
            {
                transform.position = ArenaUtil.ClampToArena(transform.position + to.normalized * Mathf.Min(to.magnitude - stop, AllyRules.MercSpeed * dt));
            }
            var look = target ? (target.transform.position - transform.position).Flat() : to;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-8f * dt));
            // Golpe
            if (swingT >= 0f)
            {
                swingT += dt;
                sword.localRotation = Quaternion.Euler(Mathf.Lerp(-60f, 110f, swingT / 0.25f), 0f, 0f);
                if (swingT >= 0.25f) { swingT = -1f; sword.localRotation = Quaternion.identity; }
            }
            if (target && cd <= 0f && (target.transform.position - transform.position).Flat().magnitude <= AllyRules.MercReach + target.Radius && !ArenaGame.Blocked)
            {
                cd = AllyRules.MercInterval;
                swingT = 0f;
                target.ApplyDamage(AllyRules.MercDamage, 0.8f, (target.Center - transform.position), 2f, new Color(0.6f, 0.75f, 1f));
                ArenaAudio.Play(Sfx.Hit, target.Center, 0.7f, 1.1f);
            }
        }

        ArenaEnemy PickTarget(Vector3 feet)
        {
            ArenaEnemy best = null; float bestD = AllyRules.MercAggro;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead || e.Def.Kind == EnemyKind.Boneco) continue;
                float d = (e.Center - transform.position).Flat().magnitude;
                if ((e.Center - feet).Flat().magnitude < AllyRules.MercAggro && d < bestD) { bestD = d; best = e; }
            }
            return best;
        }
    }
}
