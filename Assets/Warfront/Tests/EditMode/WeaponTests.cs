using NUnit.Framework;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class WeaponModelTests
    {
        private static ContentDatabase Content { get { return ContentDatabase.CreateDefault(); } }

        [Test]
        public void FreshWeaponStartsWithFullMagazine()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 1);
            Assert.AreEqual(def.MagazineSize, weapon.AmmoInMagazine);
            Assert.IsTrue(weapon.MagazineFull);
            Assert.AreEqual(WeaponState.Ready, weapon.State);
        }

        [Test]
        public void FireConsumesAmmoAndRespectsFireRate()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 7);
            int reserve = 300;

            float time = 0f;
            FireAttempt first = weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
            Assert.IsTrue(first.Fired);
            Assert.AreEqual(def.MagazineSize - 1, weapon.AmmoInMagazine);

            // Immediately firing again must be blocked by the cadence gate.
            FireAttempt tooSoon = weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
            Assert.IsFalse(tooSoon.Fired, "Fire rate limit not honoured");

            time += def.SecondsBetweenShots + 0.001f;
            FireAttempt second = weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
            Assert.IsTrue(second.Fired);
            Assert.AreEqual(def.MagazineSize - 2, weapon.AmmoInMagazine);
        }

        [Test]
        public void EmptyMagazineBlocksFireAndHintsReload()
        {
            WeaponDef def = Content.GetWeapon("wpn_pistol_sidewinder");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 3);
            int reserve = 0;

            float time = 0f;
            for (int i = 0; i < def.MagazineSize + 4; i++)
            {
                weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
                time += def.SecondsBetweenShots + 0.001f;
            }

            Assert.AreEqual(0, weapon.AmmoInMagazine);
            Assert.AreEqual(WeaponState.Empty, weapon.State);

            FireAttempt attempt = weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
            Assert.IsFalse(attempt.Fired);
            Assert.IsTrue(attempt.TriggeredReloadHint);
        }

        [Test]
        public void ReloadConsumesReserveAndFinishesAfterReloadTime()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 11);
            int reserve = 200;
            float time = 0f;

            weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
            int ammoAfterShot = weapon.AmmoInMagazine;

            bool started = weapon.BeginReload(time, ref reserve);
            Assert.IsTrue(started);
            Assert.AreEqual(WeaponState.Reloading, weapon.State);
            Assert.AreEqual(def.MagazineSize, weapon.AmmoInMagazine, "magazine should fill at reload start (single consume)");
            Assert.AreEqual(200 - (def.MagazineSize - ammoAfterShot), reserve);

            weapon.Tick(time + def.ReloadTime * 0.5f, def.ReloadTime * 0.5f);
            Assert.AreEqual(WeaponState.Reloading, weapon.State);
            Assert.Greater(weapon.ReloadProgress01, 0.4f);
            Assert.Less(weapon.ReloadProgress01, 0.8f);

            weapon.Tick(time + def.ReloadTime + 0.01f, def.ReloadTime);
            Assert.AreEqual(WeaponState.Ready, weapon.State);
        }

        [Test]
        public void ReloadWithoutReserveIsRejected()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 5);
            int reserve = 0;
            weapon.TryFire(0f, 0.016f, true, false, false, ref reserve);

            Assert.IsFalse(weapon.BeginReload(0.1f, ref reserve));
            Assert.AreEqual(0, reserve, "Reserve must never go negative");
        }

        [Test]
        public void ReloadIsRejectedWhenMagazineAlreadyFull()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 5);
            int reserve = 90;
            Assert.IsFalse(weapon.BeginReload(0f, ref reserve));
            Assert.AreEqual(90, reserve);
        }

        [Test]
        public void ReserveAmmoIsClampedAtZero()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 5);
            int reserve = 1;
            weapon.TryFire(0f, 0.016f, true, false, false, ref reserve);
            weapon.BeginReload(0.1f, ref reserve);
            Assert.GreaterOrEqual(reserve, 0);
        }

        [Test]
        public void ShotgunFiresAllPelletsAtOnce()
        {
            WeaponDef def = Content.GetWeapon("wpn_shotgun_breaker");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 2);
            int reserve = 40;
            FireAttempt attempt = weapon.TryFire(0f, 0.016f, true, false, false, ref reserve);
            Assert.IsTrue(attempt.Fired);
            Assert.AreEqual(1, attempt.Shots);
            Assert.AreEqual(def.MagazineSize - 1, weapon.AmmoInMagazine);
        }

        [Test]
        public void BurstWeaponFiresMultipleRoundsPerTriggerPull()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7").Clone();
            def.FireMode = FireMode.Burst;
            def.BurstCount = 3;
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 4);
            int reserve = 90;

            FireAttempt attempt = weapon.TryFire(0f, 0.016f, true, false, false, ref reserve);
            Assert.IsTrue(attempt.Fired);
            Assert.AreEqual(3, attempt.Shots);
            Assert.AreEqual(def.MagazineSize - 3, weapon.AmmoInMagazine);
        }

        [Test]
        public void AimingReducesSpreadAndRecoil()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel hip = new WeaponModel(def, Rarity.Common, 1, 6);
            WeaponModel ads = new WeaponModel(def, Rarity.Common, 1, 6);
            int reserveA = 120;
            int reserveB = 120;

            FireAttempt hipShot = hip.TryFire(0f, 0.016f, true, false, false, ref reserveA);
            FireAttempt adsShot = ads.TryFire(0f, 0.016f, true, true, false, ref reserveB);

            Assert.Less(adsShot.SpreadDegrees, hipShot.SpreadDegrees);
            Assert.Less(adsShot.RecoilVertical, hipShot.RecoilVertical);
        }

        [Test]
        public void SustainedFireIncreasesSpreadAndRecoveryReducesIt()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 8);
            int reserve = 300;
            float time = 0f;

            for (int i = 0; i < 10; i++)
            {
                weapon.TryFire(time, 0.016f, true, false, false, ref reserve);
                time += def.SecondsBetweenShots + 0.001f;
            }
            float afterBurst = weapon.CurrentSpread;
            Assert.Greater(afterBurst, 0f);

            for (int i = 0; i < 40; i++) weapon.Tick(time, 0.05f);
            Assert.Less(weapon.CurrentSpread, afterBurst);
        }

        [Test]
        public void UpgradeLevelRaisesDamageAndMagazineWithoutDuplicatingAmmo()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponStats baseStats = WeaponStatRules.Build(def, Rarity.Common, 1);
            WeaponStats upgraded = WeaponStatRules.Build(def, Rarity.Common, 3);

            Assert.Greater(upgraded.Damage, baseStats.Damage);
            Assert.Greater(upgraded.MagazineSize, baseStats.MagazineSize);
            Assert.Less(upgraded.ReloadTime, baseStats.ReloadTime);

            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 1, 9);
            weapon.SetUpgradeLevel(3);
            Assert.LessOrEqual(weapon.AmmoInMagazine, weapon.MagazineSize);
            Assert.GreaterOrEqual(weapon.AmmoInMagazine, 0);
        }

        [Test]
        public void UpgradeLevelIsClampedToDefinitionMaximum()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Common, 99, 1);
            Assert.AreEqual(def.MaxUpgradeLevel, weapon.UpgradeLevel);
        }

        [Test]
        public void RarityScalesDamageUpwards()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            WeaponStats common = WeaponStatRules.Build(def, Rarity.Common, 1);
            WeaponStats legendary = WeaponStatRules.Build(def, Rarity.Legendary, 1);
            Assert.Greater(legendary.Damage, common.Damage);
            Assert.Less(legendary.BaseSpread, common.BaseSpread);
        }

        [Test]
        public void UpgradeCostGrowsWithLevel()
        {
            WeaponDef def = Content.GetWeapon("wpn_ar_vk7");
            int credits1;
            int materials1;
            int credits4;
            int materials4;
            WeaponStatRules.UpgradeCost(def, 1, out credits1, out materials1);
            WeaponStatRules.UpgradeCost(def, 4, out credits4, out materials4);
            Assert.Greater(credits4, credits1);
            Assert.Greater(materials4, materials1);
        }

        [Test]
        public void EveryWeaponInContentDatabaseIsValid()
        {
            ContentDatabase content = Content;
            var weapons = content.AllWeapons();
            Assert.GreaterOrEqual(weapons.Count, 5, "WARFRONT ships at least five weapon platforms");
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDef def = weapons[i];
                Assert.IsFalse(string.IsNullOrEmpty(def.Id), "weapon id missing");
                Assert.IsFalse(string.IsNullOrEmpty(def.DisplayName), def.Id + " has no display name");
                Assert.Greater(def.Damage, 0f, def.Id + " damage");
                Assert.Greater(def.RoundsPerMinute, 0f, def.Id + " fire rate");
                Assert.Greater(def.MagazineSize, 0, def.Id + " magazine");
                Assert.Greater(def.ReloadTime, 0f, def.Id + " reload");
                Assert.Greater(def.Range, 0f, def.Id + " range");
                Assert.GreaterOrEqual(def.FalloffEnd, def.FalloffStart, def.Id + " falloff ordering");
                Assert.GreaterOrEqual(def.HeadshotMultiplier, 1f, def.Id + " headshot multiplier");
                Assert.Greater(def.MaxUpgradeLevel, 0, def.Id + " upgrade levels");
                Assert.Greater(def.LootWeight, 0, def.Id + " loot weight");
            }

            var classes = new System.Collections.Generic.HashSet<WeaponClass>();
            for (int i = 0; i < weapons.Count; i++) classes.Add(weapons[i].Class);
            Assert.IsTrue(classes.Contains(WeaponClass.AssaultRifle));
            Assert.IsTrue(classes.Contains(WeaponClass.SMG));
            Assert.IsTrue(classes.Contains(WeaponClass.Shotgun));
            Assert.IsTrue(classes.Contains(WeaponClass.SniperRifle));
            Assert.IsTrue(classes.Contains(WeaponClass.Pistol));
        }

        [Test]
        public void RecoilPatternIsDeterministicForSameSeed()
        {
            float v1;
            float h1;
            float v2;
            float h2;
            RecoilPattern.Sample(WeaponClass.AssaultRifle, 5, 99, out v1, out h1);
            RecoilPattern.Sample(WeaponClass.AssaultRifle, 5, 99, out v2, out h2);
            Assert.AreEqual(v1, v2, 0.0001f);
            Assert.AreEqual(h1, h2, 0.0001f);
        }
    }
}
