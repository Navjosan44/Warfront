using UnityEngine;
using Warfront.Core;
using Warfront.UI;

namespace Warfront.Game
{
    /// <summary>
    /// WARFRONT entry point.
    ///
    /// One scene, one bootstrap GameObject: every system is constructed here in a fixed order and handed
    /// to the rest of the game through GameContext. There are no singletons, no scene searches and no
    /// hidden dependencies - which is also why a mission can be torn down completely and rebuilt.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameBootstrap : MonoBehaviour
    {
        private GameContext _context;
        private Transform _worldRoot;
        private Transform _missionRoot;
        private Transform _menuRoot;
        private DevOverlay _devOverlay;
        private bool _built;

        public GameContext Context { get { return _context; } }

        private void Awake()
        {
            if (_built) return;
            Build();
        }

        private void Start()
        {
            if (_context == null) return;
            _context.Flow.EnterMainMenu();
        }

        private void Build()
        {
            _built = true;
            DontDestroyOnLoad(gameObject);

            _worldRoot = NewChild("World");
            _missionRoot = NewChild("Mission");
            _menuRoot = NewChild("Menu");

            _context = new GameContext();

            // 1. Asset + content layer.
            GameAssets assets = new GameAssets();
            assets.Initialize();
            _context.Assets = assets;

            ContentDatabase content = ContentDatabase.CreateDefault();
            ContentAssets.ApplyOverrides(content);
            _context.Content = content;

            // 2. Persistence and settings.
            SaveService save = new SaveService(Application.persistentDataPath);
            _context.Save = save;
            save.Load();

            SettingsService settings = new SettingsService(save);
            _context.Settings = settings;
            settings.Load();

            // 3. Platform + audio + pooling.
            AndroidPlatform platform = new AndroidPlatform();
            _context.Platform = platform;
            platform.Configure();

            bool firstRun = save.LastLoad.Status == SaveLoadStatus.CreatedDefault || save.LastLoad.Status == SaveLoadStatus.RecoveredFromCorruption;
            if (firstRun)
            {
                settings.Data.QualityPreset = PerformanceManager.RecommendQualityPreset();
                settings.Data.TargetFrameRate = platform.RecommendedFrameCap();
                settings.ApplyAll();
            }

            AudioService audio = new AudioService(settings, transform);
            _context.Audio = audio;

            Haptics haptics = new Haptics(settings);
            _context.Haptics = haptics;

            PoolRegistry pools = new PoolRegistry(_worldRoot);
            _context.Pools = pools;
            VfxFactory vfx = new VfxFactory(assets, pools, _worldRoot);
            _context.Vfx = vfx;
            Projectile.RegisterPool(pools, assets, _worldRoot);

            // 4. World presentation shared by menus and missions.
            Camera menuCamera = CreateCamera();

            // 5. Progression, rewards and AI management.
            ProgressionService progression = new ProgressionService(content, save);
            _context.Progression = progression;
            progression.Reload();

            _context.Inventory = new InventoryModel(content, save.Data.Inventory.Capacity);
            LoadInventoryFromSave();
            GrantStarterKit();

            _context.Ledger = new RewardLedger(save.Data.ClaimedRewardTokens, 512);
            _context.Rewards = new RewardService(_context);

            EnemyDirector director = new EnemyDirector(_context);
            _context.EnemyDirector = director;

            PerformanceManager performance = new PerformanceManager(settings);
            _context.Performance = performance;

            // 6. Input, UI and flow.
            InputService input = new InputService(settings);
            _context.Input = input;

            UIRoot ui = new UIRoot();
            ui.Initialize(_context, assets, menuCamera);
            _context.UI = ui;

            HudController hud = new HudController();
            hud.Initialize(_context, ui);
            _context.Hud = hud;

            MobileControls mobile = new MobileControls();
            mobile.Initialize(_context, ui);
            _context.Mobile = mobile;

            _context.Tutorial = new TutorialController(_context);
            _context.Flow = new GameFlow(_context, _worldRoot, _missionRoot);

            BuildMenuDiorama(assets);
            _devOverlay = new DevOverlay();
            _devOverlay.Initialize(_context, ui);
            _context.Dev = _devOverlay;

            Debug.Log("[Warfront] Boot complete. Quality=" + settings.QualityName +
                      " Target=" + settings.Data.TargetFrameRate + "fps Save=" + save.SavePath);
        }

        private Transform NewChild(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("WarfrontCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, 220f, -24f);
            cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = PlayerCamera.HipFov;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 700f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 1f);
            cameraObject.AddComponent<AudioListener>();

            PlayerCamera playerCamera = cameraObject.AddComponent<PlayerCamera>();
            _context.Camera = playerCamera;
            _context.MenuCamera = camera;

            // Key light: a single directional light keeps mobile lighting cheap and readable.
            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(46f, 38f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.7f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.34f, 0.33f, 0.31f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 320f;

            return camera;
        }

        /// <summary>
        /// A small diorama parked far above the map so the menus have a real 3D backdrop while the
        /// mission camera is elsewhere. Costs nothing at runtime (a handful of boxes).
        /// </summary>
        private void BuildMenuDiorama(GameAssets assets)
        {
            if (assets == null) return;
            float baseY = 200f;

            PrimitiveFactory.CreateBox("DioramaGround", _menuRoot, new Vector3(0f, baseY - 0.5f, 0f),
                new Vector3(60f, 1f, 60f), assets.Ground, false);
            PrimitiveFactory.CreateBox("DioramaPad", _menuRoot, new Vector3(0f, baseY + 0.02f, 0f),
                new Vector3(24f, 0.08f, 24f), assets.Asphalt, false);

            for (int i = 0; i < 7; i++)
            {
                float angle = i * 0.9f;
                float radius = 8f + (i % 3) * 2.4f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, baseY + 0.75f, Mathf.Sin(angle) * radius);
                PrimitiveFactory.CreateBox("DioramaCrate", _menuRoot, position, new Vector3(1.4f, 1.5f, 1.4f),
                    assets.Wood, false, Quaternion.Euler(0f, i * 21f, 0f), GameLayers.Default);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 1.7f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 16f, baseY, Mathf.Sin(angle) * 16f);
                PrimitiveFactory.CreateMesh("DioramaRock", _menuRoot, MeshFactory.Rock(500 + i), position,
                    Vector3.one * 3.2f, assets.Rust, ColliderKind.None, GameLayers.Default);
            }

            PrimitiveFactory.CreateMesh("DioramaBeacon", _menuRoot, MeshFactory.Cylinder(10), new Vector3(0f, baseY, 0f),
                new Vector3(0.6f, 9f, 0.6f), assets.Metal, ColliderKind.None, GameLayers.Default);
            PrimitiveFactory.CreateBox("DioramaBeaconGlow", _menuRoot, new Vector3(0f, baseY + 9.2f, 0f),
                new Vector3(2f, 0.8f, 2f), assets.Beacon, false, Quaternion.identity, GameLayers.Default);

            PrimitiveFactory.SetLayerRecursive(_menuRoot.gameObject, GameLayers.Default);
            _menuRoot.gameObject.SetActive(true);
        }

