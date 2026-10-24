using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator LadyBlackoutKeepsOnlyPlayerIncenseLightAndVisibleGuidingSmokeUntilBurnout()
        {
            yield return LadyPrepare(); LadyCollect(0, 3);
            ((Behaviour)player).enabled = false;
            PlacePlayer((Vector3)Call(LadyRun, "CellPosition", 0) + Vector3.up * .03f, false);
            var camera = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight");
            torch.enabled = false; camera.transform.localRotation = Quaternion.Euler(10, -30, 0);
            var ritual = Get<Component>(player, "Rituals");
            Assert.That((bool)Call(ritual, "UseIncense"), Is.True);
            yield return Delay(.4f); LadyCollect(3, 4); yield return Delay(2);
            var smoke = Get<Component>(ritual, "Smoke"); var plume = smoke.GetComponent<MeshRenderer>();
            var ember = Get<Light>(smoke, "EmberLight");
            Call(shell, "Pause"); float remaining = Get<float>(smoke, "Remaining");
            float smokeTime = Get<float>(smoke.GetComponent(RequireType("IncenseSmokeTrail")), "SimulationTime");
            Texture2D visible = null, noSmoke = null, dark = null;
            try
            {
                visible = EntranceRitualFrame(camera, 960, 540);
                Assert.That(plume.enabled && ember.isActiveAndEnabled, Is.True);
                Assert.That(ember.range, Is.InRange(2f, 2.6f)); Assert.That(ember.intensity, Is.InRange(1.3f, 1.7f));
                Assert.That(RenderSettings.ambientIntensity, Is.Zero); Assert.That(RenderSettings.reflectionIntensity, Is.Zero);
                Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).All(l => l == torch || l == ember || !l.isActiveAndEnabled), Is.True,
                    "The incense exception reopened an environment fixture");
                var offering = Get<Component>(Get<Component>(LadyRun, "AltarChamber"), "Offering");
                Assert.That(Get<IReadOnlyList<Vector3>>(smoke, "TargetPoints").Single(), Is.EqualTo(offering.transform.position));
                plume.enabled = false; noSmoke = EntranceRitualFrame(camera, 960, 540);
                ember.enabled = false; dark = EntranceRitualFrame(camera, 960, 540);
                CloudExperienceTests.Artifact("lady-incense-blackout.png", visible.EncodeToPNG());
                CloudExperienceTests.Artifact("lady-incense-light-only.png", noSmoke.EncodeToPNG());
                CloudExperienceTests.Artifact("lady-incense-extinguished.png", dark.EncodeToPNG());
                TestContext.Out.WriteLine("Incense world light position=" + ember.transform.position + " eye=" + camera.transform.position +
                    " smoke pixels=" + HauntedChanged(visible, noSmoke) + " ember pixels=" + HauntedChanged(noSmoke, dark) +
                    " lit mean=" + LadyFrameMean(noSmoke) + " dark mean=" + LadyFrameMean(dark));
                Assert.That(HauntedChanged(visible, noSmoke), Is.GreaterThan(20), "Actual camera has no visible smoke direction in blackout");
                Assert.That(HauntedChanged(noSmoke, dark), Is.GreaterThan(20), "The lighter-sized ember casts no visible local light");
                Assert.That(LadyFrameMean(dark), Is.LessThan(.001));
            }
            finally
            {
                if (visible) Object.Destroy(visible); if (noSmoke) Object.Destroy(noSmoke); if (dark) Object.Destroy(dark);
                if (plume) plume.enabled = true; if (ember) ember.enabled = true;
            }
            yield return Delay(.15f);
            Assert.That(Get<float>(smoke, "Remaining"), Is.EqualTo(remaining));
            Assert.That(Get<float>(smoke.GetComponent(RequireType("IncenseSmokeTrail")), "SimulationTime"), Is.EqualTo(smokeTime));
            Call(shell, "Resume"); Object.Destroy(smoke.gameObject); yield return null;
            // A fresh stick lit AFTER the blackout must also survive the scope.
            Call(ritual, "Restore", 1, 0, 0); Assert.That((bool)Call(ritual, "UseIncense"), Is.True);
            smoke = Get<Component>(ritual, "Smoke"); ember = Get<Light>(smoke, "EmberLight");
            yield return Delay(.2f); var frame = EntranceRitualFrame(camera, 320, 180); Object.Destroy(frame);
            Assert.That(ember.isActiveAndEnabled && smoke.GetComponent<MeshRenderer>().enabled, Is.True);
            Call(ritual, "RestoreSmoke", .04f); smoke = Get<Component>(ritual, "Smoke"); ember = Get<Light>(smoke, "EmberLight");
            yield return Wait(() => !smoke, 2, "Expired incense retained its owned light/smoke");
            Assert.That(ember == null, Is.True);
        }

        Component LadyDropSnapper(Vector3 point, Vector3 velocity)
        {
            var root = new GameObject("QA real Lady snapper impact"); root.transform.position = point;
            var projectile = root.AddComponent(RequireType("FirecrackerProjectile")); Call(projectile, "Launch", velocity); return projectile;
        }
        IEnumerator LadySnapperFixture()
        {
            yield return LadyPrepare(); LadyCollect(0, 4);
            yield return Wait(() => Get<Component>(LadyFinale, "Actor"), 4, "Lady did not spawn");
            ((Behaviour)player).enabled = false;
            foreach (var door in CheckpointItems("Door")) Call(door, "OpenForPursuer");
            var actor = Get<Component>(LadyFinale, "Actor");
            var floor = player.transform.position; floor.y = .03f;
            Assert.That(actor.GetComponent<NavMeshAgent>().Warp(floor), Is.True);
            // Use the real wide entrance floor, away from walls and player contact.
            actor.transform.rotation = Quaternion.identity;
            PlacePlayer(floor + Vector3.forward * 7, false); Physics.SyncTransforms();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LadyFrontSnapperHoldsAgentAttackAndActualPoseHalfSecondWithPauseTalismanAndSlowTime()
        {
            yield return LadySnapperFixture();
            var actor = Get<Component>(LadyFinale, "Actor"); var motion = Get<Component>(actor, "Motion");
            var agent = actor.GetComponent<NavMeshAgent>(); var ritual = Get<Component>(player, "Rituals");
            var at = actor.transform.position;
            var fire = LadyDropSnapper(at + Vector3.forward * 1.25f + Vector3.up * .08f, Vector3.down * 2);
            yield return Wait(() => Get<bool>(fire, "Exploded"), 2, "Real front floor impact never occurred");
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.GreaterThan(0));
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
            at = actor.transform.position; float clock = Get<float>(motion, "Clock");
            var head = Get<Transform>(motion, "Head"); var pose = head.localRotation;
            Call(shell, "Pause"); float held = Get<float>(actor, "SnapperHoldRemaining");
            yield return Delay(.15f); Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.EqualTo(held));
            Assert.That(Get<float>(motion, "Clock"), Is.EqualTo(clock));
            Call(shell, "Resume"); Call(ritual, "Restore", 1, 0, 1); Assert.That((bool)Call(ritual, "UseTalisman"), Is.True);
            yield return Delay(.15f); Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.EqualTo(held));
            Call(ritual, "EndEffect");
            PlacePlayer(at + Vector3.forward * .5f, false); Call(session, "ToggleCheatSafeMode");
            // Real scaled gameplay: half a second must last longer at quarter speed.
            Time.timeScale = .25f; float start = Get<float>(actor, "SnapperHoldRemaining");
            yield return Delay(.5f);
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.InRange(start - .2f, start - .08f));
            Assert.That(Vector3.Distance(actor.transform.position, at), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(head.localRotation, pose), Is.LessThan(.01f));
            Assert.That(Get<float>(motion, "Clock"), Is.EqualTo(clock)); Assert.That(agent.isStopped, Is.True);
            Assert.That(Get<bool>(Get<Component>(LadyFinale, "PulseAudio"), "Captured"), Is.False, "Stopped Lady caught a player in contact range");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            PlacePlayer(at + Vector3.forward * 7, false); Call(session, "ToggleCheatSafeMode");
            Time.timeScale = 1;
            yield return Wait(() => Get<float>(actor, "SnapperHoldRemaining") == 0, 2, "Half-second flinch did not finish");
            yield return Delay(.12f); Assert.That(Get<float>(motion, "Clock"), Is.GreaterThan(clock));
            Assert.That(Vector3.Distance(actor.transform.position, at), Is.GreaterThan(.02f));
            Assert.That(Get<bool>(actor, "TrapImmune"), Is.True);
            Assert.That((bool)Call(RequireType("EnemyTrapRestraint"), "Eligible", agent), Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LadyDirectSnapperHitUsesSameForecastContactAndWallsBlockDistantOrRearBursts()
        {
            yield return LadySnapperFixture(); var actor = Get<Component>(LadyFinale, "Actor");
            var at = actor.transform.position; actor.GetComponent<NavMeshAgent>().speed = 0;
            var rear = LadyDropSnapper(at - Vector3.forward + Vector3.up * .08f, Vector3.down * 2);
            yield return Wait(() => Get<bool>(rear, "Exploded"), 2, "Rear impact missed floor");
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.Zero);
            var far = LadyDropSnapper(at + Vector3.forward * 3 + Vector3.up * .08f, Vector3.down * 2);
            yield return Wait(() => Get<bool>(far, "Exploded"), 2, "Distant impact missed floor");
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.Zero);
            var wall = Cube("QA opaque Lady cover", at + new Vector3(0, 1.1f, .75f), new Vector3(2, 2.2f, .15f));
            Physics.SyncTransforms();
            var covered = LadyDropSnapper(at + Vector3.forward * 1.4f + Vector3.up * .08f, Vector3.down * 2);
            yield return Wait(() => Get<bool>(covered, "Exploded"), 2, "Covered impact missed floor");
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.Zero, "Flinch crossed a solid wall");
            // Forecast must hit the nearer wall rather than the Lady capsule.
            var origin = at + new Vector3(0, 1.1f, 1.6f); var velocity = Vector3.back * 14;
            var forecast = Call(RequireType("FirecrackerTrajectory"), "Forecast", origin, velocity, new Vector3[61]);
            Assert.That(Get<bool>(forecast, "HasContact"), Is.True);
            Assert.That(Get<Vector3>(forecast, "ContactPoint").z, Is.GreaterThan(at.z + .7f));
            Object.Destroy(wall); yield return null; Physics.SyncTransforms();
            // A direct hit from BEHIND still works, and shares the aim trajectory.
            origin = at + new Vector3(0, 1.1f, -1.5f); velocity = Vector3.forward * 14;
            forecast = Call(RequireType("FirecrackerTrajectory"), "Forecast", origin, velocity, new Vector3[61]);
            var fire = LadyDropSnapper(origin, velocity);
            yield return Wait(() => Get<bool>(fire, "Exploded"), 2, "The pea passed through Lady's body");
            Assert.That(Vector3.Distance(Get<Vector3>(fire, "FirstContactPoint"), Get<Vector3>(forecast, "ContactPoint")), Is.LessThan(.01f));
            Assert.That(Get<float>(actor, "SnapperHoldRemaining"), Is.GreaterThan(0));
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
            var contact = actor.GetComponentInChildren(RequireType("LadySnapperTarget"));
            Assert.That(contact.GetComponent<Collider>().isTrigger && contact.gameObject.layer == 2, Is.True);
            Object.Destroy(actor.gameObject); yield return null;
            forecast = Call(RequireType("FirecrackerTrajectory"), "Forecast", origin, velocity, new Vector3[61]);
            Assert.That(Get<Vector3>(forecast, "ContactPoint"), Is.Not.EqualTo(Get<Vector3>(fire, "FirstContactPoint")), "Destroyed Lady left a query capsule behind");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LadySnapperActualMixerKeepsOnePlayerBurstButRespectsPauseAndMasterMute()
        {
            yield return LadySnapperFixture(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)Get<Component>(player, "Feedback")).enabled = false;
            var actor = Get<Component>(LadyFinale, "Actor"); actor.GetComponent<NavMeshAgent>().speed = 0;
            var point = actor.transform.position + Vector3.forward * 1.25f + Vector3.up * .08f;
            var segments = new List<CloudExternalAudioTests.Segment>(); var montage = new List<float>();
            int rate = AudioSettings.outputSampleRate; float capture = Time.captureDeltaTime; bool started = false;
            try
            {
                Time.captureDeltaTime = 1f / 60; yield return null;
                var fire = LadyDropSnapper(point, Vector3.down * 2); var source = fire.GetComponent<AudioSource>();
                var clip = RecordedPrivate<AudioClip>(fire, "crack"); ExternalMuteExcept(source);
                started = StartOwnedAudioRenderer(); Assert.That(started, Is.True); AudioListener.volume = 1;
                yield return ExternalCapture("lady-player-snapper", source, clip, "firecracker", .4f, rate, montage, segments);
                Assert.That(AudioListener.pause && source.ignoreListenerPause && !source.ignoreListenerVolume, Is.True);
                Assert.That(segments.Last().rms, Is.GreaterThan(.00002)); Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
                fire = LadyDropSnapper(point, Vector3.down * 2); source = fire.GetComponent<AudioSource>();
                clip = RecordedPrivate<AudioClip>(fire, "crack"); ExternalMuteExcept(source); Call(shell, "Pause");
                yield return ExternalFlush(rate, .2f);
                yield return ExternalCapture("lady-snapper-paused", source, clip, "firecracker", .2f, rate, montage, segments);
                Assert.That(segments.Last().peak, Is.LessThan(.00001)); Assert.That(Get<int>(fire, "PopsPlayed"), Is.Zero);
                Call(shell, "Resume"); AudioListener.volume = 0;
                yield return ExternalCapture("lady-snapper-master-muted", source, clip, "firecracker", .4f, rate, montage, segments);
                Assert.That(segments.Last().peak, Is.LessThan(.00001)); Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
                CloudExperienceTests.Artifact("lady-snapper-actual-mixer.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report { scope = "Quiet pre-device real snapper floor impact during Lady blackout; one recorded three-tick voice, pause and master mute.", segments = segments.ToArray() }, true)));
            }
            finally { AudioListener.volume = 0; if (started) StopOwnedAudioRenderer(); Time.captureDeltaTime = capture; }
        }
    }
}
