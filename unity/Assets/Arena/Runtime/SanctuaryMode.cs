using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Santuário (hub): sala segura com a fogueira (cura, árvore, respec, troca de classe), a bigorna do ferreiro e
    // três portais (Arena, Dungeon, Treino). O jogo volta para cá depois de vencer a dungeon ou pela pausa.
    public sealed class SanctuaryMode : MonoBehaviour
    {
        public const float HalfSize = 8f;
        public const float AnvilRange = 1.6f, PortalRange = 1.1f;

        public bool Active => root;

        ArenaGame game;
        Transform root, anvil, forge;
        TextMesh anvilText, forgeText;
        static readonly System.Random fuseRng = new System.Random();
        readonly List<(Transform t, ArenaGame.GameMode mode)> portals = new List<(Transform, ArenaGame.GameMode)>();
        readonly Dictionary<ArenaGame.GameMode, (TextMesh label, string title, string desc)> portalLabels = new Dictionary<ArenaGame.GameMode, (TextMesh, string, string)>();
        readonly List<(Transform t, ArenaGame.GameMode mode)> resetStones = new List<(Transform, ArenaGame.GameMode)>();
        float resetArmT = -1f; ArenaGame.GameMode resetArmMode;
        float portalCd;

        void Awake() => game = GetComponent<ArenaGame>();

        public void Begin()
        {
            End();
            root = new GameObject("Santuario").transform;
            var area = new WalkArea(new[] { new Rect2(-HalfSize, -HalfSize, HalfSize, HalfSize) });
            ArenaUtil.Area = area;
            var stone = ArenaUtil.Lit(new Color(0.34f, 0.32f, 0.3f));
            var wall = ArenaUtil.Lit(new Color(0.3f, 0.27f, 0.25f));
            Box(new Vector3(0, -0.05f, 0), new Vector3(HalfSize * 2, 0.1f, HalfSize * 2), stone);
            foreach (var (x0, z0, x1, z1) in area.WallSegments())
            {
                bool alongX = Mathf.Approximately(z0, z1);
                float len = alongX ? x1 - x0 : z1 - z0;
                var c = new Vector3((x0 + x1) * 0.5f, 2.5f, (z0 + z1) * 0.5f);
                c += (alongX ? new Vector3(0, 0, Mathf.Sign(z0)) : new Vector3(Mathf.Sign(x0), 0, 0)) * 0.15f;
                Box(c, alongX ? new Vector3(len + 0.3f, 5f, 0.3f) : new Vector3(0.3f, 5f, len + 0.3f), wall);
            }
            // Luz quente geral e tochas nos cantos.
            Light(new Vector3(0, 4.5f, 0), 16f, 1.6f, new Color(1f, 0.75f, 0.5f));
            foreach (var p in new[] { new Vector3(-7.6f, 2.6f, -7.6f), new Vector3(7.6f, 2.6f, -7.6f) })
                Light(p, 7f, 1.6f, new Color(1f, 0.54f, 0.23f));

            // Fogueira no centro-sul, de frente para os portais.
            var fire = Bonfire(new Vector3(0, 0, -2.5f));
            game.bonfire = fire;

            // Bigorna do ferreiro (à esquerda da fogueira).
            anvil = new GameObject("Bigorna").transform;
            anvil.SetParent(root, false);
            anvil.position = new Vector3(-4f, 0, -2.5f);
            var iron = ArenaUtil.Lit(new Color(0.25f, 0.26f, 0.28f), new Color(0.03f, 0.03f, 0.04f));
            ArenaUtil.Prim(PrimitiveType.Cube, anvil, new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.6f, 0.35f), ArenaUtil.Lit(new Color(0.3f, 0.2f, 0.1f)), keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, anvil, new Vector3(0, 0.72f, 0), new Vector3(0.7f, 0.22f, 0.3f), iron, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, anvil, new Vector3(0.42f, 0.76f, 0), new Vector3(0.2f, 0.12f, 0.14f), iron);
            BuildForge(iron);
            anvilText = ArenaFx.MakeText(anvil, "", 0.005f, new Color(1f, 0.85f, 0.4f));
            anvilText.transform.localPosition = new Vector3(0, 1.6f, 0);

            // Portais na parede norte.
            portals.Clear();
            Portal(new Vector3(-4.5f, 0, 6f), ArenaGame.GameMode.Arena, "ARENA", "ondas e o Rei Goblin", new Color(1f, 0.45f, 0.3f));
            Portal(new Vector3(0f, 0, 6.5f), ArenaGame.GameMode.Dungeon, "DUNGEON", "2 andares, baús e o chefe", new Color(0.55f, 0.35f, 1f));
            Portal(new Vector3(4.5f, 0, 6f), ArenaGame.GameMode.Treino, "TREINO", "boneco; nível 10 (não salva)", new Color(0.4f, 0.9f, 0.5f));
            Portal(new Vector3(4.5f, 0, -5.5f), ArenaGame.GameMode.Menu, "TUTORIAL", "aprenda os gestos e poderes", new Color(1f, 0.85f, 0.4f));   // Menu = marcador do tutorial
            ResetStone(new Vector3(-2.7f, 0, 5.2f), ArenaGame.GameMode.Arena);
            ResetStone(new Vector3(1.8f, 0, 5.4f), ArenaGame.GameMode.Dungeon);

            var sign = ArenaFx.MakeText(root, "SANTUÁRIO\n<size=40>Fogueira: cura, árvore (A+B), respec e classe · Bigorna: ferreiro (grip/E) · Portais: entre neles</size>",
                                        0.008f, new Color(0.95f, 0.9f, 0.8f));
            sign.transform.position = new Vector3(0, 3.6f, -7.7f);
            sign.transform.rotation = Quaternion.LookRotation(Vector3.back);
            BuildRacks();   // estantes permanente (leste) e temporária (oeste) + mercador

            portalCd = 1.5f;
            game.Player.TeleportTo(game.BonfireStandPoint, new Vector3(0, 0, 6f));
        }

        public void End()
        {
            if (root) Destroy(root.gameObject);
            root = null;
            portals.Clear();
            portalLabels.Clear();
            resetStones.Clear();
        }

        GameObject Box(Vector3 pos, Vector3 size, Material m)
        {
            var go = ArenaUtil.Prim(PrimitiveType.Cube, root, pos, size, m, keepCollider: true);
            go.name = "Parede";
            return go;
        }

        void Light(Vector3 pos, float range, float intensity, Color c)
        {
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = pos;
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c;
        }

        Transform Bonfire(Vector3 pos)
        {
            var t = new GameObject("Fogueira do santuário").transform;
            t.SetParent(root, false);
            t.position = pos;
            t.rotation = Quaternion.LookRotation(Vector3.forward);
            var wood = ArenaUtil.Lit(new Color(0.3f, 0.19f, 0.1f));
            for (int i = 0; i < 3; i++)
                ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.08f, 0), new Vector3(0.1f, 0.35f, 0.1f), wood).transform.localRotation = Quaternion.Euler(90, i * 60, 0);
            ArenaUtil.Prim(PrimitiveType.Capsule, t, new Vector3(0, 0.4f, 0), Vector3.one * 0.3f, ArenaUtil.Lit(new Color(1f, 0.48f, 0.1f), new Color(1.5f, 0.6f, 0.1f)));
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0, 0.9f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.48f, 0.16f); l.range = 7f; l.intensity = 2.5f;
            return t;
        }

        // Pedra do recomeço ao lado dos portais da Arena e da Dungeon: grip duas vezes volta a profundidade a 1.
        void ResetStone(Vector3 pos, ArenaGame.GameMode mode)
        {
            var t = new GameObject("Pedra do recomeço").transform;
            t.SetParent(root, false);
            t.position = pos;
            ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.45f, 0.45f), ArenaUtil.Lit(new Color(0.3f, 0.3f, 0.33f)), keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0, 1f, 0), Vector3.one * 0.2f, ArenaUtil.Lit(new Color(0.5f, 0.8f, 1f), new Color(0.3f, 0.5f, 0.9f)));
            var lbl = ArenaFx.MakeText(t, "RECOMEÇAR\n<size=40>grip 2x: volta à profundidade 1</size>", 0.005f, new Color(0.7f, 0.85f, 1f));
            lbl.transform.localPosition = new Vector3(0, 1.45f, 0);
            resetStones.Add((t, mode));
        }

        int TierOf(ArenaGame.GameMode m) => m == ArenaGame.GameMode.Dungeon ? game.Player.State.DungeonTier : m == ArenaGame.GameMode.Arena ? game.Player.State.ArenaTier : 0;

        void UpdatePortalLabels()
        {
            foreach (var kv in portalLabels)
            {
                int tier = TierOf(kv.Key);
                string depth = tier > 0 ? "\n<size=46><color=#ffd36b>profundidade " + tier + "</color></size>" : "";
                kv.Value.label.text = kv.Value.title + depth + "\n<size=40>" + kv.Value.desc + "</size>";
            }
        }

        bool TryResetStone(ArenaPlayer player)
        {
            foreach (var (t, mode) in resetStones)
            {
                if (!t || !Near(player, t.position, 1.2f, 1.6f)) continue;
                if (TierOf(mode) <= 1) { game.Message("Já está na profundidade 1", 1.2f); return true; }
                if (resetArmT > 0f && resetArmMode == mode)
                {
                    if (mode == ArenaGame.GameMode.Dungeon) game.Player.State.DungeonTier = 1; else game.Player.State.ArenaTier = 1;
                    resetArmT = -1f;
                    ArenaAudio.Play(Sfx.Broken, t.position + Vector3.up, 0.8f, 1.2f);
                    game.Message((mode == ArenaGame.GameMode.Dungeon ? "Dungeon" : "Arena") + " voltou à profundidade 1", 1.8f);
                    game.RequestSave();
                    return true;
                }
                resetArmT = 3f; resetArmMode = mode;
                ArenaAudio.Play2D(Sfx.Click, 0.8f, 0.8f);
                game.Message("Grip de novo em 3 s para recomeçar da profundidade 1 (agora: " + TierOf(mode) + ")", 2.5f);
                return true;
            }
            return false;
        }

        void Portal(Vector3 pos, ArenaGame.GameMode mode, string title, string desc, Color c)
        {
            var t = new GameObject("Portal " + title).transform;
            t.SetParent(root, false);
            t.position = pos;
            var mat = ArenaUtil.Lit(c * 0.6f, c * 1.2f);
            ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0), new Vector3(1.8f, 0.03f, 1.8f), mat);
            // Arco: dois pilares e a viga.
            var stone = ArenaUtil.Lit(new Color(0.35f, 0.33f, 0.32f));
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(-1.05f, 1.3f, 0), new Vector3(0.25f, 2.6f, 0.25f), stone, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(1.05f, 1.3f, 0), new Vector3(0.25f, 2.6f, 0.25f), stone, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cube, t, new Vector3(0, 2.7f, 0), new Vector3(2.4f, 0.3f, 0.3f), stone);
            ArenaUtil.Prim(PrimitiveType.Quad, t, new Vector3(0, 1.3f, 0), new Vector3(1.8f, 2.5f, 1f), ArenaUtil.Unlit(new Color(c.r, c.g, c.b, 0.35f)));   // véu do portal (visto do sul)
            var label = ArenaFx.MakeText(t, title + "\n<size=40>" + desc + "</size>", 0.012f, Color.white);
            label.transform.localPosition = new Vector3(0, 3.3f, 0);
            label.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
            portalLabels[mode] = (label, title, desc);
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0, 1.5f, -0.8f);
            l.type = LightType.Point; l.color = c; l.range = 4f; l.intensity = 1.5f;
            portals.Add((t, mode));
        }

        void Update()
        {
            if (!root || ArenaGame.Blocked) return;
            portalCd -= Time.deltaTime;
            resetArmT -= Time.deltaTime;
            UpdatePortalLabels();
            var feet = game.Player.FeetPosition;
            if (portalCd <= 0f)
                foreach (var (t, mode) in portals)
                    if (t && (t.position - feet).Flat().magnitude < PortalRange)
                    {
                        ArenaAudio.Play2D(Sfx.Skill, 0.8f, 0.6f);
                        if (mode == ArenaGame.GameMode.Menu) { game.SaveNow(); game.StartTutorial(); }
                        else game.EnterFromSanctuary(mode);
                        return;
                    }
            UpdateAnvilText();
            UpdateForgeText();
            UpdateNpcTexts();
            UpdateBoardTexts();
        }

        // ---------- Quadro de desafios (meio do lado leste, longe das estantes): 3 pedestais, grip / E perto de um começa o desafio ----------
        static readonly (Challenge c, Vector3 pos, Color col, string desc)[] Boards =
        {
            (Challenge.Diario, new Vector3(1.6f, 0f, 2.2f), new Color(1f, 0.85f, 0.35f), "mesma dungeon para todos hoje"),
            (Challenge.Horda, new Vector3(3.1f, 0f, 2.2f), new Color(1f, 0.4f, 0.3f), "ondas sem fim, recorde de onda"),
            (Challenge.BossRush, new Vector3(4.6f, 0f, 2.2f), new Color(0.75f, 0.45f, 1f), "os 4 chefes seguidos"),
        };
        readonly System.Collections.Generic.List<TextMesh> boardTexts = new System.Collections.Generic.List<TextMesh>();

        void BuildChallengeBoard()
        {
            boardTexts.Clear();
            var stone = ArenaUtil.Lit(new Color(0.3f, 0.28f, 0.3f));
            foreach (var (c, pos, col, desc) in Boards)
            {
                var t = new GameObject("Desafio " + c).transform;
                t.SetParent(root, false);
                t.position = pos;
                ArenaUtil.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f), stone, keepCollider: true);
                ArenaUtil.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.2f, 0f), Vector3.one * 0.3f, ArenaUtil.Lit(col, col * 1.4f));
                var text = ArenaFx.MakeText(t, ChallengeRules.Name(c).ToUpper() + "\n<size=40>" + desc + "</size>", 0.008f, col);
                text.transform.localPosition = new Vector3(0f, 1.75f, 0f);
                boardTexts.Add(text);
            }
            UpdateBoardTexts();
        }

        void UpdateBoardTexts()
        {
            var cam = Camera.main;
            var o = SaveSystem.Options;
            for (int i = 0; i < boardTexts.Count && i < Boards.Length; i++)
            {
                var text = boardTexts[i];
                if (!text) continue;
                if (cam) text.transform.rotation = Quaternion.LookRotation(text.transform.position - cam.transform.position);
                var (c, _, _, desc) = Boards[i];
                string rec = c switch
                {
                    Challenge.Diario => o.dailyDate == ChallengeRules.DailySeed(System.DateTime.Now) && o.dailyBest > 0f ? "recorde de hoje " + ChallengeRules.Time(o.dailyBest) : "sem recorde hoje",
                    Challenge.Horda => "recorde: onda " + o.hordeBest,
                    _ => o.bossRushBest > 0f ? "recorde " + ChallengeRules.Time(o.bossRushBest) : "sem recorde",
                };
                text.text = ChallengeRules.Name(c).ToUpper() + "\n<size=40>" + desc + "\n" + rec + " · grip / E</size>";
            }
        }

        bool TryChallenge(ArenaPlayer player)
        {
            foreach (var (c, pos, _, _) in Boards)
            {
                if (!Near(player, pos, 1.3f, 1.5f)) continue;
                ArenaAudio.Play2D(Sfx.Skill, 0.8f, 0.6f);
                game.StartChallenge(c);
                return true;
            }
            return false;
        }

        // ---------- NPCs resgatados na dungeon (parede sul) ----------
        readonly System.Collections.Generic.List<(NpcId id, Transform t, TextMesh text)> npcs = new System.Collections.Generic.List<(NpcId, Transform, TextMesh)>();
        static Vector3 NpcSpot(NpcId n) => n switch
        {
            NpcId.Domadora => new Vector3(2.2f, 0f, -6.6f),
            NpcId.Capitao => new Vector3(-0.2f, 0f, -6.8f),
            _ => new Vector3(-2.6f, 0f, -6.6f),
        };

        void BuildNpcs()
        {
            npcs.Clear();
            var s = game.Player.State;
            foreach (NpcId n in System.Enum.GetValues(typeof(NpcId)))
            {
                if (!s.Rescued.Contains(n)) continue;
                var t = NpcFigure.Build(root, n);
                t.position = NpcSpot(n);
                t.rotation = Quaternion.LookRotation(Vector3.forward);
                var text = ArenaFx.MakeText(t, "", 0.008f, new Color(1f, 0.9f, 0.6f));
                text.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                npcs.Add((n, t, text));
            }
            UpdateNpcTexts();
            UpdateBoardTexts();
        }

        void UpdateNpcTexts()
        {
            var s = game.Player.State;
            var cam = Camera.main;
            foreach (var (n, t, text) in npcs)
            {
                if (!text) continue;
                if (cam) text.transform.rotation = Quaternion.LookRotation(text.transform.position - cam.transform.position);
                text.text = AllyRules.Name(n).ToUpper() + "\n<size=40>" + (n switch
                {
                    NpcId.Domadora => s.Pet == PetKind.Nenhum ? "Fada Faísca: " + AllyRules.PetPrice + " ouro\n(ataca com raio pequeno)"
                                      : "Seu pet: " + AllyRules.PetName(s.Pet) + "\ngrip / E troca de fada (grátis)",
                    NpcId.Capitao => s.MercenaryHired ? "Mercenário contratado:\nvai com você na próxima ida"
                                      : "Mercenário: " + AllyRules.MercenaryPrice + " ouro\n(luta com você na próxima ida)",
                    _ => s.ExtraPotionSlots >= AllyRules.MaxExtraPotions ? "Elixires: todos comprados"
                                      : "Elixir (+1 poção máx.): " + AllyRules.ElixirPrice(s.ExtraPotionSlots) + " ouro",
                }) + "\nvocê tem " + s.Gold + " ouro</size>";
            }
        }

        bool TryNpc(ArenaPlayer player)
        {
            var s = player.State;
            foreach (var (n, t, text) in npcs)
            {
                if (!t || !Near(player, t.position, 1.5f, 1.8f)) continue;
                bool ok = true; string msg;
                switch (n)
                {
                    case NpcId.Domadora:
                        if (s.Pet == PetKind.Nenhum)
                        {
                            ok = s.Gold >= AllyRules.PetPrice;
                            if (ok) { s.SpendGold(AllyRules.PetPrice); s.Pet = PetKind.FadaFaisca; }
                            msg = ok ? "A Fada Faísca vai com você!" : "Custa " + AllyRules.PetPrice + " de ouro";
                        }
                        else { s.Pet = s.Pet == PetKind.FadaFaisca ? PetKind.FadaCurandeira : PetKind.FadaFaisca; msg = "Agora: " + AllyRules.PetName(s.Pet); }
                        break;
                    case NpcId.Capitao:
                        if (s.MercenaryHired) { msg = "Já contratado para a próxima ida"; ok = false; break; }
                        ok = s.Gold >= AllyRules.MercenaryPrice;
                        if (ok) { s.SpendGold(AllyRules.MercenaryPrice); s.MercenaryHired = true; }
                        msg = ok ? "Mercenário contratado: entre num portal (arena ou dungeon)" : "Custa " + AllyRules.MercenaryPrice + " de ouro";
                        break;
                    default:
                        if (s.ExtraPotionSlots >= AllyRules.MaxExtraPotions) { msg = "Já comprou todos os elixires"; ok = false; break; }
                        int price = AllyRules.ElixirPrice(s.ExtraPotionSlots);
                        ok = s.Gold >= price;
                        if (ok) { s.SpendGold(price); s.ExtraPotionSlots++; s.AddPotion(); }
                        msg = ok ? "Elixir! Agora você carrega até " + s.PotionCap + " poções" : "Custa " + price + " de ouro";
                        break;
                }
                game.Message(msg, 1.8f);
                ArenaAudio.Play2D(ok ? Sfx.Level : Sfx.Deny, 0.7f, ok ? 1.2f : 1f);
                if (ok) { game.RequestSave(); UpdatePotionText(); }
                UpdateNpcTexts();
                return true;
            }
            return false;
        }

        // ---------- Forja de fusão (ao lado da bigorna) ----------
        void BuildForge(Material iron)
        {
            forge = new GameObject("Forja de fusao").transform;
            forge.SetParent(root, false);
            forge.position = new Vector3(-4f, 0f, -0.6f);
            var stone = ArenaUtil.Lit(new Color(0.35f, 0.3f, 0.32f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, forge, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.45f, 0.9f), stone, keepCollider: true);
            ArenaUtil.Prim(PrimitiveType.Cylinder, forge, new Vector3(0f, 0.92f, 0f), new Vector3(0.7f, 0.03f, 0.7f), ArenaUtil.Unlit(new Color(1f, 0.45f, 0.1f)));
            var l = new GameObject("Brasa").AddComponent<Light>();
            l.transform.SetParent(forge, false);
            l.transform.localPosition = Vector3.up * 1.3f;
            l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.15f); l.range = 3f; l.intensity = 1.5f;
            forgeText = ArenaFx.MakeText(forge, "", 0.007f, new Color(1f, 0.6f, 0.3f));
            forgeText.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        }

        void UpdateForgeText()
        {
            if (!forgeText) return;
            var cam = Camera.main;
            if (cam) forgeText.transform.rotation = Quaternion.LookRotation(forgeText.transform.position - cam.transform.position);
            bool near = (forge.position - game.Player.FeetPosition).Flat().magnitude < 4f;
            forgeText.gameObject.SetActive(near);
            if (!near) return;
            var s = game.Player.State;
            var keep = s.Loadout[s.ActiveSlot]; var food = s.Loadout[1 - s.ActiveSlot];
            if (keep == null || food == null) { forgeText.text = "FORJA DE FUSÃO\n<size=44>Junte a arma da mão com a 2ª arma\n(precisa das duas)</size>"; return; }
            var r = Fusion.ResultRarity(keep, food);
            forgeText.text = "FORJA DE FUSÃO\n<size=44>" + keep.DisplayName + " + " + food.DisplayName + "\n→ " + keep.Base.Name + " " + ItemInstance.RarityName(r)
                + "\n" + Fusion.Cost(keep, food) + " ouro (você tem " + s.Gold + ") · grip / E</size>";
        }

        bool TryFuse(ArenaPlayer player)
        {
            if (!forge || !Near(player, forge.position, 1.3f, 1.6f)) return false;
            var s = player.State;
            var keep = s.Loadout[s.ActiveSlot]; var food = s.Loadout[1 - s.ActiveSlot];
            if (keep == null || food == null) { game.Message("A forja junta a arma da mão com a 2ª arma", 1.4f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true; }
            if (!Fusion.TryFuse(s, fuseRng, out var result))
            {
                game.Message("Custa " + Fusion.Cost(keep, food) + " de ouro (você tem " + s.Gold + ")", 1.4f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true;
            }
            player.EquipActive();
            ArenaAudio.Play(Sfx.Explosion, forge.position + Vector3.up, 0.6f, 1.4f);
            ArenaFx.Burst(forge.position + Vector3.up, new Color(1f, 0.6f, 0.2f), 30, 4f);
            ArenaInput.Haptic(1f, 0.2f);
            game.Message("Fundiu: " + result.DisplayName + " (" + ItemInstance.RarityName(result.Rarity) + ")", 2f);
            game.RequestSave();
            return true;
        }

        void UpdateAnvilText()
        {
            var cam = Camera.main;
            if (cam) anvilText.transform.rotation = Quaternion.LookRotation(anvilText.transform.position - cam.transform.position);
            var item = game.Player.State.ActiveItem;
            bool near = (anvil.position - game.Player.FeetPosition).Flat().magnitude < 4f;
            anvilText.gameObject.SetActive(near);
            if (!near || item == null) return;
            anvilText.text = "FERREIRO\n<size=44>" + item.DisplayName + "\n" +
                (item.CanUpgrade ? "+" + (item.Upgrade + 1) + ": " + item.UpgradeCost + " ouro (você tem " + game.Player.State.Gold + ")\ngrip / E na bigorna" : "no máximo (+5)") + "</size>";
        }

        // ---------- Estantes de armas e mercador ----------
        // Parede leste = PERMANENTE (o que você guarda; não some). Parede oeste = TEMPORÁRIA (o que ficou para trás na
        // dungeon/arena; some quando você entra num portal sem tirar). Grip numa arma: troca pela sua (ou vai para a
        // 2ª vaga vazia). Grip num gancho vazio da permanente: guarda a sua 2ª arma. Mercador (baú com moedas, perto da
        // bigorna): grip vende a sua 2ª arma por ouro para o ferreiro.
        public const int RackSlots = 12;
        const float RackStep = 0.75f, RackZ0 = -4.6f;

        static float RackX(RackKind k) => k == RackKind.Permanente ? 7.25f : -7.25f;
        static Quaternion RackFacing(RackKind k) => Quaternion.LookRotation(k == RackKind.Permanente ? Vector3.left : Vector3.right);
        static Vector3 RackSlot(RackKind k, int i) => new Vector3(RackX(k), 0f, RackZ0 + i * RackStep);

        Transform merchant;

        void BuildRacks()
        {
            BuildRack(RackKind.Permanente, "ESTANTE PERMANENTE\n<size=40>grip numa arma: troca · grip num gancho vazio: guarda a sua 2ª arma</size>");
            BuildRack(RackKind.Temporaria, "ESTANTE TEMPORÁRIA\n<size=40>o que ficou para trás · SOME quando você entrar num portal</size>");
            var ps = game.Player.State;
            for (int i = 0; i < ps.Stash.Count && i < RackSlots; i++) Hang(ps.Stash[i], RackKind.Permanente, i);
            for (int i = 0; i < ps.LostItems.Count && i < RackSlots; i++) Hang(ps.LostItems[i], RackKind.Temporaria, i);
            if (ps.LostItems.Count > 0)
                game.Message(ps.LostItems.Count + " arma(s) na estante temporária (oeste): pegue ou venda antes de sair", 3f);
            BuildMerchant();
            BuildShop();
            BuildNpcs();
            BuildChallengeBoard();
        }

        // Banca: 3 armas e poções. O estoque renova a cada volta ao santuário.
        public Shop Shop { get; } = new Shop();
        Transform potionStand;
        TextMesh potionText;
        static readonly Vector3[] ShopSlots = { new Vector3(-5.4f, 0f, 2.9f), new Vector3(-4.6f, 0f, 2.9f), new Vector3(-3.8f, 0f, 2.9f) };
        static readonly Vector3 PotionStandPos = new Vector3(-3.0f, 0f, 2.9f);

        void BuildShop()
        {
            Shop.Restock(game.Loot, game.CurrentTier);
            var wood = ArenaUtil.Lit(new Color(0.4f, 0.27f, 0.14f));
            ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(-4.6f, 0.08f, 3.15f), new Vector3(2.4f, 0.16f, 0.4f), wood);
            for (int i = 0; i < Shop.Stock.Count && i < ShopSlots.Length; i++)
                WeaponPickup.Spawn(Shop.Stock[i], ShopSlots[i]).PlaceOnRack(ShopSlots[i], Quaternion.LookRotation(Vector3.back), RackKind.Loja)
                    .SetPriceTag(Merchant.ShopPrice(Shop.Stock[i]));
            var sign = ArenaFx.MakeText(root, "LOJA\n<size=40>renova toda vez que você volta</size>", 0.006f, new Color(1f, 0.85f, 0.4f));
            sign.transform.position = new Vector3(-4.6f, 2.6f, 3.2f);
            sign.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            potionStand = new GameObject("Pocoes").transform;
            potionStand.SetParent(root, false);
            potionStand.position = PotionStandPos;
            ArenaUtil.Prim(PrimitiveType.Cube, potionStand, new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.9f, 0.4f), wood, keepCollider: true);
            var red = ArenaUtil.Lit(new Color(0.9f, 0.15f, 0.2f), new Color(0.5f, 0.05f, 0.08f));
            for (int i = 0; i < 3; i++)
                ArenaUtil.Prim(PrimitiveType.Capsule, potionStand, new Vector3(-0.14f + i * 0.14f, 1.0f, 0), new Vector3(0.09f, 0.1f, 0.09f), red);
            potionText = ArenaFx.MakeText(potionStand, "", 0.006f, new Color(1f, 0.5f, 0.55f));
            potionText.transform.localPosition = new Vector3(0, 1.5f, 0);
            potionText.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            UpdatePotionText();
        }

        void UpdatePotionText()
        {
            if (potionText) potionText.text = "POÇÃO DE CURA  " + Merchant.PotionPrice + " ouro\n<size=40>cura " + Mathf.RoundToInt(PlayerState.PotionHeal * 100f)
                + "% · máx. " + game.Player.State.PotionCap + " · restam " + Shop.PotionsLeft + "\nbeber: mão esquerda na boca · B</size>";
        }

        bool TryBuyPotion(ArenaPlayer player)
        {
            if (!potionStand || !Near(player, potionStand.position, 0.9f, 1.4f)) return false;
            var s = player.State;
            if (!Shop.TryBuyPotion(s))
            {
                string why = Shop.PotionsLeft <= 0 ? "Acabaram as poções (volte depois)" : s.Potions >= s.PotionCap ? "Você já carrega " + s.PotionCap + " poções"
                    : "Custa " + Merchant.PotionPrice + " de ouro (você tem " + s.Gold + ")";
                game.Message(why, 1.4f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true;
            }
            ArenaAudio.Play(Sfx.Pickup, potionStand.position + Vector3.up, 1f, 1.1f);
            game.Message("Comprou uma poção (" + s.Potions + "/" + s.PotionCap + ")", 1.2f);
            UpdatePotionText();
            game.RequestSave();
            return true;
        }

        void Hang(ItemInstance item, RackKind k, int i) =>
            WeaponPickup.Spawn(item, RackSlot(k, i)).PlaceOnRack(RackSlot(k, i), RackFacing(k), k).Owned = true;

        void BuildRack(RackKind k, string title)
        {
            var wood = ArenaUtil.Lit(k == RackKind.Permanente ? new Color(0.32f, 0.2f, 0.11f) : new Color(0.25f, 0.22f, 0.2f));
            float len = RackStep * RackSlots, side = k == RackKind.Permanente ? 1f : -1f;
            var mid = new Vector3(RackX(k) + 0.25f * side, 0f, RackZ0 + RackStep * (RackSlots - 1) * 0.5f);
            ArenaUtil.Prim(PrimitiveType.Cube, root, mid + new Vector3(0, 0.08f, 0), new Vector3(0.5f, 0.16f, len), wood);
            ArenaUtil.Prim(PrimitiveType.Cube, root, mid + new Vector3(0.12f * side, 1.55f, 0), new Vector3(0.1f, 0.1f, len), wood);
            for (int i = 0; i <= RackSlots; i += 3)
                ArenaUtil.Prim(PrimitiveType.Cube, root, new Vector3(RackX(k) + 0.37f * side, 0.8f, RackZ0 - RackStep * 0.5f + i * RackStep), new Vector3(0.1f, 1.6f, 0.1f), wood);
            var sign = ArenaFx.MakeText(root, title, 0.006f, k == RackKind.Permanente ? new Color(0.95f, 0.9f, 0.8f) : new Color(1f, 0.7f, 0.5f));
            sign.transform.position = mid + new Vector3(-0.1f * side, 2.1f, 0);
            sign.transform.rotation = Quaternion.LookRotation(Vector3.right * side);
        }

        void BuildMerchant()
        {
            merchant = new GameObject("Mercador").transform;
            merchant.SetParent(root, false);
            merchant.position = new Vector3(-5.6f, 0f, 0.8f);
            var wood = ArenaUtil.Lit(new Color(0.36f, 0.24f, 0.12f));
            var gold = ArenaUtil.Lit(new Color(0.95f, 0.75f, 0.25f), new Color(0.4f, 0.3f, 0.05f));
            ArenaUtil.Prim(PrimitiveType.Cube, merchant, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.6f), wood, keepCollider: true);
            for (int i = 0; i < 7; i++)
                ArenaUtil.Prim(PrimitiveType.Cylinder, merchant, new Vector3(-0.3f + (i % 4) * 0.2f, 0.62f + (i / 4) * 0.03f, -0.1f + (i % 3) * 0.1f), new Vector3(0.12f, 0.012f, 0.12f), gold);
            var t = ArenaFx.MakeText(merchant, "MERCADOR\n<size=40>grip / E: vende a sua 2ª arma (paga 25% do preço da loja)</size>", 0.006f, new Color(1f, 0.85f, 0.4f));
            t.transform.localPosition = new Vector3(0, 1.4f, 0);
            t.transform.rotation = Quaternion.LookRotation(Vector3.left);
        }

        bool Near(ArenaPlayer player, Vector3 at, float vrRange, float desktopRange) =>
            player.IsVr ? Mathf.Min(Vector3.Distance(player.RightHand.position, at + Vector3.up * 0.6f), (player.FeetPosition - at).Flat().magnitude) < vrRange
                        : (player.FeetPosition - at).Flat().magnitude < desktopRange;

        // Grip no mercador: vende a 2ª arma.
        bool TrySell(ArenaPlayer player)
        {
            if (!merchant || !Near(player, merchant.position, 1.3f, 1.6f)) return false;
            var s = player.State;
            var item = s.Loadout[1 - s.ActiveSlot];
            int price = s.SellSecondWeapon();
            if (price <= 0) { game.Message("O mercador compra a sua 2ª arma (a que não está na mão)", 1.6f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true; }
            ArenaAudio.Play(Sfx.Pickup, merchant.position + Vector3.up, 1f, 1.3f);
            ArenaFx.FloatText("+" + price + " ouro", merchant.position + Vector3.up * 1f, new Color(1f, 0.85f, 0.3f), 1.1f);
            game.Message("Vendeu " + item.DisplayName + " por " + price + " de ouro", 1.6f);
            game.RequestSave();
            return true;
        }

        // Grip num gancho vazio da estante permanente: guarda a 2ª arma (a que não está na mão).
        bool TryStore(ArenaPlayer player)
        {
            const RackKind k = RackKind.Permanente;
            var reach = player.IsVr ? player.RightHand.position : player.FeetPosition + player.HeadForward * 0.6f;
            int best = -1; float bestD = player.IsVr ? 0.6f : 1.2f;
            for (int i = 0; i < RackSlots; i++)
            {
                var s = RackSlot(k, i);
                float d = player.IsVr ? Vector3.Distance(reach, s + Vector3.up * 0.8f) : (reach - s).Flat().magnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return false;
            var slot = RackSlot(k, best);
            foreach (var p in WeaponPickup.All)
                if (p && p.OnRack && (p.RackPos - slot).sqrMagnitude < 0.01f) return false;   // ocupado: quem pega é o WeaponPickup
            var s2 = player.State;
            int other = 1 - s2.ActiveSlot;
            var item = s2.Loadout[other];
            if (item == null) { game.Message("Nada para guardar: a estante guarda a sua 2ª arma", 1.4f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true; }
            if (!s2.AddToStash(item)) { game.Message("Estante permanente cheia (12)", 1.2f); ArenaAudio.Play2D(Sfx.Deny, 0.6f); return true; }
            s2.Loadout[other] = null;
            Hang(item, k, best);
            ArenaAudio.Play(Sfx.Pickup, slot + Vector3.up, 0.8f, 0.8f);
            game.Message("Guardou " + item.DisplayName, 1.2f);
            game.RequestSave();
            return true;
        }

        // Grip/E perto da bigorna: melhora a arma da mão.
        public bool TryInteract(ArenaPlayer player)
        {
            if (!root || !anvil) return false;
            if (TryResetStone(player) || TryStore(player) || TryBuyPotion(player) || TrySell(player) || TryFuse(player) || TryNpc(player) || TryChallenge(player)) return true;
            float d = player.IsVr
                ? Mathf.Min(Vector3.Distance(player.RightHand.position, anvil.position + Vector3.up * 0.75f), (player.FeetPosition - anvil.position).Flat().magnitude)
                : (player.FeetPosition - anvil.position).Flat().magnitude;
            if (d > AnvilRange) return false;
            var s = player.State;
            var item = s.ActiveItem;
            if (item == null || !item.CanUpgrade) { game.Message("Arma no máximo (+5)", 1f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); return true; }
            if (!Smithy.TryUpgrade(item, s)) { game.Message("Ouro insuficiente (" + item.UpgradeCost + ")", 1f); ArenaAudio.Play2D(Sfx.Deny, 0.7f); return true; }
            player.EquipActive();
            ArenaAudio.Play(Sfx.Parry, anvil.position + Vector3.up, 1f, 0.8f);
            ArenaFx.Burst(anvil.position + Vector3.up * 0.85f, new Color(1f, 0.7f, 0.3f), 20, 3f);
            ArenaInput.Haptic(0.8f, 0.1f);
            game.Message(item.DisplayName + "!", 1.2f);
            game.RequestSave();
            return true;
        }
    }
}
