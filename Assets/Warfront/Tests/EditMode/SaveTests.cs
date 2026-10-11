using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class SaveSerializerTests
    {
        [Test]
        public void DefaultSaveYieldsUsableProfile()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            Assert.IsNotNull(root.Progression);
            Assert.IsNotNull(root.Inventory);
            Assert.IsNotNull(root.Loadout);
            Assert.IsNotNull(root.ClaimedRewardTokens);
            Assert.AreEqual(SaveSchema.CurrentVersion, root.SchemaVersion);
        }

        [Test]
        public void RoundTripPreservesGameplayValues()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.Progression.Level = 7;
            root.Progression.Xp = 320;
            root.Progression.Credits = 12345;
            root.Progression.Materials = 21;
            root.Progression.UnlockedWeapons.Add("wpn_ar_vk7");
            root.Progression.WeaponUpgrades.Add(new WeaponUpgradeSave { WeaponId = "wpn_ar_vk7", Level = 4 });
            root.Inventory.LightAmmo = 240;
            root.Inventory.Slots.Add(new ItemStackSave { ItemId = "mat_alloy", Quantity = 12, Rarity = 1, UpgradeLevel = 1 });
            root.Loadout.PrimaryWeaponId = "wpn_ar_vk7";
            root.ClaimedRewardTokens.Add("mission_rust_ridge:DEADBEEF:X");
            root.Settings.MasterVolume = 0.5f;

            string json = SaveSerializer.Serialize(root);
            Assert.IsNotNull(json);
            Assert.IsNotEmpty(json);

            string error;
            SaveRoot restored = SaveSerializer.Deserialize(json, out error);
            Assert.IsNull(error);
            Assert.IsNotNull(restored);
            Assert.AreEqual(7, restored.Progression.Level);
            Assert.AreEqual(320, restored.Progression.Xp);
            Assert.AreEqual(12345, restored.Progression.Credits);
            Assert.AreEqual(21, restored.Progression.Materials);
            Assert.AreEqual(4, restored.Progression.WeaponUpgrades[0].Level);
            Assert.AreEqual(240, restored.Inventory.LightAmmo);
            Assert.AreEqual("mat_alloy", restored.Inventory.Slots[0].ItemId);
            Assert.AreEqual("wpn_ar_vk7", restored.Loadout.PrimaryWeaponId);
            Assert.AreEqual(1, restored.ClaimedRewardTokens.Count);
            Assert.AreEqual(0.5f, restored.Settings.MasterVolume, 0.0001f);
        }

        [Test]
        public void IntegrityHashDetectsTampering()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.Progression.Credits = 1000;
            string json = SaveSerializer.Serialize(root);

            string error;
            SaveRoot restored = SaveSerializer.Deserialize(json, out error);
            Assert.IsTrue(SaveSerializer.VerifyIntegrity(restored), "untouched save must verify");

            restored.Progression.Credits = 999999;
            Assert.IsFalse(SaveSerializer.VerifyIntegrity(restored), "edited credits must fail the integrity check");
        }

        [Test]
        public void HashIsStableForSameInput()
        {
            Assert.AreEqual(SaveSerializer.Hash64("warfront"), SaveSerializer.Hash64("warfront"));
            Assert.AreNotEqual(SaveSerializer.Hash64("warfront"), SaveSerializer.Hash64("warfront2"));
        }

        [Test]
        public void CorruptJsonFailsGracefullyWithoutThrowing()
        {
            string error = null;
            SaveRoot root = null;
            Assert.DoesNotThrow(() => root = SaveSerializer.Deserialize("{\"Progression\": { ", out error));
            Assert.IsNull(root);
            Assert.IsNotNull(error);
        }

        [Test]
        public void EmptyDocumentIsRejected()
        {
            string error;
            Assert.IsNull(SaveSerializer.Deserialize(null, out error));
            Assert.IsNull(SaveSerializer.Deserialize(string.Empty, out error));
        }

        [Test]
        public void NullBlocksAreRepairedDuringSanitize()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.Progression = null;
            root.Inventory = null;
            root.Loadout = null;
            root.Settings = null;
            root.ClaimedRewardTokens = null;

            var warnings = new List<string>();
            string error;
            Assert.IsTrue(SaveSerializer.TryPrepare(root, out error, warnings));
            Assert.IsNotNull(root.Progression);
            Assert.IsNotNull(root.Inventory);
            Assert.IsNotNull(root.Loadout);
            Assert.IsNotNull(root.Settings);
            Assert.IsNotNull(root.ClaimedRewardTokens);
            Assert.Greater(warnings.Count, 0);
        }

        [Test]
        public void ImpossibleValuesAreClamped()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.Progression.Level = -3;
            root.Progression.Credits = -5000;
            root.Progression.Materials = -1;
            root.Progression.Xp = 99999999;
            root.Progression.WeaponUpgrades.Add(new WeaponUpgradeSave { WeaponId = "wpn_ar_vk7", Level = -4 });
            root.Inventory.Capacity = -10;
            root.Inventory.LightAmmo = -20;
            root.Settings.MasterVolume = 4f;
            root.Settings.QualityPreset = 12;

            var warnings = new List<string>();
            string error;
            SaveSerializer.TryPrepare(root, out error, warnings);

            Assert.AreEqual(1, root.Progression.Level);
            Assert.AreEqual(0, root.Progression.Credits);
            Assert.AreEqual(0, root.Progression.Materials);
            Assert.LessOrEqual(root.Progression.Xp, ProgressionModel.XpRequiredForLevel(1));
            Assert.AreEqual(1, root.Progression.WeaponUpgrades[0].Level);
            Assert.GreaterOrEqual(root.Inventory.Capacity, 4);
            Assert.AreEqual(0, root.Inventory.LightAmmo);
            Assert.AreEqual(1f, root.Settings.MasterVolume, 0.0001f);
            Assert.AreEqual(3, root.Settings.QualityPreset);
        }

        [Test]
        public void InventoryOverflowIsTrimmedToCapacity()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.Inventory.Capacity = 5;
            for (int i = 0; i < 20; i++)
            {
                root.Inventory.Slots.Add(new ItemStackSave { ItemId = "mat_alloy", Quantity = 2, Rarity = 0, UpgradeLevel = 1 });
            }

            var warnings = new List<string>();
            string error;
            SaveSerializer.TryPrepare(root, out error, warnings);
            Assert.AreEqual(5, root.Inventory.Slots.Count);
        }

        [Test]
        public void DuplicateClaimTokensAreDeduplicated()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.ClaimedRewardTokens.Add("mission:1:X");
            root.ClaimedRewardTokens.Add("mission:1:X");
            root.ClaimedRewardTokens.Add("");
            root.ClaimedRewardTokens.Add("mission:2:F");

            var warnings = new List<string>();
            string error;
            SaveSerializer.TryPrepare(root, out error, warnings);
            Assert.AreEqual(2, root.ClaimedRewardTokens.Count);
        }

        [Test]
        public void OlderSchemaIsMigratedForward()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.SchemaVersion = 1;
            root.ClaimedRewardTokens = null;

            var warnings = new List<string>();
            string error;
            Assert.IsTrue(SaveSerializer.TryPrepare(root, out error, warnings));
            Assert.AreEqual(SaveSchema.CurrentVersion, root.SchemaVersion);
            Assert.IsNotNull(root.ClaimedRewardTokens);
            Assert.Greater(warnings.Count, 0);
        }

        [Test]
        public void FutureSchemaIsRejectedInsteadOfCorruptingData()
        {
            SaveRoot root = SaveRoot.CreateDefault();
            root.SchemaVersion = SaveSchema.CurrentVersion + 5;

            var warnings = new List<string>();
            string error;
            Assert.IsFalse(SaveSerializer.TryPrepare(root, out error, warnings));
            Assert.IsNotNull(error);
        }
    }

    public class SaveFileStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "warfront_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void WriteThenLoadReturnsTheSameData()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            SaveRoot root = SaveRoot.CreateDefault();
            root.Progression.Credits = 777;
            Assert.IsTrue(store.Write(SaveSerializer.Serialize(root)));

            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(777, result.Data.Progression.Credits);
        }

        [Test]
        public void MissingSaveCreatesADefaultProfile()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.CreatedDefault, result.Status);
            Assert.IsNotNull(result.Data);
        }

        [Test]
        public void NoTemporaryFileIsLeftBehind()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, SaveFileStore.TempFileName)));
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackup()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            SaveRoot first = SaveRoot.CreateDefault();
            first.Progression.Credits = 100;
            store.Write(SaveSerializer.Serialize(first));

            SaveRoot second = SaveRoot.CreateDefault();
            second.Progression.Credits = 250;
            store.Write(SaveSerializer.Serialize(second));

            Assert.IsTrue(File.Exists(store.BackupPath), "second write must rotate a backup");

            File.WriteAllText(store.PrimaryPath, "{ this is not valid json ");

            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.LoadedFromBackup, result.Status);
            Assert.AreEqual(100, result.Data.Progression.Credits, "backup holds the previous good state");
        }

        [Test]
        public void BothFilesCorruptProducesFreshProfileInsteadOfCrashing()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            File.WriteAllText(store.PrimaryPath, "garbage");
            File.WriteAllText(store.BackupPath, "more garbage");

            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.RecoveredFromCorruption, result.Status);
            Assert.IsNotNull(result.Data);
            Assert.Greater(result.Warnings.Count, 0);
        }

        [Test]
        public void TamperedPrimaryFallsBackToBackup()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            SaveRoot first = SaveRoot.CreateDefault();
            first.Progression.Credits = 500;
            store.Write(SaveSerializer.Serialize(first));
            SaveRoot second = SaveRoot.CreateDefault();
            second.Progression.Credits = 600;
            store.Write(SaveSerializer.Serialize(second));

            // Edit the primary while leaving the (now stale) integrity hash in place.
            string tampered = File.ReadAllText(store.PrimaryPath).Replace("\"Credits\": 600", "\"Credits\": 999999");
            File.WriteAllText(store.PrimaryPath, tampered);

            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.LoadedFromBackup, result.Status);
            Assert.AreEqual(500, result.Data.Progression.Credits);
        }

        [Test]
        public void DeleteAllRemovesEveryArtifact()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            store.Write(SaveSerializer.Serialize(SaveRoot.CreateDefault()));
            store.DeleteAll();
            Assert.IsFalse(File.Exists(store.PrimaryPath));
            Assert.IsFalse(File.Exists(store.BackupPath));
        }

        [Test]
        public void ManyWritesKeepSaveReadable()
        {
            SaveFileStore store = new SaveFileStore(_directory);
            for (int i = 0; i < 25; i++)
            {
                SaveRoot root = SaveRoot.CreateDefault();
                root.Progression.Credits = i * 10;
                Assert.IsTrue(store.Write(SaveSerializer.Serialize(root)));
            }

            SaveLoadResult result = store.Load(true);
            Assert.AreEqual(SaveLoadStatus.Loaded, result.Status);
            Assert.AreEqual(240, result.Data.Progression.Credits);
        }
    }
}
