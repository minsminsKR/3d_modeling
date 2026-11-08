// Opt-in offscreen cost experiment. Add beside TempleIndividualStoneReviewTests.cs
// and TempleAltarSoilCostReviewTests.cs only after the current review has exited.
// This file depends on those partial-class fixtures and their helper methods.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class IndividualStoneCostReport
        {
            public string scope = "Frozen actual prologue eye camera in isolated batch Editor; A/B/B/A compares existing raised path plus its recessed bed (A) against candidate stones with both old relief and bed hidden (B). Identical position, time, light, quality and output target. Each submit/readback sample includes a synchronous one-pixel GPU-completion read; it is neither pure GPU time nor displayed or player FPS. FrameTimingManager timestamps are delayed global Editor-frame samples and are not independently attributable to the offscreen request. Automatic cameras and other activity may add work. Rendering profiler counters, when present, describe entire Unity frames including other cameras/shadows, not isolated stone draw calls. A counter lacking positive samples in the two states is unverified/unavailable, not zero. This test is a controlled diagnostic, not a player-performance certification.";
            public string unityVersion, graphicsDevice, graphicsApi, candidateSelection, candidateNote, cameraPose;
            public int width, height, warmupFrames, measuredFrames, requestedStones, actualStones;
            public bool underfilled, flashlightEnabled, paused, candidateRetainsBed, frameTimingEnabled, noDesktopInteraction;
            public float fieldOfView, topAboveSoilMetres;
            public long potentialLod0Triangles, potentialLod1Triangles, potentialLod2Triangles;
            public Vector3 eyePosition;
            public Quaternion eyeRotation;
            public TempleCostBlock[] blocks;
            public string[] renderingCounterNames;
            public IndividualStoneCounterBlock[] counterBlocks;
            public string counterStatus = "global delayed frame counters; inspect each counter's status and samples";
        }

        [Serializable] sealed class IndividualStoneCounterBlock
        {
            public string state, name, unit, status;
            public int sequenceIndex, sampleCount, positiveSamples;
            public long[] frameValues;
        }

        // Run this profile with a fresh -v2-individual-stone-cost-output directory,
        // plus -batchmode -noaudio -v2-audit-no-desktop-interaction. The existing
        // feature runner supplies batch mode; don't duplicate its managed flag.
        [UnityTest, Timeout(480000)]
        public IEnumerator TempleIndividualStoneOffscreenCostAbbaReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-individual-stone-cost-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
            Directory.CreateDirectory(output);

            int requested = 240;
            int densityOption = Array.IndexOf(args, "-v2-individual-stone-cost-density");
            if (densityOption >= 0)
            {
                Assert.That(densityOption + 1, Is.LessThan(args.Length));
                Assert.That(int.TryParse(args[densityOption + 1], out requested), Is.True);
                Assert.That(new[] { 140, 240, 320 }, Does.Contain(requested));
            }
            int heightMm = 15;
            int heightOption = Array.IndexOf(args, "-v2-individual-stone-cost-height-mm");
            if (heightOption >= 0)
            {
                Assert.That(heightOption + 1, Is.LessThan(args.Length));
                Assert.That(int.TryParse(args[heightOption + 1], out heightMm), Is.True);
                Assert.That(new[] { 8, 15, 22 }, Does.Contain(heightMm));
            }
            float top = heightMm / 1000f;
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue").transform;
            IsolateLegacyStoneReview(world);
            var approach = world.Find("Stone approach"); var ground = world.Find("Forest ground");
            var relief = world.Find("Authored forest stone approach"); var bed = world.Find("Recessed forest stone bed");
            Assert.That(approach && ground && relief && bed, Is.True);
            var oldRenderers = relief.GetComponentsInChildren<Renderer>(true);
            var bedRenderers = bed.GetComponentsInChildren<Renderer>(true);
            Assert.That(oldRenderers.Length, Is.GreaterThan(0)); Assert.That(bedRenderers.Length, Is.GreaterThan(0));
            bool[] originalOff = oldRenderers.Select(r => r.forceRenderingOff).ToArray();
            bool[] originalBedOff = bedRenderers.Select(r => r.forceRenderingOff).ToArray();
            Assert.That(originalOff.Any(x => x), Is.False, "Original relief must begin visible");
            Assert.That(bedRenderers.Any(r => r.forceRenderingOff), Is.False, "Bed must remain visible in both states");
            var prefabs = IndividualStoneKeys.Select(k => Resources.Load<GameObject>(IndividualStoneRoot + k + "/visual")).ToArray();
            Assert.That(prefabs.All(p => p), Is.True, "Import both candidate prefabs before running");
            var bounds = prefabs.Select(IndividualStoneBounds).ToArray();
            var costs = new long[2, 3];
            for (int model = 0; model < prefabs.Length; model++)
            {
                var group = prefabs[model].GetComponent<LODGroup>();
                Assert.That(group && group.lodCount == 3, Is.True);
                var levels = group.GetLODs();
                for (int level = 0; level < levels.Length; level++) foreach (var renderer in levels[level].renderers)
                {
                    var filter = renderer ? renderer.GetComponent<MeshFilter>() : null;
                    Assert.That(filter && filter.sharedMesh, Is.True);
                    var mesh = filter.sharedMesh;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        Assert.That(mesh.GetTopology(sub), Is.EqualTo(MeshTopology.Triangles));
                        costs[model, level] += (long)mesh.GetIndexCount(sub) / 3;
                    }
                }
            }
            float xmin = approach.localPosition.x - approach.localScale.x * .5f, xmax = approach.localPosition.x + approach.localScale.x * .5f;
            float zmin = approach.localPosition.z - approach.localScale.z * .5f, zmax = approach.localPosition.z + approach.localScale.z * .5f;
            float soil = ground.localPosition.y + ground.localScale.y * .5f;
            var path = new TempleStoneLayoutRules.Rectangle(xmin, zmin, xmax, zmax);
            var footprints = new[] {
                new TempleStoneLayoutRules.Footprint(bounds[0].extents.x, bounds[0].extents.z, .52, .65),
                new TempleStoneLayoutRules.Footprint(bounds[1].extents.x, bounds[1].extents.z, .50, .72) };
            var layout = TempleStoneLayoutRules.Generate(path, footprints, null, 43093, .025, .012, .18, requested, 40000);
            Assert.That(layout.Count, Is.GreaterThan(50));
            var totals = new long[3];
            GameObject candidate = null; RenderTexture target = null; Texture2D pixel = null;
            var eyes = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight"); var motor = (Behaviour)player;
            Assert.That(eyes && torch, Is.True);
            Vector3 oldPosition = player.transform.position, oldEyeLocal = eyes.transform.localPosition;
            Quaternion oldRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldTorch = torch.enabled, oldMotor = motor.enabled, paused = false;
            var oldTarget = eyes.targetTexture; var oldActive = RenderTexture.active;
            int oldFrameRate = Application.targetFrameRate, oldVsync = QualitySettings.vSyncCount;
            var blocks = new TempleCostBlock[4];
            var counterBlocks = new List<IndividualStoneCounterBlock>();
            var report = new IndividualStoneCostReport {
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), width = 1920, height = 1080,
                warmupFrames = 20, measuredFrames = 60, requestedStones = requested, actualStones = layout.Count,
                underfilled = layout.Underfilled, candidateRetainsBed = false, topAboveSoilMetres = top, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(),
                noDesktopInteraction = true, candidateSelection = "seed 43093; same footprint rules and setup as TempleIndividualStoneOffscreenReview",
                candidateNote = "B hides both raised relief and recessed original bed. Fallback LOD2 may equal LOD1; no assumption of hardware batching. Baseline relief and bed remain unchanged in A.",
                cameraPose = "front: feet (0,.04,3.5), target (0,1.9,9) in temple coordinates", flashlightEnabled = false
            };
            try
            {
                candidate = IndividualStoneCreate(world, prefabs, bounds, layout, soil, top, path, totals, costs);
                candidate.SetActive(false);
                report.potentialLod0Triangles = totals[0]; report.potentialLod1Triangles = totals[1]; report.potentialLod2Triangles = totals[2];
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = 1920f / 1080;
                Call(shell, "Pause"); paused = true; yield return null; Assert.That(Time.timeScale, Is.Zero);
                PlacePlayer(world.TransformPoint(new Vector3(0, .04f, 3.5f))); eyes.transform.localPosition = oldEyeLocal;
                eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(new Vector3(0, 1.9f, 9)) - eyes.transform.position, world.up);
                torch.enabled = false; Physics.SyncTransforms(); yield return null;
                Vector3 fixedPosition = eyes.transform.position; Quaternion fixedRotation = eyes.transform.rotation;
                report.eyePosition = fixedPosition; report.eyeRotation = fixedRotation; report.fieldOfView = eyes.fieldOfView; report.paused = true;
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = 300;
                var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                target = new RenderTexture(1920, 1080, 24, format, RenderTextureReadWrite.Linear);
                Assert.That(target.Create(), Is.True); pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
                eyes.targetTexture = target; eyes.aspect = 1920f / 1080;
                // Global counters can include automatic camera renders, shadows and
                // previous-frame work. Never treat them as this camera's draw calls.
                var handles = new System.Collections.Generic.List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                report.renderingCounterNames = handles.Select(h => ProfilerRecorderHandle.GetDescription(h))
                    .Where(d => d.Category.Equals(ProfilerCategory.Render))
                    .Select(d => d.Name).Distinct().OrderBy(n => n).ToArray();
                string[] requestedCounters = { "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count" };
                var descriptions = handles.Select(h => ProfilerRecorderHandle.GetDescription(h))
                    .Where(d => requestedCounters.Contains(d.Name, StringComparer.OrdinalIgnoreCase))
                    .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
                bool[] abba = { false, true, true, false };
                for (int i = 0; i < abba.Length; i++)
                {
                    bool candidateOn = abba[i];
                    for (int j = 0; j < oldRenderers.Length; j++) oldRenderers[j].forceRenderingOff = candidateOn || originalOff[j];
                    for (int j = 0; j < bedRenderers.Length; j++) bedRenderers[j].forceRenderingOff = candidateOn || originalBedOff[j];
                    candidate.SetActive(candidateOn); Physics.SyncTransforms(); yield return null;
                    Assert.That(Vector3.Distance(eyes.transform.position, fixedPosition), Is.LessThan(.001f));
                    Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedRotation), Is.LessThan(.01f));
                    Assert.That(bedRenderers.All(r => r.forceRenderingOff == candidateOn), Is.True);
                    var sample = new TempleCostBlock {
                        comparison = "existing-relief-and-bed-vs-individual-stones", state = candidateOn ? "B" : "A", sequenceIndex = i,
                        stateMeaning = candidateOn ? "candidate stones, original relief and bed hidden" : "original relief and bed",
                        warmupFrames = 20, measuredFrames = 60
                    };
                    var recorders = new List<ProfilerRecorder>();
                    var series = new List<List<long>>();
                    try
                    {
                        foreach (var description in descriptions)
                        {
                            ProfilerRecorder recorder;
                            try { recorder = ProfilerRecorder.StartNew(description.Category, description.Name, 1); }
                            catch { recorder = default; }
                            recorders.Add(recorder); series.Add(new List<long>());
                        }
                        var routine = MeasureTempleCostBlock(eyes, target, pixel, sample);
                        try
                        {
                            int sampleFrame = -sample.warmupFrames;
                            while (routine.MoveNext())
                            {
                                if (sampleFrame >= 0)
                                    for (int n = 0; n < recorders.Count; n++)
                                        if (recorders[n].Valid && recorders[n].IsRunning) series[n].Add(recorders[n].LastValue);
                                sampleFrame++;
                                yield return routine.Current;
                            }
                        }
                        finally { (routine as IDisposable)?.Dispose(); }
                        for (int n = 0; n < descriptions.Length; n++)
                        {
                            var values = series[n].ToArray(); int positive = values.Count(v => v > 0);
                            counterBlocks.Add(new IndividualStoneCounterBlock {
                                state = sample.state, sequenceIndex = i, name = descriptions[n].Name,
                                unit = descriptions[n].UnitType.ToString(), sampleCount = values.Length, positiveSamples = positive,
                                status = values.Length > 0 && positive > 0 ? "available-global-frame-counter" : "unavailable-or-all-zero-unverified",
                                frameValues = values
                            });
                        }
                        foreach (string missing in requestedCounters.Where(n => !descriptions.Any(d => string.Equals(d.Name, n, StringComparison.OrdinalIgnoreCase))))
                            counterBlocks.Add(new IndividualStoneCounterBlock { state = sample.state, sequenceIndex = i,
                                name = missing, unit = "unknown", status = "unavailable-not-exposed", frameValues = Array.Empty<long>() });
                    }
                    finally { foreach (var recorder in recorders) recorder.Dispose(); }
                    blocks[i] = sample;
                }
                report.blocks = blocks;
                report.counterBlocks = counterBlocks.ToArray();
                File.WriteAllText(Path.Combine(output, "individual-stone-cost-abba.json"), JsonUtility.ToJson(report, true));
            }
            finally
            {
                if (candidate) Object.DestroyImmediate(candidate);
                for (int i = 0; i < oldRenderers.Length; i++) if (oldRenderers[i]) oldRenderers[i].forceRenderingOff = originalOff[i];
                for (int i = 0; i < bedRenderers.Length; i++) if (bedRenderers[i]) bedRenderers[i].forceRenderingOff = originalBedOff[i];
                if (torch) torch.enabled = oldTorch;
                PlacePlayer(oldPosition); player.transform.rotation = oldRotation; eyes.transform.localPosition = oldEyeLocal;
                eyes.transform.rotation = oldEyeRotation; eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; eyes.targetTexture = oldTarget;
                RenderTexture.active = oldActive; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
                Application.targetFrameRate = oldFrameRate; QualitySettings.vSyncCount = oldVsync;
                if (pixel) Object.Destroy(pixel);
                if (target) { target.Release(); Object.Destroy(target); }
                Physics.SyncTransforms();
            }
        }
    }
}
