using UnityEngine;

namespace Warfront.Core
{
    public enum EnemyArchetype
    {
        Soldier = 0,
        Heavy = 1,
        Rifleman = 2,
        Assault = 3,
        Elite = 4
    }

    /// <summary>
    /// Enemy archetype definition. Five original archetypes (Soldier, Heavy, Rifleman, Assault, Elite)
    /// differ in health, armour, engagement range, movement and behaviour flags - the FSM itself is
    /// shared, so adding an archetype is data, not code.
    /// </summary>
    [System.Serializable]
    public class EnemyDef
    {
        public string Id;
        public string DisplayName;
        public EnemyArchetype Archetype = EnemyArchetype.Soldier;

        public float Health = 90f;
        public float Armor = 10f;
        public float Damage = 12f;
        public float RoundsPerMinute = 240f;
        public float Accuracy = 0.6f;
        public float AttackRange = 34f;
        public float PreferredRange = 20f;
        public float MinEngagementRange = 5f;

        public float DetectionRange = 44f;
        public float FieldOfView = 130f;
        public float HearingRange = 28f;
        public float LoseTargetGraceSeconds = 4.5f;
        public float ReactionTime = 0.5f;

        public float MoveSpeed = 3f;
        public float ChaseSpeed = 4.5f;
        public float TurnSpeed = 320f;

        public float RetreatHealthThreshold = 0.3f;
        public float RetreatDuration = 4f;
        public float SearchDuration = 8f;
        public float AlertDuration = 3f;

        public int ExperienceReward = 45;
        public int CreditReward = 60;
        public string LootTableId = "loot_enemy_standard";
        public float LootChance = 0.85f;
        public int ThreatLevel = 1;

        public WeaponClass WeaponClass = WeaponClass.AssaultRifle;
        public AmmoType AmmoType = AmmoType.Light;
        public int BurstSize = 3;
        public float BurstPause = 0.6f;
        public float AimTime = 0.4f;

        public bool UsesCover;
        public bool SuppressesFire;
        public bool PrefersRanged;
        public bool ChargeBehaviour;
        public bool IsElite;
        public bool IsBoss;

        public float BodyScale = 1f;
        public Color BodyColor = new Color(0.32f, 0.36f, 0.3f, 1f);
        public Color AccentColor = new Color(0.85f, 0.55f, 0.25f, 1f);
        public string Callsign = "Coalition";

        /// <summary>Effective detection range after mission difficulty scaling.</summary>
        public float ScaledDetectionRange(int difficulty)
        {
            return DetectionRange * (1f + Mathf.Max(0, difficulty - 1) * 0.03f);
        }

        public float ScaledHealth(int difficulty)
        {
            return Health * ContentDatabase.DifficultyHealthScale(difficulty);
        }

        public float ScaledDamage(int difficulty)
        {
            return Damage * ContentDatabase.DifficultyDamageScale(difficulty);
        }

        /// <summary>Encounter weight used by the mission enemy budget planner.</summary>
        public int BudgetCost
        {
            get
            {
                switch (Archetype)
                {
                    case EnemyArchetype.Heavy: return 3;
                    case EnemyArchetype.Elite: return 5;
                    case EnemyArchetype.Rifleman: return 2;
                    case EnemyArchetype.Assault: return 2;
                    default: return 1;
                }
            }
        }

        public float SecondsBetweenShots
        {
            get { return RoundsPerMinute <= 0f ? 0.4f : 60f / RoundsPerMinute; }
        }
    }
}
