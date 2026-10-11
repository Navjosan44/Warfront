using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests. They verify that the gameplay assembly compiled and that a full
    /// mission can be simulated through the core models with real Unity time semantics, which is the
    /// closest thing to a runtime test available without the editor UI.
    /// </summary>
    public class PlayModeSmokeTests
    {
        [Test]
        public void RuntimeGameplayAssemblyIsPresent()
        {
            Type bootstrap = FindType("Warfront.Game.GameBootstrap");
            Assert.IsNotNull(bootstrap, "Warfront.Game.GameBootstrap was not found - the gameplay assembly failed to compile");
            Assert.IsNotNull(FindType("Warfront.Game.PlayerController"), "PlayerController missing");
            Assert.IsNotNull(FindType("Warfront.Game.WeaponRuntime"), "WeaponRuntime missing");
            Assert.IsNotNull(FindType("Warfront.Game.EnemyController"), "EnemyController missing");
            Assert.IsNotNull(FindType("Warfront.Game.LevelGenerator"), "LevelGenerator missing");
            Assert.IsNotNull(FindType("Warfront.Game.AudioService"), "AudioService missing");
            Assert.IsNotNull(FindType("Warfront.UI.UIRoot"), "UIRoot missing");
            Assert.IsNotNull(FindType("Warfront.UI.MobileControls"), "MobileControls missing");
            Assert.IsNotNull(FindType("Warfront.Game.SaveService"), "SaveService missing");
            Assert.IsNotNull(FindType("Warfront.Game.PerformanceManager"), "PerformanceManager missing");
        }

        [Test]
        public void FullMissionCanBeSimulatedToExtraction()
        {
            ContentDatabase content = ContentDatabase.CreateDefault();
            MissionDef def = content.GetMission("mission_rust_ridge");
            MissionModel mission = new MissionModel(def, content, 777);
            mission.Begin();

            InventoryModel inventory = new InventoryModel(content, 24);
            int leftover;
            inventory.Add("ammo_light", 4, Rarity.Common, 1, out leftover);

            // Simulate ~40 seconds of play at 30 fps.
            float dt = 1f / 30f;
            for (int frame = 0; frame < 1200; frame++)
            {
                mission.Tick(dt, false, Vector3.zero, new Vector3(50f, 0f, 50f), 6f);
                if (frame % 200 == 0) mission.RegisterKill(false, false);
            }

            mission.ReportProgress(ObjectiveType.SecureLocation, "secure_a", 1);
            mission.ReportProgress(ObjectiveType.RecoverSupplies, "supply", 2);

            // Every combat objective is done, so the extraction beacon must already be unlocked. The
            // ReachExtraction objective itself can only complete by holding the zone, which has not
            // happened yet - so the full contract deliberately still reads as incomplete here.
            Assert.IsTrue(mission.AllObjectivesCompleteExceptExtraction,
                "every objective other than extraction should be complete at this point");
            Assert.IsFalse(mission.AllRequiredComplete,
                "extraction must remain outstanding until the player actually holds the zone");

            for (int frame = 0; frame < 600; frame++)
            {
                mission.Tick(dt, true, Vector3.zero, Vector3.zero, 6f);
            }

            Assert.AreEqual(MissionState.Success, mission.State);
            Assert.IsTrue(mission.Extraction.RewardClaimed);

            MissionSummary summary = mission.BuildSummary();
            Assert.IsTrue(summary.Success);
            Assert.Greater(summary.Kills, 0);

            ProgressionModel progression = new ProgressionModel(content);
            progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            MissionRewards rewards = RewardCalculator.Compute(summary, def, progression);
            LootRoller roller = new LootRoller(content, summary.RunId);
            roller.Roll(def.LootTableId, rewards.LootLuck, rewards.LootRolls, rewards.Drops);

            Assert.Greater(rewards.Xp, 0);
            Assert.Greater(rewards.Credits, 0);
            Assert.Greater(rewards.Drops.Count, 0, "a successful extraction should produce loot");

            // The loop contract: a clean first contract banks real XP and credits on the profile and is
            // worth a large slice of the first level. It deliberately does not assert an immediate
            // level-up, because whether one contract completes level 1 outright is a balance decision
            // (it currently lands just short of the 1070 XP that level 1 -> 2 costs).
            Assert.Greater(rewards.Xp, 0, "a successful extraction must pay experience");
            Assert.GreaterOrEqual(rewards.Xp, ProgressionModel.XpRequiredForLevel(1) / 2,
                "a first contract should be worth at least half of level 1 -> 2 or the loop feels unrewarding");

            progression.AddXp(rewards.Xp);
            progression.AddCredits(rewards.Credits);

            Assert.AreEqual(rewards.Xp, progression.TotalXp, "banked XP must land on the profile exactly");
            Assert.Greater(progression.Credits, 0, "banked credits must land on the profile");
            Assert.Greater(progression.LevelProgress01, 0f, "the banked XP must show as progress on the XP bar");
            Assert.AreEqual(1, progression.Level, "a single contract must not skip the first level outright");
        }

        [Test]
        public void WeaponCanFireFullMagazineDuringPlayModeFrames()
        {
            ContentDatabase content = ContentDatabase.CreateDefault();
            WeaponDef def = content.GetWeapon("wpn_ar_vk7");
            WeaponModel weapon = new WeaponModel(def, Rarity.Uncommon, 2, 4242);
            int reserve = 240;

            float time = 0f;
            int shots = 0;
            float dt = 1f / 60f;
            for (int frame = 0; frame < 600; frame++)
            {
                FireAttempt attempt = weapon.TryFire(time, dt, true, false, false, ref reserve);
                if (attempt.Fired) shots += attempt.Shots;
                weapon.Tick(time, dt);
                time += dt;
                if (weapon.MagazineEmpty) break;
            }

            // Upgrade level and rarity scale the effective magazine, so the honest contract is that a
            // held trigger empties whatever the deployed weapon actually holds - exactly, and with
            // nothing wasted. Guard the scaling too, so an upgrade can never shrink a magazine.
            Assert.GreaterOrEqual(weapon.MagazineSize, def.MagazineSize,
                "an upgraded weapon must never hold fewer rounds than its base definition");
            Assert.AreEqual(weapon.MagazineSize, shots,
                "a held trigger should empty the magazine without wasting ammo");
            Assert.AreEqual(0, weapon.AmmoInMagazine);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(fullName, false);
                    if (type != null) return type;
                }
                catch (Exception)
                {
                    // Dynamic/reflection-only assemblies can throw - ignore and keep looking.
                }
            }
            return null;
        }
    }
}
