using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Controlled physics/encounter placements are explicit. Real keys, controller
    // motion, enemy attacks and UI pointer dispatch exercise production behavior.
    // These fixtures are separate from the unchanged authored survival route.
    internal static class CloudRecoveryFlowTests { }

    public sealed partial class CloudPlayModeTests
    {
        string RecoveryPage => Get<object>(shell, "Screen").ToString();
        IEnumerator RecoveryPulse(Key key)
        {
            Keys(key); yield return Delay(.08f); Keys(); yield return Delay(.08f);
        }
        IEnumerator RecoveryUiReady()
        {
            var view = One("GameShellView"); Call(view, "SetCaptureSize", 1280, 720);
            yield return null; yield return null;
            yield return Wait(() => Get<VisualElement>(view, "Root")?.panel != null &&
                Get<VisualElement>(view, "Root").worldBound.width > 0, 3, "Real UI panel did not lay out");
        }
        IEnumerator RecoveryClick(string id)
        {
            var view = One("GameShellView");
            yield return Wait(() => Get<VisualElement>(view, "Root")?.Q<Button>(id) != null, 3, "Missing real UI control " + id);
            var root = Get<VisualElement>(view, "Root"); var button = root.Q<Button>(id);
            Assert.That(button.enabledInHierarchy, Is.True, "Disabled UI control " + id);
            Assert.That(button.worldBound.width, Is.GreaterThan(0));
            Vector2 position = button.worldBound.center; var target = root.panel.Pick(position);
            Assert.That(target != null && (target == button || button.Contains(target)), Is.True, "Real pointer target is obscured: " + id);
            // Same supported pointer path as the existing FlowAudit; no direct
            // invocation of the button's action or private Clickable callback.
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = position, button = 0, clickCount = 1 }))
                target.SendEvent(down);
            yield return Delay(.04f);
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 0, clickCount = 1 }))
                target.SendEvent(up);
            yield return Delay(.15f);
        }
        IEnumerator RecoveryRebind(Component previous)
        {
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20, "UI action did not load a fresh game scene");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            Assert.That(Get<bool>(shell, "IsReloading"), Is.False); Assert.That(Get<string>(shell, "ReloadError"), Is.Empty);
        }
        void RecoveryUiOwnership()
        {
            Assert.That(Components("GameShellView").Length, Is.EqualTo(1));
            Assert.That(Components("GameShell").Length, Is.EqualTo(1));
            Assert.That(Components("PlayerMotor").Length, Is.EqualTo(1));
            Assert.That(Components("DetectionFeedback").Length, Is.EqualTo(1));
            Assert.That(Resources.FindObjectsOfTypeAll<PanelSettings>().Count(item => item.name == "HappyToy runtime UI panel"), Is.EqualTo(1), "Native UI panels accumulated across retries");
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(item => item.name == "HappyToy UI capture"), Is.EqualTo(1), "Native UI capture targets accumulated across retries");
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(item => item.name.StartsWith("Recognition grain ", StringComparison.Ordinal)), Is.EqualTo(8), "Recognition textures accumulated across retries");
        }

        [UnityTest, Timeout(65000)]
        public IEnumerator PhysicalSprintChargesTravelAndRecoversAtObstaclesWithoutInputLatch()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            float oldFixed = Time.fixedDeltaTime, oldCapture = Time.captureDeltaTime;
            var origin = new Vector3(500, 0, 500);
            Cube("CloudQA recovery floor", origin - Vector3.up * .2f, new Vector3(120, .4f, 120));
            PlacePlayer(origin + Vector3.up * .02f);
            yield return Wait(() => Get<bool>(player, "Grounded"), 3, "Recovery fixture never grounded");
            try
            {
                float stamina = Get<float>(player, "Stamina"); Vector3 before = player.transform.position;
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => Get<bool>(player, "Running") && Get<float>(player, "ActualSpeed") > 3.5f, 2, "Open-floor sprint did not move");
                yield return new WaitForSeconds(.65f);
                Assert.That(player.transform.position.z - before.z, Is.GreaterThan(2));
                Assert.That(Get<float>(player, "Stamina"), Is.LessThan(stamina - .08f));
                Keys(); yield return null;
                var wall = Cube("CloudQA recovery blocking wall", player.transform.position + Vector3.forward * .65f + Vector3.up * 1.5f, new Vector3(12, 3, .2f));
                Physics.SyncTransforms();
                float wallApproach = player.transform.position.z; int wallMove = Get<int>(player, "MovementUpdates");
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => Get<int>(player, "MovementUpdates") > wallMove && player.transform.position.z > wallApproach + .05f &&
                    (Get<CollisionFlags>(player, "LastCollision") & CollisionFlags.Sides) != 0 &&
                    Get<float>(player, "ActualSpeed") < .12f && !Get<bool>(player, "Running"), 3, "Actual wall contact did not settle blocked sprint");
                stamina = Get<float>(player, "Stamina"); before = player.transform.position;
                int footsteps = Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed");
                yield return new WaitForSeconds(.85f);
                Assert.That(Vector3.Distance(player.transform.position, before), Is.LessThan(.04f));
                Assert.That(Get<float>(player, "Stamina"), Is.GreaterThan(stamina + .06f), "Blocked sprint kept charging instead of recovering");
                Assert.That(Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed"), Is.EqualTo(footsteps));
                Assert.That(Get<float>(player, "FootstepNoiseRadius"), Is.Zero);
                var status = Get<VisualElement>(One("GameShellView"), "Root").Q<Label>("player-status");
                Assert.That(status.text, Is.EqualTo("멈춰서 숨을 고릅니다"), "HUD still claims running while physically stopped");
                stamina = Get<float>(player, "Stamina"); before = player.transform.position;
                yield return KeysObserved(Key.W, Key.D, Key.LeftShift);
                yield return Wait(() => Get<bool>(player, "Running") && Get<float>(player, "ActualSpeed") > 2, 2, "Diagonal wall slide was mistaken for a stop");
                yield return new WaitForSeconds(.45f);
                Assert.That(player.transform.position.x - before.x, Is.GreaterThan(1));
                Assert.That(Get<float>(player, "Stamina"), Is.LessThan(stamina - .05f), "Actual diagonal travel became free sprint");
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => !Get<bool>(player, "Running"), 2, "Player did not settle against the wall again");
                wall.SetActive(false); Physics.SyncTransforms();
                // Keep exactly the same held input across obstruction removal.
                yield return Wait(() => Get<bool>(player, "Running") && Get<float>(player, "ActualSpeed") > 3.5f, 2, "Removing the obstacle required a new key press to escape");
                Keys(); yield return null;

                // Explicit physical slope fixture, not a teleport claimed as stair
                // traversal. Coarser 20Hz fixed simulation must still charge travel.
                var ramp = Cube("CloudQA recovery ramp", origin + new Vector3(0, 1, 12), new Vector3(6, .4f, 8));
                ramp.transform.rotation = Quaternion.Euler(-15, 0, 0); Physics.SyncTransforms();
                PlacePlayer(origin + new Vector3(0, .02f, 7.3f));
                yield return Wait(() => Get<bool>(player, "Grounded"), 2, "Ramp approach did not ground");
                Time.fixedDeltaTime = .05f;
                stamina = Get<float>(player, "Stamina"); before = player.transform.position;
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => player.transform.position.z > before.z + 3 && player.transform.position.y > before.y + .5f, 3, "CharacterController never physically climbed the slope");
                Assert.That(Get<bool>(player, "Running"), Is.True);
                Assert.That(Get<float>(player, "Stamina"), Is.LessThan(stamina - .08f));
                Call(shell, "Pause"); stamina = Get<float>(player, "Stamina"); before = player.transform.position;
                yield return Delay(.3f);
                Assert.That(Get<float>(player, "Stamina"), Is.EqualTo(stamina));
                Assert.That(player.transform.position, Is.EqualTo(before)); Assert.That(Get<bool>(player, "Running"), Is.False);
                Call(shell, "Resume"); Keys(); yield return null;
                Time.fixedDeltaTime = oldFixed;

                // Apply the existing public curse effect, not a private speed or
                // stamina setter. Slower real sprint remains physical exertion.
                PlacePlayer(origin + new Vector3(20, .02f, 0));
                yield return Wait(() => Get<bool>(player, "Grounded"), 2, "Curse fixture did not ground");
                Call(player, "ApplyCurse", 1.5f); stamina = Get<float>(player, "Stamina");
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => Get<bool>(player, "Running") && Get<float>(player, "ActualSpeed") > 1.5f && Get<float>(player, "ActualSpeed") < 2.5f, 2, "Curse-slowed sprint did not retain its real speed");
                yield return new WaitForSeconds(.35f);
                Assert.That(Get<float>(player, "Stamina"), Is.LessThan(stamina - .04f));

                // A fixed capture clock deliberately creates multiple real physics
                // steps per rendered frame. This is catch-up coverage, not an FPS claim.
                Time.fixedDeltaTime = .02f; Time.captureDeltaTime = .1f;
                yield return null; int moves = Get<int>(player, "MovementUpdates"); yield return null;
                Assert.That(Get<int>(player, "MovementUpdates") - moves, Is.GreaterThan(1), "No actual fixed-step catch-up was exercised");
                yield return Wait(() => Get<bool>(player, "SprintExhausted"), 8, "Real physical sprint never exhausted");
                int heldFrames = 0;
                while (Get<float>(player, "Stamina") < .3f && heldFrames++ < 100)
                {
                    yield return null;
                    Assert.That(Get<bool>(player, "Running"), Is.False, "Held Shift re-sprinted during fixed-step catch-up recovery");
                    Assert.That(Get<bool>(player, "SprintExhausted"), Is.True);
                    Assert.That(Get<float>(player, "ActualSpeed"), Is.LessThan(3), "Exhaustion retained the previous sprint velocity");
                }
                Assert.That(Get<float>(player, "Stamina"), Is.GreaterThanOrEqualTo(.3f));
                yield return KeysObserved(Key.W);
                yield return Wait(() => !Get<bool>(player, "SprintExhausted"), 2, "Releasing Shift after recovery did not rearm sprint");
                yield return KeysObserved(Key.W, Key.LeftShift);
                yield return Wait(() => Get<bool>(player, "Running"), 2, "Recovered sprint never resumed after release");
                Keys(); Time.captureDeltaTime = oldCapture; Time.fixedDeltaTime = oldFixed; yield return null;
                var cabinet = Components("Interactable").Single(item => item.name == "음악실 은신함");
                PlacePlayer(Get<Transform>(cabinet, "outside").position); Call(cabinet, "Use", player);
                Assert.That(Get<bool>(player, "Hidden"), Is.True); stamina = Get<float>(player, "Stamina");
                yield return new WaitForSeconds(.5f);
                Assert.That(Get<float>(player, "Stamina"), Is.GreaterThan(stamina + .035f), "Moving recovery into physics broke cabinet rest");
                Assert.That(Get<bool>(player, "Running"), Is.False);
                Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.False);
                Debug.Log("HAPPYTOY_RECOVERY_PASS sprint: real travel cost, blocked recovery/truthful HUD, diagonal slide, held-input escape, physical slope at20Hz, pause, curse, actual multi-fixed catch-up exhaustion/release and cabinet rest");
            }
            finally { Keys(); Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture; }
        }

        [UnityTest, Timeout(55000)]
        public IEnumerator NurseryComfortRestoresLightDuringPausedSettingsWithoutAdvancingReveal()
        {
            // Do not disable/re-enable AnnexEncounter: disabling it correctly makes
            // cancellation terminal. Keep the real nursery owner alive throughout.
            foreach (string name in new[] { "StoryDirector", "UncatAnnexEvent", "V1HwacatEvent", "LanternMaskEncounter", "WeepingAngelEncounter" })
                foreach (var item in Components(name)) ((Behaviour)item).enabled = false;
            foreach (var enemy in Components("StalkerBrain")) enemy.gameObject.SetActive(false);
            Call(shell, "RestoreDefaultSettings"); Begin(); yield return RecoveryUiReady();
            var encounter = One("AnnexEncounter"); var monster = Get<Component>(encounter, "monster");
            var light = Get<Light>(encounter, "warningLight"); float baseline = light.intensity;
            var source = encounter.GetComponent<AudioSource>();
            var cue = (AudioClip)encounter.GetType().GetField("cue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(encounter);
            Assert.That(source && cue, Is.True);
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Vector3 room = Get<Vector3>(encounter, "roomCenter");
            Assert.That(NavMesh.SamplePosition(room + Vector3.back * 2, out var hit, 1.5f, NavMesh.AllAreas), Is.True);
            Assert.That(Mathf.Abs(hit.position.y - room.y), Is.LessThan(1.6f));
            ((Behaviour)player).enabled = false; PlacePlayer(hit.position, false);
            yield return Wait(() => Get<bool>(encounter, "Triggered"), 2, "Valid authored nursery proximity did not trigger the real reveal");
            float triggeredAt = Time.time;
            yield return Wait(() => Mathf.Abs(light.intensity - baseline) > baseline * .12f, 1, "No actual ordinary warning-light modulation was observed");
            yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            float frozenTime = Time.time; Vector3 frozenMonster = monster.transform.position;
            yield return RecoveryClick("settings"); Assert.That(RecoveryPage, Is.EqualTo("Settings"));
            yield return RecoveryClick("reduced-motion");
            Assert.That(Get<bool>(shell, "ReducedMotion"), Is.True);
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f), "Paused comfort toggle left the modulated light frozen");
            yield return Delay(5.25f);
            Assert.That(Time.time, Is.EqualTo(frozenTime)); Assert.That(Get<bool>(encounter, "Released"), Is.False);
            Assert.That(monster.transform.position, Is.EqualTo(frozenMonster));
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f));
            Assert.That(AudioListener.pause, Is.True);
            yield return RecoveryClick("back"); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            PlacePlayer(room + Vector3.up * 5, false); // Isolate the release clock from lethal contact.
            yield return RecoveryClick("resume"); Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            yield return Wait(() => Get<bool>(encounter, "Released"), 7, "Resume never completed the unchanged five-second reveal");
            Assert.That(Time.time - triggeredAt, Is.GreaterThanOrEqualTo(4.8f), "Paused wall time shortened the real reveal");
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f));
            Assert.That(((Behaviour)monster).enabled, Is.True); Assert.That(monster.GetComponent<NavMeshAgent>().enabled, Is.True);
            Call(session, "Finish", false); yield return Wait(() => Get<bool>(encounter, "Cancelled"), 2, "Result failed to cancel the nursery");
            Assert.That(monster.gameObject.activeSelf, Is.False); Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f));
            var previous = session; yield return RecoveryClick("restart"); yield return RecoveryRebind(previous);
            Assert.That(encounter == null && monster == null && source == null && cue == null && light == null, Is.True, "Nursery-owned runtime resources survived retry");
            Assert.That(Get<bool>(One("AnnexEncounter"), "Triggered"), Is.False);
            Debug.Log("HAPPYTOY_RECOVERY_PASS nursery: valid authored reveal, actual paused Settings toggle, immediate baseline light, >5s wall wait with frozen game clock, normal release, terminal result and retry cleanup");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ActualDeathRetryAndRepeatedUiNavigationKeepOwnedResourcesBounded()
        {
            IsolateThreats(); yield return RecoveryUiReady(); RecoveryUiOwnership();
            yield return RecoveryClick("settings"); Assert.That(RecoveryPage, Is.EqualTo("Settings"));
            yield return RecoveryClick("defaults"); yield return RecoveryClick("reduced-motion");
            Assert.That(Get<bool>(shell, "ReducedMotion"), Is.True);
            yield return RecoveryClick("back"); Assert.That(RecoveryPage, Is.EqualTo("Title"));
            yield return RecoveryClick("begin"); Assert.That(Get<bool>(session, "InputAllowed"), Is.True);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                IsolateThreats(); Assert.That((bool)Call(session, "Collect", "register"), Is.True);
                yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
                float frozen = Time.time;
                yield return RecoveryClick("settings");
                bool large = Get<bool>(shell, "LargeText"); yield return RecoveryClick("large-text");
                Assert.That(Get<bool>(shell, "LargeText"), Is.EqualTo(!large), "A single real click dispatched a duplicated settings callback");
                Assert.That(CloudExperienceTests.Layout(Get<VisualElement>(One("GameShellView"), "Root")), Is.Empty);
                yield return RecoveryClick("back"); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
                yield return RecoveryClick("journal"); Assert.That(RecoveryPage, Is.EqualTo("Journal"));
                var record = Get<VisualElement>(One("GameShellView"), "Root").Q<Label>("record-0");
                Assert.That(record, Is.Not.Null); Assert.That(record.text, Does.Contain("윤서"));
                Assert.That(CloudExperienceTests.Layout(Get<VisualElement>(One("GameShellView"), "Root")), Is.Empty);
                yield return RecoveryClick("back"); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
                Assert.That(Time.time, Is.EqualTo(frozen));
                yield return RecoveryClick("resume"); Assert.That(Get<bool>(session, "InputAllowed"), Is.True);
                ((Behaviour)player).enabled = false;
                Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 1.15f, false);
                Get<Light>(player, "flashlight").enabled = true;
                var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
                yield return Wait(() => Get<bool>(session, "Finished"), 5, "Real close-range recognition/attack did not reach the defeat screen");
                Assert.That(Get<int>(enemy, "AttacksStarted"), Is.EqualTo(1));
                Assert.That(Get<string>(session, "DefeatSource"), Is.EqualTo(enemy.name));
                Assert.That(RecoveryPage, Is.EqualTo("Result")); Assert.That(AudioListener.pause, Is.True);
                yield return null;
                var view = One("GameShellView"); var document = view.GetComponent<UIDocument>(); var panel = document.panelSettings;
                var capture = Get<RenderTexture>(view, "CaptureTarget");
                var oldSources = enemy.GetComponentsInChildren<AudioSource>();
                Assert.That(CloudExperienceTests.Layout(Get<VisualElement>(view, "Root")), Is.Empty);
                var previous = session; yield return RecoveryClick("restart"); yield return RecoveryRebind(previous);
                Assert.That(view == null && document == null && panel == null && capture == null && enemy == null, Is.True, "Old UI/actor native owners survived retry");
                Assert.That(oldSources.All(item => item == null), Is.True, "Old attack/movement source survived retry");
                Assert.That(Get<bool>(session, "Finished"), Is.False); Assert.That(Get<int>(session, "RecordsRecovered"), Is.Zero);
                Assert.That(Get<float>(player, "Stamina"), Is.EqualTo(1).Within(.001f));
                Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(2));
                Assert.That(Get<bool>(shell, "ReducedMotion"), Is.True); Assert.That(Get<bool>(shell, "LargeText"), Is.EqualTo(!large));
                Assert.That(Get<int>(player.GetComponent(RequireType("DetectionFeedback")), "CuesPlayed"), Is.Zero);
                yield return RecoveryUiReady(); RecoveryUiOwnership();
            }
            IsolateThreats(); yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            var beforeTitle = session; yield return RecoveryClick("title"); yield return RecoveryRebind(beforeTitle);
            Assert.That(RecoveryPage, Is.EqualTo("Title")); Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            Assert.That(AudioListener.pause, Is.True); Assert.That(Time.timeScale, Is.Zero);
            yield return RecoveryUiReady(); RecoveryUiOwnership();
            yield return RecoveryClick("settings"); yield return RecoveryClick("back"); Assert.That(RecoveryPage, Is.EqualTo("Title"));
            yield return RecoveryClick("begin"); Assert.That(Get<bool>(session, "InputAllowed"), Is.True);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.Zero);
            Debug.Log("HAPPYTOY_RECOVERY_PASS UI: real pointer settings/back/journal/title, two actual enemy deaths and retry buttons, preserved settings/reset resources, old native objects destroyed and stable owned panel/target/texture counts");
        }
    }
}
