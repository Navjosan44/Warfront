using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Designer facing ScriptableObject wrappers.
    ///
    /// The runtime database is defined in code (so a fresh clone always boots), but any asset dropped
    /// into Assets/Warfront/Resources/Content/ replaces the matching code entry. That gives designers an
    /// inspector workflow without creating a second source of truth: both paths funnel into the same
    /// Core definition types, so no gameplay rule is ever duplicated.
    /// </summary>
    public class WeaponAsset : ScriptableObject
    {
        public WeaponDef Definition = new WeaponDef();
    }

    public class ItemAsset : ScriptableObject
    {
        public ItemDef Definition = new ItemDef();
    }

    public class EnemyAsset : ScriptableObject
    {
        public EnemyDef Definition = new EnemyDef();
    }

    public class MissionAsset : ScriptableObject
    {
        public MissionDef Definition = new MissionDef();
    }

    public class LootTableAsset : ScriptableObject
    {
        public LootTableDef Definition = new LootTableDef();
    }

    public static class ContentAssets
    {
        public const string ContentPath = "Content";

        /// <summary>Applies every authored asset over the code defaults and reports how many were loaded.</summary>
        public static int ApplyOverrides(ContentDatabase database)
        {
            if (database == null) return 0;
            int applied = 0;

            WeaponAsset[] weapons = Resources.LoadAll<WeaponAsset>(ContentPath);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null || weapons[i].Definition == null) continue;
                if (string.IsNullOrEmpty(weapons[i].Definition.Id)) continue;
                database.Register(weapons[i].Definition);
                applied++;
            }

            ItemAsset[] items = Resources.LoadAll<ItemAsset>(ContentPath);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null || items[i].Definition == null) continue;
                if (string.IsNullOrEmpty(items[i].Definition.Id)) continue;
                database.Register(items[i].Definition);
                applied++;
            }

            LootTableAsset[] tables = Resources.LoadAll<LootTableAsset>(ContentPath);
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] == null || tables[i].Definition == null) continue;
                if (string.IsNullOrEmpty(tables[i].Definition.Id)) continue;
                database.Register(tables[i].Definition);
                applied++;
            }

            MissionAsset[] missions = Resources.LoadAll<MissionAsset>(ContentPath);
            for (int i = 0; i < missions.Length; i++)
            {
                if (missions[i] == null || missions[i].Definition == null) continue;
                if (string.IsNullOrEmpty(missions[i].Definition.Id)) continue;
                database.Register(missions[i].Definition);
                applied++;
            }

            if (applied > 0) Debug.Log("[Warfront] Applied " + applied + " authored content asset(s) over the code defaults.");
            return applied;
        }

        /// <summary>Every weapon asset currently in Resources (used by the generator to avoid duplicates).</summary>
        public static List<string> ExistingWeaponIds()
        {
            var ids = new List<string>(16);
            WeaponAsset[] weapons = Resources.LoadAll<WeaponAsset>(ContentPath);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null && weapons[i].Definition != null) ids.Add(weapons[i].Definition.Id);
            }
            return ids;
        }
    }
}