        private void LoadInventoryFromSave()
        {
            InventorySave block = _context.Save.Data.Inventory;
            if (block == null || _context.Inventory == null) return;

            _context.Inventory.SetCapacity(block.Capacity, true);
            _context.Inventory.SetAmmoReserve(AmmoType.Light, block.LightAmmo);
            _context.Inventory.SetAmmoReserve(AmmoType.Heavy, block.HeavyAmmo);
            _context.Inventory.SetAmmoReserve(AmmoType.Shell, block.ShellAmmo);
            _context.Inventory.SetAmmoReserve(AmmoType.Sniper, block.SniperAmmo);

            var slots = new System.Collections.Generic.List<ItemStack>(block.Slots.Count);
            for (int i = 0; i < block.Slots.Count; i++)
            {
                ItemStackSave entry = block.Slots[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0) continue;
                slots.Add(new ItemStack(entry.ItemId, entry.Quantity, (Rarity)entry.Rarity, entry.UpgradeLevel));
            }
            _context.Inventory.LoadSlots(slots);

            for (int i = 0; i < block.Weapons.Count; i++)
            {
                ItemStackSave entry = block.Weapons[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;
                int leftover;
                _context.Inventory.Add(entry.ItemId, 1, (Rarity)entry.Rarity, entry.UpgradeLevel, out leftover);
            }
        }

        /// <summary>First run: hand the player a usable kit so the tutorial is immediately playable.</summary>
        private void GrantStarterKit()
        {
            if (_context.Save.Data.Progression.TotalXp > 0) return;
            if (_context.Inventory.Weapons.Count > 0) return;

            int leftover;
            _context.Inventory.Add("item_wpn_vk7", 1, Rarity.Common, 1, out leftover);
            _context.Inventory.Add("item_wpn_sidewinder", 1, Rarity.Common, 1, out leftover);
            _context.Inventory.Add("ammo_light", 4, Rarity.Common, 1, out leftover);
            _context.Inventory.Add("med_field_bandage", 2, Rarity.Common, 1, out leftover);
            _context.Inventory.Add("plate_ceramic", 1, Rarity.Common, 1, out leftover);
            PersistInventory();
        }

        private void PersistInventory()
        {
            if (_context.Save == null || _context.Inventory == null) return;
            InventorySave block = _context.Save.Data.Inventory;
            block.Capacity = _context.Inventory.Capacity;
            block.LightAmmo = _context.Inventory.AmmoReserve(AmmoType.Light);
            block.HeavyAmmo = _context.Inventory.AmmoReserve(AmmoType.Heavy);
            block.ShellAmmo = _context.Inventory.AmmoReserve(AmmoType.Shell);
            block.SniperAmmo = _context.Inventory.AmmoReserve(AmmoType.Sniper);

            block.Slots.Clear();
            System.Collections.Generic.List<ItemStack> slots = _context.Inventory.SnapshotSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsValid) continue;
                block.Slots.Add(new ItemStackSave
                {
                    ItemId = slots[i].ItemId,
                    Quantity = slots[i].Quantity,
                    Rarity = (int)slots[i].Rarity,
                    UpgradeLevel = slots[i].UpgradeLevel
                });
            }

