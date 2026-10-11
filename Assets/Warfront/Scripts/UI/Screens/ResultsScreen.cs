using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Mission results: what the run paid out, why it failed if it did, and the way back.
    ///
    /// The payout is already banked by RewardService before this screen is shown, so the numbers here are
    /// a report of what happened rather than a claim the player triggers - re-opening the screen cannot
    /// pay twice, and the ledger token makes a replayed run impossible to double-claim.
    /// </summary>
    public class ResultsScreen : UIScreen
    {
        public override string Title { get { return "Debrief"; } }
        protected override string BackLabel { get { return "Base"; } }

        private static readonly Dictionary<string, string> AchievementNames = new Dictionary<string, string>
        {
            { "ach_first_blood", "FIRST BLOOD - score a confirmed kill" },
            { "ach_flawless_contract", "FLAWLESS CONTRACT - extract without dying" },
            { "ach_untouched", "UNTOUCHED - extract without taking damage" },
            { "ach_cleaner", "CLEANER - clear a contract with every objective done" },
            { "ach_marksman", "MARKSMAN - land a headshot kill" },
            { "ach_fast_hands", "FAST HANDS - reload under pressure" },
            { "ach_hardline", "HARDLINE - complete a difficulty 3 contract" },
            { "ach_fully_kitted", "FULLY KITTED - carry three weapons into a mission" },
            { "ach_deadzone_survivor", "DEADZONE SURVIVOR - survive the exclusion perimeter" },
            { "ach_citadel_breaker", "CITADEL BREAKER - break the Ashfall Citadel garrison" }
        };

        private MissionSummary _summary;
        private MissionDef _def;
        private RewardApplication _rewards;

        private Text _banner;
        private Text _subtitle;
        private Text _statsText;
        private Text _rewardText;
        private Text _lootText;
        private Text _achievementText;
        private Text _performanceText;
        private Image _bannerPanel;

        public void SetResult(RewardApplication rewards, MissionSummary summary, MissionDef def)
        {
            _rewards = rewards;
            _summary = summary;
            _def = def;
            if (Root != null && Root.gameObject.activeSelf) Refresh();
        }

        protected override void OnBuild()
        {
            Image banner = UIFactory.CreatePanel(Content, "Banner", Theme.Panel);
            UIFactory.SetAnchor(banner.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 150f), Vector2.zero);
            banner.rectTransform.offsetMin = new Vector2(0f, -150f);
            banner.rectTransform.offsetMax = new Vector2(0f, 0f);
            _bannerPanel = banner;

            _banner = UIFactory.CreateText(banner.transform, "Banner", string.Empty, 62, Theme.Primary, TextAnchor.MiddleLeft);
            UIFactory.TopLeft(_banner.rectTransform, 28f, 18f, 1100f, 66f);

            _subtitle = UIFactory.CreateText(banner.transform, "Subtitle", string.Empty, 25, Theme.Text, TextAnchor.MiddleLeft);
            UIFactory.TopLeft(_subtitle.rectTransform, 30f, 88f, 1500f, 34f);

            RectTransform statsPanel = UIFactory.CreateBandTop(Content, "Stats", 168f, 300f, 0f);
            Image statsBack = UIFactory.CreatePanel(statsPanel, "Back", Theme.PanelLight);
            UIFactory.Stretch(statsBack.rectTransform, 0f, 0f, 0f, 0f);
            _statsText = UIFactory.CreateText(statsPanel, "Stats", string.Empty, 23, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(_statsText.rectTransform, 26f, 26f, 16f, 16f);

            RectTransform rewardPanel = UIFactory.CreateBandTop(Content, "Rewards", 484f, 300f, 0f);
            Image rewardBack = UIFactory.CreatePanel(rewardPanel, "Back", Theme.PanelLight);
            UIFactory.Stretch(rewardBack.rectTransform, 0f, 0f, 0f, 0f);
            _rewardText = UIFactory.CreateText(rewardPanel, "Rewards", string.Empty, 23, Theme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(_rewardText.rectTransform, 26f, 26f, 16f, 16f);

            RectTransform lootPanel = UIFactory.CreateBandTop(Content, "Loot", 800f, 220f, 0f);
            Image lootBack = UIFactory.CreatePanel(lootPanel, "Back", Theme.PanelLight);
            UIFactory.Stretch(lootBack.rectTransform, 0f, 0f, 0f, 0f);
            _lootText = UIFactory.CreateText(lootPanel, "Loot", string.Empty, 22, Theme.TextDim, TextAnchor.UpperLeft);
            UIFactory.Stretch(_lootText.rectTransform, 26f, 26f, 14f, 14f);

            _achievementText = UIFactory.CreateText(Content, "Achievements", string.Empty, 22, Theme.Warning, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_achievementText.rectTransform, 4f, 1032f, 1500f, 60f);

            _performanceText = UIFactory.CreateText(Content, "Performance", string.Empty, 19, Theme.TextDim, TextAnchor.UpperRight);
            UIFactory.SetAnchor(_performanceText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(700f, 60f), new Vector2(0f, -1032f));

            LayoutFooterButtons();
        }

        private void LayoutFooterButtons()
        {
            Button again = UIFactory.CreateButton(Footer, "DeployAgain", "DEPLOY AGAIN", Theme.PrimaryDim, OnDeployAgain, 26);
            UIFactory.SetAnchor(again.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(280f, 58f), new Vector2(0f, 0f));

            Button list = UIFactory.CreateButton(Footer, "ContractList", "CONTRACTS", Theme.PanelLight, OnContractList, 26);
            UIFactory.SetAnchor(list.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(260f, 58f), new Vector2(300f, 0f));

            if (FooterHint != null)
            {
                UIFactory.SetAnchor(FooterHint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(560f, 30f), new Vector2(580f, 0f));
            }
        }

        protected override void Refresh()
        {
            UpdateCreditReadout();
            if (Context == null) return;

            bool success = _summary.Success;
            string missionName = _def != null ? _def.DisplayName.ToUpperInvariant() : "CONTRACT";

            _bannerPanel.color = success ? new Color(0.10f, 0.20f, 0.11f, 0.97f) : new Color(0.22f, 0.09f, 0.09f, 0.97f);
            _banner.text = success ? "EXTRACTION SUCCESSFUL" : "MISSION FAILED";
            _banner.color = success ? Theme.Primary : Theme.Danger;

            _subtitle.text = missionName + ( _def != null ? "   |   " + _def.RegionName : string.Empty) + "   |   " +
                             FailureText(_summary.Failure, success) +
                             "   |   " + (_rewards.Outcome == RewardOutcome.Banked
                                 ? _rewards.Message
                                 : _rewards.Outcome == RewardOutcome.AlreadyBanked
                                     ? "rewards for this run were already banked"
                                     : "no reward was banked");
            _subtitle.color = success ? Theme.Text : Theme.TextDim;

            _statsText.text =
                "OPERATION TIME     " + FormatTime(_summary.ElapsedSeconds) + "\n" +
                "OBJECTIVES         " + _summary.ObjectivesCompleted + " / " + _summary.ObjectivesTotal + " completed\n" +
                "CONFIRMED KILLS    " + _summary.Kills + "   (" + _summary.Headshots + " headshots, " +
                ( _summary.Kills > 0 ? Mathf.RoundToInt(_summary.Headshots * 100f / _summary.Kills) : 0) + "% of kills)\n" +
                "KIA                " + _summary.Deaths + "\n" +
                "LOOT RECOVERED     " + _summary.LootCollected + " items worth " + _summary.LootValue.ToString("N0") + " CR\n" +
                "DIFFICULTY         " + _summary.Difficulty + " / 5   " + Stars(_summary.Difficulty);

            MissionRewards rewards = _rewards.Rewards;
            string payout =
                "EXPERIENCE         +" + _rewards.XpBanked + " XP\n" +
                "CREDITS            +" + _rewards.CreditsBanked.ToString("N0") + " CR\n" +
                "MATERIALS          +" + _rewards.MaterialsBanked + "\n" +
                "SALVAGE            " + rewards.SalvageCredits.ToString("N0") + " CR from unrecoverable loot\n" +
                "SCORE              " + rewards.Score.ToString("N0") + "    RANK " + (string.IsNullOrEmpty(rewards.Rank) ? "-" : rewards.Rank) + "\n" +
                "LOOT               " + _rewards.LootBanked + " stowed" +
                (_rewards.LootLeftBehind > 0 ? ", " + _rewards.LootLeftBehind + " converted to credits (pack full)" : ", nothing left behind");

            if (!success)
            {
                payout += "\nA failed run still banks salvage, but the contract payout requires an extraction.";
            }
            else if (rewards.SalvageCredits > 0)
            {
                payout += "\nPart of the payout was paid as salvage because the extraction was completed without every bonus objective.";
            }
            _rewardText.text = payout;

            _lootText.text = LootSummary(rewards);
            _achievementText.text = AchievementSummary(rewards);

            if (_performanceText != null && Context.Performance != null)
            {
                _performanceText.text =
                    "MEASURED " + Context.Performance.SessionAverageFps.ToString("0.0") + " fps session average" +
                    "\n" + Context.Performance.AverageFrameMs.ToString("0.00") + " ms mean frame   peak " +
                    Context.Performance.WorstFrameMs.ToString("0.0") + " ms" +
                    "\n" + Context.Settings.QualityName + " preset   render scale " + Context.Performance.RenderScale.ToString("0.00");
            }
        }

        private string LootSummary(MissionRewards rewards)
        {
            if (rewards.Drops == null || rewards.Drops.Count == 0)
            {
                return "LOOT ROLLED        nothing this time. Higher difficulty contracts roll more loot.";
            }

            // Rolled drops are listed with the rarity the roller assigned so the roll is auditable.
            List<string> lines = new List<string>(rewards.Drops.Count + 1);
            lines.Add("LOOT ROLLED        " + rewards.Drops.Count + " item" + (rewards.Drops.Count == 1 ? string.Empty : "s") +
                      "   luck " + rewards.LootLuck.ToString("0.00"));

            for (int i = 0; i < rewards.Drops.Count; i++)
            {
                LootDrop drop = rewards.Drops[i];
                ItemDef item = Context.Content.GetItem(drop.ItemId);
                string name = item != null ? item.DisplayName : drop.ItemId;
                lines.Add("    " + name + "  x" + drop.Quantity + "   " + RarityRules.Name(drop.Rarity));
            }
            return string.Join("\n", lines.ToArray());
        }

        private string AchievementSummary(MissionRewards rewards)
        {
            if (rewards.UnlockedAchievements == null || rewards.UnlockedAchievements.Count == 0) return string.Empty;

            string text = "ACHIEVEMENTS UNLOCKED\n";
            for (int i = 0; i < rewards.UnlockedAchievements.Count; i++)
            {
                string id = rewards.UnlockedAchievements[i];
                string title;
                text += "- " + (AchievementNames.TryGetValue(id, out title) ? title : id) + "\n";
            }
            return text;
        }

        private static string FailureText(FailureReason reason, bool success)
        {
            if (success) return "CONTRACT COMPLETE";
            switch (reason)
            {
                case FailureReason.PlayerKilled: return "OPERATOR KILLED IN ACTION";
                case FailureReason.TimeExpired: return "CONTRACT TIMER EXPIRED";
                case FailureReason.ObjectiveLost: return "OBJECTIVE LOST";
                case FailureReason.Abandoned: return "CONTRACT ABANDONED";
                default: return "CONTRACT NOT COMPLETED";
            }
        }

        private static string Stars(int difficulty)
        {
            string stars = string.Empty;
            for (int i = 0; i < 5; i++) stars += i < difficulty ? "*" : "-";
            return stars;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private void OnDeployAgain()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Confirm);
            string missionId = !string.IsNullOrEmpty(_summary.MissionId)
                ? _summary.MissionId
                : Context.Flow.CurrentMissionId;
            if (string.IsNullOrEmpty(missionId) || !Context.Flow.StartMission(missionId))
            {
                Context.Flow.ShowMissionSelect();
            }
        }

        private void OnContractList()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Click);
            Context.Flow.ShowMissionSelect();
        }

        protected override void OnBackPressed()
        {
            if (Context == null || Context.Flow == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            Context.Flow.EnterMainMenu();
        }
    }
}
