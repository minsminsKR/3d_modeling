using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
        // Diagnostic only: hide each actual production renderer independently
        // without changing its meshes, transforms, material, physics, or LODs.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleStoneLayerOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-temple-stone-layer-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length), "Review output is required");
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True,
                "Batch mode, muted output and desktop isolation are required");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            IsolateLegacyStoneReview(world.transform);
            var relief = world.transform.Find("Authored forest stone approach");
            var bed = world.transform.Find("Recessed forest stone bed");
            Assert.That(relief, Is.Not.Null); Assert.That(bed, Is.Not.Null);
            var reliefRenderers = relief.GetComponentsInChildren<Renderer>(true);
            var bedRenderers = bed.GetComponentsInChildren<Renderer>(true);
            Assert.That(reliefRenderers.Length, Is.GreaterThan(0));
            Assert.That(bedRenderers.Length, Is.GreaterThan(0));
            Assert.That(relief.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(bed.GetComponentsInChildren<Collider>(true), Is.Empty);
            var reliefWasForcedOff = reliefRenderers.Select(r => r.forceRenderingOff).ToArray();
            var bedWasForcedOff = bedRenderers.Select(r => r.forceRenderingOff).ToArray();
            Assert.That(reliefWasForcedOff.Any(value => value) || bedWasForcedOff.Any(value => value), Is.False,
                "The reference camera must start with both production layers visible");
            var eyes = Get<Camera>(player, "eyes");
            var flashlight = Get<Light>(player, "flashlight");
            Assert.That(eyes && flashlight, Is.True);
            Vector3 oldPlayerPosition = player.transform.position, oldEyeLocalPosition = eyes.transform.localPosition;
            Quaternion oldPlayerRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            float oldFieldOfView = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldFlashlight = flashlight.enabled;
            var motor = (Behaviour)player; bool oldMotor = motor.enabled;
            bool paused = false;
            try
            {
                motor.enabled = false; eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero, "Capture must freeze simulation");
                var feet = new[] { new Vector3(500, .04f, 503.5f), new Vector3(500, .04f, 503.5f),
                    new Vector3(500, .04f, 502.5f) };
                var targets = new[] { new Vector3(500, 1.9f, 509), new Vector3(500, 1.1f, 509),
                    new Vector3(500, .65f, 508.5f) };
                var names = new[] { "front", "low", "flashlight" };
                var labels = new[] { "baseline", "relief-off", "bed-off", "baseline-restored" };
                var report = new StringBuilder();
                report.AppendLine("Diagnostic offscreen captures of actual renderers. Changed pixels are full-frame, not a perfect object mask; transparency and post-processing can affect them. No GPU timing, temporal flicker, actual player FPS, or subjective quality is established.");
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(feet[pose]); eyes.transform.localPosition = oldEyeLocalPosition;
                    if (pose == 1) eyes.transform.position -= Vector3.up * .7f;
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    flashlight.enabled = pose == 2;
                    Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    if (pose == 2) Assert.That(flashlight.isActiveAndEnabled, Is.True);
                    Vector3 cameraPosition = eyes.transform.position;
                    Quaternion cameraRotation = eyes.transform.rotation;
                    var frames = new Color32[4][];
                    for (int frameIndex = 0; frameIndex < labels.Length; frameIndex++)
                    {
                        for (int i = 0; i < reliefRenderers.Length; i++)
                            reliefRenderers[i].forceRenderingOff = reliefWasForcedOff[i] || frameIndex == 1;
                        for (int i = 0; i < bedRenderers.Length; i++)
                            bedRenderers[i].forceRenderingOff = bedWasForcedOff[i] || frameIndex == 2;
                        Assert.That(Vector3.Distance(eyes.transform.position, cameraPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, cameraRotation), Is.LessThan(.01f));
                        var frame = SchoolHorrorFrame(eyes, 1280, 720);
                        try
                        {
                            frames[frameIndex] = frame.GetPixels32();
                            File.WriteAllBytes(Path.Combine(output, names[pose] + "-" + labels[frameIndex] + ".png"), frame.EncodeToPNG());
                        }
                        finally { Object.Destroy(frame); }
                    }
                    int[] changed = new int[3]; int maxRestore = 0;
                    for (int i = 0; i < frames[0].Length; i++)
                    for (int k = 1; k < frames.Length; k++)
                    {
                        int difference = Mathf.Max(Mathf.Abs(frames[0][i].r - frames[k][i].r),
                            Mathf.Abs(frames[0][i].g - frames[k][i].g), Mathf.Abs(frames[0][i].b - frames[k][i].b));
                        if (difference >= 3) changed[k - 1]++;
                        if (k == 3 && difference > maxRestore) maxRestore = difference;
                    }
                    report.AppendLine(names[pose] + ": relief-off/bed-off/restored pixels with channel change >=3 = " +
                        changed[0] + "/" + changed[1] + "/" + changed[2] + "; restored maximum channel difference = " + maxRestore);
                    Assert.That(changed[2], Is.LessThanOrEqualTo(8), "Restored frame is not stable enough for attribution");
                }
                File.WriteAllText(Path.Combine(output, "layer-contribution.txt"), report.ToString());
            }
            finally
            {
                for (int i = 0; i < reliefRenderers.Length; i++)
                    if (reliefRenderers[i]) reliefRenderers[i].forceRenderingOff = reliefWasForcedOff[i];
                for (int i = 0; i < bedRenderers.Length; i++)
                    if (bedRenderers[i]) bedRenderers[i].forceRenderingOff = bedWasForcedOff[i];
                if (flashlight) flashlight.enabled = oldFlashlight;
                PlacePlayer(oldPlayerPosition); player.transform.rotation = oldPlayerRotation;
                eyes.transform.localPosition = oldEyeLocalPosition; eyes.transform.rotation = oldEyeRotation;
                eyes.fieldOfView = oldFieldOfView; eyes.aspect = oldAspect;
                motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
            }
        }
    }
}
