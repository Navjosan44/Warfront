using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public enum RewardClaimResult
    {
        Granted = 0,
        AlreadyClaimed = 1,
        InvalidToken = 2,
        RunStillActive = 3
    }

    /// <summary>
    /// Anti-duplication ledger for mission rewards.
    ///
    /// Every completed or failed run produces one token (mission + run id + outcome). A token can be
    /// banked exactly once, and the tokens are persisted with the save, so replaying a results screen,
    /// restoring a backup in the middle of payout, or re-entering the scene can never pay twice.
    /// </summary>
    public class RewardLedger
    {
        private readonly HashSet<string> _claimed = new HashSet<string>();
        private readonly int _maxHistory;

        public RewardLedger(IEnumerable<string> existingTokens, int maxHistory)
        {
            _maxHistory = Mathf.Max(32, maxHistory);
            if (existingTokens == null) return;
            foreach (string token in existingTokens)
            {
                if (!string.IsNullOrEmpty(token)) _claimed.Add(token);
            }
        }

        public int ClaimedCount { get { return _claimed.Count; } }

        public bool IsClaimed(string token)
        {
            return !string.IsNullOrEmpty(token) && _claimed.Contains(token);
        }

        /// <summary>
        /// Marks a token as claimed. Returns Granted only the first time - the caller must apply the
        /// reward exactly when this returns Granted.
        /// </summary>
        public RewardClaimResult Claim(string token)
        {
            if (string.IsNullOrEmpty(token)) return RewardClaimResult.InvalidToken;
            if (_claimed.Contains(token)) return RewardClaimResult.AlreadyClaimed;
            _claimed.Add(token);
            return RewardClaimResult.Granted;
        }

        /// <summary>Rolls a claim back if applying the reward failed halfway.</summary>
        public void Revoke(string token)
        {
            if (!string.IsNullOrEmpty(token)) _claimed.Remove(token);
        }

        public List<string> Snapshot()
        {
            var list = new List<string>(_claimed);
            // Keep the ledger bounded so a long-lived profile cannot grow the save forever.
            if (list.Count > _maxHistory)
            {
                list.Sort();
                list.RemoveRange(0, list.Count - _maxHistory);
            }
            return list;
        }
    }
}
