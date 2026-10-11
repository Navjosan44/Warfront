using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public enum MissionState
    {
        Inactive = 0,
        Active = 1,
        Success = 2,
        Failed = 3
    }

    public enum FailureReason
    {
        None = 0,
        PlayerKilled = 1,
        TimeExpired = 2,
        ObjectiveLost = 3,
        Abandoned = 4
    }

    /// <summary>
    /// Authoritative mission state machine. The runtime MissionRuntime MonoBehaviour feeds events
    /// in and reads state out; no gameplay rule lives in the MonoBehaviours themselves.
    /// </summary>
    public class MissionModel
    {
        private readonly List<ObjectiveProgress> _objectives = new List<ObjectiveProgress>(4);
        private readonly List<LootDrop> _collectedLoot = new List<LootDrop>(32);
        private readonly ContentDatabase _content;

        public event Action<ObjectiveProgress> ObjectiveProgressed;
        public event Action<ObjectiveProgress> ObjectiveCompleted;
        public event Action<MissionModel> MissionSucceeded;
        public event Action<MissionModel, FailureReason> MissionFailedEvent;

        public MissionModel(MissionDef def, ContentDatabase content, int runId)
        {
            Def = def;
            _content = content;
            RunId = runId;
            Extraction = new ExtractionModel(def != null ? def.ExtractionHoldSeconds : 12f);
            if (def != null)
            {
                for (int i = 0; i < def.Objectives.Count; i++) _objectives.Add(new ObjectiveProgress(def.Objectives[i]));
            }
        }

        public MissionDef Def { get; private set; }
        public int RunId { get; private set; }
        public MissionState State { get; private set; } = MissionState.Inactive;
        public FailureReason Failure { get; private set; } = FailureReason.None;
        public float ElapsedSeconds { get; private set; }
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public int Headshots { get; private set; }
        public int LootPickedUp { get; private set; }
        public ExtractionModel Extraction { get; private set; }
        public IList<ObjectiveProgress> Objectives { get { return _objectives; } }
        public IList<LootDrop> CollectedLoot { get { return _collectedLoot; } }
        public bool IsRunning { get { return State == MissionState.Active; } }

        public float TimeRemaining
        {
            get
            {
                if (Def == null || Def.TimeLimitSeconds <= 0f) return -1f;
                return Mathf.Max(0f, Def.TimeLimitSeconds - ElapsedSeconds);
            }
        }

        public void Begin()
        {
            if (State == MissionState.Active) return;
            State = MissionState.Active;
            Failure = FailureReason.None;
            ElapsedSeconds = 0f;
            Kills = 0;
            Deaths = 0;
            Headshots = 0;
            LootPickedUp = 0;
            _collectedLoot.Clear();
            for (int i = 0; i < _objectives.Count; i++) _objectives[i].ResetProgress();
            Extraction.Reset(Def != null ? Def.ExtractionHoldSeconds : 12f);
        }

        public bool AllRequiredComplete
        {
            get
            {
                for (int i = 0; i < _objectives.Count; i++)
                {
                    ObjectiveProgress progress = _objectives[i];
                    if (progress.Required && !progress.Completed) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Gate for bringing the extraction beacon online. The ReachExtraction objective is excluded
        /// because completing it IS entering the zone - including it here would deadlock the run.
        /// </summary>
        public bool AllObjectivesCompleteExceptExtraction
        {
            get
            {
                for (int i = 0; i < _objectives.Count; i++)
                {
                    ObjectiveProgress progress = _objectives[i];
                    if (!progress.Required) continue;
                    if (progress.Def.Type == ObjectiveType.ReachExtraction) continue;
                    if (!progress.Completed) return false;
                }
                return true;
            }
        }

        /// <summary>Called when the player steps into the extraction zone.</summary>
        public bool NotifyExtractionZoneEntered()
        {
            if (State != MissionState.Active) return false;
            bool any = false;
            for (int i = 0; i < _objectives.Count; i++)
            {
                ObjectiveProgress progress = _objectives[i];
                if (progress.Def.Type != ObjectiveType.ReachExtraction || progress.Completed) continue;
                any = Complete(progress) || any;
            }
            return any;
        }

        public int CompletedObjectiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _objectives.Count; i++)
                {
                    if (_objectives[i].Completed) count++;
                }
                return count;
            }
        }

        /// <summary>Feeds the mission clock and extraction contract.</summary>
        public void Tick(float deltaTime, bool playerInZone, Vector3 playerPosition, Vector3 zonePosition, float zoneRadius)
        {
            if (State != MissionState.Active) return;

            float dt = Mathf.Max(0f, deltaTime);
            ElapsedSeconds += dt;

            for (int i = 0; i < _objectives.Count; i++)
            {
                if (_objectives[i].Def.Type == ObjectiveType.SurviveTime) _objectives[i].Tick(dt);
            }

            // SurviveTime objectives auto complete when their clock is done.
            for (int i = 0; i < _objectives.Count; i++)
            {
                ObjectiveProgress progress = _objectives[i];
                if (progress.Def.Type != ObjectiveType.SurviveTime || progress.Completed) continue;
                if (ElapsedSeconds + 0.0001f >= progress.TargetCount) Complete(progress);
            }

            if (playerInZone) NotifyExtractionZoneEntered();

            bool canUnlock = AllObjectivesCompleteExceptExtraction;
            if (canUnlock) Extraction.Unlock();
            if (Extraction.Tick(dt, playerInZone, canUnlock))
            {
                Succeed();
                return;
            }

            if (Def != null && Def.TimeLimitSeconds > 0f && ElapsedSeconds >= Def.TimeLimitSeconds)
            {
                Fail(FailureReason.TimeExpired);
            }
        }

        public bool ReportProgress(ObjectiveType type, string tag, int amount)
        {
            if (State != MissionState.Active) return false;
            bool any = false;
            for (int i = 0; i < _objectives.Count; i++)
            {
                ObjectiveProgress progress = _objectives[i];
                if (progress.Def.Type != type || progress.Completed) continue;
                if (!MatchesTag(progress.Def.TargetTag, tag)) continue;

                bool reachedTarget = progress.Add(amount);
                any = true;
                if (ObjectiveProgressed != null) ObjectiveProgressed(progress);
                // Add() already flipped the completed flag, so fire the completion here rather than
                // routing through Complete() (which would see an already-completed objective and bail).
                if (reachedTarget) NotifyObjectiveCompleted(progress);
            }
            if (any && AllObjectivesCompleteExceptExtraction) Extraction.Unlock();
            return any;
        }

        public bool CompleteObjective(string objectiveId)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (_objectives[i].Def.Id != objectiveId) continue;
                return Complete(_objectives[i]);
            }
            return false;
        }

        /// <summary>Marks an objective complete directly (reach extraction, survive timer, scripted).</summary>
        private bool Complete(ObjectiveProgress progress)
        {
            if (progress.Completed) return false;
            progress.Complete();
            NotifyObjectiveCompleted(progress);
            return true;
        }

        private void NotifyObjectiveCompleted(ObjectiveProgress progress)
        {
            if (ObjectiveCompleted != null) ObjectiveCompleted(progress);
            if (AllObjectivesCompleteExceptExtraction) Extraction.Unlock();
        }

        private static bool MatchesTag(string objectiveTag, string tag)
        {
            if (string.IsNullOrEmpty(objectiveTag)) return true;
            return string.Equals(objectiveTag, tag, StringComparison.OrdinalIgnoreCase);
        }

        public void RegisterKill(bool headshot, bool wasTarget)
        {
            if (State != MissionState.Active) return;
            Kills++;
            if (headshot) Headshots++;
            ReportProgress(ObjectiveType.EliminateCount, null, 1);
            if (wasTarget) ReportProgress(ObjectiveType.EliminateTarget, null, 1);
        }

        public void RegisterDeath()
        {
            Deaths++;
        }

        public void RegisterLoot(LootDrop drop)
        {
            if (!drop.IsValid()) return;
            LootPickedUp += Mathf.Max(1, drop.Quantity);
            _collectedLoot.Add(drop);
        }

        public bool Succeed()
        {
            if (State != MissionState.Active) return false;
            if (!AllRequiredComplete) return false;
            if (!Extraction.TryConsumeReward()) return false;
            State = MissionState.Success;
            if (MissionSucceeded != null) MissionSucceeded(this);
            return true;
        }

        public void Fail(FailureReason reason)
        {
            if (State != MissionState.Active) return;
            State = MissionState.Failed;
            Failure = reason;
            Extraction.Fail();
            if (MissionFailedEvent != null) MissionFailedEvent(this, reason);
        }

        public MissionSummary BuildSummary()
        {
            MissionSummary summary;
            summary.MissionId = Def != null ? Def.Id : null;
            summary.RunId = RunId;
            summary.Success = State == MissionState.Success;
            summary.Failure = Failure;
            summary.Difficulty = Def != null ? Def.Difficulty : 1;
            summary.ElapsedSeconds = ElapsedSeconds;
            summary.Kills = Kills;
            summary.Headshots = Headshots;
            summary.Deaths = Deaths;
            summary.ObjectivesCompleted = CompletedObjectiveCount;
            summary.ObjectivesTotal = _objectives.Count;
            summary.LootValue = LootValue();
            summary.LootCollected = _collectedLoot.Count;
            return summary;
        }

        public int LootValue()
        {
            int value = 0;
            for (int i = 0; i < _collectedLoot.Count; i++)
            {
                ItemDef item = _content != null ? _content.GetItem(_collectedLoot[i].ItemId) : null;
                if (item == null) continue;
                value += item.Value * Mathf.Max(1, _collectedLoot[i].Quantity);
            }
            return value;
        }
    }

    public struct MissionSummary
    {
        public string MissionId;
        public int RunId;
        public bool Success;
        public FailureReason Failure;
        public int Difficulty;
        public float ElapsedSeconds;
        public int Kills;
        public int Headshots;
        public int Deaths;
        public int ObjectivesCompleted;
        public int ObjectivesTotal;
        public int LootValue;
        public int LootCollected;

        public bool IsValid { get { return !string.IsNullOrEmpty(MissionId); } }
    }

    public static class LootDropExtensions
    {
        public static bool IsValid(this LootDrop drop)
        {
            return !string.IsNullOrEmpty(drop.ItemId) && drop.Quantity > 0;
        }
    }
}
