using UnityEngine;

namespace Warfront.Core
{
    public enum ExtractionState
    {
        Locked = 0,       // objectives not finished yet
        Available = 1,    // beacon live, zone idle
        Countdown = 2,    // player inside the zone, hold the timer
        Interrupted = 3,  // player left the zone, progress decays
        Complete = 4,     // extraction succeeded
        Failed = 5        // timed out or abandoned while the run ended
    }

    public struct ExtractionStatus
    {
        public ExtractionState State;
        public float HoldSeconds;
        public float RequiredSeconds;
        public float Progress01;
        public float DistanceToZone;
        public bool PlayerInZone;
        public string Label;
        public bool RewardClaimed;
    }

    /// <summary>
    /// The extraction contract: rewards are only ever granted once, and only after a completed
    /// hold. Leaving the zone loses progress but never the run, and a finished extraction can
    /// never be completed twice (anti-duplication).
    /// </summary>
    public class ExtractionModel
    {
        private float _holdTime;
        private bool _consumed;

        public ExtractionModel(float requiredSeconds)
        {
            RequiredSeconds = Mathf.Max(3f, requiredSeconds);
            State = ExtractionState.Locked;
        }

        public float RequiredSeconds { get; private set; }
        public ExtractionState State { get; private set; }
        public float HoldTime { get { return _holdTime; } }
        public float Progress01 { get { return WfMath.Clamp01(_holdTime / RequiredSeconds); } }
        public float SecondsRemaining { get { return Mathf.Max(0f, RequiredSeconds - _holdTime); } }
        public int RewardClaimCount { get; private set; }

        /// <summary>Unlocks once every required objective is complete. Only unlocks, never re-locks.</summary>
        public void Unlock()
        {
            if (State == ExtractionState.Locked) State = ExtractionState.Available;
        }

        public void Reset(float requiredSeconds)
        {
            RequiredSeconds = Mathf.Max(3f, requiredSeconds);
            _holdTime = 0f;
            _consumed = false;
            RewardClaimCount = 0;
            State = ExtractionState.Locked;
        }

        /// <summary>
        /// Advances the hold timer. Returns true on the frame extraction completes.
        /// </summary>
        public bool Tick(float deltaTime, bool playerInZone, bool objectivesComplete)
        {
            if (State == ExtractionState.Complete || State == ExtractionState.Failed) return false;
            if (objectivesComplete) Unlock();

            if (State == ExtractionState.Locked)
            {
                _holdTime = 0f;
                return false;
            }

            if (playerInZone)
            {
                State = ExtractionState.Countdown;
                _holdTime += Mathf.Max(0f, deltaTime);
                if (_holdTime >= RequiredSeconds)
                {
                    _holdTime = RequiredSeconds;
                    State = ExtractionState.Complete;
                    return true;
                }
                return false;
            }

            // Outside the zone the hold decays quickly - stepping out is a real cost.
            if (_holdTime > 0f)
            {
                _holdTime = Mathf.Max(0f, _holdTime - Mathf.Max(0f, deltaTime) * 2f);
                State = _holdTime > 0f ? ExtractionState.Interrupted : ExtractionState.Available;
            }
            else if (State == ExtractionState.Countdown || State == ExtractionState.Interrupted)
            {
                State = ExtractionState.Available;
            }
            return false;
        }

        public void Fail()
        {
            if (State == ExtractionState.Complete) return;
            State = ExtractionState.Failed;
        }

        /// <summary>Consumes the reward right exactly once. Prevents replaying the results screen.</summary>
        public bool TryConsumeReward()
        {
            if (State != ExtractionState.Complete || _consumed) return false;
            _consumed = true;
            RewardClaimCount++;
            return true;
        }

        public bool RewardClaimed { get { return _consumed; } }

        public ExtractionStatus GetStatus(Vector3 playerPosition, Vector3 zonePosition, float zoneRadius)
        {
            ExtractionStatus status;
            status.State = State;
            status.HoldSeconds = _holdTime;
            status.RequiredSeconds = RequiredSeconds;
            status.Progress01 = Progress01;
            float distance = Vector3.Distance(playerPosition, zonePosition) - Mathf.Max(0.1f, zoneRadius);
            status.DistanceToZone = Mathf.Max(0f, distance);
            status.PlayerInZone = distance <= 0f;
            status.RewardClaimed = _consumed;
            status.Label = Label();
            return status;
        }

        private string Label()
        {
            switch (State)
            {
                case ExtractionState.Locked: return "EXTRACTION LOCKED";
                case ExtractionState.Available: return "EXTRACTION AVAILABLE";
                case ExtractionState.Countdown: return "HOLD THE ZONE";
                case ExtractionState.Interrupted: return "HOLD INTERRUPTED";
                case ExtractionState.Complete: return "EXTRACTED";
                default: return "EXTRACTION FAILED";
            }
        }
    }
}
