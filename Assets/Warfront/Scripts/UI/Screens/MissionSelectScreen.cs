using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>Mission selection: the campaign list with difficulty, rewards and lock state.</summary>
    public class MissionSelectScreen : UIScreen
    {
        public override string Title { get { return "Contracts"; } }

        private RectTransform _content;
        private ScrollRect _scroll;
        private readonly List<GameObject> _rows = new List<GameObject>(12);

        protected override void OnBuild()
        {
            _scroll = UIFactory.CreateScrollView(Content, "MissionScroll", out _content);
            UIFactory.Stretch(((RectTransform)_scroll.transform), 0f, 0f, 0f, 0f);
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null || Context.Content == null) return;

            ClearChildren(_content);
            _rows.Clear();

            List<MissionDef> missions = Context.Content.OrderedMissions();
            for (int i = 0; i < missions.Count; i++)
            {
                CreateRow(missions[i]);
            }

            SetHeaderSubtitle("Select a contract. Locked contracts unlock by level and by completing the previous mission.");
        }

        private void CreateRow(MissionDef def)
        {
            bool unlocked = Context.Progression.IsMissionUnlocked(def.Id);
            MissionRecord record = Context.Progression.Model.GetMissionRecord(def.Id);

            Image rowBackground = UIFactory.CreatePanel(_content, "Mission_" + def.Id,
                unlocked ? Theme.Panel : new Color(0.08f, 0.08f, 0.08f, 0.75f));
            LayoutElement element = rowBackground.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 168f;
            element.minHeight = 168f;
            _rows.Add(rowBackground.gameObject);

            Text name = UIFactory.CreateText(rowBackground.transform, "Name", def.DisplayName.ToUpperInvariant(), 34,
                unlocked ? Theme.Text : Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 40f), new Vector2(0f, -12f));
            name.rectTransform.offsetMin = new Vector2(24f, 0f);
            name.rectTransform.offsetMax = new Vector2(-320f, 0f);

            Text region = UIFactory.CreateText(rowBackground.transform, "Region", def.RegionName + "   |   " + def.Faction, 22, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(region.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 30f), new Vector2(0f, -50f));
            region.rectTransform.offsetMin = new Vector2(24f, 0f);
            region.rectTransform.offsetMax = new Vector2(-320f, 0f);

            string status = record.Completed
                ? "COMPLETED " + record.TimesCompleted + "x   BEST " + Mathf.RoundToInt(record.BestTimeSeconds) + "s   BEST SCORE " + record.BestScore
                : unlocked ? "AVAILABLE" : "LOCKED";
            if (!unlocked)
            {
                status = def.UnlockLevel > Context.Progression.Model.Level
                    ? "LOCKED - REQUIRES LEVEL " + def.UnlockLevel
                    : "LOCKED - COMPLETE " + (Context.Content.GetMission(def.RequiredMissionId) != null
                        ? Context.Content.GetMission(def.RequiredMissionId).DisplayName.ToUpperInvariant()
                        : "PREVIOUS CONTRACT");
            }

            Text statusText = UIFactory.CreateText(rowBackground.transform, "Status", status, 22,
                record.Completed ? Theme.Primary : unlocked ? Theme.Accent : Theme.Warning, TextAnchor.UpperLeft);
            UIFactory.SetAnchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 26f), new Vector2(0f, -84f));
            statusText.rectTransform.offsetMin = new Vector2(24f, 0f);
            statusText.rectTransform.offsetMax = new Vector2(-320f, 0f);

            Text objectives = UIFactory.CreateText(rowBackground.transform, "Objectives",
                "OBJECTIVES " + def.Objectives.Count + "   REWARD " + def.RewardCredits + " CR / " + def.RewardXp + " XP   DIFFICULTY " + DifficultyStars(def.Difficulty),
                22, Theme.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetAnchor(objectives.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(0f, 14f));
            objectives.rectTransform.offsetMin = new Vector2(24f, 0f);
            objectives.rectTransform.offsetMax = new Vector2(-320f, 0f);

            Button select = UIFactory.CreateButton(rowBackground.transform, "Select",
                unlocked ? "BRIEFING" : "LOCKED", unlocked ? Theme.PrimaryDim : Theme.Panel, null, 28);
            UIFactory.SetAnchor(select.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(280f, 76f), new Vector2(-20f, 0f));
            select.interactable = unlocked;
            string missionId = def.Id;
            select.onClick.AddListener(() => OnSelect(missionId));
        }

        private static string DifficultyStars(int difficulty)
        {
            string stars = string.Empty;
            for (int i = 0; i < 5; i++) stars += i < difficulty ? "*" : "-";
            return stars;
        }

        private void OnSelect(string missionId)
        {
            if (Context.Audio != null) Context.Audio.PlayUi(AudioCue.Confirm);
            Context.Flow.ShowBriefing(missionId);
        }
    }
}
