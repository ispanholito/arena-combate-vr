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

        WeaponDef[] loadout;
        int weaponIndex;
        float yaw, pitch;
        float dashT, chargeT, chargeLen;
        Vector3 dashDir, chargeDir;
        readonly HashSet<ArenaEnemy> chargeHits = new HashSet<ArenaEnemy>();
        float vignette;

        public float VignetteLevel => vignette;

        void Awake()
        {
            Weapon = new GameObject("ArmaDoJogador").AddComponent<PlayerWeapon>();
            Weapon.Init(this);
            Shield = GetComponent<PlayerShield>();
            ResetCharacter(startClass);
            yaw = rigRoot.eulerAngles.y;
        }

        // Personagem novo (nível 1) da classe escolhida. Usado pelo menu inicial a cada partida.
        public void ResetCharacter(ClassId cls)
        {
            State = new PlayerState(cls);
            State.Died += lost =>
            {
                if (ArenaGame.Instance) ArenaGame.Instance.OnPlayerDied(lost);
            };
            SetupLoadout();
            dashT = chargeT = 0f;
        }

        void SetupLoadout()
        {
            loadout = WeaponDef.LoadoutFor(State.Class.Id);
            weaponIndex = 0;
            Weapon.Equip(loadout[0]);
        }

        public void ChangeClass(ClassId cls)
        {
            State.SetClass(cls);
            SetupLoadout();
        }

        public void SwitchWeapon()
        {
            if (loadout.Length < 2 || !Weapon.Held) return;
            weaponIndex = (weaponIndex + 1) % loadout.Length;
            Weapon.Equip(loadout[weaponIndex]);
            if (ArenaGame.Instance) ArenaGame.Instance.Message(loadout[weaponIndex].Name, 0.9f);
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
                if (Input.MenuConfirm) game.SetPaused(false);          // A / Enter: continuar
                else if (Input.MenuRespec) game.ReturnToMenu();      // B / R: menu inicial
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
                if (Input.SnapTurn != 0) SnapTurn(Input.SnapTurn * 30f);
                Locomotion(dt);
            }
            State.Tick(dt);
            vignette = Mathf.MoveTowards(vignette, 0f, dt * 2f);
            Weapon.Tick(dt, IsVr, !IsVr && Input.DesktopParry);
            if (!IsVr && !TreeOpen && Input.DesktopSwing) Weapon.DesktopSwing();
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
            trackingSpace.localPosition = new Vector3(0f, heightOffset, 0f);
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
            }
            // Mantém a cabeça dentro da arena (vale também para quem anda no quarto)
            var hp = head.position;
            var clamped = ArenaUtil.ClampToArena(hp);
            rigRoot.position += new Vector3(clamped.x - hp.x, 0, clamped.z - hp.z);
        }

        public void TryDash()
        {
            if (dashT > 0f || chargeT > 0f || !State.TryDash()) return;
            dashT = Combat.DashDuration;
            var f = HeadForward; var r = new Vector3(f.z, 0, -f.x);
            var mv = Input.Move;
            dashDir = mv.sqrMagnitude < 0.01f ? -f : (f * mv.y + r * mv.x).normalized;
            vignette = 1f;
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
            if (!State.TakeDamage(amount)) return;
            Hurt?.Invoke(amount);
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
