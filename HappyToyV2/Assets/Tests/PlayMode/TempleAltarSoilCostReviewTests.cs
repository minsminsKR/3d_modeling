using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class TempleCostBlock
        {
            public string comparison, state, stateMeaning, gpuTimingStatus, cpuTimingStatus, cpuMainTimingStatus;
            public int sequenceIndex, warmupFrames, measuredFrames, uniqueFrameTimings, cpuSamples, cpuMainSamples, gpuSamples, gc0Collections;
            public bool vaporUpdateEnabled, vaporRendererEnabled;
            public double submitReadbackMedianMs, submitReadbackP95Ms, cpuFrameMedianMs, cpuFrameP95Ms,
                cpuMainMedianMs, cpuMainP95Ms, gpuFrameMedianMs, gpuFrameP95Ms;
            public double[] submitReadbackSamplesMs, cpuFrameSamplesMs, cpuMainSamplesMs, gpuFrameSamplesMs;
        }
        [Serializable] sealed class TempleCostReport
        {
            public string scope = "Isolated Unity batch Editor; production eye camera to a 1920x1080 offscreen target. A/B/B/A compares vapor render-only, vapor update+render, and legacy 1024 (A) versus current default 2048 (B) soil at fixed positions. Existing baselineSoil fields describe A/legacy and candidateSoil fields describe B/default. A command-line diagnostic can select legacy soil at scene startup; activeSoilAtStart records the actual material. CPU frame samples include unrelated Editor/test work; submit/readback times include synchronous 1px readback and are not display FPS or pure GPU time. GPU=0 or absent timing means unavailable, never zero cost. Automatic camera renders, if present, add work. Texture runtime bytes are Unity estimates, not measured GPU residency. This is not a native player or hardware-presented FPS measurement.";
            public string unityVersion, device, graphics, colorFormat, baselineSoil, candidateSoil;
            public string activeSoilAtStart, activeNormalAtStart;
            public bool activeSoilMatchesLegacy, activeSoilMatchesDefault;
            public int width, height, warmupFrames, measuredFrames, initialTargetFrameRate, initialVsync;
            public int baselineColorWidth, candidateColorWidth, baselineNormalWidth, candidateNormalWidth;
            public long baselineColorRuntimeBytes, candidateColorRuntimeBytes, baselineNormalRuntimeBytes, candidateNormalRuntimeBytes;
            public bool frameTimingEnabled, isolated, muted;
            public TempleCostBlock[] blocks;
        }
        static double TempleCostPercentile(double[] values, float p)
        {
            if (values == null || values.Length == 0) return -1;
            var copy = (double[])values.Clone(); Array.Sort(copy);
            return copy[Mathf.CeilToInt((copy.Length - 1) * p)];
        }

        // Opt-in diagnostic only: no desktop input, no saved-run mutation, no
        // visible player, and all temporary state is restored in finally.
        [UnityTest, Timeout(480000)]
        public IEnumerator TempleAltarSoilCostOffscreenAbbaReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-temple-cost-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            bool oldReducedMotion = Get<bool>(shell, "ReducedMotion");
            if (oldReducedMotion) Call(shell, "ToggleReducedMotion");
            var world = Get<Component>(session, "Prologue");
            var vaporNode = world.transform.Find("Altar spectral vapor");
            Assert.That(vaporNode, Is.Not.Null);
            var vapor = (Behaviour)vaporNode.GetComponent(RequireType("TempleAltarVapor"));
            var vaporRenderer = vaporNode.GetComponent<MeshRenderer>();
            var floor = world.transform.Find("Forest ground").GetComponent<Renderer>();
            var horizon = world.transform.Find("Forest horizon terrain").GetComponent<Renderer>();
            Assert.That(vapor && vaporRenderer && floor && horizon, Is.True);
            var originalFloor = floor.sharedMaterial; var originalHorizon = horizon.sharedMaterial;
            Assert.That(originalFloor && originalHorizon && originalFloor == originalHorizon, Is.True);
            var originalColor = Resources.Load<Texture2D>("TempleForest/forest-earth/forest-earth-albedo");
            var originalNormal = Resources.Load<Texture2D>("TempleForest/forest-earth/forest-earth-height");
            var candidateColor = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-albedo");
            var candidateNormal = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-height");
            Assert.That(originalColor && originalNormal && candidateColor && candidateNormal, Is.True, "Both authored soil variants are required");
            Assert.That(originalColor.width, Is.EqualTo(1024)); Assert.That(candidateColor.width, Is.EqualTo(2048));
            Assert.That(originalNormal.width, Is.EqualTo(1024)); Assert.That(candidateNormal.width, Is.EqualTo(2048));
            var baselineMaterial = new Material(originalFloor) { name = "Diagnostic legacy 1024 soil" };
            var candidateMaterial = new Material(originalFloor) { name = "Diagnostic current-default 2048 soil" };
            baselineMaterial.SetTexture("_BaseMap", originalColor); baselineMaterial.SetTexture("_BumpMap", originalNormal);
            baselineMaterial.SetTextureScale("_BaseMap", new Vector2(14, 18)); baselineMaterial.SetTextureScale("_BumpMap", new Vector2(14, 18));
            candidateMaterial.SetTexture("_BaseMap", candidateColor); candidateMaterial.SetTexture("_BumpMap", candidateNormal);
            candidateMaterial.SetTextureScale("_BaseMap", new Vector2(7, 9)); candidateMaterial.SetTextureScale("_BumpMap", new Vector2(7, 9));
            var activeColor = originalFloor.GetTexture("_BaseMap");
            var activeNormal = originalFloor.GetTexture("_BumpMap");
            Assert.That(activeColor && activeNormal, Is.True, "The actual initial soil material must retain both maps");
            var eyes = Get<Camera>(player, "eyes"); var motor = (Behaviour)player;
            bool oldMotor = motor.enabled, oldVaporEnabled = vapor.enabled, oldForceOff = vaporRenderer.forceRenderingOff,
                oldRendererEnabled = vaporRenderer.enabled;
            Vector3 oldPosition = player.transform.position; Quaternion oldRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            var oldTarget = eyes.targetTexture; var oldActive = RenderTexture.active; float oldAspect = eyes.aspect;
            int oldFrameRate = Application.targetFrameRate, oldVsync = QualitySettings.vSyncCount;
            var report = new TempleCostReport { unityVersion = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
                graphics = SystemInfo.graphicsDeviceType.ToString(), width = 1920, height = 1080, warmupFrames = 20, measuredFrames = 60,
                frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), isolated = true, muted = true,
                initialTargetFrameRate = oldFrameRate, initialVsync = oldVsync,
                baselineSoil = originalColor.name, candidateSoil = candidateColor.name,
                activeSoilAtStart = activeColor.name, activeNormalAtStart = activeNormal.name,
                activeSoilMatchesLegacy = activeColor == originalColor && activeNormal == originalNormal,
                activeSoilMatchesDefault = activeColor == candidateColor && activeNormal == candidateNormal,
                baselineColorWidth = originalColor.width, candidateColorWidth = candidateColor.width,
                baselineNormalWidth = originalNormal.width, candidateNormalWidth = candidateNormal.width,
                baselineColorRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(originalColor), candidateColorRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(candidateColor),
                baselineNormalRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(originalNormal), candidateNormalRuntimeBytes = Profiler.GetRuntimeMemorySizeLong(candidateNormal) };
            RenderTexture target = null; Texture2D pixel = null;
            var blocks = new List<TempleCostBlock>();
            try
            {
                motor.enabled = false; PlacePlayer(new Vector3(500f, .04f, 503.5f));
                eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500f, 1.15f, 507.2f) - eyes.transform.position);
                Physics.SyncTransforms(); yield return null;
                yield return Wait(() => Get<float>(vapor, "SimulationTime") >= 4f, 25, "Vapor must reach four seconds before cost review");
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = 300;
                var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
                report.colorFormat = format.ToString();
                target = new RenderTexture(1920, 1080, 24, format, RenderTextureReadWrite.Linear);
                Assert.That(target.Create(), Is.True, "Offscreen timing target unavailable");
                pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
                eyes.targetTexture = target; eyes.aspect = 1920f / 1080;
                string[] comparisons = { "vapor-render-only", "vapor-update-and-render", "soil-texture-1024-vs-2048" };
                bool[] abba = { false, true, true, false };
                for (int mode = 0; mode < comparisons.Length; mode++)
                {
                    if (mode == 2)
                    {
                        PlacePlayer(new Vector3(503.5f, .04f, 490f));
                        eyes.transform.rotation = Quaternion.Euler(38, 0, 0); Physics.SyncTransforms(); yield return null;
                    }
                    for (int block = 0; block < abba.Length; block++)
                    {
                        bool b = abba[block];
                        if (mode == 0) { vapor.enabled = true; vaporRenderer.forceRenderingOff = !b; }
                        else if (mode == 1) { vapor.enabled = b; vaporRenderer.forceRenderingOff = !b; }
                        else { vapor.enabled = true; vaporRenderer.forceRenderingOff = oldForceOff; floor.sharedMaterial = horizon.sharedMaterial = b ? candidateMaterial : baselineMaterial; }
                        var sample = new TempleCostBlock { comparison = comparisons[mode], state = b ? "B" : "A", sequenceIndex = block,
                            warmupFrames = report.warmupFrames, measuredFrames = report.measuredFrames,
                            stateMeaning = mode == 0 ? (b ? "update on, render on" : "update on, render off") :
                                mode == 1 ? (b ? "update on, render on" : "update off, render off") :
                                (b ? "2048 current default maps" : "1024 legacy comparison maps"),
                            vaporUpdateEnabled = vapor.enabled, vaporRendererEnabled = vaporRenderer.enabled && !vaporRenderer.forceRenderingOff };
                        yield return MeasureTempleCostBlock(eyes, target, pixel, sample);
                        blocks.Add(sample);
                    }
                }
                report.blocks = blocks.ToArray();
                File.WriteAllText(Path.Combine(output, "temple-cost-abba.json"), JsonUtility.ToJson(report, true));
                TestContext.Out.WriteLine("Wrote opt-in altar/soil timing report: " + Path.Combine(output, "temple-cost-abba.json"));
            }
            finally
            {
                if (floor) floor.sharedMaterial = originalFloor;
                if (horizon) horizon.sharedMaterial = originalHorizon;
                if (vaporRenderer) { vaporRenderer.forceRenderingOff = oldForceOff; vaporRenderer.enabled = oldRendererEnabled; }
                if (vapor) vapor.enabled = oldVaporEnabled;
                if (player) { PlacePlayer(oldPosition); player.transform.rotation = oldRotation; }
                if (eyes) { eyes.targetTexture = oldTarget; eyes.aspect = oldAspect; eyes.transform.rotation = oldEyeRotation; }
                RenderTexture.active = oldActive;
                if (motor) motor.enabled = oldMotor;
                if (shell && Get<bool>(shell, "ReducedMotion") != oldReducedMotion) Call(shell, "ToggleReducedMotion");
                Application.targetFrameRate = oldFrameRate; QualitySettings.vSyncCount = oldVsync;
                if (pixel) Object.Destroy(pixel);
                if (target) { target.Release(); Object.Destroy(target); }
                if (baselineMaterial) Object.Destroy(baselineMaterial);
                if (candidateMaterial) Object.Destroy(candidateMaterial);
            }
            Assert.That(floor.sharedMaterial, Is.SameAs(originalFloor));
            Assert.That(horizon.sharedMaterial, Is.SameAs(originalHorizon));
        }

        static IEnumerator MeasureTempleCostBlock(Camera eyes, RenderTexture target, Texture2D pixel, TempleCostBlock result)
        {
            var stopwatchFrequency = (double)Stopwatch.Frequency;
            var submit = new double[result.measuredFrames];
            var cpu = new List<double>(); var cpuMain = new List<double>(); var gpu = new List<double>();
            var timing = new FrameTiming[1]; ulong previousStamp = 0;
            int gc0 = GC.CollectionCount(0);
            for (int frame = -result.warmupFrames; frame < result.measuredFrames; frame++)
            {
                long begin = Stopwatch.GetTimestamp();
                RenderPipeline.SubmitRenderRequest(eyes, new RenderPipeline.StandardRequest { destination = target });
                var before = RenderTexture.active; RenderTexture.active = target;
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); pixel.Apply(); RenderTexture.active = before;
                double elapsed = (Stopwatch.GetTimestamp() - begin) * 1000.0 / stopwatchFrequency;
                FrameTimingManager.CaptureFrameTimings();
                if (frame >= 0)
                {
                    submit[frame] = elapsed;
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != 0 && timing[0].frameStartTimestamp != previousStamp)
                    {
                        previousStamp = timing[0].frameStartTimestamp; result.uniqueFrameTimings++;
                        if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                        if (timing[0].cpuMainThreadFrameTime > 0) cpuMain.Add(timing[0].cpuMainThreadFrameTime);
                        if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                    }
                }
                yield return null;
            }
            result.gc0Collections = GC.CollectionCount(0) - gc0;
            result.submitReadbackSamplesMs = submit; result.cpuFrameSamplesMs = cpu.ToArray(); result.cpuMainSamplesMs = cpuMain.ToArray(); result.gpuFrameSamplesMs = gpu.ToArray();
            result.cpuSamples = cpu.Count; result.cpuMainSamples = cpuMain.Count; result.gpuSamples = gpu.Count;
            result.cpuTimingStatus = cpu.Count > 0 ? "available" : "unavailable";
            result.cpuMainTimingStatus = cpuMain.Count > 0 ? "available" : "unavailable";
            result.gpuTimingStatus = gpu.Count > 0 ? "available" : "unavailable-zero-or-no-timestamps";
            result.submitReadbackMedianMs = TempleCostPercentile(submit, .5f); result.submitReadbackP95Ms = TempleCostPercentile(submit, .95f);
            result.cpuFrameMedianMs = TempleCostPercentile(result.cpuFrameSamplesMs, .5f); result.cpuFrameP95Ms = TempleCostPercentile(result.cpuFrameSamplesMs, .95f);
            result.cpuMainMedianMs = TempleCostPercentile(result.cpuMainSamplesMs, .5f); result.cpuMainP95Ms = TempleCostPercentile(result.cpuMainSamplesMs, .95f);
            result.gpuFrameMedianMs = TempleCostPercentile(result.gpuFrameSamplesMs, .5f); result.gpuFrameP95Ms = TempleCostPercentile(result.gpuFrameSamplesMs, .95f);
        }
    }
}
