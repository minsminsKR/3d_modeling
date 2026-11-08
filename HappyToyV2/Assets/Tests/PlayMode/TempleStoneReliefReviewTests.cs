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
        // This explicit review changes only the existing visual holders. No
        // floor, player, collider, source mesh, or production code is changed.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleStoneReliefOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-temple-stone-relief-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length), "Review output is required");
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True,
                "Batch mode, muted output and desktop isolation are required");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a new output directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            IsolateLegacyStoneReview(world.transform);
            var path = world.transform.Find("Authored forest stone approach");
            Assert.That(path, Is.Not.Null);
            var holders = path.Cast<Transform>().ToArray();
            Assert.That(holders.Length, Is.EqualTo(25));
            Assert.That(path.GetComponentsInChildren<Collider>(true), Is.Empty);
            var positions = holders.Select(t => t.localPosition).ToArray();
            var scales = holders.Select(t => t.localScale).ToArray();
            var floors = holders.Select(t => t.GetComponentsInChildren<Renderer>(true).Min(r => r.bounds.min.y)).ToArray();
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
                var factors = new[] { 1f, 2f, 3f, 1f };
                var labels = new[] { "baseline-003", "candidate-006", "candidate-009", "baseline-restored" };
                var measured = new StringBuilder();
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(feet[pose]);
                    eyes.transform.localPosition = oldEyeLocalPosition;
                    if (pose == 1) eyes.transform.position -= Vector3.up * .7f;
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    flashlight.enabled = pose == 2;
                    Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(targets[pose] - eyes.transform.position);
                    if (pose == 2) Assert.That(flashlight.isActiveAndEnabled, Is.True);
                    Vector3 cameraPosition = eyes.transform.position;
                    Quaternion cameraRotation = eyes.transform.rotation;
                    for (int candidate = 0; candidate < factors.Length; candidate++)
                    {
                        // Scale about each holder's actual rendered bottom, not
                        // its prefab origin. The original floor and footstep
                        // footprint remain untouched and own all collision.
                        for (int i = 0; i < holders.Length; i++)
                        {
                            holders[i].localPosition = positions[i];
                            var s = scales[i]; s.y *= factors[candidate]; holders[i].localScale = s;
                            float currentBottom = holders[i].GetComponentsInChildren<Renderer>(true).Min(r => r.bounds.min.y);
                            Vector3 correction = path.InverseTransformVector(Vector3.up * (floors[i] - currentBottom));
                            holders[i].localPosition += correction;
                            float fixedBottom = holders[i].GetComponentsInChildren<Renderer>(true).Min(r => r.bounds.min.y);
                            Assert.That(fixedBottom, Is.EqualTo(floors[i]).Within(.001f));
                        }
                        Physics.SyncTransforms();
                        if (pose == 0)
                        {
                            var heights = holders.Select(holder => {
                                var renderers = holder.GetComponentsInChildren<Renderer>(true);
                                return renderers.Max(r => r.bounds.max.y) - renderers.Min(r => r.bounds.min.y);
                            }).ToArray();
                            measured.AppendLine(labels[candidate] + " rendered bounds height min/max: " +
                                heights.Min().ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "/" +
                                heights.Max().ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + " metres");
                        }
                        Assert.That(Vector3.Distance(eyes.transform.position, cameraPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, cameraRotation), Is.LessThan(.01f));
                        var frame = SchoolHorrorFrame(eyes, 1280, 720);
                        try { File.WriteAllBytes(Path.Combine(output, names[pose] + "-" + labels[candidate] + ".png"), frame.EncodeToPNG()); }
                        finally { Object.Destroy(frame); }
                    }
                }
                File.WriteAllText(Path.Combine(output, "review.txt"),
                    "Diagnostic only: factor 1/2/3 corresponds to the existing 0.03m relief fitted to 0.03/0.06/0.09m. " +
                    "Each visual holder retains its original bottom; collisions and footstep surfaces are unchanged. " +
                    "The named sizes are the requested original FitBox dimensions; actual retained-renderer bounds below can be smaller. " +
                    "Images are single offscreen frames, not proof of motion stability, GPU cost, player FPS, or subjective quality.\n" + measured);
            }
            finally
            {
                for (int i = 0; i < holders.Length; i++)
                    if (holders[i]) { holders[i].localPosition = positions[i]; holders[i].localScale = scales[i]; }
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
