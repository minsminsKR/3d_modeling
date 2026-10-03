using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.Tests.EditMode
{
    // Test-only assembly: runtime scripts and their serialized assembly identities stay unchanged.
    [TestFixture]
    public sealed class CloudEditModeTests
    {
        const string ScenePath = "Assets/Annex/SchoolAnnex.unity";
        const string ExpectedUnityVersion = "6000.6.0f1";
        const string EditorAssembly = "Assembly-CSharp-Editor";
        const string RuntimeAssembly = "Assembly-CSharp";

        // Pin the existing preservation contract as well as reading it. A rewritten baseline
        // must not make a changed authored scene or build selection silently pass these tests.
        static readonly Dictionary<string, string> ProtectedInputs = new Dictionary<string, string>
        {
            { ScenePath, "0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede" },
            { ScenePath + ".meta", "cced22a74976e0f4ebaa80138bda97d0bf5b1103a776613a31b2f1279a0ed77b" },
            { "ProjectSettings/EditorBuildSettings.asset", "bba9b21dd739d2451b44faa463225a94be4ea90b6c6ff284b27a14b06de2ef21" }
        };

        [Serializable]
        sealed class BaselineHeader
        {
            public int schema;
            public string selected_scene;
        }

        [Serializable]
        sealed class ValidationReport
        {
            public string status, unityVersion, scene, sceneSha256;
            public int pureClockAssertions, pureRippleAssertions, pureStealthAssertions, pureRestartAssertions;
            public int worldSigns, gameObjects, missingScripts, interactables, inspections;
        }

        [Test]
        public void TargetEditorAndGameplayAssemblyIdentitiesArePreserved()
        {
            Assert.That(Application.unityVersion, Is.EqualTo(ExpectedUnityVersion),
                "Run this release gate with the exact project's Unity editor version.");
            foreach (string name in new[]
            {
                "GameSession", "PlayerMotor", "GameShell", "GameShellView", "Interactable",
                "StalkerBrain", "EnemyAttackClock", "SurfaceRippleBuffer", "StealthRules",
                "SceneRestartGate", "AnnexSignFont", "FirecrackerInventory", "NavMeshStartup"
            })
            {
                Type type = RequireType("HappyToy.V2." + name, RuntimeAssembly);
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(RuntimeAssembly),
                    "Do not migrate existing gameplay scripts into a test-support or runtime asmdef.");
            }
            Type validator = RequireType("HappyToy.V2.Editor.QualityValidation", EditorAssembly);
            Assert.That(validator.Assembly.GetName().Name, Is.EqualTo(EditorAssembly));
        }

        [Test]
        public void ActualEnemyAttackClockPassesAll29Checks()
        {
            Assert.That(InvokeEditorCheck("EnemyAttackClockChecks", "Run", typeof(int)), Is.EqualTo(29));
        }

        [Test]
        public void ActualSurfaceRippleBufferPassesAll20Checks()
        {
            Assert.That(InvokeEditorCheck("SurfaceRippleChecks", "Run", typeof(int)), Is.EqualTo(20));
        }

        [Test]
        public void ActualStealthRulesPassAll68Checks()
        {
            Assert.That(InvokeEditorCheck("StealthRulesChecks", "Run", typeof(int)), Is.EqualTo(68));
        }

        [Test]
        public void ActualSceneRestartGatePassesAll26Checks()
        {
            Assert.That(InvokeEditorCheck("SceneRestartGateChecks", "Run", typeof(int)), Is.EqualTo(26));
        }

        [Test]
        public void ImportedAuthoredScenePassesQualityValidationWithoutChangingProtectedFiles()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            AssertPreservedInputs(projectRoot);
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            // Never discard an editor user's unsaved work in order to run a release gate.
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                Assert.That(scene.isDirty, Is.False,
                    "Close or save your own modified scene before running this read-only test: " + scene.path);
            }

            try
            {
                // Calls the real editor command: imported Korean font/shader coverage, scene
                // missing scripts, session/player/camera references, and progression inventory.
                // No scene builder, generator, expansion or save API is called by this suite.
                InvokeEditorCheck("QualityValidation", "Validate", typeof(void));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
                EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
                Assert.That(enabled.Length, Is.EqualTo(1));
                Assert.That(enabled[0].path, Is.EqualTo(ScenePath));

                string reportPath = Path.Combine(projectRoot, "Verification/quality-unity/editor-validation.json");
                Assert.That(File.Exists(reportPath), Is.True, "The real validator must produce its current result.");
                ValidationReport report = JsonUtility.FromJson<ValidationReport>(File.ReadAllText(reportPath));
                Assert.That(report, Is.Not.Null);
                Assert.That(report.status, Is.EqualTo("PASS"));
                Assert.That(report.unityVersion, Is.EqualTo(ExpectedUnityVersion));
                Assert.That(report.scene, Is.EqualTo(ScenePath));
                Assert.That(report.sceneSha256, Is.EqualTo(ProtectedInputs[ScenePath]));
                Assert.That(report.pureClockAssertions, Is.EqualTo(29));
                Assert.That(report.pureRippleAssertions, Is.EqualTo(20));
                Assert.That(report.pureStealthAssertions, Is.EqualTo(68));
                Assert.That(report.pureRestartAssertions, Is.EqualTo(26));
                Assert.That(report.worldSigns, Is.EqualTo(9), "All nine preserved authored signs must pass font validation.");
                Assert.That(report.missingScripts, Is.Zero);
                Assert.That(report.gameObjects, Is.GreaterThan(0));
                Assert.That(report.interactables, Is.GreaterThanOrEqualTo(7));
                Assert.That(report.inspections, Is.GreaterThan(0));
            }
            finally
            {
                try
                {
                    // Restore which scenes were open/active, including when validation fails.
                    // Deliberately do not save the loaded authored scene, even if import hooks dirty it.
                    if (previousSetup.Length > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                    else
                        // Match UTF's restoration fallback for an untitled initial editor scene.
                        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    SceneSetup[] restored = EditorSceneManager.GetSceneManagerSetup();
                    Assert.That(restored.Length, Is.EqualTo(previousSetup.Length), "Restore the caller's scene setup.");
                    for (int index = 0; index < previousSetup.Length; index++)
                    {
                        Assert.That(restored[index].path, Is.EqualTo(previousSetup[index].path));
                        Assert.That(restored[index].isLoaded, Is.EqualTo(previousSetup[index].isLoaded));
                        Assert.That(restored[index].isActive, Is.EqualTo(previousSetup[index].isActive));
                    }
                }
                finally
                {
                    AssertPreservedInputs(projectRoot);
                }
            }
        }

        static Type RequireType(string fullName, string assemblyName)
        {
            Type type = Type.GetType(fullName + ", " + assemblyName, false);
            Assert.That(type, Is.Not.Null,
                "Required production type was not loaded: " + fullName + ", " + assemblyName);
            return type;
        }

        static object InvokeEditorCheck(string typeName, string methodName, Type returnType)
        {
            Type type = RequireType("HappyToy.V2.Editor." + typeName, EditorAssembly);
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Required real editor validation method is missing: " + typeName + "." + methodName);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), "The production validator contract changed.");
            try
            {
                return method.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                // Preserve the real failing check and stack in NUnit/UBA results.
                if (exception.InnerException != null)
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        static void AssertPreservedInputs(string projectRoot)
        {
            string baselinePath = Path.Combine(projectRoot, "Tools/quality/scene_baseline.json");
            Assert.That(File.Exists(baselinePath), Is.True, "The original scene-preservation baseline is required.");
            string json = File.ReadAllText(baselinePath);
            BaselineHeader header = JsonUtility.FromJson<BaselineHeader>(json);
            Assert.That(header, Is.Not.Null);
            Assert.That(header.schema, Is.EqualTo(1));
            Assert.That(header.selected_scene, Is.EqualTo(ScenePath));
            Match protectedSection = Regex.Match(json, "\"protected_files\"\\s*:\\s*\\{(?<files>[^}]+)\\}");
            Assert.That(protectedSection.Success, Is.True, "The preservation baseline must declare protected_files.");
            MatchCollection entries = Regex.Matches(protectedSection.Groups["files"].Value,
                "\"(?<path>[^\"]+)\"\\s*:\\s*\"(?<sha256>[a-f0-9]{64})\"");
            Assert.That(entries.Count, Is.EqualTo(ProtectedInputs.Count));
            var observed = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match entry in entries)
            {
                string path = entry.Groups["path"].Value;
                Assert.That(observed.Add(path), Is.True, "Duplicate preservation entry: " + path);
                Assert.That(ProtectedInputs.ContainsKey(path), Is.True, "Unexpected protected file: " + path);
                string expected = ProtectedInputs[path];
                Assert.That(entry.Groups["sha256"].Value, Is.EqualTo(expected),
                    "The preserved baseline must not be rewritten to silence a failure: " + path);
                string fullPath = Path.Combine(projectRoot, path);
                Assert.That(File.Exists(fullPath), Is.True, "Protected input is missing: " + path);
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(fullPath))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    Assert.That(actual, Is.EqualTo(expected), "Protected authored input changed: " + path);
                }
            }
        }
    }
}
