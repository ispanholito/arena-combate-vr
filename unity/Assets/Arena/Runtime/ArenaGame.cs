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
        public GoblinKing Boss { get; private set; }
        public string CurrentMessage { get; private set; } = "";
        public float MessageTime { get; private set; }

        public const float BonfireRadius = 1.6f, BonfireHealPerSecond = 25f;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            player.TeleportTo(BonfireStandPoint, Vector3.zero);
            Message($"{player.State.Class.Name}: sobreviva às ondas. Fogueira = cura.", 3f);
        }

        Vector3 BonfireStandPoint => bonfire.position + (Vector3.zero - bonfire.position).Flat().normalized * 1.5f;

        void Update()
        {
            float dt = Time.deltaTime;
            MessageTime -= dt;
            if (spawnWaves && !player.TreeOpen)
            {
                int alive = 0, goblins = 0;
                foreach (var e in ArenaEnemy.All) { if (e == null || e.Dead) continue; alive++; if (e is GoblinEnemy && !(e is GoblinKing)) goblins++; }
                var def = Director.Tick(dt, alive, goblins, player.State.Progression.Level, player.State.Kills);
                if (def != null) SpawnFromDirector(def);
            }
            // Fogueira: perto dela cura
            if (Vector3.Distance(player.FeetPosition.Flat(), bonfire.position.Flat()) < BonfireRadius)
                player.State.Hp.Restore(BonfireHealPerSecond * dt);
        }

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

        public ArenaEnemy Spawn(EnemyDef def, Vector3 pos, bool fromDirector)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(enemyRoot, false);
            go.transform.position = ArenaUtil.ClampToArena(new Vector3(pos.x, 0, pos.z));
            ArenaEnemy e = def.Kind switch
            {
                EnemyKind.Slime => go.AddComponent<SlimeEnemy>(),
                EnemyKind.Goblin => go.AddComponent<GoblinEnemy>(),
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
            if (e is GoblinKing)
            {
                Director.OnBossDefeated();
                player.State.ResetKills();
                Boss = null;
                Message("Rei Goblin derrotado!", 2.5f);
            }
            if (player.State.Progression.Level > levelBefore)
                Message($"Nível {player.State.Progression.Level}! +1 ponto de habilidade (abra a árvore)", 2f);
        }

        public void OnBossPhase2()
        {
            Director.BossPhase = 2;
            Message("O Rei Goblin entrou em fúria!", 1.8f);
        }

        public void OnPlayerDied(int lostXp)
        {
            foreach (var e in ArenaEnemy.All.ToArray()) if (e) Destroy(e.gameObject);
            ArenaEnemy.All.Clear();
            Boss = null;
            Director.Reset(3f);
            if (!player.Weapon.Held) player.Weapon.Recall();
            player.TeleportTo(BonfireStandPoint, Vector3.zero);
            Message($"Você morreu: perdeu {lostXp} XP do nível atual", 2.6f);
        }

        public void Message(string text, float seconds)
        {
            CurrentMessage = text;
            MessageTime = seconds;
        }
    }
}
