using UnityEngine;
using UnityEngine.InputSystem;

namespace Arena
{
    // Uma entrada só para VR (OVRInput) e desktop (teclado + mouse), lida uma vez por frame.
    // VR com mão esquerda livre: analógico direito move, clique curto = esquiva, segurar = árvore. A + B juntos = árvore (qualquer modo).
    // VR com dois controles: analógico esquerdo move, X esquiva, Y árvore, clique esquerdo troca arma, direito gira.
    // Desktop: WASD, mouse olha, botão esq = golpe, dir = aparo, Shift/Espaço = esquiva, 1/2/3 habilidades,
    //          Q troca arma, Tab árvore, Z metralhadora / X soco no chão / C corte da palma (poderes da mão), F escudo nas costas/na mão.
    public sealed class ArenaInput
    {
        public bool Vr { get; private set; }
        // Controle esquerdo SEGURO na mão (não só conectado: largado na mesa conta como mão livre).
        public bool LeftControllerConnected { get; private set; }
        public bool LeftHandFree => Vr && !LeftControllerConnected;

        public Vector2 Move, Look;
        public bool Dash, ToggleTree, SwitchWeapon, ShieldToggle, PausePressed;
        public int SnapTurn;                       // -1, 0, +1
        public bool APressed, BPressed, BHeld, BReleased;
        public bool TriggerPressed, TriggerHeld, TriggerReleased, GripPressed;
        public bool DesktopSwing, DesktopParry, DesktopThrust, DesktopThrustHeld, DesktopTwoHand;    // desktop: golpe animado / pose de aparo
        public bool DesktopLaser, DesktopSlam, DesktopPalm;
        public bool MenuUp, MenuDown, MenuConfirm, MenuRespec;
        public Vector2 MousePointer;

        bool trigWasHeld, snapReady = true, menuReady = true;
        float stickHoldT; bool stickHoldFired;

        public void Update(bool xrActive, float dt)
        {
            Vr = xrActive;
            Reset();
            if (Vr) ReadVr(dt); else ReadDesktop();
        }

        void Reset()
        {
            Move = Look = Vector2.zero;
            Dash = ToggleTree = SwitchWeapon = ShieldToggle = PausePressed = false; SnapTurn = 0;
            APressed = BPressed = BHeld = BReleased = false;
            TriggerPressed = TriggerHeld = TriggerReleased = GripPressed = false;
            DesktopSwing = DesktopParry = DesktopThrust = DesktopThrustHeld = DesktopTwoHand = DesktopLaser = DesktopSlam = DesktopPalm = false;
            MenuUp = MenuDown = MenuConfirm = MenuRespec = false;
        }

        void ReadVr(float dt)
        {
            const OVRInput.Controller R = OVRInput.Controller.RTouch, L = OVRInput.Controller.LTouch;
            UpdateLeftInHand(dt);
            var rStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, R);

