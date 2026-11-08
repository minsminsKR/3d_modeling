using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class TempleNightSkyMetrics
        {
            public string pose, note;
            public int referenceDifferenceAtLeastOne, referenceDifferenceAtLeastTwo;
            public int restoredDifferencePixels, baselineErrorPixels;
            public string[] candidates;
            public int[] changedPixelsAtLeastTwo, errorPixels;
        }

        // Explicit editor-only GPU experiment: all candidates use the actual
        // sky and camera in a frozen prologue. It never installs new defaults.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleNightSkyOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-temple-night-sky-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review directory");
#if UNITY_EDITOR
            var referenceShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Tests/PlayMode/TempleForestNightSkyReference.shader");
            Assert.That(referenceShader && referenceShader.isSupported, Is.True, "Original sky shader reference is required");
#else
            Assert.Ignore("The pre-change reference shader is an editor-only test asset");
            yield break;
#endif
            Directory.CreateDirectory(output);
            var previousSkybox = RenderSettings.skybox;
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var atmosphere = world.GetComponent(RequireType("TempleForestAtmosphere"));
            Assert.That(atmosphere, Is.Not.Null, "The production atmosphere must own this prologue");
            Assert.That(Get<int>(atmosphere, "VolumeCount"), Is.EqualTo(5), "The production atmosphere must be ready");
            var sky = RenderSettings.skybox;
            Assert.That(sky && sky.shader && sky.shader.isSupported, Is.True);
            Assert.That(sky, Is.Not.SameAs(previousSkybox), "The prologue must install its own sky material");
            Assert.That(sky.name, Is.EqualTo("Owned temple night sky"));
            Assert.That(sky.shader, Is.SameAs(Resources.Load<Shader>("TempleForestNightSky")));
            Assert.That(sky.GetFloat("_SkyTime"), Is.EqualTo(Get<float>(atmosphere, "SimulationTime")).Within(.0001f),
                "The installed sky must follow the production atmosphere's simulation clock");
            Assert.That(sky.HasProperty("_CloudBrightness") && sky.HasProperty("_MoonHaloStrength") &&
                sky.HasProperty("_CloudScale") && sky.HasProperty("_CloudDetail"), Is.True);
            float oldCloud = sky.GetFloat("_CloudBrightness"), oldHalo = sky.GetFloat("_MoonHaloStrength");
            float oldScale = sky.GetFloat("_CloudScale"), oldDetail = sky.GetFloat("_CloudDetail");
            Assert.That(oldCloud, Is.EqualTo(1f)); Assert.That(oldHalo, Is.EqualTo(1f));
            Assert.That(oldScale, Is.EqualTo(1f)); Assert.That(oldDetail, Is.EqualTo(0f));
            var eyes = Get<Camera>(player, "eyes");
            var motor = (Behaviour)player;
            bool oldMotorEnabled = motor.enabled;
            var oldPosition = player.transform.position;
            var oldPlayerRotation = player.transform.rotation;
            var oldEyeRotation = eyes.transform.rotation;
            float oldFieldOfView = eyes.fieldOfView, oldAspect = eyes.aspect;
            var oldSkybox = RenderSettings.skybox;
            Material reference = null;
            bool paused = false;
            try
            {
#if UNITY_EDITOR
                reference = new Material(referenceShader) { name = "Original temple sky comparison reference" };
                reference.SetVector("_MoonDirection", sky.GetVector("_MoonDirection"));
                motor.enabled = false; eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                float skyTime = sky.GetFloat("_SkyTime");
                reference.SetFloat("_SkyTime", skyTime);
                var labels = new[] { "cloud-2_5", "cloud-4", "halo-1_5", "halo-2", "combined-2_5-1_5", "combined-4-2",
                    "scale-1_6", "scale-2_2", "cloud-2_5-scale-1_6", "cloud-2_5-scale-2_2",
                    "cloud-2_5-scale-1_6-detail-0_5", "cloud-2_5-scale-1_6-detail-1",
                    "cloud-2_5-scale-2_2-detail-0_5", "cloud-2_5-scale-2_2-detail-1" };
                var clouds = new[] { 2.5f, 4f, 1f, 1f, 2.5f, 4f, 1f, 1f, 2.5f, 2.5f, 2.5f, 2.5f, 2.5f, 2.5f };
                var halos = new[] { 1f, 1f, 1.5f, 2f, 1.5f, 2f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
                var scales = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1.6f, 2.2f, 1.6f, 2.2f, 1.6f, 1.6f, 2.2f, 2.2f };
                var details = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .5f, 1f, .5f, 1f };
                for (int pose = 0; pose < 2; pose++)
                {
                    string name = pose == 0 ? "front" : "upward";
                    PlacePlayer(new Vector3(500f, .04f, 503.5f));
                    var direction = pose == 0 ? new Vector3(500f, 1.9f, 509f) - eyes.transform.position
                        : new Vector3(-.10f, .50f, .90f);
                    eyes.transform.rotation = Quaternion.LookRotation(direction);
                    Physics.SyncTransforms(); yield return null;
                    direction = pose == 0 ? new Vector3(500f, 1.9f, 509f) - eyes.transform.position
                        : new Vector3(-.10f, .50f, .90f);
                    eyes.transform.rotation = Quaternion.LookRotation(direction);
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(sky.GetFloat("_SkyTime"), Is.EqualTo(skyTime));
                    RenderSettings.skybox = reference;
                    var original = TempleNightSkyFrame(eyes, Path.Combine(output, name + "-original-reference.png"));
                    RenderSettings.skybox = sky;
                    sky.SetFloat("_CloudBrightness", 1f); sky.SetFloat("_MoonHaloStrength", 1f);
                    sky.SetFloat("_CloudScale", 1f); sky.SetFloat("_CloudDetail", 0f);
                    var baseline = TempleNightSkyFrame(eyes, Path.Combine(output, name + "-baseline.png"));
                    int baselineErrors = TempleNightSkyErrors(baseline);
                    Assert.That(baselineErrors, Is.LessThanOrEqualTo(128), "Baseline has magenta error-shader pixels");
                    int referenceDiffOne = TempleNightSkyDifference(original, baseline, 1);
                    int referenceDiffTwo = TempleNightSkyDifference(original, baseline, 2);
                    Assert.That(referenceDiffTwo, Is.Zero, "Default differs from the original by two or more RGB levels");
                    Assert.That(referenceDiffOne, Is.LessThanOrEqualTo(8),
                        "Default differs by one RGB level in more than eight pixels");
                    var changed = new int[labels.Length];
                    var errors = new int[labels.Length];
                    for (int i = 0; i < labels.Length; i++)
                    {
                        sky.SetFloat("_CloudBrightness", clouds[i]); sky.SetFloat("_MoonHaloStrength", halos[i]);
                        sky.SetFloat("_CloudScale", scales[i]); sky.SetFloat("_CloudDetail", details[i]);
                        var candidate = TempleNightSkyFrame(eyes, Path.Combine(output, name + "-" + labels[i] + ".png"));
                        changed[i] = TempleNightSkyDifference(baseline, candidate, 2);
                        errors[i] = TempleNightSkyErrors(candidate);
                        Assert.That(errors[i], Is.LessThanOrEqualTo(baselineErrors + 128), labels[i] + " introduced magenta error-shader pixels");
                    }
                    sky.SetFloat("_CloudBrightness", oldCloud); sky.SetFloat("_MoonHaloStrength", oldHalo);
                    sky.SetFloat("_CloudScale", oldScale); sky.SetFloat("_CloudDetail", oldDetail);
                    var restored = TempleNightSkyFrame(eyes, Path.Combine(output, name + "-restored.png"));
                    int restoredDiff = TempleNightSkyDifference(baseline, restored, 1);
                    Assert.That(restoredDiff, Is.Zero, "The original sky values must be restored");
                    Assert.That(RenderSettings.skybox, Is.SameAs(sky));
                    var metrics = new TempleNightSkyMetrics {
                        pose = name, referenceDifferenceAtLeastOne = referenceDiffOne,
                        referenceDifferenceAtLeastTwo = referenceDiffTwo, restoredDifferencePixels = restoredDiff,
                        baselineErrorPixels = baselineErrors, candidates = labels, changedPixelsAtLeastTwo = changed,
                        errorPixels = errors, note = "Frozen editor camera comparison; not a GPU benchmark or visual quality certification."
                    };
                    File.WriteAllText(Path.Combine(output, name + "-metrics.json"), JsonUtility.ToJson(metrics, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(metrics));
                }
#endif
            }
            finally
            {
                if (sky) { sky.SetFloat("_CloudBrightness", oldCloud); sky.SetFloat("_MoonHaloStrength", oldHalo);
                    sky.SetFloat("_CloudScale", oldScale); sky.SetFloat("_CloudDetail", oldDetail); }
                RenderSettings.skybox = oldSkybox;
                PlacePlayer(oldPosition); player.transform.rotation = oldPlayerRotation; eyes.transform.rotation = oldEyeRotation;
                eyes.fieldOfView = oldFieldOfView; eyes.aspect = oldAspect; motor.enabled = oldMotorEnabled;
                if (paused) Call(shell, "Resume");
                if (reference) Object.Destroy(reference);
            }
        }

        static Color32[] TempleNightSkyFrame(Camera eyes, string path)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(path, frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }

        static int TempleNightSkyDifference(Color32[] a, Color32[] b, int threshold)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length));
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= threshold)
                    changed++;
            return changed;
        }

        static int TempleNightSkyErrors(Color32[] pixels)
        {
            int error = 0;
            foreach (var p in pixels) if (p.r >= 220 && p.b >= 220 && p.g <= 64) error++;
            return error;
        }
    }
}
