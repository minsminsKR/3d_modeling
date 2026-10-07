using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

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
                minimumFlameHeight, maximumFlameHeight, gameSeconds, wallSeconds, largestFrameGap;
            public int samples, completedPulses;
            public CandleFlickerFrame[] frames;
        }
        [Serializable] sealed class CandleFlickerEvidence
        {
            public string scope = "Controlled real active mode-owned Watchman, natural frames/timeScale1 and actual candle LocalLight intensity. No theoretical intensity evaluation or forced frame cadence. Does not certify GPU/FPS, visual comfort or native survival.";
            public CandleFlickerWindow far, middle, near;
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
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 10));
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll(); yield return Delay(.15f);
            evidence.far = new CandleFlickerWindow { label = "far-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.far.danger, Is.GreaterThan(0).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.far);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 6));
            Call(LightRun, "RefreshDanger"); yield return Delay(.15f);
            evidence.middle = new CandleFlickerWindow { label = "middle-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.middle.danger, Is.GreaterThan(evidence.far.danger).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.middle);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3));
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
            Assert.That(evidence.near.mean, Is.LessThan(evidence.far.mean - .15f), "Danger did not dim the surrounding pool overall");
            Assert.That(evidence.near.minimumEmission, Is.LessThan(evidence.far.minimumEmission * .3f), "The unlit flame sprite stayed radiant while its actual light collapsed");
            Assert.That(evidence.near.maximumEmission, Is.GreaterThan(evidence.near.minimumEmission + 1), "Actual photographic flame emission lacked the collapse and surge");
            Assert.That(evidence.near.minimumFlameHeight, Is.LessThan(evidence.near.maximumFlameHeight * .3f), "The visible flame did not shrink as it choked");
            // A constant old ~1Hz flutter cannot complete six full measured pulses in
            // this three-second window. No exact theoretical waveform is sampled here.
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(6));
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(evidence.far.completedPulses + 2),
                "Actual nearby warning did not show more completed light pulses than the far warning");
            Assert.That(evidence.middle.completedPulses, Is.GreaterThanOrEqualTo(evidence.far.completedPulses + 1));
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(evidence.middle.completedPulses + 1));
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleViolentNearWarningFreezesOnPauseAndRecoversSilentlyAfterBlackout()
        {
            yield return CandleDangerPrepare();
            ((Behaviour)player).enabled = false; PlacePlayer(LightApproach(LightTargets("Candle")[0]), false);
            var actor = CandleDangerOwnedBrain(); var candle = CandleDangerMarks()[0];
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3));
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
