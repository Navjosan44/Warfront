using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    public enum ScreenId
    {
        None = 0,
        MainMenu = 1,
        MissionSelect = 2,
        Briefing = 3,
        Loadout = 4,
        Inventory = 5,
        Upgrades = 6,
        Settings = 7,
        Pause = 8,
        Results = 9
    }

    /// <summary>
    /// Owns the UI canvas and every screen.
    ///
    /// The canvas is a screen-space overlay scaled from a 1920x1080 reference, and the whole screen
    /// layer is inset to Screen.safeArea so notches, punch holes and rounded corners never cover a
    /// button. Screens are built lazily on first use and then simply toggled.
    /// </summary>
    public class UIRoot
    {
        private GameContext _context;
        private UITheme _theme;
        private Canvas _canvas;
        private RectTransform _safeArea;
        private RectTransform _screenLayer;
        private RectTransform _overlayLayer;

        private MainMenuScreen _mainMenu;
        private MissionSelectScreen _missionSelect;
        private BriefingScreen _briefing;
        private LoadoutScreen _loadout;
        private InventoryScreen _inventory;
        private UpgradesScreen _upgrades;
        private SettingsScreen _settings;
        private PauseScreen _pause;
        private ResultsScreen _results;

        public UITheme Theme { get { return _theme; } }
        public RectTransform OverlayLayer { get { return _overlayLayer; } }
        public RectTransform ScreenLayer { get { return _screenLayer; } }
        public Canvas Canvas { get { return _canvas; } }
        public ScreenId Current { get; private set; }
        public GameContext Context { get { return _context; } }

        public void Initialize(GameContext context, GameAssets assets, Camera uiCamera)
        {
            _context = context;
            _theme = UITheme.Create(assets);
            UIFactory.UITheme = _theme;

            GameObject canvasObject = new GameObject("WarfrontUI");
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UIFactory.ReferenceWidth, UIFactory.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasObject);

            // A single full screen backdrop keeps the 3D diorama visible behind translucent panels.
            _safeArea = UIFactory.CreateRect(canvasObject.transform, "SafeArea");
            UIFactory.Stretch(_safeArea, 0f, 0f, 0f, 0f);

            _screenLayer = UIFactory.CreateRect(_safeArea, "Screens");
            UIFactory.Stretch(_screenLayer, 0f, 0f, 0f, 0f);

            _overlayLayer = UIFactory.CreateRect(_safeArea, "Overlays");
            UIFactory.Stretch(_overlayLayer, 0f, 0f, 0f, 0f);

            EnsureEventSystem();
            ApplySafeArea();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();

            // The module must match the active input backend, otherwise touch/pointer events never
            // reach the UI and the on-screen controls would be purely decorative.
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
            Object.DontDestroyOnLoad(eventSystem);
        }

        /// <summary>Keeps the UI inside the device safe area (notch, gesture bar, rounded corners).</summary>
        public void ApplySafeArea()
        {
            if (_safeArea == null) return;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f)
            {
                UIFactory.Stretch(_safeArea, 0f, 0f, 0f, 0f);
                return;
            }

            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;
            anchorMin.x /= Mathf.Max(1f, Screen.width);
            anchorMin.y /= Mathf.Max(1f, Screen.height);
            anchorMax.x /= Mathf.Max(1f, Screen.width);
            anchorMax.y /= Mathf.Max(1f, Screen.height);

            _safeArea.anchorMin = anchorMin;
            _safeArea.anchorMax = anchorMax;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ screens

        public void ShowMainMenu()
        {
            Ensure(ref _mainMenu, ScreenId.MainMenu);
            Show(ScreenId.MainMenu);
        }

        public void ShowMissionSelect()
        {
            Ensure(ref _missionSelect, ScreenId.MissionSelect);
            Show(ScreenId.MissionSelect);
        }

        public void ShowBriefing(MissionDef def)
        {
            Ensure(ref _briefing, ScreenId.Briefing);
            if (_briefing != null) _briefing.SetMission(def);
            Show(ScreenId.Briefing);
        }

        public void ShowLoadout()
        {
            Ensure(ref _loadout, ScreenId.Loadout);
            Show(ScreenId.Loadout);
        }

        public void ShowInventory()
        {
            Ensure(ref _inventory, ScreenId.Inventory);
            Show(ScreenId.Inventory);
        }

        public void ShowUpgrades()
        {
            Ensure(ref _upgrades, ScreenId.Upgrades);
            Show(ScreenId.Upgrades);
        }

        public void ShowSettings(bool fromPause)
        {
            Ensure(ref _settings, ScreenId.Settings);
            if (_settings != null) _settings.SetOrigin(fromPause);
            Show(ScreenId.Settings);
        }

        public void ShowPause()
        {
            Ensure(ref _pause, ScreenId.Pause);
            Show(ScreenId.Pause);
        }

        public void ShowResults(RewardApplication rewards, MissionSummary summary, MissionDef def)
        {
            Ensure(ref _results, ScreenId.Results);
            if (_results != null) _results.SetResult(rewards, summary, def);
            Show(ScreenId.Results);
        }

        public void HidePause()
        {
            if (_pause != null) _pause.Hide();
            if (Current == ScreenId.Pause) Current = ScreenId.None;
        }

        /// <summary>Result of the last screen visibility change - used by the bootstrap.</summary>
        public bool AnyScreenVisible { get { return Current != ScreenId.None; } }

        private void Ensure<T>(ref T screen, ScreenId id) where T : UIScreen, new()
        {
            if (screen != null) return;
            screen = new T();
            screen.Build(_context, this, _theme, _screenLayer);
        }

        public void Show(ScreenId id)
        {
            HideAll();
            Current = id;
            switch (id)
            {
                case ScreenId.MainMenu: if (_mainMenu != null) _mainMenu.Show(); break;
                case ScreenId.MissionSelect: if (_missionSelect != null) _missionSelect.Show(); break;
                case ScreenId.Briefing: if (_briefing != null) _briefing.Show(); break;
                case ScreenId.Loadout: if (_loadout != null) _loadout.Show(); break;
                case ScreenId.Inventory: if (_inventory != null) _inventory.Show(); break;
                case ScreenId.Upgrades: if (_upgrades != null) _upgrades.Show(); break;
                case ScreenId.Settings: if (_settings != null) _settings.Show(); break;
                case ScreenId.Pause: if (_pause != null) _pause.Show(); break;
                case ScreenId.Results: if (_results != null) _results.Show(); break;
                default: break;
            }
        }

        public void HideAll()
        {
            if (_mainMenu != null) _mainMenu.Hide();
            if (_missionSelect != null) _missionSelect.Hide();
            if (_briefing != null) _briefing.Hide();
            if (_loadout != null) _loadout.Hide();
            if (_inventory != null) _inventory.Hide();
            if (_upgrades != null) _upgrades.Hide();
            if (_settings != null) _settings.Hide();
            if (_pause != null) _pause.Hide();
            if (_results != null) _results.Hide();
            Current = ScreenId.None;
        }

        /// <summary>Called every frame by the bootstrap so screens can refresh live data.</summary>
        public void Tick(float deltaTime)
        {
            if (_canvas == null) return;
            _canvas.enabled = true;

            switch (Current)
            {
                case ScreenId.MainMenu: if (_mainMenu != null) _mainMenu.Tick(deltaTime); break;
                case ScreenId.MissionSelect: if (_missionSelect != null) _missionSelect.Tick(deltaTime); break;
                case ScreenId.Briefing: if (_briefing != null) _briefing.Tick(deltaTime); break;
                case ScreenId.Loadout: if (_loadout != null) _loadout.Tick(deltaTime); break;
                case ScreenId.Inventory: if (_inventory != null) _inventory.Tick(deltaTime); break;
                case ScreenId.Upgrades: if (_upgrades != null) _upgrades.Tick(deltaTime); break;
                case ScreenId.Settings: if (_settings != null) _settings.Tick(deltaTime); break;
                case ScreenId.Results: if (_results != null) _results.Tick(deltaTime); break;
                default: break;
            }
        }
    }
}
