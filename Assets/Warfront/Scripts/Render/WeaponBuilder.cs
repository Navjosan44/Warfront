using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Builds weapon models from primitives, giving each class a distinct silhouette (pistol, SMG,
    /// rifle, shotgun, marksman, sniper, LMG) that matches its stats. Original geometry, no imports.
    /// </summary>
    public static class WeaponBuilder
    {
        public const string MuzzleName = "Muzzle";

        public static GameObject Build(WeaponDef def, Transform parent, GameAssets assets, int layer)
        {
            GameObject root = new GameObject("Weapon_" + (def != null ? def.Id : "unknown"));
            root.layer = layer;
            root.transform.SetParent(parent, false);

            Material metal = assets != null ? assets.WeaponMetal : null;
            Material grip = assets != null ? assets.WeaponGrip : null;
            WeaponClass weaponClass = def != null ? def.Class : WeaponClass.AssaultRifle;

            float length;
            float barrel;
            float bodyHeight;
            switch (weaponClass)
            {
                case WeaponClass.Pistol:
                    length = 0.22f; barrel = 0.1f; bodyHeight = 0.07f;
                    Part(root.transform, "Slide", new Vector3(0f, 0.03f, 0.06f), new Vector3(0.05f, 0.06f, 0.22f), metal, layer);
                    Part(root.transform, "Grip", new Vector3(0f, -0.07f, -0.02f), new Vector3(0.045f, 0.13f, 0.06f), grip, layer);
                    Part(root.transform, "Sight", new Vector3(0f, 0.08f, 0.04f), new Vector3(0.02f, 0.02f, 0.02f), metal, layer);
                    break;
                case WeaponClass.SMG:
                    length = 0.42f; barrel = 0.12f; bodyHeight = 0.09f;
                    Part(root.transform, "Body", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.07f, 0.1f, 0.34f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.03f, 0.28f), new Vector3(0.04f, 0.04f, barrel), metal, layer);
                    Part(root.transform, "Magazine", new Vector3(0f, -0.1f, 0.04f), new Vector3(0.05f, 0.17f, 0.07f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, 0.02f, -0.2f), new Vector3(0.05f, 0.07f, 0.12f), grip, layer);
                    break;
                case WeaponClass.AssaultRifle:
                    length = 0.62f; barrel = 0.2f; bodyHeight = 0.1f;
                    Part(root.transform, "Body", new Vector3(0f, 0.02f, 0.04f), new Vector3(0.075f, 0.11f, 0.44f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.03f, 0.38f), new Vector3(0.045f, 0.045f, barrel), metal, layer);
                    Part(root.transform, "Handguard", new Vector3(0f, 0.02f, 0.28f), new Vector3(0.07f, 0.07f, 0.18f), grip, layer);
                    Part(root.transform, "Magazine", new Vector3(0f, -0.11f, 0.06f), new Vector3(0.055f, 0.2f, 0.09f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, 0.02f, -0.26f), new Vector3(0.06f, 0.09f, 0.16f), grip, layer);
                    Part(root.transform, "Optic", new Vector3(0f, 0.11f, 0.06f), new Vector3(0.05f, 0.05f, 0.12f), metal, layer);
                    break;
                case WeaponClass.Shotgun:
                    length = 0.66f; barrel = 0.3f; bodyHeight = 0.1f;
                    Part(root.transform, "Receiver", new Vector3(0f, 0.02f, -0.02f), new Vector3(0.08f, 0.11f, 0.3f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.05f, 0.34f), new Vector3(0.07f, 0.07f, barrel), metal, layer);
                    Part(root.transform, "Pump", new Vector3(0f, 0.0f, 0.24f), new Vector3(0.075f, 0.07f, 0.14f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, -0.01f, -0.26f), new Vector3(0.06f, 0.11f, 0.2f), grip, layer);
                    break;
                case WeaponClass.MarksmanRifle:
                    length = 0.8f; barrel = 0.34f; bodyHeight = 0.1f;
                    Part(root.transform, "Body", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.07f, 0.1f, 0.5f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.03f, 0.5f), new Vector3(0.04f, 0.04f, barrel), metal, layer);
                    Part(root.transform, "Scope", new Vector3(0f, 0.13f, 0.1f), new Vector3(0.055f, 0.06f, 0.24f), metal, layer);
                    Part(root.transform, "Magazine", new Vector3(0f, -0.1f, 0.06f), new Vector3(0.05f, 0.16f, 0.08f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, 0.0f, -0.3f), new Vector3(0.06f, 0.11f, 0.22f), grip, layer);
                    break;
                case WeaponClass.SniperRifle:
                    length = 1.05f; barrel = 0.5f; bodyHeight = 0.1f;
                    Part(root.transform, "Body", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.07f, 0.1f, 0.52f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.03f, 0.62f), new Vector3(0.04f, 0.04f, barrel), metal, layer);
                    Part(root.transform, "Scope", new Vector3(0f, 0.14f, 0.08f), new Vector3(0.06f, 0.07f, 0.32f), metal, layer);
                    Part(root.transform, "Magazine", new Vector3(0f, -0.09f, 0.08f), new Vector3(0.05f, 0.13f, 0.08f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, 0.0f, -0.32f), new Vector3(0.06f, 0.12f, 0.24f), grip, layer);
                    Part(root.transform, "Bipod", new Vector3(0f, -0.08f, 0.5f), new Vector3(0.02f, 0.12f, 0.02f), metal, layer);
                    break;
                default:
                    length = 0.7f; barrel = 0.3f; bodyHeight = 0.13f;
                    Part(root.transform, "Body", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.09f, 0.14f, 0.48f), metal, layer);
                    Part(root.transform, "Barrel", new Vector3(0f, 0.03f, 0.42f), new Vector3(0.05f, 0.05f, barrel), metal, layer);
                    Part(root.transform, "Drum", new Vector3(0f, -0.12f, 0.04f), new Vector3(0.16f, 0.16f, 0.12f), grip, layer);
                    Part(root.transform, "Stock", new Vector3(0f, 0.0f, -0.28f), new Vector3(0.07f, 0.12f, 0.2f), grip, layer);
                    break;
            }

            if (def != null && assets != null)
            {
                // One accent plate per platform carries the weapon's identity colour.
                Material tinted = assets.Lit("WeaponAccent_" + def.Id, def.DisplayColor, 0.35f, 0.4f);
                GameObject accent = PrimitiveFactory.CreateBox("Accent", root.transform,
                    new Vector3(0f, -0.02f, 0.1f), new Vector3(0.082f, 0.05f, 0.1f), tinted, false, Quaternion.identity, layer);
                if (accent == null) Debug.LogWarning("[Warfront] Weapon accent plate could not be created.");
            }

            // Barrels scale with the platform so the silhouette matches the range stat.
            bodyHeight = Mathf.Max(bodyHeight, 0.06f);

            GameObject muzzle = new GameObject(MuzzleName);
            muzzle.layer = layer;
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.05f, length);

            return root;
        }

        private static GameObject Part(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, int layer)
        {
            return PrimitiveFactory.CreateBox(name, parent, localPosition, size, material, false, Quaternion.identity, layer);
        }

        public static Transform FindMuzzle(Transform root)
        {
            if (root == null) return null;
            if (root.name == MuzzleName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindMuzzle(root.GetChild(i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
