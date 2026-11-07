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
        [Serializable] sealed class TempleFacadeColorReview
        {
            public string pose, note;
            public int width, height;
            public bool flashlightEnabled;
            public Color baselineColor, candidateColor;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float baselineRepeatError, candidateRepeatError;
            public CandleReviewRegion[] regions;
        }

        // Explicitly opt-in: the temporary candidate never changes production settings.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleFacadeColorOffscreenAbbaReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-facade-color-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh comparison output");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var eyes = Get<Camera>(player, "eyes");
            var facadeObject = world.transform.Find("Temple facade fill");
            var hall = world.transform.Find("Temple hall exterior");
            var vessel = world.transform.Find("Incense vessel");
            Assert.That(facadeObject, Is.Not.Null); Assert.That(hall, Is.Not.Null); Assert.That(vessel, Is.Not.Null);
            var facade = facadeObject.GetComponent<Light>();
            var flashlight = Get<Light>(player, "flashlight");
            Assert.That(facade, Is.Not.Null); Assert.That(flashlight, Is.Not.Null);
            var hallRenderers = hall.GetComponentsInChildren<Renderer>(true);
            var vesselRenderers = vessel.GetComponentsInChildren<Renderer>(true);
            var oldColor = facade.color;
            var candidateColor = new Color(1f, .38f, .25f, oldColor.a);
            var oldFlashlightEnabled = flashlight.enabled;
            var motor = (Behaviour)player; bool oldMotorEnabled = motor.enabled;
            var oldPosition = player.transform.position;
            var oldPlayerRotation = player.transform.rotation;
            var oldEyeRotation = eyes.transform.rotation;
            float oldFieldOfView = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool paused = false;
            try
            {
                motor.enabled = false;
                eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero, "The A/B comparison must freeze simulation");
                var positions = new[] { new Vector3(500f, .04f, 503.5f), new Vector3(503f, .04f, 506f), new Vector3(500f, .04f, 508.5f) };
                var targets = new[] { new Vector3(500f, 2.1f, 514f), new Vector3(500f, 2.1f, 514f), new Vector3(500f, 2.1f, 514f) };
                var names = new[] { "front", "side", "flashlight" };
                const int width = 1280, height = 720;
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(positions[pose]);
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    flashlight.enabled = pose == 2;
                    Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    if (pose == 2) Assert.That(flashlight.isActiveAndEnabled, Is.True, "The actual flashlight must illuminate the scene");
                    var fixedPosition = eyes.transform.position;
                    var fixedRotation = eyes.transform.rotation;
                    var hallRect = CandleReviewBounds(eyes, hallRenderers, width, height);
                    var vesselRect = CandleReviewBounds(eyes, vesselRenderers, width, height);
                    Assert.That(hallRect.width * hallRect.height, Is.GreaterThan(0), "Hall must be in the camera view");
                    var regions = new[] { hallRect, vesselRect };
                    var frames = new Color32[4][];
                    var colors = new[] { oldColor, candidateColor, candidateColor, oldColor };
                    var labels = new[] { "a-baseline", "b-candidate", "b-candidate-repeat", "a-baseline-restored" };
                    for (int i = 0; i < colors.Length; i++)
                    {
                        facade.color = colors[i]; yield return null;
                        Assert.That(Time.timeScale, Is.Zero);
                        Assert.That(Vector3.Distance(eyes.transform.position, fixedPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedRotation), Is.LessThan(.01f));
                        var frame = SchoolHorrorFrame(eyes, width, height);
                        try
                        {
                            frames[i] = frame.GetPixels32();
                            File.WriteAllBytes(Path.Combine(output, names[pose] + "-" + labels[i] + ".png"), frame.EncodeToPNG());
                        }
                        finally { Object.Destroy(frame); }
                    }
                    float baselineError = CandleReviewMaxRegionError(frames[0], frames[3], width, regions);
                    float candidateError = CandleReviewMaxRegionError(frames[1], frames[2], width, regions);
                    Assert.That(baselineError, Is.LessThanOrEqualTo(1f), "Restored A must match the original frozen frame");
                    Assert.That(candidateError, Is.LessThanOrEqualTo(1f), "Repeated B must remain stable");
                    var report = new TempleFacadeColorReview {
                        pose = names[pose], width = width, height = height,
                        flashlightEnabled = flashlight.isActiveAndEnabled,
                        baselineColor = oldColor, candidateColor = candidateColor,
                        cameraPosition = fixedPosition, cameraRotation = fixedRotation,
                        baselineRepeatError = baselineError, candidateRepeatError = candidateError,
                        regions = new[] { CandleReviewRegionMetrics("temple-hall", frames[0], frames[1], width, hallRect),
                                          CandleReviewRegionMetrics("incense-vessel", frames[0], frames[1], width, vesselRect) },
                        note = "Frozen ABBA offscreen camera comparison. Screen-space bounding rectangles include background; clipping counts are not proof of photographic quality. No player FPS or GPU timing claim."
                    };
                    File.WriteAllText(Path.Combine(output, names[pose] + "-metrics.json"), JsonUtility.ToJson(report, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                }
            }
            finally
            {
                if (facade) facade.color = oldColor;
                if (flashlight) flashlight.enabled = oldFlashlightEnabled;
                PlacePlayer(oldPosition); player.transform.rotation = oldPlayerRotation; eyes.transform.rotation = oldEyeRotation;
                eyes.fieldOfView = oldFieldOfView; eyes.aspect = oldAspect;
                motor.enabled = oldMotorEnabled;
                if (paused) Call(shell, "Resume");
            }
        }
    }
}
