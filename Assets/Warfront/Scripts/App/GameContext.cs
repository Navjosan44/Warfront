using UnityEngine;
using Warfront.Core;
using Warfront.UI;

namespace Warfront.Game
{
    /// <summary>
    /// Service container. Everything the game needs is created once by GameBootstrap and handed to
    /// systems through this object - there is deliberately no static Instance anywhere, which makes
    /// lifetime obvious and keeps the code testable and teardown-safe.
    /// </summary>
    public class GameContext
    {
        public ContentDatabase Content { get; set; }
        public GameAssets Assets { get; set; }
        public VfxFactory Vfx { get; set; }
        public Haptics Haptics { get; set; }
        public PlayerCamera Camera { get; set; }

        /// <summary>The single scene camera. Owned by the bootstrap, driven by PlayerCamera.</summary>
        public Camera MenuCamera { get; set; }
        public DevOverlay Dev { get; set; }
        public SaveService Save { get; set; }
        public SettingsService Settings { get; set; }
        public ProgressionService Progression { get; set; }
        public AudioService Audio { get; set; }
        public PoolRegistry Pools { get; set; }
        public PerformanceManager Performance { get; set; }
        public RewardService Rewards { get; set; }
        public UIRoot UI { get; set; }
        public HudController Hud { get; set; }
        public MobileControls Mobile { get; set; }
        public InputService Input { get; set; }
        public GameFlow Flow { get; set; }
        public MissionRuntime Mission { get; set; }
        public PlayerController Player { get; set; }
        public TutorialController Tutorial { get; set; }
        public EnemyDirector EnemyDirector { get; set; }
        public AndroidPlatform Platform { get; set; }
        public InventoryModel Inventory { get; set; }
        public RewardLedger Ledger { get; set; }

        public bool HasPlayer { get { return Player != null; } }
        public bool IsMissionRunning { get { return Mission != null && Mission.IsRunning; } }

        /// <summary>Starts a fresh campaign profile (used by the settings screen "reset progress").</summary>
        public void ResetProfile()
        {
            Save.ResetToDefault();
            Progression.Reload();
            Inventory = new InventoryModel(Content, Save.Data.Inventory.Capacity);
            Ledger = new RewardLedger(Save.Data.ClaimedRewardTokens, 512);
        }
    }
}
