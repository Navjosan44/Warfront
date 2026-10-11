using System.Collections.Generic;
using NUnit.Framework;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class LootRollerTests
    {
        private ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
        }

        [Test]
        public void SameSeedProducesIdenticalDrops()
        {
            List<LootDrop> first = RollWithSeed("loot_standard", 4242, 0f, 5);
            List<LootDrop> second = RollWithSeed("loot_standard", 4242, 0f, 5);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].ItemId, second[i].ItemId);
                Assert.AreEqual(first[i].Quantity, second[i].Quantity);
                Assert.AreEqual(first[i].Rarity, second[i].Rarity);
                Assert.AreEqual(first[i].UpgradeLevel, second[i].UpgradeLevel);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentDrops()
        {
            List<LootDrop> a = RollWithSeed("loot_standard", 1, 0f, 8);
            List<LootDrop> b = RollWithSeed("loot_standard", 987654, 0f, 8);
            Assert.IsFalse(SameContent(a, b), "different seeds should not produce identical sessions");
        }

        [Test]
        public void DropsAlwaysReferenceRealItems()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                List<LootDrop> drops = RollWithSeed("loot_standard", seed, 0.2f, 3);
                for (int i = 0; i < drops.Count; i++)
                {
                    Assert.IsNotNull(_content.GetItem(drops[i].ItemId), "loot referenced unknown item " + drops[i].ItemId);
                    Assert.Greater(drops[i].Quantity, 0);
                }
            }
        }

        [Test]
        public void NonStackableItemsNeverRollMoreThanOne()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                List<LootDrop> drops = RollWithSeed("loot_secure", seed, 0.5f, 4);
                for (int i = 0; i < drops.Count; i++)
                {
                    ItemDef item = _content.GetItem(drops[i].ItemId);
                    if (item != null && !item.IsStackable) Assert.AreEqual(1, drops[i].Quantity, item.Id + " stacked illegally");
                }
            }
        }

        [Test]
        public void HighLuckShiftsRarityDistributionUpward()
        {
            int commonLow = 0;
            int highTierLow = 0;
            int commonHigh = 0;
            int highTierHigh = 0;

            for (int seed = 0; seed < 300; seed++)
            {
                List<LootDrop> low = RollWithSeed("loot_standard", seed, 0f, 3);
                List<LootDrop> high = RollWithSeed("loot_standard", seed, 1f, 3);
                for (int i = 0; i < low.Count; i++)
                {
                    if (low[i].Rarity == Rarity.Common) commonLow++;
                    if (low[i].Rarity >= Rarity.Rare) highTierLow++;
                }
                for (int i = 0; i < high.Count; i++)
                {
                    if (high[i].Rarity == Rarity.Common) commonHigh++;
                    if (high[i].Rarity >= Rarity.Rare) highTierHigh++;
                }
            }

            Assert.Less(commonHigh, commonLow, "luck should reduce common drops");
            Assert.Greater(highTierHigh, highTierLow, "luck should increase rare drops");
        }

        [Test]
        public void RollBonusAddsExtraRolls()
        {
            List<LootDrop> baseline = RollWithSeed("loot_enemy_light", 77, 0f, 0);
            List<LootDrop> bonus = RollWithSeed("loot_enemy_light", 77, 0f, 3);
            Assert.Greater(bonus.Count, baseline.Count);
        }

        [Test]
        public void UnknownTableProducesNothingAndDoesNotThrow()
        {
            LootRoller roller = new LootRoller(_content, 5);
            List<LootDrop> drops = new List<LootDrop>();
            Assert.DoesNotThrow(() => roller.Roll("table_that_does_not_exist", 0f, 2, drops));
            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void WeaponDropsRespectUpgradeLevelCeiling()
        {
            List<LootDrop> drops = new List<LootDrop>();
            for (int seed = 0; seed < 50; seed++)
            {
                LootRoller roller = new LootRoller(_content, seed);
                roller.Roll("loot_secure", 0.6f, 2, drops);
            }

            for (int i = 0; i < drops.Count; i++)
            {
                Assert.LessOrEqual(drops[i].UpgradeLevel, 6, "secure table caps weapon drops at upgrade level 6");
                Assert.GreaterOrEqual(drops[i].UpgradeLevel, 1);
            }
        }

        [Test]
        public void RngIsStableAcrossIdenticalSequences()
        {
            WfRandom a = new WfRandom(1234);
            WfRandom b = new WfRandom(1234);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void RngFloatStaysInUnitRange()
        {
            WfRandom rng = new WfRandom(99);
            for (int i = 0; i < 1000; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void WeightedIndexNeverReturnsOutOfRange()
        {
            WfRandom rng = new WfRandom(7);
            int[] weights = { 0, 5, 0, 3 };
            for (int i = 0; i < 500; i++)
            {
                int index = rng.WeightedIndex(weights);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, weights.Length);
                Assert.AreNotEqual(0, weights[index]);
            }
        }

        private List<LootDrop> RollWithSeed(string tableId, int seed, float luck, int bonus)
        {
            LootRoller roller = new LootRoller(_content, seed);
            List<LootDrop> drops = new List<LootDrop>();
            roller.Roll(tableId, luck, bonus, drops);
            return drops;
        }

        private static bool SameContent(List<LootDrop> a, List<LootDrop> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].ItemId != b[i].ItemId || a[i].Quantity != b[i].Quantity || a[i].Rarity != b[i].Rarity) return false;
            }
            return true;
        }
    }
}
