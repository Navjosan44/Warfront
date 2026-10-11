using System;
using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Runtime wrapper around ProgressionModel.
    ///
    /// It mirrors progression into the save document, exposes the events the UI needs and centralises
    /// every unlock/upgrade request, so nothing else in the game can write to progression directly.
    /// </summary>
    public class ProgressionService
    {
        private readonly ContentDatabase _content;
        private readonly SaveService _save;

        public event Action<int, int> LevelChanged;
        public event Action<string, int> WeaponUpgraded;
        public event Action<string> WeaponUnlocked;
        public event Action<string> AchievementUnlocked;

        public ProgressionService(ContentDatabase content, SaveService save)
        {
            _content = content;
            _save = save;
            Model = new ProgressionModel(content);
        }

        public ProgressionModel Model { get; private set; }

        public void Reload()
        {
            Model = new ProgressionModel(_content);
            ProgressionSave block = _save != null && _save.Data != null ? _save.Data.Progression : null;
            Model.Restore(block);
            Model.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            HookEvents();
            LevelChanged?.Invoke(Model.Level, Model.Level);
        }

        private void HookEvents()
        {
            Model.LevelChanged += (level, previous) =>
            {
                Commit();
                if (LevelChanged != null) LevelChanged(level, previous);
                GameEvents.RaiseNotice("Level " + level + " reached");
            };
            Model.WeaponUnlocked += id =>
            {
                Commit();
                if (WeaponUnlocked != null) WeaponUnlocked(id);
            };
            Model.WeaponUpgraded += (id, level) =>
            {
                Commit();
                if (WeaponUpgraded != null) WeaponUpgraded(id, level);
                GameEvents.RaiseNotice("Upgraded to level " + level);
            };
            Model.AchievementUnlocked += id =>
            {
                Commit();
                if (AchievementUnlocked != null) AchievementUnlocked(id);
                GameEvents.RaiseNotice("Achievement unlocked");
            };
        }

        /// <summary>Writes the live model back into the save block and marks the save dirty.</summary>
        public void Commit()
        {
            if (_save == null || _save.Data == null) return;
            ProgressionSave block = _save.Data.Progression;
            if (block == null)
            {
                block = new ProgressionSave();
                _save.Data.Progression = block;
            }

            block.Level = Model.Level;
            block.Xp = Model.Xp;
            block.TotalXp = Model.TotalXp;
            block.Credits = Model.Credits;
            block.Materials = Model.Materials;
            block.MissionsCompleted = Model.MissionsCompleted;
            block.TotalKills = Model.TotalKills;
            block.TotalDeaths = Model.TotalDeaths;
            block.TotalExtractions = Model.TotalExtractions;
            block.TotalPlaySeconds = Model.TotalPlaySeconds;
            block.SelectedMissionId = Model.SelectedMissionId;

            block.WeaponUpgrades.Clear();
            Dictionary<string, int> upgrades = Model.WeaponUpgradeSnapshot();
            foreach (KeyValuePair<string, int> kv in upgrades)
            {
                block.WeaponUpgrades.Add(new WeaponUpgradeSave { WeaponId = kv.Key, Level = kv.Value });
            }

            block.WeaponRarities.Clear();
            Dictionary<string, Rarity> rarities = Model.WeaponRaritySnapshot();
            foreach (KeyValuePair<string, Rarity> kv in rarities)
            {
                block.WeaponRarities.Add(new WeaponRaritySave { WeaponId = kv.Key, Rarity = (int)kv.Value });
            }

            block.UnlockedWeapons.Clear();
            block.UnlockedWeapons.AddRange(Model.UnlockSnapshot());

            block.Achievements.Clear();
            block.Achievements.AddRange(Model.AchievementSnapshot());

            block.Missions.Clear();
            List<MissionRecord> records = Model.MissionSnapshot();
            for (int i = 0; i < records.Count; i++)
            {
                MissionRecord record = records[i];
                block.Missions.Add(new MissionRecordSave
                {
                    MissionId = record.MissionId,
                    Completed = record.Completed,
                    Extracted = record.Extracted,
                    TimesCompleted = record.TimesCompleted,
                    TimesFailed = record.TimesFailed,
                    BestTimeSeconds = record.BestTimeSeconds,
                    BestScore = record.BestScore
                });
            }

            _save.MarkDirty();
        }

        public void AddXp(int amount)
        {
            Model.AddXp(amount);
            Commit();
        }

        public void AddCredits(int amount)
        {
            Model.AddCredits(amount);
            Commit();
        }

        public void AddMaterials(int amount)
        {
            Model.AddMaterials(amount);
            Commit();
        }

        public bool UnlockWeapon(string weaponId)
        {
            bool unlocked = Model.UnlockWeapon(weaponId);
            if (unlocked) Commit();
            return unlocked;
        }

        public UpgradeResult UpgradeWeapon(string weaponId)
        {
            UpgradeResult result = Model.UpgradeWeapon(weaponId);
            if (result.Success) Commit();
            return result;
        }

        public bool IsMissionUnlocked(string missionId)
        {
            return Model.IsMissionUnlocked(missionId);
        }

        public void RecordMissionSuccess(string missionId, float timeSeconds, int score)
        {
            Model.RecordMissionSuccess(missionId, timeSeconds, score);
            Model.RegisterExtraction();
            Commit();
        }

        public void RecordMissionFailure(string missionId)
        {
            Model.RecordMissionFailure(missionId);
            Model.RegisterDeath();
            Commit();
        }

        public void AddPlayTime(float seconds)
        {
            Model.RegisterPlayTime(seconds);
        }

        public void SetSelectedMission(string missionId)
        {
            Model.SetSelectedMission(missionId);
            Commit();
        }

        public string SelectedMissionId
        {
            get { return Model.SelectedMissionId; }
        }

        /// <summary>Unlocks every weapon the player's level now qualifies for, free of charge if listed free.</summary>
        public void SyncLevelUnlocks()
        {
            List<WeaponDef> weapons = _content.AllWeapons();
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDef def = weapons[i];
                if (def.UnlockCost > 0) continue;
                if (def.UnlockLevel > Model.Level) continue;
                if (Model.IsWeaponUnlocked(def.Id)) continue;
                Model.GrantWeapon(def.Id, Rarity.Common);
            }
            Commit();
        }

        public List<WeaponDef> AvailableWeapons()
        {
            List<WeaponDef> all = _content.AllWeapons();
            var list = new List<WeaponDef>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (Model.IsWeaponUnlocked(all[i].Id)) list.Add(all[i]);
            }
            list.Sort((a, b) => a.UnlockLevel.CompareTo(b.UnlockLevel));
            return list;
        }

        public List<WeaponDef> LockedWeapons()
        {
            List<WeaponDef> all = _content.AllWeapons();
            var list = new List<WeaponDef>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!Model.IsWeaponUnlocked(all[i].Id)) list.Add(all[i]);
            }
            list.Sort((a, b) => a.UnlockLevel.CompareTo(b.UnlockLevel));
            return list;
        }
    }
}
