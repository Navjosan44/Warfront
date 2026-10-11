using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public enum GameState
    {
        Boot = 0,
        MainMenu = 1,
        MissionSelect = 2,
        Briefing = 3,
        Loadout = 4,
        Inventory = 5,
        Upgrades = 6,
        Settings = 7,
        Mission = 8,
        Results = 9
    }

    /// <summary>
    /// Drives the whole application flow: menus -> briefing -> loadout -> mission -> results -> menus.
    ///
    /// It owns mission lifetime (build, start, resolve, teardown), pause handling and the player
    /// instance, and is the only thing that talks to the UI root about which screen is visible. Every
    /// transition is explicit so there is no state where the game is neither in a menu nor in a mission.
    /// </summary>
    public class GameFlow
    {
        private readonly GameContext _context;
        private readonly Transform _worldRoot;
        private readonly Transform _missionRoot;

        private PlayerBuild _player;
        private bool _playerBuilt;
        private float _deathTimer;
        private bool _awaitingDeath;
        private int _attemptIndex;
        private float _missionTime;
        private float _resultsGuard;

        public GameFlow(GameContext context, Transform worldRoot, Transform missionRoot)
        {
            _context = context;
            _worldRoot = worldRoot;
            _missionRoot = missionRoot;
        }

        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPaused { get; private set; }
        public bool IsMissionActive { get { return State == GameState.Mission && _context.Mission != null; } }
        public string CurrentMissionId { get; private set; }
        public MissionDef CurrentMissionDef { get; private set; }
        public MissionSummary LastSummary { get; private set; }
        public RewardApplication LastRewards { get; private set; }
        public float MissionTime { get { return _missionTime; } }
        public PlayerBuild Player { get { return _player; } }

        // ------------------------------------------------------------------ menus

        public void EnterBoot()
        {
            State = GameState.Boot;
            if (_context.UI != null) _context.UI.HideAll();
        }

        public void EnterMainMenu()
        {
            TeardownMission();
            State = GameState.MainMenu;
            IsPaused = false;
            Time.timeScale = 1f;
            if (_context.Hud != null) _context.Hud.Show(false);
            if (_context.Mobile != null) _context.Mobile.Show(false);
            if (_context.Audio != null) _context.Audio.SetMusicState(true);
            if (_context.UI != null) _context.UI.ShowMainMenu();
        }

        public void ShowMissionSelect()
        {
            State = GameState.MissionSelect;
            if (_context.UI != null) _context.UI.ShowMissionSelect();
        }

        public void ShowBriefing(string missionId)
        {
            MissionDef def = _context.Content.GetMission(missionId);
            if (def == null) return;
            CurrentMissionDef = def;
            _context.Progression.SetSelectedMission(missionId);
            State = GameState.Briefing;
            if (_context.UI != null) _context.UI.ShowBriefing(def);
        }

        public void ShowLoadout()
        {
            State = GameState.Loadout;
            if (_context.UI != null) _context.UI.ShowLoadout();
        }

        public void ShowInventory()
        {
            State = GameState.Inventory;
            if (_context.UI != null) _context.UI.ShowInventory();
        }

        public void ShowUpgrades()
        {
            State = GameState.Upgrades;
            if (_context.UI != null) _context.UI.ShowUpgrades();
        }

        public void ShowSettings()
        {
            State = GameState.Settings;
            if (_context.UI != null) _context.UI.ShowSettings(false);
        }

        // ---------------------------------------------------------------- mission

        /// <summary>Builds and starts a mission. Returns false when the mission is locked or missing.</summary>
        public bool StartMission(string missionId)
        {
            MissionDef def = _context.Content.GetMission(missionId);
            if (def == null)
            {
                GameEvents.RaiseNotice("Mission unavailable");
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Deny);
                return false;
            }

            if (!_context.Progression.IsMissionUnlocked(missionId))
            {
                GameEvents.RaiseNotice("Mission locked - finish the previous contract");
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Deny);
                return false;
            }

            TeardownMission();

            CurrentMissionId = missionId;
            CurrentMissionDef = def;
            _attemptIndex++;
            int runId = RewardCalculator.MakeRunId(missionId, _attemptIndex, def.Seed);

            if (!EnsurePlayer()) return false;

            _context.Mission = new MissionRuntime(_context, _missionRoot);
            _context.Mission.Build(def, runId);

            PlayerFactory.PlaceAt(_context, _player, _context.Mission.PlayerSpawn, true);
            _player.Controller.SetInputEnabled(true);
            _player.Health.ResetForMission();
            _player.Loadout.BuildFromInventory(_context.Content, _context.Inventory, _context.Save.Data.Loadout);
            _player.Loadout.OnMissionStart();

            _missionTime = 0f;
            _awaitingDeath = false;
            _deathTimer = 0f;
            IsPaused = false;
            Time.timeScale = 1f;
            State = GameState.Mission;

            if (_context.UI != null) _context.UI.HideAll();
            if (_context.Hud != null) _context.Hud.Show(true);
            if (_context.Mobile != null) _context.Mobile.Show(_context.Platform.IsTouchDevice);
            if (_context.Audio != null) _context.Audio.SetMusicState(false);

            _context.Mission.Begin();
            if (_context.Tutorial != null) _context.Tutorial.Begin(def);

            // Snapshot the loadout so the next deploy remembers the choice.
            PersistLoadout();

            return true;
        }

        private void PersistLoadout()
        {
            if (_context.Save == null || _context.Save.Data == null || _player.Loadout == null) return;
            LoadoutSave save = _context.Save.Data.Loadout;
            WeaponSlot primary = _player.Loadout.GetSlot(0);
            WeaponSlot secondary = _player.Loadout.GetSlot(1);
            WeaponSlot sidearm = _player.Loadout.GetSlot(2);
            if (primary != null && primary.Def != null) save.PrimaryWeaponId = primary.Def.Id;
            if (secondary != null && secondary.Def != null) save.SecondaryWeaponId = secondary.Def.Id;
            if (sidearm != null && sidearm.Def != null) save.SidearmWeaponId = sidearm.Def.Id;
            _context.Save.MarkDirty();
        }

        private bool EnsurePlayer()
        {
            if (_playerBuilt && _player.Controller != null) return true;

            _player = PlayerFactory.Build(_context, _worldRoot);
            _playerBuilt = true;
            _context.Player = _player.Controller;
            _context.Camera = _player.Camera;
            return true;
        }

        public void RestartMission()
        {
            if (string.IsNullOrEmpty(CurrentMissionId)) { EnterMainMenu(); return; }
            string missionId = CurrentMissionId;
            StartMission(missionId);
        }

        public void AbortMission()
        {
            if (_context.Mission == null) { EnterMainMenu(); return; }
            _context.Mission.Fail(FailureReason.Abandoned);
        }

        // ----------------------------------------------------------------- pause

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (State != GameState.Mission) return;
            IsPaused = true;
            Time.timeScale = 0f;
            if (_context.UI != null) _context.UI.ShowPause();
            if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Click);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            if (_context.UI != null) _context.UI.HidePause();
        }

        public void OnApplicationPaused(bool paused)
        {
            if (paused)
            {
                if (State == GameState.Mission && !IsPaused) Pause();
                if (_context.Save != null) _context.Save.Flush();
            }
        }

        // -------------------------------------------------------------- resolution

        public void OnPlayerDied()
        {
            if (_awaitingDeath || _context.Mission == null) return;
            _awaitingDeath = true;
            _deathTimer = 3.2f;
        }

        public void OnMissionResolved(MissionModel model)
        {
            if (model == null || _context.Mission == null) return;
            if (_resultsGuard > 0f) return;
            _resultsGuard = 1f;

            MissionSummary summary = model.BuildSummary();
            LastSummary = summary;
            LastRewards = _context.Rewards.Bank(summary, model.Def, _context.Ledger);

            State = GameState.Results;
            Time.timeScale = 1f;
            IsPaused = false;
            _awaitingDeath = false;

            if (_context.Hud != null) _context.Hud.Show(false);
            if (_context.Mobile != null) _context.Mobile.Show(false);
            if (_context.Audio != null)
            {
                _context.Audio.PlayMissionStinger(summary.Success, false);
                _context.Audio.StopMissionBeds();
                _context.Audio.SetMusicState(true);
            }
            if (_context.UI != null) _context.UI.ShowResults(LastRewards, summary, model.Def);
            if (_context.Tutorial != null) _context.Tutorial.End();
        }

        public void TeardownMission()
        {
            if (_context.Mission != null)
            {
                _context.Mission.Teardown();
                _context.Mission = null;
            }
            _context.Mission = null;
            if (_player.Controller != null) _player.Controller.SetInputEnabled(false);
            CurrentMissionId = null;
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float deltaTime)
        {
            _resultsGuard = Mathf.Max(0f, _resultsGuard - deltaTime);

            if (_awaitingDeath)
            {
                _deathTimer -= deltaTime;
                if (_deathTimer <= 0f)
                {
                    _awaitingDeath = false;
                    if (_context.Mission != null) _context.Mission.Fail(FailureReason.PlayerKilled);
                }
                return;
            }

            if (State == GameState.Mission && _context.Mission != null)
            {
                _missionTime += deltaTime;
                _context.Mission.Tick(deltaTime);

                if (_context.Tutorial != null) _context.Tutorial.Tick(deltaTime);

                if (_context.Platform != null && _context.Platform.BackButtonPressed())
                {
                    Pause();
                }
                return;
            }

            if (_context.Platform != null && _context.Platform.BackButtonPressed())
            {
                HandleBackNavigation();
            }
        }

        /// <summary>Hardware back button behaviour outside a mission.</summary>
        public void HandleBackNavigation()
        {
            switch (State)
            {
                case GameState.MissionSelect:
                case GameState.Loadout:
                case GameState.Inventory:
                case GameState.Upgrades:
                case GameState.Settings:
                    if (State == GameState.Loadout && !string.IsNullOrEmpty(CurrentMissionId)) ShowBriefing(CurrentMissionId);
                    else EnterMainMenu();
                    break;
                case GameState.Briefing:
                    ShowMissionSelect();
                    break;
                default:
                    break;
            }
        }

        public void QuitGame()
        {
            if (_context.Save != null) _context.Save.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
