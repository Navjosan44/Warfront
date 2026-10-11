using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Warfront.Game;

namespace Warfront.EditorTools
{
    /// <summary>
    /// One-shot project configuration for WARFRONT.
    ///
    /// A Unity project is not fully described by its C# alone: the render pipeline asset, the physics
    /// layers, the Android player settings and the boot scene all live in binary-ish YAML that has to be
    /// authored somewhere. This tool authors them from code so a fresh clone of the repository can be
    /// brought to a buildable state with a single command, which is also what CI (and the batchmode
    /// build entry point) calls.
    ///
    /// Everything is idempotent: running it twice changes nothing the second time.
    /// </summary>
    public static class WarfrontProjectSetup
    {
        public const string BootScenePath = "Assets/Scenes/Warfront.unity";
        public const string ArtFolder = "Assets/Warfront/Art";
        public const string SettingsFolder = "Assets/Warfront/Settings";
        public const string UrpAssetPath = SettingsFolder + "/WarfrontURP.asset";
        public const string RendererAssetPath = SettingsFolder + "/WarfrontRenderer.asset";

        public const string PackageName = "com.warfront.game";
        public const string ProductName = "WARFRONT";
        public const string VersionName = "1.0.0";

        /// <summary>Physics layers the gameplay masks depend on (indices are fixed in GameLayers).</summary>
        private static readonly KeyValuePair<int, string>[] RequiredLayers =
        {
            new KeyValuePair<int, string>(8, "Player"),
            new KeyValuePair<int, string>(9, "Enemy"),
            new KeyValuePair<int, string>(10, "Environment"),
            new KeyValuePair<int, string>(11, "Hitbox"),
            new KeyValuePair<int, string>(12, "Projectile"),
            new KeyValuePair<int, string>(13, "Interactable")
        };

        private static readonly string[] RequiredTags = { "Enemy", "Player", "Loot", "Objective" };

        // ------------------------------------------------------------------ menu

        [MenuItem("Warfront/Setup Project (URP, Layers, Android, Scenes)", false, 10)]
        public static void RunFromMenu()
        {
            if (ConfigureAll())
            {
                EditorUtility.DisplayDialog("WARFRONT",
                    "Project configured. Open Assets/Scenes/Warfront.unity and press Play.",
                    "OK");
            }
        }

        [MenuItem("Warfront/Create or Repair Boot Scene", false, 20)]
        public static void CreateBootSceneFromMenu()
        {
            CreateBootScene(true);
            EditorUtility.DisplayDialog("WARFRONT", "Boot scene written to " + BootScenePath, "OK");
        }

        [MenuItem("Warfront/Report Configuration", false, 40)]
        public static void Report()
        {
            Debug.Log("[Warfront] Configuration report\n" + DescribeConfiguration());
        }

        // ------------------------------------------------------------ entry point

        /// <summary>Applies every configuration step. Safe to call from batchmode.</summary>
        public static bool ConfigureAll()
        {
            bool ok = true;
            try
            {
                EnsureFolders();
                ConfigureLayersAndTags();
                ConfigureRenderPipeline();
                ConfigurePlayerSettings();
                ConfigureQualityLevels();
                CreateIconArt();
                CreateBootScene(false);
            }
            catch (Exception ex)
            {
                ok = false;
                Debug.LogError("[Warfront] Project setup failed: " + ex);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Warfront] Configuration complete\n" + DescribeConfiguration());
            return ok;
        }

