using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Warfront.Tests.EditMode
{
    /// <summary>
    /// Source level performance/architecture lint. It runs in the Unity Test Runner and also in the
    /// headless verification harness (set WARFRONT_SOURCE_ROOT to the project root), which means the
    /// performance rules are enforced by CI rather than by memory.
    /// </summary>
    public class SourceLintTests
    {
        private static string ResolveRoot()
        {
            string env = System.Environment.GetEnvironmentVariable("WARFRONT_SOURCE_ROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "Assets"))) return env;
            if (!string.IsNullOrEmpty(Application.dataPath) && Directory.Exists(Application.dataPath))
            {
                return Directory.GetParent(Application.dataPath) != null
                    ? Directory.GetParent(Application.dataPath).FullName
                    : Application.dataPath;
            }
            return null;
        }

        private static List<string> RuntimeFiles(string root)
        {
            var files = new List<string>();
            string[] folders = { Path.Combine(root, "Assets", "Warfront", "Core"), Path.Combine(root, "Assets", "Warfront", "Scripts") };
            for (int i = 0; i < folders.Length; i++)
            {
                if (!Directory.Exists(folders[i])) continue;
                string[] found = Directory.GetFiles(folders[i], "*.cs", SearchOption.AllDirectories);
                for (int j = 0; j < found.Length; j++) files.Add(found[j]);
            }
            return files;
        }

        private static void AssertNoPattern(string file, string pattern, string message)
        {
            string text = StripComments(File.ReadAllText(file));
            if (text.Contains(pattern))
            {
                Assert.Fail(message + " (" + Path.GetFileName(file) + ")");
            }
        }

        /// <summary>
        /// Removes comments so documentation may mention banned APIs without tripping the lint.
        /// String contents are preserved: using a banned name in a string is still a code smell.
        /// </summary>
        private static string StripComments(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        sb.Append(c);
                    }
                    continue;
                }
                if (blockComment)
                {
                    if (c == '*' && next == '/') { blockComment = false; i++; }
                    continue;
                }
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < text.Length) { sb.Append(text[++i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < text.Length) { sb.Append(text[++i]); continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && next == '/') { lineComment = true; i++; continue; }
                if (c == '/' && next == '*') { blockComment = true; i++; continue; }
                if (c == '"') { inString = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        [Test]
        public void RuntimeCodeNeverUsesSceneWideSearches()
        {
            string root = ResolveRoot();
            if (root == null) Assert.Ignore("Project root could not be resolved");

            List<string> files = RuntimeFiles(root);
            Assert.Greater(files.Count, 0, "no runtime source files found - lint would be meaningless");

            string[] banned = { "FindObjectOfType", "FindObjectsOfType", "FindObjectsByType", "GameObject.Find(", "FindGameObjectsWithTag", "GameObject.FindWithTag" };
            for (int i = 0; i < files.Count; i++)
            {
                for (int j = 0; j < banned.Length; j++)
                {
                    AssertNoPattern(files[i], banned[j],
                        "Scene wide lookup '" + banned[j] + "' is banned in runtime code - inject the dependency instead");
                }
            }
        }

        [Test]
        public void RuntimeCodeNeverUsesLinq()
        {
            string root = ResolveRoot();
            if (root == null) Assert.Ignore("Project root could not be resolved");

            List<string> files = RuntimeFiles(root);
            for (int i = 0; i < files.Count; i++)
            {
                AssertNoPattern(files[i], "using System.Linq", "LINQ is banned in runtime code (per-frame allocations)");
            }
        }

        [Test]
        public void RuntimeCodeNeverUsesSendMessageOrInvokeStrings()
        {
            string root = ResolveRoot();
            if (root == null) Assert.Ignore("Project root could not be resolved");

            string[] banned = { "SendMessage(", "InvokeRepeating(\"" };
            List<string> files = RuntimeFiles(root);
            for (int i = 0; i < files.Count; i++)
            {
                for (int j = 0; j < banned.Length; j++)
                {
                    AssertNoPattern(files[i], banned[j], "Reflection based invocation '" + banned[j] + "' is banned");
                }
            }
        }

        [Test]
        public void CoreLogicAssemblyStaysEngineIndependent()
        {
            string root = ResolveRoot();
            if (root == null) Assert.Ignore("Project root could not be resolved");

            string coreRoot = Path.Combine(root, "Assets", "Warfront", "Core");
            Assert.IsTrue(Directory.Exists(coreRoot), "Core folder missing");

            string[] files = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text = StripComments(File.ReadAllText(files[i]));
                Assert.IsFalse(text.Contains("MonoBehaviour"),
                    "Core must stay pure logic (" + Path.GetFileName(files[i]) + ")");
                Assert.IsFalse(text.Contains("ScriptableObject"),
                    "Core must not use ScriptableObject (" + Path.GetFileName(files[i]) + ")");
                Assert.IsFalse(text.Contains("Resources.Load"),
                    "Core must not load assets (" + Path.GetFileName(files[i]) + ")");
            }
        }

        [Test]
        public void NoFileDeclaresASingletonPattern()
        {
            string root = ResolveRoot();
            if (root == null) Assert.Ignore("Project root could not be resolved");

            List<string> files = RuntimeFiles(root);
            for (int i = 0; i < files.Count; i++)
            {
                string text = File.ReadAllText(files[i]);
                Assert.IsFalse(text.Contains("public static GameManager Instance") || text.Contains("public static Game Instance"),
                    "Global singleton access is banned - use GameContext injection (" + Path.GetFileName(files[i]) + ")");
            }
        }
    }
}
