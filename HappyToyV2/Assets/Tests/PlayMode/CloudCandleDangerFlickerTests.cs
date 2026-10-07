using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class CandleFlickerFrame
        {
            public float gameSeconds, intensity, danger, emission, opacity, flameHeight;
        }
        [Serializable] sealed class CandleFlickerWindow
        {
            public string label;
            public float enemyDistance, danger, minimum, maximum, depth, mean, minimumEmission, maximumEmission,
                minimumFlameHeight, maximumFlameHeight, gameSeconds, wallSeconds, largestFrameGap, longestDarkHoldSeconds;
            public int samples, completedPulses;
            public CandleFlickerFrame[] frames;
        }
        [Serializable] sealed class CandleFlickerEvidence
        {
            public string scope = "Controlled real active mode-owned Watchman, natural frames/timeScale1 and actual candle LocalLight intensity. No theoretical intensity evaluation or forced frame cadence. Does not certify GPU/FPS, visual comfort or native survival.";
            public CandleFlickerWindow far, middle, near;
        }
        [Serializable] sealed class CandleRenderedWarning
        {
            public string mode, candleId;
            public string scope = "Production player camera and ordinary enabled flashlight, same physical approach and natural frames/timeScale1. Peak/trough are captured live without forcing a phase or changing materials/lights. No comfort/FPS claim.";
            public float cameraDistance, enemyDistance, danger, minimumIntensity, maximumIntensity,
                flameHeightPixels, brightRegionLuminance, darkRegionLuminance;
            public bool flashlightEnabled;
            public int brighterFlamePixels;
            public Rect flameRegion;
        }

        static void CandleRenderedRegion(Texture2D bright, Texture2D dark, Rect region,
            out float brightLuminance, out float darkLuminance, out int brighterPixels)
        {
            var a = bright.GetPixels32(); var b = dark.GetPixels32();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(region.xMin * bright.width) - 24, 0, bright.width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(region.xMax * bright.width) + 24, 0, bright.width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(region.yMin * bright.height) - 24, 0, bright.height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(region.yMax * bright.height) + 24, 0, bright.height - 1);
            double sumA = 0, sumB = 0; int count = 0; brighterPixels = 0;
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                int index = y * bright.width + x;
                float la = (a[index].r * .2126f + a[index].g * .7152f + a[index].b * .0722f) / 255;
                float lb = (b[index].r * .2126f + b[index].g * .7152f + b[index].b * .0722f) / 255;
                sumA += la; sumB += lb; count++;
                if (la > lb + .08f) brighterPixels++;
            }
            brightLuminance = (float)(sumA / count); darkLuminance = (float)(sumB / count);
        }

        Vector3 CandleProductionViewApproach(Component target, Renderer flameRenderer)
        {
            float floor = target.transform.position.y - 1.06f;
            var controller = player.GetComponent<CharacterController>();
            for (int index = 0; index < 48; index++)
            {
                float angle = index * Mathf.PI / 24;
                var desired = target.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.3f;
                desired.y = floor;
                if (!NavMesh.SamplePosition(desired, out var hit, .15f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floor) > .1f) continue;
                var body = hit.position + controller.center;
                float half = controller.height * .5f - controller.radius;
                if (Physics.OverlapCapsule(body - Vector3.up * half, body + Vector3.up * half,
                    controller.radius - .02f, ~0, QueryTriggerInteraction.Ignore).Any(item => !item.transform.IsChildOf(player.transform))) continue;
                var eye = hit.position + Get<Camera>(player, "eyes").transform.localPosition;
                if (Physics.Linecast(eye, flameRenderer.bounds.center, out var ray, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && !ray.collider.transform.IsChildOf(target.transform)) continue;
                return hit.position;
            }
            Assert.Fail("No physical production eye-level flame approach at 2.3m to " + target.name);
            return Vector3.zero;
        }

        void CandleProductionCombinedView(Component target, Renderer flameRenderer, Component actor)
        {
            var camera = Get<Camera>(player, "eyes");
            var controller = player.GetComponent<CharacterController>();
            float floor = target.transform.position.y - 1.06f;
            for (int view = 0; view < 48; view++)
            {
                float angle = view * Mathf.PI / 24;
                var desired = target.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.3f;
                desired.y = floor;
                if (!NavMesh.SamplePosition(desired, out var playerHit, .15f, NavMesh.AllAreas) ||
                    Mathf.Abs(playerHit.position.y - floor) > .1f) continue;
                var body = playerHit.position + controller.center;
                float half = controller.height * .5f - controller.radius;
                if (Physics.OverlapCapsule(body - Vector3.up * half, body + Vector3.up * half,
                    controller.radius - .02f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(item => !item.transform.IsChildOf(player.transform))) continue;
                PlacePlayer(playerHit.position, false);
                var flameDirection = (flameRenderer.bounds.center - camera.transform.position).normalized;
                for (int ray = 0; ray < 96; ray++)
                {
                    float enemyAngle = ray * Mathf.PI / 48;
                    var enemyPoint = playerHit.position + new Vector3(Mathf.Cos(enemyAngle), 0, Mathf.Sin(enemyAngle)) * 3;
                    if (!NavMesh.SamplePosition(enemyPoint, out var enemyHit, .22f, NavMesh.AllAreas) ||
                        Mathf.Abs(enemyHit.position.y - floor) > .2f) continue;
                    CandleDangerPlaceActor(actor, enemyHit.position);
                    var monsterRenderer = actor.GetComponentsInChildren<Renderer>().FirstOrDefault(item =>
                        item.enabled && (item is MeshRenderer || item is SkinnedMeshRenderer));
                    if (!monsterRenderer) continue;
                    var monsterDirection = (monsterRenderer.bounds.center - camera.transform.position).normalized;
                    var combined = flameDirection + monsterDirection;
                    if (combined.sqrMagnitude < .001f) continue;
                    camera.transform.rotation = Quaternion.LookRotation(combined);
                    if (!CandleDangerPlayerSees(actor)) continue;
                    var bounds = flameRenderer.bounds; bool fits = true;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        var viewport = camera.WorldToViewportPoint(point);
                        if (viewport.z <= camera.nearClipPlane || viewport.x < .05f || viewport.x > .95f ||
                            viewport.y < .05f || viewport.y > .95f) { fits = false; break; }
                    }
                    if (!fits || Physics.Linecast(camera.transform.position, bounds.center, out var hit,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        !hit.collider.transform.IsChildOf(target.transform)) continue;
                    return;
                }
            }
            Assert.Fail("No physical 2.3m candle approach and 3m monster placement visible together in the actual production camera");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator CandleProductionCameraShowsNearDarkHoldsAndBrightCatchesWithFlashlightInBothModes()
        {
            foreach (bool corridor in new[] { true, false })
            {
                yield return CandleDangerPrepare(corridor);
                var target = LightTargets("Candle")[0]; var candle = CandleDangerMarks()[0];
                var camera = Get<Camera>(player, "eyes"); var flashlight = Get<Light>(player, "flashlight");
                flashlight.enabled = true; CandleDangerArmAll();
                var flame = Get<Transform>(candle, "Flame"); var light = Get<Light>(candle, "LocalLight");
                var renderer = flame.GetComponentInChildren<Renderer>();
                ((Behaviour)player).enabled = false;
                var actor = CandleDangerOwnedBrain();
                CandleProductionCombinedView(target, renderer, actor);
                var actorPoint = actor.transform.position; actor.gameObject.SetActive(false);
                Call(LightRun, "RefreshDanger"); yield return Delay(.2f);
                Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.captureDeltaTime, Is.EqualTo(0));
                Assert.That(Get<bool>(shell, "ReducedMotion"), Is.False);
                string mode = corridor ? "corridor" : "school";
                // Encode evidence as high-quality JPEG within the bounded log envelope.
                // Pixel assertions still measure the original, unencoded camera textures.
                var far = SchoolCameraFrame(camera, out _, out _, out _);
                try { CloudExperienceTests.Artifact("candle-" + mode + "-safe-flashlight.jpg", far.EncodeToJPG(95)); }
                finally { Object.Destroy(far); }
                CandleDangerPlaceActor(actor, actorPoint);
                Assert.That(CandleDangerPlayerSees(actor), Is.True,
                    "The flame capture camera must also see the actual nearby monster");
                Call(LightRun, "RefreshDanger"); yield return Delay(.15f);
                var evidence = new CandleRenderedWarning { mode = mode,
                    candleId = Get<string>(target, "stableId"), flashlightEnabled = flashlight.enabled,
                    cameraDistance = Vector3.Distance(camera.transform.position, renderer.bounds.center),
                    enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position),
                    danger = Get<float>(LightRun, "Danger"), minimumIntensity = float.PositiveInfinity,
                    maximumIntensity = float.NegativeInfinity };
                Texture2D dark = null, bright = null;
                float start = Time.time, wallStart = Time.realtimeSinceStartup;
                try
                {
                    while (Time.time - start < 4)
                    {
                        Assert.That(Time.realtimeSinceStartup - wallStart, Is.LessThan(20));
                        Assert.That(Get<bool>(candle, "Lit"), Is.True);
                        Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
                        if (light.intensity < evidence.minimumIntensity)
                        {
                            if (dark) Object.Destroy(dark);
                            evidence.minimumIntensity = light.intensity;
                            dark = SchoolCameraFrame(camera, out _, out _, out _);
                        }
                        if (light.intensity > evidence.maximumIntensity)
                        {
                            if (bright) Object.Destroy(bright);
                            evidence.maximumIntensity = light.intensity;
                            bright = SchoolCameraFrame(camera, out var matrix, out _, out var viewport);
                            evidence.flameRegion = SchoolScreenBounds(matrix, viewport, renderer.bounds);
                            evidence.flameHeightPixels = evidence.flameRegion.height * bright.height;
                        }
                        yield return null;
                    }
                    Assert.That(dark && bright, Is.True);
                    CandleRenderedRegion(bright, dark, evidence.flameRegion,
                        out evidence.brightRegionLuminance, out evidence.darkRegionLuminance, out evidence.brighterFlamePixels);
                    CloudExperienceTests.Artifact("candle-" + mode + "-near-dark-flashlight.jpg", dark.EncodeToJPG(95));
                    CloudExperienceTests.Artifact("candle-" + mode + "-near-bright-flashlight.jpg", bright.EncodeToJPG(95));
                    CloudExperienceTests.Artifact("candle-" + mode + "-rendered-warning.json",
                        Encoding.UTF8.GetBytes(JsonUtility.ToJson(evidence, true)));
                    Assert.That(evidence.minimumIntensity, Is.LessThan(.05f));
                    Assert.That(evidence.maximumIntensity, Is.GreaterThan(.55f));
                    Assert.That(evidence.flameRegion.xMin, Is.InRange(0, 1)); Assert.That(evidence.flameRegion.xMax, Is.InRange(0, 1));
                    Assert.That(evidence.flameRegion.yMin, Is.InRange(0, 1)); Assert.That(evidence.flameRegion.yMax, Is.InRange(0, 1));
                    Assert.That(evidence.flameHeightPixels, Is.GreaterThan(16), "Actual flame is too small to read at a physical 2.3m approach");
                    Assert.That(evidence.brighterFlamePixels, Is.GreaterThan(20), "Ordinary flashlight hid the candle's actual dark/bright change");
                    Assert.That(evidence.brightRegionLuminance, Is.GreaterThan(evidence.darkRegionLuminance + .01f),
                        "The actual flame region has no visible near-threat dark hold and bright catch");
                }
                finally { if (dark) Object.Destroy(dark); if (bright) Object.Destroy(bright); }
                actor.gameObject.SetActive(false);
                if (corridor)
                { var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous); }
            }
        }

        IEnumerator CandleCaptureLiveFlicker(Component candle, CandleFlickerWindow result)
        {
            var light = Get<Light>(candle, "LocalLight");
            var flame = Get<Transform>(candle, "Flame");
            var renderer = flame.GetComponentInChildren<Renderer>();
            var properties = new MaterialPropertyBlock();
            int emission = Shader.PropertyToID("_Emission"), opacity = Shader.PropertyToID("_Opacity");
            var frames = new List<CandleFlickerFrame>();
            float start = Time.time, wallStart = Time.realtimeSinceStartup;
            while (Time.time - start < 3)
            {
                Assert.That(Time.realtimeSinceStartup - wallStart, Is.LessThan(8), "Natural fixture clock failed to advance");
                Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
                Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True, "Warning-only sampling extinguished a marker");
                float elapsed = Time.time - start;
                if (frames.Count == 0 || elapsed > frames[frames.Count - 1].gameSeconds)
                {
                    renderer.GetPropertyBlock(properties);
                    frames.Add(new CandleFlickerFrame { gameSeconds = elapsed, intensity = light.intensity,
                        danger = Get<float>(candle, "Danger"), emission = properties.GetFloat(emission),
                        opacity = properties.GetFloat(opacity), flameHeight = flame.localScale.y });
                }
                yield return null;
            }
            result.frames = frames.ToArray(); result.samples = frames.Count;
            result.gameSeconds = Time.time - start; result.wallSeconds = Time.realtimeSinceStartup - wallStart;
            result.minimum = frames.Min(frame => frame.intensity); result.maximum = frames.Max(frame => frame.intensity);
            result.depth = result.maximum - result.minimum;
            result.mean = frames.Average(frame => frame.intensity);
            result.minimumEmission = frames.Min(frame => frame.emission); result.maximumEmission = frames.Max(frame => frame.emission);
            result.minimumFlameHeight = frames.Min(frame => frame.flameHeight); result.maximumFlameHeight = frames.Max(frame => frame.flameHeight);
            result.largestFrameGap = Enumerable.Range(1, frames.Count - 1)
                .Select(index => frames[index].gameSeconds - frames[index - 1].gameSeconds).DefaultIfEmpty(0).Max();
            float darkStart = -1;
            foreach (var frame in frames)
            {
                if (frame.intensity < .068f)
                {
                    if (darkStart < 0) darkStart = frame.gameSeconds;
                    result.longestDarkHoldSeconds = Mathf.Max(result.longestDarkHoldSeconds, frame.gameSeconds - darkStart);
                }
                else darkStart = -1;
            }
            // Independent hysteresis over the measured intensity range counts complete
            // dark->bright->dark pulses, excluding partial cycles at the window edges.
            float low = result.minimum + result.depth * .30f, high = result.minimum + result.depth * .70f;
            bool startedDark = false, reachedBright = false;
            foreach (var frame in frames)
            {
                if (!startedDark && frame.intensity <= low) startedDark = true;
                else if (startedDark && !reachedBright && frame.intensity >= high) reachedBright = true;
                else if (startedDark && reachedBright && frame.intensity <= low)
                { result.completedPulses++; reachedBright = false; }
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleActualNormalMotionWarningGrowsFlickerDepthAndCompletedPulseCount()
        {
            yield return CandleDangerPrepare();
            ((Behaviour)player).enabled = false; PlacePlayer(LightApproach(LightTargets("Candle")[0]), false);
            Assert.That(Get<bool>(shell, "ReducedMotion"), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(Time.captureDeltaTime, Is.EqualTo(0));
            var actor = CandleDangerOwnedBrain(); var candle = CandleDangerMarks()[0];
            var evidence = new CandleFlickerEvidence();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 10)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll(); yield return Delay(.15f);
            evidence.far = new CandleFlickerWindow { label = "far-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.far.danger, Is.GreaterThan(0).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.far);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 6)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); yield return Delay(.15f);
            evidence.middle = new CandleFlickerWindow { label = "middle-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.middle.danger, Is.GreaterThan(evidence.far.danger).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.middle);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); yield return Delay(.15f);
            evidence.near = new CandleFlickerWindow { label = "near-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.near.danger, Is.GreaterThan(evidence.far.danger).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.near);
            CloudExperienceTests.Artifact("candle-danger-live-flicker.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(evidence, true)));
            Assert.That(evidence.far.samples, Is.GreaterThanOrEqualTo(30), "Insufficient actual frame samples to judge the far pulse train");
            Assert.That(evidence.middle.samples, Is.GreaterThanOrEqualTo(30));
            Assert.That(evidence.near.samples, Is.GreaterThanOrEqualTo(30), "Insufficient actual frame samples to judge the near pulse train");
            Assert.That(evidence.far.completedPulses, Is.GreaterThanOrEqualTo(1));
            Assert.That(evidence.near.depth, Is.GreaterThan(evidence.far.depth + .10f), "Actual nearby warning did not deepen its intensity modulation");
            Assert.That(evidence.middle.depth, Is.GreaterThan(evidence.far.depth + .10f), "The warning did not visibly deepen while the monster approached");
            Assert.That(evidence.near.depth, Is.GreaterThan(evidence.middle.depth + .10f), "The last few metres did not strengthen the flame collapse");
            Assert.That(evidence.near.minimum, Is.LessThan(.05f), "A nearby undetected monster never nearly choked the actual light");
            Assert.That(evidence.near.maximum, Is.GreaterThan(.55f), "The nearly collapsed light never visibly caught again");
            Assert.That(evidence.near.longestDarkHoldSeconds, Is.GreaterThanOrEqualTo(.07f),
                "The near warning has no sustained visible dark hold between its bright catches");
            Assert.That(evidence.near.minimumEmission, Is.LessThan(evidence.far.minimumEmission * .3f), "The unlit flame sprite stayed radiant while its actual light collapsed");
            Assert.That(evidence.near.maximumEmission, Is.GreaterThan(evidence.near.minimumEmission + 1), "Actual photographic flame emission lacked the collapse and surge");
            Assert.That(evidence.near.minimumFlameHeight, Is.LessThan(evidence.near.maximumFlameHeight * .3f), "The visible flame did not shrink as it choked");
            // Readable dark holds replace the old rapid shimmer. Judge complete
            // natural pulses and their distance trend, not an exact oscillator rate.
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(2));
            Assert.That(evidence.near.completedPulses, Is.GreaterThan(evidence.far.completedPulses),
                "Actual nearby warning did not show more completed light pulses than the far warning");
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleViolentNearWarningFreezesOnPauseAndRecoversSilentlyAfterBlackout()
        {
            yield return CandleDangerPrepare();
            ((Behaviour)player).enabled = false; PlacePlayer(LightApproach(LightTargets("Candle")[0]), false);
            var actor = CandleDangerOwnedBrain(); var candle = CandleDangerMarks()[0];
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll(); yield return Delay(.3f);
            var flame = Get<Transform>(candle, "Flame"); var light = Get<Light>(candle, "LocalLight");
            var renderer = flame.GetComponentInChildren<Renderer>(); var properties = new MaterialPropertyBlock();
            int emission = Shader.PropertyToID("_Emission"), opacity = Shader.PropertyToID("_Opacity");
            Call(shell, "Pause"); yield return null;
            float brightness = light.intensity, danger = Get<float>(LightRun, "Danger");
            var position = flame.localPosition; var rotation = flame.localRotation; var scale = flame.localScale;
            renderer.GetPropertyBlock(properties); float radiance = properties.GetFloat(emission), alpha = properties.GetFloat(opacity);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 1.7f));
            Call(LightRun, "RefreshDanger"); yield return Delay(.25f);
            Assert.That(Get<float>(LightRun, "Danger"), Is.EqualTo(danger));
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False); Assert.That(Get<bool>(candle, "Lit"), Is.True);
            Assert.That(light.intensity, Is.EqualTo(brightness));
            Assert.That(flame.localPosition, Is.EqualTo(position)); Assert.That(flame.localRotation, Is.EqualTo(rotation)); Assert.That(flame.localScale, Is.EqualTo(scale));
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(emission), Is.EqualTo(radiance)); Assert.That(properties.GetFloat(opacity), Is.EqualTo(alpha));
            Call(shell, "Resume");
            yield return Wait(() => Get<bool>(LightRun, "Blackout"), 2, "Resuming did not sample the nearby real actor");
            CandleDangerExpectExtinguished();
            actor.gameObject.SetActive(false); CandleDangerExpectSafe(); yield return Delay(.3f);
            Assert.That(Get<bool>(candle, "Lit"), Is.True); Assert.That(Get<bool>(candle, "HasBeenLit"), Is.True);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(0)); Assert.That(Get<AudioSource>(candle, "IgnitionSource").isPlaying, Is.False);
            Assert.That(light.intensity, Is.GreaterThan(.6f));
            renderer.GetPropertyBlock(properties); Assert.That(properties.GetFloat(emission), Is.GreaterThan(1.4f));
        }
    }
}
