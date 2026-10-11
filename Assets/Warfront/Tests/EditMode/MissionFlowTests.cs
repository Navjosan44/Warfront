using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class MissionFlowTests
    {
        private ContentDatabase _content;
        private MissionDef _mission;
        private MissionModel _model;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
            _mission = _content.GetMission("mission_rust_ridge");
            _model = new MissionModel(_mission, _content, 1234);
            _model.Begin();
        }

        [Test]
        public void MissionStartsActiveWithObjectivesPending()
        {
            Assert.AreEqual(MissionState.Active, _model.State);
            Assert.AreEqual(_mission.Objectives.Count, _model.Objectives.Count);
            Assert.IsFalse(_model.AllRequiredComplete);
            Assert.AreEqual(ExtractionState.Locked, _model.Extraction.State);
        }

        [Test]
        public void ObjectiveProgressCountsUpAndCompletesExactlyOnce()
        {
            int completions = 0;
            _model.ObjectiveCompleted += progress => completions++;

            // Rust Ridge requires two supply crates.
            _model.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 1);
            Assert.AreEqual(0, completions, "one crate must not finish a two crate objective");

            _model.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 1);
            Assert.AreEqual(1, completions);

            _model.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 1);
            Assert.AreEqual(1, completions, "objective must not complete twice");
        }

        [Test]
        public void ProgressWithWrongTagIsIgnored()
        {
            int completions = 0;
            _model.ObjectiveCompleted += progress => completions++;
            _model.ReportProgress(ObjectiveType.CollectIntel, "intel", 1);
            Assert.AreEqual(0, completions, "no intel objective exists on this mission");
            _model.ReportProgress(ObjectiveType.SecureLocation, "wrong_tag", 1);
            Assert.AreEqual(0, completions, "tag mismatch must not satisfy the objective");
        }

        [Test]
        public void ExtractionStaysLockedUntilRequiredObjectivesComplete()
        {
            _model.Tick(1f, true, Vector3.zero, Vector3.zero, 5f);
            Assert.AreEqual(ExtractionState.Locked, _model.Extraction.State);

            _model.ReportProgress(ObjectiveType.SecureLocation, "secure_a", 1);
            Assert.AreEqual(ExtractionState.Locked, _model.Extraction.State);

            _model.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 5);
            Assert.IsTrue(_model.AllRequiredComplete);
            Assert.AreEqual(ExtractionState.Available, _model.Extraction.State);
        }

        [Test]
        public void HoldingTheZoneCompletesTheExtraction()
        {
            CompleteAllRequiredObjectives();
            float required = _model.Extraction.RequiredSeconds;

            _model.Tick(required * 0.5f, true, Vector3.zero, Vector3.zero, 5f);
            Assert.AreEqual(ExtractionState.Countdown, _model.Extraction.State);
            Assert.AreEqual(MissionState.Active, _model.State);

            _model.Tick(required * 0.6f, true, Vector3.zero, Vector3.zero, 5f);
            Assert.AreEqual(ExtractionState.Complete, _model.Extraction.State);
            Assert.AreEqual(MissionState.Success, _model.State);
        }

        [Test]
        public void LeavingTheZoneInterruptsAndDecaysProgress()
        {
            CompleteAllRequiredObjectives();
            _model.Tick(6f, true, Vector3.zero, Vector3.zero, 5f);
            float held = _model.Extraction.HoldTime;
            Assert.Greater(held, 0f);

            _model.Tick(1f, false, Vector3.zero, Vector3.zero, 5f);
            Assert.Less(_model.Extraction.HoldTime, held);
            Assert.AreEqual(ExtractionState.Interrupted, _model.Extraction.State);

            _model.Tick(10f, false, Vector3.zero, Vector3.zero, 5f);
            Assert.AreEqual(0f, _model.Extraction.HoldTime, 0.001f);
            Assert.AreEqual(ExtractionState.Available, _model.Extraction.State);
            Assert.AreEqual(MissionState.Active, _model.State, "leaving the zone must not fail the mission");
        }

        [Test]
        public void ExtractionRewardCanOnlyBeConsumedOnce()
        {
            CompleteAllRequiredObjectives();
            _model.Tick(_model.Extraction.RequiredSeconds + 1f, true, Vector3.zero, Vector3.zero, 5f);
            Assert.AreEqual(MissionState.Success, _model.State);
            Assert.IsTrue(_model.Extraction.RewardClaimed);

            Assert.IsFalse(_model.Extraction.TryConsumeReward(), "second consumption must be refused");
            Assert.IsFalse(_model.Succeed(), "replaying success must not grant rewards again");
        }

        [Test]
        public void TimeLimitFailureIsReported()
        {
            MissionDef timed = new MissionDef { Id = "timed", TimeLimitSeconds = 10f, ExtractionHoldSeconds = 5f };
            timed.AddObjective(new ObjectiveDef("hold", ObjectiveType.SecureLocation, "Hold", 1));
            MissionModel model = new MissionModel(timed, _content, 1);
            model.Begin();

            FailureReason reason = FailureReason.None;
            model.MissionFailedEvent += (m, r) => reason = r;
            model.Tick(11f, false, Vector3.zero, Vector3.zero, 1f);

            Assert.AreEqual(MissionState.Failed, model.State);
            Assert.AreEqual(FailureReason.TimeExpired, reason);
        }

        [Test]
        public void SurviveTimeObjectiveCompletesAutomatically()
        {
            MissionDef survive = new MissionDef { Id = "survive", ExtractionHoldSeconds = 5f };
            survive.AddObjective(new ObjectiveDef("hold", ObjectiveType.SurviveTime, "Hold", 5));
            MissionModel model = new MissionModel(survive, _content, 1);
            model.Begin();

            model.Tick(3f, false, Vector3.zero, Vector3.zero, 1f);
            Assert.IsFalse(model.AllRequiredComplete);
            model.Tick(2.5f, false, Vector3.zero, Vector3.zero, 1f);
            Assert.IsTrue(model.AllRequiredComplete);
        }

        [Test]
        public void KillAndLootReportingUpdatesTheSummary()
        {
            _model.RegisterKill(true, true);
            _model.RegisterKill(false, false);
            _model.RegisterLoot(new LootDrop { ItemId = "mat_alloy", Quantity = 4, Rarity = Rarity.Common, UpgradeLevel = 1 });

            MissionSummary summary = _model.BuildSummary();
            Assert.AreEqual(2, summary.Kills);
            Assert.AreEqual(1, summary.Headshots);
            Assert.AreEqual(1, summary.LootCollected);
            Assert.Greater(summary.LootValue, 0);
        }

        [Test]
        public void FailIsIgnoredAfterSuccess()
        {
            CompleteAllRequiredObjectives();
            _model.Tick(_model.Extraction.RequiredSeconds + 1f, true, Vector3.zero, Vector3.zero, 5f);
            _model.Fail(FailureReason.PlayerKilled);
            Assert.AreEqual(MissionState.Success, _model.State, "a completed extraction cannot be failed afterwards");
        }

        [Test]
        public void FailedMissionCannotAlsoSucceed()
        {
            _model.Fail(FailureReason.PlayerKilled);
            Assert.AreEqual(MissionState.Failed, _model.State);
            Assert.IsFalse(_model.Succeed());
        }

        [Test]
        public void ExtractionStatusReportsDistanceAndLabel()
        {
            CompleteAllRequiredObjectives();
            Vector3 zone = new Vector3(10f, 0f, 0f);
            ExtractionStatus status = _model.Extraction.GetStatus(new Vector3(0f, 0f, 0f), zone, 5f);
            Assert.AreEqual(5f, status.DistanceToZone, 0.01f);
            Assert.IsFalse(status.PlayerInZone);
            Assert.AreEqual("EXTRACTION AVAILABLE", status.Label);

            ExtractionStatus inside = _model.Extraction.GetStatus(new Vector3(9f, 0f, 0f), zone, 5f);
            Assert.IsTrue(inside.PlayerInZone);
        }

        private void CompleteAllRequiredObjectives()
        {
            _model.ReportProgress(ObjectiveType.SecureLocation, "secure_a", 1);
            _model.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 5);
            // The ReachExtraction objective completes by entering the zone - that is the whole point.
            _model.NotifyExtractionZoneEntered();
            Assert.IsTrue(_model.AllRequiredComplete, "test setup expects all required objectives to be complete");
        }
    }

    public class RewardTests
    {
        private ContentDatabase _content;
        private ProgressionModel _progression;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
            _progression = new ProgressionModel(_content);
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
        }

        private MissionSummary Summary(bool success, int difficulty, int kills, int objectivesDone, int objectivesTotal, int lootValue)
        {
            MissionSummary summary;
            summary.MissionId = "mission_rust_ridge";
            summary.RunId = 42;
            summary.Success = success;
            summary.Failure = success ? FailureReason.None : FailureReason.PlayerKilled;
            summary.Difficulty = difficulty;
            summary.ElapsedSeconds = 300f;
            summary.Kills = kills;
            summary.Headshots = 4;
            summary.Deaths = success ? 0 : 1;
            summary.ObjectivesCompleted = objectivesDone;
            summary.ObjectivesTotal = objectivesTotal;
            summary.LootValue = lootValue;
            summary.LootCollected = 4;
            return summary;
        }

        [Test]
        public void SuccessfulExtractionPaysFullReward()
        {
            MissionDef def = _content.GetMission("mission_rust_ridge");
            MissionRewards rewards = RewardCalculator.Compute(Summary(true, 1, 12, 3, 3, 900), def, _progression);

            Assert.Greater(rewards.Credits, 0);
            Assert.Greater(rewards.Xp, 0);
            Assert.Greater(rewards.LootRolls, 0);
            Assert.IsTrue(rewards.Extracted);
            Assert.IsFalse(string.IsNullOrEmpty(rewards.ClaimToken));
            StringAssert.Contains(":X", rewards.ClaimToken);
        }

        [Test]
        public void FailedRunPaysSalvageOnlyAndLessThanSuccess()
        {
            MissionDef def = _content.GetMission("mission_rust_ridge");
            MissionRewards success = RewardCalculator.Compute(Summary(true, 1, 12, 3, 3, 900), def, _progression);
            MissionRewards failed = RewardCalculator.Compute(Summary(false, 1, 12, 2, 3, 900), def, _progression);

            Assert.Less(failed.Credits, success.Credits);
            Assert.Less(failed.Xp, success.Xp);
            Assert.AreEqual(0, failed.LootRolls, "a failed run must not roll mission loot");
            Assert.AreEqual(0, failed.Materials);
            Assert.Greater(failed.SalvageCredits, 0);
            StringAssert.Contains(":F", failed.ClaimToken);
        }

        [Test]
        public void FailedRunWithNoLootPaysNothing()
        {
            MissionDef def = _content.GetMission("mission_rust_ridge");
            MissionRewards failed = RewardCalculator.Compute(Summary(false, 1, 0, 0, 3, 0), def, _progression);
            Assert.AreEqual(0, failed.Credits);
            Assert.AreEqual(0, failed.SalvageCredits);
        }

        [Test]
        public void SalvageIsCappedSoFailureCannotBeatSuccess()
        {
            MissionDef def = _content.GetMission("mission_rust_ridge");
            MissionRewards failed = RewardCalculator.Compute(Summary(false, 1, 0, 3, 3, 100000), def, _progression);
            MissionRewards success = RewardCalculator.Compute(Summary(true, 1, 0, 3, 3, 100000), def, _progression);
            Assert.Less(failed.Credits, success.Credits);
        }

        [Test]
        public void HigherDifficultyPaysMore()
        {
            MissionDef easy = _content.GetMission("mission_rust_ridge");
            MissionDef hard = _content.GetMission("mission_ashfall_citadel");
            MissionRewards easyRewards = RewardCalculator.Compute(Summary(true, 1, 12, 3, 3, 500), easy, _progression);
            MissionRewards hardRewards = RewardCalculator.Compute(Summary(true, 5, 12, 3, 3, 500), hard, _progression);
            Assert.Greater(hardRewards.Credits, easyRewards.Credits);
            Assert.Greater(hardRewards.Xp, easyRewards.Xp);
        }

        [Test]
        public void ScoreAndRankAreDeterministic()
        {
            MissionSummary summary = Summary(true, 3, 20, 4, 4, 1500);
            int scoreA = RewardCalculator.ComputeScore(summary);
            int scoreB = RewardCalculator.ComputeScore(summary);
            Assert.AreEqual(scoreA, scoreB);
            Assert.AreEqual(RewardCalculator.RankFor(scoreA, 3), RewardCalculator.RankFor(scoreB, 3));
            Assert.Greater(scoreA, 0);
        }

        [Test]
        public void DeathsReduceScoreButNeverBelowZero()
        {
            MissionSummary clean = Summary(true, 2, 10, 3, 3, 0);
            MissionSummary bloody = Summary(true, 2, 10, 3, 3, 0);
            bloody.Deaths = 50;
            Assert.Less(RewardCalculator.ComputeScore(bloody), RewardCalculator.ComputeScore(clean));
            Assert.GreaterOrEqual(RewardCalculator.ComputeScore(bloody), 0);
        }

        [Test]
        public void ClaimTokensAreUniquePerRun()
        {
            string a = RewardCalculator.MakeClaimToken("mission_x", 1, true);
            string b = RewardCalculator.MakeClaimToken("mission_x", 2, true);
            string c = RewardCalculator.MakeClaimToken("mission_x", 1, false);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void LedgerGrantsOncePerToken()
        {
            RewardLedger ledger = new RewardLedger(null, 64);
            string token = RewardCalculator.MakeClaimToken("mission_x", 7, true);

            Assert.AreEqual(RewardClaimResult.Granted, ledger.Claim(token));
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, ledger.Claim(token));
            Assert.AreEqual(RewardClaimResult.InvalidToken, ledger.Claim(null));
            Assert.AreEqual(1, ledger.ClaimedCount);
        }

        [Test]
        public void LedgerRejectsAlreadyBankedTokenRestoredFromSave()
        {
            var saved = new List<string> { "mission_rust_ridge:0000002A:X" };
            RewardLedger ledger = new RewardLedger(saved, 64);
            Assert.AreEqual(RewardClaimResult.AlreadyClaimed, ledger.Claim("mission_rust_ridge:0000002A:X"));
            Assert.IsTrue(ledger.IsClaimed("mission_rust_ridge:0000002A:X"));
            Assert.AreEqual(1, ledger.Snapshot().Count);
        }

        [Test]
        public void LedgerRevokeAllowsRetryAfterAFailedPayout()
        {
            RewardLedger ledger = new RewardLedger(null, 64);
            Assert.AreEqual(RewardClaimResult.Granted, ledger.Claim("token"));
            ledger.Revoke("token");
            Assert.AreEqual(RewardClaimResult.Granted, ledger.Claim("token"));
        }

        [Test]
        public void RunIdIsStableForSameMissionAndAttempt()
        {
            int a = RewardCalculator.MakeRunId("mission_rust_ridge", 1, 99);
            int b = RewardCalculator.MakeRunId("mission_rust_ridge", 1, 99);
            int c = RewardCalculator.MakeRunId("mission_rust_ridge", 2, 99);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }
    }
}
