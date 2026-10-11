using UnityEngine;

namespace Warfront.Core
{
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum WeaponClass
    {
        Pistol = 0,
        SMG = 1,
        AssaultRifle = 2,
        Shotgun = 3,
        MarksmanRifle = 4,
        SniperRifle = 5,
        LightMachineGun = 6
    }

    public enum FireMode
    {
        Single = 0,
        Auto = 1,
        Burst = 2
    }

    public enum AmmoType
    {
        Light = 0,
        Heavy = 1,
        Shell = 2,
        Sniper = 3
    }

    /// <summary>
    /// Plain-data weapon definition. The Unity layer mirrors this in a ScriptableObject
    /// (WeaponAsset) for artists/designers, and both funnel into this single definition type
    /// so no weapon logic is ever duplicated.
    /// </summary>
    [System.Serializable]
    public class WeaponDef
    {
        public string Id;
        public string DisplayName;
        public WeaponClass Class;
        public FireMode FireMode = FireMode.Auto;
        public AmmoType AmmoType = AmmoType.Light;

        public float Damage = 20f;
        public float RoundsPerMinute = 600f;
        public int MagazineSize = 30;
        public float ReloadTime = 2.1f;
        public float Range = 120f;
        public float FalloffStart = 40f;
        public float FalloffEnd = 90f;
        public float MinFalloffMultiplier = 0.55f;

        public float BaseSpread = 0.6f;
        public float MoveSpreadPenalty = 1.6f;
        public float AimSpreadMultiplier = 0.35f;
        public float SpreadPerShot = 0.35f;
        public float MaxSpread = 5f;
        public float SpreadRecovery = 6f;

        public float RecoilVertical = 0.6f;
        public float RecoilHorizontal = 0.25f;
        public float RecoilRecovery = 8f;

        public float HeadshotMultiplier = 2f;
        public float CritChance = 0.05f;
        public float CritMultiplier = 1.5f;
        public float ArmorPenetration = 0.1f;

        public int PelletsPerShot = 1;
        public float AdsSpeed = 8f;
        public float AdsFov = 45f;
        public float EquipTime = 0.45f;
        public float ReloadAnimationScale = 1f;
        public int BurstCount = 3;
        public float BurstInterval = 0.08f;

        public int UnlockLevel = 1;
        public int UnlockCost = 0;
        public int MaxUpgradeLevel = 5;

        /// <summary>Relative weight when rolling this weapon from loot tables.</summary>
        public int LootWeight = 100;
        public Rarity MinRarity = Rarity.Common;

        public Color DisplayColor = new Color(0.32f, 0.34f, 0.36f, 1f);

        public float SecondsBetweenShots
        {
            get { return RoundsPerMinute <= 0f ? 0.1f : 60f / RoundsPerMinute; }
        }

        public WeaponDef Clone()
        {
            return (WeaponDef)MemberwiseClone();
        }
    }

    /// <summary>Per-rarity scaling applied on top of a weapon definition.</summary>
    public static class RarityRules
    {
        public static float DamageMultiplier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return 1.06f;
                case Rarity.Rare: return 1.13f;
                case Rarity.Epic: return 1.22f;
                case Rarity.Legendary: return 1.34f;
                default: return 1f;
            }
        }

        public static float HandlingMultiplier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return 1.05f;
                case Rarity.Rare: return 1.12f;
                case Rarity.Epic: return 1.2f;
                case Rarity.Legendary: return 1.3f;
                default: return 1f;
            }
        }

        public static string Name(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return "Uncommon";
                case Rarity.Rare: return "Rare";
                case Rarity.Epic: return "Epic";
                case Rarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        public static Color Color(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.4f, 1f);
                case Rarity.Rare: return new Color(0.35f, 0.6f, 0.98f, 1f);
                case Rarity.Epic: return new Color(0.72f, 0.42f, 0.95f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.72f, 0.25f, 1f);
                default: return new Color(0.82f, 0.84f, 0.86f, 1f);
            }
        }
    }

    /// <summary>Effective stats after rarity and upgrade level are applied.</summary>
    public struct WeaponStats
    {
        public float Damage;
        public float RoundsPerMinute;
        public int MagazineSize;
        public float ReloadTime;
        public float Range;
        public float FalloffStart;
        public float FalloffEnd;
        public float MinFalloffMultiplier;
        public float BaseSpread;
        public float RecoilVertical;
        public float RecoilHorizontal;
        public float HeadshotMultiplier;
        public float CritChance;
        public float CritMultiplier;
        public float ArmorPenetration;
        public int PelletsPerShot;
        public float EquipTime;
        public float AdsFov;

        public float SecondsBetweenShots
        {
            get { return RoundsPerMinute <= 0f ? 0.1f : 60f / RoundsPerMinute; }
        }
    }

    public static class WeaponStatRules
    {
        public const int DamageUpgradePerLevel = 7;     // percent
        public const int MagazineUpgradePerLevel = 5;   // percent
        public const int ReloadUpgradePerLevel = 4;     // percent faster
        public const int RecoilUpgradePerLevel = 5;     // percent less

        public static WeaponStats Build(WeaponDef def, Rarity rarity, int upgradeLevel)
        {
            int level = WfMath.ClampInt(upgradeLevel, 1, Mathf.Max(1, def.MaxUpgradeLevel));
            float steps = level - 1;
            float rarityDamage = RarityRules.DamageMultiplier(rarity);
            float handling = RarityRules.HandlingMultiplier(rarity);

            WeaponStats stats;
            stats.Damage = def.Damage * rarityDamage * (1f + steps * DamageUpgradePerLevel / 100f);
            stats.RoundsPerMinute = def.RoundsPerMinute;
            stats.MagazineSize = Mathf.RoundToInt(def.MagazineSize * (1f + steps * MagazineUpgradePerLevel / 100f));
            stats.ReloadTime = Mathf.Max(0.4f, def.ReloadTime * (1f - steps * ReloadUpgradePerLevel / 100f) / handling);
            stats.Range = def.Range;
            stats.FalloffStart = def.FalloffStart;
            stats.FalloffEnd = Mathf.Max(def.FalloffStart + 1f, def.FalloffEnd);
            stats.MinFalloffMultiplier = def.MinFalloffMultiplier;
            stats.BaseSpread = def.BaseSpread / handling;
            stats.RecoilVertical = def.RecoilVertical * (1f - steps * RecoilUpgradePerLevel / 100f);
            stats.RecoilHorizontal = def.RecoilHorizontal * (1f - steps * RecoilUpgradePerLevel / 100f);
            stats.HeadshotMultiplier = def.HeadshotMultiplier;
            stats.CritChance = def.CritChance;
            stats.CritMultiplier = def.CritMultiplier;
            stats.ArmorPenetration = def.ArmorPenetration;
            stats.PelletsPerShot = Mathf.Max(1, def.PelletsPerShot);
            stats.EquipTime = def.EquipTime;
            stats.AdsFov = def.AdsFov;
            return stats;
        }

        /// <summary>Credit + material cost to go from upgradeLevel to upgradeLevel+1.</summary>
        public static void UpgradeCost(WeaponDef def, int upgradeLevel, out int credits, out int materials)
        {
            int level = WfMath.ClampInt(upgradeLevel, 1, def.MaxUpgradeLevel);
            credits = 400 * level * level + 200 * level;
            materials = 2 + level * 2;
        }
    }
}
