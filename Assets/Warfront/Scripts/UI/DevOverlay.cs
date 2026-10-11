using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Warfront.UI
{
    /// <summary>
    /// Developer diagnostics overlay (toggle with F3 in the editor, or a three finger tap on device).
    ///
    /// Everything it prints is measured at runtime by PerformanceManager, the AI director and the save
    /// service - nothing is estimated - so the numbers can be trusted when profiling a device build.
    /// It is disabled by default and costs a single boolean check per frame when hidden.
    /// </summary>
    public class DevOverlay
    {
        private GameContext _context;
        private UIRoot _ui;
        private UITheme _theme;

        private RectTransform _root;
        private Text _readout;
        private bool _built;
        private bool _visible;
        private float _fpsAccumulator;
        private int _fpsFrames;
        private float _smoothedFps;
        private float _peakFrameMs;
        private readonly StringBuilder _builder = new StringBuilder(1400);

        public bool IsVisible { get { return _visible; } }

        public void Initialize(GameContext context, UIRoot ui)
        {
            _context = context;
            _ui = ui;
            if (ui == null || ui.OverlayLayer == null) return;

            _theme = ui.Theme;

            Image panel = UIFactory.CreatePanel(ui.OverlayLayer, "DevOverlay", new Color(0.02f, 0.03f, 0.03f, 0.72f));
            _root = panel.rectTransform;
            UIFactory.SetAnchor(_root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(430f, 330f), new Vector2(16f, 16f));

            _readout = UIFactory.CreateText(_root, "Readout", string.Empty, 18,
                new Color(0.72f, 0.95f, 0.68f, 1f), TextAnchor.UpperLeft);
            UIFactory.Stretch(_readout.rectTransform, 12f, 12f, 10f, 10f);
            _readout.lineSpacing = 1.05f;

            _root.gameObject.SetActive(false);
            _built = true;
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_root != null) _root.gameObject.SetActive(_visible);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_built || _context == null) return;

            ReadToggle();

            _fpsAccumulator += unscaledDeltaTime;
            _fpsFrames++;
            float frameMs = unscaledDeltaTime * 1000f;
            if (frameMs > _peakFrameMs) _peakFrameMs = frameMs;

            // Roll the smoothed counter once a second so the number is readable rather than jittering.
            if (_fpsAccumulator >= 1f)
            {
                _smoothedFps = _fpsFrames / _fpsAccumulator;
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
            }

            if (!_visible || _readout == null) return;
            if (_context.Performance == null) return;

            PerformanceManager performance = _context.Performance;
            _builder.Length = 0;
            Compose(performance, _builder);
            _readout.text = _builder.ToString();
            _peakFrameMs = Mathf.Max(_peakFrameMs - unscaledDeltaTime * 40f, frameMs);
        }

        /// <summary>Writes the diagnostic block into a reused builder so the overlay allocates nothing.</summary>
        private void Compose(PerformanceManager performance, StringBuilder sb)
        {
            AndroidPlatform platform = _context.Platform;
            PlayerController player = _context.Player;
            MissionRuntime mission = _context.Mission;
            SaveService save = _context.Save;

            sb.Append("WARFRONT DEV\n");
            sb.Append("fps      ").Append(_smoothedFps.ToString("0.0")).Append("  (avg ").Append(performance.AverageFps.ToString("0.0")).Append(")\n");
            sb.Append("frame    ").Append(performance.AverageFrameMs.ToString("0.00")).Append(" ms   peak ").Append(performance.WorstFrameMs.ToString("0.0")).Append(" ms\n");
            sb.Append("session  ").Append(performance.SessionAverageFps.ToString("0.0")).Append(" fps avg over ")
              .Append(performance.SessionSeconds.ToString("0")).Append("s (").Append(performance.SessionFrames).Append(" frames)\n");
            sb.Append("render   scale ").Append(performance.RenderScale.ToString("0.00")).Append("   downgrades ").Append(performance.DowngradeCount)
              .Append(performance.Throttled ? "  THROTTLED" : string.Empty).Append('\n');
            sb.Append("quality  ").Append(_context.Settings != null ? _context.Settings.QualityName : "?").Append("   cap ")
              .Append(Application.targetFrameRate).Append(" fps\n");
            sb.Append("memory   alloc ").Append(performance.AllocatedMemoryBytes / (1024 * 1024)).Append(" MB   reserved ")
              .Append(performance.ReservedMemoryBytes / (1024 * 1024)).Append(" MB\n");
            sb.Append("gpu      ").Append(SystemInfo.graphicsDeviceType).Append("   ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
            sb.Append("device   ").Append(platform != null ? platform.Describe() : "n/a").Append('\n');
            sb.Append("--- gameplay ---\n");
            sb.Append("flow     ").Append(_context.Flow != null ? _context.Flow.State.ToString() : "n/a")
              .Append(_context.Flow != null && _context.Flow.IsPaused ? " (PAUSED)" : string.Empty).Append('\n');

            if (mission != null && mission.Model != null)
            {
                MissionModel model = mission.Model;
                sb.Append("mission  ").Append(model.Def.DisplayName).Append("  run ").Append(model.RunId).Append("  ").Append(model.State.ToString()).Append('\n');
                sb.Append("clock    ").Append(model.ElapsedSeconds.ToString("0.0")).Append("s   objectives ")
                  .Append(model.CompletedObjectiveCount).Append('/').Append(model.Objectives.Count).Append('\n');
                sb.Append("kills    ").Append(model.Kills).Append("  headshots ").Append(model.Headshots).Append("  loot ").Append(model.LootPickedUp).Append('\n');
                sb.Append("extract  ").Append(model.Extraction.State.ToString()).Append("  ").Append(model.Extraction.Progress01.ToString("0.00")).Append('\n');
                sb.Append("enemies  alive ").Append(mission.AliveEnemyCount).Append("  budget ").Append(model.Def.EnemyBudget)
                  .Append("  reinforcement ").Append(model.Def.EnemyReinforceInterval.ToString("0")).Append("s\n");
            }

            if (player != null)
            {
                Vector3 position = player.transform.position;
                sb.Append("player   ").Append(position.x.ToString("0.0")).Append(", ").Append(position.y.ToString("0.0")).Append(", ").Append(position.z.ToString("0.0"))
                  .Append("  speed ").Append(player.Speed.ToString("0.00")).Append('\n');
            }

            if (_context.Inventory != null)
            {
                sb.Append("inventory ").Append(_context.Inventory.UsedSlots).Append('/').Append(_context.Inventory.Capacity).Append(" slots   weapons ")
                  .Append(_context.Inventory.Weapons.Count).Append('\n');
            }

            if (_context.Progression != null && _context.Progression.Model != null)
            {
                ProgressionModel model = _context.Progression.Model;
                sb.Append("profile  level ").Append(model.Level).Append("  xp ").Append(model.Xp).Append("  credits ").Append(model.Credits)
                  .Append("  materials ").Append(model.Materials).Append('\n');
            }

            if (save != null)
            {
                sb.Append("save     ").Append(save.FileSizeBytes / 1024).Append(" KB  saves ").Append(save.SaveCount)
                  .Append("  dirty ").Append(save.SecondsSinceSave < 0.5f ? "no" : "pending").Append('\n');
                sb.Append("path     ").Append(save.SavePath).Append('\n');
            }

            sb.Append("f3 toggle overlay");
        }

        private void ReadToggle()
        {
            // Mouse and keyboard only: on a touch build the overlay stays off unless toggled in code.
            if (_context != null && _context.Platform != null && _context.Platform.IsTouchDevice) return;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) Toggle();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F3)) Toggle();
#endif
        }
    }
}
