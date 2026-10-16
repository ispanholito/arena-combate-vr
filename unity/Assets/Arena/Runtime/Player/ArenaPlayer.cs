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

        void Awake()
        {
            Weapon = new GameObject("ArmaDoJogador").AddComponent<PlayerWeapon>();
            Weapon.Init(this);
            Shield = GetComponent<PlayerShield>();
            if (!GetComponent<WeaponAbilityCaster>()) gameObject.AddComponent<WeaponAbilityCaster>();   // cenas antigas não têm
            pauseMenu = GetComponent<PauseMenu>();
            if (!pauseMenu) pauseMenu = gameObject.AddComponent<PauseMenu>();
            if (!GetComponent<DungeonMap>()) gameObject.AddComponent<DungeonMap>();
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

        void Update()
        {
            float dt = Time.deltaTime;
            IsVr = XRSettings.isDeviceActive;
            Input.Update(IsVr, Time.unscaledDeltaTime);   // entrada e altura funcionam também pausado
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
            if (!IsVr && !TreeOpen && Input.DesktopSwing) Weapon.DesktopSwing();
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
                rigRoot.position += (f * mv.y + r * mv.x) * Combat.MoveSpeed * dt;
                // Passos: um a cada 0,75 m andados com o analógico.
                stepDist += mv.magnitude * Combat.MoveSpeed * dt;
                if (stepDist > 0.75f) { stepDist = 0f; ArenaAudio.Play(Sfx.Step, FeetPosition, 0.35f, Random.Range(0.85f, 1.1f)); }
                if (IsVr && SaveSystem.Options.moveVignette && mv.magnitude > 0.15f)
                    vignette = Mathf.Max(vignette, GameOptions.MoveVignetteLevel * Mathf.Min(1f, mv.magnitude));
            }
            if (pushVel.sqrMagnitude > 1e-4f) { rigRoot.position += pushVel * dt; pushVel *= Mathf.Exp(-6f * dt); }
            // Mantém a cabeça dentro da arena (vale também para quem anda no quarto)
            var hp = head.position;
            var clamped = ArenaUtil.ClampToArena(hp);
            rigRoot.position += new Vector3(clamped.x - hp.x, 0, clamped.z - hp.z);
        }

        // Empurrão no jogador (onda de choque do chefe): somado ao movimento e amortecido.
        Vector3 pushVel;
        public void Push(Vector3 velocity) => pushVel = velocity.Flat();

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
            amount *= DifficultyRules.DamageTaken(SaveSystem.Options.Difficulty) * ArcadeRules.DamageMultiplier(ArenaGame.Instance ? ArenaGame.Instance.CurrentTier : 1);
            if (!State.TakeDamage(amount)) return;
            Hurt?.Invoke(amount);
            ArenaAudio.Play2D(Sfx.Hurt, 0.8f);
            ArenaInput.Haptic(0.8f, 0.12f); ArenaInput.Haptic(0.8f, 0.12f, left: true);
        }

        public void Vignette(float level) => vignette = Mathf.Max(vignette, level);

        public void TeleportTo(Vector3 feet, Vector3 lookAt)
        {
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
