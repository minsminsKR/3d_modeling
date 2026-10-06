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
            public float gameSeconds, intensity, danger;
        }
        [Serializable] sealed class CandleFlickerWindow
        {
            public string label;
            public float enemyDistance, danger, minimum, maximum, depth, gameSeconds, wallSeconds, largestFrameGap;
            public int samples, completedPulses;
            public CandleFlickerFrame[] frames;
        }
        [Serializable] sealed class CandleFlickerEvidence
        {
            public string scope = "Controlled real active mode-owned Watchman, natural frames/timeScale1 and actual candle LocalLight intensity. No theoretical intensity evaluation or forced frame cadence. Does not certify GPU/FPS, visual comfort or native survival.";
            public CandleFlickerWindow far, near;
        }

        IEnumerator CandleCaptureLiveFlicker(Component candle, CandleFlickerWindow result)
        {
            var light = Get<Light>(candle, "LocalLight");
            var frames = new List<CandleFlickerFrame>();
            float start = Time.time, wallStart = Time.realtimeSinceStartup;
            while (Time.time - start < 3)
            {
                Assert.That(Time.realtimeSinceStartup - wallStart, Is.LessThan(8), "Natural fixture clock failed to advance");
                Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
                Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True, "Warning-only sampling extinguished a marker");
                float elapsed = Time.time - start;
                if (frames.Count == 0 || elapsed > frames[frames.Count - 1].gameSeconds)
                    frames.Add(new CandleFlickerFrame { gameSeconds = elapsed, intensity = light.intensity, danger = Get<float>(candle, "Danger") });
                yield return null;
            }
            result.frames = frames.ToArray(); result.samples = frames.Count;
            result.gameSeconds = Time.time - start; result.wallSeconds = Time.realtimeSinceStartup - wallStart;
            result.minimum = frames.Min(frame => frame.intensity); result.maximum = frames.Max(frame => frame.intensity);
            result.depth = result.maximum - result.minimum;
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
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3));
            Call(LightRun, "RefreshDanger"); yield return Delay(.15f);
            evidence.near = new CandleFlickerWindow { label = "near-warning", enemyDistance = Vector3.Distance(actor.transform.position, player.transform.position), danger = Get<float>(LightRun, "Danger") };
            Assert.That(evidence.near.danger, Is.GreaterThan(evidence.far.danger).And.LessThan(1));
            yield return CandleCaptureLiveFlicker(candle, evidence.near);
            CloudExperienceTests.Artifact("candle-danger-live-flicker.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(evidence, true)));
            Assert.That(evidence.far.samples, Is.GreaterThanOrEqualTo(30), "Insufficient actual frame samples to judge the far pulse train");
            Assert.That(evidence.near.samples, Is.GreaterThanOrEqualTo(30), "Insufficient actual frame samples to judge the near pulse train");
            Assert.That(evidence.far.completedPulses, Is.GreaterThanOrEqualTo(1));
            Assert.That(evidence.near.depth, Is.GreaterThan(evidence.far.depth + .10f), "Actual nearby warning did not deepen its intensity modulation");
            // A constant old ~1Hz flutter cannot complete six full measured pulses in
            // this three-second window. No exact theoretical waveform is sampled here.
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(6));
            Assert.That(evidence.near.completedPulses, Is.GreaterThanOrEqualTo(evidence.far.completedPulses + 2),
                "Actual nearby warning did not show more completed light pulses than the far warning");
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
        }
    }
}
