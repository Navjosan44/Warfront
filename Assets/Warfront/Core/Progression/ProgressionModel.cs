using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public struct MissionRecord
    {
        public string MissionId;
        public bool Completed;
        public int TimesCompleted;
        public int TimesFailed;
        public float BestTimeSeconds;
        public int BestScore;
        public bool Extracted;
    }

    public struct UpgradeResult
    {
        public bool Success;
        public string Reason;
        public int NewLevel;
        public int CreditsSpent;
        public int MaterialsSpent;
    }

    /// <summary>
    /// Authoritative progression state. The runtime (ProgressionService) only forwards events into
    /// this model, so XP curves, costs and anti-cheat clamps are unit tested without the engine.
    /// </summary>
    public class ProgressionModel
    {
        public const int MaxLevel = 50;
        public const int MaxWeaponUpgrade = 9;
        public const int MaxCredits = 99_999_999;

        private readonly ContentDatabase _content;
        private readonly Dictionary<string, int> _weaponUpgrades = new Dictionary<string, int>(16);
        private readonly Dictionary<string, Rarity> _weaponRarities = new Dictionary<string, Rarity>(16);
        private readonly HashSet<string> _unlockedWeapons = new HashSet<string>();
        private readonly Dictionary<string, MissionRecord> _missions = new Dictionary<string, MissionRecord>(16);
        private readonly HashSet<string> _achievements = new HashSet<string>();

        public event Action<int, int> LevelChanged;
        public event Action<int> XpGained;
        public event Action<string> WeaponUnlocked;
        public event Action<string, int> WeaponUpgraded;
        public event Action<string> AchievementUnlocked;

        public ProgressionModel(ContentDatabase content)
        {
            _content = content;
        }

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int TotalXp { get; private set; }
        public int Credits { get; private set; }
        public int Materials { get; private set; }
        public int MissionsCompleted { get; private set; }
        public int TotalKills { get; private set; }
        public int TotalDeaths { get; private set; }
        public int TotalExtractions { get; private set; }
        public float TotalPlaySeconds { get; private set; }
        public string SelectedMissionId { get; private set; }

        public IEnumerable<string> UnlockedWeapons { get { return _unlockedWeapons; } }
        public IEnumerable<string> Achievements { get { return _achievements; } }

        public static int XpRequiredForLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, MaxLevel);
            return Mathf.RoundToInt(650f + 420f * Mathf.Pow(clamped, 1.35f));
        }

        public int XpForNextLevel { get { return Level >= MaxLevel ? 0 : XpRequiredForLevel(Level); } }

        public float LevelProgress01
        {
            get
            {
                if (Level >= MaxLevel) return 1f;
                int need = XpForNextLevel;
                return need <= 0 ? 1f : WfMath.Clamp01((float)Xp / need);
            }
        }

        public void AddXp(int amount)
        {
            int gain = Mathf.Clamp(amount, 0, 1_000_000);
            if (gain <= 0) return;

            Xp += gain;
            TotalXp += gain;
            if (XpGained != null) XpGained(gain);

            int previousLevel = Level;
            while (Level < MaxLevel && Xp >= XpRequiredForLevel(Level))
            {
                Xp -= XpRequiredForLevel(Level);
                Level++;
            }
            if (Level >= MaxLevel) Xp = Mathf.Min(Xp, XpRequiredForLevel(MaxLevel));

            if (Level != previousLevel && LevelChanged != null) LevelChanged(Level, previousLevel);
        }

        public void AddCredits(int amount)
        {
            // Integer maths only: float rounding around 1e8 could grant a credit the cap forbids.
            long total = (long)Credits + amount;
            if (total < 0L) total = 0L;
            if (total > MaxCredits) total = MaxCredits;
            Credits = (int)total;
        }

        public void AddMaterials(int amount)
        {
            long total = (long)Materials + amount;
            if (total < 0L) total = 0L;
            if (total > 99999L) total = 99999L;
            Materials = (int)total;
        }

        public bool CanAfford(int credits)
        {
            return credits >= 0 && Credits >= credits;
        }

        public bool SpendCredits(int credits)
        {
            if (credits < 0 || Credits < credits) return false;
            Credits -= credits;
            return true;
        }

        public void RegisterKills(int count) { TotalKills += Mathf.Clamp(count, 0, 10000); }
        public void RegisterDeath() { TotalDeaths++; }
        public void RegisterExtraction() { TotalExtractions++; }
        public void RegisterPlayTime(float seconds) { TotalPlaySeconds += Mathf.Clamp(seconds, 0f, 86400f); }
        public void SetSelectedMission(string missionId) { SelectedMissionId = missionId; }

        public int GetWeaponUpgradeLevel(string weaponId)
        {
            int level;
            return weaponId != null && _weaponUpgrades.TryGetValue(weaponId, out level) ? level : 1;
        }

        public Rarity GetWeaponRarity(string weaponId)
        {
            Rarity rarity;
            return weaponId != null && _weaponRarities.TryGetValue(weaponId, out rarity) ? rarity : Rarity.Common;
        }

        public bool IsWeaponUnlocked(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            WeaponDef def = _content != null ? _content.GetWeapon(weaponId) : null;
            if (def != null && def.UnlockLevel <= Level && def.UnlockCost <= 0) return true;
            return _unlockedWeapons.Contains(weaponId);
        }

        public bool UnlockWeapon(string weaponId)
        {
            WeaponDef def = _content != null ? _content.GetWeapon(weaponId) : null;
            if (def == null) return false;
            if (IsWeaponUnlocked(weaponId)) return false;
            if (Level < def.UnlockLevel) return false;
            if (def.UnlockCost > 0 && !SpendCredits(def.UnlockCost)) return false;

            _unlockedWeapons.Add(weaponId);
            if (!_weaponUpgrades.ContainsKey(weaponId)) _weaponUpgrades[weaponId] = 1;
            if (WeaponUnlocked != null) WeaponUnlocked(weaponId);
            return true;
        }

        public void GrantWeapon(string weaponId, Rarity rarity)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            _unlockedWeapons.Add(weaponId);
            if (!_weaponUpgrades.ContainsKey(weaponId)) _weaponUpgrades[weaponId] = 1;
            Rarity current = GetWeaponRarity(weaponId);
            _weaponRarities[weaponId] = (Rarity)Mathf.Max((int)current, (int)rarity);
        }

        public UpgradeResult UpgradeWeapon(string weaponId)
        {
            UpgradeResult result;
            result.Success = false;
            result.Reason = null;
            result.NewLevel = 1;
            result.CreditsSpent = 0;
            result.MaterialsSpent = 0;

            WeaponDef def = _content != null ? _content.GetWeapon(weaponId) : null;
            if (def == null)
            {
                result.Reason = "Unknown weapon";
                return result;
            }
            if (!IsWeaponUnlocked(weaponId))
            {
                result.Reason = "Weapon not unlocked";
                return result;
            }

            int level = GetWeaponUpgradeLevel(weaponId);
            int maxLevel = Mathf.Min(def.MaxUpgradeLevel, MaxWeaponUpgrade);
            if (level >= maxLevel)
            {
                result.Reason = "Already at maximum level";
                result.NewLevel = level;
                return result;
            }

            int credits;
            int materials;
            WeaponStatRules.UpgradeCost(def, level, out credits, out materials);
            if (!CanAfford(credits))
            {
                result.Reason = "Not enough credits";
                result.NewLevel = level;
                return result;
            }
            if (Materials < materials)
            {
                result.Reason = "Not enough materials";
                result.NewLevel = level;
                return result;
            }

            Credits -= credits;
            Materials -= materials;
            int newLevel = level + 1;
            _weaponUpgrades[weaponId] = newLevel;

            result.Success = true;
            result.NewLevel = newLevel;
            result.CreditsSpent = credits;
            result.MaterialsSpent = materials;
            if (WeaponUpgraded != null) WeaponUpgraded(weaponId, newLevel);
            return result;
        }

        public MissionRecord GetMissionRecord(string missionId)
        {
            MissionRecord record = default(MissionRecord);
            if (missionId != null && _missions.TryGetValue(missionId, out record)) return record;
            record.MissionId = missionId;
            return record;
        }

        public void RecordMissionSuccess(string missionId, float timeSeconds, int score)
        {
            if (string.IsNullOrEmpty(missionId)) return;
            MissionRecord record = GetMissionRecord(missionId);
            bool first = !record.Completed;
            record.Completed = true;
            record.Extracted = true;
            record.TimesCompleted++;
            record.BestScore = Mathf.Max(record.BestScore, Mathf.Max(0, score));
            if (timeSeconds > 0f && (record.BestTimeSeconds <= 0f || timeSeconds < record.BestTimeSeconds))
            {
                record.BestTimeSeconds = timeSeconds;
            }
            _missions[missionId] = record;
            if (first) MissionsCompleted++;
        }

        public void RecordMissionFailure(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return;
            MissionRecord record = GetMissionRecord(missionId);
            record.TimesFailed++;
            _missions[missionId] = record;
        }

        public bool IsMissionUnlocked(string missionId)
        {
            MissionDef def = _content != null ? _content.GetMission(missionId) : null;
            if (def == null) return false;
            if (def.UnlockLevel > Level) return false;
            if (string.IsNullOrEmpty(def.RequiredMissionId)) return true;
            return GetMissionRecord(def.RequiredMissionId).Completed;
        }

        public bool UnlockAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!_achievements.Add(id)) return false;
            if (AchievementUnlocked != null) AchievementUnlocked(id);
            return true;
        }

        public bool HasAchievement(string id)
        {
            return !string.IsNullOrEmpty(id) && _achievements.Contains(id);
        }

        public void VisitMissionRecords(Action<MissionRecord> visitor)
        {
            if (visitor == null) return;
            foreach (KeyValuePair<string, MissionRecord> kv in _missions) visitor(kv.Value);
        }

        public Dictionary<string, int> WeaponUpgradeSnapshot()
        {
            return new Dictionary<string, int>(_weaponUpgrades);
        }

        public Dictionary<string, Rarity> WeaponRaritySnapshot()
        {
            return new Dictionary<string, Rarity>(_weaponRarities);
        }

        public List<string> UnlockSnapshot()
        {
            return new List<string>(_unlockedWeapons);
        }

        public List<string> AchievementSnapshot()
        {
            return new List<string>(_achievements);
        }

        public List<MissionRecord> MissionSnapshot()
        {
            return new List<MissionRecord>(_missions.Values);
        }

        /// <summary>Restores progression from save data, clamping every value to legal ranges.</summary>
        public void Restore(ProgressionSave saved)
        {
            if (saved == null) return;
            Level = WfMath.ClampInt(saved.Level <= 0 ? 1 : saved.Level, 1, MaxLevel);
            Xp = Mathf.Clamp(saved.Xp, 0, XpRequiredForLevel(Level));
            TotalXp = Mathf.Clamp(saved.TotalXp, 0, int.MaxValue);
            Credits = Mathf.Clamp(saved.Credits, 0, MaxCredits);
            Materials = Mathf.Clamp(saved.Materials, 0, 99999);
            MissionsCompleted = Mathf.Clamp(saved.MissionsCompleted, 0, 10000);
            TotalKills = Mathf.Clamp(saved.TotalKills, 0, int.MaxValue);
            TotalDeaths = Mathf.Clamp(saved.TotalDeaths, 0, int.MaxValue);
            TotalExtractions = Mathf.Clamp(saved.TotalExtractions, 0, int.MaxValue);
            TotalPlaySeconds = Mathf.Clamp(saved.TotalPlaySeconds, 0f, 100_000_000f);
            SelectedMissionId = saved.SelectedMissionId;

            _weaponUpgrades.Clear();
            if (saved.WeaponUpgrades != null)
            {
                for (int i = 0; i < saved.WeaponUpgrades.Count; i++)
                {
                    WeaponUpgradeSave entry = saved.WeaponUpgrades[i];
                    if (entry == null || string.IsNullOrEmpty(entry.WeaponId)) continue;
                    int max = _content != null && _content.GetWeapon(entry.WeaponId) != null
                        ? Mathf.Min(_content.GetWeapon(entry.WeaponId).MaxUpgradeLevel, MaxWeaponUpgrade)
                        : 1;
                    _weaponUpgrades[entry.WeaponId] = WfMath.ClampInt(entry.Level, 1, Mathf.Max(1, max));
                }
            }

            _weaponRarities.Clear();
            if (saved.WeaponRarities != null)
            {
                for (int i = 0; i < saved.WeaponRarities.Count; i++)
                {
                    WeaponRaritySave entry = saved.WeaponRarities[i];
                    if (entry == null || string.IsNullOrEmpty(entry.WeaponId)) continue;
                    _weaponRarities[entry.WeaponId] = (Rarity)WfMath.ClampInt(entry.Rarity, 0, (int)Rarity.Legendary);
                }
            }

            _unlockedWeapons.Clear();
            if (saved.UnlockedWeapons != null)
            {
                for (int i = 0; i < saved.UnlockedWeapons.Count; i++)
                {
                    string id = saved.UnlockedWeapons[i];
                    if (_content != null && _content.GetWeapon(id) != null) _unlockedWeapons.Add(id);
                }
            }

            _achievements.Clear();
            if (saved.Achievements != null)
            {
                for (int i = 0; i < saved.Achievements.Count; i++)
                {
                    if (!string.IsNullOrEmpty(saved.Achievements[i])) _achievements.Add(saved.Achievements[i]);
                }
            }

            _missions.Clear();
            if (saved.Missions != null)
            {
                for (int i = 0; i < saved.Missions.Count; i++)
                {
                    MissionRecordSave entry = saved.Missions[i];
                    if (entry == null || string.IsNullOrEmpty(entry.MissionId)) continue;
                    MissionRecord record;
                    record.MissionId = entry.MissionId;
                    record.Completed = entry.Completed;
                    record.Extracted = entry.Extracted;
                    record.TimesCompleted = Mathf.Clamp(entry.TimesCompleted, 0, 10000);
                    record.TimesFailed = Mathf.Clamp(entry.TimesFailed, 0, 10000);
                    record.BestTimeSeconds = Mathf.Clamp(entry.BestTimeSeconds, 0f, 100000f);
                    record.BestScore = Mathf.Clamp(entry.BestScore, 0, int.MaxValue);
                    _missions[entry.MissionId] = record;
                }
            }
        }

        /// <summary>Ensures the two starter weapons are always available - no soft lock on a fresh save.</summary>
        public void EnsureStarterWeapons(string[] starterWeaponIds)
        {
            if (starterWeaponIds == null) return;
            for (int i = 0; i < starterWeaponIds.Length; i++)
            {
                string id = starterWeaponIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (_content != null && _content.GetWeapon(id) == null) continue;
                _unlockedWeapons.Add(id);
                if (!_weaponUpgrades.ContainsKey(id)) _weaponUpgrades[id] = 1;
            }
        }
    }
}
