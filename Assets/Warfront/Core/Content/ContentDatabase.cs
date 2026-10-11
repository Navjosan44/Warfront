using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Authoritative content database: weapons, items, loot tables, enemies and missions.
    ///
    /// The data lives in code so the project boots identically from a fresh clone, but every type
    /// can be overridden by a Unity ScriptableObject asset dropped into Resources/Content
    /// (see WeaponAsset/ItemAsset/MissionAsset in the gameplay assembly) which is what designers
    /// edit in the inspector. One definition type per concept, no duplicated rules.
    /// </summary>
    public class ContentDatabase
    {
        private readonly Dictionary<string, WeaponDef> _weapons = new Dictionary<string, WeaponDef>(16);
        private readonly Dictionary<string, ItemDef> _items = new Dictionary<string, ItemDef>(48);
        private readonly Dictionary<string, LootTableDef> _lootTables = new Dictionary<string, LootTableDef>(12);
        private readonly Dictionary<string, EnemyDef> _enemies = new Dictionary<string, EnemyDef>(8);
        private readonly List<MissionDef> _missions = new List<MissionDef>(10);
        private readonly Dictionary<string, MissionDef> _missionsById = new Dictionary<string, MissionDef>(10);

        public static readonly string[] StarterWeapons = { "wpn_ar_vk7", "wpn_pistol_sidewinder" };

        public static readonly string[] AllAchievementIds =
        {
            "ach_first_blood", "ach_flawless_contract", "ach_untouched", "ach_cleaner",
            "ach_marksman", "ach_fast_hands", "ach_hardline", "ach_fully_kitted",
            "ach_deadzone_survivor", "ach_citadel_breaker"
        };

        public IList<MissionDef> Missions { get { return _missions; } }

        public static ContentDatabase CreateDefault()
        {
            ContentDatabase db = new ContentDatabase();
            db.BuildItems();
            db.BuildWeapons();
            db.BuildEnemies();
            db.BuildLootTables();
            db.BuildMissions();
            return db;
        }

        public WeaponDef GetWeapon(string id)
        {
            WeaponDef def;
            return !string.IsNullOrEmpty(id) && _weapons.TryGetValue(id, out def) ? def : null;
        }

        public ItemDef GetItem(string id)
        {
            ItemDef def;
            return !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out def) ? def : null;
        }

        public LootTableDef GetLootTable(string id)
        {
            LootTableDef def;
            return !string.IsNullOrEmpty(id) && _lootTables.TryGetValue(id, out def) ? def : null;
        }

        public EnemyDef GetEnemy(string id)
        {
            EnemyDef def;
            return !string.IsNullOrEmpty(id) && _enemies.TryGetValue(id, out def) ? def : null;
        }

        public MissionDef GetMission(string id)
        {
            MissionDef def;
            return !string.IsNullOrEmpty(id) && _missionsById.TryGetValue(id, out def) ? def : null;
        }

        public void Register(WeaponDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _weapons[def.Id] = def;
        }

        public void Register(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _items[def.Id] = def;
        }

        public void Register(LootTableDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _lootTables[def.Id] = def;
        }

        public void Register(MissionDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            if (!_missionsById.ContainsKey(def.Id)) _missions.Add(def);
            else
            {
                int index = _missions.FindIndex(m => m.Id == def.Id);
                if (index >= 0) _missions[index] = def;
            }
            _missionsById[def.Id] = def;
        }

        public List<WeaponDef> AllWeapons()
        {
            return new List<WeaponDef>(_weapons.Values);
        }

        public List<ItemDef> AllItems()
        {
            return new List<ItemDef>(_items.Values);
        }

        /// <summary>Missions in campaign order, which is what the mission select screen shows.</summary>
        public List<MissionDef> OrderedMissions()
        {
            List<MissionDef> ordered = new List<MissionDef>(_missions);
            ordered.Sort((a, b) =>
            {
                int order = a.Order.CompareTo(b.Order);
                if (order != 0) return order;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return ordered;
        }

        // ---------------------------------------------------------------- items

        private void AddItem(ItemDef def)
        {
            _items[def.Id] = def;
        }

        private void BuildItems()
        {
            AddItem(new ItemDef
            {
                Id = "ammo_light",
                DisplayName = "5.4mm Light Ammo",
                Description = "Standard issue rifle and SMG ammunition.",
                Category = ItemCategory.Ammo,
                IsAmmo = true,
                AmmoType = AmmoType.Light,
                AmmoPerUnit = 30,
                MaxStack = 999,
                Value = 3,
                IconGlyph = "A",
                DisplayColor = new Color(0.85f, 0.78f, 0.45f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "ammo_heavy",
                DisplayName = "8.1mm Heavy Ammo",
                Description = "Heavier rounds for marksman and machine gun platforms.",
                Category = ItemCategory.Ammo,
                IsAmmo = true,
                AmmoType = AmmoType.Heavy,
                AmmoPerUnit = 20,
                MaxStack = 999,
                Value = 5,
                IconGlyph = "H",
                DisplayColor = new Color(0.9f, 0.66f, 0.35f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "ammo_shell",
                DisplayName = "12ga Shells",
                Description = "Buckshot for close quarter breaching.",
                Category = ItemCategory.Ammo,
                IsAmmo = true,
                AmmoType = AmmoType.Shell,
                AmmoPerUnit = 12,
                MaxStack = 999,
                Value = 6,
                IconGlyph = "S",
                DisplayColor = new Color(0.85f, 0.45f, 0.3f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "ammo_sniper",
                DisplayName = "12.7mm Precision",
                Description = "Long range precision cartridges.",
                Category = ItemCategory.Ammo,
                IsAmmo = true,
                AmmoType = AmmoType.Sniper,
                AmmoPerUnit = 8,
                MaxStack = 999,
                Value = 10,
                IconGlyph = "P",
                DisplayColor = new Color(0.72f, 0.6f, 0.95f, 1f)
            });

            AddItem(new ItemDef
            {
                Id = "med_field_bandage",
                DisplayName = "Field Bandage",
                Description = "Stops bleeding and restores 35 health.",
                Category = ItemCategory.Medical,
                HealAmount = 35f,
                MaxStack = 6,
                Value = 60,
                IconGlyph = "+",
                DisplayColor = new Color(0.85f, 0.35f, 0.35f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "med_trauma_kit",
                DisplayName = "Trauma Kit",
                Description = "Full stabilisation. Restores 75 health.",
                Category = ItemCategory.Medical,
                HealAmount = 75f,
                MaxStack = 3,
                Value = 180,
                Rarity = Rarity.Uncommon,
                IconGlyph = "+",
                DisplayColor = new Color(0.95f, 0.4f, 0.45f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "plate_ceramic",
                DisplayName = "Ceramic Plate",
                Description = "Slots into the vest and adds 50 armour.",
                Category = ItemCategory.Equipment,
                ArmorAmount = 50f,
                MaxStack = 4,
                Value = 140,
                IconGlyph = "D",
                DisplayColor = new Color(0.6f, 0.75f, 0.9f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "mat_alloy",
                DisplayName = "Salvaged Alloy",
                Description = "Workshop material used to upgrade weapons.",
                Category = ItemCategory.Material,
                MaxStack = 99,
                Value = 45,
                IconGlyph = "M",
                DisplayColor = new Color(0.7f, 0.72f, 0.66f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "mat_circuit",
                DisplayName = "Signal Board",
                Description = "Rare electronics. Worth more to the workshop.",
                Category = ItemCategory.Material,
                MaxStack = 99,
                Value = 95,
                Rarity = Rarity.Uncommon,
                IconGlyph = "C",
                DisplayColor = new Color(0.5f, 0.9f, 0.7f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "intel_data_slate",
                DisplayName = "Data Slate",
                Description = "Recovered enemy intelligence.",
                Category = ItemCategory.Intel,
                MaxStack = 10,
                Value = 220,
                Rarity = Rarity.Rare,
                IconGlyph = "I",
                DisplayColor = new Color(0.4f, 0.7f, 1f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "obj_supply_crate",
                DisplayName = "Supply Crate",
                Description = "Command-issued supplies. Deliver it to the extraction point.",
                Category = ItemCategory.Objective,
                MaxStack = 1,
                Value = 0,
                IconGlyph = "!",
                DisplayColor = new Color(0.95f, 0.8f, 0.3f, 1f)
            });
            AddItem(new ItemDef
            {
                Id = "obj_comms_beacon",
                DisplayName = "Comms Module",
                Description = "Portable radio relay recovered from the field.",
                Category = ItemCategory.Objective,
                MaxStack = 1,
                Value = 0,
                IconGlyph = "!",
                DisplayColor = new Color(0.95f, 0.8f, 0.3f, 1f)
            });

            // Weapon pickup items (inventory grants the weapon, not a generic item).
            AddWeaponItem("item_wpn_vk7", "VK-7 Vanguard", "wpn_ar_vk7", AmmoType.Light, 90);
            AddWeaponItem("item_wpn_hornet", "SP-9 Hornet", "wpn_smg_hornet", AmmoType.Light, 120);
            AddWeaponItem("item_wpn_breaker", "SG-12 Breaker", "wpn_shotgun_breaker", AmmoType.Shell, 24);
            AddWeaponItem("item_wpn_longwatch", "LR-8 Longwatch", "wpn_sniper_longwatch", AmmoType.Sniper, 16);
            AddWeaponItem("item_wpn_sidewinder", "P-3 Sidewinder", "wpn_pistol_sidewinder", AmmoType.Light, 60);
            AddWeaponItem("item_wpn_watchman", "DM-6 Watchman", "wpn_dmr_watchman", AmmoType.Heavy, 40);
            AddWeaponItem("item_wpn_rampart", "LMG-5 Rampart", "wpn_lmg_rampart", AmmoType.Heavy, 120);
        }

        private void AddWeaponItem(string itemId, string displayName, string weaponId, AmmoType ammo, int startingAmmo)
        {
            AddItem(new ItemDef
            {
                Id = itemId,
                DisplayName = displayName,
                Description = "Field pickup - equips " + displayName + ".",
                Category = ItemCategory.Weapon,
                WeaponId = weaponId,
                AmmoType = ammo,
                AmmoPerUnit = startingAmmo,
                MaxStack = 1,
                Value = 300,
                Rarity = Rarity.Uncommon,
                IconGlyph = "W",
                DisplayColor = new Color(0.4f, 0.45f, 0.5f, 1f)
            });
        }

        // -------------------------------------------------------------- weapons

        private void BuildWeapons()
        {
            Register(new WeaponDef
            {
                Id = "wpn_ar_vk7",
                DisplayName = "VK-7 Vanguard",
                Class = WeaponClass.AssaultRifle,
                FireMode = FireMode.Auto,
                AmmoType = AmmoType.Light,
                Damage = 21f,
                RoundsPerMinute = 640f,
                MagazineSize = 30,
                ReloadTime = 2.1f,
                Range = 140f,
                FalloffStart = 45f,
                FalloffEnd = 100f,
                MinFalloffMultiplier = 0.62f,
                BaseSpread = 0.55f,
                SpreadPerShot = 0.32f,
                RecoilVertical = 0.55f,
                RecoilHorizontal = 0.22f,
                HeadshotMultiplier = 2f,
                CritChance = 0.05f,
                ArmorPenetration = 0.15f,
                UnlockLevel = 1,
                LootWeight = 130,
                DisplayColor = new Color(0.24f, 0.27f, 0.26f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_pistol_sidewinder",
                DisplayName = "P-3 Sidewinder",
                Class = WeaponClass.Pistol,
                FireMode = FireMode.Single,
                AmmoType = AmmoType.Light,
                Damage = 26f,
                RoundsPerMinute = 320f,
                MagazineSize = 14,
                ReloadTime = 1.5f,
                Range = 70f,
                FalloffStart = 20f,
                FalloffEnd = 50f,
                MinFalloffMultiplier = 0.5f,
                BaseSpread = 0.7f,
                SpreadPerShot = 0.5f,
                RecoilVertical = 0.8f,
                RecoilHorizontal = 0.25f,
                AdsSpeed = 11f,
                UnlockLevel = 1,
                LootWeight = 90,
                DisplayColor = new Color(0.2f, 0.2f, 0.22f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_smg_hornet",
                DisplayName = "SP-9 Hornet",
                Class = WeaponClass.SMG,
                FireMode = FireMode.Auto,
                AmmoType = AmmoType.Light,
                Damage = 15f,
                RoundsPerMinute = 880f,
                MagazineSize = 35,
                ReloadTime = 1.8f,
                Range = 60f,
                FalloffStart = 18f,
                FalloffEnd = 45f,
                MinFalloffMultiplier = 0.45f,
                BaseSpread = 0.9f,
                SpreadPerShot = 0.25f,
                MoveSpreadPenalty = 1.1f,
                RecoilVertical = 0.42f,
                RecoilHorizontal = 0.3f,
                UnlockLevel = 3,
                UnlockCost = 1800,
                LootWeight = 100,
                DisplayColor = new Color(0.3f, 0.32f, 0.3f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_shotgun_breaker",
                DisplayName = "SG-12 Breaker",
                Class = WeaponClass.Shotgun,
                FireMode = FireMode.Single,
                AmmoType = AmmoType.Shell,
                Damage = 13f,
                PelletsPerShot = 8,
                RoundsPerMinute = 75f,
                MagazineSize = 6,
                ReloadTime = 3f,
                Range = 40f,
                FalloffStart = 7f,
                FalloffEnd = 22f,
                MinFalloffMultiplier = 0.25f,
                BaseSpread = 3.4f,
                SpreadPerShot = 1.2f,
                RecoilVertical = 1.6f,
                RecoilHorizontal = 0.5f,
                HeadshotMultiplier = 1.5f,
                ArmorPenetration = 0.05f,
                UnlockLevel = 4,
                UnlockCost = 2600,
                LootWeight = 70,
                DisplayColor = new Color(0.28f, 0.22f, 0.2f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_dmr_watchman",
                DisplayName = "DM-6 Watchman",
                Class = WeaponClass.MarksmanRifle,
                FireMode = FireMode.Single,
                AmmoType = AmmoType.Heavy,
                Damage = 52f,
                RoundsPerMinute = 150f,
                MagazineSize = 12,
                ReloadTime = 2.4f,
                Range = 220f,
                FalloffStart = 90f,
                FalloffEnd = 180f,
                MinFalloffMultiplier = 0.8f,
                BaseSpread = 0.28f,
                RecoilVertical = 1.3f,
                RecoilHorizontal = 0.2f,
                HeadshotMultiplier = 2.4f,
                CritChance = 0.12f,
                CritMultiplier = 1.6f,
                ArmorPenetration = 0.35f,
                AdsFov = 32f,
                AdsSpeed = 6f,
                UnlockLevel = 6,
                UnlockCost = 4200,
                LootWeight = 60,
                DisplayColor = new Color(0.22f, 0.25f, 0.28f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_sniper_longwatch",
                DisplayName = "LR-8 Longwatch",
                Class = WeaponClass.SniperRifle,
                FireMode = FireMode.Single,
                AmmoType = AmmoType.Sniper,
                Damage = 118f,
                RoundsPerMinute = 48f,
                MagazineSize = 5,
                ReloadTime = 3.4f,
                Range = 320f,
                FalloffStart = 160f,
                FalloffEnd = 300f,
                MinFalloffMultiplier = 0.9f,
                BaseSpread = 0.12f,
                RecoilVertical = 2.4f,
                RecoilHorizontal = 0.18f,
                HeadshotMultiplier = 2.6f,
                CritChance = 0.18f,
                CritMultiplier = 1.8f,
                ArmorPenetration = 0.65f,
                AdsFov = 22f,
                AdsSpeed = 4.5f,
                EquipTime = 0.7f,
                UnlockLevel = 8,
                UnlockCost = 6500,
                LootWeight = 35,
                MinRarity = Rarity.Uncommon,
                DisplayColor = new Color(0.18f, 0.2f, 0.22f, 1f)
            });

            Register(new WeaponDef
            {
                Id = "wpn_lmg_rampart",
                DisplayName = "LMG-5 Rampart",
                Class = WeaponClass.LightMachineGun,
                FireMode = FireMode.Auto,
                AmmoType = AmmoType.Heavy,
                Damage = 24f,
                RoundsPerMinute = 700f,
                MagazineSize = 75,
                ReloadTime = 4.6f,
                Range = 110f,
                FalloffStart = 40f,
                FalloffEnd = 95f,
                MinFalloffMultiplier = 0.6f,
                BaseSpread = 1.1f,
                SpreadPerShot = 0.3f,
                MoveSpreadPenalty = 2.4f,
                RecoilVertical = 0.6f,
                RecoilHorizontal = 0.34f,
                ArmorPenetration = 0.4f,
                UnlockLevel = 10,
                UnlockCost = 8200,
                LootWeight = 40,
                MinRarity = Rarity.Rare,
                EquipTime = 0.75f,
                DisplayColor = new Color(0.26f, 0.26f, 0.24f, 1f)
            });
        }

        // -------------------------------------------------------------- enemies

        private void BuildEnemies()
        {
            _enemies["enemy_soldier"] = new EnemyDef
            {
                Id = "enemy_soldier",
                DisplayName = "Ashfall Soldier",
                Archetype = EnemyArchetype.Soldier,
                Health = 90f,
                Armor = 15f,
                Damage = 11f,
                RoundsPerMinute = 260f,
                Accuracy = 0.62f,
                AttackRange = 34f,
                PreferredRange = 22f,
                DetectionRange = 44f,
                FieldOfView = 130f,
                HearingRange = 28f,
                MoveSpeed = 3.1f,
                ChaseSpeed = 4.6f,
                RetreatHealthThreshold = 0.3f,
                ReactionTime = 0.5f,
                ExperienceReward = 45,
                CreditReward = 60,
                LootTableId = "loot_enemy_standard",
                LootChance = 0.85f,
                ThreatLevel = 1,
                BodyScale = 1f,
                BodyColor = new Color(0.32f, 0.36f, 0.3f, 1f)
            };

            _enemies["enemy_heavy"] = new EnemyDef
            {
                Id = "enemy_heavy",
                DisplayName = "Ironhide Heavy",
                Archetype = EnemyArchetype.Heavy,
                Health = 260f,
                Armor = 90f,
                Damage = 19f,
                RoundsPerMinute = 180f,
                Accuracy = 0.55f,
                AttackRange = 28f,
                PreferredRange = 14f,
                DetectionRange = 38f,
                FieldOfView = 120f,
                HearingRange = 20f,
                MoveSpeed = 2.1f,
                ChaseSpeed = 3.2f,
                RetreatHealthThreshold = 0.15f,
                ReactionTime = 0.85f,
                ExperienceReward = 110,
                CreditReward = 150,
                LootTableId = "loot_enemy_heavy",
                LootChance = 1f,
                ThreatLevel = 3,
                BodyScale = 1.28f,
                BodyColor = new Color(0.4f, 0.35f, 0.28f, 1f),
                SuppressesFire = true
            };

            _enemies["enemy_rifleman"] = new EnemyDef
            {
                Id = "enemy_rifleman",
                DisplayName = "Coalition Rifleman",
                Archetype = EnemyArchetype.Rifleman,
                Health = 75f,
                Armor = 10f,
                Damage = 16f,
                RoundsPerMinute = 200f,
                Accuracy = 0.78f,
                AttackRange = 55f,
                PreferredRange = 40f,
                DetectionRange = 64f,
                FieldOfView = 140f,
                HearingRange = 30f,
                MoveSpeed = 2.8f,
                ChaseSpeed = 3.8f,
                RetreatHealthThreshold = 0.35f,
                ReactionTime = 0.7f,
                ExperienceReward = 65,
                CreditReward = 85,
                LootTableId = "loot_enemy_standard",
                LootChance = 0.9f,
                ThreatLevel = 2,
                UsesCover = true,
                PrefersRanged = true,
                BodyScale = 0.98f,
                BodyColor = new Color(0.3f, 0.3f, 0.34f, 1f)
            };

            _enemies["enemy_raider"] = new EnemyDef
            {
                Id = "enemy_raider",
                DisplayName = "Rustbelly Raider",
                Archetype = EnemyArchetype.Assault,
                Health = 70f,
                Armor = 0f,
                Damage = 9f,
                RoundsPerMinute = 420f,
                Accuracy = 0.5f,
                AttackRange = 14f,
                PreferredRange = 6f,
                DetectionRange = 46f,
                FieldOfView = 150f,
                HearingRange = 34f,
                MoveSpeed = 3.8f,
                ChaseSpeed = 6.1f,
                RetreatHealthThreshold = 0.2f,
                ReactionTime = 0.3f,
                ExperienceReward = 40,
                CreditReward = 50,
                LootTableId = "loot_enemy_light",
                LootChance = 0.75f,
                ThreatLevel = 2,
                ChargeBehaviour = true,
                BodyScale = 0.94f,
                BodyColor = new Color(0.45f, 0.3f, 0.25f, 1f)
            };

            _enemies["enemy_elite"] = new EnemyDef
            {
                Id = "enemy_elite",
                DisplayName = "Vanguard Elite",
                Archetype = EnemyArchetype.Elite,
                Health = 420f,
                Armor = 140f,
                Damage = 24f,
                RoundsPerMinute = 380f,
                Accuracy = 0.84f,
                AttackRange = 52f,
                PreferredRange = 26f,
                DetectionRange = 70f,
                FieldOfView = 160f,
                HearingRange = 44f,
                MoveSpeed = 3.4f,
                ChaseSpeed = 5.2f,
                RetreatHealthThreshold = 0.25f,
                ReactionTime = 0.28f,
                ExperienceReward = 320,
                CreditReward = 480,
                LootTableId = "loot_enemy_elite",
                LootChance = 1f,
                ThreatLevel = 5,
                IsElite = true,
                UsesCover = true,
                SuppressesFire = true,
                BodyScale = 1.15f,
                BodyColor = new Color(0.24f, 0.22f, 0.3f, 1f)
            };
        }

        // ----------------------------------------------------------- loot tables

        private void BuildLootTables()
        {
            LootTableDef standard = new LootTableDef { Id = "loot_standard", MinRolls = 2, MaxRolls = 3, ExtraRollChance = 0.2f };
            standard.RarityWeights = new[] { 55, 28, 12, 4, 1 };
            standard.Add("ammo_light", 120, 1, 2, Rarity.Common, 1)
                    .Add("mat_alloy", 90, 1, 3, Rarity.Common, 1)
                    .Add("med_field_bandage", 70, 1, 2, Rarity.Common, 1)
                    .Add("plate_ceramic", 40, 1, 1, Rarity.Uncommon, 1)
                    .Add("mat_circuit", 35, 1, 2, Rarity.Uncommon, 1)
                    .Add("item_wpn_vk7", 22, 1, 1, Rarity.Uncommon, 3)
                    .Add("item_wpn_hornet", 18, 1, 1, Rarity.Uncommon, 3)
                    .Add("item_wpn_breaker", 14, 1, 1, Rarity.Rare, 3)
                    .Add("item_wpn_watchman", 10, 1, 1, Rarity.Rare, 4)
                    .Add("item_wpn_longwatch", 5, 1, 1, Rarity.Epic, 4)
                    .Add("med_trauma_kit", 26, 1, 1, Rarity.Uncommon, 1);
            Register(standard);

            LootTableDef secure = new LootTableDef { Id = "loot_secure", MinRolls = 3, MaxRolls = 4, ExtraRollChance = 0.35f };
            secure.RarityWeights = new[] { 34, 30, 22, 10, 4 };
            secure.Add("intel_data_slate", 60, 1, 2, Rarity.Rare, 1)
                  .Add("mat_circuit", 80, 2, 4, Rarity.Uncommon, 1)
                  .Add("mat_alloy", 70, 2, 5, Rarity.Common, 1)
                  .Add("ammo_heavy", 70, 2, 3, Rarity.Common, 1)
                  .Add("ammo_sniper", 45, 1, 2, Rarity.Uncommon, 1)
                  .Add("med_trauma_kit", 55, 1, 2, Rarity.Uncommon, 1)
                  .Add("plate_ceramic", 50, 1, 2, Rarity.Uncommon, 1)
                  .Add("item_wpn_watchman", 24, 1, 1, Rarity.Rare, 5)
                  .Add("item_wpn_longwatch", 16, 1, 1, Rarity.Epic, 5)
                  .Add("item_wpn_rampart", 10, 1, 1, Rarity.Epic, 5);
            Register(secure);

            LootTableDef enemyStandard = new LootTableDef { Id = "loot_enemy_standard", MinRolls = 1, MaxRolls = 2, ExtraRollChance = 0.15f };
            enemyStandard.RarityWeights = new[] { 68, 22, 8, 2, 0 };
            enemyStandard.Add("ammo_light", 140, 1, 2, Rarity.Common, 1)
                         .Add("med_field_bandage", 60, 1, 1, Rarity.Common, 1)
                         .Add("mat_alloy", 55, 1, 2, Rarity.Common, 1)
                         .Add("mat_circuit", 18, 1, 1, Rarity.Uncommon, 1)
                         .Add("item_wpn_hornet", 8, 1, 1, Rarity.Rare, 2)
                         .Add("intel_data_slate", 6, 1, 1, Rarity.Rare, 1);
            Register(enemyStandard);

            LootTableDef enemyLight = new LootTableDef { Id = "loot_enemy_light", MinRolls = 1, MaxRolls = 1, ExtraRollChance = 0.1f };
            enemyLight.RarityWeights = new[] { 78, 18, 4, 0, 0 };
            enemyLight.Add("ammo_light", 120, 1, 2, Rarity.Common, 1)
                      .Add("med_field_bandage", 45, 1, 1, Rarity.Common, 1)
                      .Add("mat_alloy", 40, 1, 1, Rarity.Common, 1);
            Register(enemyLight);

            LootTableDef enemyHeavy = new LootTableDef { Id = "loot_enemy_heavy", MinRolls = 2, MaxRolls = 3, ExtraRollChance = 0.25f };
            enemyHeavy.RarityWeights = new[] { 45, 30, 17, 6, 2 };
            enemyHeavy.Add("ammo_heavy", 110, 2, 3, Rarity.Common, 1)
                      .Add("plate_ceramic", 70, 1, 2, Rarity.Uncommon, 1)
                      .Add("mat_circuit", 60, 1, 3, Rarity.Uncommon, 1)
                      .Add("med_trauma_kit", 45, 1, 1, Rarity.Uncommon, 1)
                      .Add("item_wpn_rampart", 12, 1, 1, Rarity.Rare, 3);
            Register(enemyHeavy);

            LootTableDef enemyElite = new LootTableDef { Id = "loot_enemy_elite", MinRolls = 3, MaxRolls = 4, ExtraRollChance = 0.5f };
            enemyElite.RarityWeights = new[] { 18, 27, 30, 18, 7 };
            enemyElite.Add("intel_data_slate", 90, 2, 3, Rarity.Rare, 1)
                      .Add("mat_circuit", 80, 3, 5, Rarity.Uncommon, 1)
                      .Add("ammo_sniper", 60, 2, 3, Rarity.Uncommon, 1)
                      .Add("item_wpn_longwatch", 30, 1, 1, Rarity.Epic, 6)
                      .Add("item_wpn_rampart", 26, 1, 1, Rarity.Epic, 6)
                      .Add("item_wpn_watchman", 34, 1, 1, Rarity.Rare, 6);
            Register(enemyElite);

            LootTableDef supply = new LootTableDef { Id = "loot_supply_crate", MinRolls = 2, MaxRolls = 2, ExtraRollChance = 0f };
            supply.RarityWeights = new[] { 60, 26, 11, 3, 0 };
            supply.Add("ammo_light", 100, 2, 3, Rarity.Common, 1)
                  .Add("med_field_bandage", 80, 1, 2, Rarity.Common, 1)
                  .Add("plate_ceramic", 55, 1, 1, Rarity.Uncommon, 1)
                  .Add("mat_alloy", 70, 2, 4, Rarity.Common, 1);
            Register(supply);
        }

        // -------------------------------------------------------------- missions

        private void BuildMissions()
        {
            // ---------------------------------------------------------- tutorial
            MissionDef tutorial = new MissionDef
            {
                Id = "mission_tutorial",
                Order = 0,
                DisplayName = "First Light",
                RegionName = "Forward Operating Base Saber",
                Briefing =
                    "Welcome to the front, contractor. Before we hand you a real contract you will run the " +
                    "training lane: move, shoot, reload, recover supplies and walk out through the extraction " +
                    "zone. Get this right and the Coalition will not know what hit them.",
                Difficulty = 1,
                IsTutorial = true,
                Seed = 424242,
                MapSize = 70f,
                EnemyBudget = 4,
                MaxEnemiesAlive = 2,
                ExtractionHoldSeconds = 8f,
                LootContainers = 3,
                IntelCount = 0,
                RewardCredits = 150,
                RewardXp = 220,
                RewardMaterials = 1,
                LootRolls = 1,
                LootTableId = "loot_enemy_light",
                RarityLuck = 0f
            };
            tutorial.AddObjective(new ObjectiveDef("tut_shoot", ObjectiveType.EliminateCount, "Eliminate training drones", 3)
                { Description = "Neutralise the three canvas targets." });
            tutorial.AddObjective(new ObjectiveDef("tut_supplies", ObjectiveType.RecoverSupplies, "Recover training supplies", 1)
                { Description = "Open the marked supply crate." });
            tutorial.AddObjective(new ObjectiveDef("tut_loot", ObjectiveType.CollectIntel, "Collect the intel slate", 1)
                { Description = "Pick up the data slate from the container." });
            tutorial.AddObjective(new ObjectiveDef("tut_extract", ObjectiveType.ReachExtraction, "Reach the extraction point", 1)
                { Description = "Hold the extraction zone until the dropship confirms." });
            Register(tutorial);

            // -------------------------------------------------------------- m1
            MissionDef ridge = new MissionDef
            {
                Id = "mission_rust_ridge",
                Order = 1,
                DisplayName = "Rust Ridge",
                RegionName = "Rust Ridge Salvage Belt",
                Briefing =
                    "The Coalition pushed a screening force into the salvage belt. Secure the relay shack, " +
                    "recover the command supplies and pull out before their patrol rotation closes the valley.",
                Difficulty = 1,
                UnlockLevel = 1,
                RequiredMissionId = "mission_tutorial",
                Seed = 90210,
                MapSize = 120f,
                EnemyBudget = 12,
                MaxEnemiesAlive = 5,
                ExtractionHoldSeconds = 12f,
                LootContainers = 7,
                RewardCredits = 750,
                RewardXp = 620,
                RewardMaterials = 3,
                LootRolls = 2,
                LootTableId = "loot_standard",
                RarityLuck = 0.05f
            };
            ridge.AddObjective(new ObjectiveDef("ridge_secure", ObjectiveType.SecureLocation, "Secure the relay shack", 1)
                { Description = "Hold the marked shack until the area is clear." }.WithTag("secure_a"));
            ridge.AddObjective(new ObjectiveDef("ridge_supplies", ObjectiveType.RecoverSupplies, "Recover command supplies", 2)
                { Description = "Recover two supply crates from the belt." }.WithTag("supply"));
            ridge.AddObjective(new ObjectiveDef("ridge_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(ridge);

            // -------------------------------------------------------------- m2
            MissionDef hollow = new MissionDef
            {
                Id = "mission_signal_hollow",
                Order = 2,
                DisplayName = "Signal Hollow",
                RegionName = "Hollow Basin Relay",
                Briefing =
                    "A Coalition relay is pushing jamming traffic across our frequency. Slice into the centre, " +
                    "bring the comms module online and remove the officer running the operation.",
                Difficulty = 2,
                UnlockLevel = 2,
                RequiredMissionId = "mission_rust_ridge",
                Seed = 5150,
                MapSize = 140f,
                EnemyBudget = 16,
                MaxEnemiesAlive = 6,
                ExtractionHoldSeconds = 14f,
                LootContainers = 8,
                RewardCredits = 1100,
                RewardXp = 980,
                RewardMaterials = 4,
                LootRolls = 3,
                LootTableId = "loot_standard",
                RarityLuck = 0.1f
            };
            hollow.AddObjective(new ObjectiveDef("hollow_device", ObjectiveType.ActivateDevice, "Activate the comms relay", 1)
                { Description = "Bring the recovered comms module online." }.WithTag("device"));
            hollow.AddObjective(new ObjectiveDef("hollow_target", ObjectiveType.EliminateTarget, "Eliminate the signals officer", 1)
                { Description = "Remove the officer directing the jamming." }.WithTag("target_officer"));
            hollow.AddObjective(new ObjectiveDef("hollow_intel", ObjectiveType.CollectIntel, "Recover sigint slates", 2)
                { Description = "Two slates are cached around the basin." }.WithTag("intel"));
            hollow.AddObjective(new ObjectiveDef("hollow_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(hollow);

            // -------------------------------------------------------------- m3
            MissionDef depot = new MissionDef
            {
                Id = "mission_blackwater_depot",
                Order = 3,
                DisplayName = "Blackwater Depot",
                RegionName = "Blackwater Freight Depot",
                Briefing =
                    "Blackwater is the Coalition's forward fuel and ammunition store. Strip the manifests, " +
                    "secure the loading yard and leave before their Quick Reaction Force arrives.",
                Difficulty = 2,
                UnlockLevel = 3,
                RequiredMissionId = "mission_signal_hollow",
                Seed = 77123,
                MapSize = 150f,
                EnemyBudget = 18,
                MaxEnemiesAlive = 7,
                ExtractionHoldSeconds = 15f,
                LootContainers = 10,
                RewardCredits = 1350,
                RewardXp = 1150,
                RewardMaterials = 5,
                LootRolls = 3,
                LootTableId = "loot_secure",
                RarityLuck = 0.12f
            };
            depot.AddObjective(new ObjectiveDef("depot_intel", ObjectiveType.CollectIntel, "Recover shipping manifests", 3)
                { Description = "Three manifest slates are cached in the depot." }.WithTag("intel"));
            depot.AddObjective(new ObjectiveDef("depot_secure", ObjectiveType.SecureLocation, "Secure the loading yard", 1)
                { Description = "Clear and hold the loading yard." }.WithTag("secure_a"));
            depot.AddObjective(new ObjectiveDef("depot_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(depot);

            // -------------------------------------------------------------- m4
            MissionDef cinder = new MissionDef
            {
                Id = "mission_cinder_yards",
                Order = 4,
                DisplayName = "Cinder Yards",
                RegionName = "Cinder Foundry Yards",
                Briefing =
                    "An Ironhide section is billeted in the foundry. Command wants the section leader's head " +
                    "and the dispatch office burnt out of the network.",
                Difficulty = 3,
                UnlockLevel = 5,
                RequiredMissionId = "mission_blackwater_depot",
                Seed = 31337,
                MapSize = 160f,
                EnemyBudget = 22,
                MaxEnemiesAlive = 8,
                ExtractionHoldSeconds = 16f,
                LootContainers = 11,
                RewardCredits = 1800,
                RewardXp = 1520,
                RewardMaterials = 6,
                LootRolls = 4,
                LootTableId = "loot_secure",
                RarityLuck = 0.18f
            };
            cinder.AddObjective(new ObjectiveDef("cinder_target", ObjectiveType.EliminateTarget, "Eliminate the section leader", 1)
                { Description = "The Ironhide section leader is in the foundry hall." }.WithTag("target_elite"));
            cinder.AddObjective(new ObjectiveDef("cinder_device", ObjectiveType.ActivateDevice, "Plant the network tap", 1)
                { Description = "Bring the tap online in the dispatch office." }.WithTag("device"));
            cinder.AddObjective(new ObjectiveDef("cinder_intel", ObjectiveType.CollectIntel, "Recover dispatchslates", 2).WithTag("intel"));
            cinder.AddObjective(new ObjectiveDef("cinder_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(cinder);

            // -------------------------------------------------------------- m5
            MissionDef vault = new MissionDef
            {
                Id = "mission_iron_vault",
                Order = 5,
                DisplayName = "Iron Vault",
                RegionName = "Iron Vault Munitions Bunker",
                Briefing =
                    "The bunker holds Coalition munitions records and an elite garrison. Go deep, take the " +
                    "records, kill the garrison commander and climb out before the lockdown routine starts.",
                Difficulty = 4,
                UnlockLevel = 8,
                RequiredMissionId = "mission_cinder_yards",
                Seed = 24680,
                MapSize = 175f,
                EnemyBudget = 26,
                MaxEnemiesAlive = 8,
                ExtractionHoldSeconds = 18f,
                LootContainers = 13,
                RewardCredits = 2400,
                RewardXp = 2050,
                RewardMaterials = 8,
                LootRolls = 5,
                LootTableId = "loot_secure",
                RarityLuck = 0.24f
            };
            vault.AddObjective(new ObjectiveDef("vault_intel", ObjectiveType.CollectIntel, "Recover munitions records", 3).WithTag("intel"));
            vault.AddObjective(new ObjectiveDef("vault_secure", ObjectiveType.SecureLocation, "Secure the vault door", 1).WithTag("secure_a"));
            vault.AddObjective(new ObjectiveDef("vault_target", ObjectiveType.EliminateTarget, "Eliminate the garrison commander", 1)
                { Description = "The commander holds the bunker depths." }.WithTag("target_elite"));
            vault.AddObjective(new ObjectiveDef("vault_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(vault);

            // -------------------------------------------------------------- m6
            MissionDef perimeter = new MissionDef
            {
                Id = "mission_deadzone_perimeter",
                Order = 6,
                DisplayName = "Deadzone Perimeter",
                RegionName = "Deadzone Exclusion Perimeter",
                Briefing =
                    "Our forward team is pinned behind the exclusion line. Push through the perimeter, " +
                    "recover their supply drop, and hold the lane until the convoy reaches you.",
                Difficulty = 4,
                UnlockLevel = 11,
                RequiredMissionId = "mission_iron_vault",
                Seed = 60606,
                MapSize = 190f,
                EnemyBudget = 30,
                MaxEnemiesAlive = 9,
                ExtractionHoldSeconds = 20f,
                LootContainers = 14,
                RewardCredits = 2900,
                RewardXp = 2600,
                RewardMaterials = 9,
                LootRolls = 5,
                LootTableId = "loot_secure",
                RarityLuck = 0.3f,
                EnemyReinforceInterval = 35f
            };
            perimeter.AddObjective(new ObjectiveDef("dz_supplies", ObjectiveType.RecoverSupplies, "Recover the marked drop", 3).WithTag("supply"));
            perimeter.AddObjective(new ObjectiveDef("dz_survive", ObjectiveType.SurviveTime, "Hold the line for 4 minutes", 240)
                { Description = "Survive the counter-attack." }.WithHiddenProgress());
            perimeter.AddObjective(new ObjectiveDef("dz_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(perimeter);

            // -------------------------------------------------------------- m7
            MissionDef citadel = new MissionDef
            {
                Id = "mission_ashfall_citadel",
                Order = 7,
                DisplayName = "Ashfall Citadel",
                RegionName = "Ashfall Citadel Approach",
                Briefing =
                    "Everything the Coalition has left is garrisoned at the citadel. Break the garrison, " +
                    "recover the war ledger, kill their warlord and take the long walk home.",
                Difficulty = 5,
                UnlockLevel = 15,
                RequiredMissionId = "mission_deadzone_perimeter",
                Seed = 99991,
                MapSize = 210f,
                EnemyBudget = 34,
                MaxEnemiesAlive = 10,
                ExtractionHoldSeconds = 22f,
                LootContainers = 16,
                RewardCredits = 3800,
                RewardXp = 3400,
                RewardMaterials = 12,
                LootRolls = 6,
                LootTableId = "loot_secure",
                RarityLuck = 0.38f,
                EnemyReinforceInterval = 40f
            };
            citadel.AddObjective(new ObjectiveDef("cit_intel", ObjectiveType.CollectIntel, "Recover the war ledger", 4).WithTag("intel"));
            citadel.AddObjective(new ObjectiveDef("cit_device", ObjectiveType.ActivateDevice, "Bring down the citadel jammer", 1).WithTag("device"));
            citadel.AddObjective(new ObjectiveDef("cit_target", ObjectiveType.EliminateTarget, "Eliminate the warlord", 1).WithTag("target_elite"));
            citadel.AddObjective(new ObjectiveDef("cit_secure", ObjectiveType.SecureLocation, "Secure the citadel gate", 1).WithTag("secure_a"));
            citadel.AddObjective(new ObjectiveDef("cit_extract", ObjectiveType.ReachExtraction, "Extract", 1));
            Register(citadel);
        }

        /// <summary>Difficulty scaling applied to enemy health/damage as the campaign progresses.</summary>
        public static float DifficultyHealthScale(int difficulty)
        {
            return 1f + Mathf.Max(0, difficulty - 1) * 0.14f;
        }

        public static float DifficultyDamageScale(int difficulty)
        {
            return 1f + Mathf.Max(0, difficulty - 1) * 0.09f;
        }

        public static float DifficultyBudgetScale(int difficulty)
        {
            return 1f + Mathf.Max(0, difficulty - 1) * 0.18f;
        }
    }
}
