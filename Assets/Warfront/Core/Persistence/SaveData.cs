using System;
using System.Collections.Generic;

namespace Warfront.Core
{
    /// <summary>
    /// Save schema. Everything is a plain serializable class with public fields because
    /// Unity's JsonUtility cannot serialize dictionaries, interfaces or properties.
    /// Bump SaveSchema.CurrentVersion and add a migration step whenever this changes.
    /// </summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 3;

        /// <summary>Version at which the integrity hash was introduced (older saves skip the check).</summary>
        public const int IntegrityVersion = 3;
    }

    [Serializable]
    public class WeaponUpgradeSave
    {
        public string WeaponId;
        public int Level = 1;
    }

    [Serializable]
    public class WeaponRaritySave
    {
        public string WeaponId;
        public int Rarity;
    }

    [Serializable]
    public class MissionRecordSave
    {
        public string MissionId;
        public bool Completed;
        public bool Extracted;
        public int TimesCompleted;
        public int TimesFailed;
        public float BestTimeSeconds;
        public int BestScore;
    }

    [Serializable]
    public class ProgressionSave
    {
        public int Level = 1;
        public int Xp;
        public int TotalXp;
        public int Credits;
        public int Materials;
        public int MissionsCompleted;
        public int TotalKills;
        public int TotalDeaths;
        public int TotalExtractions;
        public float TotalPlaySeconds;
        public string SelectedMissionId;
        public List<WeaponUpgradeSave> WeaponUpgrades = new List<WeaponUpgradeSave>();
        public List<WeaponRaritySave> WeaponRarities = new List<WeaponRaritySave>();
        public List<string> UnlockedWeapons = new List<string>();
        public List<string> Achievements = new List<string>();
        public List<MissionRecordSave> Missions = new List<MissionRecordSave>();
    }

    [Serializable]
    public class ItemStackSave
    {
        public string ItemId;
        public int Quantity;
        public int Rarity;
        public int UpgradeLevel = 1;
    }

    [Serializable]
    public class InventorySave
    {
        public int Capacity = InventoryModel.DefaultCapacity;
        public List<ItemStackSave> Slots = new List<ItemStackSave>();
        public List<ItemStackSave> Weapons = new List<ItemStackSave>();
        public int LightAmmo;
        public int HeavyAmmo;
        public int ShellAmmo;
        public int SniperAmmo;

        public int GetAmmo(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Heavy: return HeavyAmmo;
                case AmmoType.Shell: return ShellAmmo;
                case AmmoType.Sniper: return SniperAmmo;
                default: return LightAmmo;
            }
        }

        public void SetAmmo(AmmoType type, int value)
        {
            switch (type)
            {
                case AmmoType.Heavy: HeavyAmmo = value; break;
                case AmmoType.Shell: ShellAmmo = value; break;
                case AmmoType.Sniper: SniperAmmo = value; break;
                default: LightAmmo = value; break;
            }
        }
    }

    [Serializable]
    public class LoadoutSave
    {
        public string PrimaryWeaponId;
        public string SecondaryWeaponId;
        public string SidearmWeaponId;
        public string EquipmentItemId = "med_kit_small";
        public int EquipmentCount = 2;
    }

    [Serializable]
    public class SettingsSave
    {
        public int SchemaVersion = 1;
        public float MasterVolume = 0.85f;
        public float MusicVolume = 0.6f;
        public float SfxVolume = 1f;
        public float LookSensitivity = 1f;
        public bool InvertY;
        public bool AimAssist = true;
        public bool Vibration = true;
        public int QualityPreset = 2;
        public bool DynamicResolution = true;
        public int TargetFrameRate = 60;
        public bool ShowDamageNumbers = true;
        public bool LeftHandedControls;
        public float JoystickScale = 1f;
        public bool AutoFire = false;
    }

    /// <summary>Root document written to disk.</summary>
    [Serializable]
    public class SaveRoot
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public double SavedAtUnix;
        public float TotalPlaySeconds;
        public string IntegrityHash;

        public ProgressionSave Progression = new ProgressionSave();
        public InventorySave Inventory = new InventorySave();
        public LoadoutSave Loadout = new LoadoutSave();
        public SettingsSave Settings = new SettingsSave();

        /// <summary>
        /// Ids of mission completed runs whose rewards were already banked. This is the offline
        /// defence against reward duplication (replaying a results screen / restoring a backup).
        /// </summary>
        public List<string> ClaimedRewardTokens = new List<string>();

        public static SaveRoot CreateDefault()
        {
            SaveRoot root = new SaveRoot();
            root.SchemaVersion = SaveSchema.CurrentVersion;
            root.Progression = new ProgressionSave();
            root.Inventory = new InventorySave();
            root.Loadout = new LoadoutSave();
            root.Settings = new SettingsSave();
            root.ClaimedRewardTokens = new List<string>();
            return root;
        }
    }

    /// <summary>Result of a load attempt, including how it was recovered.</summary>
    public enum SaveLoadStatus
    {
        Loaded = 0,
        LoadedFromBackup = 1,
        CreatedDefault = 2,
        RecoveredFromCorruption = 3
    }

    public struct SaveLoadResult
    {
        public SaveLoadStatus Status;
        public SaveRoot Data;
        public string Message;
        public List<string> Warnings;
    }
}
