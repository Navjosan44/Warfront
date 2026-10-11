using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Pure save-data pipeline: serialize, validate, sanitize, hash and migrate.
    /// No file IO lives here so every rule is unit testable in isolation.
    /// </summary>
    public static class SaveSerializer
    {
        private const string IntegritySalt = "warfront-offline-v3";

        public static string Serialize(SaveRoot root)
        {
            if (root == null) return null;
            if (root.SchemaVersion <= 0) root.SchemaVersion = SaveSchema.CurrentVersion;
            root.SavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                root.IntegrityHash = ComputeIntegrityHash(root);
                return JsonUtility.ToJson(root, true);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Warfront] Save serialization failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Deserializes raw json. Never throws: a broken document produces null and the caller
        /// decides whether to fall back to the backup or to a fresh save.
        /// </summary>
        public static SaveRoot Deserialize(string json, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "empty document";
                return null;
            }

            SaveRoot root = null;
            try
            {
                root = JsonUtility.FromJson<SaveRoot>(json);
            }
            catch (Exception ex)
            {
                error = "parse error: " + ex.Message;
                return null;
            }

            if (root == null)
            {
                error = "null document";
                return null;
            }
            return root;
        }

        /// <summary>Runs validation + migration in the correct order and reports what changed.</summary>
        public static bool TryPrepare(SaveRoot root, out string error, List<string> warnings)
        {
            error = null;
            if (root == null)
            {
                error = "no data";
                return false;
            }

            if (root.SchemaVersion > SaveSchema.CurrentVersion)
            {
                error = "save was written by a newer version (" + root.SchemaVersion + ")";
                return false;
            }

            Migrate(root, warnings);
            Sanitize(root, warnings);
            return true;
        }

        /// <summary>Step-by-step schema migration. Each step upgrades one version and never fails hard.</summary>
        public static void Migrate(SaveRoot root, List<string> warnings)
        {
            if (root == null) return;
            if (root.SchemaVersion <= 0) root.SchemaVersion = 1;

            if (root.SchemaVersion < 2)
            {
                // v1 -> v2: loadouts and ammo pools became first class.
                if (root.Loadout == null) root.Loadout = new LoadoutSave();
                if (root.Inventory == null) root.Inventory = new InventorySave();
                root.SchemaVersion = 2;
                Warn(warnings, "Migrated save v1 -> v2");
            }

            if (root.SchemaVersion < 3)
            {
                // v2 -> v3: campaign claims + integrity hash added.
                if (root.ClaimedRewardTokens == null) root.ClaimedRewardTokens = new List<string>();
                root.SchemaVersion = 3;
                Warn(warnings, "Migrated save v2 -> v3");
            }
        }

        /// <summary>Clamps every field to legal ranges. Invalid data can never crash the game.</summary>
        public static void Sanitize(SaveRoot root, List<string> warnings)
        {
            if (root == null) return;

            if (root.Progression == null)
            {
                root.Progression = new ProgressionSave();
                Warn(warnings, "Progression block missing - defaults applied");
            }
            if (root.Inventory == null)
            {
                root.Inventory = new InventorySave();
                Warn(warnings, "Inventory block missing - defaults applied");
            }
            if (root.Loadout == null)
            {
                root.Loadout = new LoadoutSave();
                Warn(warnings, "Loadout block missing - defaults applied");
            }
            if (root.Settings == null)
            {
                root.Settings = new SettingsSave();
                Warn(warnings, "Settings block missing - defaults applied");
            }
            if (root.ClaimedRewardTokens == null) root.ClaimedRewardTokens = new List<string>();
            if (root.Progression.WeaponUpgrades == null) root.Progression.WeaponUpgrades = new List<WeaponUpgradeSave>();
            if (root.Progression.WeaponRarities == null) root.Progression.WeaponRarities = new List<WeaponRaritySave>();
            if (root.Progression.UnlockedWeapons == null) root.Progression.UnlockedWeapons = new List<string>();
            if (root.Progression.Achievements == null) root.Progression.Achievements = new List<string>();
            if (root.Progression.Missions == null) root.Progression.Missions = new List<MissionRecordSave>();
            if (root.Inventory.Slots == null) root.Inventory.Slots = new List<ItemStackSave>();
            if (root.Inventory.Weapons == null) root.Inventory.Weapons = new List<ItemStackSave>();

            ProgressionSave p = root.Progression;
            bool negativeCurrency = p.Credits < 0 || p.Materials < 0 || p.Xp < 0 || p.TotalXp < 0;
            p.Level = WfMath.ClampInt(p.Level <= 0 ? 1 : p.Level, 1, ProgressionModel.MaxLevel);
            p.Xp = Mathf.Clamp(p.Xp, 0, ProgressionModel.XpRequiredForLevel(p.Level));
            p.TotalXp = Mathf.Clamp(p.TotalXp, 0, int.MaxValue);
            p.Credits = Mathf.Clamp(p.Credits, 0, ProgressionModel.MaxCredits);
            p.Materials = Mathf.Clamp(p.Materials, 0, 99999);
            p.MissionsCompleted = Mathf.Clamp(p.MissionsCompleted, 0, 10000);
            p.TotalKills = Mathf.Clamp(p.TotalKills, 0, int.MaxValue);
            p.TotalDeaths = Mathf.Clamp(p.TotalDeaths, 0, int.MaxValue);
            p.TotalExtractions = Mathf.Clamp(p.TotalExtractions, 0, int.MaxValue);
            p.TotalPlaySeconds = WfMath.Clamp(p.TotalPlaySeconds, 0f, 100_000_000f);
            if (negativeCurrency) Warn(warnings, "Negative currency/xp corrected");

            for (int i = 0; i < p.WeaponUpgrades.Count; i++)
            {
                WeaponUpgradeSave entry = p.WeaponUpgrades[i];
                if (entry == null) continue;
                entry.Level = WfMath.ClampInt(entry.Level <= 0 ? 1 : entry.Level, 1, ProgressionModel.MaxWeaponUpgrade);
            }

            for (int i = 0; i < p.WeaponRarities.Count; i++)
            {
                WeaponRaritySave entry = p.WeaponRarities[i];
                if (entry == null) continue;
                entry.Rarity = WfMath.ClampInt(entry.Rarity, 0, (int)Rarity.Legendary);
            }

            for (int i = 0; i < p.Missions.Count; i++)
            {
                MissionRecordSave entry = p.Missions[i];
                if (entry == null) continue;
                entry.TimesCompleted = Mathf.Clamp(entry.TimesCompleted, 0, 10000);
                entry.TimesFailed = Mathf.Clamp(entry.TimesFailed, 0, 10000);
                entry.BestTimeSeconds = WfMath.Clamp(entry.BestTimeSeconds, 0f, 100000f);
                entry.BestScore = Mathf.Clamp(entry.BestScore, 0, int.MaxValue);
            }

            InventorySave inv = root.Inventory;
            int capacity = Mathf.Clamp(inv.Capacity <= 0 ? InventoryModel.DefaultCapacity : inv.Capacity, 4, 128);
            inv.Capacity = capacity;

            // Trim overflow slots rather than letting the inventory report impossible usage.
            if (inv.Slots.Count > capacity)
            {
                inv.Slots.RemoveRange(capacity, inv.Slots.Count - capacity);
                Warn(warnings, "Inventory overflow trimmed to capacity");
            }
            for (int i = 0; i < inv.Slots.Count; i++)
            {
                ItemStackSave slot = inv.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.ItemId) || slot.Quantity <= 0)
                {
                    if (slot != null)
                    {
                        slot.ItemId = null;
                        slot.Quantity = 0;
                    }
                    continue;
                }
                slot.Quantity = Mathf.Clamp(slot.Quantity, 1, 9999);
                slot.Rarity = WfMath.ClampInt(slot.Rarity, 0, (int)Rarity.Legendary);
                slot.UpgradeLevel = WfMath.ClampInt(slot.UpgradeLevel <= 0 ? 1 : slot.UpgradeLevel, 1, ProgressionModel.MaxWeaponUpgrade);
            }

            if (inv.Weapons.Count > InventoryModel.DefaultWeaponSlots)
            {
                inv.Weapons.RemoveRange(InventoryModel.DefaultWeaponSlots, inv.Weapons.Count - InventoryModel.DefaultWeaponSlots);
                Warn(warnings, "Weapon overflow trimmed");
            }

            inv.LightAmmo = Mathf.Clamp(inv.LightAmmo, 0, 99999);
            inv.HeavyAmmo = Mathf.Clamp(inv.HeavyAmmo, 0, 99999);
            inv.ShellAmmo = Mathf.Clamp(inv.ShellAmmo, 0, 99999);
            inv.SniperAmmo = Mathf.Clamp(inv.SniperAmmo, 0, 99999);

            LoadoutSave loadout = root.Loadout;
            if (loadout.EquipmentCount < 0) loadout.EquipmentCount = 0;
            if (loadout.EquipmentCount > 99) loadout.EquipmentCount = 99;

            // Duplicate claim tokens would break the anti-duplication ledger - dedupe defensively.
            HashSet<string> seen = new HashSet<string>();
            for (int i = root.ClaimedRewardTokens.Count - 1; i >= 0; i--)
            {
                string token = root.ClaimedRewardTokens[i];
                if (string.IsNullOrEmpty(token) || !seen.Add(token)) root.ClaimedRewardTokens.RemoveAt(i);
            }

            SettingsSave settings = root.Settings;
            settings.MasterVolume = WfMath.Clamp01(settings.MasterVolume);
            settings.MusicVolume = WfMath.Clamp01(settings.MusicVolume);
            settings.SfxVolume = WfMath.Clamp01(settings.SfxVolume);
            settings.LookSensitivity = WfMath.Clamp(settings.LookSensitivity <= 0f ? 1f : settings.LookSensitivity, 0.2f, 4f);
            settings.QualityPreset = WfMath.ClampInt(settings.QualityPreset, 0, 3);
            settings.TargetFrameRate = WfMath.ClampInt(settings.TargetFrameRate, 30, 120);
            settings.JoystickScale = WfMath.Clamp(settings.JoystickScale <= 0f ? 1f : settings.JoystickScale, 0.6f, 1.6f);
        }

        /// <summary>Stable integrity hash over the gameplay payload (not the settings).</summary>
        public static string ComputeIntegrityHash(SaveRoot root)
        {
            if (root == null) return null;
            StringBuilder sb = new StringBuilder(512);
            sb.Append(IntegritySalt).Append('|');
            try
            {
                if (root.Progression != null) sb.Append(JsonUtility.ToJson(root.Progression));
                sb.Append('|');
                if (root.Inventory != null) sb.Append(JsonUtility.ToJson(root.Inventory));
                sb.Append('|');
                if (root.Loadout != null) sb.Append(JsonUtility.ToJson(root.Loadout));
                sb.Append('|');
                if (root.ClaimedRewardTokens != null) sb.Append(root.ClaimedRewardTokens.Count);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Warfront] Integrity hash computation issue: " + ex.Message);
            }
            return Hash64(sb.ToString());
        }

        /// <summary>True when the stored hash matches the payload (v1/v2 saves have no hash).</summary>
        public static bool VerifyIntegrity(SaveRoot root)
        {
            if (root == null) return false;
            if (root.SchemaVersion < SaveSchema.IntegrityVersion) return true;
            if (string.IsNullOrEmpty(root.IntegrityHash)) return false;
            return string.Equals(root.IntegrityHash, ComputeIntegrityHash(root), StringComparison.Ordinal);
        }

        /// <summary>FNV-1a 64 over UTF8 - small, fast and stable across runtimes.</summary>
        public static string Hash64(string text)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                if (text != null)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        hash ^= bytes[i];
                        hash *= prime;
                    }
                }
                return hash.ToString("x16");
            }
        }

        private static void Warn(List<string> warnings, string message)
        {
            if (warnings != null) warnings.Add(message);
        }
    }
}
