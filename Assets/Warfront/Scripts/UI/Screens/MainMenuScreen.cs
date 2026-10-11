using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>Main menu: campaign entry point plus a live profile summary.</summary>
    public class MainMenuScreen : UIScreen
    {
        public override string Title { get { return "Warfront"; } }
        protected override string BackLabel { get { return "Quit"; } }
        protected override bool ShowCredits { get { return true; } }

        private RectTransform _buttonColumn;
        private RectTransform _profilePanel;
        private Text _profileTitle;
        private Text _profileBody;
        private Text _diagnostics;
        private Button _deployButton;
        private Text _deployLabel;

        protected override void OnBuild()
        {
            Image title = UIFactory.CreatePanel(Content, "TitleBlock", new Color(0.05f, 0.06f, 0.06f, 0.0f));
            UIFactory.SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(760f, 150f), new Vector2(0f, 0f));
            Text big = UIFactory.CreateText(title.transform, "Wordmark", "W A R F R O N T", 86, Theme.Primary, TextAnchor.MiddleLeft);
            UIFactory.Stretch(big.rectTransform, 0f, 0f, 0f, 60f);
            Text tagline = UIFactory.CreateText(title.transform, "Tagline", "THIRD PERSON EXTRACTION SURVIVAL  |  ASHFALL CAMPAIGN", 24, Theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.Stretch(tagline.rectTransform, 6f, 0f, 96f, 0f);

            _buttonColumn = UIFactory.CreateRect(Content, "Buttons");
            UIFactory.SetAnchor(_buttonColumn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(540f, 560f), new Vector2(0f, 180f));
            VerticalLayoutGroup layout = _buttonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _deployButton = UIFactory.CreateButton(_buttonColumn, "Deploy", "DEPLOY", Theme.PrimaryDim, OnDeploy, 36);
            LayoutElement deployElement = _deployButton.gameObject.AddComponent<LayoutElement>();
            deployElement.preferredHeight = 84f;
            _deployLabel = _deployButton.GetComponentInChildren<Text>();

            AddMenuButton("MISSIONS", OnMissions);
            AddMenuButton("LOADOUT", OnLoadout);
            AddMenuButton("INVENTORY", OnInventory);
            AddMenuButton("UPGRADES", OnUpgrades);
            AddMenuButton("SETTINGS", OnSettings);
            AddMenuButton("QUIT", OnQuit);

            _profilePanel = UIFactory.CreateRect(Content, "Profile");
            UIFactory.SetAnchor(_profilePanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(620f, 470f), new Vector2(0f, 0f));
            Image profileBackground = UIFactory.CreatePanel(_profilePanel, "Background", Theme.Panel);
            UIFactory.Stretch(profileBackground.rectTransform, 0f, 0f, 0f, 0f);

            _profileTitle = UIFactory.CreateText(profileBackground.transform, "Header", "OPERATOR FILE", 28, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(_profileTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 46f), Vector2.zero);
            _profileTitle.rectTransform.offsetMin = new Vector2(24f, 0f);
            _profileTitle.rectTransform.offsetMax = new Vector2(-24f, 0f);

            _profileBody = UIFactory.CreateText(profileBackground.transform, "Body", string.Empty, 26, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(_profileBody.rectTransform, 24f, 24f, 62f, 24f);

            _diagnostics = UIFactory.CreateText(Content, "Diagnostics", string.Empty, 20, Theme.TextDim, TextAnchor.LowerRight);
            UIFactory.SetAnchor(_diagnostics.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(760f, 72f), new Vector2(0f, 8f));
        }

        private Button AddMenuButton(string label, UnityEngine.Events.UnityAction action)
        {
            Button button = UIFactory.CreateButton(_buttonColumn, label, label, Theme.PanelLight, action, 32);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 70f;
            return button;
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Progression == null) return;

            ProgressionModel model = Context.Progression.Model;

            // Deploy button: resume the selected mission if it is playable, else open the list.
            string selected = model.SelectedMissionId;
            MissionDef selectedDef = Context.Content.GetMission(selected);
            bool canDeploy = selectedDef != null && Context.Progression.IsMissionUnlocked(selectedDef.Id);
            if (_deployLabel != null)
            {
                _deployLabel.text = canDeploy ? "DEPLOY  " + selectedDef.DisplayName.ToUpperInvariant() : "DEPLOY";
            }
            if (_deployButton != null) _deployButton.interactable = true;

            if (_profileBody != null)
            {
                int weaponsOwned = Context.Inventory != null ? Context.Inventory.Weapons.Count : 0;
                int itemsHeld = Context.Inventory != null ? Context.Inventory.UsedSlots : 0;
                string time = FormatTime(model.TotalPlaySeconds);

                _profileBody.text =
                    "LEVEL            " + model.Level + "   (" + Mathf.RoundToInt(model.LevelProgress01 * 100f) + "%)\n" +
                    "CREDITS          " + model.Credits.ToString("N0") + "\n" +
                    "MATERIALS        " + model.Materials + "\n" +
                    "CONTRACTS        " + model.MissionsCompleted + " completed\n" +
                    "EXTRACTIONS      " + model.TotalExtractions + "\n" +
                    "CONFIRMED KILLS  " + model.TotalKills + "\n" +
                    "KIA              " + model.TotalDeaths + "\n" +
                    "OPERATION TIME   " + time + "\n" +
                    "ARSENAL          " + weaponsOwned + " weapons\n" +
                    "FIELD STORES     " + itemsHeld + " / " + (Context.Inventory != null ? Context.Inventory.Capacity : 0) + " slots\n" +
                    "\nACHIEVEMENTS     " + model.AchievementSnapshot().Count + " / " + ContentDatabase.AllAchievementIds.Length;
            }

            if (_diagnostics != null && Context.Platform != null && Context.Settings != null)
            {
                _diagnostics.text = Context.Settings.QualityName + " quality  |  " + Context.Settings.Data.TargetFrameRate + " fps cap" +
                                    "  |  save " + (Context.Save != null ? Context.Save.FileSizeBytes / 1024 + " KB" : "n/a") +
                                    "\n" + (Context.Save != null ? Context.Save.LastLoad.Message : string.Empty);
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            return hours > 0 ? hours + "h " + minutes + "m" : minutes + "m " + (total % 60) + "s";
        }

        private void OnDeploy()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Confirm);
            string selected = Context.Progression.Model.SelectedMissionId;
            MissionDef def = Context.Content.GetMission(selected);
            if (def != null && Context.Progression.IsMissionUnlocked(def.Id))
            {
                Context.Flow.ShowBriefing(def.Id);
                return;
            }
            Context.Flow.ShowMissionSelect();
        }

        private void OnMissions()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowMissionSelect();
        }

        private void OnLoadout()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowLoadout();
        }

        private void OnInventory()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowInventory();
        }

        private void OnUpgrades()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowUpgrades();
        }

        private void OnSettings()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowSettings();
        }

        private void OnQuit()
        {
            Context.Audio.PlayUi(AudioCue.Back);
            Context.Flow.QuitGame();
        }

        public override void Tick(float deltaTime)
        {
            UpdateCreditReadout();
        }
    }
}
