using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Pause menu. Reached from the hardware back button, the on-screen II button or ESC.
    ///
    /// Time is frozen by GameFlow (timeScale 0) while this is up, which also parks every AI, projectile
    /// and weapon - so the pause is a real pause and not a UI overlay on a live simulation.
    /// </summary>
    public class PauseScreen : UIScreen
    {
        public override string Title { get { return "Paused"; } }
        protected override string BackLabel { get { return "Resume"; } }

        private Text _missionName;
        private Text _statusText;
        private Text _objectiveText;
        private Button _restartButton;
        private Button _abortButton;

        protected override void OnBuild()
        {
            Image panel = UIFactory.CreatePanel(Content, "PausePanel", Theme.Panel);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(920f, 700f);
            panelRect.anchoredPosition = Vector2.zero;

            Text header = UIFactory.CreateText(panel.transform, "Header", "MISSION PAUSED", 30, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(header.rectTransform, 30f, 20f, 500f, 34f);

            _missionName = UIFactory.CreateText(panel.transform, "MissionName", string.Empty, 44, Theme.Primary, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_missionName.rectTransform, 30f, 58f, 860f, 54f);

            _statusText = UIFactory.CreateText(panel.transform, "Status", string.Empty, 25, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_statusText.rectTransform, 30f, 120f, 860f, 130f);

            _objectiveText = UIFactory.CreateText(panel.transform, "Objectives", string.Empty, 23, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_objectiveText.rectTransform, 30f, 258f, 860f, 170f);

            Button resume = UIFactory.CreateButton(panel.transform, "Resume", "RESUME", Theme.PrimaryDim, OnResume, 32);
            UIFactory.SetAnchor(resume.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 68f), new Vector2(0f, 426f));
            resume.GetComponent<RectTransform>().offsetMin = new Vector2(30f, 426f);
            resume.GetComponent<RectTransform>().offsetMax = new Vector2(-30f, 494f);

            Button settings = UIFactory.CreateButton(panel.transform, "Settings", "SETTINGS", Theme.PanelLight, OnSettings, 28);
            UIFactory.SetAnchor(settings.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 64f), new Vector2(0f, 348f));
            settings.GetComponent<RectTransform>().offsetMin = new Vector2(30f, 348f);
            settings.GetComponent<RectTransform>().offsetMax = new Vector2(-16f, 412f);

            _restartButton = UIFactory.CreateButton(panel.transform, "Restart", "RESTART", Theme.PanelLight, OnRestart, 28);
            UIFactory.SetAnchor(_restartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 64f), new Vector2(0f, 348f));
            _restartButton.GetComponent<RectTransform>().offsetMin = new Vector2(16f, 348f);
            _restartButton.GetComponent<RectTransform>().offsetMax = new Vector2(-30f, 412f);

            Button quit = UIFactory.CreateButton(panel.transform, "Quit", "ABANDON MISSION", new Color(0.34f, 0.16f, 0.15f, 0.97f), OnAbandon, 28);
            UIFactory.SetAnchor(quit.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 64f), new Vector2(0f, 270f));
            quit.GetComponent<RectTransform>().offsetMin = new Vector2(30f, 270f);
            quit.GetComponent<RectTransform>().offsetMax = new Vector2(-16f, 334f);

            _abortButton = UIFactory.CreateButton(panel.transform, "Base", "RETURN TO BASE", Theme.PanelLight, OnReturnToBase, 28);
            UIFactory.SetAnchor(_abortButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 64f), new Vector2(0f, 270f));
            _abortButton.GetComponent<RectTransform>().offsetMin = new Vector2(16f, 270f);
            _abortButton.GetComponent<RectTransform>().offsetMax = new Vector2(-30f, 334f);

            Text note = UIFactory.CreateText(panel.transform, "Note",
                "Abandoning forfeits the contract and any loot still on the field. Progress already banked is kept.",
                20, Theme.Warning, TextAnchor.LowerCenter);
            UIFactory.SetAnchor(note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 44f), Vector2.zero);
            note.rectTransform.offsetMin = new Vector2(30f, 20f);
            note.rectTransform.offsetMax = new Vector2(-30f, 64f);
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Flow == null) return;

            MissionDef def = Context.Flow.CurrentMissionDef;
            _missionName.text = def != null ? def.DisplayName.ToUpperInvariant() : "NO ACTIVE CONTRACT";

            MissionRuntime mission = Context.Mission;
            if (mission == null || mission.Model == null)
            {
                _statusText.text = "No mission in progress.";
                _objectiveText.text = string.Empty;
                _restartButton.interactable = false;
                _abortButton.interactable = false;
                return;
            }

            MissionModel model = mission.Model;
            int minutes = Mathf.FloorToInt(model.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(model.ElapsedSeconds % 60f);
            float remaining = model.TimeRemaining;

            _statusText.text =
                "REGION          " + (def != null ? def.RegionName : "-") + "\n" +
                "ELAPSED         " + minutes.ToString("00") + ":" + seconds.ToString("00") +
                (remaining > 0f ? "   (limit in " + Mathf.CeilToInt(remaining) + "s)" : string.Empty) + "\n" +
                "CONFIRMED KILLS " + model.Kills + "    HEADSHOTS " + model.Headshots + "\n" +
                "HOSTILES ACTIVE " + mission.AliveEnemyCount + " / " + (def != null ? def.EnemyBudget : 0) +
                "    LOOT HELD " + model.LootPickedUp + "\n" +
                "EXTRACTION      " + model.Extraction.State;

            string objectives = "OBJECTIVES\n";
            for (int i = 0; i < model.Objectives.Count; i++)
            {
                ObjectiveProgress progress = model.Objectives[i];
                objectives += (progress.Completed ? "[x] " : progress.Required ? "[ ] " : "[-] ") +
                              progress.Def.Title + "   " + progress.ProgressLabel + "\n";
            }
            _objectiveText.text = objectives;

            _restartButton.interactable = !string.IsNullOrEmpty(Context.Flow.CurrentMissionId);
            _abortButton.interactable = mission != null;
        }

        private void OnResume()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Confirm);
            Context.Flow.Resume();
        }

        private void OnSettings()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowSettings();
        }

        private void OnRestart()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Confirm);
            Context.Flow.Resume();
            Context.Flow.RestartMission();
        }

        private void OnAbandon()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Deny);
            Context.Flow.AbortMission();
        }

        private void OnReturnToBase()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            Context.Flow.Resume();
            Context.Flow.EnterMainMenu();
        }

        protected override void OnBackPressed()
        {
            OnResume();
        }
    }
}
