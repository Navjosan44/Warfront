using UnityEngine;

namespace Warfront.Game
{
    /// <summary>Layer assignment used by every physics query. Indices are fixed, names are cosmetic.</summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Ui = 5;
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Environment = 10;
        public const int Hitbox = 11;
        public const int Projectile = 12;
        public const int Interactable = 13;

        public static readonly int PlayerMask = 1 << Player;
        public static readonly int EnemyMask = 1 << Enemy;
        public static readonly int EnvironmentMask = 1 << Environment;
        public static readonly int HitboxMask = 1 << Hitbox;
        public static readonly int InteractMask = 1 << Interactable;

        /// <summary>Anything a bullet can stop on or damage.</summary>
        public static readonly int ShootableMask = (1 << Player) | (1 << Enemy) | (1 << Environment) | (1 << Hitbox);

        /// <summary>Solid geometry only - used for line of sight and ground probes.</summary>
        public static readonly int BlockingMask = 1 << Environment;

        public static readonly int GroundMask = (1 << Environment);
    }

    /// <summary>
    /// Builds level props, characters and weapons from primitives and procedural meshes.
    /// Box colliders are used for generated geometry: cheap for mobile physics and predictable for
    /// navigation and cover checks.
    /// </summary>
    public static class PrimitiveFactory
    {
        public static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider)
        {
            return CreateBox(name, parent, localPosition, size, material, collider, Quaternion.identity, GameLayers.Environment);
        }

        public static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider, Quaternion rotation, int layer)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = rotation;
            t.localScale = size;

            Collider existing = go.GetComponent<Collider>();
            if (!collider && existing != null) Object.Destroy(existing);
            if (collider && existing is BoxCollider)
            {
                ((BoxCollider)existing).isTrigger = false;
            }

            ApplyMaterial(go, material);
            return go;
        }

        public static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, float radius, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.layer = GameLayers.Environment;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = Vector3.one * (radius * 2f);

            Collider existing = go.GetComponent<Collider>();
            if (!collider && existing != null) Object.Destroy(existing);

            ApplyMaterial(go, material);
            return go;
        }

        public static GameObject CreateCapsule(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.layer = GameLayers.Environment;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            Collider existing = go.GetComponent<Collider>();
            if (!collider && existing != null) Object.Destroy(existing);

            ApplyMaterial(go, material);
            return go;
        }

        /// <summary>Creates an object from a procedural mesh. Colliders are fitted primitives.</summary>
        public static GameObject CreateMesh(string name, Transform parent, Mesh mesh, Vector3 localPosition, Vector3 scale, Material material, ColliderKind colliderKind, int layer)
        {
            GameObject go = new GameObject(name);
            go.layer = layer;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = scale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            switch (colliderKind)
            {
                case ColliderKind.Box:
                {
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.5f, 0f);
                    box.size = Vector3.one;
                    break;
                }
                case ColliderKind.Sphere:
                {
                    SphereCollider sphere = go.AddComponent<SphereCollider>();
                    sphere.center = new Vector3(0f, 0.35f, 0f);
                    sphere.radius = 0.5f;
                    break;
                }
                case ColliderKind.None:
                default:
                    break;
            }

            return go;
        }

        public static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null) return;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
            }
        }

        public static void SetShadowsRecursive(GameObject go, bool castShadows)
        {
            if (go == null) return;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetShadowsRecursive(t.GetChild(i).gameObject, castShadows);
            }
        }
    }

    public enum ColliderKind
    {
        None = 0,
        Box = 1,
        Sphere = 2
    }
}
