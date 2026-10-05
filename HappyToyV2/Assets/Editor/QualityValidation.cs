using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Safe opt-in batch commands. Never call Generate, Expand, Apply, or save the authored scene.
    public static class QualityValidation
    {
        const string ScenePath = "Assets/Annex/SchoolAnnex.unity";
        const string Output = "Verification/quality-unity";
        [Serializable] class ValidationReport
        {
            public string status, unityVersion, scene, sceneSha256;
            public int pureClockAssertions, pureRippleAssertions, pureStealthAssertions, pureRestartAssertions, worldSigns, gameObjects, missingScripts, interactables, inspections;
        }
        [Serializable] class FingerprintEntry { public string path, sha256; }
        [Serializable] class BuildManifest
        {
            public string status, unityVersion, scene, createdUtc;
            public FingerprintEntry[] inputs;
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (scenes.Length != 1 || scenes[0].path != ScenePath)
                throw new InvalidOperationException("Select only the preserved authored annex scene before validation.");
            string before = Hash(ScenePath);
            int clockAssertions = EnemyAttackClockChecks.Run();
            int rippleAssertions = SurfaceRippleChecks.Run();
            int stealthAssertions = StealthRulesChecks.Run();
            int restartAssertions = SceneRestartGateChecks.Run();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int worldSignCount = AnnexSignFontValidation.Run(scene);
            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing = transforms.Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            if (missing != 0) throw new InvalidOperationException("Authored scene has " + missing + " missing scripts.");
            var sessions = transforms.Select(transform => transform.GetComponent<GameSession>()).Where(session => session).ToArray();
            if (sessions.Length != 1 || !sessions[0].player || !sessions[0].player.eyes)
                throw new InvalidOperationException("Expected one session with its authored player and camera references.");
            if (!sessions[0].requireAnnexRecords) throw new InvalidOperationException("Authored annex progression gate was disabled.");
            var items = transforms.SelectMany(transform => transform.GetComponents<Interactable>()).ToArray();
            foreach (var id in new[] { "register", "ribbon", "record", "restore", "music-roster", "archive-record", "nursery-tag" })
                if (items.Count(item => item.stableId == id) != 1)
                    throw new InvalidOperationException("Expected exactly one authored progression interaction: " + id);
            var inspections = items.Where(item => item.kind == Interactable.Kind.Inspect).ToArray();
            if (inspections.Any(item => string.IsNullOrWhiteSpace(item.inspectionText)))
                throw new InvalidOperationException("An inspection record has empty text.");
            var duplicate = inspections.GroupBy(item => string.IsNullOrEmpty(item.stableId) ? item.name : item.stableId).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("Duplicate inspection identity: " + duplicate.Key);
            if (Hash(ScenePath) != before) throw new InvalidOperationException("Authored scene bytes changed during read-only validation.");
            File.WriteAllText(Path.Combine(Output, "editor-validation.json"), JsonUtility.ToJson(new ValidationReport
            {
                status = "PASS", unityVersion = Application.unityVersion, scene = ScenePath, sceneSha256 = before,
                pureClockAssertions = clockAssertions, pureRippleAssertions = rippleAssertions, pureStealthAssertions = stealthAssertions,
                pureRestartAssertions = restartAssertions, worldSigns = worldSignCount, gameObjects = transforms.Length, missingScripts = missing,
                interactables = items.Length, inspections = inspections.Length
            }, true));
            Debug.Log("HAPPYTOY_QUALITY_VALIDATION_PASS clockAssertions=" + clockAssertions + " scene=" + ScenePath);
        }
        public static void BuildWindows()
        {
            Validate();
            string before = Hash(ScenePath);
            string destination = "Builds/Windows";
            var args = Environment.GetCommandLineArgs();
            int outputArg = Array.IndexOf(args, "-v2-build-output");
            if (outputArg >= 0 && outputArg + 1 < args.Length) destination = Path.GetFullPath(args[outputArg + 1]);
            Directory.CreateDirectory(destination);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, Path.Combine(destination, "HappyToyV2.exe"),
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (Hash(ScenePath) != before) throw new InvalidOperationException("Authored scene changed during build.");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Quality build failed: " + report.summary.result);
            // Ship the attribution alongside every playable build, including the
            // CC BY cabinet recordings. Source metadata remains in the project.
            File.Copy("ThirdParty/Audio/Audio-Credits.txt", Path.Combine(destination, "Audio-Credits.txt"), true);
            var inputs = new[] { "Assets", "ProjectSettings", "Packages" }
                .SelectMany(directory => Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                .Select(path => path.Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new FingerprintEntry { path = path, sha256 = Hash(path) }).ToArray();
            File.WriteAllText(Path.Combine(destination, "quality-build.json"), JsonUtility.ToJson(new BuildManifest
            {
                status = "PASS", unityVersion = Application.unityVersion, scene = ScenePath,
                createdUtc = DateTime.UtcNow.ToString("o"), inputs = inputs
            }, true));
            Debug.Log("HAPPYTOY_QUALITY_BUILD_PASS");
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
