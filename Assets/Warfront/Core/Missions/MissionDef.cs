using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public enum ObjectiveType
    {
        CollectIntel = 0,
        EliminateTarget = 1,
        RecoverSupplies = 2,
        SecureLocation = 3,
        ActivateDevice = 4,
        ReachExtraction = 5,
        EliminateCount = 6,
        SurviveTime = 7
    }

    [System.Serializable]
    public class ObjectiveDef
    {
        public string Id;
        public ObjectiveType Type;
        public string Title;
        public string Description;
        public int TargetCount = 1;
        /// <summary>For EliminateTarget/CollectIntel: the specific spawn marker or loot tag.</summary>
        public string TargetTag;
        public bool Optional;
        public bool HideProgress;

        public ObjectiveDef(string id, ObjectiveType type, string title, int targetCount)
        {
            Id = id;
            Type = type;
            Title = title;
            TargetCount = Mathf.Max(1, targetCount);
        }

        public ObjectiveDef WithTag(string tag)
        {
            TargetTag = tag;
            return this;
        }

        public ObjectiveDef AsOptional()
        {
            Optional = true;
            return this;
        }

        public ObjectiveDef WithHiddenProgress()
        {
            HideProgress = true;
            return this;
        }
    }

    /// <summary>Runtime progress for a single objective.</summary>
    public class ObjectiveProgress
    {
        public ObjectiveProgress(ObjectiveDef def)
        {
            Def = def;
        }

        public ObjectiveDef Def { get; private set; }
        public int Current { get; private set; }
        public bool Completed { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int TargetCount { get { return Def == null ? 1 : Def.TargetCount; } }
        public bool Required { get { return Def != null && !Def.Optional; } }

        public float Progress01
        {
            get
            {
                int target = Mathf.Max(1, TargetCount);
                return Completed ? 1f : WfMath.Clamp01((float)Current / target);
            }
        }

        public string ProgressLabel
        {
            get
            {
                if (Def == null) return string.Empty;
                if (Def.HideProgress) return Completed ? "Done" : "Active";
                switch (Def.Type)
                {
                    case ObjectiveType.ReachExtraction: return Completed ? "Reached" : "Pending";
                    case ObjectiveType.SecureLocation: return Completed ? "Secured" : "Secure it";
                    case ObjectiveType.ActivateDevice: return Completed ? "Activated" : "Activate";
                    default: return Current + " / " + TargetCount;
                }
            }
        }

        public bool Add(int amount)
        {
            if (Def == null || Completed) return false;
            Current = Mathf.Clamp(Current + Mathf.Max(0, amount), 0, TargetCount);
            if (Current >= TargetCount)
            {
                Completed = true;
                return true;
            }
            return false;
        }

        public bool Complete()
        {
            if (Def == null || Completed) return false;
            Current = TargetCount;
            Completed = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Completed) return;
            ElapsedSeconds += deltaTime;
        }

        public void ResetProgress()
        {
            Current = 0;
            Completed = false;
            ElapsedSeconds = 0f;
        }
    }

    /// <summary>Mission definition. Mirrored by MissionAsset (ScriptableObject) in the Unity layer.</summary>
    [System.Serializable]
    public class MissionDef
    {
        public string Id;
        public string DisplayName;
        public string RegionName;
        public string Briefing;
        public string Faction = "Ashfall Coalition";

        public int Difficulty = 1;
        /// <summary>Explicit campaign order. Sorting by id would break the prerequisite chain.</summary>
        public int Order;
        public int UnlockLevel = 1;
        public string RequiredMissionId;
        public bool IsTutorial;

        public int Seed = 1337;
        public float MapSize = 120f;
        public int EnemyBudget = 12;
        public int MaxEnemiesAlive = 6;
        public float ExtractionHoldSeconds = 12f;
        public float TimeLimitSeconds;
        public int LootContainers = 8;
        public int IntelCount = 3;
        public float EnemyReinforceInterval = 45f;

        public int RewardCredits = 600;
        public int RewardXp = 500;
        public int RewardMaterials = 3;
        public int LootRolls = 2;
        public string LootTableId = "loot_standard";
        public float RarityLuck;

        public List<ObjectiveDef> Objectives = new List<ObjectiveDef>(4);

        public MissionDef AddObjective(ObjectiveDef objective)
        {
            if (objective != null) Objectives.Add(objective);
            return this;
        }

        public ObjectiveDef FindObjective(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Objectives.Count; i++)
            {
                if (Objectives[i].Id == id) return Objectives[i];
            }
            return null;
        }

        /// <summary>Locations the level generator must place for this mission's objectives.</summary>
        public List<string> RequiredTags()
        {
            var tags = new List<string>(4);
            for (int i = 0; i < Objectives.Count; i++)
            {
                string tag = Objectives[i].TargetTag;
                if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag)) tags.Add(tag);
            }
            tags.Add("extraction");
            tags.Add("player_spawn");
            return tags;
        }

        public int RequiredObjectiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Objectives.Count; i++)
                {
                    if (!Objectives[i].Optional) count++;
                }
                return count;
            }
        }
    }
}
