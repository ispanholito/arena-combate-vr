using System.Collections.Generic;
using Arena.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Arena
{
    // Jogador: OVRCameraRig + PlayerState (Core). Funciona em VR e em modo desktop (sem headset) para testar no editor.
    public sealed class ArenaPlayer : MonoBehaviour
    {
        [Header("OVRCameraRig")]
        public Transform rigRoot;
        public Transform trackingSpace;
        public Transform head;
        public Transform rightHand;
        public Transform leftHand;

        [Header("Jogo")]
        public ClassId startClass = ClassId.Lamina;
        public float desktopEyeHeight = 1.7f;
        public float mouseSensitivity = 0.12f;

        public PlayerState State { get; private set; }
        public ArenaInput Input { get; } = new ArenaInput();
        public PlayerWeapon Weapon { get; private set; }
        public PlayerShield Shield { get; private set; }
        public bool IsVr { get; private set; }
        public bool TreeOpen { get; set; }

        public Transform Head => head;
        public Transform RightHand => rightHand;
        public Vector3 HeadPosition => head.position;
        public Vector3 FeetPosition { get { var p = head.position; p.y = rigRoot.position.y; return p; } }
        public Vector3 HeadForward { get { var f = head.forward.Flat(); return f.sqrMagnitude > 1e-4f ? f.normalized : rigRoot.forward; } }

        public event System.Action<float> Hurt;

        float yaw, pitch;
        float dashT, chargeT, chargeLen;
        Vector3 dashDir, chargeDir;
        readonly HashSet<ArenaEnemy> chargeHits = new HashSet<ArenaEnemy>();
        float vignette;
        PauseMenu pauseMenu;
        float stepDist;

        public float VignetteLevel => vignette;

        // ---------- Mãos do jogo ----------
        // O OVRCameraRig troca sozinho a âncora da mão para o rastreamento de mão quando o sensor da Meta acha que o
        // controle foi largado — e isso falha com o controle NA mão. Por isso o jogo usa as próprias "mãos": com o
        // controle na mão, a pose vem SEMPRE do controle (OVRInput); sem controle, da âncora (mão rastreada).
        // Controle fora da vista das câmeras (posição não rastreada): a posição fica parada, só a rotação atualiza —
        // evita a arma dar saltos.
        Transform anchorRight, anchorLeft;

        void CreateGameHands()
        {
            anchorRight = rightHand; anchorLeft = leftHand;
            rightHand = new GameObject("MaoDireitaJogo").transform;
            rightHand.SetParent(trackingSpace, false);
            leftHand = new GameObject("MaoEsquerdaJogo").transform;
            leftHand.SetParent(trackingSpace, false);
            SyncGameHands();
        }

        void SyncGameHands()
        {
            if (!anchorRight) return;
            bool vr = XRSettings.isDeviceActive;
            SyncHand(rightHand, anchorRight, OVRInput.Controller.RTouch, vr && Input.RightControllerHeld && OVRInput.IsControllerConnected(OVRInput.Controller.RTouch), ref untrackedR);
            SyncHand(leftHand, anchorLeft, OVRInput.Controller.LTouch, vr && Input.LeftControllerConnected, ref untrackedL);
        }

        // No balanço rápido o Quest perde o rastreamento óptico por instantes, mas continua prevendo a posição pelos sensores
        // do controle: usa essa previsão. Só congela se ficar mais de 0,4 s sem rastreamento (controle fora das câmeras).
        // (Antes congelava na hora — a arma ficava para trás e "saía da mão" no balanço.)
        float untrackedR, untrackedL;
        const float FreezeAfterUntracked = 0.4f;

        static void SyncHand(Transform hand, Transform anchor, OVRInput.Controller c, bool useController, ref float untracked)
        {
            if (!useController)
            {
                hand.SetLocalPositionAndRotation(anchor.localPosition, anchor.localRotation);
                untracked = 0f;
                return;
            }
            var rot = OVRInput.GetLocalControllerRotation(c);
            untracked = OVRInput.GetControllerPositionTracked(c) ? 0f : untracked + Time.deltaTime;
            var pos = untracked < FreezeAfterUntracked ? OVRInput.GetLocalControllerPosition(c) : hand.localPosition;
            hand.SetLocalPositionAndRotation(pos, rot);
        }

        void FixedUpdate() => SyncGameHands();

        void Awake()
        {
            CreateGameHands();
            Weapon = new GameObject("ArmaDoJogador").AddComponent<PlayerWeapon>();
            Weapon.Init(this);
            Shield = GetComponent<PlayerShield>();
            if (!GetComponent<WeaponAbilityCaster>()) gameObject.AddComponent<WeaponAbilityCaster>();   // cenas antigas não têm
            pauseMenu = GetComponent<PauseMenu>();
            if (!pauseMenu) pauseMenu = gameObject.AddComponent<PauseMenu>();
            if (!GetComponent<DungeonMap>()) gameObject.AddComponent<DungeonMap>();
            if (!GetComponent<HandVisual>()) gameObject.AddComponent<HandVisual>();   // mãos rastreadas visíveis
            if (!GetComponent<EnemyGrabber>()) gameObject.AddComponent<EnemyGrabber>();   // agarrar inimigos com a mão vazia
            if (!GetComponent<SpiderLocomotion>()) gameObject.AddComponent<SpiderLocomotion>();   // teia por pontos de gancho + dash (sinal do Homem-Aranha)
            if (!GetComponent<HandMagic>()) gameObject.AddComponent<HandMagic>();   // mago sem controle: raízes, Kamehameha, bola de fogo
            if (!GetComponent<HandPointer>()) gameObject.AddComponent<HandPointer>();   // ponteiro do dedo + pinça nos menus
            if (!GetComponent<SpellBook>()) gameObject.AddComponent<SpellBook>();
            if (!GetComponent<FlameBlade>()) gameObject.AddComponent<FlameBlade>();
            if (!GetComponent<GunController>()) gameObject.AddComponent<GunController>();   // pistola e bacamarte
            if (!GetComponent<PetCompanion>()) gameObject.AddComponent<PetCompanion>();   // fada da Domadora
            if (!GetComponent<WeaponSwapGesture>()) gameObject.AddComponent<WeaponSwapGesture>();   // mão livre por cima do ombro = troca de arma
            if (!GetComponent<ArmSwingRun>()) gameObject.AddComponent<ArmSwingRun>();   // sem controle: correr balançando os braços
            if (!GetComponent<HandInteract>()) gameObject.AddComponent<HandInteract>();
            if (!GetComponent<PotionBelt>()) gameObject.AddComponent<PotionBelt>();   // poção no peito (VR)   // sem controle: pinça perto = interagir
            if (!GetComponent<Muralha>()) gameObject.AddComponent<Muralha>();   // punho na palma: aura vermelha, só soco forte
            if (!GetComponent<SkyBolt>()) gameObject.AddComponent<SkyBolt>();   // mão fechada ao céu e abrir: raio em cadeia   // arma mergulhada na bola de fogo pega fogo   // livro de magias (abre pela pausa)
            ResetCharacter(startClass);
            yaw = rigRoot.eulerAngles.y;
        }

        // Personagem novo (nível 1) da classe escolhida. Usado pelo menu inicial a cada partida.
        public void ResetCharacter(ClassId cls) => UseCharacter(new PlayerState(cls));

        // Personagem carregado do jogo salvo (ou um novo).
        public void UseCharacter(PlayerState state)
        {
            if (State != state)   // o mesmo personagem passa de um modo para outro: não assina os eventos de novo
            {
                State = state;
                State.LeveledUp += _ => ArenaAudio.Play2D(Sfx.Level, 0.8f);
                State.Died += lost =>
                {
                    if (ArenaGame.Instance) ArenaGame.Instance.OnPlayerDied(lost);
                };
                State.SecondChance += () =>
                {
                    if (ArenaGame.Instance) ArenaGame.Instance.Message("SEGUNDA CHANCE! Você levantou com metade da vida", 2.5f);
                    ArenaAudio.Play2D(Sfx.Level, 1f, 0.7f);
                    ArenaFx.Burst(HeadPosition + Vector3.down * 0.6f, new Color(1f, 1f, 0.75f), 40, 4f);
                };
            }
            SetupLoadout();
            dashT = chargeT = 0f;
        }

        void SetupLoadout() => EquipActive();

        // Equipa a arma do slot ativo (com raridade, atributos e ferreiro). Chamado também depois de pegar loot ou melhorar.
        public void EquipActive()
        {
            var item = State.ActiveItem;
            if (item != null) Weapon.Equip(item.Resolve(State.Class.Id), item.Rarity);
        }

        public void ChangeClass(ClassId cls)
        {
            State.SetClass(cls);
            SetupLoadout();
        }

        public void SwitchWeapon()
        {
            int other = 1 - State.ActiveSlot;
            if (State.Loadout[other] == null || !Weapon.Held) return;
            State.ActiveSlot = other;
            EquipActive();
            if (ArenaGame.Instance) ArenaGame.Instance.Message(State.ActiveItem.DisplayName, 0.9f);
        }

        // Poção: no VR fica no peito (PotionBelt: pegar o frasco e levar à boca); desktop B.
        void UpdatePotion(float dt)
        {
            if (State == null || ArenaGame.Blocked || !Input.DesktopDrink) return;
            if (State.Potions <= 0) { ArenaGame.Instance?.Message("Sem poções (baús e o mercador do santuário)", 1.2f); ArenaAudio.Play2D(Sfx.Deny, 0.5f); return; }
            DrinkPotion();
        }

        public void DrinkPotion()
        {
            var game = ArenaGame.Instance;
            if (!State.TryDrinkPotion()) return;
            ArenaAudio.Play2D(Sfx.Slime, 0.7f, 1.5f);
            ArenaFx.Burst(head.position - head.up * 0.25f + head.forward * 0.3f, new Color(1f, 0.25f, 0.3f), 14, 1.5f);
            ArenaInput.Haptic(0.4f, 0.15f);
            game?.Message("Poção: +" + Mathf.RoundToInt(State.Hp.Max * PlayerState.PotionHeal) + " de vida (" + State.Potions + " restantes)", 1.4f);
            game?.RequestSave();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            IsVr = XRSettings.isDeviceActive;
            Input.Update(IsVr, Time.unscaledDeltaTime);   // entrada e altura funcionam também pausado
            SyncGameHands();
            var game = ArenaGame.Instance;
            if (Input.PausePressed && game && !ArenaGame.InMenu) game.SetPaused(!ArenaGame.Paused);
            if (ArenaGame.Paused)
            {
                pauseMenu.HandleInput();   // continuar, opções, voltar ao santuário
                if (!IsVr) Cursor.lockState = CursorLockMode.None;
                else VrHeight(Time.unscaledDeltaTime);
                return;
            }
            if (!IsVr) DesktopLook();
            else VrHeight(dt);

            if (Input.ToggleTree && !ArenaGame.InMenu) TreeOpen = !TreeOpen;
            UpdatePotion(dt);
            if (!TreeOpen)
            {
                if (Input.SwitchWeapon) SwitchWeapon();
                if (Input.Dash) TryDash();
                var opts = SaveSystem.Options;
                if (opts.Turn == TurnMode.Suave)
                {
                    if (Mathf.Abs(Input.TurnAxis) > 0.2f)
                    {
                        SnapTurn(Input.TurnAxis * GameOptions.SmoothTurnSpeed * dt);
                        if (opts.moveVignette) vignette = Mathf.Max(vignette, GameOptions.MoveVignetteLevel);
                    }
                }
                else if (Input.SnapTurn != 0) SnapTurn(Input.SnapTurn * opts.SnapDegrees);
                Locomotion(dt);
            }
            State.Tick(dt);
            vignette = Mathf.MoveTowards(vignette, 0f, dt * 2f);
            Weapon.Tick(dt, IsVr, !IsVr && Input.DesktopParry);
            if (!IsVr && !TreeOpen && Input.DesktopSwing && !Weapon.Def.IsGun) Weapon.DesktopSwing();   // arma de fogo: o clique atira (GunController)
            if (!IsVr && !TreeOpen && Input.DesktopThrust) Weapon.DesktopThrust();
            if (!IsVr) Weapon.DesktopThrustHold(Input.DesktopThrustHeld);
        }

        // ---------- Altura no VR ----------
        // Com o chão do Guardian mal calibrado (ou depois de recentralizar) a cabeça pode ficar perto de 0 e o jogador
        // "entra na terra". Na partida e a cada recentralização: se a cabeça estiver abaixo de 1,2 m, sobe o jogador
        // para a altura em pé dele (a maior medida até agora, ou 1,65 m).
        public const float DefaultStandingEye = 1.65f, MinPlausibleEye = 1.2f;
        float heightOffset, standingEye = DefaultStandingEye, heightCheckT = 0.5f;
        bool recenterHooked;

        void VrHeight(float dt)
        {
            if (!recenterHooked && OVRManager.display != null) { OVRManager.display.RecenteredPose += () => heightCheckT = 0.3f; recenterHooked = true; }
            float eye = head.localPosition.y;   // altura da cabeça medida pelo headset (sem o nosso deslocamento)
            if (heightOffset == 0f && eye > MinPlausibleEye && eye < 2.1f) standingEye = Mathf.Max(standingEye == DefaultStandingEye ? eye : standingEye, eye);
            if (heightCheckT > 0f)
            {
                heightCheckT -= dt;
                if (heightCheckT <= 0f) heightOffset = eye < MinPlausibleEye ? standingEye - eye : 0f;
            }
            trackingSpace.localPosition = new Vector3(0f, heightOffset + (SaveSystem.Options.seated ? GameOptions.SeatedBoost : 0f), 0f);
        }

        // ---------- Movimento ----------
        void DesktopLook()
        {
            trackingSpace.localPosition = new Vector3(0, desktopEyeHeight, 0);
            if (Cursor.lockState != CursorLockMode.Locked && Input.DesktopSwing) Cursor.lockState = CursorLockMode.Locked;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked || TreeOpen) return;
            yaw += Input.Look.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - Input.Look.y * mouseSensitivity, -80f, 80f);
            rigRoot.rotation = Quaternion.Euler(0, yaw, 0);
            trackingSpace.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void SnapTurn(float degrees)
        {
            var pivot = head.position;
            rigRoot.RotateAround(pivot, Vector3.up, degrees);
            yaw = rigRoot.eulerAngles.y;
        }

        void Locomotion(float dt)
        {
            var f = HeadForward; var r = new Vector3(f.z, 0, -f.x);
            if (chargeT > 0f)
            {
                chargeT -= dt;
                rigRoot.position += chargeDir * (chargeLen / Combat.ChargeDuration) * dt;
                ChargeHits();
                if (chargeT <= 0f && chargeHits.Count > 1 && ArenaGame.Instance) ArenaGame.Instance.Message($"Perfurou {chargeHits.Count}!", 0.9f);
            }
            else if (dashT > 0f)
            {
                dashT -= dt;
                rigRoot.position += dashDir * Combat.DashSpeed * dt;
            }
            else
            {
                var mv = Input.Move;
                float speed = Combat.MoveSpeed * State.Blessings.MoveMultiplier;   // bênção Pés Leves
                rigRoot.position += (f * mv.y + r * mv.x) * speed * dt;
                // Passos: um a cada 0,75 m andados com o analógico.
                stepDist += mv.magnitude * speed * dt;
                if (stepDist > 0.75f) { stepDist = 0f; ArenaAudio.Play(Sfx.Step, FeetPosition, 0.35f, Random.Range(0.85f, 1.1f)); }
                if (IsVr && SaveSystem.Options.moveVignette && mv.magnitude > 0.15f)
                    vignette = Mathf.Max(vignette, GameOptions.MoveVignetteLevel * Mathf.Min(1f, mv.magnitude));
            }
            if (pushVel.sqrMagnitude > 1e-4f) { rigRoot.position += pushVel * dt; pushVel *= Mathf.Exp(-6f * dt); }
            // Mantém a cabeça dentro da arena (vale também para quem anda no quarto)
            var hp = head.position;
            var clamped = ArenaUtil.ClampToArena(hp);
            rigRoot.position += new Vector3(clamped.x - hp.x, 0, clamped.z - hp.z);
            AirPhysics(dt);
        }

        // Empurrão no jogador (onda de choque do chefe): somado ao movimento e amortecido.
        Vector3 pushVel;
        public void Push(Vector3 velocity) => pushVel = velocity.Flat();

        // ---------- Sem controle: dash para trás e teia (SpiderLocomotion) ----------
        // Segurando a teia: sem gravidade. Solto: continua com o embalo e cai em arco (2026-10-28: antes parava no lugar).
        public bool Hanging { get; set; }
        public bool Coasting { get; set; }   // acabou de puxar a teia: ainda não freia
        Vector3 airVel;
        public Vector3 AirVelocity { get => airVel; set => airVel = Vector3.ClampMagnitude(value, HandMagicRules.MaxAirSpeed); }

        public void DashBack()
        {
            if (dashT > 0f || chargeT > 0f || !State.TryDash()) return;
            dashT = Combat.DashDuration;
            dashDir = -HeadForward;
            TutorialEvents.Raise("dash_tras");
            vignette = 1f;
            ArenaAudio.Play2D(Sfx.Dash, 0.6f);
        }

        // Move o corpo (a teia puxa em 3D): sobe até o teto das salas, nunca abaixo do chão, sem sair da área.
        public void MoveRig(Vector3 delta)
        {
            var p = rigRoot.position + delta;
            p.y = Mathf.Clamp(p.y, 0f, RopeRules.MaxHeight);
            rigRoot.position = p;
        }

        // Física do corpo no ar: velocidade (embalo da teia) + gravidade; no chão o embalo some rápido.
        void AirPhysics(float dt)
        {
            var p = rigRoot.position;
            bool onGround = p.y <= 0.001f && !Hanging;
            if (onGround && airVel.sqrMagnitude < 1e-4f) { airVel = Vector3.zero; return; }
            var (vx, vy, vz) = HandMagicRules.AirStep(airVel.x, airVel.y, airVel.z, Hanging, Coasting, onGround, dt);
            airVel = new Vector3(vx, vy, vz);
            p += airVel * dt;
            if (p.y <= 0f)
            {
                if (airVel.y < -4f) { ArenaAudio.Play(Sfx.Step, FeetPosition, 1f, 0.6f); ArenaInput.Haptic(0.5f, 0.08f); vignette = Mathf.Max(vignette, 0.4f); }
                p.y = 0f; airVel.y = 0f;
            }
            if (p.y > RopeRules.MaxHeight) { p.y = RopeRules.MaxHeight; airVel.y = Mathf.Min(0f, airVel.y); }
            rigRoot.position = p;
            // Paredes: para o embalo na direção da parede.
            var hp = head.position;
            var c = ArenaUtil.ClampToArena(hp);
            if (!Mathf.Approximately(c.x, hp.x)) airVel.x = 0f;
            if (!Mathf.Approximately(c.z, hp.z)) airVel.z = 0f;
            rigRoot.position += new Vector3(c.x - hp.x, 0f, c.z - hp.z);
            if (airVel.sqrMagnitude > 9f && SaveSystem.Options.moveVignette) vignette = Mathf.Max(vignette, GameOptions.MoveVignetteLevel);
        }

        public void TryDash()
        {
            if (dashT > 0f || chargeT > 0f || !State.TryDash()) return;
            dashT = Combat.DashDuration;
            var f = HeadForward; var r = new Vector3(f.z, 0, -f.x);
            var mv = Input.Move;
            dashDir = mv.sqrMagnitude < 0.01f ? -f : (f * mv.y + r * mv.x).normalized;
            vignette = 1f;
            ArenaAudio.Play2D(Sfx.Dash, 0.6f);
        }

        // Investida Perfurante: chamada pelo SkillCaster já com a direção validada.
        public void StartCharge(Vector3 dir)
        {
            chargeT = Combat.ChargeDuration;
            chargeDir = dir.Flat().normalized;
            chargeLen = Combat.ChargeLength(State.Has(SkillIds.InvestidaCadeia));
            chargeHits.Clear();
            State.SetInvulnerable(Combat.ChargeInvulnerable);
            vignette = 0.8f;
            ArenaInput.Haptic(0.8f, 0.2f);
            ArenaAudio.Play2D(Sfx.Skill, 0.6f); ArenaAudio.Play2D(Sfx.Dash, 0.8f);
        }

        void ChargeHits()
        {
            var probe = head.position + Vector3.down * 0.6f;
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (e == null || e.Dead || chargeHits.Contains(e)) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.PointSegmentDistance(a.ToN(), b.ToN(), probe.ToN()) < e.Radius + 0.9f)
                {
                    chargeHits.Add(e);
                    float dmg = Combat.ChargeDamage * (State.Has(SkillIds.FioAfiado) ? Combat.SharpEdgeBonus : 1f);
                    e.ApplyDamage(dmg, 1f, chargeDir, 3f, new Color(0.81f, 0.96f, 1f));
                    ArenaInput.Haptic(1f, 0.06f);
                }
            }
        }

        // ---------- Dano ----------
        public void TakeDamage(float amount, Vector3 from, bool ignoreGuardCooldown = false)
        {
            if (Muralha.TryGuard(from)) return;   // Muralha Impenetrável: as mãos bloqueiam/aparam
            amount *= DifficultyRules.DamageTaken(SaveSystem.Options.Difficulty) * ArcadeRules.DamageMultiplier(ArenaGame.Instance ? ArenaGame.Instance.CurrentTier : 1);
            if (!State.TakeDamage(amount)) return;
            Hurt?.Invoke(amount);
            ArenaAudio.Play2D(Sfx.Hurt, 0.8f);
            ArenaInput.Haptic(0.8f, 0.12f); ArenaInput.Haptic(0.8f, 0.12f, left: true);
        }

        public void Vignette(float level) => vignette = Mathf.Max(vignette, level);

        public void TeleportTo(Vector3 feet, Vector3 lookAt)
        {
            Hanging = false; airVel = Vector3.zero;
            if (rigRoot.position.y != 0f) { var r0 = rigRoot.position; r0.y = 0f; rigRoot.position = r0; }   // volta ao chão
            var offset = head.position - rigRoot.position; offset.y = 0;
            rigRoot.position = new Vector3(feet.x, rigRoot.position.y, feet.z) - offset;
            var dir = (lookAt - feet).Flat();
            if (dir.sqrMagnitude > 0.01f)
            {
                float delta = Vector3.SignedAngle(HeadForward, dir, Vector3.up);
                SnapTurn(delta);
            }
            dashT = chargeT = 0f;
        }
    }
}
