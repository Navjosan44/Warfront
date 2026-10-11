using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    public enum InventoryResult
    {
        Success = 0,
        PartialFill = 1,
        Full = 2,
        InvalidItem = 3,
        NotOwned = 4,
        InvalidQuantity = 5
    }

    public struct InventorySlotState
    {
        public string ItemId;
        public int Quantity;
        public Rarity Rarity;
        public int UpgradeLevel;
        public bool IsEmpty { get { return string.IsNullOrEmpty(ItemId) || Quantity <= 0; } }
    }

    /// <summary>
    /// Authoritative inventory. Every mutation goes through validation so items can never be
    /// duplicated, lost silently, or pushed into impossible states (negative counts, over-capacity).
    /// </summary>
    public class InventoryModel
    {
        public const int DefaultCapacity = 24;
        public const int DefaultWeaponSlots = 6;

        private readonly ContentDatabase _content;
        private readonly List<ItemStack> _slots = new List<ItemStack>(DefaultCapacity);
        private readonly List<ItemStack> _weapons = new List<ItemStack>(DefaultWeaponSlots);
        private readonly int[] _ammoReserve = new int[4];

        public InventoryModel(ContentDatabase content, int capacity)
        {
            _content = content;
            Capacity = Mathf.Max(4, capacity);
            for (int i = 0; i < Capacity; i++) _slots.Add(default(ItemStack));
        }

        public int Capacity { get; private set; }
        public int AmmoReserve(AmmoType type) { return _ammoReserve[(int)type]; }
        public void SetAmmoReserve(AmmoType type, int amount) { _ammoReserve[(int)type] = Mathf.Clamp(amount, 0, 99999); }
        public List<ItemStack> Weapons { get { return _weapons; } }
        public int WeaponCapacity { get { return DefaultWeaponSlots; } }

        public void SetCapacity(int capacity, bool keepOverflow)
        {
            int clamped = Mathf.Clamp(capacity, 4, 128);
            if (clamped == Capacity) return;
            if (clamped < Capacity && !keepOverflow)
            {
                for (int i = clamped; i < _slots.Count; i++) _slots[i] = default(ItemStack);
            }
            while (_slots.Count < clamped) _slots.Add(default(ItemStack));
            Capacity = clamped;
        }

        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return default(ItemStack);
            return _slots[index];
        }

        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsValid) used++;
                }
                return used;
            }
        }

        public bool HasSpace { get { return UsedSlots < Capacity; } }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId) total += _slots[i].Quantity;
            }
            return total;
        }

        public bool Has(string itemId, int quantity)
        {
            return CountOf(itemId) >= Mathf.Max(1, quantity);
        }

        /// <summary>
        /// Adds a stack. Returns PartialFill/Full when the inventory could not take everything;
        /// leftover is reported so the caller can drop it back into the world instead of voiding it.
        /// </summary>
        public InventoryResult Add(ItemStack stack, out int leftover)
        {
            leftover = Mathf.Max(0, stack.Quantity);
            if (!stack.IsValid) return InventoryResult.InvalidItem;

            ItemDef def = _content != null ? _content.GetItem(stack.ItemId) : null;
            if (def == null) return InventoryResult.InvalidItem;

            Rarity rarity = def.Category == ItemCategory.Weapon ? stack.Rarity : Rarity.Common;
            int upgrade = def.Category == ItemCategory.Weapon ? Mathf.Clamp(stack.UpgradeLevel, 1, 10) : 1;
            int maxStack = def.IsStackable ? def.MaxStack : 1;

            // Weapons live in their own collection and also occupy a slot each.
            if (def.Category == ItemCategory.Weapon)
            {
                if (_weapons.Count >= WeaponCapacity) return InventoryResult.Full;
                if (HasWeapon(def.WeaponId)) return InventoryResult.Success; // never duplicate the same weapon
                _weapons.Add(new ItemStack(stack.ItemId, 1, rarity, upgrade));
                if (def.AmmoPerUnit > 0) AddAmmo(def.AmmoType, def.AmmoPerUnit);
                leftover = 0;
                return InventoryResult.Success;
            }

            if (def.IsAmmo)
            {
                // Ammo is stored as a numeric pool - no slot waste, no duplication.
                int amount = def.AmmoPerUnit > 0 ? def.AmmoPerUnit * stack.Quantity : stack.Quantity;
                _ammoReserve[(int)def.AmmoType] = Mathf.Clamp(_ammoReserve[(int)def.AmmoType] + amount, 0, 99999);
                leftover = 0;
                return InventoryResult.Success;
            }

            int remaining = stack.Quantity;

            // Fill existing stacks first.
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                ItemStack slot = _slots[i];
                if (!slot.IsValid || slot.ItemId != stack.ItemId) continue;
                if (slot.Quantity >= maxStack) continue;
                int add = Mathf.Min(maxStack - slot.Quantity, remaining);
                slot.Quantity += add;
                _slots[i] = slot;
                remaining -= add;
            }

            // Then use empty slots.
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                if (_slots[i].IsValid) continue;
                int add = Mathf.Min(maxStack, remaining);
                _slots[i] = new ItemStack(stack.ItemId, add, rarity, upgrade);
                remaining -= add;
            }

            leftover = remaining;
            if (leftover <= 0) return InventoryResult.Success;
            return leftover >= stack.Quantity ? InventoryResult.Full : InventoryResult.PartialFill;
        }

        public InventoryResult Add(string itemId, int quantity, Rarity rarity, int upgradeLevel, out int leftover)
        {
            return Add(new ItemStack(itemId, quantity, rarity, upgradeLevel), out leftover);
        }

        public bool HasWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            for (int i = 0; i < _weapons.Count; i++)
            {
                ItemStack w = _weapons[i];
                ItemDef def = _content != null ? _content.GetItem(w.ItemId) : null;
                if (def != null && def.WeaponId == weaponId) return true;
            }
            return false;
        }

        /// <summary>Removes up to 'quantity' of an item, returns how many were actually removed.</summary>
        public int Remove(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return 0;
            int remaining = quantity;
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                ItemStack slot = _slots[i];
                if (!slot.IsValid || slot.ItemId != itemId) continue;
                int take = Mathf.Min(slot.Quantity, remaining);
                slot.Quantity -= take;
                remaining -= take;
                _slots[i] = slot.Quantity > 0 ? slot : default(ItemStack);
            }
            return quantity - remaining;
        }

        /// <summary>Discards a full slot (no partial semantics) - used by the inventory screen.</summary>
        public bool DiscardSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;
            if (!_slots[index].IsValid) return false;
            _slots[index] = default(ItemStack);
            return true;
        }

        public bool ConsumeAmmo(AmmoType type, int amount)
        {
            if (amount <= 0) return true;
            int pool = _ammoReserve[(int)type];
            if (pool < amount) return false;
            _ammoReserve[(int)type] = pool - amount;
            return true;
        }

        public void AddAmmo(AmmoType type, int amount)
        {
            if (amount <= 0) return;
            SetAmmoReserve(type, _ammoReserve[(int)type] + amount);
        }

        public int CountCategory(ItemCategory category)
        {
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsValid) continue;
                ItemDef def = _content != null ? _content.GetItem(_slots[i].ItemId) : null;
                if (def != null && def.Category == category) total += _slots[i].Quantity;
            }
            return total;
        }

        /// <summary>Scrubs the inventory of impossible values (corrupt save, bad migration, bug).</summary>
        public void Sanitize()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemStack slot = _slots[i];
                if (string.IsNullOrEmpty(slot.ItemId) || slot.Quantity <= 0)
                {
                    _slots[i] = default(ItemStack);
                    continue;
                }
                ItemDef def = _content != null ? _content.GetItem(slot.ItemId) : null;
                if (def == null || def.Category == ItemCategory.Weapon)
                {
                    _slots[i] = default(ItemStack);
                    continue;
                }
                slot.Quantity = Mathf.Clamp(slot.Quantity, 1, def.IsStackable ? def.MaxStack : 1);
                slot.UpgradeLevel = Mathf.Clamp(slot.UpgradeLevel <= 0 ? 1 : slot.UpgradeLevel, 1, 10);
                _slots[i] = slot;
            }

            for (int i = _weapons.Count - 1; i >= 0; i--)
            {
                ItemDef def = _content != null ? _content.GetItem(_weapons[i].ItemId) : null;
                if (def == null || def.Category != ItemCategory.Weapon) _weapons.RemoveAt(i);
            }

            for (int i = 0; i < _ammoReserve.Length; i++)
            {
                _ammoReserve[i] = Mathf.Clamp(_ammoReserve[i], 0, 99999);
            }
        }

        public List<ItemStack> SnapshotSlots()
        {
            var copy = new List<ItemStack>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++) copy.Add(_slots[i]);
            return copy;
        }

        public void LoadSlots(List<ItemStack> slots)
        {
            if (slots == null) return;
            while (_slots.Count < Capacity) _slots.Add(default(ItemStack));
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i] = i < slots.Count ? slots[i] : default(ItemStack);
            }
            Sanitize();
        }
    }
}
