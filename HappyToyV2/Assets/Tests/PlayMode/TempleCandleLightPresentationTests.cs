using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        [Serializable] sealed class CandleReviewRegion
        {
            public string name;
            public int x, y, width, height, clippedBefore, clippedAfter;
            public float baselineLuma, candidateLuma, meanAbsoluteDifference;
        }
        [Serializable] sealed class CandleReviewLight
        {
            public string name;
            public float x, y, z, range, intensity;
            public float altarContributionWithoutCandidate, altarContributionWithCandidate;
            public float hallContributionWithoutCandidate, hallContributionWithCandidate;
            public float baselineRestorationMeanError, candidateRestorationMeanError;
        }
        [Serializable] sealed class CandleReviewReport
        {
            public string cameraPose, note;
            public int width, height, perObjectLightLimit, nearbyPointLightsAtAltarBefore, nearbyPointLightsAtAltarAfter;
            public float candidateIntensity, candidateRange, initialBaselineRepeatMeanError;
            public CandleReviewRegion[] regions;
            public CandleReviewLight[] existingLights;
            public double[] synchronousOffMs, synchronousOnMs;
        }

        static RectInt CandleReviewBounds(Camera camera, Renderer[] renderers, int width, int height)
        {
            var valid = renderers.Where(r => r && r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (valid.Length == 0) return new RectInt();
            var bounds = valid[0].bounds;
            for (int i = 1; i < valid.Length; i++) bounds.Encapsulate(valid[i].bounds);
            float minX = 1, minY = 1, maxX = 0, maxY = 0; bool inFront = false;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var projected = camera.WorldToViewportPoint(corner);
                if (projected.z <= camera.nearClipPlane) continue;
                inFront = true; minX = Mathf.Min(minX, projected.x); maxX = Mathf.Max(maxX, projected.x);
                minY = Mathf.Min(minY, projected.y); maxY = Mathf.Max(maxY, projected.y);
            }
            if (!inFront) return new RectInt();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * width), 0, width);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * width), 0, width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * height), 0, height);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * height), 0, height);
            return new RectInt(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
        static float CandleReviewLuma(Color32 color) => .2126f * color.r + .7152f * color.g + .0722f * color.b;
        static float CandleReviewContribution(Color32[] on, Color32[] off, int width, RectInt rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return 0;
            double total = 0;
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = y * width + x;
                total += CandleReviewLuma(on[i]) - CandleReviewLuma(off[i]);
            }
            return (float)(total / (rect.width * rect.height));
        }
        static CandleReviewRegion CandleReviewRegionMetrics(string name, Color32[] baseline, Color32[] candidate,
            int width, RectInt rect)
        {
            var result = new CandleReviewRegion { name = name, x = rect.x, y = rect.y, width = rect.width, height = rect.height };
            if (rect.width <= 0 || rect.height <= 0) return result;
            double before = 0, after = 0, difference = 0;
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = y * width + x; var a = baseline[i]; var b = candidate[i];
                float la = CandleReviewLuma(a), lb = CandleReviewLuma(b);
                before += la; after += lb; difference += Math.Abs(lb - la);
                if (Math.Max(a.r, Math.Max(a.g, a.b)) >= 245) result.clippedBefore++;
                if (Math.Max(b.r, Math.Max(b.g, b.b)) >= 245) result.clippedAfter++;
            }
            int count = rect.width * rect.height;
            result.baselineLuma = (float)(before / count); result.candidateLuma = (float)(after / count);
            result.meanAbsoluteDifference = (float)(difference / count);
            return result;
        }
        static float CandleReviewMaxRegionError(Color32[] expected, Color32[] actual, int width, RectInt[] regions)
        {
            float maximum = 0;
            foreach (var region in regions)
                if (region.width > 0 && region.height > 0)
                    maximum = Mathf.Max(maximum, CandleReviewRegionMetrics("stability", expected, actual, width, region).meanAbsoluteDifference);
            return maximum;
        }
        static int CandleReviewNearby(Light[] lights, Bounds bounds, Light extra = null)
        {
            int count = lights.Count(l => l && l.enabled && l.gameObject.activeInHierarchy && l.type == LightType.Point &&
                (l.cullingMask & (1 << 8)) != 0 && bounds.SqrDistance(l.transform.position) <= l.range * l.range);
            if (extra && extra.enabled && bounds.SqrDistance(extra.transform.position) <= extra.range * extra.range) count++;
            return count;
        }
        static Bounds CandleReviewCombinedBounds(Renderer[] renderers)
        {
            var active = renderers.Where(r => r && r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Assert.That(active.Length, Is.GreaterThan(0), "The comparison target has no active renderers");
            var bounds = active[0].bounds;
            for (int i = 1; i < active.Length; i++) bounds.Encapsulate(active[i].bounds);
            return bounds;
        }
        static double CandleReviewTimedReadback(Camera eyes)
        {
            var watch = Stopwatch.StartNew();
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            watch.Stop(); Object.Destroy(frame); return watch.Elapsed.TotalMilliseconds;
        }

        // Opt-in investigation only: this temporary light is not gameplay art.
        // Output compares light isolation in the same frozen production frame.
        [UnityTest, Timeout(360000)]
        public IEnumerator TempleCandleLightOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-altar-light-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
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
            var vessel = world.transform.Find("Incense vessel"); var hall = world.transform.Find("Temple hall exterior");
            Assert.That(vessel, Is.Not.Null); Assert.That(hall, Is.Not.Null);
            var bowlRenderers = vessel.GetComponentsInChildren<Renderer>(true);
            var hallRenderers = hall.GetComponentsInChildren<Renderer>(true);
            var lanternRoots = world.transform.Cast<Transform>().Where(t => t.name == "Temple lantern").ToArray();
            Assert.That(lanternRoots.Length, Is.EqualTo(4));
            var existing = world.GetComponentsInChildren<Light>(true)
                .Where(l => l && l.enabled && l.gameObject.activeInHierarchy && l.type == LightType.Point &&
                    (l.name == "Temple lantern light" || l.name == "Temple facade fill" || l.name == "Faint light inside temple hall")).ToList();
            var flashlight = Get<Light>(player, "flashlight");
            if (flashlight && flashlight.enabled && flashlight.gameObject.activeInHierarchy) existing.Add(flashlight);
            Assert.That(existing.Count(l => l.name == "Temple lantern light"), Is.EqualTo(4));
            var oldStates = existing.Select(l => l.enabled).ToArray();
            var motor = (Behaviour)player; bool motorEnabled = motor.enabled;
            var position = player.transform.position; var playerRotation = player.transform.rotation; var eyeRotation = eyes.transform.rotation;
            var candidateObject = new GameObject("Test-only altar candle point light");
            candidateObject.transform.SetParent(world.transform, false);
            candidateObject.transform.localPosition = new Vector3(0, 1.17f, 7.05f);
            var candidate = candidateObject.AddComponent<Light>();
            candidate.type = LightType.Point; candidate.color = new Color(1f, .48f, .18f);
            candidate.intensity = .12f; candidate.range = 2.6f; candidate.shadows = LightShadows.None;
            candidate.cullingMask = 1 << 8; candidate.enabled = false;
            bool paused = false;
            try
            {
                motor.enabled = false;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero, "Light isolation requires a frozen scene");
                var points = new[] { new Vector3(500f, .04f, 503.5f), new Vector3(500f, .04f, 505.2f) };
                var names = new[] { "approach", "altar-front" };
                var trialIntensities = new[] { .12f, .25f, .5f };
                var trialNames = new[] { "012", "025", "050" };
                var pointTargets = new[] { new Vector3(500f, 1.65f, 509f), new Vector3(500f, 1.0f, 507.2f) };
                for (int pose = 0; pose < points.Length; pose++)
                {
                    PlacePlayer(points[pose]);
                    eyes.transform.rotation = Quaternion.LookRotation(pointTargets[pose] - eyes.transform.position);
                    Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(pointTargets[pose] - eyes.transform.position);
                    const int width = 1280, height = 720;
                    var bowlRect = CandleReviewBounds(eyes, bowlRenderers, width, height);
                    var hallRect = CandleReviewBounds(eyes, hallRenderers, width, height);
                    var lanternRects = lanternRoots.Select(t => CandleReviewBounds(eyes,
                        t.GetComponentsInChildren<Renderer>(true), width, height)).ToArray();
                    Assert.That(bowlRect.width * bowlRect.height, Is.GreaterThan(0), "The actual bowl must be visible");
                    var stabilityRegions = new[] { bowlRect, hallRect }.Concat(lanternRects).ToArray();
                    var fixedCameraPosition = eyes.transform.position;
                    var fixedCameraRotation = eyes.transform.rotation;
                    var bowlBounds = CandleReviewCombinedBounds(bowlRenderers);
                    Color32[] before, after;
                    candidate.enabled = false;
                    yield return null; // Flush light changes before rendering this frozen state.
                    var baseImage = SchoolHorrorFrame(eyes, width, height);
                    try { before = baseImage.GetPixels32(); File.WriteAllBytes(Path.Combine(output, names[pose] + "-baseline.png"), baseImage.EncodeToPNG()); }
                    finally { Object.Destroy(baseImage); }
                    yield return null;
                    var baselineRepeat = SchoolHorrorFrame(eyes, width, height);
                    float initialRepeatError;
                    try
                    {
                        initialRepeatError = CandleReviewMaxRegionError(before, baselineRepeat.GetPixels32(), width, stabilityRegions);
                        File.WriteAllBytes(Path.Combine(output, names[pose] + "-baseline-repeat.png"), baselineRepeat.EncodeToPNG());
                    }
                    finally { Object.Destroy(baselineRepeat); }
                    Assert.That(Vector3.Distance(eyes.transform.position, fixedCameraPosition), Is.LessThan(.001f), "Camera position must stay fixed");
                    Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedCameraRotation), Is.LessThan(.01f), "Camera orientation must stay fixed");
                    Assert.That(initialRepeatError, Is.LessThanOrEqualTo(1f), "Baseline must remain stable across visible target regions while paused");
                    for (int trial = 0; trial < trialIntensities.Length; trial++)
                    {
                        candidate.intensity = trialIntensities[trial];
                        candidate.enabled = true;
                        yield return null;
                        var candidateImage = SchoolHorrorFrame(eyes, width, height);
                        try { after = candidateImage.GetPixels32(); File.WriteAllBytes(Path.Combine(output, names[pose] + "-candidate-" + trialNames[trial] + ".png"), candidateImage.EncodeToPNG()); }
                        finally { Object.Destroy(candidateImage); }
                        var lights = new List<CandleReviewLight>();
                        for (int i = 0; i < existing.Count; i++)
                        {
                            var current = existing[i]; Color32[] baseWithout, candidateWithout, baseRestored, candidateRestored;
                            try
                            {
                                current.enabled = false; candidate.enabled = false;
                                yield return null;
                                var frame = SchoolHorrorFrame(eyes, width, height);
                                try { baseWithout = frame.GetPixels32(); }
                                finally { Object.Destroy(frame); }
                                candidate.enabled = true;
                                yield return null;
                                frame = SchoolHorrorFrame(eyes, width, height);
                                try { candidateWithout = frame.GetPixels32(); }
                                finally { Object.Destroy(frame); }
                                current.enabled = oldStates[i];
                                yield return null;
                                frame = SchoolHorrorFrame(eyes, width, height);
                                try { candidateRestored = frame.GetPixels32(); }
                                finally { Object.Destroy(frame); }
                                candidate.enabled = false;
                                yield return null;
                                frame = SchoolHorrorFrame(eyes, width, height);
                                try { baseRestored = frame.GetPixels32(); }
                                finally { Object.Destroy(frame); }
                            }
                            finally { current.enabled = oldStates[i]; }
                            float baselineError = CandleReviewMaxRegionError(before, baseRestored, width, stabilityRegions);
                            float candidateError = CandleReviewMaxRegionError(after, candidateRestored, width, stabilityRegions);
                            Assert.That(Vector3.Distance(eyes.transform.position, fixedCameraPosition), Is.LessThan(.001f), "Camera position changed during comparison");
                            Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedCameraRotation), Is.LessThan(.01f), "Camera orientation changed during comparison");
                            Assert.That(baselineError, Is.LessThanOrEqualTo(1f), "Existing light restoration changed the paused baseline: " + current.name);
                            Assert.That(candidateError, Is.LessThanOrEqualTo(1f), "Existing light restoration changed the paused candidate: " + current.name);
                            var local = world.transform.InverseTransformPoint(current.transform.position);
                            lights.Add(new CandleReviewLight { name = current.name + " #" + i,
                                x = local.x, y = local.y, z = local.z, range = current.range, intensity = current.intensity,
                                baselineRestorationMeanError = baselineError, candidateRestorationMeanError = candidateError,
                                altarContributionWithoutCandidate = CandleReviewContribution(before, baseWithout, width, bowlRect),
                                altarContributionWithCandidate = CandleReviewContribution(after, candidateWithout, width, bowlRect),
                                hallContributionWithoutCandidate = CandleReviewContribution(before, baseWithout, width, hallRect),
                                hallContributionWithCandidate = CandleReviewContribution(after, candidateWithout, width, hallRect) });
                        }
                        var allPoints = world.GetComponentsInChildren<Light>(true).Where(l => l != candidate).ToArray();
                        var offTimes = new List<double>(); var onTimes = new List<double>();
                        // A fixed warmup and alternating order reduce, but do not eliminate, CPU/readback timing noise.
                        for (int i = 0; i < 2; i++)
                        {
                            candidate.enabled = false; yield return null; CandleReviewTimedReadback(eyes);
                            candidate.enabled = true; yield return null; CandleReviewTimedReadback(eyes);
                        }
                        for (int i = 0; i < 8; i++)
                        {
                            bool onFirst = (i & 1) == 0;
                            candidate.enabled = onFirst; yield return null; double first = CandleReviewTimedReadback(eyes);
                            candidate.enabled = !onFirst; yield return null; double second = CandleReviewTimedReadback(eyes);
                            if (onFirst) { onTimes.Add(first); offTimes.Add(second); } else { offTimes.Add(first); onTimes.Add(second); }
                        }
                        var pipeline = GraphicsSettings.currentRenderPipeline;
                        var maxProperty = pipeline ? pipeline.GetType().GetProperty("maxAdditionalLightsCount") : null;
                        int maxLights = maxProperty != null ? (int)maxProperty.GetValue(pipeline) : -1;
                        candidate.enabled = false; int nearbyBefore = CandleReviewNearby(allPoints, bowlBounds);
                        candidate.enabled = true; int nearbyAfter = CandleReviewNearby(allPoints, bowlBounds, candidate);
                        var regions = new List<CandleReviewRegion> {
                            CandleReviewRegionMetrics("incense-vessel", before, after, width, bowlRect),
                            CandleReviewRegionMetrics("temple-hall", before, after, width, hallRect) };
                        for (int i = 0; i < lanternRects.Length; i++)
                            regions.Add(CandleReviewRegionMetrics("lantern-" + i, before, after, width, lanternRects[i]));
                        var report = new CandleReviewReport { cameraPose = names[pose], width = width, height = height,
                            initialBaselineRepeatMeanError = initialRepeatError,
                            perObjectLightLimit = maxLights, nearbyPointLightsAtAltarBefore = nearbyBefore,
                            nearbyPointLightsAtAltarAfter = nearbyAfter, candidateIntensity = candidate.intensity, candidateRange = candidate.range,
                            regions = regions.ToArray(), existingLights = lights.ToArray(),
                            synchronousOffMs = offTimes.ToArray(), synchronousOnMs = onTimes.ToArray(),
                            note = "Light state changes settle across paused frames; same-state captures must agree in all visible target ROIs. Screen bounds include background; contributions and nearby range counts do not prove URP light selection. Timing includes synchronous offscreen render/readback and is not GPU time or displayed FPS." };
                        File.WriteAllText(Path.Combine(output, names[pose] + "-metrics-" + trialNames[trial] + ".json"), JsonUtility.ToJson(report, true));
                        TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                    }
                }
            }
            finally
            {
                candidate.enabled = false;
                for (int i = 0; i < existing.Count; i++) if (existing[i]) existing[i].enabled = oldStates[i];
                Object.Destroy(candidateObject);
                PlacePlayer(position); player.transform.rotation = playerRotation; eyes.transform.rotation = eyeRotation;
                motor.enabled = motorEnabled;
                if (paused) Call(shell, "Resume");
            }
        }
    }
}