            if (LeftControllerConnected)
            {
                Move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, L);
                Dash = OVRInput.GetDown(OVRInput.Button.One, L);          // X
                ToggleTree = OVRInput.GetDown(OVRInput.Button.Two, L);    // Y
                SwitchWeapon = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, L);
                if (Mathf.Abs(rStick.x) > 0.7f && snapReady) { snapReady = false; SnapTurn = rStick.x > 0 ? 1 : -1; }
                else if (Mathf.Abs(rStick.x) < 0.3f) snapReady = true;
            }
            else
            {
                Move = rStick;
                bool held = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, R);
                if (held)
                {
                    stickHoldT += dt;
                    if (stickHoldT > 0.8f && !stickHoldFired) { stickHoldFired = true; ToggleTree = true; }
                }
                else
                {
                    if (stickHoldT > 0f && !stickHoldFired) Dash = true;
                    stickHoldT = 0f; stickHoldFired = false;
                }
            }
            if (Move.magnitude < 0.15f) Move = Vector2.zero;

            ReadAB(dt);
            // Pausa: botão Menu (☰) do controle esquerdo, ou o gesto de menu da Meta com a mão esquerda livre
            // (palma virada para você + pinça). Os dois chegam como Button.Start.
            PausePressed = OVRInput.GetDown(OVRInput.Button.Start);
            TriggerHeld = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, R) > 0.55f;
            TriggerPressed = TriggerHeld && !trigWasHeld; TriggerReleased = !TriggerHeld && trigWasHeld; trigWasHeld = TriggerHeld;
            GripPressed = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, R);

            // Menu da árvore: analógico direito para cima/baixo, A confirma, B respec
            MenuUp = rStick.y > 0.7f && menuReady; MenuDown = rStick.y < -0.7f && menuReady;
            if (Mathf.Abs(rStick.y) > 0.7f) menuReady = false; else if (Mathf.Abs(rStick.y) < 0.3f) menuReady = true;
            MenuConfirm = APressed; MenuRespec = BPressed;
        }

        // Usa o sensor da Meta que diz se o controle está na mão; o estado só muda depois de 0,2 s estável (evita piscar).
        float inHandStableT;
        void UpdateLeftInHand(float dt)
        {
            var st = OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandLeft);
            bool raw = st == OVRInput.ControllerInHandState.ControllerInHand
                       || (st == OVRInput.ControllerInHandState.NoHand && OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch));
            if (raw == LeftControllerConnected) { inHandStableT = 0f; return; }
            inHandStableT += dt;
            if (inHandStableT >= 0.2f) { LeftControllerConnected = raw; inHandStableT = 0f; }
        }

        // A + B juntos = abre/fecha a árvore. Para distinguir do toque simples, A e B sozinhos só valem depois de
        // ComboWindow (ou ao soltar antes disso).
        public const float ComboWindow = 0.12f;
        float aPend = -1f, bPend = -1f;
        bool bLatched, comboLock;

        void ReadAB(float dt)
        {
            const OVRInput.Controller R = OVRInput.Controller.RTouch;
            bool aRaw = OVRInput.Get(OVRInput.Button.One, R), bRaw = OVRInput.Get(OVRInput.Button.Two, R);
            if (comboLock) { if (!aRaw && !bRaw) comboLock = false; return; }
            if (OVRInput.GetDown(OVRInput.Button.One, R)) aPend = 0f;
            if (OVRInput.GetDown(OVRInput.Button.Two, R)) bPend = 0f;
            if (aPend >= 0f && bPend >= 0f)
            {
                ToggleTree = true; aPend = bPend = -1f; bLatched = false; comboLock = true;
                return;
            }
            if (aPend >= 0f) { aPend += dt; if (aPend >= ComboWindow || !aRaw) { APressed = true; aPend = -1f; } }
            if (bPend >= 0f) { bPend += dt; if (bPend >= ComboWindow || !bRaw) { BPressed = true; bLatched = true; bPend = -1f; } }
            BHeld = bLatched && bRaw;
            if (bLatched && !bRaw) { BReleased = true; bLatched = false; }
        }

        void ReadDesktop()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null) return;
            Move = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            if (Move.sqrMagnitude > 1f) Move.Normalize();
            Dash = kb.leftShiftKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            ToggleTree = kb.tabKey.wasPressedThisFrame;
            SwitchWeapon = kb.qKey.wasPressedThisFrame;
            APressed = kb.digit1Key.wasPressedThisFrame;          // habilidade 1 (Investida / Lança de Gelo)
            TriggerHeld = kb.digit2Key.isPressed;                  // habilidade 2 (Contra / Bola de Fogo segurada)
            TriggerPressed = kb.digit2Key.wasPressedThisFrame;
            TriggerReleased = kb.digit2Key.wasReleasedThisFrame;
            BHeld = kb.digit3Key.isPressed;                        // habilidade 3 (Lâmina de Vento / Raízes)
            BPressed = kb.digit3Key.wasPressedThisFrame;
            BReleased = kb.digit3Key.wasReleasedThisFrame;
            GripPressed = kb.gKey.wasPressedThisFrame;
            ShieldToggle = kb.fKey.wasPressedThisFrame;
            DesktopThrust = kb.tKey.wasPressedThisFrame;   // estocada (crava a espada)
            DesktopThrustHeld = kb.tKey.isPressed;
            DesktopTwoHand = kb.vKey.wasPressedThisFrame;   // duas mãos
            PausePressed = kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
            DesktopLaser = kb.zKey.isPressed;
            DesktopSlam = kb.xKey.wasPressedThisFrame;
            DesktopPalm = kb.cKey.wasPressedThisFrame;
            MenuUp = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            MenuDown = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            MenuConfirm = kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
            MenuRespec = kb.rKey.wasPressedThisFrame;
            if (mouse != null)
            {
                Look = mouse.delta.ReadValue();
                DesktopSwing = mouse.leftButton.wasPressedThisFrame;
                DesktopParry = mouse.rightButton.isPressed;
                MousePointer = mouse.position.ReadValue();
            }
        }

        public static void Haptic(float amplitude, float seconds, bool left = false)
        {
            var c = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            if (!OVRInput.IsControllerConnected(c)) return;
            OVRInput.SetControllerVibration(0.5f, Mathf.Clamp01(amplitude), c);
            HapticStopper.Schedule(c, seconds);
        }
    }

    // Desliga a vibração depois do tempo pedido (OVRInput não tem duração).
    sealed class HapticStopper : MonoBehaviour
    {
        static HapticStopper inst;
        float stopR, stopL;

        public static void Schedule(OVRInput.Controller c, float seconds)
        {
            if (inst == null) { var go = new GameObject("HapticStopper") { hideFlags = HideFlags.HideAndDontSave }; inst = go.AddComponent<HapticStopper>(); }
            float t = Time.unscaledTime + seconds;
            if (c == OVRInput.Controller.LTouch) inst.stopL = t; else inst.stopR = t;
        }

        void Update()
        {
            if (stopR > 0 && Time.unscaledTime > stopR) { stopR = 0; OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch); }
            if (stopL > 0 && Time.unscaledTime > stopL) { stopL = 0; OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch); }
        }
    }
}
