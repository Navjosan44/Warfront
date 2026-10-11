using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Complete touch control layer.
    ///
    /// Everything the player needs in a firefight is on screen and every visible control is wired to
    /// the shared InputService, so gameplay code cannot tell (and does not care) whether the input came
    /// from a thumb, a keyboard or a gamepad. The layout is anchor based, mirrors for left handed
    /// players and hides itself on non-touch devices and while the game is paused.
    /// </summary>
    public class MobileControls
    {
        private GameContext _context;
        private UIRoot _ui;
        private RectTransform _root;
        private RectTransform _moveArea;
        private RectTransform _lookAreaRect;

        private TouchJoystick _joystick;
        private TouchLookArea _look;
        private Image _joystickHandle;

        private TouchButton _fire;
        private TouchButton _aim;
        private TouchButton _reload;
        private TouchButton _crouch;
        private TouchButton _dodge;
        private TouchButton _jump;
        private TouchButton _interact;
        private TouchButton _equip;
        private TouchButton _switchWeapon;
        private TouchButton _pause;

        private Text _weaponLabel;
        private Text _statusLabel;
        private bool _visible;
        private bool _built;
        private PlayerInteractor _interactor;

        public bool IsVisible { get { return _visible && _root != null && _root.gameObject.activeSelf; } }

        public void Initialize(GameContext context, UIRoot ui)
        {
            _context = context;
            _ui = ui;
            Build();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (_ui == null || _ui.OverlayLayer == null) return;
            _built = true;

            _root = UIFactory.CreateRect(_ui.OverlayLayer, "MobileControls");
            UIFactory.Stretch(_root, 0f, 0f, 0f, 0f);

            BuildLookArea();
            BuildMoveArea();
            BuildActionButtons();
            BuildStatusReadout();

            ApplyLayout();
            _root.gameObject.SetActive(false);
        }

        private Image CreateTouchSurface(string name, Color color)
        {
            Image image = UIFactory.CreateImage(_root, name, color, true);
            return image;
        }

        private void BuildLookArea()
        {
            Image area = CreateTouchSurface("LookArea", new Color(1f, 1f, 1f, 0.02f));
            _lookAreaRect = area.rectTransform;
            _look = area.gameObject.AddComponent<TouchLookArea>();
            _look.Configure(0.062f);
        }

        private void BuildMoveArea()
        {
            Image area = CreateTouchSurface("MoveArea", new Color(1f, 1f, 1f, 0.02f));
            _moveArea = area.rectTransform;

            Image background = UIFactory.CreateImage(_moveArea, "StickBase", new Color(1f, 1f, 1f, 0.10f), false);
            background.rectTransform.sizeDelta = new Vector2(250f, 250f);
            background.rectTransform.anchoredPosition = Vector2.zero;

            Image handle = UIFactory.CreateImage(background.transform, "StickHandle", new Color(0.72f, 0.82f, 0.6f, 0.55f), false);
            handle.rectTransform.sizeDelta = new Vector2(110f, 110f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            _joystickHandle = handle;

            _joystick = area.gameObject.AddComponent<TouchJoystick>();
            _joystick.Configure(_moveArea, background.rectTransform, handle.rectTransform, 120f);
            background.gameObject.SetActive(false);
        }

        private TouchButton CreateButton(string name, string label, Color color, int fontSize, float size)
        {
            return CreateButton(name, label, color, fontSize, new Vector2(size, size));
        }

        private TouchButton CreateButton(string name, string label, Color color, int fontSize, Vector2 size)
        {
            Image background = UIFactory.CreatePanel(_root, name, color);
            background.rectTransform.sizeDelta = size;

            Text text = UIFactory.CreateText(background.transform, "Label", label, fontSize,
                new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(text.rectTransform, 6f, 6f, 6f, 6f);

            TouchButton button = background.gameObject.AddComponent<TouchButton>();
            button.Configure(background, text, color);
            return button;
        }

        private void BuildActionButtons()
        {
            Color fire = new Color(0.72f, 0.24f, 0.20f, 0.72f);
            Color aim = new Color(0.24f, 0.36f, 0.5f, 0.66f);
            Color utility = new Color(0.16f, 0.17f, 0.17f, 0.62f);
            Color interact = new Color(0.62f, 0.55f, 0.2f, 0.7f);

            _fire = CreateButton("FireButton", "FIRE", fire, 40, 236f);
            _aim = CreateButton("AimButton", "AIM", aim, 34, 176f);
            _reload = CreateButton("ReloadButton", "RELOAD", utility, 26, 152f);
            _switchWeapon = CreateButton("SwitchButton", "SWAP", utility, 26, 132f);
            _crouch = CreateButton("CrouchButton", "CROUCH", utility, 24, 132f);
            _jump = CreateButton("JumpButton", "JUMP", utility, 24, 132f);
            _dodge = CreateButton("DodgeButton", "DODGE", utility, 24, 132f);
            _equip = CreateButton("EquipButton", "MED", new Color(0.5f, 0.24f, 0.24f, 0.7f), 24, 132f);
            _interact = CreateButton("InteractButton", "USE", interact, 26, 176f);
            _pause = CreateButton("PauseButton", "II", utility, 28, 96f);

            _reload.Pressed = () => { if (_context.Input != null) _context.Input.MobileReloadQueued = true; };
            _crouch.Pressed = () => { if (_context.Input != null) _context.Input.MobileCrouchQueued = true; };
            _jump.Pressed = () => { if (_context.Input != null) _context.Input.MobileJumpQueued = true; };
            _dodge.Pressed = () => { if (_context.Input != null) _context.Input.MobileDodgeQueued = true; };
            _equip.Pressed = () => { if (_context.Input != null) _context.Input.MobileUseEquipmentQueued = true; };
            _switchWeapon.Pressed = OnSwitchWeapon;
            _interact.Pressed = () => { if (_context.Input != null) _context.Input.MobileInteractHeld = true; };
            _interact.Released = () => { if (_context.Input != null) _context.Input.MobileInteractHeld = false; };
            _pause.Pressed = OnPausePressed;
        }

        private void BuildStatusReadout()
        {
            _weaponLabel = UIFactory.CreateText(_root, "WeaponReadout", string.Empty, 26,
                new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerRight);
            _weaponLabel.rectTransform.sizeDelta = new Vector2(420f, 40f);

            _statusLabel = UIFactory.CreateText(_root, "StatusReadout", string.Empty, 24,
                new Color(1f, 1f, 1f, 0.6f), TextAnchor.LowerLeft);
            _statusLabel.rectTransform.sizeDelta = new Vector2(520f, 40f);
        }

        // ----------------------------------------------------------------- layout

        private void ApplyLayout()
        {
            if (_root == null) return;

            bool leftHanded = _context != null && _context.Settings != null && _context.Settings.Data.LeftHandedControls;
            float scale = _context != null && _context.Settings != null
                ? Mathf.Clamp(_context.Settings.Data.JoystickScale, 0.7f, 1.4f)
                : 1f;

            // Look surface always occupies the half opposite the thumbstick.
            UIFactory.SetAnchor(_lookAreaRect,
                new Vector2(leftHanded ? 0f : 0.5f, 0f), new Vector2(leftHanded ? 0.5f : 1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _lookAreaRect.offsetMin = Vector2.zero;
            _lookAreaRect.offsetMax = Vector2.zero;

            UIFactory.SetAnchor(_moveArea,
                new Vector2(leftHanded ? 0.5f : 0f, 0f), new Vector2(leftHanded ? 1f : 0.5f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _moveArea.offsetMin = Vector2.zero;
            _moveArea.offsetMax = Vector2.zero;

            float side = leftHanded ? -1f : 1f;
            float anchorX = leftHanded ? 0f : 1f;
            Vector2 corner = new Vector2(anchorX, 0f);

            Place(_fire, corner, new Vector2(side * 34f, 34f), 236f * scale);
            Place(_aim, corner, new Vector2(side * 300f, 46f), 176f * scale);
            Place(_reload, corner, new Vector2(side * 64f, 300f), 152f * scale);
            Place(_switchWeapon, corner, new Vector2(side * 232f, 248f), 132f * scale);
            Place(_crouch, corner, new Vector2(side * 386f, 74f), 132f * scale);
            Place(_jump, corner, new Vector2(side * 386f, 222f), 132f * scale);
            Place(_dodge, corner, new Vector2(side * 392f, 370f), 132f * scale);
            Place(_equip, corner, new Vector2(side * 540f, 222f), 132f * scale);
            Place(_interact, corner, new Vector2(side * 556f, 74f), 176f * scale);

            UIFactory.SetAnchor(_pause.GetComponent<RectTransform>(), new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(anchorX, 1f), new Vector2(96f, 96f), new Vector2(side * -26f, -26f));

            UIFactory.SetAnchor(_weaponLabel.rectTransform, corner, corner, corner, _weaponLabel.rectTransform.sizeDelta,
                new Vector2(side * -30f, 296f));
            UIFactory.SetAnchor(_statusLabel.rectTransform, new Vector2(leftHanded ? 1f : 0f, 0f), new Vector2(leftHanded ? 1f : 0f, 0f),
                new Vector2(leftHanded ? 1f : 0f, 0f), _statusLabel.rectTransform.sizeDelta,
                new Vector2(leftHanded ? -30f : 30f, 40f));
        }

        private static void Place(TouchButton button, Vector2 corner, Vector2 position, float size)
        {
            if (button == null) return;
            RectTransform rect = button.GetComponent<RectTransform>();
            UIFactory.SetAnchor(rect, corner, corner, corner, new Vector2(size, size), position);
        }

        // ------------------------------------------------------------------ state

        public void Show(bool visible)
        {
            _visible = visible;
            if (!_built) return;
            if (_root != null) _root.gameObject.SetActive(visible);
            if (!visible)
            {
                ReleaseAll();
                if (_context != null && _context.Input != null)
                {
                    _context.Input.MobileMove = Vector2.zero;
                    _context.Input.MobileSprintHeld = false;
                    _context.Input.MobileFireHeld = false;
                    _context.Input.MobileAimHeld = false;
                    _context.Input.MobileInteractHeld = false;
                    _context.Input.MobileLookDelta = Vector2.zero;
                }
            }
        }

        /// <summary>Re-applies layout (called when settings change handedness or stick scale).</summary>
        public void RefreshLayout()
        {
            ApplyLayout();
        }

        private void ReleaseAll()
        {
            if (_joystick != null) _joystick.Release();
            if (_look != null) _look.Reset();
            Release(_fire);
            Release(_aim);
            Release(_reload);
            Release(_crouch);
            Release(_jump);
            Release(_dodge);
            Release(_equip);
            Release(_interact);
            Release(_switchWeapon);
        }

        private static void Release(TouchButton button)
        {
            if (button != null) button.Release();
        }

        // ------------------------------------------------------------------- tick

        /// <summary>Called before InputService.Tick so a touch in this frame is never a frame late.</summary>
        public void Tick(float deltaTime)
        {
            if (!_built) return;

            GameContext context = _context;
            if (context == null || context.Flow == null) return;

            bool shouldShow = _visible && context.Flow.State == GameState.Mission;
            bool paused = context.Flow.IsPaused;
            if (_root.gameObject.activeSelf != (shouldShow && !paused))
            {
                _root.gameObject.SetActive(shouldShow && !paused);
                if (paused || !shouldShow) ReleaseAll();
            }
            if (!shouldShow || paused) return;

            InputService input = context.Input;
            if (input == null) return;

            // ---- movement
            Vector2 move = _joystick != null ? _joystick.Value : Vector2.zero;
            input.MobileMove = move;
            input.MobileSprintHeld = move.y > 0.55f && move.magnitude > 0.9f;

            if (_joystickHandle != null)
            {
                _joystickHandle.color = input.MobileSprintHeld
                    ? new Color(0.95f, 0.72f, 0.35f, 0.7f)
                    : new Color(0.72f, 0.82f, 0.6f, 0.55f);
            }

            // ---- camera
            Vector2 look = _look != null ? _look.ConsumeDelta() : Vector2.zero;
            if (look.sqrMagnitude > 0f) input.MobileLookDelta += look;

            bool aimHeld = _aim != null && _aim.Held;
            if (_look != null && _look.ConsumeDoubleTap()) aimHeld = !aimHeld;

            input.MobileAimHeld = aimHeld;
            input.MobileFireHeld = (_fire != null && _fire.Held) ||
                                   (context.Settings != null && context.Settings.Data.AutoFire && aimHeld);

            // ---- context sensitive interact button
            UpdateInteractButton(context);
            UpdateReadouts(context);
        }

        private void UpdateInteractButton(GameContext context)
        {
            if (_interact == null) return;

            if (_interactor == null && context.Player != null)
            {
                _interactor = context.Player.GetComponent<PlayerInteractor>();
            }

            string prompt = _interactor != null ? _interactor.CurrentPrompt : null;
            bool available = !string.IsNullOrEmpty(prompt);
            if (_interact.gameObject.activeSelf != available)
            {
                _interact.SetVisible(available);
                if (!available) _interact.Release();
            }
            if (available) _interact.SetLabel(ShortPromptLabel(prompt));
        }

        private static string ShortPromptLabel(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return "USE";
            // Prompts arrive as "Pick up X" / "Open Y" - the verb is the useful part at button size.
            int space = prompt.IndexOf(' ');
            string verb = space > 0 ? prompt.Substring(0, space) : prompt;
            return verb.ToUpperInvariant();
        }

        private void UpdateReadouts(GameContext context)
        {
            PlayerController player = context.Player;
            if (player == null) return;

            if (_context.Platform != null && !_context.Platform.IsTouchDevice)
            {
                // Desktop editor: keep the readout quiet so the touch layer does not clutter a mouse build.
                if (_statusLabel != null) _statusLabel.text = string.Empty;
                return;
            }

            if (_weaponLabel != null)
            {
                WeaponModel weapon = player.Loadout != null ? player.Loadout.CurrentModel : null;
                if (weapon != null)
                {
                    WeaponDef def = weapon.Def;
                    int reserve = player.Loadout.ReserveAmmo(def.AmmoType);
                    _weaponLabel.text = def.DisplayName + "   " + weapon.AmmoInMagazine + " / " + reserve;
                }
                else
                {
                    _weaponLabel.text = string.Empty;
                }
            }

            if (_statusLabel != null)
            {
                MissionRuntime mission = context.Mission;
                int alive = mission != null ? mission.AliveEnemyCount : 0;
                int used = context.Inventory != null ? context.Inventory.UsedSlots : 0;
                int capacity = context.Inventory != null ? context.Inventory.Capacity : 0;
                _statusLabel.text = "HOSTILES " + alive + "   PACK " + used + "/" + capacity;
            }
        }

        private void OnSwitchWeapon()
        {
            if (_context == null || _context.Input == null) return;
            if (_context.Flow == null || _context.Flow.Player.Loadout == null) return;

            PlayerLoadout loadout = _context.Flow.Player.Loadout;
            int current = loadout.ActiveIndex;
            for (int step = 1; step <= PlayerLoadout.SlotCount; step++)
            {
                int candidate = WfMath.RepeatIndex((current < 0 ? 0 : current) + step, PlayerLoadout.SlotCount);
                WeaponSlot slot = loadout.GetSlot(candidate);
                if (slot == null || !slot.IsValid) continue;
                _context.Input.MobileWeaponSlotQueued = candidate;
                return;
            }
        }

        private void OnPausePressed()
        {
            if (_context == null || _context.Flow == null) return;
            _context.Flow.TogglePause();
        }
    }
}
