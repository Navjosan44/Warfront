using NUnit.Framework;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class InventoryTests
    {
        private ContentDatabase _content;
        private InventoryModel _inventory;

        [SetUp]
        public void SetUp()
        {
            _content = ContentDatabase.CreateDefault();
            _inventory = new InventoryModel(_content, 6);
        }

        [Test]
        public void AddingItemFillsFirstEmptySlot()
        {
            int leftover;
            InventoryResult result = _inventory.Add("mat_alloy", 3, Rarity.Common, 1, out leftover);

            Assert.AreEqual(InventoryResult.Success, result);
            Assert.AreEqual(0, leftover);
            Assert.AreEqual(1, _inventory.UsedSlots);
            Assert.AreEqual(3, _inventory.CountOf("mat_alloy"));
        }

        [Test]
        public void StacksAreMergedUpToMaxStack()
        {
            int leftover;
            _inventory.Add("mat_circuit", 50, Rarity.Uncommon, 1, out leftover);
            ItemDef def = _content.GetItem("mat_circuit");

            Assert.AreEqual(0, leftover);
            Assert.AreEqual(1, _inventory.UsedSlots);
            Assert.LessOrEqual(_inventory.CountOf("mat_circuit"), def.MaxStack);
            Assert.AreEqual(50, _inventory.CountOf("mat_circuit"));
        }

        [Test]
        public void OverflowIsReportedInsteadOfBeingLost()
        {
            InventoryModel small = new InventoryModel(_content, 4);
            ItemDef def = _content.GetItem("mat_alloy");
            int total = def.MaxStack * 4 + 10;

            int leftover;
            InventoryResult result = small.Add("mat_alloy", total, Rarity.Common, 1, out leftover);

            Assert.AreEqual(InventoryResult.PartialFill, result);
            Assert.AreEqual(10, leftover, "leftover must be returned to the caller, not silently voided");
            Assert.AreEqual(4, small.UsedSlots);
        }

        [Test]
        public void FullInventoryReportsFullFailure()
        {
            InventoryModel tiny = new InventoryModel(_content, 4);
            int leftover;
            for (int i = 0; i < 4; i++) tiny.Add("med_field_bandage", _content.GetItem("med_field_bandage").MaxStack, Rarity.Common, 1, out leftover);

            InventoryResult result = tiny.Add("plate_ceramic", 1, Rarity.Common, 1, out leftover);
            Assert.AreEqual(InventoryResult.Full, result);
            Assert.AreEqual(1, leftover);
        }

        [Test]
        public void UnknownItemIsRejected()
        {
            int leftover;
            InventoryResult result = _inventory.Add("item_that_does_not_exist", 1, Rarity.Common, 1, out leftover);
            Assert.AreEqual(InventoryResult.InvalidItem, result);
            Assert.AreEqual(0, _inventory.UsedSlots);
        }

        [Test]
        public void ZeroOrNegativeQuantityIsRejected()
        {
            int leftover;
            Assert.AreEqual(InventoryResult.InvalidItem, _inventory.Add("mat_alloy", 0, Rarity.Common, 1, out leftover));
            Assert.AreEqual(InventoryResult.InvalidItem, _inventory.Add("mat_alloy", -5, Rarity.Common, 1, out leftover));
            Assert.AreEqual(0, _inventory.UsedSlots);
        }

        [Test]
        public void AmmoItemsFeedTheSharedPoolInsteadOfEatingSlots()
        {
            int leftover;
            _inventory.Add("ammo_light", 2, Rarity.Common, 1, out leftover);
            _inventory.Add("ammo_light", 1, Rarity.Common, 1, out leftover);

            Assert.AreEqual(0, _inventory.UsedSlots, "ammunition is stored as a pool, not as slots");
            Assert.AreEqual(90, _inventory.AmmoReserve(AmmoType.Light));
            Assert.IsTrue(_inventory.ConsumeAmmo(AmmoType.Light, 30));
            Assert.AreEqual(60, _inventory.AmmoReserve(AmmoType.Light));
        }

        [Test]
        public void CannotConsumeMoreAmmoThanAvailable()
        {
            _inventory.AddAmmo(AmmoType.Heavy, 10);
            Assert.IsFalse(_inventory.ConsumeAmmo(AmmoType.Heavy, 11));
            Assert.AreEqual(10, _inventory.AmmoReserve(AmmoType.Heavy));
        }

        [Test]
        public void WeaponsGoToTheWeaponCollectionAndCannotBeDuplicated()
        {
            int leftover;
            _inventory.Add("item_wpn_vk7", 1, Rarity.Rare, 2, out leftover);
            _inventory.Add("item_wpn_vk7", 1, Rarity.Epic, 5, out leftover);

            Assert.AreEqual(1, _inventory.Weapons.Count, "the same weapon must never be duplicated");
            Assert.IsTrue(_inventory.HasWeapon("wpn_ar_vk7"));
            Assert.AreEqual(Rarity.Rare, _inventory.Weapons[0].Rarity);
            Assert.AreEqual(2, _inventory.Weapons[0].UpgradeLevel);
        }

        [Test]
        public void WeaponPickupGrantsStartingAmmunition()
        {
            int leftover;
            _inventory.Add("item_wpn_vk7", 1, Rarity.Common, 1, out leftover);
            Assert.Greater(_inventory.AmmoReserve(AmmoType.Light), 0);
        }

        [Test]
        public void WeaponCapacityIsEnforced()
        {
            int leftover;
            string[] weapons = { "item_wpn_vk7", "item_wpn_hornet", "item_wpn_breaker", "item_wpn_watchman", "item_wpn_longwatch", "item_wpn_rampart", "item_wpn_sidewinder" };
            for (int i = 0; i < weapons.Length; i++) _inventory.Add(weapons[i], 1, Rarity.Common, 1, out leftover);

            Assert.LessOrEqual(_inventory.Weapons.Count, InventoryModel.DefaultWeaponSlots);
        }

        [Test]
        public void RemoveReturnsActualRemovedCount()
        {
            int leftover;
            _inventory.Add("mat_alloy", 10, Rarity.Common, 1, out leftover);
            Assert.AreEqual(4, _inventory.Remove("mat_alloy", 4));
            Assert.AreEqual(6, _inventory.CountOf("mat_alloy"));
            Assert.AreEqual(6, _inventory.Remove("mat_alloy", 50));
            Assert.AreEqual(0, _inventory.CountOf("mat_alloy"));
        }

        [Test]
        public void DiscardSlotRejectsInvalidIndices()
        {
            Assert.IsFalse(_inventory.DiscardSlot(-1));
            Assert.IsFalse(_inventory.DiscardSlot(9999));
        }

        [Test]
        public void DiscardSlotRemovesTheWholeStack()
        {
            int leftover;
            _inventory.Add("mat_alloy", 5, Rarity.Common, 1, out leftover);
            Assert.IsTrue(_inventory.DiscardSlot(0));
            Assert.AreEqual(0, _inventory.CountOf("mat_alloy"));
            Assert.AreEqual(0, _inventory.UsedSlots);
        }

        [Test]
        public void SanitizeRemovesUnknownAndImpossibleEntries()
        {
            InventoryModel inventory = new InventoryModel(_content, 6);
            var slots = new System.Collections.Generic.List<ItemStack>();
            slots.Add(new ItemStack("does_not_exist", 5, Rarity.Common, 1));
            slots.Add(new ItemStack("mat_alloy", -3, Rarity.Common, 1));
            slots.Add(new ItemStack("mat_alloy", 99999, Rarity.Common, 99));
            slots.Add(new ItemStack("mat_alloy", 4, Rarity.Common, 1));

            inventory.LoadSlots(slots);
            Assert.AreEqual(0, inventory.CountOf("does_not_exist"), "unknown items must be dropped");

            ItemDef alloy = _content.GetItem("mat_alloy");
            int positiveSlots = 0;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                ItemStack slot = inventory.GetSlot(i);
                if (!slot.IsValid) continue;
                positiveSlots++;
                Assert.LessOrEqual(slot.Quantity, alloy.MaxStack, "per slot quantity must respect the stack size");
                Assert.GreaterOrEqual(slot.UpgradeLevel, 1);
                Assert.LessOrEqual(slot.UpgradeLevel, 10);
            }
            Assert.Greater(positiveSlots, 0);
            Assert.Greater(inventory.CountOf("mat_alloy"), 0);
        }

        [Test]
        public void AmmoReserveCannotExceedHardCap()
        {
            _inventory.AddAmmo(AmmoType.Shell, int.MaxValue);
            Assert.LessOrEqual(_inventory.AmmoReserve(AmmoType.Shell), 99999);
        }

        [Test]
        public void CapacityShrinkDropsOverflowSlots()
        {
            int leftover;
            // Objective crates do not stack, so six of them really occupy six slots.
            for (int i = 0; i < 6; i++) _inventory.Add("obj_supply_crate", 1, Rarity.Common, 1, out leftover);
            Assert.AreEqual(6, _inventory.UsedSlots);
            _inventory.SetCapacity(4, false);
            Assert.LessOrEqual(_inventory.UsedSlots, 4);
        }
    }
}
