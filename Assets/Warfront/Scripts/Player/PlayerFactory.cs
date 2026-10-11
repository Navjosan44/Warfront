using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>References to everything the player is made of.</summary>
    public struct PlayerBuild
    {
        public GameObject Root;
        public PlayerController Controller;
        public PlayerHealth Health;
        public PlayerInteractor Interactor;
        public PlayerLoadout Loadout;
        public CharacterRig Rig;
        public Damageable Damageable;
        public PlayerCamera Camera;
        public Camera UnityCamera;
    }

    /// <summary>
    /// Constructs the player once. The same rig is reused between missions (only the loadout and
    /// position change), which keeps mission restarts instant and allocation free.
    /// </summary>
    public static class PlayerFactory
    {
        public const float RigHeight = 1.78f;

        public static PlayerBuild Build(GameContext context, Transform parent)
        {
            PlayerBuild build = default(PlayerBuild);

            GameObject root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            root.layer = GameLayers.Player;

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = RigHeight;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, RigHeight * 0.5f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;

            Damageable damageable = root.AddComponent<Damageable>();
            PlayerController playerController = root.AddComponent<PlayerController>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            PlayerLoadout loadout = root.AddComponent<PlayerLoadout>();

            CharacterRig rig = new CharacterRig();
            GameAssets assets = context.Assets;
            Material bodyMaterial = assets != null ? assets.PlayerBody : null;
            Material accentMaterial = assets != null ? assets.PlayerAccent : null;
            rig.Build(root.transform, bodyMaterial, accentMaterial, 1f, true, damageable, true);

            // Camera: the bootstrap owns exactly one camera; the player just drives it.
            Camera unityCamera = context.MenuCamera;
            GameObject cameraObject;
            if (unityCamera == null)
            {
                cameraObject = new GameObject("WarfrontCamera");
                cameraObject.transform.SetParent(parent, false);
                unityCamera = cameraObject.AddComponent<Camera>();
                unityCamera.fieldOfView = PlayerCamera.HipFov;
                unityCamera.nearClipPlane = 0.08f;
                unityCamera.farClipPlane = 700f;
                cameraObject.AddComponent<AudioListener>();
                context.MenuCamera = unityCamera;
            }
            else
            {
                cameraObject = unityCamera.gameObject;
            }

            PlayerCamera playerCamera = cameraObject.GetComponent<PlayerCamera>();
            if (playerCamera == null) playerCamera = cameraObject.AddComponent<PlayerCamera>();
            context.Camera = playerCamera;

            playerController.Initialize(context, controller, rig, damageable, loadout);
            health.Initialize(context, damageable, playerController);
            interactor.Initialize(context, playerController);
            loadout.Initialize(context, context.Inventory, rig);

            build.Root = root;
            build.Controller = playerController;
            build.Health = health;
            build.Interactor = interactor;
            build.Loadout = loadout;
            build.Rig = rig;
            build.Damageable = damageable;
            build.Camera = playerCamera;
            build.UnityCamera = unityCamera;
            return build;
        }

        /// <summary>Attaches the camera to a fresh target and places the player at a spawn point.</summary>
        public static void PlaceAt(GameContext context, PlayerBuild build, Vector3 spawnPosition, bool snapCamera)
        {
            if (build.Controller == null) return;
            build.Controller.Teleport(spawnPosition + Vector3.up * 0.1f);
            build.Controller.SetInputEnabled(true);

            if (build.Camera != null)
            {
                build.Camera.Initialize(context, build.UnityCamera, build.Root.transform, build.Controller, 0f);
                if (snapCamera) build.Camera.SnapToTarget();
            }
        }
    }
}
