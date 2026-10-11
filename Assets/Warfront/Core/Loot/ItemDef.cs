using UnityEngine;

namespace Warfront.Core
{
    public enum ItemCategory
    {
        Weapon = 0,
        Ammo = 1,
        Medical = 2,
        Equipment = 3,
        Material = 4,
        Intel = 5,
        Objective = 6
    }

    /// <summary>Plain-data item definition. Mirrored by ItemAsset (ScriptableObject) for authoring.</summary>
    [System.Serializable]
    public class ItemDef
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public ItemCategory Category = ItemCategory.Material;
        public Rarity Rarity = Rarity.Common;
        public int MaxStack = 999;
        public int Value = 50;
        public int Weight = 1;

        /// <summary>Set for Ammo items - which pool this item feeds.</summary>
        public bool IsAmmo;
        public AmmoType AmmoType = AmmoType.Light;
        public int AmmoPerUnit = 30;

        /// <summary>Set for Weapon items - the WeaponDef id this item grants.</summary>
        public string WeaponId;

        /// <summary>Medical items heal this much.</summary>
        public float HealAmount;

        /// <summary>Equipment grants this much armor when used.</summary>
        public float ArmorAmount;

        public Color DisplayColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        /// <summary>Procedural icon glyph drawn by the UI (single letter) - keeps the build asset free.</summary>
        public string IconGlyph = "?";

        public bool IsStackable { get { return MaxStack > 1; } }
    }

    /// <summary>A concrete item in the world/inventory: definition + rarity + upgrade level + count.</summary>
    public struct ItemStack
    {
        public string ItemId;
        public int Quantity;
        public Rarity Rarity;
        public int UpgradeLevel;

        public ItemStack(string itemId, int quantity, Rarity rarity, int upgradeLevel)
        {
            ItemId = itemId;
            Quantity = quantity;
            Rarity = rarity;
            UpgradeLevel = upgradeLevel <= 0 ? 1 : upgradeLevel;
        }

        public static ItemStack Create(string itemId, int quantity)
        {
            return new ItemStack(itemId, quantity, Rarity.Common, 1);
        }

        public bool IsValid { get { return !string.IsNullOrEmpty(ItemId) && Quantity > 0; } }
    }

    public struct LootDrop
    {
        public string ItemId;
        public int Quantity;
        public Rarity Rarity;
        public int UpgradeLevel;
        public int LootTableId;

        public ItemStack ToStack()
        {
            return new ItemStack(ItemId, Quantity, Rarity, UpgradeLevel);
        }
    }
}
