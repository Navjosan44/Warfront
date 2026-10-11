using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>Mission briefing: objectives, rewards, threat and the deploy action.</summary>
    public class BriefingScreen : UIScreen
    {
        public override string Title { get { return "Briefing"; } }

        private MissionDef _mission;
        private Text _missionName;
        private Text _region;
        private Text _briefing;
        private RectTransform _objectiveList;
        private Text _rewardText;
        private Text _threatText;
        private Button _deploy;
        private Button _loadout;

        public void SetMission(MissionDef def)
        {
            _mission = def;
            if (Root != null && Root.gameObject.activeSelf) Refresh();
        }

        protected override void OnBuild()
        {
            Image left = UIFactory.CreatePanel(Content, "BriefingPanel", Theme.Panel);
            UIFactory.SetAnchor(left.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero);
            left.rectTransform.offsetMin = new Vector2(0f, 0f);
            left.rectTransform.offsetMax = new Vector2(-16f, 0f);

            _missionName = UIFactory.CreateText(left.transform, "MissionName", string.Empty, 48, Theme.Primary, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(_missionName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 60f), new Vector2(0f, -22f));
            _missionName.rectTransform.offsetMin = new Vector2(26f, 0f);
            _missionName.rectTransform.offsetMax = new Vector2(-26f, 0f);

            _region = UIFactory.CreateText(left.transform, "Region", string.Empty, 24, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(_region.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 30f), new Vector2(0f, -86f));
            _region.rectTransform.offsetMin = new Vector2(26f, 0f);
            _region.rectTransform.offsetMax = new Vector2(-26f, 0f);

            _briefing = UIFactory.CreateText(left.transform, "Briefing", string.Empty, 27, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(_briefing.rectTransform, 26f, 26f, 132f, 250f);

            Text objectiveHeader = UIFactory.CreateText(left.transform, "ObjectiveHeader", "OBJECTIVES", 24, Theme.Accent, TextAnchor.LowerLeft);
            UIFactory.SetAnchor(objectiveHeader.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(0f, 210f));
            objectiveHeader.rectTransform.offsetMin = new Vector2(26f, 0f);
            objectiveHeader.rectTransform.offsetMax = new Vector2(-26f, 0f);

            _objectiveList = UIFactory.CreateRect(left.transform, "ObjectiveList");
            UIFactory.SetAnchor(_objectiveList, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 190f), new Vector2(0f, 16f));
            _objectiveList.offsetMin = new Vector2(26f, 16f);
            _objectiveList.offsetMax = new Vector2(-26f, 16f);
            VerticalLayoutGroup layout = _objectiveList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            Image right = UIFactory.CreatePanel(Content, "RewardPanel", Theme.PanelLight);
            UIFactory.SetAnchor(right.rectTransform, new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            right.rectTransform.offsetMin = new Vector2(16f, 0f);
            right.rectTransform.offsetMax = new Vector2(0f, 0f);

            Text rewardHeader = UIFactory.CreateText(right.transform, "Header", "CONTRACT TERMS", 24, Theme.Accent, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(rewardHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 30f), new Vector2(0f, -18f));
            rewardHeader.rectTransform.offsetMin = new Vector2(22f, 0f);
            rewardHeader.rectTransform.offsetMax = new Vector2(-22f, 0f);

            _rewardText = UIFactory.CreateText(right.transform, "Rewards", string.Empty, 27, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(_rewardText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 250f), new Vector2(0f, -60f));
            _rewardText.rectTransform.offsetMin = new Vector2(22f, 0f);
            _rewardText.rectTransform.offsetMax = new Vector2(-22f, 0f);

            _threatText = UIFactory.CreateText(right.transform, "Threat", string.Empty, 24, Theme.Warning, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(_threatText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 220f), new Vector2(0f, -320f));
            _threatText.rectTransform.offsetMin = new Vector2(22f, 0f);
            _threatText.rectTransform.offsetMax = new Vector2(-22f, 0f);

            _deploy = UIFactory.CreateButton(right.transform, "Deploy", "DEPLOY", Theme.PrimaryDim, OnDeploy, 36);
            UIFactory.SetAnchor(_deploy.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 84f), new Vector2(0f, 108f));
            _deploy.GetComponent<RectTransform>().offsetMin = new Vector2(22f, 108f);
            _deploy.GetComponent<RectTransform>().offsetMax = new Vector2(-22f, 192f);

            _loadout = UIFactory.CreateButton(right.transform, "Loadout", "CHANGE LOADOUT", Theme.Panel, OnLoadout, 30);
            UIFactory.SetAnchor(_loadout.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 72f), new Vector2(0f, 24f));
            _loadout.GetComponent<RectTransform>().offsetMin = new Vector2(22f, 24f);
            _loadout.GetComponent<RectTransform>().offsetMax = new Vector2(-22f, 96f);
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (_mission == null)
            {
                _mission = Context.Content.GetMission(Context.Progression.Model.SelectedMissionId);
            }
            if (_mission == null) return;

            bool unlocked = Context.Progression.IsMissionUnlocked(_mission.Id);
            _missionName.text = _mission.DisplayName.ToUpperInvariant();
            _region.text = _mission.RegionName + "   |   " + _mission.Faction + "   |   DIFFICULTY " + _mission.Difficulty + " / 5";
            _briefing.text = _mission.Briefing;

            ClearChildren(_objectiveList);
            for (int i = 0; i < _mission.Objectives.Count; i++)
            {
                ObjectiveDef objective = _mission.Objectives[i];
                Color color = objective.Optional ? Theme.TextDim : Theme.Text;
                Text line = UIFactory.CreateText(_objectiveList, "Objective_" + objective.Id,
                    (objective.Optional ? "[OPTIONAL] " : "[ ] ") + objective.Title + "  -  " + objective.Description,
                    24, color, TextAnchor.MiddleLeft);
                LayoutElement element = line.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 34f;
                element.minHeight = 34f;
            }

            _rewardText.text =
                "PAYOUT          " + _mission.RewardCredits.ToString("N0") + " CR\n" +
                "EXPERIENCE      " + _mission.RewardXp + " XP\n" +
                "MATERIALS       " + _mission.RewardMaterials + "\n" +
                "LOOT ROLLS      " + _mission.LootRolls + "\n" +
                (string.IsNullOrEmpty(_mission.RequiredMissionId) ? string.Empty :
                    "\nUNLOCKED BY     " + (Context.Content.GetMission(_mission.RequiredMissionId) != null
                        ? Context.Content.GetMission(_mission.RequiredMissionId).DisplayName : "-"));

            _threatText.text =
                "HOSTILE BUDGET  " + _mission.EnemyBudget + " units\n" +
                "MAX ACTIVE      " + _mission.MaxEnemiesAlive + "\n" +
                "EXTRACT HOLD    " + Mathf.RoundToInt(_mission.ExtractionHoldSeconds) + "s\n" +
                "TIME LIMIT      " + (_mission.TimeLimitSeconds > 0f ? Mathf.RoundToInt(_mission.TimeLimitSeconds) + "s" : "none") + "\n" +
                (_mission.EnemyReinforceInterval > 0f ? "REINFORCEMENTS  every " + Mathf.RoundToInt(_mission.EnemyReinforceInterval) + "s\n" : string.Empty) +
                (_mission.IsTutorial ? "\nTRAINING CONTRACT - short and guided." : "\nFail and you keep a salvage share of collected loot.");

            _deploy.interactable = unlocked;
            Text deployLabel = _deploy.GetComponentInChildren<Text>();
            if (deployLabel != null) deployLabel.text = unlocked ? "DEPLOY" : "LOCKED";
        }

        private void OnDeploy()
        {
            if (_mission == null) return;
            if (!Context.Progression.IsMissionUnlocked(_mission.Id))
            {
                Context.Audio.PlayUi(AudioCue.Deny);
                return;
            }
            Context.Audio.PlayUi(AudioCue.Confirm);
            Context.Flow.ShowLoadout();
        }

        private void OnLoadout()
        {
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowLoadout();
        }
    }
}
