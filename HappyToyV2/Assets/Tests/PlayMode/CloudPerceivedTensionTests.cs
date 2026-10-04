using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    internal static class CloudPerceivedTensionTests
    {
        [Serializable] internal sealed class Segment
        {
            public string name, scope = "isolated production sources and explicitly controlled event admission";
            public int offset, samples, rate, channels = 2, flushedSamples, clipped;
            public float seconds, stressStart, stressEnd, targetStart, targetEnd, bedGainStart, bedGainEnd, masterVolume;
            public float pulseVolume, airVolume, pulsePitch, airPitch, footVolume, footPitch;
            public bool paused, softened, observedContactDuck;
            public string[] audibleSources;
            public double rms, peak;
        }
        [Serializable] internal sealed class Report
        {
            public string capture = "UnityAudioRendererMainOutput", wave = "perceived-tension-mix.wav";
            public string scope = "Actual engine PCM; direct recognition/contact event controls isolate the bed. Natural recognition/navigation are tested separately. No synthetic replacement PCM, no device-listening or human-fear certification.";
            public int rate, channels = 2, samples, discardedCaptureSamples;
            public int[] discardedCaptureBatches;
            public float quietWaitGameSeconds;
            public Segment[] segments;
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        readonly List<int> tensionDiscardedBatches = new List<int>();
        Component TensionOwner()
        {
            var tension = player.GetComponent(RequireType("PerceivedTension"));
            Assert.That(tension, Is.Not.Null, "Player feedback did not install its one local tension owner"); return tension;
        }
        static bool TensionQuiet(Component tension) => Get<float>(tension, "Stress") <= .0001f &&
            Get<float>(tension, "TargetStress") <= .0001f && Get<float>(tension, "BedGain") <= .0001f;
        void TensionRecognition() => Call(RequireType("PerceivedTension"), "ReportRecognition", session);
        void TensionContact(Component steps, bool emit = true)
        {
            var source = Get<AudioSource>(steps, "MovementSource");
            if (emit) { source.Stop(); source.pitch = 1; source.PlayOneShot(Get<AudioClip>(steps, "MovementClip")); }
            Call(RequireType("PerceivedTension"), "ReportSound", session, source,
                Get<Component>(steps, "MovementAcoustics"), false, 1f);
        }
        IEnumerator TensionAdmissionFrames() { yield return null; yield return null; yield return null; }
        static void TensionSelectSources(params AudioSource[] selected)
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                source.mute = !selected.Contains(source);
        }
        IEnumerator AssertRejectedContact(Component tension, Component steps, string why, bool emit = true, Action afterQueue = null)
        {
            int count = Get<int>(tension, "PerceivedEvents"); float strength = Get<float>(tension, "LastPerceivedStrength");
            float target = Get<float>(tension, "TargetStress"), hold = Get<float>(tension, "HoldRemaining"), duck = Get<float>(tension, "DuckRemaining");
            TensionContact(steps, emit); afterQueue?.Invoke(); yield return TensionAdmissionFrames();
            Assert.That(Get<int>(tension, "PerceivedEvents"), Is.EqualTo(count), why + " invented sensory evidence");
            Assert.That(Get<float>(tension, "LastPerceivedStrength"), Is.EqualTo(strength));
            Assert.That(Get<float>(tension, "TargetStress"), Is.LessThanOrEqualTo(target + .00001f));
            Assert.That(Get<float>(tension, "HoldRemaining"), Is.LessThanOrEqualTo(hold + .00001f));
            Assert.That(Get<float>(tension, "DuckRemaining"), Is.LessThanOrEqualTo(duck + .00001f));
        }

        [UnityTest, Timeout(25000)]
        public IEnumerator SilentEnemyStateCannotInventTensionButActualUnseenFootstepsCanAnticipate()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            var tension = TensionOwner(); var ambience = One("RoomAmbience");
            Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 4, false);
            var silent = StalkerAt(origin); silent.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var wall = Cube("CloudQA silent chase cover", origin + Vector3.right * 2 + Vector3.up * 1.5f, new Vector3(.3f, 3, 8));
            Physics.SyncTransforms();
            // Deliberate negative-control knowledge state. Speed is zero and real
            // geometry blocks sight, so no sensory event has occurred.
            Set(silent, "state", "Chase");
            var privateFields = BindingFlags.Instance | BindingFlags.NonPublic;
            silent.GetType().GetField("memory", privateFields).SetValue(silent, 5f);
            silent.GetType().GetField("lastKnown", privateFields).SetValue(silent, origin + Vector3.right * 4);
            yield return Delay(.65f);
            Assert.That(Get<object>(silent, "state").ToString(), Is.EqualTo("Chase"), "Negative control accidentally left its hidden Chase state");
            Assert.That((bool)Call(silent, "CanSeePlayer"), Is.False);
            Assert.That(Get<int>(AudioSteps(silent), "StepsPlayed"), Is.Zero);
            Assert.That(Get<int>(tension, "PerceivedEvents"), Is.Zero); Assert.That(TensionQuiet(tension), Is.True);
            Assert.That(Get<float>(ambience, "StoryGain"), Is.EqualTo(1).Within(.001f), "Unseen Chase state still ducks local ambience");
            wall.SetActive(false); PlacePlayer(origin + Vector3.right * 4 + Vector3.up * 5, false);
            yield return Delay(.4f);
            Assert.That(Get<object>(silent, "state").ToString(), Is.EqualTo("Chase"));
            Assert.That(Get<int>(tension, "PerceivedEvents"), Is.Zero); Assert.That(TensionQuiet(tension), Is.True);
            Assert.That(Get<float>(ambience, "StoryGain"), Is.EqualTo(1).Within(.001f));
            silent.gameObject.SetActive(false);
            PlacePlayer(origin, false);
            var walker = MovingStalker(origin + Vector3.right * 3, Vector3.right);
            Assert.That((bool)Call(walker, "HearNoise", origin + Vector3.right * 8, 7f), Is.True);
            yield return Wait(() => Get<int>(AudioSteps(walker), "StepsPlayed") > 0 && Get<int>(tension, "ContactEvents") > 0, 4,
                "Natural out-of-view travel emitted no perceived anticipation");
            Assert.That((bool)Call(walker, "CanSeePlayer"), Is.False);
            Assert.That(Get<int>(tension, "RecognitionEvents"), Is.Zero);
            Assert.That(Get<int>(player.GetComponent(RequireType("DetectionFeedback")), "CuesPlayed"), Is.Zero);
            Assert.That(Get<float>(tension, "TargetStress"), Is.InRange(.001f, .35001f));
            yield return Wait(() => Get<float>(tension, "Stress") > .001f && Get<float>(ambience, "StoryGain") < .999f, 1, "Audible evidence did not gently reshape the room bed");
            walker.gameObject.SetActive(false); float aftermath = Get<float>(tension, "Stress");
            yield return new WaitForSeconds(.12f);
            Assert.That(Get<float>(tension, "Stress"), Is.GreaterThan(0), "Stopping an actor instantly erased perceived aftermath");
            Assert.That(aftermath, Is.GreaterThan(0));
            yield return Wait(() => TensionQuiet(tension), 4, "Contact anticipation did not settle after evidence stopped");
            yield return Wait(() => Get<float>(ambience, "StoryGain") > .999f, 2, "Quiet perception never restored local ambience");
            var attacker = StalkerAt(origin + Vector3.right * 1.15f); attacker.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Get<Light>(player, "flashlight").enabled = true;
            yield return Wait(() => Get<int>(attacker, "AttacksStarted") == 1 && Get<int>(tension, "AttackEvents") == 1, 3,
                "Actual close-range attack cue was not admitted as perceived evidence");
            Assert.That(Get<float>(tension, "LastPerceivedStrength"), Is.GreaterThan(.35f), "True nearby attack was no stronger than ordinary anticipation");
            Assert.That(Get<float>(tension, "TargetStress"), Is.LessThanOrEqualTo(.95001f));
            PlacePlayer(origin + Vector3.right * 6, false); // Explicit controlled dodge placement, no attack-state mutation.
            yield return Wait(() => !Get<bool>(attacker, "AttackActive"), 3, "Real attack did not finish after the player left its range/arc");
            Assert.That(Get<int>(tension, "AttackEvents"), Is.EqualTo(1));
            attacker.gameObject.SetActive(false); Assert.That(Get<bool>(session, "Finished"), Is.False);
            Debug.Log("HAPPYTOY_TENSION_PASS anticipation: sustained hidden/static Chase and silent other floor create no stress/duck; natural out-of-view footsteps precede recognition, remain capped/settle; true attack cue is stronger");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator PerceivedHearingUsesCurrentTransmissionAndRejectsUnheardOrInactiveCues()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            var tension = TensionOwner(); Vector3 fixture = new Vector3(850, 20, 850);
            var enemy = AudioStalkerAt(MainCorridorPoint(), "CloudQA Cyclopse perceived boundaries");
            ((Behaviour)enemy).enabled = false; var steps = AudioSteps(enemy); ((Behaviour)steps).enabled = false;
            var source = Get<AudioSource>(steps, "MovementSource"); var acoustics = Get<Component>(steps, "MovementAcoustics");
            PlacePlayer(fixture, false); PositionAudioFixture(steps, fixture + Vector3.forward * 3);
            var wall = Cube("CloudQA perception wall", fixture + new Vector3(0, 1.3f, 1.5f), new Vector3(8, 6, .3f));
            var slab = Cube("CloudQA perception slab", fixture + Vector3.up * 3, new Vector3(10, .3f, 10));
            wall.SetActive(false); slab.SetActive(false); Physics.SyncTransforms();
            yield return new WaitForSeconds(.7f);
            TensionContact(steps); yield return TensionAdmissionFrames();
            Assert.That(Get<int>(tension, "ContactEvents"), Is.EqualTo(1));
            float clear = Get<float>(tension, "LastPerceivedStrength"); Assert.That(clear, Is.GreaterThan(0));
            wall.SetActive(true); Physics.SyncTransforms(); yield return new WaitForSeconds(.7f);
            TensionContact(steps); yield return TensionAdmissionFrames();
            Assert.That(Get<int>(tension, "ContactEvents"), Is.EqualTo(2));
            float muffled = Get<float>(tension, "LastPerceivedStrength");
            Assert.That(Get<bool>(acoustics, "Occluded"), Is.True);
            Assert.That(muffled, Is.GreaterThan(0)); Assert.That(muffled, Is.LessThan(clear * .6f), "Wall transmission did not reduce perceived evidence");
            wall.SetActive(false); PositionAudioFixture(steps, fixture + Vector3.up * 5); slab.SetActive(true); Physics.SyncTransforms();
            yield return new WaitForSeconds(.7f);
            TensionContact(steps); yield return TensionAdmissionFrames();
            Assert.That(Get<bool>(acoustics, "FloorOccluded"), Is.True);
            Assert.That(Get<float>(tension, "LastPerceivedStrength"), Is.LessThan(muffled), "A physically blocked floor retained clear-room threat weight");
            source.Stop(); yield return Wait(() => TensionQuiet(tension), 4, "Boundary setup did not return to quiet");
            slab.SetActive(false); PositionAudioFixture(steps, fixture + Vector3.forward * 3); yield return new WaitForSeconds(.7f);
            source.Stop(); yield return AssertRejectedContact(tension, steps, "Stopped source", false);
            source.mute = true; yield return AssertRejectedContact(tension, steps, "Source mute"); source.Stop(); source.mute = false;
            Call(shell, "AdjustSettings", -1f, 0f);
            yield return AssertRejectedContact(tension, steps, "Master zero"); source.Stop(); Call(shell, "RestoreDefaultSettings");
            // No settling wait after a warp: current finite distance, not a cached
            // previously-near gain, must reject the very next emitted cue.
            PositionAudioFixture(steps, fixture + Vector3.forward * (source.maxDistance + 5));
            yield return AssertRejectedContact(tension, steps, "Fresh out-of-range warp"); source.Stop();
            PositionAudioFixture(steps, fixture + Vector3.forward * 3); yield return new WaitForSeconds(.7f);
            source.enabled = false; yield return AssertRejectedContact(tension, steps, "Disabled emitter", false); source.enabled = true;
            enemy.gameObject.SetActive(false); yield return AssertRejectedContact(tension, steps, "Inactive actor", false); enemy.gameObject.SetActive(true);
            Call(shell, "Pause"); yield return AssertRejectedContact(tension, steps, "Paused event", false); Call(shell, "Resume");
            yield return new WaitForSeconds(.3f);
            // Queue while eligible, then invalidate before deferred admission. The
            // restored listener must never inherit an unheard pending event.
            yield return AssertRejectedContact(tension, steps, "Queued then muted", true, () => source.mute = true);
            source.Stop(); source.mute = false; yield return TensionAdmissionFrames();
            Assert.That(TensionQuiet(tension), Is.True);
            yield return AssertRejectedContact(tension, steps, "Queued then paused", true, () => Call(shell, "Pause"));
            source.Stop(); Call(shell, "Resume");
            yield return TensionAdmissionFrames();
            Assert.That(TensionQuiet(tension), Is.True, "Unmuting/resuming revealed stress banked from rejected cues");
            Debug.Log("HAPPYTOY_TENSION_PASS hearing: actual source replay with deferred event admission; proportional wall/floor weight; stopped/muted/zero-master/fresh-far/disabled/inactive/paused rejection has no hold/duck/event side effects");
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator TrueRecognitionHasBoundedAftermathAndClearsOnRestorationAndRetry()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings"); ((Behaviour)player).enabled = false;
            var tension = TensionOwner(); Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 6, false);
            Get<Light>(player, "flashlight").enabled = true;
            var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var wall = Cube("CloudQA tension recognition cover", origin + Vector3.right * 3 + Vector3.up * 1.5f, new Vector3(.3f, 3, 8));
            Physics.SyncTransforms(); yield return Delay(.4f); Assert.That(Get<int>(tension, "RecognitionEvents"), Is.Zero);
            Call(shell, "AdjustSettings", -1f, 0f); wall.SetActive(false); Physics.SyncTransforms();
            yield return Wait(() => Get<int>(tension, "RecognitionEvents") == 1 && Get<float>(tension, "Stress") > .6f, 3,
                "True visual recognition did not raise stress independently of master mute");
            Assert.That(Get<float>(player, "Stamina"), Is.EqualTo(1));
            Assert.That(Get<int>(player.GetComponent(RequireType("DetectionFeedback")), "CuesPlayed"), Is.EqualTo(1));
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True);
            float unrefreshedHold = Get<float>(tension, "HoldRemaining"), unrefreshedAge = Get<float>(tension, "LastStimulusAge");
            for (int repeat = 0; repeat < 5; repeat++) Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            yield return TensionAdmissionFrames();
            Assert.That(Get<int>(tension, "RecognitionEvents"), Is.EqualTo(1), "Coalesced valid recognition refreshed the subjective bed");
            Assert.That(Get<int>(player.GetComponent(RequireType("DetectionFeedback")), "CuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<float>(tension, "HoldRemaining"), Is.LessThanOrEqualTo(unrefreshedHold));
            Assert.That(Get<float>(tension, "LastStimulusAge"), Is.GreaterThanOrEqualTo(unrefreshedAge));
            ((Behaviour)enemy).enabled = false;
            Call(shell, "RestoreDefaultSettings"); Call(shell, "Pause");
            float frozen = Get<float>(tension, "Stress"), target = Get<float>(tension, "TargetStress");
            float hold = Get<float>(tension, "HoldRemaining"), age = Get<float>(tension, "LastStimulusAge");
            yield return Delay(.4f);
            Assert.That(Get<float>(tension, "Stress"), Is.EqualTo(frozen)); Assert.That(Get<float>(tension, "TargetStress"), Is.EqualTo(target));
            Assert.That(Get<float>(tension, "HoldRemaining"), Is.EqualTo(hold)); Assert.That(Get<float>(tension, "LastStimulusAge"), Is.EqualTo(age));
            Call(shell, "Resume");
            // Explicit repeated contact controls test aggregation, independently
            // from the natural footsteps above. Weak feet must not pin strong fear.
            var steps = AudioSteps(enemy); ((Behaviour)steps).enabled = false;
            for (int i = 0; i < 20; i++) { TensionContact(steps); yield return new WaitForSeconds(.28f); }
            Assert.That(Get<int>(tension, "ContactEvents"), Is.GreaterThan(2));
            Assert.That(Get<float>(tension, "TargetStress"), Is.LessThanOrEqualTo(.35001f), "Ordinary repeated feet kept refreshing stronger recognition hold");
            Assert.That(Get<float>(tension, "Stress"), Is.LessThanOrEqualTo(.36f));
            Assert.That(Get<bool>(player.GetComponent(RequireType("DetectionFeedback")), "Active"), Is.False, "Recognition overlay outlived its original brief window");
            Get<AudioSource>(steps, "MovementSource").Stop();
            yield return Wait(() => TensionQuiet(tension), 4, "Aftermath did not settle without new evidence");
            TensionRecognition(); yield return Wait(() => Get<float>(tension, "Stress") > .5f, 2, "Controlled lifecycle stimulus failed");
            foreach (string id in new[] { "register", "ribbon", "record" }) Assert.That((bool)Call(session, "Collect", id), Is.True);
            foreach (string id in new[] { "music-roster", "archive-record", "nursery-tag" }) Inspect(id);
            Assert.That((bool)Call(session, "Collect", "restore"), Is.True); yield return TensionAdmissionFrames();
            Assert.That(TensionQuiet(tension), Is.True, "Restoration carried threat aftermath into peaceful escape");
            int events = Get<int>(tension, "PerceivedEvents"); TensionRecognition(); TensionContact(steps); yield return TensionAdmissionFrames();
            Assert.That(Get<int>(tension, "PerceivedEvents"), Is.EqualTo(events)); Assert.That(TensionQuiet(tension), Is.True);
            var pulse = Get<AudioSource>(tension, "PulseSource"); var air = Get<AudioSource>(tension, "AirSource");
            var pulseClip = Get<AudioClip>(tension, "PulseClip"); var airClip = Get<AudioClip>(tension, "AirClip");
            Call(session, "TryEscape"); Assert.That(Get<bool>(session, "Escaped"), Is.True);
            var previous = session; Call(shell, "Restart", true); yield return RecoveryRebind(previous);
            Assert.That(tension == null && pulse == null && air == null && pulseClip == null && airClip == null, Is.True, "Old player-local tension resources survived retry");
            var fresh = TensionOwner(); Assert.That(Components("PerceivedTension").Length, Is.EqualTo(1));
            Assert.That(Get<int>(fresh, "PerceivedEvents"), Is.Zero); Assert.That(TensionQuiet(fresh), Is.True);
            Call(RequireType("PerceivedTension"), "ReportRecognition", previous); yield return TensionAdmissionFrames();
            Assert.That(Get<int>(fresh, "PerceivedEvents"), Is.Zero, "A stale previous-scene event reached the new player");
            Debug.Log("HAPPYTOY_TENSION_PASS aftermath: true visual recognition under mute, pause freeze, weak-contact cap without high-hold pinning, finite quiet, restored escape and native retry cleanup");
        }

        IEnumerator CaptureTensionSegment(string name, Component tension, AudioSource foot, float seconds, int rate,
            List<float> montage, List<CloudPerceivedTensionTests.Segment> segments, int flushed = 0, bool requireContactDuck = false)
        {
            var pulse = Get<AudioSource>(tension, "PulseSource"); var air = Get<AudioSource>(tension, "AirSource");
            var segment = new CloudPerceivedTensionTests.Segment {
                name = name, offset = montage.Count, rate = rate, flushedSamples = flushed,
                stressStart = Get<float>(tension, "Stress"), targetStart = Get<float>(tension, "TargetStress"), bedGainStart = Get<float>(tension, "BedGain"),
                masterVolume = AudioListener.volume, paused = AudioListener.pause, softened = Get<bool>(shell, "ReducedMotion"),
                audibleSources = new[] { pulse, air, foot }.Where(item => item && !item.mute).Select(item => item.clip ? item.clip.name : item.name).ToArray()
            };
            var samples = new List<float>(); float expectedFootVolume = foot.volume;
            var recording = CaptureAudio(samples, Mathf.RoundToInt(rate * seconds) * 2, 8);
            try
            {
                // Drive the unchanged capture helper while observing the real
                // deferred duck. No pre-record wait discards the foot transient.
                while (recording.MoveNext())
                {
                    yield return recording.Current;
                    if (requireContactDuck)
                    {
                        segment.observedContactDuck |= Get<float>(tension, "DuckRemaining") > 0;
                        Assert.That(foot.volume, Is.EqualTo(expectedFootVolume).Within(.0001f), "Bed duck altered the real enemy source");
                    }
                }
            }
            finally { (recording as IDisposable)?.Dispose(); }
            if (requireContactDuck) Assert.That(segment.observedContactDuck, Is.True, "Admitted contact never ducked the new bed");
            segment.samples = samples.Count; segment.seconds = samples.Count / (rate * 2f);
            segment.stressEnd = Get<float>(tension, "Stress"); segment.targetEnd = Get<float>(tension, "TargetStress"); segment.bedGainEnd = Get<float>(tension, "BedGain");
            segment.pulseVolume = pulse.volume; segment.airVolume = air.volume; segment.pulsePitch = pulse.pitch; segment.airPitch = air.pitch; segment.footVolume = foot.volume; segment.footPitch = foot.pitch;
            segment.rms = Math.Sqrt(samples.Average(value => (double)value * value)); segment.peak = samples.Max(value => Math.Abs(value));
            segment.clipped = samples.Count(value => Math.Abs(value) >= 1); segments.Add(segment); montage.AddRange(samples);
        }
        IEnumerator FlushTensionAudio(float seconds, int rate)
        { var samples = new List<float>(); yield return CaptureAudio(samples, Mathf.RoundToInt(rate * seconds) * 2, 8); tensionDiscardedBatches.Add(samples.Count); }

        [UnityTest, Timeout(65000)]
        public IEnumerator RealPerceivedTensionMixHasQuietBoundsAndLeavesFootstepHeadroom()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings"); ((Behaviour)player).enabled = false;
            ((Behaviour)Get<Component>(player, "Feedback")).enabled = false;
            var tension = TensionOwner(); var pulse = Get<AudioSource>(tension, "PulseSource"); var air = Get<AudioSource>(tension, "AirSource");
            foreach (var bed in new[] { pulse, air })
            { Assert.That(bed.spatialBlend, Is.Zero); Assert.That(bed.ignoreListenerPause || bed.ignoreListenerVolume, Is.False); }
            Assert.That(pulse.pitch, Is.InRange(.9f, 1.2f)); Assert.That(air.pitch, Is.EqualTo(1));
            var enemy = AudioStalkerAt(MainCorridorPoint(), "CloudQA Hwacat tension mix"); ((Behaviour)enemy).enabled = false;
            var steps = AudioSteps(enemy); ((Behaviour)steps).enabled = false;
            var foot = Get<AudioSource>(steps, "MovementSource"); var clip = Get<AudioClip>(steps, "MovementClip");
            Vector3 fixture = new Vector3(900, 20, 900); PlacePlayer(fixture, false); PositionAudioFixture(steps, fixture + Vector3.forward * 3);
            TensionSelectSources(pulse, air); foot.Stop();
            Assert.That(AudioSettings.speakerMode, Is.EqualTo(AudioSpeakerMode.Stereo));
            int rate = AudioSettings.outputSampleRate; Assert.That(rate, Is.EqualTo(48000), "Bounded tension montage uses the native cloud48k stereo contract");
            Assert.That(44 + rate * 4 * 7.3f, Is.LessThan(1500000));
            tensionDiscardedBatches.Clear();
            var montage = new List<float>(); var segments = new List<CloudPerceivedTensionTests.Segment>();
            float capture = Time.captureDeltaTime, quietWait = 0; bool started = false;
            try
            {
                Time.captureDeltaTime = 1f / 60f; yield return null;
                started = AudioRenderer.Start(); Assert.That(started, Is.True);
                yield return FlushTensionAudio(.3f, rate);
                yield return CaptureTensionSegment("quiet-baseline", tension, foot, .4f, rate, montage, segments, rate * 2 * 3 / 10);
                TensionRecognition();
                yield return CaptureTensionSegment("recognition-onset", tension, foot, .9f, rate, montage, segments);
                yield return CaptureTensionSegment("after-evidence-stops", tension, foot, .9f, rate, montage, segments);
                float quietStart = Time.time;
                yield return Wait(() => TensionQuiet(tension), 8, "Max recognition aftermath never reached actual quiet"); quietWait = Time.time - quietStart;
                yield return FlushTensionAudio(.3f, rate);
                yield return CaptureTensionSegment("eventual-quiet", tension, foot, .4f, rate, montage, segments, rate * 2 * 3 / 10);
                TensionSelectSources(foot); yield return FlushTensionAudio(.3f, rate);
                foot.pitch = 1; foot.PlayOneShot(clip);
                yield return CaptureTensionSegment("footstep-only", tension, foot, .75f, rate, montage, segments, rate * 2 * 3 / 10);
                foot.Stop(); TensionSelectSources(pulse, air); TensionRecognition(); yield return FlushTensionAudio(.35f, rate);
                yield return CaptureTensionSegment("bed-only", tension, foot, .75f, rate, montage, segments, rate * 2 * 35 / 100);
                TensionRecognition(); yield return FlushTensionAudio(.35f, rate);
                TensionSelectSources(pulse, air, foot); TensionContact(steps);
                yield return CaptureTensionSegment("bed-with-contact", tension, foot, .75f, rate, montage, segments, rate * 2 * 35 / 100, true);
                foot.Stop(); TensionSelectSources(pulse, air); Call(shell, "AdjustSettings", -1f, 0f);
                yield return FlushTensionAudio(.2f, rate);
                yield return CaptureTensionSegment("master-zero", tension, foot, .4f, rate, montage, segments, rate * 2 / 5);
                Call(shell, "RestoreDefaultSettings"); TensionRecognition(); yield return FlushTensionAudio(.3f, rate); Call(shell, "Pause");
                yield return FlushTensionAudio(.2f, rate);
                yield return CaptureTensionSegment("paused", tension, foot, .4f, rate, montage, segments, rate * 2 / 5);
                Call(shell, "Resume");
                yield return CaptureTensionSegment("resumed", tension, foot, .65f, rate, montage, segments);
                TensionRecognition(); yield return FlushTensionAudio(.6f, rate); float normalGain = Get<float>(tension, "BedGain");
                Call(shell, "ToggleReducedMotion"); yield return FlushTensionAudio(.2f, rate);
                Assert.That(Get<float>(tension, "BedGain"), Is.LessThan(normalGain * .6f), "Comfort gain did not soften the original bed");
                Assert.That(pulse.pitch, Is.EqualTo(1)); Assert.That(air.pitch, Is.EqualTo(1));
                yield return CaptureTensionSegment("softened", tension, foot, .6f, rate, montage, segments, rate * 2 / 5);
                Call(session, "Finish", false); yield return FlushTensionAudio(.2f, rate);
                yield return CaptureTensionSegment("result-silence", tension, foot, .4f, rate, montage, segments, rate * 2 / 5);
                Assert.That(segments.Count, Is.EqualTo(12));
                CloudExperienceTests.Artifact("perceived-tension-mix.wav", CloudExperienceTests.Wave(montage, rate, 2));
                var report = new CloudPerceivedTensionTests.Report { rate = rate, samples = montage.Count, quietWaitGameSeconds = quietWait, discardedCaptureSamples = tensionDiscardedBatches.Sum(), discardedCaptureBatches = tensionDiscardedBatches.ToArray(), segments = segments.ToArray() };
                CloudExperienceTests.Artifact("perceived-tension-mix.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
                var byName = segments.ToDictionary(segment => segment.name);
                foreach (var segment in segments) Assert.That(segment.clipped, Is.Zero, "Actual tension mix clipped: " + segment.name);
                foreach (string label in new[] { "quiet-baseline", "eventual-quiet", "master-zero", "paused", "result-silence" })
                    Assert.That(byName[label].peak, Is.LessThan(.00001), "Expected engine silence: " + label);
                foreach (string label in new[] { "recognition-onset", "after-evidence-stops", "footstep-only", "bed-only", "bed-with-contact", "resumed", "softened" })
                    Assert.That(byName[label].rms, Is.GreaterThan(.00001), "Expected real audible PCM: " + label);
                Assert.That(byName["after-evidence-stops"].stressEnd, Is.LessThan(byName["recognition-onset"].stressEnd));
                Assert.That(byName["bed-only"].rms, Is.LessThan(byName["footstep-only"].rms * .3), "The restrained local bed is too loud relative to a useful nearby footstep");
                Assert.That(byName["bed-with-contact"].footVolume, Is.EqualTo(byName["footstep-only"].footVolume).Within(.0001f));
                Assert.That(byName["paused"].stressEnd, Is.EqualTo(byName["paused"].stressStart));
                Assert.That(TensionQuiet(tension), Is.True);
                Debug.Log("HAPPYTOY_TENSION_PASS PCM: exact quiet baseline/onset/aftermath/quiet, full original foot cue versus low bed/combined headroom, master-zero/pause/resume/comfort/result; isolated actual engine mix, not subjective listening certification");
            }
            finally { if (started) AudioRenderer.Stop(); Time.captureDeltaTime = capture; }
        }
    }
}
