using UnityEngine;
using Warfront.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Warfront.Game
{
    /// <summary>
    /// Single source of truth for player intent.
    ///
    /// Touch (mobile UI), keyboard/mouse and gamepad all feed the same flags, and edge detection is
    /// computed here so gameplay code never cares which device is in use. Both the new Input System
    /// and the legacy Input Manager are supported through compile time guards, so the project works
    /// whichever backend the Player Settings have active.
    /// </summary>
    public class InputService
    {
        private SettingsService _settings;
        private bool _prevFire;
        private bool _prevAim;
        private bool _prevInteract;
        private bool _prevWeaponSwitch;

        // Mobile UI contributions (set by the on-screen controls each frame).
        public Vector2 MobileMove;
        public Vector2 MobileLookDelta;
        public bool MobileFireHeld;
        public bool MobileAimHeld;
        public bool MobileSprintHeld;
        public bool MobileInteractHeld;
        public bool MobileReloadQueued;
        public bool MobileCrouchQueued;
        public bool MobileDodgeQueued;
        public bool MobileJumpQueued;
        public bool MobilePauseQueued;
        public int MobileWeaponSlotQueued = -1;
        public bool MobileUseEquipmentQueued;

        public InputService(SettingsService settings)
        {
            _settings = settings;
        }

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool AimHeld { get; private set; }
        public bool AimPressed { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool CrouchPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool InteractHeld { get; private set; }
        public bool PausePressed { get; private set; }
        public bool UseEquipmentPressed { get; private set; }
        public bool WeaponSwitchPressed { get; private set; }
        public int WeaponSlotPressed { get; private set; }
        public bool AnyMovementInput { get { return Move.sqrMagnitude > 0.01f; } }

        /// <summary>True while the player is actively steering the camera (used for aim assist / idle).</summary>
        public bool LookActive { get { return LookDelta.sqrMagnitude > 0.0001f; } }

        public void Tick(float deltaTime)
        {
            Vector2 move = MobileMove;
            Vector2 look = MobileLookDelta;
            bool fire = MobileFireHeld;
            bool aim = MobileAimHeld;
            bool sprint = MobileSprintHeld;
            bool reload = MobileReloadQueued;
            bool crouch = MobileCrouchQueued;
            bool dodge = MobileDodgeQueued;
            bool jump = MobileJumpQueued;
            bool pause = MobilePauseQueued;
            bool interact = MobileInteractHeld;
            bool useEquipment = MobileUseEquipmentQueued;
            bool weaponSwitch = false;
            int weaponSlot = MobileWeaponSlotQueued;

#if ENABLE_INPUT_SYSTEM
            ReadKeyboardAndGamepad(ref move, ref look, ref fire, ref aim, ref sprint, ref reload, ref crouch,
                ref dodge, ref jump, ref pause, ref interact, ref useEquipment, ref weaponSwitch, ref weaponSlot);
#elif ENABLE_LEGACY_INPUT_MANAGER
            ReadLegacy(ref move, ref look, ref fire, ref aim, ref sprint, ref reload, ref crouch,
                ref dodge, ref jump, ref pause, ref interact, ref useEquipment, ref weaponSwitch, ref weaponSlot);
#endif

            float sensitivity = _settings != null ? _settings.Data.LookSensitivity : 1f;
            bool invertY = _settings != null && _settings.Data.InvertY;

            Move = Vector2.ClampMagnitude(move, 1f);
            LookDelta = new Vector2(look.x * sensitivity, look.y * sensitivity * (invertY ? -1f : 1f));

            FirePressed = fire && !_prevFire;
            AimPressed = aim && !_prevAim;
            InteractPressed = interact && !_prevInteract;
            WeaponSwitchPressed = weaponSwitch && !_prevWeaponSwitch;
            WeaponSlotPressed = weaponSlot;

            FireHeld = fire;
            AimHeld = aim;
            SprintHeld = sprint;
            ReloadPressed = reload;
            CrouchPressed = crouch;
            DodgePressed = dodge;
            JumpPressed = jump;
            PausePressed = pause;
            InteractHeld = interact;
            UseEquipmentPressed = useEquipment;

            _prevFire = fire;
            _prevAim = aim;
            _prevInteract = interact;
            _prevWeaponSwitch = weaponSwitch;

            // One-frame mobile queues are consumed by this tick.
            MobileReloadQueued = false;
            MobileCrouchQueued = false;
            MobileDodgeQueued = false;
            MobileJumpQueued = false;
            MobilePauseQueued = false;
            MobileUseEquipmentQueued = false;
            MobileWeaponSlotQueued = -1;
            MobileLookDelta = Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        private void ReadKeyboardAndGamepad(ref Vector2 move, ref Vector2 look, ref bool fire, ref bool aim, ref bool sprint,
            ref bool reload, ref bool crouch, ref bool dodge, ref bool jump, ref bool pause, ref bool interact,
            ref bool useEquipment, ref bool weaponSwitch, ref int weaponSlot)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 keyboardMove = Vector2.zero;
                if (keyboard.aKey.isPressed) keyboardMove.x -= 1f;
                if (keyboard.dKey.isPressed) keyboardMove.x += 1f;
                if (keyboard.sKey.isPressed) keyboardMove.y -= 1f;
                if (keyboard.wKey.isPressed) keyboardMove.y += 1f;
                if (keyboardMove.sqrMagnitude > move.sqrMagnitude) move = keyboardMove;

                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) sprint = true;
                if (keyboard.rKey.wasPressedThisFrame) reload = true;
                if (keyboard.cKey.wasPressedThisFrame) crouch = true;
                if (keyboard.spaceKey.wasPressedThisFrame) { dodge = true; jump = true; }
                if (keyboard.eKey.isPressed) interact = true;
                if (keyboard.fKey.wasPressedThisFrame) useEquipment = true;
                if (keyboard.qKey.wasPressedThisFrame) { weaponSwitch = true; weaponSlot = -2; }
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) pause = true;
                if (keyboard.digit1Key.wasPressedThisFrame) weaponSlot = 0;
                if (keyboard.digit2Key.wasPressedThisFrame) weaponSlot = 1;
                if (keyboard.digit3Key.wasPressedThisFrame) weaponSlot = 2;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed) fire = true;
                if (mouse.rightButton.isPressed) aim = true;
                if (mouse.scroll.ReadValue().y < -0.01f) weaponSwitch = true;
                Vector2 delta = mouse.delta.ReadValue() * 0.06f;
                look += delta;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude) move = stick;
                if (gamepad.rightTrigger.isPressed) fire = true;
                if (gamepad.leftTrigger.isPressed) aim = true;
                if (gamepad.leftStickButton.isPressed) sprint = true;
                if (gamepad.buttonSouth.wasPressedThisFrame) { dodge = true; jump = true; }
                if (gamepad.buttonEast.wasPressedThisFrame) crouch = true;
                if (gamepad.buttonWest.wasPressedThisFrame) reload = true;
                if (gamepad.buttonNorth.isPressed) interact = true;
                if (gamepad.dpadRight.wasPressedThisFrame) weaponSwitch = true;
                if (gamepad.startButton.wasPressedThisFrame) pause = true;
                look += gamepad.rightStick.ReadValue() * 2.4f;
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private void ReadLegacy(ref Vector2 move, ref Vector2 look, ref bool fire, ref bool aim, ref bool sprint,
            ref bool reload, ref bool crouch, ref bool dodge, ref bool jump, ref bool pause, ref bool interact,
            ref bool useEquipment, ref bool weaponSwitch, ref int weaponSlot)
        {
            Vector2 keyboardMove = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keyboardMove.sqrMagnitude > move.sqrMagnitude) move = keyboardMove;

            look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2.2f;
            if (Input.GetMouseButton(0)) fire = true;
            if (Input.GetMouseButton(1)) aim = true;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) sprint = true;
            if (Input.GetKeyDown(KeyCode.R)) reload = true;
            if (Input.GetKeyDown(KeyCode.C)) crouch = true;
            if (Input.GetKeyDown(KeyCode.Space)) { dodge = true; jump = true; }
            if (Input.GetKey(KeyCode.E)) interact = true;
            if (Input.GetKeyDown(KeyCode.F)) useEquipment = true;
            if (Input.GetKeyDown(KeyCode.Q)) { weaponSwitch = true; weaponSlot = -2; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) pause = true;
            if (Input.GetKeyDown(KeyCode.Alpha1)) weaponSlot = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) weaponSlot = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) weaponSlot = 2;
            if (Input.mouseScrollDelta.y < -0.01f) weaponSwitch = true;
        }
#endif
    }
}
