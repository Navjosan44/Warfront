using NUnit.Framework;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class ProgressionTests
    {
        private ContentDatabase _content;
        private ProgressionModel _progression;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
            _progression = new ProgressionModel(_content);
        }

        [Test]
        public void NewProfileStartsAtLevelOneWithNoCurrency()
        {
            Assert.AreEqual(1, _progression.Level);
            Assert.AreEqual(0, _progression.Xp);
            Assert.AreEqual(0, _progression.Credits);
            Assert.AreEqual(0, _progression.Materials);
        }

        [Test]
        public void XpRequirementIncreasesWithLevel()
        {
            Assert.Greater(ProgressionModel.XpRequiredForLevel(5), ProgressionModel.XpRequiredForLevel(2));
            Assert.Greater(ProgressionModel.XpRequiredForLevel(20), ProgressionModel.XpRequiredForLevel(10));
        }

        [Test]
        public void EnoughXpRaisesLevelAndCarriesRemainder()
        {
            int needed = ProgressionModel.XpRequiredForLevel(1);
            _progression.AddXp(needed + 50);

            Assert.AreEqual(2, _progression.Level);
            Assert.AreEqual(50, _progression.Xp);
            Assert.AreEqual(needed + 50, _progression.TotalXp);
        }

        [Test]
        public void LevelIsCappedAndXpIsBounded()
        {
            for (int i = 0; i < 400; i++) _progression.AddXp(50000);
            Assert.AreEqual(ProgressionModel.MaxLevel, _progression.Level);
            Assert.LessOrEqual(_progression.Xp, ProgressionModel.XpRequiredForLevel(ProgressionModel.MaxLevel));
        }

        [Test]
        public void NegativeXpIsIgnored()
        {
            _progression.AddXp(-5000);
            Assert.AreEqual(0, _progression.Xp);
            Assert.AreEqual(1, _progression.Level);
        }

        [Test]
        public void CreditsCannotGoNegative()
        {
            _progression.AddCredits(-100);
            Assert.AreEqual(0, _progression.Credits);
            _progression.AddCredits(500);
            Assert.IsFalse(_progression.SpendCredits(501));
            Assert.AreEqual(500, _progression.Credits);
            Assert.IsTrue(_progression.SpendCredits(200));
            Assert.AreEqual(300, _progression.Credits);
        }

        [Test]
        public void CreditsAreClampedToMaximum()
        {
            _progression.AddCredits(int.MaxValue);
            Assert.LessOrEqual(_progression.Credits, ProgressionModel.MaxCredits);
            Assert.GreaterOrEqual(_progression.Credits, 0);
        }

        [Test]
        public void StarterWeaponsAreAlwaysUnlocked()
        {
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            Assert.IsTrue(_progression.IsWeaponUnlocked("wpn_ar_vk7"));
            Assert.IsTrue(_progression.IsWeaponUnlocked("wpn_pistol_sidewinder"));
        }

        [Test]
        public void LockedWeaponCannotBeUpgradedUntilUnlocked()
        {
            UpgradeResult result = _progression.UpgradeWeapon("wpn_sniper_longwatch");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Weapon not unlocked", result.Reason);
        }

        [Test]
        public void WeaponUnlockRequiresLevelAndCredits()
        {
            string weaponId = "wpn_smg_hornet";
            WeaponDef def = _content.GetWeapon(weaponId);

            Assert.IsFalse(_progression.UnlockWeapon(weaponId), "level requirement not met");

            for (int i = 0; i < 200; i++) _progression.AddXp(4000);
            Assert.GreaterOrEqual(_progression.Level, def.UnlockLevel);

            Assert.IsFalse(_progression.UnlockWeapon(weaponId), "no credits");
            _progression.AddCredits(def.UnlockCost);
            Assert.IsTrue(_progression.UnlockWeapon(weaponId));
            Assert.AreEqual(0, _progression.Credits);
            Assert.IsTrue(_progression.IsWeaponUnlocked(weaponId));
        }

        [Test]
        public void UpgradingConsumesCurrencyAndRaisesLevel()
        {
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            _progression.AddCredits(200000);
            _progression.AddMaterials(500);

            int creditsBefore = _progression.Credits;
            UpgradeResult result = _progression.UpgradeWeapon("wpn_ar_vk7");

            Assert.IsTrue(result.Success, result.Reason);
            Assert.AreEqual(2, result.NewLevel);
            Assert.AreEqual(2, _progression.GetWeaponUpgradeLevel("wpn_ar_vk7"));
            Assert.Less(_progression.Credits, creditsBefore);
            Assert.Greater(result.CreditsSpent, 0);
        }

        [Test]
        public void UpgradeFailsWithoutMaterials()
        {
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            _progression.AddCredits(200000);
            UpgradeResult result = _progression.UpgradeWeapon("wpn_ar_vk7");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Not enough materials", result.Reason);
        }

        [Test]
        public void UpgradeStopsAtDefinitionMaximum()
        {
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            _progression.AddCredits(5000000);
            _progression.AddMaterials(99999);

            WeaponDef def = _content.GetWeapon("wpn_ar_vk7");
            for (int i = 0; i < 20; i++) _progression.UpgradeWeapon("wpn_ar_vk7");

            Assert.AreEqual(def.MaxUpgradeLevel, _progression.GetWeaponUpgradeLevel("wpn_ar_vk7"));
            UpgradeResult capped = _progression.UpgradeWeapon("wpn_ar_vk7");
            Assert.IsFalse(capped.Success);
            Assert.AreEqual("Already at maximum level", capped.Reason);
        }

        [Test]
        public void MissionGatingFollowsPrerequisitesAndLevel()
        {
            string first = "mission_rust_ridge";
            Assert.IsFalse(_progression.IsMissionUnlocked(first), "tutorial must be completed first");

            _progression.RecordMissionSuccess("mission_tutorial", 120f, 900);
            Assert.IsTrue(_progression.IsMissionUnlocked(first));

            Assert.IsFalse(_progression.IsMissionUnlocked("mission_signal_hollow"), "needs level 2 and Rust Ridge");
        }

        [Test]
        public void MissionRecordsTrackBestTimeAndCounts()
        {
            _progression.RecordMissionSuccess("mission_rust_ridge", 300f, 1200);
            _progression.RecordMissionSuccess("mission_rust_ridge", 250f, 1500);
            _progression.RecordMissionFailure("mission_rust_ridge");

            MissionRecord record = _progression.GetMissionRecord("mission_rust_ridge");
            Assert.IsTrue(record.Completed);
            Assert.AreEqual(2, record.TimesCompleted);
            Assert.AreEqual(1, record.TimesFailed);
            Assert.AreEqual(250f, record.BestTimeSeconds, 0.01f);
            Assert.AreEqual(1500, record.BestScore);
        }

        [Test]
        public void AchievementsAreUnlockedOnlyOnce()
        {
            Assert.IsTrue(_progression.UnlockAchievement("ach_first_blood"));
            Assert.IsFalse(_progression.UnlockAchievement("ach_first_blood"));
            Assert.IsTrue(_progression.HasAchievement("ach_first_blood"));
            Assert.AreEqual(1, _progression.AchievementSnapshot().Count);
        }

        [Test]
        public void RestoreClampsTamperedSaveValues()
        {
            ProgressionSave tampered = new ProgressionSave();
            tampered.Level = 9999;
            tampered.Xp = -500;
            tampered.Credits = -100000;
            tampered.Materials = -50;
            tampered.TotalKills = -10;
            tampered.WeaponUpgrades.Add(new WeaponUpgradeSave { WeaponId = "wpn_ar_vk7", Level = 900 });
            tampered.WeaponRarities.Add(new WeaponRaritySave { WeaponId = "wpn_ar_vk7", Rarity = 77 });
            tampered.UnlockedWeapons.Add("weapon_that_does_not_exist");
            tampered.UnlockedWeapons.Add("wpn_ar_vk7");
            tampered.Missions.Add(null);

            _progression.Restore(tampered);

            Assert.AreEqual(ProgressionModel.MaxLevel, _progression.Level);
            Assert.AreEqual(0, _progression.Xp);
            Assert.AreEqual(0, _progression.Credits);
            Assert.AreEqual(0, _progression.Materials);
            Assert.AreEqual(0, _progression.TotalKills);
            int expectedCap = Mathf.Min(ProgressionModel.MaxWeaponUpgrade, _content.GetWeapon("wpn_ar_vk7").MaxUpgradeLevel);
            Assert.AreEqual(expectedCap, _progression.GetWeaponUpgradeLevel("wpn_ar_vk7"), "levels clamp to the per weapon ceiling");
            Assert.AreEqual(Rarity.Legendary, _progression.GetWeaponRarity("wpn_ar_vk7"));
            Assert.IsFalse(_progression.IsWeaponUnlocked("weapon_that_does_not_exist"));
            Assert.IsTrue(_progression.IsWeaponUnlocked("wpn_ar_vk7"));
        }

        [Test]
        public void RestoreOfNullDoesNotThrow()
        {
            Assert.DoesNotThrow(() => _progression.Restore(null));
        }

        [Test]
        public void SnapshotRoundTripsThroughSaveBlock()
        {
            _progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            _progression.AddCredits(1500);
            _progression.AddMaterials(7);
            _progression.AddXp(3000);
            _progression.GrantWeapon("wpn_smg_hornet", Rarity.Rare);
            _progression.RecordMissionSuccess("mission_tutorial", 100f, 800);

            ProgressionModel restored = new ProgressionModel(_content);
            ProgressionSave save = new ProgressionSave();
            save.Level = _progression.Level;
            save.Xp = _progression.Xp;
            save.TotalXp = _progression.TotalXp;
            save.Credits = _progression.Credits;
            save.Materials = _progression.Materials;
            save.MissionsCompleted = _progression.MissionsCompleted;
            save.TotalKills = _progression.TotalKills;
            foreach (var kv in _progression.WeaponUpgradeSnapshot())
            {
                save.WeaponUpgrades.Add(new WeaponUpgradeSave { WeaponId = kv.Key, Level = kv.Value });
            }
            foreach (var kv in _progression.WeaponRaritySnapshot())
            {
                save.WeaponRarities.Add(new WeaponRaritySave { WeaponId = kv.Key, Rarity = (int)kv.Value });
            }
            save.UnlockedWeapons.AddRange(_progression.UnlockSnapshot());

            restored.Restore(save);

            Assert.AreEqual(_progression.Level, restored.Level);
            Assert.AreEqual(_progression.Xp, restored.Xp);
            Assert.AreEqual(_progression.Credits, restored.Credits);
            Assert.AreEqual(Rarity.Rare, restored.GetWeaponRarity("wpn_smg_hornet"));
            Assert.AreEqual(1, restored.MissionsCompleted);
        }

        [Test]
        public void DifficultyScalingIncreasesEnemyStats()
        {
            Assert.Greater(ContentDatabase.DifficultyHealthScale(5), ContentDatabase.DifficultyHealthScale(1));
            Assert.Greater(ContentDatabase.DifficultyDamageScale(3), ContentDatabase.DifficultyDamageScale(1));
            Assert.AreEqual(1f, ContentDatabase.DifficultyDamageScale(1), 0.0001f);
        }
    }
}
