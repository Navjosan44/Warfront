using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Warfront.EditorTools
{
    /// <summary>
    /// Build and verification entry points.
    ///
    /// These are the methods the command line calls (-executeMethod) so the whole pipeline - configure,
    /// verify, build - is reproducible without a human clicking through menus. Each entry point sets a
    /// non-zero exit code on failure so a CI job cannot mistake a failed build for a successful one.
    /// </summary>
    public static class WarfrontBuild
    {
        public const string BuildFolder = "Builds";
        public const string ApkName = "Warfront.apk";
        public const string AabName = "Warfront.aab";

        [MenuItem("Warfront/Build Android APK", false, 100)]
        public static void BuildAndroidApkFromMenu()
        {
            BuildAndroidApk();
        }

        /// <summary>Configures the project, then builds a signed-and-installable ARM64 APK.</summary>
        public static void BuildAndroidApk()
        {
            try
            {
                if (!WarfrontProjectSetup.ConfigureAll())
                {
                    Fail("Project configuration reported a failure before the build started.");
                    return;
                }

                if (!SwitchToAndroid())
                {
                    Fail("Could not switch the active build target to Android. Is Android Build Support installed?");
                    return;
                }

                VerifyScenesInBuild();

                string output = ResolveApkPath();
                string directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = GetScenePaths(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result != BuildResult.Succeeded)
                {
                    Fail("Android build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
                    return;
                }

                long size = 0;
                try
                {
                    if (File.Exists(output)) size = new FileInfo(output).Length;
                }
                catch (Exception)
                {
                    size = 0;
                }

                Debug.Log("[Warfront] APK built: " + Path.GetFullPath(output) +
                          "  " + (size / (1024 * 1024)) + " MB  in " + summary.totalTime.TotalSeconds.ToString("0") + "s" +
                          "  warnings " + summary.totalWarnings);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Fail("Android build threw: " + ex);
            }
        }

        [MenuItem("Warfront/Build Android App Bundle (Play Store)", false, 101)]
        public static void BuildAndroidAppBundle()
        {
            try
            {
                if (!WarfrontProjectSetup.ConfigureAll()) { Fail("Configuration failed."); return; }
                if (!SwitchToAndroid()) { Fail("Could not switch build target to Android."); return; }

                EditorUserBuildSettings.buildAppBundle = true;
                Directory.CreateDirectory(BuildFolder);
                string output = Path.Combine(BuildFolder, AabName);

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = GetScenePaths(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                bool success = report.summary.result == BuildResult.Succeeded;
                if (success)
                {
                    Debug.Log("[Warfront] AAB built: " + Path.GetFullPath(output));
                    EditorApplication.Exit(0);
                }
                else
                {
                    Fail("AAB build failed: " + report.summary.result);
                }
            }
            catch (Exception ex)
            {
                Fail("AAB build threw: " + ex);
            }
        }

        /// <summary>Configures the project and compiles scripts without building. Used by CI on every push.</summary>
        public static void VerifyProject()
        {
            try
            {
                bool ok = WarfrontProjectSetup.ConfigureAll();
                if (!ok)
                {
                    Fail("Project configuration failed.");
                    return;
                }

                if (!VerifyScenesInBuild()) { Fail("Boot scene is missing from the build settings."); return; }

                Debug.Log("[Warfront] VerifyProject passed\n" + WarfrontProjectSetup.DescribeConfiguration());
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Fail("VerifyProject threw: " + ex);
            }
        }

        /// <summary>
        /// Where the APK should be written.
        ///
        /// Locally that is Builds/Warfront.apk. When GameCI runs the build it exports the location it
        /// will look in through BUILD_PATH (and BUILD_FILE for the name), and a custom build method must
        /// honour them or the action cannot find the artifact it was told to publish. Both the "full path
        /// including filename" and "directory + filename" shapes of BUILD_PATH are accepted because the
        /// action has used both over time.
        /// </summary>
        private static string ResolveApkPath()
        {
            string buildPath = Environment.GetEnvironmentVariable("BUILD_PATH");
            if (string.IsNullOrEmpty(buildPath)) return Path.Combine(BuildFolder, ApkName);

            if (buildPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) return buildPath;

            string fileName = Environment.GetEnvironmentVariable("BUILD_FILE");
            if (string.IsNullOrEmpty(fileName)) fileName = ApkName;
            if (!fileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) fileName += ".apk";

            return Path.Combine(buildPath, fileName);
        }

        private static bool SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return true;
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!switched)
            {
                Debug.LogError("[Warfront] SwitchActiveBuildTarget(Android) failed. Installed modules: " +
                               string.Join(", ", GetInstalledBuildTargets()));
            }
            return switched;
        }

        private static string[] GetInstalledBuildTargets()
        {
            List<string> targets = new List<string>(8);
            Array values = Enum.GetValues(typeof(BuildTarget));
            foreach (BuildTarget target in values)
            {
                if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, target)) targets.Add(target.ToString());
            }
            return targets.ToArray();
        }

        private static bool VerifyScenesInBuild()
        {
            string[] scenes = GetScenePaths();
            if (scenes.Length > 0) return true;
            Debug.LogError("[Warfront] No scenes registered in EditorBuildSettings.");
            return false;
        }

        private static string[] GetScenePaths()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            List<string> paths = new List<string>(scenes.Length);
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled) paths.Add(scenes[i].path);
            }
            return paths.ToArray();
        }

        /// <summary>Logs a failed build and exits with a non-zero code so CI cannot pass silently.</summary>
        private static void Fail(string message)
        {
            Debug.LogError("[Warfront] " + message);
            EditorApplication.Exit(1);
        }
    }
}
