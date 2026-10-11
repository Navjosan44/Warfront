using System.Collections.Generic;
using NUnit.Framework;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    /// <summary>
    /// Content level tests: catches designer data errors (dangling item ids, unreachable missions,
    /// missing extraction objectives) at test time instead of at runtime.
    /// </summary>
    public class ContentIntegrityTests
    {
        private ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
        }

        [Test]
        public void MissionIdsAreUniqueAndHaveCoreFields()
        {
            HashSet<string> ids = new HashSet<string>();
            IList<MissionDef> missions = _content.Missions;
            Assert.GreaterOrEqual(missions.Count, 6, "WARFRONT ships a campaign, not a single mission");

            for (int i = 0; i < missions.Count; i++)
            {
                MissionDef mission = missions[i];
                Assert.IsFalse(string.IsNullOrEmpty(mission.Id), "mission id missing");
                Assert.IsTrue(ids.Add(mission.Id), "duplicate mission id " + mission.Id);
                Assert.IsFalse(string.IsNullOrEmpty(mission.DisplayName), mission.Id + " display name");
                Assert.IsFalse(string.IsNullOrEmpty(mission.Briefing), mission.Id + " briefing");
                Assert.GreaterOrEqual(mission.Difficulty, 1, mission.Id + " difficulty");
                Assert.LessOrEqual(mission.Difficulty, 5, mission.Id + " difficulty");
                Assert.Greater(mission.ExtractionHoldSeconds, 0f, mission.Id + " extraction hold");
                Assert.Greater(mission.Objectives.Count, 0, mission.Id + " has no objectives");
                Assert.Greater(mission.EnemyBudget, 0, mission.Id + " enemy budget");
                Assert.Greater(mission.MaxEnemiesAlive, 0, mission.Id + " alive cap");
                Assert.LessOrEqual(mission.MaxEnemiesAlive, mission.EnemyBudget, mission.Id + " alive cap exceeds budget");
                Assert.IsNotNull(_content.GetLootTable(mission.LootTableId), mission.Id + " references unknown loot table");
                Assert.Greater(mission.MapSize, 20f, mission.Id + " map size");
                Assert.AreNotEqual(0, mission.Seed, mission.Id + " seed must be deterministic and non-zero");
            }
        }

        [Test]
        public void EveryMissionHasAnExtractionObjective()
        {
            IList<MissionDef> missions = _content.Missions;
            for (int i = 0; i < missions.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < missions[i].Objectives.Count; j++)
                {
                    if (missions[i].Objectives[j].Type == ObjectiveType.ReachExtraction) found = true;
                }
                Assert.IsTrue(found, missions[i].Id + " has no ReachExtraction objective");
            }
        }

        [Test]
        public void MissionPrerequisitesExistAndFormOneChain()
        {
            IList<MissionDef> missions = _content.Missions;
            int tutorials = 0;
            for (int i = 0; i < missions.Count; i++)
            {
                MissionDef mission = missions[i];
                if (mission.IsTutorial)
                {
                    tutorials++;
                    Assert.IsTrue(string.IsNullOrEmpty(mission.RequiredMissionId), "tutorial must not require another mission");
                }
                else
                {
                    Assert.IsFalse(string.IsNullOrEmpty(mission.RequiredMissionId), mission.Id + " needs a prerequisite");
                    Assert.IsNotNull(_content.GetMission(mission.RequiredMissionId), mission.Id + " prerequisite does not exist");
                }
                Assert.GreaterOrEqual(mission.UnlockLevel, 1);
                Assert.LessOrEqual(mission.UnlockLevel, ProgressionModel.MaxLevel);
            }
            Assert.AreEqual(1, tutorials, "there must be exactly one tutorial mission");
        }

        [Test]
        public void ProgressionChainIsCompletable()
        {
            ProgressionModel progression = new ProgressionModel(_content);
            progression.EnsureStarterWeapons(ContentDatabase.StarterWeapons);
            List<MissionDef> ordered = _content.OrderedMissions();

            // Walk the campaign the way a player would: level up, complete, unlock the next.
            for (int i = 0; i < ordered.Count; i++)
            {
                MissionDef mission = ordered[i];
                while (progression.Level < mission.UnlockLevel) progression.AddXp(20000);
                Assert.IsTrue(progression.IsMissionUnlocked(mission.Id), mission.Id + " is unreachable in the campaign chain");
                progression.RecordMissionSuccess(mission.Id, 300f, 1000);
                progression.AddCredits(20000);
            }
        }

        [Test]
        public void EveryObjectiveHasTitleAndLegalTargetCount()
        {
            IList<MissionDef> missions = _content.Missions;
            for (int i = 0; i < missions.Count; i++)
            {
                MissionDef mission = missions[i];
                for (int j = 0; j < mission.Objectives.Count; j++)
                {
                    ObjectiveDef objective = mission.Objectives[j];
                    Assert.IsFalse(string.IsNullOrEmpty(objective.Id), mission.Id + " objective id");
                    Assert.IsFalse(string.IsNullOrEmpty(objective.Title), mission.Id + "/" + objective.Id + " title");
                    Assert.GreaterOrEqual(objective.TargetCount, 1, mission.Id + "/" + objective.Id + " target count");
                    if (objective.Type == ObjectiveType.EliminateTarget || objective.Type == ObjectiveType.SecureLocation ||
                        objective.Type == ObjectiveType.ActivateDevice || objective.Type == ObjectiveType.ReachExtraction)
                    {
                        Assert.AreEqual(1, objective.TargetCount, mission.Id + "/" + objective.Id + " should be single-step");
                    }
                }
                Assert.Greater(mission.RequiredObjectiveCount, 0, mission.Id + " has no required objectives");
            }
        }

        [Test]
        public void ObjectiveIdsAreUniqueWithinAMission()
        {
            IList<MissionDef> missions = _content.Missions;
            for (int i = 0; i < missions.Count; i++)
            {
                HashSet<string> ids = new HashSet<string>();
                for (int j = 0; j < missions[i].Objectives.Count; j++)
                {
                    Assert.IsTrue(ids.Add(missions[i].Objectives[j].Id), "duplicate objective id in " + missions[i].Id);
                }
            }
        }

        [Test]
        public void EveryLootTableEntryReferencesARealItem()
        {
            string[] tables = { "loot_standard", "loot_secure", "loot_enemy_standard", "loot_enemy_light", "loot_enemy_heavy", "loot_enemy_elite", "loot_supply_crate" };
            for (int i = 0; i < tables.Length; i++)
            {
                LootTableDef table = _content.GetLootTable(tables[i]);
                Assert.IsNotNull(table, "missing loot table " + tables[i]);
                Assert.Greater(table.Entries.Count, 0, tables[i] + " is empty");
                for (int j = 0; j < table.Entries.Count; j++)
                {
                    LootTableEntry entry = table.Entries[j];
                    ItemDef item = _content.GetItem(entry.ItemId);
                    Assert.IsNotNull(item, tables[i] + " references unknown item " + entry.ItemId);
                    Assert.Greater(entry.Weight, 0, tables[i] + "/" + entry.ItemId + " weight");
                    Assert.GreaterOrEqual(entry.MaxQuantity, entry.MinQuantity, tables[i] + "/" + entry.ItemId + " quantity range");
                    Assert.GreaterOrEqual(entry.MinQuantity, 1, tables[i] + "/" + entry.ItemId + " minimum quantity");
                    Assert.GreaterOrEqual(entry.MaxUpgradeLevel, 1, tables[i] + "/" + entry.ItemId + " upgrade ceiling");
                }
            }
        }

        [Test]
        public void WeaponItemsAndWeaponDefsAreLinkedBothWays()
        {
            List<WeaponDef> weapons = _content.AllWeapons();
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDef weapon = weapons[i];
                bool found = false;
                List<ItemDef> items = _content.AllItems();
                for (int j = 0; j < items.Count; j++)
                {
                    if (items[j].Category == ItemCategory.Weapon && items[j].WeaponId == weapon.Id)
                    {
                        found = true;
                        Assert.Greater(items[j].AmmoPerUnit, 0, weapon.Id + " pickup grants no ammo");
                        Assert.AreEqual(weapon.AmmoType, items[j].AmmoType, weapon.Id + " pickup ammo type must match the weapon");
                    }
                }
                Assert.IsTrue(found, weapon.Id + " has no pickup item, so it can never be found as loot");
            }
        }

        [Test]
        public void EveryEnemyArchetypeExistsAndIsBalanced()
        {
            string[] ids = { "enemy_soldier", "enemy_heavy", "enemy_rifleman", "enemy_raider", "enemy_elite" };
            HashSet<EnemyArchetype> archetypes = new HashSet<EnemyArchetype>();
            for (int i = 0; i < ids.Length; i++)
            {
                EnemyDef enemy = _content.GetEnemy(ids[i]);
                Assert.IsNotNull(enemy, "missing enemy " + ids[i]);
                archetypes.Add(enemy.Archetype);
                Assert.Greater(enemy.Health, 0f, ids[i] + " health");
                Assert.Greater(enemy.Damage, 0f, ids[i] + " damage");
                Assert.Greater(enemy.AttackRange, 1f, ids[i] + " attack range");
                Assert.Greater(enemy.DetectionRange, enemy.AttackRange, ids[i] + " must detect before it can shoot");
                Assert.Greater(enemy.HearingRange, 0f, ids[i] + " hearing");
                Assert.Greater(enemy.ChaseSpeed, enemy.MoveSpeed, ids[i] + " chase speed must exceed patrol speed");
                Assert.IsNotNull(_content.GetLootTable(enemy.LootTableId), ids[i] + " loot table");
                Assert.Greater(enemy.ExperienceReward, 0, ids[i] + " xp reward");
                Assert.GreaterOrEqual(enemy.ReactionTime, 0f, ids[i] + " reaction time");
            }

            Assert.AreEqual(5, archetypes.Count, "all five archetypes must be represented");
            Assert.IsTrue(archetypes.Contains(EnemyArchetype.Soldier));
            Assert.IsTrue(archetypes.Contains(EnemyArchetype.Heavy));
            Assert.IsTrue(archetypes.Contains(EnemyArchetype.Rifleman));
            Assert.IsTrue(archetypes.Contains(EnemyArchetype.Assault));
            Assert.IsTrue(archetypes.Contains(EnemyArchetype.Elite));
        }

        [Test]
        public void EnemyThreatScalesWithArchetype()
        {
            Assert.Greater(_content.GetEnemy("enemy_heavy").ThreatLevel, _content.GetEnemy("enemy_soldier").ThreatLevel);
            Assert.Greater(_content.GetEnemy("enemy_elite").ThreatLevel, _content.GetEnemy("enemy_heavy").ThreatLevel);
            Assert.Greater(_content.GetEnemy("enemy_elite").ScaledHealth(5), _content.GetEnemy("enemy_elite").ScaledHealth(1));
        }

        [Test]
        public void EveryItemHasIconAndValueSane()
        {
            List<ItemDef> items = _content.AllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDef item = items[i];
                Assert.IsFalse(string.IsNullOrEmpty(item.Id), "item id");
                Assert.IsFalse(string.IsNullOrEmpty(item.DisplayName), item.Id + " display name");
                Assert.IsFalse(string.IsNullOrEmpty(item.IconGlyph), item.Id + " glyph");
                Assert.GreaterOrEqual(item.Value, 0, item.Id + " value");
                Assert.GreaterOrEqual(item.MaxStack, 1, item.Id + " max stack");
                if (item.IsAmmo) Assert.Greater(item.AmmoPerUnit, 0, item.Id + " ammo per unit");
                if (item.Category == ItemCategory.Weapon) Assert.IsNotNull(_content.GetWeapon(item.WeaponId), item.Id + " weapon link");
            }
        }

        [Test]
        public void AchievementIdsUsedByRewardsExistInTheCatalog()
        {
            var catalog = new HashSet<string>(ContentDatabase.AllAchievementIds);
            MissionSummary summary;
            summary.MissionId = "mission_rust_ridge";
            summary.RunId = 1;
            summary.Success = true;
            summary.Failure = FailureReason.None;
            summary.Difficulty = 5;
            summary.ElapsedSeconds = 120f;
            summary.Kills = 30;
            summary.Headshots = 9;
            summary.Deaths = 0;
            summary.ObjectivesCompleted = 4;
            summary.ObjectivesTotal = 4;
            summary.LootValue = 5000;
            summary.LootCollected = 9;

            List<string> unlocked = new List<string>();
            RewardCalculator.ComputeAchievements(summary, new ProgressionModel(_content), unlocked);
            Assert.Greater(unlocked.Count, 0, "a perfect run should unlock achievements");
            for (int i = 0; i < unlocked.Count; i++)
            {
                Assert.IsTrue(catalog.Contains(unlocked[i]), "unknown achievement id " + unlocked[i]);
            }
        }

        [Test]
        public void StarterWeaponsExistAndAreCheapToObtain()
        {
            Assert.GreaterOrEqual(ContentDatabase.StarterWeapons.Length, 2);
            for (int i = 0; i < ContentDatabase.StarterWeapons.Length; i++)
            {
                WeaponDef def = _content.GetWeapon(ContentDatabase.StarterWeapons[i]);
                Assert.IsNotNull(def, "starter weapon " + ContentDatabase.StarterWeapons[i] + " missing");
                Assert.AreEqual(1, def.UnlockLevel, def.Id + " must be available at level 1");
                Assert.AreEqual(0, def.UnlockCost, def.Id + " must be free");
            }
        }
    }
}