            block.Weapons.Clear();
            for (int i = 0; i < _context.Inventory.Weapons.Count; i++)
            {
                ItemStack weapon = _context.Inventory.Weapons[i];
                block.Weapons.Add(new ItemStackSave
                {
                    ItemId = weapon.ItemId,
                    Quantity = 1,
                    Rarity = (int)weapon.Rarity,
                    UpgradeLevel = weapon.UpgradeLevel
                });
            }

            _context.Save.MarkDirty();
        }

        private void Update()
        {
            if (_context == null) return;

            float unscaled = Time.unscaledDeltaTime;
            float scaled = Time.deltaTime;

            // Touch input is sampled before the input service so a tap is never a frame late.
            if (_context.Mobile != null) _context.Mobile.Tick(scaled);
            _context.Input.Tick(scaled);
            _context.Flow.Tick(scaled);
            _context.Performance.Tick(unscaled);
            _context.Save.Tick(unscaled);
            if (_context.UI != null) _context.UI.Tick(unscaled);
            if (_context.Hud != null) _context.Hud.Tick(scaled);

            // Haptics back off automatically when the device is thermally throttled.
            if (_context.Haptics != null) _context.Haptics.SetIntensityScale(_context.Performance.Throttled ? 0.35f : 1f);
            if (_devOverlay != null) _devOverlay.Tick(unscaled);

            // Keep the saved inventory fresh whenever the player leaves a mission with new loot.
            if (_context.Flow.State == GameState.MainMenu) PersistInventory();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_context == null) return;
            _context.Flow.OnApplicationPaused(paused);
            if (!paused) _context.Settings.ApplyAll();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_context == null) return;
            if (!hasFocus)
            {
                _context.Flow.OnApplicationPaused(true);
            }
        }

        private void OnApplicationQuit()
        {
            if (_context == null) return;
            PersistInventory();
            _context.Save.Flush();
        }

        private void OnDestroy()
        {
            if (_context == null) return;
            GameEvents.Clear();
            if (_context.Audio != null) _context.Audio.Dispose();
        }
    }
}
