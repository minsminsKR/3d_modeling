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
        [Serializable] sealed class TempleForegroundVisibilityReport
        {
            public string note;
            public int width, height, baselineRepeatPixels;
            public TempleForegroundVisibilityTree[] trees;
        }

        [Serializable] sealed class TempleForegroundVisibilityTree
        {
            public string name;
            public Vector3 localPosition;
            public int rendererCount, changedPixels, minX, minY, maxX, maxY;
        }

        // Opt-in identification aid: each real pine's visual renderers are
        // hidden for one render and restored immediately. A difference can
        // include uncovered surfaces, shadows, mist and postprocessing; it is
        // not a perfect object-ID mask or evidence of gameplay performance.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleForegroundTreeVisibilityOffscreenReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-foreground-tree-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a new output directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var eyes = Get<Camera>(player, "eyes");
            var flashlight = Get<Light>(player, "flashlight");
            var motor = (Behaviour)player;
            Vector3 oldPosition = player.transform.position;
            Quaternion oldPlayerRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldMotor = motor.enabled, oldFlashlight = flashlight.enabled, paused = false;
            const int width = 1280, height = 720;
            try
            {
                motor.enabled = false; flashlight.enabled = false;
                eyes.fieldOfView = 72f; eyes.aspect = (float)width / height;
                Call(player, "RelocateWorld", new Vector3(500f, .04f, 503.5f), Quaternion.identity);
                eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.9f, 509f) - eyes.transform.position);
                Physics.SyncTransforms();
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero, "A frozen scene is required for frame comparisons");
                eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.9f, 509f) - eyes.transform.position);
                var baseline = CaptureTempleForegroundFrame(eyes, output, "baseline.png", width, height);
                var results = new TempleForegroundVisibilityTree[6];
                for (int index = 10; index <= 15; index++)
                {
                    var tree = world.transform.Find("Pine " + index);
                    Assert.That(tree, Is.Not.Null, "Missing production tree " + index);
                    var visual = tree.Find("Reviewed forest pine");
                    Assert.That(visual, Is.Not.Null, "Missing reviewed pine visual " + index);
                    var renderers = visual.GetComponentsInChildren<Renderer>(true);
                    Assert.That(renderers.Length, Is.GreaterThan(0));
                    var previous = renderers.Select(renderer => renderer.forceRenderingOff).ToArray();
                    Color32[] hidden;
                    try
                    {
                        foreach (var renderer in renderers) renderer.forceRenderingOff = true;
                        hidden = CaptureTempleForegroundFrame(eyes, output, "pine-" + index + "-hidden.png", width, height);
                    }
                    finally
                    {
                        for (int i = 0; i < renderers.Length; i++)
                            if (renderers[i]) renderers[i].forceRenderingOff = previous[i];
                    }
                    int count = 0, minX = width, minY = height, maxX = -1, maxY = -1;
                    var mask = new Color32[baseline.Length];
                    for (int i = 0; i < baseline.Length; i++)
                    {
                        bool changed = TempleForegroundPixelDiffers(baseline[i], hidden[i]);
                        mask[i] = changed ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
                        if (!changed) continue;
                        int x = i % width, y = height - 1 - i / width;
                        minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); count++;
                    }
                    var maskTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    try
                    {
                        maskTexture.SetPixels32(mask); maskTexture.Apply();
                        File.WriteAllBytes(Path.Combine(output, "pine-" + index + "-difference.png"), maskTexture.EncodeToPNG());
                    }
                    finally { Object.Destroy(maskTexture); }
                    results[index - 10] = new TempleForegroundVisibilityTree {
                        name = tree.name, localPosition = tree.localPosition, rendererCount = renderers.Length,
                        changedPixels = count, minX = count > 0 ? minX : -1, minY = count > 0 ? minY : -1,
                        maxX = maxX, maxY = maxY };
                }
                var repeat = CaptureTempleForegroundFrame(eyes, output, "baseline-restored.png", width, height);
                int repeatPixels = 0;
                for (int i = 0; i < baseline.Length; i++)
                    if (TempleForegroundPixelDiffers(baseline[i], repeat[i])) repeatPixels++;
                var report = new TempleForegroundVisibilityReport {
                    width = width, height = height, baselineRepeatPixels = repeatPixels, trees = results,
                    note = "Pixels differ by at least 3 in one RGB channel; coordinates use top-left origin. Differences include uncovered background, lighting and postprocessing. Baseline repeat difference measures stability. This is not an object-ID mask or performance measurement." };
                File.WriteAllText(Path.Combine(output, "foreground-tree-visibility.json"), JsonUtility.ToJson(report, true));
                TestContext.Out.WriteLine(JsonUtility.ToJson(report));
            }
            finally
            {
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect;
                Call(player, "RelocateWorld", oldPosition, oldPlayerRotation);
                eyes.transform.rotation = oldEyeRotation;
                flashlight.enabled = oldFlashlight; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
                Physics.SyncTransforms();
            }
        }

        static Color32[] CaptureTempleForegroundFrame(Camera eyes, string output, string name, int width, int height)
        {
            var frame = SchoolHorrorFrame(eyes, width, height);
            try
            {
                var pixels = frame.GetPixels32();
                File.WriteAllBytes(Path.Combine(output, name), frame.EncodeToPNG());
                return pixels;
            }
            finally { Object.Destroy(frame); }
        }

        static bool TempleForegroundPixelDiffers(Color32 a, Color32 b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)) >= 3;
    }
}
