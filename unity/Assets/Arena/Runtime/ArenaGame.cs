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
        public ArenaEnemy Boss { get; private set; }   // chefe vivo (qualquer um dos 4; ver IBoss)
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
            if (value) { player.TreeOpen = false; TutorialEvents.Raise("pausa"); }
        }

        void PauseFromSystem() => SetPaused(true);

        // Câmera lenta curta (morte do chefe). Conta em tempo real; a pausa tem prioridade.
        float slowLeft, slowScale = 1f;
        public void SlowMotion(float scale, float realSeconds) { slowScale = scale; slowLeft = realSeconds; }
        void UpdateSlowMotion()
        {
            if (paused || slowLeft <= 0f) return;
            slowLeft -= Time.unscaledDeltaTime;
            Time.timeScale = slowLeft > 0f ? slowScale : 1f;
        }   // tirou o headset ou abriu o menu do sistema

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

        // ---------- Tutorial dos gestos (TutorialMode) ----------
        // Roda dentro do Treino (sem dano, mana infinita, sala KayKit) só com o boneco. Sair/terminar volta ao santuário.
        public bool InTutorial => TutorialMode.Instance;

        public void StartTutorial(PlayerState character = null)
        {
            StartMode(GameMode.Treino, character ?? player.State);
            if (ragdollSpawner) Destroy(ragdollSpawner.gameObject);
            new GameObject("Tutorial").AddComponent<TutorialMode>();
        }

        // Sala de Criação: vitrine para avaliar arte e animações (roda dentro do Treino, como o tutorial).
        public void StartCreationRoom(PlayerState character = null)
        {
            StartMode(GameMode.Treino, character ?? player.State);
            if (ragdollSpawner) Destroy(ragdollSpawner.gameObject);
            new GameObject("SalaDeCriacaoModo").AddComponent<CreationRoom>();
        }

        public void FinishTutorial()
        {
            SaveSystem.Options.tutorialSeen = true;
            SaveSystem.SaveOptions();
            SetPaused(false);
            BackFromPause();   // do Treino volta ao santuário com o personagem salvo
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
            if (TutorialMode.Instance) Destroy(TutorialMode.Instance.gameObject);
            if (CreationRoom.Instance) Destroy(CreationRoom.Instance.gameObject);
            SetTrainingHall(true);   // fundo do menu inicial com a arte
            spawnWaves = false;
            player.TreeOpen = false;
            player.TeleportTo(BonfireStandPoint, Vector3.zero);
        }

        // character: personagem do salvo ou novo. Arena/Dungeon gravam nele; Treino usa um personagem de nível 10 à parte.
        // ---------- Desafios (diário, horda infinita, boss rush) ----------
        public Challenge Challenge { get; private set; }
        float challengeT, rushRestT = -1f;
        int rushIndex, hordeWave;
        System.Collections.Generic.List<EnemyKind> rushOrder;
        int? nextDungeonSeed;

        public void StartChallenge(Challenge c)
        {
            SaveNow();
            if (c == Challenge.Diario)
            {
                int seed = ChallengeRules.DailySeed(System.DateTime.Now);
                nextDungeonSeed = seed;
                StartMode(GameMode.Dungeon, player.State);
                player.State.ChooseBlessing(ChallengeRules.DailyBlessing(seed));
                var b = RunBlessings.Info(ChallengeRules.DailyBlessing(seed));
                Message("DESAFIO DIÁRIO: mesma dungeon para todos hoje. Bênção do dia: " + b.Name, 4f);
            }
            else
            {
                StartMode(GameMode.Arena, player.State);
                if (c == Challenge.Horda) { hordeWave = 1; Message("HORDA INFINITA: a cada " + ChallengeRules.KillsPerWave + " abates, uma onda mais forte. Sem chefe.", 4f); }
                else
                {
                    spawnWaves = false;
                    rushOrder = ChallengeRules.BossRushOrder(new System.Random());
                    rushIndex = 0;
                    rushRestT = ChallengeRules.BossRushFirstDelay;
                    Message("BOSS RUSH: os 4 chefes seguidos. Descanso curto entre eles.", 4f);
                }
            }
            Challenge = c;
            challengeT = 0f;
            Director.NoBoss = c == Challenge.Horda;
            Director.ExtraTarget = 0;
        }

        // Linha do HUD com o andamento do desafio.
        public string ChallengeStatus
        {
            get
            {
                var o = SaveSystem.Options;
                switch (Challenge)
                {
                    case Challenge.Diario:
                        return "DIÁRIO " + ChallengeRules.Time(challengeT) + (o.dailyDate == ChallengeRules.DailySeed(System.DateTime.Now) && o.dailyBest > 0f ? "  (recorde " + ChallengeRules.Time(o.dailyBest) + ")" : "");
                    case Challenge.Horda:
                        return "HORDA onda " + hordeWave + "  (recorde " + o.hordeBest + ")";
                    case Challenge.BossRush:
                        return "BOSS RUSH " + Mathf.Min(rushIndex + 1, 4) + "/4  " + ChallengeRules.Time(challengeT) + (o.bossRushBest > 0f ? "  (recorde " + ChallengeRules.Time(o.bossRushBest) + ")" : "");
                    default: return null;
                }
            }
        }

        void TickChallenge(float dt)
        {
            if (Challenge == Challenge.Nenhum) return;
            challengeT += dt;
            if (Challenge == Challenge.Horda)
            {
                int w = ChallengeRules.HordeWave(player.State.Kills);
                if (w != hordeWave)
                {
                    hordeWave = w;
                    Director.ExtraTarget = ChallengeRules.HordeExtraEnemies(w);
                    Message("ONDA " + w + "!", 2f);
                    ArenaAudio.Play2D(Sfx.Roar, 0.6f, 1.2f);
                }
            }
            if (Challenge == Challenge.BossRush && rushRestT >= 0f)
            {
                rushRestT -= dt;
                if (rushRestT < 0f && rushIndex < rushOrder.Count)
                    SpawnFromDirector(EnemyDef.Boss(rushOrder[rushIndex]));
            }
        }

        // Chefe morto no boss rush: descanso (cura metade + altar) e o próximo; o último = vitória.
        void OnRushBossKilled()
        {
            rushIndex++;
            var st = player.State;
            if (rushIndex >= rushOrder.Count)
            {
                var o = SaveSystem.Options;
                bool record = ChallengeRules.BetterTime(challengeT, o.bossRushBest);
                if (record) { o.bossRushBest = challengeT; SaveSystem.SaveOptions(); }
                st.AddGold(ChallengeRules.BossRushGold);
                var item = Loot.RollItem(Rarity.Raro, CurrentTier);
                WeaponPickup.Spawn(item, player.FeetPosition + player.HeadForward.Flat().normalized * 1.5f + Vector3.up, Vector3.up * 2f);
                Message("BOSS RUSH VENCIDO em " + ChallengeRules.Time(challengeT) + (record ? " — NOVO RECORDE!" : "") + "  +" + ChallengeRules.BossRushGold + " ouro", 6f);
                ArenaAudio.Play2D(Sfx.Level, 1f, 1f);
                Challenge = Challenge.Nenhum;
                RequestSave();
                return;
            }
            st.Hp.Restore(st.Hp.Max * ChallengeRules.BossRushHeal);
            rushRestT = ChallengeRules.BossRushRest;
            OfferBlessings(Vector3.zero, null);
            Message("Descanse: próximo chefe em " + Mathf.RoundToInt(ChallengeRules.BossRushRest) + " s (" + (rushIndex + 1) + "/4)", 3f);
        }

        // Morreu num desafio: guarda o recorde e volta ao santuário.
        bool EndChallengeOnDeath()
        {
            if (Challenge == Challenge.Nenhum) return false;
            var o = SaveSystem.Options;
            string msg = Challenge switch
            {
                Challenge.Horda => "Horda: você chegou à onda " + hordeWave + (hordeWave > o.hordeBest ? " — NOVO RECORDE!" : " (recorde " + o.hordeBest + ")"),
                Challenge.BossRush => "Boss rush: caiu no chefe " + (rushIndex + 1) + "/4",
                _ => "Desafio diário: você morreu. Tente de novo (a dungeon de hoje é a mesma)",
            };
            if (Challenge == Challenge.Horda && hordeWave > o.hordeBest) { o.hordeBest = hordeWave; SaveSystem.SaveOptions(); }
            Challenge = Challenge.Nenhum;
            if (!player.Weapon.Held) player.Weapon.Recall();
            GoToSanctuary();
            Message(msg, 5f);
            return true;
        }

        public void StartMode(GameMode mode, PlayerState character)
        {
            Challenge = Challenge.Nenhum;
            Director.NoBoss = false; Director.ExtraTarget = 0; rushRestT = -1f;
            // Trocar de modo encerra a partida: as bênçãos acabam.
            BlessingAltar.ClearAll();
            BlessingStatue.ClearAll();
            Mercenary.Clear();
            player.State?.ClearBlessings();
            character?.ClearBlessings();
            ClearEnemies();
            LeaveDungeon();
            if (TutorialMode.Instance) Destroy(TutorialMode.Instance.gameObject);
            if (CreationRoom.Instance) Destroy(CreationRoom.Instance.gameObject);
            Mode = mode;
            SetTrainingHall(mode == GameMode.Treino || mode == GameMode.Arena);
            NavObstacles.SetFromArena(arenaWorld, mode == GameMode.Arena || mode == GameMode.Treino);
            if (mode == GameMode.Arena || mode == GameMode.Treino) SpawnArenaBreakables();
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
            // Mercenário contratado com o Capitão: vai junto nesta ida (arena ou dungeon) e o contrato acaba.
            if ((mode == GameMode.Arena || mode == GameMode.Dungeon) && character.MercenaryHired) { character.MercenaryHired = false; Mercenary.Spawn(player); }
            SaveNow();
            if (mode == GameMode.Santuario)
            {
                spawnWaves = false;
                if (arenaWorld) arenaWorld.gameObject.SetActive(false);
                Message("Santuário: escolha um portal. Fogueira cura e abre a árvore.", 3f);
                sanctuary.Begin();   // depois da mensagem geral (ela avisa das armas esquecidas)
                if (!SaveSystem.Options.tutorialSeen && player.IsVr) StartTutorial();   // 1ª vez: ensina os gestos
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
                dungeon.Begin(nextDungeonSeed ?? Random.Range(1, 1000000), player.State.DungeonTier);
                nextDungeonSeed = null;
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
            Breakable.ClearAll();
            Tokens.Clear();
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            ArenaEnemy.All.Clear();
            Boss = null;
        }

        // Profundidade do modo arcade no modo atual (1 fora da dungeon/arena).
        public int CurrentTier => player == null || player.State == null ? 1
            : Mode == GameMode.Dungeon ? player.State.DungeonTier : Mode == GameMode.Arena ? player.State.ArenaTier : 1;

        public void SetBoss(ArenaEnemy boss) => Boss = boss;

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
            UpdateSlowMotion();
            float dt = Time.deltaTime;
            MessageTime -= dt;
            Tokens.Capacity = CombatAI.AttackTokens(SaveSystem.Options.Difficulty);
            RunBlessings.Current = player.State?.Blessings;   // os inimigos leem daqui o dano extra das bênçãos
            if (!Paused) TickChallenge(dt);
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
                foreach (var e in ArenaEnemy.All) { if (e == null || e.Dead) continue; alive++; if (e is GoblinEnemy && !(e is IBoss)) goblins++; }
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
            if (EnemyDef.IsBoss(def.Kind))
            {
                var pos = bossSpawn ? bossSpawn.position : new Vector3(0, 0, -7);
                Boss = Spawn(def, pos, true);
                ArenaFx.Burst(pos + Vector3.up, new Color(1f, 0.19f, 0.13f), 40, 5f, 2f);
                Message(def.Name + " chegou!", 2.2f);
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
        static readonly System.Random eliteRng = new System.Random();

        public ArenaEnemy Spawn(EnemyDef def, Vector3 pos, bool fromDirector, bool forcePhysical = false)
        {
            var diff = SaveSystem.Options.Difficulty;
            // Elite: só na Arena e na Dungeon (Treino e chefe não).
            var elite = Mode == GameMode.Dungeon ? EliteRules.Roll(def.Kind, EliteRules.Chance(true, dungeon.Run?.Floor.Number ?? 1), eliteRng)
                      : Mode == GameMode.Arena ? EliteRules.Roll(def.Kind, Challenge == Challenge.Horda ? ChallengeRules.HordeEliteChance(hordeWave) : EliteRules.Chance(false, player.State.Kills), eliteRng)
                      : EliteKind.Nenhum;
            if (def.Kind != EnemyKind.Boneco) def = def.Scaled(DifficultyRules.EnemyHp(diff) * ArcadeRules.HpMultiplier(CurrentTier) * EliteRules.HpMultiplier(elite) * (Challenge == Challenge.Horda ? ChallengeRules.HordeHpMultiplier(hordeWave) : 1f),
                                                               DifficultyRules.WindupMultiplier(diff) * EliteRules.WindupMultiplier(elite));
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
                EnemyKind.Xama => go.AddComponent<ShamanEnemy>(),
                EnemyKind.Orc => go.AddComponent<OrcEnemy>(),
                EnemyKind.RainhaSlime => go.AddComponent<SlimeQueen>(),
                EnemyKind.ChefeOrc => go.AddComponent<OrcWarlord>(),
                EnemyKind.Lich => go.AddComponent<LichBoss>(),
                _ => go.AddComponent<GoblinKing>(),
            };
            e.Init(def);
            if (elite != EliteKind.Nenhum) { e.State.Elite = elite; EliteAura.Attach(e); }
            var look = (player.FeetPosition - go.transform.position).Flat();
            if (look.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(look);
            return e;
        }

        public void OnEnemyKilled(ArenaEnemy e)
        {
            int levelBefore = player.State.Progression.Level;
            bool elite = e.State.Elite != EliteKind.Nenhum;
            player.State.AddKill(elite ? Mathf.RoundToInt(e.Def.Xp * EliteRules.XpMultiplier) : e.Def.Xp);
            if (e.State.Elite == EliteKind.Explosivo) EliteAura.ExplodeLater(e.Center);
            player.State.OnKillPassive();
            DropLoot(e);
            if (e is IBoss && Challenge == Challenge.BossRush) { Boss = null; OnRushBossKilled(); }
            else if (e is IBoss && Mode == GameMode.Dungeon)
            {
                if (Challenge == Challenge.Diario)
                {
                    var o = SaveSystem.Options;
                    int today = ChallengeRules.DailySeed(System.DateTime.Now);
                    bool record = o.dailyDate != today || ChallengeRules.BetterTime(challengeT, o.dailyBest);
                    if (record) { o.dailyDate = today; o.dailyBest = challengeT; SaveSystem.SaveOptions(); }
                    Message("DESAFIO DIÁRIO VENCIDO em " + ChallengeRules.Time(challengeT) + (record ? " — RECORDE DO DIA!" : ""), 5f);
                    Challenge = Challenge.Nenhum;
                }
                Boss = null;
                dungeon.OnBossKilled(e.Center);
                player.State.DungeonTier = ArcadeRules.Clamp(player.State.DungeonTier + 1);
                Message(e.Def.Name + " derrotado! Profundidade " + player.State.DungeonTier + " liberada (mais difícil, itens mais raros)", 3.5f);
                var saved = SaveSystem.Load();
                SaveSystem.Save(player.State, -1, (saved?.bossesDefeated ?? 0) + 1);
            }
            else if (e is IBoss)
            {
                Director.OnBossDefeated();
                player.State.ResetKills();
                Boss = null;
                player.State.ArenaTier = ArcadeRules.Clamp(player.State.ArenaTier + 1);
                Message(e.Def.Name + " derrotado! Arena na profundidade " + player.State.ArenaTier, 2.5f);
            }
            // Arena: altar de bênçãos a cada N abates (no centro, se não houver outro).
            if (Mode == GameMode.Arena && Boss == null && player.State.Kills > 0 && player.State.Kills % RunBlessings.ArenaKillsPerAltar == 0 && BlessingAltar.All.Count == 0)
                OfferBlessings(Vector3.zero, null);
            if (player.State.Progression.Level > levelBefore)
                Message($"Nível {player.State.Progression.Level}! +1 ponto de habilidade (abra a árvore)", 2f);
            RequestSave();
        }

        // Ouro vai direto para o jogador; às vezes cai uma arma (o chefe sempre deixa uma rara ou melhor).
        void DropLoot(ArenaEnemy e)
        {
            if (Mode == GameMode.Treino) return;
            int gold = Loot.RollGold(e.Def.Kind, CurrentTier);
            if (e.State.Elite != EliteKind.Nenhum) gold = Mathf.RoundToInt(gold * EliteRules.GoldMultiplier);
            gold = player.State.Blessings.ScaleGold(gold);
            if (gold > 0)
            {
                player.State.AddGold(gold);
                ArenaFx.FloatText("+" + gold + " ouro", e.Center + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.3f), 0.9f);
            }
            var item = Loot.RollDrop(e.Def.Kind, CurrentTier);
            if (item == null && e.State.Elite != EliteKind.Nenhum && Random.value < EliteRules.WeaponDropChance) item = Loot.RollChest(2, CurrentTier).Item2;
            if (item == null) return;
            var dir = Random.insideUnitCircle.normalized;
            WeaponPickup.Spawn(item, e.Center, new Vector3(dir.x * 1.5f, 3.5f, dir.y * 1.5f));
            ArenaAudio.Play(Sfx.Pickup, e.Center, 0.8f, 0.7f);
            if (item.Rarity >= Rarity.Raro) Message(ItemInstance.RarityName(item.Rarity) + ": " + item.DisplayName + " caiu!", 1.8f);
        }

        public void OnBossPhase2()
        {
            Director.BossPhase = 2;
            Message((Boss ? Boss.Def.Name : "O chefe") + " entrou em fúria!", 1.8f);
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
            if (EndChallengeOnDeath()) return;
            // Modo arcade: morrer volta à profundidade 1. Na dungeon a descida acaba e você volta ao santuário.
            if (Mode == GameMode.Dungeon)
            {
                player.State.DungeonTier = 1;
                if (!player.Weapon.Held) player.Weapon.Recall();
                GoToSanctuary();
                Message($"Você morreu: {lossText} e a dungeon recomeça da profundidade 1", 3.5f);
                return;
            }
            if (Mode == GameMode.Arena) { player.State.ArenaTier = 1; st.ClearBlessings(); BlessingAltar.ClearAll(); Mercenary.Clear(); }   // morrer na arena acaba a partida
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

        // Arena/Treino: caixas e barris perto das paredes; quebrou = renasce no mesmo lugar depois de um tempo.
        void SpawnArenaBreakables()
        {
            float h = Combat.ArenaHalfSize - 1.3f;
            var spots = new (float x, float z, BreakableKind k)[]
            {
                (-h, -4f, BreakableKind.BarrilExplosivo), (h, 4f, BreakableKind.BarrilExplosivo),
                (4f, -h, BreakableKind.BarrilExplosivo), (-4f, h, BreakableKind.BarrilExplosivo),
                (-h, 5f, BreakableKind.Caixa), (h, -5f, BreakableKind.Barril), (-5f, -h, BreakableKind.Barril), (5f, h, BreakableKind.Caixa),
            };
            foreach (var s in spots) SpawnArenaBreakable(new Vector3(s.x, 0f, s.z), s.k);
            if (Mode == GameMode.Arena) BlessingStatue.Spawn(new Vector3(h - 0.6f, 0f, h - 0.6f), null, BlessingStatue.Price(CurrentTier, 0), repeat: true);   // estátua num canto
        }

        void SpawnArenaBreakable(Vector3 pos, BreakableKind kind)
        {
            var b = Breakable.Spawn(kind, pos, Random.Range(0f, 360f), null);
            var mode = Mode;
            b.OnBroken = _ => StartCoroutine(RespawnBreakable(pos, kind, mode));
        }

        System.Collections.IEnumerator RespawnBreakable(Vector3 pos, BreakableKind kind, GameMode mode)
        {
            yield return new WaitForSeconds(BreakableRules.ArenaRespawnSeconds);
            if (Mode == mode && (Mode == GameMode.Arena || Mode == GameMode.Treino)) SpawnArenaBreakable(pos, kind);
        }

        // Altar com 3 bênçãos que o jogador ainda não tem.
        public void OfferBlessings(Vector3 at, Transform parent)
        {
            if (!Demo.Blessings) return;
            var choices = player.State.Blessings.RollChoices(new System.Random(Random.Range(0, int.MaxValue)));
            if (choices.Count == 0) return;
            BlessingAltar.Spawn(at, player.FeetPosition - at, choices, parent);
            Message("Altar de bênçãos: toque num orbe para escolher", 2.5f);
        }

        public void Message(string text, float seconds)
        {
            CurrentMessage = text;
            MessageTime = seconds;
        }
    }
}
