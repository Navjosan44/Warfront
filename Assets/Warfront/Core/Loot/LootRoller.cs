using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    [System.Serializable]
    public struct LootTableEntry
    {
        public string ItemId;
        public int Weight;
        public int MinQuantity;
        public int MaxQuantity;
        public Rarity MinRarity;
        public int MaxUpgradeLevel;
    }

    [System.Serializable]
    public class LootTableDef
    {
        public string Id;
        public List<LootTableEntry> Entries = new List<LootTableEntry>(8);
        public int MinRolls = 1;
        public int MaxRolls = 2;
        public float ExtraRollChance = 0f;
        /// <summary>Rarity weights ordered Common..Legendary. Used to upgrade rolled rarity.</summary>
        public int[] RarityWeights = { 70, 20, 8, 2, 0 };

        public LootTableDef Add(string itemId, int weight, int minQty, int maxQty, Rarity minRarity, int maxUpgrade)
        {
            LootTableEntry entry;
            entry.ItemId = itemId;
            entry.Weight = weight;
            entry.MinQuantity = minQty;
            entry.MaxQuantity = Mathf.Max(minQty, maxQty);
            entry.MinRarity = minRarity;
            entry.MaxUpgradeLevel = Mathf.Max(1, maxUpgrade);
            Entries.Add(entry);
            return this;
        }
    }

    /// <summary>
    /// Seeded loot generation. The same seed always produces the same drops, which makes loot
    /// reproducible for debugging, QA and (later) server-authoritative validation.
    /// </summary>
    public class LootRoller
    {
        private readonly ContentDatabase _content;
        private WfRandom _random;

        private int _seed;

        public LootRoller(ContentDatabase content, int seed)
        {
            _content = content;
            _seed = seed == 0 ? 12345 : seed;
            _random = new WfRandom(_seed);
        }

        public int Seed { get { return _seed; } }

        public void Reseed(int seed)
        {
            _seed = seed == 0 ? 12345 : seed;
            _random = new WfRandom(_seed);
        }

        public void ResetRandomFromSeed()
        {
            _random = new WfRandom(_seed);
        }

        /// <summary>
        /// Rolls a table. luck (0..1) biases rarity upward, rollBonus adds guaranteed extra rolls.
        /// All quantities are clamped to legal ranges so a table can never emit invalid stacks.
        /// </summary>
        public void Roll(string tableId, float luck, int rollBonus, List<LootDrop> results)
        {
            if (results == null) return;
            LootTableDef table = _content != null ? _content.GetLootTable(tableId) : null;
            if (table == null || table.Entries.Count == 0) return;

            int minRolls = Mathf.Max(0, table.MinRolls);
            int maxRolls = Mathf.Max(minRolls, table.MaxRolls);
            int guaranteed = _random.RangeInt(minRolls, maxRolls + 1) + Mathf.Max(0, rollBonus);
            // A single bonus roll may still trigger - it is chance based so tables stay surprising
            // without producing runaway loot counts.
            bool extra = table.ExtraRollChance > 0f && _random.Chance(table.ExtraRollChance);
            int rolls = guaranteed + (extra ? 1 : 0);

            for (int i = 0; i < rolls; i++)
            {
                LootDrop drop = RollOnce(table, luck);
                if (drop.Quantity > 0) results.Add(drop);
            }
        }

        private LootDrop RollOnce(LootTableDef table, float luck)
        {
            int[] weights = new int[table.Entries.Count];
            for (int i = 0; i < table.Entries.Count; i++)
            {
                weights[i] = Mathf.Max(1, table.Entries[i].Weight);
            }

            int index = _random.WeightedIndex(weights);
            LootDrop drop;
            drop.LootTableId = WfMath.Hash(table.Id);
            drop.Rarity = Rarity.Common;
            drop.UpgradeLevel = 1;
            drop.ItemId = null;
            drop.Quantity = 0;
            if (index < 0 || index >= table.Entries.Count) return drop;

            LootTableEntry entry = table.Entries[index];
            ItemDef item = _content != null ? _content.GetItem(entry.ItemId) : null;

            int quantity = _random.RangeInt(entry.MinQuantity, entry.MaxQuantity + 1);
            quantity = Mathf.Max(1, quantity);

            // Stacking rules: non-stackables can never roll more than one per slot.
            if (item != null && !item.IsStackable) quantity = 1;

            Rarity rarity = RollRarity(table, entry.MinRarity, luck);
            int upgradeLevel = item != null && item.Category == ItemCategory.Weapon && item.WeaponId != null
                ? _random.RangeInt(1, Mathf.Max(1, entry.MaxUpgradeLevel) + 1)
                : 1;

            drop.ItemId = entry.ItemId;
            drop.Quantity = quantity;
            drop.Rarity = rarity;
            drop.UpgradeLevel = upgradeLevel;
            return drop;
        }

        private Rarity RollRarity(LootTableDef table, Rarity minRarity, float luck)
        {
            int[] baseWeights = table.RarityWeights ?? new[] { 70, 20, 8, 2, 0 };
            float clampedLuck = WfMath.Clamp01(luck);
            int[] weights = new int[baseWeights.Length];
            for (int i = 0; i < baseWeights.Length; i++)
            {
                // Luck shifts weight from common tiers into higher tiers.
                float bias = 1f + clampedLuck * i * 0.55f;
                weights[i] = Mathf.Max(0, Mathf.RoundToInt(baseWeights[i] * bias));
            }

            int index = _random.WeightedIndex(weights);
            Rarity rolled = index <= 0 ? Rarity.Common : (Rarity)Mathf.Min(index, (int)Rarity.Legendary);
            return (Rarity)Mathf.Max((int)rolled, (int)minRarity);
        }
    }
}
