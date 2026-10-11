using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public struct MissionRewards
    {
        public string MissionId;
        public string ClaimToken;
        public int RunId;
        public bool Extracted;

        public int Credits;
        public int Xp;
        public int Materials;
        public int Score;
        public string Rank;
        public int LootRolls;
        public float LootLuck;
        public int SalvageCredits;
        public List<LootDrop> Drops;
        public List<string> UnlockedAchievements;

        public bool HasAnyReward
        {
            get { return Credits > 0 || Xp > 0 || Materials > 0 || (Drops != null && Drops.Count > 0); }
        }
    }

    /// <summary>
    /// Pure reward maths. Only a completed extraction banks the full reward; a failed run converts
    /// a fraction of the loot that was physically collected into salvage credits so losing is
    /// meaningful but never a total wipe of an evening's progress.
    /// </summary>
    public static class RewardCalculator
    {
        public const float SalvageRate = 0.4f;
        public const float FailureXpRate = 0.3f;
        public const int ExtractionBonusScore = 500;

        /// <summary>Stable per-run id. Same mission + same run index always produces the same id.</summary>
        public static int MakeRunId(string missionId, int attemptIndex, int seed)
        {
            return WfMath.Combine(WfMath.Hash(missionId), WfMath.Combine(attemptIndex, seed));
        }

        /// <summary>
        /// Replay-proof claim token: mission + run id + outcome. The reward ledger stores tokens of
        /// already banked runs, so replaying the results screen cannot pay out twice.
        /// </summary>
        public static string MakeClaimToken(string missionId, int runId, bool extracted)
        {
            return (missionId ?? "unknown") + ":" + runId.ToString("X8") + (extracted ? ":X" : ":F");
        }

        public static int ComputeScore(MissionSummary summary)
        {
            if (!summary.IsValid) return 0;
            int score = 250 * Mathf.Max(1, summary.Difficulty);
            score += summary.ObjectivesCompleted * 150;
            score += summary.Kills * 12;
            score += summary.Headshots * 8;
            score += summary.LootValue / 10;
            score -= summary.Deaths * 75;
            if (summary.Success) score += ExtractionBonusScore;

            // Fast, clean extractions earn a time bonus (capped so it never dominates).
            if (summary.Success && summary.ElapsedSeconds > 1f)
            {
                int par = 240 + 60 * Mathf.Max(1, summary.Difficulty);
                if (summary.ElapsedSeconds < par) score += Mathf.RoundToInt((par - summary.ElapsedSeconds) * 1.5f);
            }
            return Mathf.Max(0, score);
        }

        public static string RankFor(int score, int difficulty)
        {
            float perDifficulty = 1100f;
            float ratio = score / Mathf.Max(1f, perDifficulty * Mathf.Max(1, difficulty));
            if (ratio >= 0.95f) return "S";
            if (ratio >= 0.78f) return "A";
            if (ratio >= 0.6f) return "B";
            if (ratio >= 0.4f) return "C";
            return "D";
        }

        public static void ComputeAchievements(MissionSummary summary, ProgressionModel progression, List<string> results)
        {
            if (results == null) return;
            if (!summary.Success) return;

            if (summary.ObjectivesCompleted >= summary.ObjectivesTotal && summary.ObjectivesTotal > 0)
            {
                results.Add("ach_flawless_contract");
            }
            if (summary.Deaths == 0) results.Add("ach_untouched");
            if (summary.Kills >= 20) results.Add("ach_cleaner");
            if (summary.Headshots >= 5) results.Add("ach_marksman");
            if (summary.ElapsedSeconds > 0f && summary.ElapsedSeconds < 180f) results.Add("ach_fast_hands");
            if (progression != null && summary.Difficulty >= 4) results.Add("ach_hardline");
        }

        /// <summary>
        /// Builds the reward bundle. Loot is rolled by the caller (it owns the seeded roller) but the
        /// roll count and luck are decided here so balance stays in one place.
        /// </summary>
        public static MissionRewards Compute(MissionSummary summary, MissionDef def, ProgressionModel progression)
        {
            MissionRewards rewards;
            rewards.MissionId = summary.MissionId;
            rewards.RunId = summary.RunId;
            rewards.Extracted = summary.Success;
            rewards.Credits = 0;
            rewards.Xp = 0;
            rewards.Materials = 0;
            rewards.Score = ComputeScore(summary);
            rewards.Rank = RankFor(rewards.Score, summary.Difficulty);
            rewards.LootRolls = 0;
            rewards.LootLuck = 0f;
            rewards.SalvageCredits = 0;
            rewards.Drops = new List<LootDrop>();
            rewards.UnlockedAchievements = new List<string>();
            rewards.ClaimToken = MakeClaimToken(summary.MissionId, summary.RunId, summary.Success);

            if (def == null || !summary.IsValid)
            {
                rewards.ClaimToken = null;
                return rewards;
            }

            int level = progression != null ? progression.Level : 1;

            if (summary.Success)
            {
                float difficultyScale = 1f + (summary.Difficulty - 1) * 0.45f;
                float completionRatio = summary.ObjectivesTotal <= 0
                    ? 1f
                    : WfMath.Clamp01((float)summary.ObjectivesCompleted / summary.ObjectivesTotal);
                float levelPenalty = level > 0 ? WfMath.Clamp(1.25f - level * 0.01f, 0.6f, 1.25f) : 1f;
                float rankBonus = RankBonus(rewards.Rank);

                rewards.Credits = Mathf.RoundToInt(def.RewardCredits * difficultyScale * (0.7f + 0.3f * completionRatio) * rankBonus);
                rewards.Xp = Mathf.RoundToInt(def.RewardXp * difficultyScale * (0.75f + 0.25f * completionRatio) * rankBonus * levelPenalty);
                rewards.Materials = Mathf.Max(0, Mathf.RoundToInt(def.RewardMaterials * difficultyScale * rankBonus));

                rewards.LootRolls = Mathf.Max(0, def.LootRolls) + (rewards.Rank == "S" ? 2 : rewards.Rank == "A" ? 1 : 0);
                rewards.LootLuck = WfMath.Clamp01(def.RarityLuck + (summary.Difficulty - 1) * 0.08f + (summary.Success ? 0.1f : 0f));
                ComputeAchievements(summary, progression, rewards.UnlockedAchievements);
            }
            else
            {
                // Failed run: salvage only. No mission bonus, no loot rolls, capped so it can never
                // beat a successful extraction of the same mission.
                rewards.SalvageCredits = Mathf.RoundToInt(summary.LootValue * SalvageRate);
                int salvageCap = Mathf.RoundToInt(def.RewardCredits * 0.5f);
                rewards.SalvageCredits = Mathf.Min(rewards.SalvageCredits, salvageCap);
                rewards.Credits = rewards.SalvageCredits;
                rewards.Xp = Mathf.RoundToInt(def.RewardXp * FailureXpRate * (0.5f + 0.5f * (progress01(summary))));
                rewards.Materials = 0;
                rewards.LootRolls = 0;
                rewards.LootLuck = 0f;
            }

            if (rewards.Credits < 0) rewards.Credits = 0;
            if (rewards.Xp < 0) rewards.Xp = 0;
            return rewards;
        }

        private static float progress01(MissionSummary summary)
        {
            if (summary.ObjectivesTotal <= 0) return 0.5f;
            return WfMath.Clamp01((float)summary.ObjectivesCompleted / summary.ObjectivesTotal);
        }

        private static float RankBonus(string rank)
        {
            switch (rank)
            {
                case "S": return 1.35f;
                case "A": return 1.2f;
                case "B": return 1.08f;
                case "C": return 1f;
                default: return 0.9f;
            }
        }
    }
}
