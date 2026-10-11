using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public enum RewardOutcome
    {
        Banked = 0,
        AlreadyBanked = 1,
        Invalid = 2
    }

    public struct RewardApplication
    {
        public RewardOutcome Outcome;
        public MissionRewards Rewards;
        public int CreditsBanked;
        public int XpBanked;
        public int MaterialsBanked;
        public int LootBanked;
        public int LootLeftBehind;
        public string Message;
    }

    /// <summary>
    /// Single gateway for mission payouts.
    ///
    /// It rolls loot with a seeded roller, checks the anti-duplication ledger before paying anything,
    /// applies XP/credits/materials through the progression service, moves loot into the inventory and
    /// records the mission result. Replaying a results screen can never pay twice.
    /// </summary>
    public class RewardService
    {
        private readonly GameContext _context;

        public RewardService(GameContext context)
        {
            _context = context;
        }

        public RewardApplication Bank(MissionSummary summary, MissionDef def, RewardLedger ledger)
        {
            RewardApplication application = default(RewardApplication);
            application.Outcome = RewardOutcome.Invalid;
            application.Message = "No mission summary";

            if (!summary.IsValid || def == null || _context == null)
            {
                return application;
            }

            MissionRewards rewards = RewardCalculator.Compute(summary, def, _context.Progression != null ? _context.Progression.Model : null);

            RewardLedger targetLedger = ledger ?? _context.Ledger;
            if (targetLedger != null)
            {
                RewardClaimResult claim = targetLedger.Claim(rewards.ClaimToken);
                if (claim == RewardClaimResult.AlreadyClaimed)
                {
                    application.Outcome = RewardOutcome.AlreadyBanked;
                    application.Rewards = rewards;
                    application.Message = "Rewards for this run were already banked";
                    return application;
                }
                if (claim != RewardClaimResult.Granted)
                {
                    application.Message = "Invalid reward token";
                    return application;
                }
            }

            try
            {
                RollLoot(summary, def, rewards);
                ApplyProgression(summary, def, rewards, ref application);
                ApplyLootToInventory(rewards, ref application);
                PersistLedger(targetLedger);
                RecordMission(summary);
                application.Outcome = RewardOutcome.Banked;
                application.Rewards = rewards;
                application.Message = summary.Success ? "Rewards banked" : "Salvage banked";
            }
            catch (System.Exception ex)
            {
                // Any failure rolls the claim back so the reward can be attempted again safely.
                if (targetLedger != null) targetLedger.Revoke(rewards.ClaimToken);
                Debug.LogError("[Warfront] Reward payout failed: " + ex.Message);
                application.Outcome = RewardOutcome.Invalid;
                application.Message = "Reward payout failed";
            }

            return application;
        }

        private void RollLoot(MissionSummary summary, MissionDef def, MissionRewards rewards)
        {
            if (!summary.Success || rewards.LootRolls <= 0) return;
            LootRoller roller = new LootRoller(_context.Content, WfMath.Combine(summary.RunId, def.Seed));
            roller.Roll(def.LootTableId, rewards.LootLuck, rewards.LootRolls, rewards.Drops);
        }

        private void ApplyProgression(MissionSummary summary, MissionDef def, MissionRewards rewards, ref RewardApplication application)
        {
            ProgressionService progression = _context.Progression;
            if (progression == null) return;

            if (rewards.Xp > 0)
            {
                progression.AddXp(rewards.Xp);
                application.XpBanked = rewards.Xp;
            }
            if (rewards.Credits > 0)
            {
                progression.AddCredits(rewards.Credits);
                application.CreditsBanked = rewards.Credits;
            }
            if (rewards.Materials > 0)
            {
                progression.AddMaterials(rewards.Materials);
                application.MaterialsBanked = rewards.Materials;
            }

            for (int i = 0; i < rewards.UnlockedAchievements.Count; i++)
            {
                progression.Model.UnlockAchievement(rewards.UnlockedAchievements[i]);
            }

            progression.AddPlayTime(summary.ElapsedSeconds);
            progression.SyncLevelUnlocks();
        }

        private void ApplyLootToInventory(MissionRewards rewards, ref RewardApplication application)
        {
            InventoryModel inventory = _context.Inventory;
            if (inventory == null || rewards.Drops == null) return;

            for (int i = 0; i < rewards.Drops.Count; i++)
            {
                LootDrop drop = rewards.Drops[i];
                ItemDef item = _context.Content.GetItem(drop.ItemId);
                if (item == null) continue;

                int leftover;
                InventoryResult result = inventory.Add(drop.ItemId, drop.Quantity, drop.Rarity, drop.UpgradeLevel, out leftover);
                if (result == InventoryResult.Success)
                {
                    application.LootBanked++;
                    continue;
                }

                // No room: convert the remainder into a small credit value instead of losing it silently.
                int amount = result == InventoryResult.Full ? drop.Quantity : leftover;
                if (amount <= 0) continue;

                int salvage = Mathf.RoundToInt(item.Value * amount * 0.5f);
                if (salvage > 0 && _context.Progression != null)
                {
                    _context.Progression.AddCredits(salvage);
                    application.CreditsBanked += salvage;
                }
                application.LootLeftBehind++;
            }
        }

        private void PersistLedger(RewardLedger ledger)
        {
            if (ledger == null || _context.Save == null || _context.Save.Data == null) return;
            _context.Save.Data.ClaimedRewardTokens.Clear();
            _context.Save.Data.ClaimedRewardTokens.AddRange(ledger.Snapshot());
            _context.Save.MarkDirty();
        }

        private void RecordMission(MissionSummary summary)
        {
            ProgressionService progression = _context.Progression;
            if (progression == null) return;

            if (summary.Success)
            {
                progression.RecordMissionSuccess(summary.MissionId, summary.ElapsedSeconds, RewardCalculator.ComputeScore(summary));
            }
            else
            {
                progression.RecordMissionFailure(summary.MissionId);
            }
            _context.Save.Flush();
        }
    }
}
