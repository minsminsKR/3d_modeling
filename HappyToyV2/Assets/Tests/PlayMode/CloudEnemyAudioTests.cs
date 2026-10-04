using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Controlled scene/NavMesh fixtures and isolated real-engine PCM. These are not
    // a human listening verdict or another full-route survival/performance result.
    internal static class CloudEnemyAudioTests
    {
        [Serializable] internal sealed class Segment
        {
            public string name, profile, clip, phase = "complete-cue";
            public int sampleOffset, samples, channels = 2, sampleRate, flushSamples, clippedSamples;
            public float clipSeconds, captureSeconds, listenerVolume, sourceGain, sourceVolume, sourceMinDistance, sourceMaxDistance, cutoffHz, sourceDistance;
            public string[] playbackProfiles;
            public Vector3[] playbackPositions;
            public bool paused, occluded, floorOccluded, inRange, physicalCaptionAudibility, simultaneous;
            public Vector3 listenerPosition, sourcePosition, listenerForward, listenerRight, listenerUp;
            public double peak, rms, leftRms, rightRms;
        }
        [Serializable] internal sealed class Report
        {
            public string capture = "UnityAudioRendererMainOutput", wave = "enemy-movement-acoustics.wav";
            public string scope = "Positioned isolated production movement clips; real colliders for wall/slab; natural authored lantern transformation. Separate tests cover actual locomotion and attack events.";
            public bool syntheticPcm = false, fullRouteEvidence = false, deviceListeningCertification = false;
            public int sampleRate, channels = 2, totalSamples;
            public Vector3 wallCenter, wallSize, slabCenter, slabSize;
            public Segment[] segments;
        }
        internal static Segment Measure(string label, Component steps, List<float> values, int rate, int offset, int flushed,
            bool simultaneous)
        {
            var source = Get<AudioSource>(steps, "MovementSource"); var acoustics = Get<Component>(steps, "MovementAcoustics");
            var camera = Get<Camera>(Get<Component>(One("GameSession"), "player"), "eyes");
            double left = 0, right = 0;
            for (int i = 0; i < values.Count; i += 2) { left += values[i] * (double)values[i]; right += values[i + 1] * (double)values[i + 1]; }
            return new Segment {
                name = label, profile = Get<object>(steps, "Profile").ToString(), clip = Get<AudioClip>(steps, "MovementClip").name,
                sampleOffset = offset, samples = values.Count, sampleRate = rate, flushSamples = flushed,
                clipSeconds = Get<AudioClip>(steps, "MovementClip").length, captureSeconds = values.Count / (rate * 2f),
                listenerVolume = AudioListener.volume, paused = AudioListener.pause, sourceGain = Get<float>(acoustics, "Gain"),
                sourceVolume = source.volume, sourceMinDistance = source.minDistance, sourceMaxDistance = source.maxDistance,
                playbackProfiles = new[] { Get<object>(steps, "Profile").ToString() }, playbackPositions = new[] { source.transform.position },
                cutoffHz = Get<float>(acoustics, "CutoffFrequency"), occluded = Get<bool>(acoustics, "Occluded"),
                floorOccluded = Get<bool>(acoustics, "FloorOccluded"), inRange = Get<bool>(acoustics, "InAudibleRange"),
                physicalCaptionAudibility = (bool)Call(acoustics, "IsAudible", source),
                listenerPosition = camera.transform.position, sourcePosition = source.transform.position,
                listenerForward = camera.transform.forward, listenerRight = camera.transform.right, listenerUp = camera.transform.up,
                sourceDistance = Vector3.Distance(camera.transform.position, source.transform.position), simultaneous = simultaneous,
                peak = values.Max(value => Math.Abs(value)), rms = Math.Sqrt(values.Average(value => value * (double)value)),
                leftRms = Math.Sqrt(left / (values.Count / 2)), rightRms = Math.Sqrt(right / (values.Count / 2)),
                clippedSamples = values.Count(value => Math.Abs(value) >= 1)
            };
        }
        internal static string Fingerprint(AudioClip clip)
        {
            Assert.That(clip, Is.Not.Null);
            var values = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(values, 0), Is.True);
            Assert.That(values.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) < 1), Is.True);
            Assert.That(values.Any(value => Math.Abs(value) > .001f), Is.True, "Production cue is silent");
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = new byte[values.Length * sizeof(float)]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }
        internal static void SourceContract(AudioSource source)
        {
            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1));
            Assert.That(source.dopplerLevel, Is.Zero);
            Assert.That(source.ignoreListenerPause || source.ignoreListenerVolume, Is.False);
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.minDistance, Is.GreaterThan(0));
            Assert.That(source.maxDistance, Is.InRange(source.minDistance + 1, 30));
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        Component AudioStalkerAt(Vector3 at, string name)
        {
            var actor = new GameObject(name); actor.SetActive(false); actor.transform.position = at;
            var agent = actor.AddComponent<NavMeshAgent>(); agent.radius = .3f; agent.height = 1.8f; agent.updateRotation = true;
            var brain = actor.AddComponent(RequireType("StalkerBrain"));
            Set(brain, "player", player); Set(brain, "patrolSpeed", 1.45f); Set(brain, "chaseSpeed", 3.5f);
            actor.SetActive(true);
            Assert.That(agent.Warp(at), Is.True, "Audio actor must use the real authored NavMesh");
            return brain;
        }
        static Component AudioSteps(Component actor)
        {
            var steps = actor.GetComponent(RequireType("StalkerFootsteps"));
            Assert.That(steps, Is.Not.Null, "Actual actor has no movement audio binding: " + actor.name);
            return steps;
        }

        [UnityTest, Timeout(35000)]
        public IEnumerator EnemyMovementAudioFollowsTravelAndSurvivesPauseAndWarp()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.up * 5, false);
            var enemy = AudioStalkerAt(origin, "CloudQA Cyclopse movement");
            var agent = enemy.GetComponent<NavMeshAgent>(); var steps = AudioSteps(enemy);
            Assert.That(Get<object>(steps, "Profile").ToString(), Is.EqualTo("Cyclopse"));
            float stride = Get<float>(steps, "StepDistance");
            Assert.That(stride, Is.InRange(.25f, 2f));
            Assert.That((bool)Call(enemy, "HearNoise", origin + Vector3.right * 7, 12f), Is.True);
            float travel = 0; int previousSteps = 0; Vector3 previous = enemy.transform.position;
            float until = Time.realtimeSinceStartup + 6;
            while (Get<int>(steps, "StepsPlayed") < 3 && Time.realtimeSinceStartup < until)
            {
                yield return null;
                Vector3 delta = enemy.transform.position - previous; delta.y = 0; travel += delta.magnitude; previous = enemy.transform.position;
                int count = Get<int>(steps, "StepsPlayed");
                Assert.That(count - previousSteps, Is.InRange(0, 1), "One frame emitted a burst of steps"); previousSteps = count;
            }
            Assert.That(Get<int>(steps, "StepsPlayed"), Is.GreaterThanOrEqualTo(3), "Natural navigation emitted no movement cues");
            Assert.That(travel, Is.GreaterThan(stride * 2), "Cue count is disconnected from real travel");
            Call(shell, "Pause"); int pausedCount = Get<int>(steps, "StepsPlayed"); Vector3 pausedAt = enemy.transform.position;
            yield return Delay(.3f);
            Assert.That(Get<int>(steps, "StepsPlayed"), Is.EqualTo(pausedCount));
            Assert.That(enemy.transform.position, Is.EqualTo(pausedAt));
            Call(shell, "Resume");
            yield return Wait(() => Get<int>(steps, "StepsPlayed") > pausedCount, 3, "Movement cues never resumed");
            ((Behaviour)enemy).enabled = false; agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero;
            yield return null; int stoppedCount = Get<int>(steps, "StepsPlayed");
            yield return Delay(.35f);
            Assert.That(Get<int>(steps, "StepsPlayed"), Is.EqualTo(stoppedCount), "Stationary actor made phantom footsteps");
            int warps = Get<int>(steps, "TeleportsSuppressed");
            Assert.That(agent.Warp(origin), Is.True); Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That(Get<int>(steps, "StepsPlayed"), Is.EqualTo(stoppedCount), "Warp emitted a footstep burst");
            Assert.That(Get<int>(steps, "TeleportsSuppressed"), Is.GreaterThan(warps));
            // The real Cyclopse reveal walks while its AI brain is disabled. Movement
            // audio belongs to actual travel and must keep working in that state.
            agent.speed = .85f; agent.isStopped = false;
            Assert.That(agent.SetDestination(origin + Vector3.right * 4), Is.True);
            yield return Wait(() => Get<int>(steps, "StepsPlayed") > stoppedCount, 4, "Scripted brain-disabled travel became silent");
            Assert.That(((Behaviour)enemy).enabled, Is.False);
            CloudEnemyAudioTests.SourceContract(Get<AudioSource>(steps, "MovementSource"));
            Debug.Log("HAPPYTOY_ENEMY_AUDIO_PASS movement: real navigation/cadence, stationary silence, pause/resume, warp suppression, brain-disabled scripted walk");
        }

        [UnityTest, Timeout(35000)]
        public IEnumerator EnemyAttackUsesDistinctCueOnceAndReloadDestroysOwnedAudio()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 1.15f, false);
            Get<Light>(player, "flashlight").enabled = true;
            var enemy = AudioStalkerAt(origin, "CloudQA Cyclopse attack");
            Set(enemy, "patrolSpeed", 0f); Set(enemy, "chaseSpeed", 0f);
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var steps = AudioSteps(enemy);
            var movement = Get<AudioClip>(steps, "MovementClip"); var attack = Get<AudioClip>(steps, "AttackClip");
            Assert.That(CloudEnemyAudioTests.Fingerprint(attack), Is.Not.EqualTo(CloudEnemyAudioTests.Fingerprint(movement)),
                "Attack anticipation is still just the walking waveform");
            var movementSource = Get<AudioSource>(steps, "MovementSource"); var attackSource = Get<AudioSource>(steps, "AttackSource");
            CloudEnemyAudioTests.SourceContract(attackSource);
            Assert.That(attackSource, Is.Not.SameAs(movementSource), "Walking pitch can still retune an in-flight warning");
            yield return Wait(() => Get<int>(enemy, "AttacksStarted") == 1, 3, "Actual recognition/contact never began an attack");
            Assert.That(Get<int>(steps, "AttackCuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<int>(steps, "CabinetAttackCuesPlayed"), Is.Zero);
            Call(shell, "Pause"); yield return Delay(.3f);
            Assert.That(Get<int>(steps, "AttackCuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<bool>(enemy, "AttackActive"), Is.True);
            PlacePlayer(origin + Vector3.right * 5, false); Call(shell, "Resume");
            yield return Wait(() => !Get<bool>(enemy, "AttackActive"), 3, "Attack did not complete after a genuine dodge");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Get<int>(steps, "AttackCuesPlayed"), Is.EqualTo(Get<int>(enemy, "AttacksStarted")));
            var previousSession = session; Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previousSession, 20, "Audio retry did not load a fresh scene");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            Assert.That(enemy == null && steps == null && movementSource == null && attackSource == null, Is.True);
            Assert.That(movement == null && attack == null, Is.True, "Procedural audio clips leaked across restart");
            foreach (var fresh in Components("StalkerFootsteps"))
            {
                Assert.That(Get<int>(fresh, "AttackCuesPlayed"), Is.Zero);
                Assert.That(Get<int>(fresh, "StepsPlayed"), Is.Zero);
            }
            Debug.Log("HAPPYTOY_ENEMY_AUDIO_PASS attack: true LOS/contact warning, distinct waveform/source, one cue per attack, paused clock, dodge and retry resource cleanup");
        }

        void PlaceForLanternContact(Component lantern)
        {
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 candidate = lantern.transform.position + direction * .6f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .2f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - lantern.transform.position.y) > .2f) continue;
                PlacePlayer(candidate, false);
                // First appearance now requires an actual camera-visible encounter.
                // Orient this existing contact fixture without bypassing that gate;
                // the original curse, timing and waveform assertions remain intact.
                var eyes = Get<Camera>(player, "eyes");
                eyes.transform.rotation = Quaternion.LookRotation(lantern.transform.TransformPoint(new Vector3(0, 1.25f, 0)) - eyes.transform.position);
                if ((bool)Call(lantern, "CanSeePlayer")) return;
            }
            Assert.Fail("No real same-floor contact-range NavMesh/LOS fixture at the authored lantern start");
        }

        [UnityTest, Timeout(35000)]
        public IEnumerator AuthoredEnemyFamiliesBindDistinctCuesAndLanternChangesNaturally()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            var fingerprints = new HashSet<string>();
            var authored = Components("StalkerBrain");
            Assert.That(authored.Length, Is.EqualTo(4), "The protected scene's four stalkers changed");
            foreach (var enemy in authored)
            {
                string expected = new[] { "Cyclopse", "Hwacat", "Uncat", "Baby" }
                    .Single(kind => enemy.name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0);
                ((Behaviour)enemy).enabled = false;
                var motion = enemy.GetComponent(RequireType("V1MonsterMotion")); if (motion) ((Behaviour)motion).enabled = false;
                enemy.gameObject.SetActive(true); yield return null;
                var steps = AudioSteps(enemy);
                Assert.That(Get<object>(steps, "Profile").ToString(), Is.EqualTo(expected), enemy.name);
                var movement = Get<AudioClip>(steps, "MovementClip"); var attack = Get<AudioClip>(steps, "AttackClip");
                Assert.That(fingerprints.Add(CloudEnemyAudioTests.Fingerprint(movement)), Is.True, "Authored identities share the same movement waveform");
                Assert.That(CloudEnemyAudioTests.Fingerprint(attack), Is.Not.EqualTo(CloudEnemyAudioTests.Fingerprint(movement)));
                CloudEnemyAudioTests.SourceContract(Get<AudioSource>(steps, "MovementSource"));
                CloudEnemyAudioTests.SourceContract(Get<AudioSource>(steps, "AttackSource"));
                enemy.gameObject.SetActive(false);
            }
            var lantern = One("LanternMaskEncounter"); var lanternSteps = AudioSteps(lantern);
            Assert.That(Get<object>(lanternSteps, "Profile").ToString(), Is.EqualTo("Lantern"));
            Assert.That(fingerprints.Add(CloudEnemyAudioTests.Fingerprint(Get<AudioClip>(lanternSteps, "MovementClip"))), Is.True);
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
            PlaceForLanternContact(lantern);
            Get<Light>(player, "flashlight").enabled = true; ((Behaviour)lantern).enabled = true;
            yield return Wait(() => Get<int>(lantern, "CursesApplied") == 1, 5, "Real lantern sight/attack did not apply its curse");
            Assert.That(Get<bool>(lantern, "Transformed"), Is.False);
            PlacePlayer(lantern.transform.position + Vector3.up * 5, false);
            yield return Wait(() => Get<bool>(lantern, "Transformed"), 7, "The real five-second transformation did not complete");
            yield return null;
            Assert.That(Get<object>(lanternSteps, "Profile").ToString(), Is.EqualTo("Wraith"));
            Assert.That(fingerprints.Add(CloudEnemyAudioTests.Fingerprint(Get<AudioClip>(lanternSteps, "MovementClip"))), Is.True,
                "Transformed lantern kept its floating movement identity");
            Assert.That(fingerprints.Count, Is.EqualTo(6));
            Assert.That(Get<int>(lanternSteps, "AttackCuesPlayed"), Is.Zero, "Shared locomotion duplicated the lantern's existing warning");
            var mannequin = One("WeepingAngelEncounter");
            var mannequinSources = mannequin.GetComponentsInChildren<AudioSource>(true);
            Assert.That(mannequinSources.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(mannequinSources.Any(source => source.GetComponent(RequireType("EnemyAcoustics"))), Is.True,
                "Existing mannequin cue missed shared acoustic treatment");
            Debug.Log("HAPPYTOY_ENEMY_AUDIO_PASS identities: four actual authored stalkers, lantern, real transformed lantern; six distinct movement waveforms and mannequin acoustic binding");
        }

        static void PositionAudioFixture(Component steps, Vector3 position)
        {
            var agent = steps.GetComponent<NavMeshAgent>();
            if (agent.enabled) agent.enabled = false;
            steps.transform.position = position;
            Physics.SyncTransforms();
        }
        IEnumerator CaptureEnemySegment(string label, Component primary, int rate, List<float> montage,
            List<CloudEnemyAudioTests.Segment> segments, Component[] simultaneous = null)
        {
            var chosen = simultaneous ?? new[] { primary };
            foreach (var steps in chosen) ((Behaviour)steps).enabled = false; // Separate cadence fixture covers Update; retain bound production acoustics.
            var sources = chosen.Select(steps => Get<AudioSource>(steps, "MovementSource")).ToArray();
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            { source.Stop(); source.mute = !sources.Contains(source); }
            // Advance the actual mixer, discard old one-shot/filter tails and let
            // production gain/occlusion settle. These samples never stand in for a cue.
            var flush = new List<float>(); yield return CaptureAudio(flush, Mathf.CeilToInt(rate * .7f) * 2, 6);
            foreach (var steps in chosen)
            {
                var source = Get<AudioSource>(steps, "MovementSource"); var clip = Get<AudioClip>(steps, "MovementClip");
                Assert.That(clip.length, Is.LessThanOrEqualTo(.351f), "The bounded full-cue capture window must be redesigned for longer production clips");
                source.pitch = 1; source.PlayOneShot(clip);
            }
            var values = new List<float>(); yield return CaptureAudio(values, Mathf.CeilToInt(rate * .45f) * 2, 6);
            var segment = CloudEnemyAudioTests.Measure(label, primary, values, rate, montage.Count, flush.Count, simultaneous != null);
            segment.playbackProfiles = chosen.Select(steps => Get<object>(steps, "Profile").ToString()).ToArray();
            segment.playbackPositions = sources.Select(item => item.transform.position).ToArray();
            segments.Add(segment); montage.AddRange(values);
        }

        IEnumerator CaptureEnemyPauseResume(Component steps, int rate, List<float> montage, List<CloudEnemyAudioTests.Segment> segments)
        {
            ((Behaviour)steps).enabled = false;
            var source = Get<AudioSource>(steps, "MovementSource");
            foreach (var other in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            { other.Stop(); other.mute = other != source; }
            var flush = new List<float>(); yield return CaptureAudio(flush, Mathf.CeilToInt(rate * .7f) * 2, 6);
            source.pitch = 1; source.PlayOneShot(Get<AudioClip>(steps, "MovementClip"));
            var lead = new List<float>(); yield return CaptureAudio(lead, Mathf.CeilToInt(rate * .06f) * 2, 6);
            var before = CloudEnemyAudioTests.Measure("before-pause", steps, lead, rate, montage.Count, flush.Count, false);
            before.phase = "in-flight cue prefix"; segments.Add(before); montage.AddRange(lead);
            Call(shell, "Pause");
            var pauseFlush = new List<float>(); yield return CaptureAudio(pauseFlush, Mathf.CeilToInt(rate * .2f) * 2, 6);
            var paused = new List<float>(); yield return CaptureAudio(paused, Mathf.CeilToInt(rate * .45f) * 2, 6);
            var hold = CloudEnemyAudioTests.Measure("paused", steps, paused, rate, montage.Count, pauseFlush.Count, false);
            hold.phase = "already-playing cue frozen by listener pause"; segments.Add(hold); montage.AddRange(paused);
            Call(shell, "Resume");
            // Deliberately no Play/PlayOneShot/Stop here: only the frozen original
            // one-shot may supply this remaining waveform.
            var resumed = new List<float>(); yield return CaptureAudio(resumed, Mathf.CeilToInt(rate * .45f) * 2, 6);
            var tail = CloudEnemyAudioTests.Measure("resumed", steps, resumed, rate, montage.Count, 0, false);
            tail.phase = "same in-flight cue tail; no replay"; segments.Add(tail); montage.AddRange(resumed);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator RealEnemyMixProvesIdentitySpatialAttenuationAndControls()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            ((Behaviour)Get<Component>(player, "Feedback")).enabled = false;
            var camera = Get<Camera>(player, "eyes"); camera.transform.localRotation = Quaternion.identity;
            Vector3 origin = MainCorridorPoint(), fixture = new Vector3(800, 20, 800);
            var stalkers = new[] { "Cyclopse", "Hwacat", "Uncat", "Baby" }.Select(kind => {
                var brain = AudioStalkerAt(origin, "CloudQA " + kind + " isolated PCM");
                ((Behaviour)brain).enabled = false;
                return AudioSteps(brain);
            }).ToArray();
            var lantern = One("LanternMaskEncounter"); var lanternSteps = AudioSteps(lantern);
            var lanternAgent = lantern.GetComponent<NavMeshAgent>(); Vector3 lanternHome = lantern.transform.position;
            var allSteps = stalkers.Concat(new[] { lanternSteps }).ToArray();
            foreach (var steps in allSteps) PositionAudioFixture(steps, fixture + Vector3.forward * 3);
            PlacePlayer(fixture, false); camera.transform.localRotation = Quaternion.identity;
            var source = Get<AudioSource>(stalkers[0], "MovementSource");
            var wall = Cube("CloudQA enemy sound wall", fixture + new Vector3(0, 1.3f, 1.5f), new Vector3(8, 6, .3f));
            var slab = Cube("CloudQA enemy sound floor slab", fixture + Vector3.up * 3, new Vector3(10, .3f, 10));
            wall.SetActive(false); slab.SetActive(false); Physics.SyncTransforms();
            Assert.That(AudioSettings.speakerMode, Is.EqualTo(AudioSpeakerMode.Stereo));
            int rate = AudioSettings.outputSampleRate, segmentSamples = Mathf.CeilToInt(rate * .45f) * 2;
            Assert.That(rate, Is.EqualTo(48000), "Cloud stereo montage has an explicit 48kHz native-output contract");
            Assert.That(44L + 16L * segmentSamples * 2 + Mathf.CeilToInt(rate * .06f) * 4L, Is.LessThanOrEqualTo(1500000),
                "Worker's native sample rate exceeds the unchanged bounded stereo evidence transport; do not truncate or fabricate PCM");
            var montage = new List<float>(); var segments = new List<CloudEnemyAudioTests.Segment>();
            float captureDelta = Time.captureDeltaTime; bool started = false;
            try
            {
                Time.captureDeltaTime = 1f / 60f; yield return null;
                started = AudioRenderer.Start(); Assert.That(started, Is.True, "Actual Unity main-output capture is unavailable");
                yield return CaptureEnemySegment("identity-Lantern", lanternSteps, rate, montage, segments);
                foreach (var steps in stalkers)
                    yield return CaptureEnemySegment("identity-" + Get<object>(steps, "Profile"), steps, rate, montage, segments);
                // Obtain the transformed identity from the real encounter. No private
                // setter, replacement clip or mock sound generator supplies this cue.
                lantern.transform.position = lanternHome; lanternAgent.enabled = true;
                Assert.That(lanternAgent.Warp(lanternHome), Is.True);
                PlaceForLanternContact(lantern);
                Get<Light>(player, "flashlight").enabled = true;
                Assert.That((bool)Call(session, "Collect", "register"), Is.True);
                Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
                ((Behaviour)lantern).enabled = true; ((Behaviour)lanternSteps).enabled = true;
                yield return Wait(() => Get<int>(lantern, "CursesApplied") == 1, 5, "Audio fixture lantern never applied its real curse");
                PlacePlayer(lanternHome + Vector3.up * 5, false);
                yield return Wait(() => Get<bool>(lantern, "Transformed"), 7, "Audio fixture lantern never transformed naturally");
                yield return Wait(() => Get<object>(lanternSteps, "Profile").ToString() == "Wraith", 1, "Natural transformation did not update the movement profile");
                ((Behaviour)lantern).enabled = false;
                PositionAudioFixture(lanternSteps, fixture + Vector3.forward * 3); PlacePlayer(fixture, false);
                // The genuine first-sight contact fixture turns the listener. Restore
                // the isolated world's declared basis before its directional samples.
                camera.transform.localRotation = Quaternion.identity;
                Physics.SyncTransforms(); yield return null;
                Assert.That(Vector3.Angle(camera.transform.forward, Vector3.forward), Is.LessThan(.001f));
                Assert.That(Vector3.Angle(camera.transform.right, Vector3.right), Is.LessThan(.001f));
                Assert.That(Vector3.Angle(camera.transform.up, Vector3.up), Is.LessThan(.001f));
                Assert.That(Get<object>(lanternSteps, "Profile").ToString(), Is.EqualTo("Wraith"));
                yield return CaptureEnemySegment("identity-Wraith", lanternSteps, rate, montage, segments);
                var primary = stalkers[0];
                PositionAudioFixture(primary, fixture + Vector3.forward * (source.maxDistance + 5));
                yield return CaptureEnemySegment("beyond-range", primary, rate, montage, segments);
                PositionAudioFixture(primary, fixture + Vector3.forward * 3); wall.SetActive(true); Physics.SyncTransforms();
                yield return CaptureEnemySegment("wall-blocked", primary, rate, montage, segments);
                wall.SetActive(false); PositionAudioFixture(primary, fixture + Vector3.up * 5);
                yield return CaptureEnemySegment("vertical-open", primary, rate, montage, segments);
                slab.SetActive(true); Physics.SyncTransforms();
                yield return CaptureEnemySegment("floor-blocked", primary, rate, montage, segments);
                slab.SetActive(false); PositionAudioFixture(primary, fixture + new Vector3(-4, 0, 3));
                yield return CaptureEnemySegment("left", primary, rate, montage, segments);
                PositionAudioFixture(primary, fixture + new Vector3(4, 0, 3));
                yield return CaptureEnemySegment("right", primary, rate, montage, segments);
                PositionAudioFixture(primary, fixture + Vector3.forward * 3);
                Call(shell, "AdjustSettings", -1f, 0f);
                yield return CaptureEnemySegment("master-zero", primary, rate, montage, segments);
                Call(shell, "RestoreDefaultSettings");
                yield return CaptureEnemyPauseResume(primary, rate, montage, segments);
                for (int i = 0; i < stalkers.Length; i++) PositionAudioFixture(stalkers[i], fixture + new Vector3((i - 1.5f) * .6f, 0, 2));
                yield return CaptureEnemySegment("four-nearby-stalkers", primary, rate, montage, segments, stalkers);
                Assert.That(segments.Count, Is.EqualTo(17));
                CloudExperienceTests.Artifact("enemy-movement-acoustics.wav", CloudExperienceTests.Wave(montage, rate, 2));
                var report = new CloudEnemyAudioTests.Report { sampleRate = rate, totalSamples = montage.Count, segments = segments.ToArray(),
                    wallCenter = wall.transform.position, wallSize = wall.transform.localScale, slabCenter = slab.transform.position, slabSize = slab.transform.localScale };
                CloudExperienceTests.Artifact("enemy-movement-acoustics.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
                var byName = segments.ToDictionary(segment => segment.name);
                foreach (var segment in segments)
                {
                    Assert.That(segment.clippedSamples, Is.Zero, "Actual mixed PCM clips: " + segment.name);
                    Assert.That(double.IsNaN(segment.rms) || double.IsInfinity(segment.rms), Is.False);
                }
                foreach (var segment in segments.Where(segment => segment.name.StartsWith("identity-")))
                {
                    Assert.That(segment.rms, Is.GreaterThan(.00005), "Silent production identity: " + segment.name);
                    Assert.That(segment.occluded, Is.False, "Clear isolated fixture self-occludes: " + segment.name);
                    Assert.That(segment.captureSeconds, Is.GreaterThan(segment.clipSeconds + .08f));
                }
                var near = byName["identity-Cyclopse"]; var blocked = byName["wall-blocked"];
                Assert.That(byName["beyond-range"].inRange, Is.False);
                Assert.That(byName["beyond-range"].physicalCaptionAudibility, Is.False, "Beyond-range sound remains caption-eligible");
                Assert.That(byName["beyond-range"].peak, Is.LessThan(.00001), "Distant enemy leaks past its finite audible range");
                Assert.That(blocked.occluded, Is.True); Assert.That(blocked.floorOccluded, Is.False);
                Assert.That(blocked.rms, Is.LessThan(near.rms * .65), "Real wall failed to reduce the real engine mix");
                Assert.That(blocked.cutoffHz, Is.LessThan(near.cutoffHz * .5f));
                var vertical = byName["vertical-open"]; var floor = byName["floor-blocked"];
                Assert.That(vertical.occluded || vertical.floorOccluded, Is.False, "Height alone incorrectly muffles an open stairwell-like path");
                Assert.That(vertical.rms, Is.GreaterThan(.00005));
                Assert.That(floor.occluded && floor.floorOccluded, Is.True);
                Assert.That(floor.sourceGain, Is.LessThan(blocked.sourceGain), "A blocked different floor needs stronger separation");
                Assert.That(floor.rms, Is.LessThan(vertical.rms * .65), "Solid floor failed to reduce the real engine mix");
                foreach (string side in new[] { "left", "right" })
                {
                    var sample = byName[side];
                    Assert.That(Vector3.Angle(sample.listenerForward, Vector3.forward), Is.LessThan(.001f));
                    Assert.That(Vector3.Angle(sample.listenerRight, Vector3.right), Is.LessThan(.001f));
                    float lateral = Vector3.Dot(sample.sourcePosition - sample.listenerPosition, sample.listenerRight);
                    Assert.That(side == "left" ? lateral < 0 : lateral > 0, Is.True, "Stereo fixture mislabeled listener-relative side");
                }
                Assert.That(byName["left"].leftRms, Is.GreaterThan(byName["left"].rightRms * 1.1));
                Assert.That(byName["right"].rightRms, Is.GreaterThan(byName["right"].leftRms * 1.1));
                Assert.That(byName["master-zero"].listenerVolume, Is.Zero);
                Assert.That(byName["master-zero"].physicalCaptionAudibility, Is.True, "Master mute incorrectly removed physical caption eligibility");
                Assert.That(byName["master-zero"].peak, Is.LessThan(.00001), "Master-zero leaks enemy PCM");
                Assert.That(byName["before-pause"].rms, Is.GreaterThan(.00005), "No real in-flight cue existed before pause");
                Assert.That(byName["paused"].paused, Is.True);
                Assert.That(byName["paused"].peak, Is.LessThan(.00001), "Paused enemy PCM leaks");
                Assert.That(byName["resumed"].rms, Is.GreaterThan(.00005), "Resume did not continue the already-playing enemy cue");
                Assert.That(byName["four-nearby-stalkers"].rms, Is.GreaterThan(.00005));
                Debug.Log("HAPPYTOY_ENEMY_AUDIO_PASS PCM: six complete identity cues; finite distance; real wall/slab versus open vertical; stereo sides; master-zero; pause/resume; four-enemy unclipped overlap. Positioned fixtures, not a device listening verdict.");
            }
            finally
            {
                if (started) AudioRenderer.Stop(); Time.captureDeltaTime = captureDelta;
                Object.Destroy(wall); Object.Destroy(slab);
            }
        }
    }
}
