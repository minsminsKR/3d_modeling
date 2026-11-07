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
        [Serializable] sealed class AltarVaporFrameEvidence
        {
            public string cameraPose;
            public float simulationSeconds, candidateSimulationSeconds;
            public int width, height, changedAtLeast2, changedAtLeast4, changedAtLeast8, maximumChannelDifference;
            public int candidateChangedAtLeast2, candidateChangedAtLeast4, candidateChangedAtLeast8, candidateMaximumChannelDifference;
            public int productionVsCandidateAtLeast2, productionVsCandidateAtLeast4, productionVsCandidateAtLeast8;
            public double totalAbsoluteChannelDifference, candidateAbsoluteChannelDifference, productionVsCandidateAbsoluteDifference;
            public float pausedBefore, pausedAfter, resumedAfter, candidatePausedBefore, candidatePausedAfter, candidateResumedAfter;
        }

        // Explicitly opted-in: production world and eye camera, offscreen only.
        // No UI, OS input or persisted gameplay save is touched. Production and
        // test-only reference are captured in the same frame at the same pose.
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleAltarVaporProductionCameraBaseline()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-altar-vapor-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-v2-audit-no-desktop-interaction") && args.Contains("-noaudio"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            if (Get<bool>(shell, "ReducedMotion")) Call(shell, "ToggleReducedMotion");
            var world = Get<Component>(session, "Prologue");
            var source = world.transform.Find("Altar spectral vapor");
            Assert.That(source, Is.Not.Null);
            var vapor = source.GetComponent(RequireType("TempleAltarVapor"));
            var renderer = source.GetComponent<MeshRenderer>();
            Assert.That(vapor, Is.Not.Null); Assert.That(renderer, Is.Not.Null);
            Assert.That(source.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(1), "Production must own one renderer only");
            Assert.That(source.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1), "Production must own one mesh only");
            var eyes = Get<Camera>(player, "eyes");
            PlacePlayer(new Vector3(500f, .04f, 503.5f)); yield return null;
            var motor = (Behaviour)player; bool wasEnabled = motor.enabled;
            var priorRotation = eyes.transform.rotation;
            bool oldForceOff = renderer.forceRenderingOff;
            GameObject candidateObject = null;
            try
            {
                motor.enabled = false;
                candidateObject = new GameObject("Test-only altar vapor candidate");
                candidateObject.transform.SetParent(world.transform, false);
                var candidate = candidateObject.AddComponent<TempleAltarVaporCandidateProbe>();
                candidate.Prepare(eyes, world, session);
                var candidateRenderers = candidateObject.GetComponentsInChildren<MeshRenderer>(true);
                var candidateFilters = candidateObject.GetComponentsInChildren<MeshFilter>(true);
                var candidateVisible = candidate.VisibleRenderer;
                Assert.That(candidateRenderers.Length, Is.EqualTo(4)); Assert.That(candidateFilters.Length, Is.EqualTo(4));
                Assert.That(candidateVisible, Is.Not.Null);
                Assert.That(candidateRenderers.Count(r => r != candidateVisible && r.forceRenderingOff), Is.EqualTo(3));
                var candidateMeshes = candidateFilters.Select(f => f.sharedMesh).ToArray();
                var candidateMaterials = candidateRenderers.Select(r => r.sharedMaterial).ToArray();
                candidateVisible.forceRenderingOff = true;
                yield return Wait(() => Get<float>(vapor, "SimulationTime") >= 4f && candidate.SimulationTime >= 4f,
                    25, "Production and reference vapor must both mature for four simulation seconds");
                Assert.That(Get<int>(vapor, "SampleCount"), Is.GreaterThan(3));
                Assert.That(candidateMeshes.All(m => m && m.vertexCount > 0), Is.True);
                Assert.That(candidateMaterials.All(m => m), Is.True);
                var productionMesh = source.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(productionMesh && productionMesh.vertexCount > 0, Is.True);

                var points = new[] { new Vector3(500f, .04f, 503.5f), new Vector3(500f, .04f, 505.2f),
                    new Vector3(498.6f, .04f, 505.2f) };
                string[] names = { "approach", "altar-front", "altar-oblique" };
                var frames = new System.Collections.Generic.List<AltarVaporFrameEvidence>();
                for (int i = 0; i < points.Length; i++)
                {
                    PlacePlayer(points[i]);
                    eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.15f, 507.2f) - eyes.transform.position);
                    Physics.SyncTransforms(); yield return null; yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.15f, 507.2f) - eyes.transform.position);
                    Texture2D productionOn = null, candidateOn = null, bothOff = null;
                    try
                    {
                        renderer.forceRenderingOff = false; candidateVisible.forceRenderingOff = true;
                        productionOn = SchoolHorrorFrame(eyes, 1280, 720);
                        renderer.forceRenderingOff = true; candidateVisible.forceRenderingOff = false;
                        candidateOn = SchoolHorrorFrame(eyes, 1280, 720);
                        candidateVisible.forceRenderingOff = true;
                        bothOff = SchoolHorrorFrame(eyes, 1280, 720);
                        File.WriteAllBytes(Path.Combine(output, names[i] + "-vapor-on.png"), productionOn.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(output, "candidate-" + names[i] + "-vapor-on.png"), candidateOn.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(output, names[i] + "-vapor-off.png"), bothOff.EncodeToPNG());
                        var a = productionOn.GetPixels32(); var b = candidateOn.GetPixels32(); var off = bothOff.GetPixels32();
                        var evidence = new AltarVaporFrameEvidence { cameraPose = names[i], width = productionOn.width,
                            height = productionOn.height, simulationSeconds = Get<float>(vapor, "SimulationTime"),
                            candidateSimulationSeconds = candidate.SimulationTime };
                        for (int j = 0; j < a.Length; j++)
                        {
                            int pr = Math.Abs(a[j].r - off[j].r), pg = Math.Abs(a[j].g - off[j].g), pb = Math.Abs(a[j].b - off[j].b);
                            int cr = Math.Abs(b[j].r - off[j].r), cg = Math.Abs(b[j].g - off[j].g), cb = Math.Abs(b[j].b - off[j].b);
                            int dr = Math.Abs(a[j].r - b[j].r), dg = Math.Abs(a[j].g - b[j].g), db = Math.Abs(a[j].b - b[j].b);
                            int pm = Math.Max(pr, Math.Max(pg, pb)), cm = Math.Max(cr, Math.Max(cg, cb)), dm = Math.Max(dr, Math.Max(dg, db));
                            if (pm >= 2) evidence.changedAtLeast2++; if (pm >= 4) evidence.changedAtLeast4++; if (pm >= 8) evidence.changedAtLeast8++;
                            if (cm >= 2) evidence.candidateChangedAtLeast2++; if (cm >= 4) evidence.candidateChangedAtLeast4++; if (cm >= 8) evidence.candidateChangedAtLeast8++;
                            if (dm >= 2) evidence.productionVsCandidateAtLeast2++; if (dm >= 4) evidence.productionVsCandidateAtLeast4++; if (dm >= 8) evidence.productionVsCandidateAtLeast8++;
                            evidence.maximumChannelDifference = Math.Max(evidence.maximumChannelDifference, pm);
                            evidence.candidateMaximumChannelDifference = Math.Max(evidence.candidateMaximumChannelDifference, cm);
                            evidence.totalAbsoluteChannelDifference += pr + pg + pb;
                            evidence.candidateAbsoluteChannelDifference += cr + cg + cb;
                            evidence.productionVsCandidateAbsoluteDifference += dr + dg + db;
                        }
                        frames.Add(evidence);
                    }
                    finally
                    {
                        renderer.forceRenderingOff = oldForceOff;
                        if (candidateVisible) candidateVisible.forceRenderingOff = true;
                        if (productionOn) Object.Destroy(productionOn); if (candidateOn) Object.Destroy(candidateOn); if (bothOff) Object.Destroy(bothOff);
                    }
                }

                Call(shell, "Pause");
                float pausedBefore = Get<float>(vapor, "SimulationTime"), candidatePausedBefore = candidate.SimulationTime;
                yield return Delay(.3f);
                float pausedAfter = Get<float>(vapor, "SimulationTime"), candidatePausedAfter = candidate.SimulationTime;
                Assert.That(pausedAfter, Is.EqualTo(pausedBefore).Within(.0001f), "Pause must hold production vapor");
                Assert.That(candidatePausedAfter, Is.EqualTo(candidatePausedBefore).Within(.0001f), "Pause must hold candidate vapor");
                Call(shell, "Resume");
                yield return Wait(() => Get<float>(vapor, "SimulationTime") > pausedAfter + .05f && candidate.SimulationTime > candidatePausedAfter + .05f,
                    5, "Both vapors must resume");
                float resumed = Get<float>(vapor, "SimulationTime"), candidateResumedAfter = candidate.SimulationTime;
                Call(shell, "ToggleReducedMotion"); yield return null;
                float comfortTime = Get<float>(vapor, "SimulationTime"), candidateComfortTime = candidate.SimulationTime;
                yield return Delay(.2f);
                Assert.That(Get<float>(vapor, "SimulationTime"), Is.EqualTo(comfortTime).Within(.0001f), "Reduced motion must hold production vapor");
                Assert.That(candidate.SimulationTime, Is.EqualTo(candidateComfortTime).Within(.0001f), "Reduced motion must hold candidate vapor");
                Call(shell, "ToggleReducedMotion");
                Object.Destroy(candidateObject); candidateObject = null;
                yield return null; yield return null;
                Assert.That(candidateMeshes.All(m => !m), Is.True, "Candidate meshes must be destroyed");
                Assert.That(candidateMaterials.All(m => !m), Is.True, "Candidate materials must be destroyed");
                foreach (var entry in frames)
                {
                    entry.pausedBefore = pausedBefore; entry.pausedAfter = pausedAfter; entry.resumedAfter = resumed;
                    entry.candidatePausedBefore = candidatePausedBefore; entry.candidatePausedAfter = candidatePausedAfter;
                    entry.candidateResumedAfter = candidateResumedAfter;
                    File.WriteAllText(Path.Combine(output, entry.cameraPose + "-metrics.json"), JsonUtility.ToJson(entry, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(entry));
                }
                // Pixel deltas remain evidence, not a guessed visual-quality or FPS threshold.
            }
            finally
            {
                if (candidateObject) Object.Destroy(candidateObject);
                if (renderer) renderer.forceRenderingOff = oldForceOff;
                if (eyes) eyes.transform.rotation = priorRotation;
                if (motor) motor.enabled = wasEnabled;
            }
        }
    }
}