        public static string DescribeConfiguration()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(1024);
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            sb.Append("  render pipeline     : ").Append(pipeline != null ? pipeline.GetType().Name + " (" + pipeline.name + ")" : "Built-in").Append('\n');
            sb.Append("  colour space        : ").Append(PlayerSettings.colorSpace).Append('\n');
            sb.Append("  active input        : ").Append(DescribeInputBackend()).Append('\n');
            sb.Append("  scripting backend   : ").Append(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)).Append('\n');
            sb.Append("  android arch        : ").Append(PlayerSettings.Android.targetArchitectures).Append('\n');
            sb.Append("  android sdk         : min ").Append((int)PlayerSettings.Android.minSdkVersion)
              .Append(", target ").Append((int)PlayerSettings.Android.targetSdkVersion).Append('\n');
            sb.Append("  package             : ").Append(PlayerSettings.applicationIdentifier).Append('\n');
            sb.Append("  orientation         : ").Append(PlayerSettings.defaultInterfaceOrientation).Append('\n');
            sb.Append("  boot scene          : ").Append(BootScenePath).Append(File.Exists(BootScenePath) ? " (present)" : " (MISSING)").Append('\n');
            sb.Append("  scenes in build     : ").Append(EditorBuildSettings.scenes.Length).Append('\n');
            sb.Append("  urp asset           : ").Append(AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath) != null ? "present" : "MISSING").Append('\n');
            return sb.ToString();
        }

        private static string DescribeInputBackend()
        {
            // The define set is baked at compile time, so mirrored defines are the honest answer here.
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            return "Both (Input System + legacy)";
#elif ENABLE_INPUT_SYSTEM
            return "Input System package";
#elif ENABLE_LEGACY_INPUT_MANAGER
            return "Legacy Input Manager";
#else
            return "none";
#endif
        }

        // ------------------------------------------------------------------ folders

        public static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/Warfront", "Art");
            EnsureFolder("Assets/Warfront", "Settings");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(parent, child);
        }

        // ------------------------------------------------------------ layers/tags

        public static void ConfigureLayersAndTags()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < RequiredLayers.Length; i++)
            {
                int index = RequiredLayers[i].Key;
                string name = RequiredLayers[i].Value;
                if (index < 0 || index >= layers.arraySize) continue;
                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                if (slot.stringValue == name) continue;
                slot.stringValue = name;
                Debug.Log("[Warfront] Layer " + index + " -> " + name);
            }

            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < RequiredTags.Length; i++)
            {
                string tag = RequiredTags[i];
                bool present = false;
                for (int t = 0; t < tags.arraySize; t++)
                {
                    if (tags.GetArrayElementAtIndex(t).stringValue != tag) continue;
                    present = true;
                    break;
                }
                if (present) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                Debug.Log("[Warfront] Tag added: " + tag);
            }

            tagManager.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------- render pipeline

        public static void ConfigureRenderPipeline()
        {
            // Linear colour space: URP looks wrong in gamma and lighting on mobile assumes linear.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }

            RenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (existing == null)
            {
                existing = CreateUrpAsset();
            }
            if (existing == null)
            {
                Debug.LogWarning("[Warfront] URP asset could not be created - the project will use the Built-in " +
                                 "pipeline, whose shaders GameAssets resolves automatically.");
                return;
            }

            if (GraphicsSettings.defaultRenderPipeline != existing)
            {
                GraphicsSettings.defaultRenderPipeline = existing;
            }
            QualitySettings.renderPipeline = existing;

            // Mobile-first renderer settings: no HDR, 2x MSAA at most, shadows that fit a phone budget.
            ConfigureUrpAssetDefaults(existing);
        }

        private static RenderPipelineAsset CreateUrpAsset()
        {
            try
            {
                Type rendererDataType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
                Type pipelineType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                if (rendererDataType == null || pipelineType == null)
                {
                    Debug.LogWarning("[Warfront] URP package types not found; is com.unity.render-pipelines.universal installed?");
                    return null;
                }

                ScriptableObject rendererData = ScriptableObject.CreateInstance(rendererDataType);
                if (rendererData == null) return null;
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);

                System.Reflection.MethodInfo create = pipelineType.GetMethod("Create",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { rendererDataType }, null);
                if (create == null)
                {
                    Debug.LogWarning("[Warfront] UniversalRenderPipelineAsset.Create(rendererData) not found.");
                    return null;
                }

                RenderPipelineAsset asset = create.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;
                if (asset == null) return null;

                AssetDatabase.CreateAsset(asset, UrpAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Warfront] Created URP assets at " + UrpAssetPath);
                return asset;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Warfront] URP asset creation failed: " + ex.Message);
                return null;
            }
        }

        private static Type FindType(string fullName)
        {
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static void ConfigureUrpAssetDefaults(RenderPipelineAsset asset)
        {
            try
            {
                Type type = asset.GetType();
                SetProperty(type, asset, "msaaSampleCount", 2);
                SetProperty(type, asset, "supportsHDR", false);
                SetProperty(type, asset, "renderScale", 1f);
                SetProperty(type, asset, "shadowDistance", 45f);
                SetProperty(type, asset, "shadowCascadeCount", 2);
                SetProperty(type, asset, "supportsCameraDepthTexture", false);
                SetProperty(type, asset, "supportsCameraOpaqueTexture", false);
                SetProperty(type, asset, "shadowCascade2Split", 0.33f);
                EditorUtility.SetDirty(asset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Warfront] URP defaults partially applied: " + ex.Message);
            }
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            System.Reflection.PropertyInfo property = type.GetProperty(name);
            if (property == null || !property.CanWrite) return;
            if (value is int && property.PropertyType == typeof(int)) property.SetValue(target, value);
            else if (value is float && property.PropertyType == typeof(float)) property.SetValue(target, (float)value);
            else if (value is bool && property.PropertyType == typeof(bool)) property.SetValue(target, value);
        }

        // -------------------------------------------------------- player settings

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Warfront Studio";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.applicationIdentifier = PackageName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.gpuSkinning = false;      // simple rigs: CPU skinning is cheaper than the extra state
            PlayerSettings.MTRendering = true;
            PlayerSettings.useFlipModelSwapchain = true;

            // Landscape only: the third person camera and the touch layout both assume a wide viewport.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SplashScreen.show = false;

            // ---- Android
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.forceInternetPermission = false;   // the core game is offline
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.blitType = AndroidBlitType.Never;
            PlayerSettings.Android.androidIsGame = true;

            // Vulkan first with a GLES3 fallback covers modern devices and driver-buggy ones alike.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            }
            // IL2CPP needs a managed code generation path that plays well with generics-heavy code.
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        }

        public static void ConfigureQualityLevels()
        {
            string[] names = { "Low", "Medium", "High", "Ultra" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                if (index < QualitySettings.names.Length && QualitySettings.names[index] != names[index])
                {
                    // QualitySettings.SetQualityLevel cannot rename levels; this is done by index only.
                }
            }

            // Bind the URP asset to every quality level so switching preset never loses the pipeline.
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (pipeline == null) return;

            int previous = QualitySettings.GetQualityLevel();
            int count = Mathf.Max(1, QualitySettings.names.Length);
            for (int level = 0; level < count; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
                QualitySettings.skinWeights = SkinWeights.OneBone;
                bool low = level == 0;
                bool ultra = level >= count - 1;
                QualitySettings.shadows = low ? ShadowQuality.Disable : ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = low ? 20f : ultra ? 55f : 38f;
                QualitySettings.antiAliasing = ultra ? 4 : level >= 2 ? 2 : 0;
                QualitySettings.particleRaycastBudget = low ? 64 : 256;
                QualitySettings.lodBias = low ? 0.7f : ultra ? 1.6f : 1.1f;
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(previous, 0, count - 1), true);
            Debug.Log("[Warfront] Quality levels: " + string.Join(", ", QualitySettings.names));
        }

        // ------------------------------------------------------------------- icon

        /// <summary>Generates the app icon as original art so the project ships no third party imagery.</summary>
        public static void CreateIconArt()
        {
            EnsureFolders();

            WriteIcon("warfront_icon_512.png", 512, false);
            WriteIcon("warfront_adaptive_foreground_432.png", 432, true);
            WriteIcon("warfront_adaptive_background_432.png", 432, false);

            AssetDatabase.Refresh();
            ConfigureIconImporter("warfront_icon_512.png");
            ConfigureIconImporter("warfront_adaptive_foreground_432.png");
            ConfigureIconImporter("warfront_adaptive_background_432.png");
            ApplyAndroidIcons();
        }

        /// <summary>
        /// Paints the WARFRONT mark: a dark plate, a horizon slash and a rising sun block.
        /// Kept procedural because the project deliberately ships zero licensed art.
        /// </summary>
        private static void WriteIcon(string fileName, int size, bool transparent)
        {
            string path = ArtFolder + "/" + fileName;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color backgroundTop = new Color(0.09f, 0.10f, 0.10f, transparent ? 0f : 1f);
            Color backgroundBottom = new Color(0.14f, 0.15f, 0.13f, transparent ? 0f : 1f);
            Color ground = new Color(0.20f, 0.22f, 0.19f, 1f);
            Color horizon = new Color(0.72f, 0.80f, 0.42f, 1f);
            Color sun = new Color(0.86f, 0.62f, 0.24f, 1f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    Color color = Color.Lerp(backgroundTop, backgroundBottom, v);
                    if (transparent) color.a = 0f;

                    // Ground band across the lower third.
                    if (v < 0.34f) color = ground;

                    // Sun disc, upper left of centre.
                    float dx = u - 0.42f;
                    float dy = v - 0.62f;
                    if (dx * dx + dy * dy < 0.055f) color = sun;

                    // Horizon slash: a thick diagonal band across the middle.
                    float slash = v - (0.34f + (u - 0.5f) * 0.22f);
                    if (slash > -0.035f && slash < 0.035f) color = horizon;

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("[Warfront] Icon art written: " + path);
        }

        private static void ConfigureIconImporter(string fileName)
        {
            string path = ArtFolder + "/" + fileName;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Assigns the Android icon set.
        ///
        /// IconKind gained and renamed members across Unity versions, so the slots are discovered by
        /// name at runtime rather than hard-coded: whatever adaptive slots this editor exposes get the
        /// matching texture, and anything it does not expose is skipped instead of failing the build.
        /// </summary>
        private static void ApplyAndroidIcons()
        {
            Texture2D main = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/warfront_icon_512.png");
            Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/warfront_adaptive_foreground_432.png");
            Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/warfront_adaptive_background_432.png");
            if (main == null)
            {
                Debug.LogWarning("[Warfront] Icon source missing; the default Unity icon will be used.");
                return;
            }

            int assigned = 0;
            Array kinds = Enum.GetValues(typeof(IconKind));
            foreach (IconKind kind in kinds)
            {
                string name = kind.ToString();

                if (string.Equals(name, "Application", StringComparison.Ordinal))
                {
                    if (AssignIcon(main, kind, name)) assigned++;
                    continue;
                }

                if (name.IndexOf("Adaptive", StringComparison.OrdinalIgnoreCase) < 0) continue;

                Texture2D source = name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ? background : foreground;
                if (source == null) continue;
                if (AssignIcon(source, kind, name)) assigned++;
            }

            Debug.Log("[Warfront] Android icon slots assigned: " + assigned);
        }

        private static bool AssignIcon(Texture2D texture, IconKind kind, string kindName)
        {
            try
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { texture }, kind);
                return true;
            }
            catch (Exception ex)
            {
                // Some slots only accept exact sizes or a specific texture format; skip rather than fail.
                Debug.LogWarning("[Warfront] Icon slot '" + kindName + "' skipped: " + ex.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ scene

        /// <summary>
        /// Creates the single boot scene. WARFRONT builds its level, UI and player procedurally at
        /// runtime from GameBootstrap, so the scene intentionally contains one GameObject and nothing else.
        ///
        /// When the scene already exists it is left untouched (pass force to rebuild it) so a normal
        /// Configure run never discards a scene a developer has open with unsaved work.
        /// </summary>
        public static void CreateBootScene(bool force = false)
        {
            EnsureFolders();

            if (File.Exists(BootScenePath) && !force)
            {
                RegisterBootScene();
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrap = new GameObject("Warfront");
            bootstrap.AddComponent<GameBootstrap>();

            bool saved = EditorSceneManager.SaveScene(scene, BootScenePath);
            if (!saved)
            {
                Debug.LogError("[Warfront] Boot scene could not be saved to " + BootScenePath);
                return;
            }

            RegisterBootScene();
            Debug.Log("[Warfront] Boot scene created: " + BootScenePath);
        }

        /// <summary>Makes sure the boot scene is the enabled scene 0 in the build settings.</summary>
        private static void RegisterBootScene()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path != BootScenePath) continue;
                if (existing[i].enabled) return;
                existing[i].enabled = true;
                EditorBuildSettings.scenes = existing;
                return;
            }

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(existing.Length + 1);
            scenes.Add(new EditorBuildSettingsScene(BootScenePath, true));
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == BootScenePath) continue;
                scenes.Add(existing[i]);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
