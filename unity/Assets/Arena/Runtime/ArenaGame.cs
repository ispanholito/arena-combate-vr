using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Diretor da arena: ondas de inimigos, chefe, fogueira (checkpoint/cura), morte do jogador e mensagens.
    public sealed class ArenaGame : MonoBehaviour
    {
        public static ArenaGame Instance { get; private set; }

        public ArenaPlayer player;
        public Transform bonfire;
        public Transform[] spawnPoints;
        public Transform enemyRoot;
        public Transform bossSpawn;
        public bool spawnWaves = true;

        public ArenaPlayer Player => player;
        public WaveDirector Director { get; } = new WaveDirector();
        public LootTable Loot { get; } = new LootTable();
        // Fichas de ataque: quantos inimigos corpo a corpo atacam ao mesmo tempo (pela dificuldade; ver CombatAI).
        public static readonly AttackTokenPool Tokens = new AttackTokenPool();
        public GoblinKing Boss { get; private set; }
        public string CurrentMessage { get; private set; } = "";
        public float MessageTime { get; private set; }

        public const float BonfireRadius = 1.6f, BonfireHealPerSecond = 25f;

        // Física na mesma frequência da tela do headset (Quest 2: 72 Hz). Com 90 Hz de física e 72 de tela, a arma
        // avançava ora 1 ora 2 passos por quadro e tremia.
        void SyncPhysicsRate()
        {
            if (!UnityEngine.XR.XRSettings.isDeviceActive || OVRManager.display == null) return;
            float hz = OVRManager.display.displayFrequency;
            if (hz >= 60f && Mathf.Abs(Time.fixedDeltaTime - 1f / hz) > 1e-5f) Time.fixedDeltaTime = 1f / hz;
        }

        // ---------- Pausa ----------
        public static bool Paused => Instance && Instance.paused;
        bool paused;

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            if (value) player.TreeOpen = false;
        }

        void PauseFromSystem() => SetPaused(true);   // tirou o headset ou abriu o menu do sistema

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 1f / 90f;   // no VR passa a seguir a frequência da tela (ver Update)
            OVRManager.HMDUnmounted += PauseFromSystem;
            OVRManager.InputFocusLost += PauseFromSystem;
            arenaBonfire = bonfire;
            if (!arenaWorld) { var w = GameObject.Find("Arena"); if (w) arenaWorld = w.transform; }
            dungeon = gameObject.AddComponent<DungeonMode>();
            gameObject.AddComponent<ArenaMusic>();
            sanctuary = gameObject.AddComponent<SanctuaryMode>();
        }

        void OnDestroy()
        {
            OVRManager.HMDUnmounted -= PauseFromSystem;
            OVRManager.InputFocusLost -= PauseFromSystem;
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        // ---------- Modos: menu inicial, Arena (ondas + chefe), Dungeon (2 andares + chefe) e Treino (boneco) ----------
        public enum GameMode { Menu, Santuario, Arena, Dungeon, Treino }
        public GameMode Mode { get; private set; } = GameMode.Menu;
        public static bool InMenu => Instance && Instance.Mode == GameMode.Menu;
        // Bloqueia habilidades, poderes e árvore (pausado ou no menu inicial).
        public static bool Blocked => Paused || InMenu;

        public Transform arenaWorld;          // geometria da arena (escondida na dungeon)
        Transform arenaBonfire;
        DungeonMode dungeon;
        SanctuaryMode sanctuary;
        public DungeonMode Dungeon => dungeon;
        public SanctuaryMode Sanctuary => sanctuary;
        // Santuário, Arena e Dungeon salvam o progresso; o Treino (nível 10 de mentira) não.
        bool Persistent => Mode == GameMode.Santuario || Mode == GameMode.Arena || Mode == GameMode.Dungeon;

        // Volta ao santuário (vitória na dungeon, pausa). Saindo do Treino, recarrega o personagem salvo.
        public void GoToSanctuary()
        {
            if (Persistent) SaveNow();
            var character = Mode == GameMode.Treino || player.State == null
                ? SaveSystem.Load()?.ToPlayer() ?? new PlayerState(player.State?.Class.Id ?? ClassId.Lamina)
                : player.State;
            SetPaused(false);
            StartMode(GameMode.Santuario, character);
        }

        // Portal do santuário: leva o personagem atual para o modo.
        public void EnterFromSanctuary(GameMode mode)
        {
            SaveNow();
            StartMode(mode, player.State);
            // O que ficou na estante temporária e não foi pego some ao entrar num portal.
            int gone = player.State.DiscardLostItems();
            if (gone > 0) { Message(gone + " arma(s) da estante temporária sumiram", 2.5f); RequestSave(); }
        }

        // Pausa → B: do santuário vai ao menu inicial (trocar de personagem); de qualquer outro lugar, ao santuário.
        public void BackFromPause()
        {
            if (Mode == GameMode.Santuario) ReturnToMenu();
            else GoToSanctuary();
        }

        void Start()
        {
            SpawnArenaGrapples();
            ReturnToMenu();
        }

        // 1º teste do visual (2026-10-28): no Treino a arena ganha a arte do KayKit. As formas simples continuam (colisão,
        // ganchos da teia, luzes); só o desenho delas some enquanto a sala KayKit aparece.
        Transform trainingHall;
        readonly List<Renderer> arenaPrims = new List<Renderer>();

        void SetTrainingHall(bool on)
        {
            if (!arenaWorld) return;
            if (on && !trainingHall)
            {
                var pillars = new List<Transform>(); var torches = new List<Vector3>();
                arenaPrims.Clear();
                foreach (Transform t in arenaWorld)
                {
                    if (t.name == "Pilar") pillars.Add(t);
                    if (t.name == "Tocha") torches.Add(t.position);
                    if (t.name == "Chao" || t.name.StartsWith("Parede") || t.name == "Pilar" || t.name == "Tocha")
                        arenaPrims.AddRange(t.GetComponentsInChildren<Renderer>());
                }
                trainingHall = KayKitArt.BuildTrainingHall(arenaWorld, 13f, pillars, torches);
                if (!trainingHall) return;   // arte não encontrada: fica a arena simples
            }
            if (trainingHall) trainingHall.gameObject.SetActive(on);
            foreach (var r in arenaPrims) if (r) r.enabled = !on || !trainingHall;
        }

        // Pontos de gancho da teia na arena (filhos da geometria: somem junto na dungeon): topo das pilastras e alto das paredes.
        void SpawnArenaGrapples()
        {
            if (!arenaWorld) return;
            foreach (Transform t in arenaWorld)
                if (t.name == "Pilar") GrapplePoint.Spawn(arenaWorld, t.position + Vector3.up * (t.localScale.y + 0.2f));
            float h = Combat.ArenaHalfSize;
            foreach (float s in new[] { -6f, 6f })
            {
                GrapplePoint.Spawn(arenaWorld, new Vector3(s, 4.6f, -h));
                GrapplePoint.Spawn(arenaWorld, new Vector3(s, 4.6f, h));
                GrapplePoint.Spawn(arenaWorld, new Vector3(-h, 4.6f, s));
                GrapplePoint.Spawn(arenaWorld, new Vector3(h, 4.6f, s));
            }
        }

        public void ReturnToMenu()
        {
            SetPaused(false);
            ClearEnemies();   // antes de salvar: armas esquecidas entram no salvo
            if (Persistent) SaveNow();
            LeaveDungeon();
            Mode = GameMode.Menu;
            SetTrainingHall(false);
            spawnWaves = false;
            player.TreeOpen = false;
            player.TeleportTo(BonfireStandPoint, Vector3.zero);
        }

        // character: personagem do salvo ou novo. Arena/Dungeon gravam nele; Treino usa um personagem de nível 10 à parte.
        public void StartMode(GameMode mode, PlayerState character)
        {
            ClearEnemies();
            LeaveDungeon();
            Mode = mode;
            SetTrainingHall(mode == GameMode.Treino);
            if (mode == GameMode.Treino)
            {
                player.ResetCharacter(character.Class.Id);
                spawnWaves = false;
                player.TeleportTo(BonfireStandPoint, Vector3.zero);
                // Treino: nível máximo do MVP com os pontos livres, sem tomar dano, estamina e mana infinitas.
                var p = player.State.Progression;
                p.GainXp(1000000);
                Spawn(EnemyDef.Boneco, new Vector3(0f, 0f, 0f), false);
                // Teste do passo A1 do plano: um goblin com corpo físico (ragdoll ativo) ao lado do boneco.
                ragdollSpawner = new GameObject("GoblinFisicoSpawner").AddComponent<RagdollGoblinSpawner>();
                ragdollSpawner.Point = new Vector3(2.5f, 0f, 1.5f);
                ragdollSpawner.LookAt = BonfireStandPoint;
                Message("Treino: nível " + p.Level + ", " + p.UnspentPoints + " pontos para gastar na árvore (A+B)", 4f);
                return;
            }
            player.UseCharacter(character);
            character.Penalty = SaveSystem.Options.DeathPenalty;
            SaveNow();
            if (mode == GameMode.Santuario)
            {
                spawnWaves = false;
                if (arenaWorld) arenaWorld.gameObject.SetActive(false);
                Message("Santuário: escolha um portal. Fogueira cura e abre a árvore.", 3f);
                sanctuary.Begin();   // depois da mensagem geral (ela avisa das armas esquecidas)
                return;
            }
            if (mode == GameMode.Arena)
            {
                spawnWaves = true;
                Director.Reset(2f);
                player.TeleportTo(BonfireStandPoint, Vector3.zero);
                Message($"{player.State.Class.Name}: sobreviva às ondas. Fogueira = cura.", 3f);
            }
            else
            {
                spawnWaves = false;
                if (arenaWorld) arenaWorld.gameObject.SetActive(false);
                var saved = SaveSystem.Load();
                dungeon.Begin(Random.Range(1, 1000000), player.State.DungeonTier);
            }
        }

        void LeaveDungeon()
        {
            if (dungeon && dungeon.Active) dungeon.End();
            if (sanctuary && sanctuary.Active) sanctuary.End();
            ArenaUtil.Area = null;
            bonfire = arenaBonfire;
            if (arenaWorld) arenaWorld.gameObject.SetActive(true);
        }

        void ClearEnemies()
        {
            WeaponPickup.ClearAll(Mode == GameMode.Treino || Mode == GameMode.Menu ? null : player.State);
            if (ragdollSpawner) Destroy(ragdollSpawner.gameObject);
            RagdollGoblin.ClearAll();
            Tokens.Clear();
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            ArenaEnemy.All.Clear();
            Boss = null;
        }

        // Profundidade do modo arcade no modo atual (1 fora da dungeon/arena).
        public int CurrentTier => player == null || player.State == null ? 1
            : Mode == GameMode.Dungeon ? player.State.DungeonTier : Mode == GameMode.Arena ? player.State.ArenaTier : 1;

        public void SetBoss(GoblinKing king) => Boss = king;

        // Na arena fica de frente para o centro; na dungeon, na frente da fogueira (para onde ela "olha").
        public Vector3 BonfireStandPoint => bonfire == arenaBonfire
            ? bonfire.position + (Vector3.zero - bonfire.position).Flat().normalized * 1.5f
            : bonfire.position + bonfire.forward * 1.5f;

        // ---------- Salvar ----------
        bool saveRequested;
        float saveCd;

        public void RequestSave() { if (Persistent) saveRequested = true; }

        public void SaveNow()
        {
            if (!Persistent) return;
            SaveSystem.Save(player.State);
            saveRequested = false;
            saveCd = 2f;
        }

        public void SaveDeepestFloor(int floor)
        {
            if (!Persistent) return;
            var old = SaveSystem.Load();
            SaveSystem.Save(player.State, Mathf.Max(floor, old?.deepestFloor ?? 0));
        }

        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() => SaveNow();

        void Update()
        {
            float dt = Time.deltaTime;
            MessageTime -= dt;
            Tokens.Capacity = CombatAI.AttackTokens(SaveSystem.Options.Difficulty);
            SyncPhysicsRate();
            if (Mode == GameMode.Treino)
            {
                player.State.SetInvulnerable(1f);
                player.State.Stamina.Fill();   // infinitas no Treino (pedido do Pablo, 2026-10-28)
                player.State.Mana.Fill();
            }
            if (Mode == GameMode.Arena && spawnWaves && !player.TreeOpen)
            {
                int alive = 0, goblins = 0;
                foreach (var e in ArenaEnemy.All) { if (e == null || e.Dead) continue; alive++; if (e is GoblinEnemy && !(e is GoblinKing)) goblins++; }
                var def = Director.Tick(dt, alive, goblins, player.State.Progression.Level, player.State.Kills);
                if (def != null) SpawnFromDirector(def);
            }
            // Fogueira: perto dela cura
            if (Vector3.Distance(player.FeetPosition.Flat(), bonfire.position.Flat()) < BonfireRadius)
                player.State.Hp.Restore(BonfireHealPerSecond * dt);
            // Salvamento pedido pelos eventos (no máximo a cada 2 s) e de garantia a cada 20 s.
            saveCd -= Time.unscaledDeltaTime;
            autoSaveT += Time.unscaledDeltaTime;
            if (Persistent && ((saveRequested && saveCd <= 0f) || autoSaveT > 20f)) { autoSaveT = 0f; SaveNow(); }
        }

        float autoSaveT;

        public bool NearBonfire => Vector3.Distance(player.FeetPosition.Flat(), bonfire.position.Flat()) < BonfireRadius + 0.6f;

        void SpawnFromDirector(EnemyDef def)
        {
            if (def.Kind == EnemyKind.ReiGoblin)
            {
                var pos = bossSpawn ? bossSpawn.position : new Vector3(0, 0, -7);
                Boss = Spawn(def, pos, true) as GoblinKing;
                ArenaFx.Burst(pos + Vector3.up, new Color(1f, 0.19f, 0.13f), 40, 5f, 2f);
                Message("O Rei Goblin chegou!", 2.2f);
                return;
            }
            // Nasce no ponto mais longe do jogador (com um pouco de sorte)
            Transform best = spawnPoints[0]; float bestD = -1f;
            foreach (var s in spawnPoints)
            {
                float d = Vector3.Distance(s.position, player.FeetPosition) + Random.value * 3f;
                if (d > bestD) { bestD = d; best = s; }
            }
            Spawn(def, best.position, true);
            ArenaFx.Burst(best.position + Vector3.up * 0.5f, new Color(0.6f, 0.42f, 1f), 14, 2.5f);
        }

        // forcePhysical: corpo físico mesmo com a opção beta desligada (goblins do Treino).
        public ArenaEnemy Spawn(EnemyDef def, Vector3 pos, bool fromDirector, bool forcePhysical = false)
        {
            var diff = SaveSystem.Options.Difficulty;
            if (def.Kind != EnemyKind.Boneco) def = def.Scaled(DifficultyRules.EnemyHp(diff) * ArcadeRules.HpMultiplier(CurrentTier), DifficultyRules.WindupMultiplier(diff));
            var go = new GameObject(def.Name);
            go.transform.SetParent(enemyRoot, false);
            go.transform.position = ArenaUtil.ClampToArena(new Vector3(pos.x, 0, pos.z));
            // Opção beta: goblin, esqueleto e orc com corpo físico (ragdoll ativo).
            ArenaEnemy e = (forcePhysical || SaveSystem.Options.physicalGoblins) && RagdollSpec.HasPhysicalBody(def.Kind) ? go.AddComponent<PhysicalGoblinEnemy>() : def.Kind switch
            {
                EnemyKind.Slime => go.AddComponent<SlimeEnemy>(),
                EnemyKind.Goblin => go.AddComponent<GoblinEnemy>(),
                EnemyKind.Boneco => go.AddComponent<TrainingDummy>(),
                EnemyKind.Esqueleto => go.AddComponent<SkeletonEnemy>(),
                EnemyKind.Mimico => go.AddComponent<MimicEnemy>(),
                EnemyKind.Arqueiro => go.AddComponent<ArcherEnemy>(),
                EnemyKind.Orc => go.AddComponent<OrcEnemy>(),
                _ => go.AddComponent<GoblinKing>(),
            };
            e.Init(def);
            var look = (player.FeetPosition - go.transform.position).Flat();
            if (look.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(look);
            return e;
        }

        public void OnEnemyKilled(ArenaEnemy e)
        {
            int levelBefore = player.State.Progression.Level;
            player.State.AddKill(e.Def.Xp);
            player.State.OnKillPassive();
            DropLoot(e);
            if (e is GoblinKing && Mode == GameMode.Dungeon)
            {
                Boss = null;
                dungeon.OnBossKilled(e.Center);
                player.State.DungeonTier = ArcadeRules.Clamp(player.State.DungeonTier + 1);
                Message("Rei Goblin derrotado! Profundidade " + player.State.DungeonTier + " liberada (mais difícil, itens mais raros)", 3.5f);
                var saved = SaveSystem.Load();
                SaveSystem.Save(player.State, -1, (saved?.bossesDefeated ?? 0) + 1);
            }
            else if (e is GoblinKing)
            {
                Director.OnBossDefeated();
                player.State.ResetKills();
                Boss = null;
                player.State.ArenaTier = ArcadeRules.Clamp(player.State.ArenaTier + 1);
                Message("Rei Goblin derrotado! Arena na profundidade " + player.State.ArenaTier, 2.5f);
            }
            if (player.State.Progression.Level > levelBefore)
                Message($"Nível {player.State.Progression.Level}! +1 ponto de habilidade (abra a árvore)", 2f);
            RequestSave();
        }

        // Ouro vai direto para o jogador; às vezes cai uma arma (o chefe sempre deixa uma rara ou melhor).
        void DropLoot(ArenaEnemy e)
        {
            if (Mode == GameMode.Treino) return;
            int gold = Loot.RollGold(e.Def.Kind, CurrentTier);
            if (gold > 0)
            {
                player.State.AddGold(gold);
                ArenaFx.FloatText("+" + gold + " ouro", e.Center + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.3f), 0.9f);
            }
            var item = Loot.RollDrop(e.Def.Kind, CurrentTier);
            if (item == null) return;
            var dir = Random.insideUnitCircle.normalized;
            WeaponPickup.Spawn(item, e.Center, new Vector3(dir.x * 1.5f, 3.5f, dir.y * 1.5f));
            ArenaAudio.Play(Sfx.Pickup, e.Center, 0.8f, 0.7f);
            if (item.Rarity >= Rarity.Raro) Message(ItemInstance.RarityName(item.Rarity) + ": " + item.DisplayName + " caiu!", 1.8f);
        }

        public void OnBossPhase2()
        {
            Director.BossPhase = 2;
            Message("O Rei Goblin entrou em fúria!", 1.8f);
        }

        public void OnPlayerDied(int lostXp)
        {
            var st = player.State;
            string lossText = (lostXp > 0 ? $"perdeu {lostXp} XP" : "não perdeu XP") + (st.LastGoldLost > 0 ? $" e {st.LastGoldLost} de ouro" : "");
            // Uma vida: o personagem acaba. Apaga o salvo e volta ao menu inicial (sem salvar de novo).
            if (DeathRules.Permadeath(st.Penalty) && Persistent && Mode != GameMode.Santuario)
            {
                ClearEnemies();
                LeaveDungeon();
                Mode = GameMode.Menu;
                SaveSystem.Delete();
                ReturnToMenu();
                Message($"FIM DA JORNADA: {st.Class.Name} nível {st.Progression.Level} morreu (uma vida). Crie um novo personagem.", 6f);
                return;
            }
            // Modo arcade: morrer volta à profundidade 1. Na dungeon a descida acaba e você volta ao santuário.
            if (Mode == GameMode.Dungeon)
            {
                player.State.DungeonTier = 1;
                if (!player.Weapon.Held) player.Weapon.Recall();
                GoToSanctuary();
                Message($"Você morreu: {lossText} e a dungeon recomeça da profundidade 1", 3.5f);
                return;
            }
            if (Mode == GameMode.Arena) player.State.ArenaTier = 1;
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            ArenaEnemy.All.Clear();
            RagdollGoblin.ClearAll();
            Boss = null;
            Director.Reset(3f);
            if (Mode == GameMode.Dungeon) dungeon.OnPlayerDied();
            if (!player.Weapon.Held) player.Weapon.Recall();
            player.TeleportTo(BonfireStandPoint, Vector3.zero);
            Message($"Você morreu: {lossText}", 2.6f);
            RequestSave();
        }

        RagdollGoblinSpawner ragdollSpawner;

        public void Message(string text, float seconds)
        {
            CurrentMessage = text;
            MessageTime = seconds;
        }
    }
}
