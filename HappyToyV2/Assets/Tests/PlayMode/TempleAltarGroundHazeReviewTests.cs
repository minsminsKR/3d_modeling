using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class GroundHazeEvidence
        {
            public string pose, timingCaveat;
            public int width, height, changed2, changed4, changed8, topHalfChanged4, bottomHalfChanged4, maxDelta;
            public int minX, minY, maxX, maxY;
            public float pauseBefore, pauseAfter, resumeAfter, reducedBefore, reducedAfter;
            public double baselineCpuCaptureMs, candidateCpuCaptureMs;
            public int vertices, triangles;
        }

        // Copy the two candidate files to the existing PlayMode tests only after
        // the current Unity run exits. This test does nothing without the flag.
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleAltarGroundHazeOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-altar-ground-haze-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False);
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            bool initialReduced = Get<bool>(shell, "ReducedMotion");
            if (initialReduced) Call(shell, "ToggleReducedMotion");
            var world = Get<Component>(session, "Prologue");
            var incense = world.transform.Find("Altar spectral vapor");
            Assert.That(incense, Is.Not.Null);
            var incenseRenderer = incense.GetComponent<MeshRenderer>();
            var production = incense.GetComponent(RequireType("TempleAltarVapor"));
            var eye = Get<Camera>(player, "eyes"); Assert.That(eye.GetUniversalAdditionalCameraData().requiresDepthTexture, Is.True);
            var flashlight = Get<Light>(player, "flashlight");
            var motor = (Behaviour)player;
            bool motorEnabled = motor.enabled, originalFlashlight = flashlight.enabled;
            bool oldIncenseOff = incenseRenderer.forceRenderingOff;
            var oldPosition = player.transform.position;
            var oldPlayerRotation = player.transform.rotation;
            var oldEyeRotation = eye.transform.rotation;
            float oldFov = eye.fieldOfView, oldAspect = eye.aspect;
            GameObject testObject = null;
            bool paused = false, reducedChanged = initialReduced;
            try
            {
                motor.enabled = false;
                eye.fieldOfView = 72f; eye.aspect = 1280f / 720f;
                testObject = new GameObject("Test-only low altar haze"); testObject.layer = 8;
                testObject.transform.SetParent(world.transform, false);
                var haze = testObject.AddComponent<TempleAltarGroundHazeProbe>();
                haze.Prepare(eye, world, session, shell, args.Contains("-v2-altar-haze-variant2"));
                var renderer = haze.Surface;
                var mesh = testObject.GetComponent<MeshFilter>().sharedMesh;
                var material = renderer.sharedMaterial;
                Assert.That(mesh.vertexCount, Is.EqualTo(216));
                Assert.That(mesh.triangles.Length / 3, Is.EqualTo(144));
                Assert.That(incenseRenderer.forceRenderingOff, Is.EqualTo(oldIncenseOff));
                PlacePlayer(new Vector3(500f, .04f, 503.5f));
                yield return Wait(() => haze.SimulationTime > .5f, 10, "Haze must start");

                Call(shell, "Pause"); paused = true;
                float before = haze.SimulationTime, productionBefore = Get<float>(production, "SimulationTime");
                yield return Delay(.3f);
                float after = haze.SimulationTime;
                Assert.That(after, Is.EqualTo(before).Within(.0001f));
                Assert.That(Get<float>(production, "SimulationTime"), Is.EqualTo(productionBefore).Within(.0001f));
                Call(shell, "Resume"); paused = false;
                yield return Wait(() => haze.SimulationTime > after + .05f, 5, "Haze must resume");
                float resumed = haze.SimulationTime;
                Call(shell, "ToggleReducedMotion"); reducedChanged = !reducedChanged; yield return null;
                float rb = haze.SimulationTime; yield return Delay(.2f);
                float ra = haze.SimulationTime;
                Assert.That(ra, Is.EqualTo(rb).Within(.0001f));
                Call(shell, "ToggleReducedMotion"); reducedChanged = !reducedChanged;
                Call(shell, "Pause"); paused = true; yield return null; Assert.That(Time.timeScale, Is.Zero);

                var positions = new[] { new Vector3(500f,.04f,503.5f), new Vector3(500f,.04f,505.2f),
                    new Vector3(498.6f,.04f,505.2f), new Vector3(500f,.04f,505.2f) };
                var names = new[] { "approach", "front", "oblique", "flashlight" };
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(positions[pose]);
                    eye.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.0f, 507.2f) - eye.transform.position);
                    flashlight.enabled = pose == 3;
                    Physics.SyncTransforms(); yield return null;
                    eye.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.0f, 507.2f) - eye.transform.position);
                    var images = new Texture2D[4];
                    var values = new[] { true, false, false, true };
                    var labels = new[] { "a-off", "b-on", "b-on-repeat", "a-off-restored" };
                    double offMs = 0, onMs = 0;
                    try
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            renderer.forceRenderingOff = values[i];
                            var clock = Stopwatch.StartNew();
                            images[i] = SchoolHorrorFrame(eye, 1280, 720);
                            clock.Stop();
                            if (values[i]) offMs += clock.Elapsed.TotalMilliseconds;
                            else onMs += clock.Elapsed.TotalMilliseconds;
                            File.WriteAllBytes(Path.Combine(output, names[pose] + "-" + labels[i] + ".png"), images[i].EncodeToPNG());
                        }
                        var a = images[0].GetPixels32(); var b = images[1].GetPixels32();
                        var repeatB = images[2].GetPixels32(); var repeatA = images[3].GetPixels32();
                        var e = new GroundHazeEvidence { pose = names[pose], width = 1280, height = 720,
                            minX = 1280, minY = 720, maxX = -1, maxY = -1, pauseBefore = before, pauseAfter = after,
                            resumeAfter = resumed, reducedBefore = rb, reducedAfter = ra, vertices = mesh.vertexCount,
                            triangles = mesh.triangles.Length / 3, baselineCpuCaptureMs = offMs / 2, candidateCpuCaptureMs = onMs / 2,
                            timingCaveat = "Two offscreen render/readback CPU wall times each; includes synchronization and test overhead, not GPU frame cost or displayed FPS." };
                        int repeatError = 0;
                        for (int j = 0; j < a.Length; j++)
                        {
                            int d = Delta(a[j], b[j]); repeatError = Math.Max(repeatError, Math.Max(Delta(a[j], repeatA[j]), Delta(b[j], repeatB[j])));
                            if (d >= 2) e.changed2++;
                            if (d >= 4)
                            {
                                e.changed4++; int x = j % 1280, y = j / 1280;
                                e.minX = Math.Min(e.minX,x); e.maxX = Math.Max(e.maxX,x);
                                e.minY = Math.Min(e.minY,y); e.maxY = Math.Max(e.maxY,y);
                                // Texture2D rows start at the bottom.
                                if (y < 360) e.bottomHalfChanged4++; else e.topHalfChanged4++;
                            }
                            if (d >= 8) e.changed8++;
                            e.maxDelta = Math.Max(e.maxDelta, d);
                        }
                        Assert.That(repeatError, Is.LessThanOrEqualTo(1), "Frozen ABBA must be stable");
                        File.WriteAllText(Path.Combine(output, names[pose] + "-metrics.json"), JsonUtility.ToJson(e, true));
                    }
                    finally { foreach (var frame in images) if (frame) Object.Destroy(frame); }
                }
                Object.Destroy(testObject); testObject = null;
                yield return null; yield return null;
                Assert.That(!mesh && !material, Is.True, "Probe resources must be destroyed");
                Assert.That(incenseRenderer && incenseRenderer.forceRenderingOff == oldIncenseOff, Is.True);
            }
            finally
            {
                if (testObject) Object.Destroy(testObject);
                if (paused) Call(shell, "Resume");
                if (reducedChanged) Call(shell, "ToggleReducedMotion");
                if (incenseRenderer) incenseRenderer.forceRenderingOff = oldIncenseOff;
                if (flashlight) flashlight.enabled = originalFlashlight;
                if (eye) { eye.transform.rotation = oldEyeRotation; eye.fieldOfView = oldFov; eye.aspect = oldAspect; }
                if (player) { player.transform.position = oldPosition; player.transform.rotation = oldPlayerRotation; }
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static int Delta(Color32 a, Color32 b)
        {
            return Math.Max(Math.Abs(a.r-b.r), Math.Max(Math.Abs(a.g-b.g), Math.Abs(a.b-b.b)));
        }
    }
}

