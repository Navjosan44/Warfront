using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>One equipped weapon: the pure model plus its scene runtime.</summary>
    public class WeaponSlot
    {
        public WeaponDef Def;
        public WeaponModel Model;
        public WeaponRuntime Runtime;
        public ItemStack Item;
        public string ItemId;
        public bool IsValid { get { return Def != null && Model != null && Runtime != null; } }
        public string DisplayName { get { return Def != null ? Def.DisplayName : "Empty"; } }
        public Rarity Rarity { get { return Model != null ? Model.Rarity : Rarity.Common; } }
        public int UpgradeLevel { get { return Model != null ? Model.UpgradeLevel : 1; } }
    }

    /// <summary>
    /// Owns the player's equipped weapons and is the single place that talks to the inventory about
    /// ammunition. Reserve ammo is never duplicated: the weapon model consumes the inventory pool
    /// directly, so looted ammo is the only source of reloads.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        public const int SlotCount = 3;

        private readonly WeaponSlot[] _slots = new WeaponSlot[SlotCount];
        private GameContext _context;
        private InventoryModel _inventory;
        private CharacterRig _rig;
        private int _activeIndex = -1;
        private float _switchCooldown;

        public int ActiveIndex { get { return _activeIndex; } }
        public WeaponSlot Active { get { return _activeIndex >= 0 && _activeIndex < SlotCount ? _slots[_activeIndex] : null; } }
        public WeaponModel CurrentModel { get { return Active != null ? Active.Model : null; } }
        public WeaponDef CurrentDef { get { return Active != null ? Active.Def : null; } }

        public WeaponStats CurrentStats
        {
            get
            {
                WeaponSlot slot = Active;
                if (slot == null || slot.Model == null)
                {
                    WeaponStats empty = default(WeaponStats);
                    empty.AdsFov = 45f;
                    empty.HeadshotMultiplier = 2f;
                    return empty;
                }
                return slot.Model.Stats;
            }
        }

        public WeaponSlot GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        public void Initialize(GameContext context, InventoryModel inventory, CharacterRig rig)
        {
            _context = context;
            _inventory = inventory;
            _rig = rig;
        }

        /// <summary>
        /// Builds the three weapon slots from the player's inventory, filling gaps with the starter
        /// loadout so a player can never end up unarmed (and therefore soft locked).
        /// </summary>
        public void BuildFromInventory(ContentDatabase content, InventoryModel inventory, LoadoutSave loadout)
        {
            _inventory = inventory;
            for (int i = 0; i < SlotCount; i++) _slots[i] = null;

            string primary = loadout != null ? loadout.PrimaryWeaponId : null;
            string secondary = loadout != null ? loadout.SecondaryWeaponId : null;
            string sidearm = loadout != null ? loadout.SidearmWeaponId : null;

            if (string.IsNullOrEmpty(primary) || content.GetWeapon(primary) == null) primary = FirstStarter(content, 0);
            if (string.IsNullOrEmpty(sidearm) || content.GetWeapon(sidearm) == null) sidearm = FirstStarter(content, 1);

            _slots[0] = CreateSlot(content, inventory, primary);
            _slots[1] = CreateSlot(content, inventory, secondary);
            _slots[2] = CreateSlot(content, inventory, sidearm);

            int firstValid = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null && _slots[i].IsValid && firstValid < 0) firstValid = i;
            }

            if (firstValid < 0)
            {
                // Absolute fallback: the player is always armed.
                _slots[0] = CreateSlot(content, inventory, FirstStarter(content, 0));
                firstValid = 0;
            }

            _activeIndex = firstValid;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null || !_slots[i].IsValid) continue;
                _slots[i].Runtime.SetEquipped(i == _activeIndex, true);
            }
            PublishWeaponChanged();
        }

        private static string FirstStarter(ContentDatabase content, int index)
        {
            if (ContentDatabase.StarterWeapons.Length == 0) return "wpn_ar_vk7";
            int clamped = Mathf.Clamp(index, 0, ContentDatabase.StarterWeapons.Length - 1);
            return ContentDatabase.StarterWeapons[clamped];
        }

        private WeaponSlot CreateSlot(ContentDatabase content, InventoryModel inventory, string weaponId)
        {
            WeaponDef def = content.GetWeapon(weaponId);
            if (def == null) return null;

            Rarity rarity = Rarity.Common;
            int upgradeLevel = 1;
            string itemId = ResolveWeaponItem(content, inventory, def.Id, ref rarity, ref upgradeLevel);

            WeaponSlot slot = new WeaponSlot();
            slot.Def = def;
            slot.Model = new WeaponModel(def, rarity, upgradeLevel, WfMath.Hash(def.Id) + 7919);
            slot.ItemId = itemId;
            slot.Item = new ItemStack(itemId, 1, rarity, upgradeLevel);

            if (_rig != null && _context != null && _context.Assets != null)
            {
                GameObject model = WeaponBuilder.Build(def, _rig.WeaponAnchor, _context.Assets, _rig.RootObject.layer);
                slot.Runtime = model.AddComponent<WeaponRuntime>();
                slot.Runtime.Initialize(_context, this, model, def, slot.Model);
                _rig.AttachWeapon(model);
            }

            return slot;
        }

        /// <summary>
        /// Finds the inventory item that grants this weapon and reports how good it is. Falls back to
        /// the plain item id so a starter weapon given directly by the campaign still equips.
        /// </summary>
        private static string ResolveWeaponItem(ContentDatabase content, InventoryModel inventory, string weaponId, ref Rarity rarity, ref int upgradeLevel)
        {
            if (inventory != null)
            {
                List<ItemStack> weapons = inventory.Weapons;
                for (int i = 0; i < weapons.Count; i++)
                {
                    ItemStack stack = weapons[i];
                    ItemDef item = content.GetItem(stack.ItemId);
                    if (item == null || item.WeaponId != weaponId) continue;
                    rarity = stack.Rarity;
                    upgradeLevel = stack.UpgradeLevel;
                    return stack.ItemId;
                }
            }

            List<ItemDef> items = content.AllItems();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Category == ItemCategory.Weapon && items[i].WeaponId == weaponId) return items[i].Id;
            }
            return null;
        }

        private void Update()
        {
            if (_context == null || _context.Input == null) return;
            if (_context.Flow != null && _context.Flow.IsPaused) return;

            float dt = Time.deltaTime;
            _switchCooldown = Mathf.Max(0f, _switchCooldown - dt);
            InputService input = _context.Input;

            if (_switchCooldown <= 0f)
            {
                if (input.WeaponSlotPressed >= 0) SwitchTo(input.WeaponSlotPressed);
                else if (input.WeaponSwitchPressed) SwitchTo(NextValidIndex());
            }

            if (input.UseEquipmentPressed) UseEquipment();
        }

        public void SwitchTo(int index)
        {
            if (index < 0 || index >= SlotCount || index == _activeIndex) return;
            WeaponSlot slot = _slots[index];
            if (slot == null || !slot.IsValid) return;

            WeaponSlot previous = Active;
            if (previous != null && previous.IsValid) previous.Runtime.SetEquipped(false, false);
            _activeIndex = index;
            slot.Runtime.SetEquipped(true, true);
            _switchCooldown = 0.45f;
            if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.WeaponSwap);
            PublishWeaponChanged();
        }

        private int NextValidIndex()
        {
            for (int step = 1; step <= SlotCount; step++)
            {
                int candidate = WfMath.RepeatIndex((_activeIndex < 0 ? 0 : _activeIndex) + step, SlotCount);
                if (_slots[candidate] != null && _slots[candidate].IsValid) return candidate;
            }
            return _activeIndex;
        }

        public int ReserveAmmo(AmmoType type)
        {
            return _inventory != null ? _inventory.AmmoReserve(type) : 0;
        }

        /// <summary>Reloads the active weapon, drawing ammunition from the inventory pool.</summary>
        public bool TryReloadActive(float now, bool force)
        {
            WeaponSlot slot = Active;
            if (slot == null || slot.Model == null) return false;
            if (!force && !slot.Model.CanReload()) return false;

            AmmoType type = slot.Def.AmmoType;
            int reserve = ReserveAmmo(type);
            if (reserve <= 0)
            {
                if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Deny);
                GameEvents.RaiseNotice("Out of reserve ammunition for " + slot.Def.DisplayName);
                return false;
            }

            if (!slot.Model.BeginReload(now, ref reserve)) return false;
            if (_inventory != null) _inventory.SetAmmoReserve(type, reserve);
            return true;
        }

        /// <summary>Top all magazines up at mission start (ammunition still comes from the pool).</summary>
        public void RefillMagazinesFromReserve()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponSlot slot = _slots[i];
                if (slot == null || !slot.IsValid) continue;
                AmmoType type = slot.Def.AmmoType;
                int reserve = ReserveAmmo(type);
                slot.Model.TopUpFromReserve(ref reserve);
                if (_inventory != null) _inventory.SetAmmoReserve(type, reserve);
            }
        }

        private void UseEquipment()
        {
            if (_inventory == null || _context.Player == null || _context.Player.Health == null) return;
            HealthModel model = _context.Player.Health.Model;
            if (model == null) return;

            if (model.Health < model.MaxHealth * 0.995f)
            {
                if (_inventory.Remove("med_trauma_kit", 1) > 0)
                {
                    _context.Player.Health.Heal(75f);
                    PlayEquipmentUsed();
                    return;
                }
                if (_inventory.Remove("med_field_bandage", 1) > 0)
                {
                    _context.Player.Health.Heal(35f);
                    PlayEquipmentUsed();
                    return;
                }
            }

            if (model.Armor < model.MaxArmor * 0.995f && _inventory.Remove("plate_ceramic", 1) > 0)
            {
                _context.Player.Health.GrantArmor(50f);
                PlayEquipmentUsed();
                return;
            }

            if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Deny);
            GameEvents.RaiseNotice("No usable equipment");
        }

        private void PlayEquipmentUsed()
        {
            if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.UseItem);
            GameEvents.RaiseInventoryChanged(null, 0);
        }

        private void PublishWeaponChanged()
        {
            WeaponSlot slot = Active;
            if (slot == null || slot.Model == null) return;
            GameEvents.RaiseWeaponChanged(slot.Model, slot.Def.AmmoType);
        }

        public void OnMissionStart()
        {
            RefillMagazinesFromReserve();
            PublishWeaponChanged();
        }

        /// <summary>Equips a newly looted weapon into a free or secondary slot.</summary>
        public void EquipLootedWeapon(ContentDatabase content, InventoryModel inventory, string weaponId)
        {
            WeaponDef def = content.GetWeapon(weaponId);
            if (def == null) return;

            int target = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) { target = i; break; }
            }
            if (target < 0)
            {
                target = 1;
                if (_slots[target] != null && _slots[target].Def != null && _slots[target].Def.Id == weaponId) target = 2;
            }
            if (target < 0) return;

            if (_slots[target] != null && _slots[target].Runtime != null && _slots[target].Runtime.ModelObject != null)
            {
                Object.Destroy(_slots[target].Runtime.ModelObject);
                _slots[target] = null;
            }

            WeaponSlot slot = CreateSlot(content, inventory, weaponId);
            if (slot == null || !slot.IsValid) return;
            _slots[target] = slot;
            slot.Runtime.SetEquipped(false, false);
            SwitchTo(target);
            GameEvents.RaiseNotice("Equipped " + def.DisplayName);
        }
    }
}
