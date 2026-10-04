using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Real loaded scene, engine physics and NavMesh. Controlled setup is explicit;
    // these tests do not claim manual survival balance, rendered quality or audio mixing.
    public sealed partial class CloudPlayModeTests
    {
        Component session, player, shell;
        Keyboard keyboard, previousKeyboard;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInputBehavior;
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly HashSet<string> existing = new HashSet<string>();
        float oldTimeScale, oldVolume;
        bool oldAudioPause, oldBackgroundRun;
        int oldFrameRate;
        CursorLockMode oldCursorLock;
        bool oldCursorVisible;
        Scene cleanupScene;
        int dynamicInputUpdates;
        bool observedCPress, observedWHeld;
        static readonly string[] FloatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        static readonly string[] IntKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };

        [UnitySetUp]
        public IEnumerator LoadRealAuthoredScene()
        {
            oldTimeScale = Time.timeScale; oldVolume = AudioListener.volume; oldAudioPause = AudioListener.pause;
            oldBackgroundRun = Application.runInBackground; oldFrameRate = Application.targetFrameRate;
            oldCursorLock = UnityEngine.Cursor.lockState; oldCursorVisible = UnityEngine.Cursor.visible;
            oldBackground = InputSystem.settings.backgroundBehavior; previousKeyboard = Keyboard.current;
            oldEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            existing.Clear(); floats.Clear(); ints.Clear();
            dynamicInputUpdates = 0; observedCPress = observedWHeld = false;
            InputSystem.onAfterUpdate += ObserveInput;
            foreach (string key in FloatKeys) { if (PlayerPrefs.HasKey(key)) existing.Add(key); floats[key] = PlayerPrefs.GetFloat(key); }
            foreach (string key in IntKeys) { if (PlayerPrefs.HasKey(key)) existing.Add(key); ints[key] = PlayerPrefs.GetInt(key); }
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // IgnoreFocus alone does not route keyboards into the game's state buffer
            // when the batch Editor has no focused Game View (Input System 1.19).
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode,
                Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView));
            Assert.That(Application.unityVersion, Is.EqualTo("6000.6.0f1"));
            Assert.That(Application.CanStreamedLevelBeLoaded(ScenePath), Is.True, "The protected scene must be in build settings");
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return Wait(() => load.isDone, 30, "Authored scene load did not finish");
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            Assert.That(player, Is.Not.Null); Assert.That(shell, Is.Not.Null);
            Assert.That(Get<bool>(session, "requireAnnexRecords"), Is.True);
            Assert.That(Get<string>(shell, "ReloadError"), Is.Empty);
            Assert.That(Get<bool>(shell, "IsReloading"), Is.False);
        }

        [UnityTearDown]
        public IEnumerator RestoreStateAndUnload()
        {
            try
            {
                InputSystem.onAfterUpdate -= ObserveInput;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                previousKeyboard = null;
                // Only unload our actual game scene. A failed setup must not destroy
                // the UTF runner's own scene before it can report that failure.
                var game = SceneManager.GetSceneByPath(ScenePath);
                if (game.IsValid() && game.isLoaded)
                {
                    cleanupScene = SceneManager.CreateScene("CloudQA cleanup " + Guid.NewGuid().ToString("N"));
                    SceneManager.SetActiveScene(cleanupScene);
                    var unload = SceneManager.UnloadSceneAsync(game);
                    if (unload != null) yield return Wait(() => unload.isDone, 20, "Authored scene cleanup timed out");
                }
            }
            finally
            {
                // Cleanup only: don't let a failed reload assertion poison another
                // fixture. All behavior assertions above use real public game APIs.
                try
                {
                    var field = RequireType("GameShell").GetField("restartGate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    Assert.That(field, Is.Not.Null, "Reload gate cleanup contract changed");
                    Call(field.GetValue(null), "Cancel");
                }
                finally
                {
                    foreach (string key in FloatKeys) if (existing.Contains(key)) PlayerPrefs.SetFloat(key, floats[key]); else PlayerPrefs.DeleteKey(key);
                    foreach (string key in IntKeys) if (existing.Contains(key)) PlayerPrefs.SetInt(key, ints[key]); else PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    InputSystem.settings.backgroundBehavior = oldBackground;
                    InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInputBehavior;
                    Time.timeScale = oldTimeScale; AudioListener.pause = oldAudioPause; AudioListener.volume = oldVolume;
                    Application.runInBackground = oldBackgroundRun; Application.targetFrameRate = oldFrameRate;
                    UnityEngine.Cursor.lockState = oldCursorLock; UnityEngine.Cursor.visible = oldCursorVisible;
                    session = player = shell = null;
                }
            }
        }

        void Begin() { Call(shell, "Begin"); Assert.That(Get<bool>(session, "InputAllowed"), Is.True); }
        void Keys(params Key[] held)
        {
            if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(keyboard.added && keyboard.enabled, Is.True, "Fixture keyboard is not active");
            Assert.That(Keyboard.current, Is.SameAs(keyboard), "Fixture keyboard is not current");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
        }
        IEnumerator KeysObserved(params Key[] held)
        {
            Keys(held);
            yield return Wait(() => Keyboard.current == keyboard && held.All(key => keyboard[key].isPressed), 2,
                "Queued keyboard state never reached the game input buffer; editorMode=" + InputSystem.settings.editorInputBehaviorInPlayMode +
                ", background=" + InputSystem.settings.backgroundBehavior + ", focused=" + Application.isFocused, InputDiagnostics);
            Assert.That(Get<bool>(session, "InputAllowed"), Is.True, "Keyboard reached the device but gameplay input is paused");
        }
        void ObserveInput()
        {
            if (keyboard == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            dynamicInputUpdates++;
            observedCPress |= keyboard.cKey.wasPressedThisFrame;
            observedWHeld |= keyboard.wKey.isPressed;
        }
        string InputDiagnostics()
        {
            return "dynamicUpdates=" + dynamicInputUpdates + ", CPress=" + observedCPress + ", WHeld=" + observedWHeld +
                ", inputAllowed=" + Get<bool>(session, "InputAllowed") + ", crouching=" + Get<bool>(player, "Crouching") +
                ", eyeY=" + Get<Camera>(player, "eyes").transform.localPosition.y + ", grounded=" + Get<bool>(player, "Grounded") +
                ", speed=" + Get<float>(player, "ActualSpeed") + ", position=" + player.transform.position +
                ", footsteps=" + Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed");
        }
        Component Record(string id)
        {
            var matches = Components("Interactable").Where(item => Get<string>(item, "stableId") == id).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Missing or duplicate authored record " + id);
            return matches[0];
        }
        void Inspect(string id) { Call(Record(id), "Use", player); }
        void PlacePlayer(Vector3 at, bool enableController = true)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(at, Quaternion.identity);
            controller.enabled = enableController;
            Physics.SyncTransforms();
        }
        static GameObject Cube(string name, Vector3 at, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.position = at; cube.transform.localScale = scale;
            return cube;
        }
        void IsolateThreats()
        {
            foreach (string name in new[] { "StoryDirector", "AnnexEncounter", "UncatAnnexEvent", "V1HwacatEvent", "LanternMaskEncounter", "WeepingAngelEncounter" })
                foreach (var item in Components(name)) ((Behaviour)item).enabled = false;
            foreach (var item in Components("StalkerBrain")) item.gameObject.SetActive(false);
        }
        Component StalkerAt(Vector3 at)
        {
            var actor = new GameObject("CloudQA controlled stalker"); actor.SetActive(false); actor.transform.position = at;
            var agent = actor.AddComponent<NavMeshAgent>(); agent.radius = .3f; agent.height = 1.8f; agent.updateRotation = false;
            var brain = actor.AddComponent(RequireType("StalkerBrain"));
            Set(brain, "player", player); Set(brain, "patrolSpeed", 0f); Set(brain, "chaseSpeed", 0f);
            actor.SetActive(true);
            Assert.That(agent.Warp(at), Is.True, "Controlled actor must bind to the authored NavMesh");
            return brain;
        }
        static Vector3 MainCorridorPoint()
        {
            Assert.That(NavMesh.SamplePosition(new Vector3(-6.5f, 0, 0), out var hit, 1.5f, NavMesh.AllAreas), Is.True,
                "Protected main corridor has no baked NavMesh");
            return hit.position;
        }

        [UnityTest]
        public IEnumerator TitleAndRequiredRecordsGateRealEscape()
        {
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Title"));
            Assert.That(Time.timeScale, Is.Zero); Assert.That(AudioListener.pause, Is.True);
            Assert.That((bool)Call(session, "Collect", "register"), Is.False, "Title cannot collect records");
            Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Authored navigation never registered");
            Assert.That((bool)Call(session, "Collect", "restore"), Is.False);
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Assert.That((bool)Call(session, "Collect", "register"), Is.False);
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
            Assert.That((bool)Call(session, "Collect", "record"), Is.True);
            Assert.That((bool)Call(session, "Collect", "restore"), Is.False, "Missing annex evidence must block restoration");
            Call(session, "TryEscape"); Assert.That(Get<bool>(session, "Finished"), Is.False);
            foreach (string id in new[] { "music-roster", "archive-record", "nursery-tag" }) Inspect(id);
            int discoveries = Get<int>(session, "ExplorationCount"); Inspect("nursery-tag");
            Assert.That(Get<int>(session, "ExplorationCount"), Is.EqualTo(discoveries));
            Assert.That(Get<bool>(session, "AnnexRecordsComplete"), Is.True);
            Assert.That((bool)Call(session, "Collect", "restore"), Is.True);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(7));
            Call(session, "TryEscape");
            Assert.That(Get<bool>(session, "Escaped"), Is.True); Assert.That(Get<bool>(session, "Finished"), Is.True);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Result"));
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseJournalAndMixedRestartRequestsKeepTheirDestination()
        {
            Begin(); Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Assert.That((bool)Call(player, "TrySetCrouching", true), Is.True); Call(player, "ApplyCurse", 10f);
            Call(shell, "Pause"); Call(shell, "Journal");
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Journal"));
            float stamina = Get<float>(player, "Stamina"); var position = player.transform.position;
            yield return Delay(.15f);
            Assert.That(player.transform.position, Is.EqualTo(position)); Assert.That(Get<float>(player, "Stamina"), Is.EqualTo(stamina));
            Call(shell, "Back"); Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Pause"));
            var previous = session;
            Call(shell, "Restart", true); Assert.That(Get<bool>(shell, "IsReloading"), Is.True);
            Call(shell, "Restart", false);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20, "Restart did not replace session");
            yield return null; yield return null;
            session = One("GameSession"); shell = Get<Component>(session, "Shell"); player = Get<Component>(session, "player");
            Assert.That(Get<bool>(session, "InputAllowed"), Is.True); Assert.That(Get<int>(session, "StoryStep"), Is.Zero);
            Assert.That(Get<bool>(player, "Crouching"), Is.False); Assert.That(Get<float>(player, "SlowRemaining"), Is.Zero);
            Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(2));
            previous = session; Call(shell, "Restart", false); Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20, "Return to title did not replace session");
            yield return null; yield return null;
            session = One("GameSession"); shell = Get<Component>(session, "Shell"); player = Get<Component>(session, "player");
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Title"));
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False); Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CrouchUsesActualCapsuleAndOnlyMovingFeetMakeNoise()
        {
            IsolateThreats(); Begin();
            Vector3 origin = new Vector3(500, 0, 500);
            Cube("CloudQA floor", origin - Vector3.up * .2f, new Vector3(30, .4f, 30));
            PlacePlayer(origin + Vector3.up * .02f);
            yield return Wait(() => Get<bool>(player, "Grounded"), 3, "Controller did not ground on the test floor");
            var controller = player.GetComponent<CharacterController>(); float height = controller.height, feet = player.transform.position.y;
            yield return KeysObserved(Key.C); yield return Wait(() => Get<bool>(player, "Crouching") && Get<Camera>(player, "eyes").transform.localPosition.y < 1.2f, 3, "Crouch input or eye-height transition did not finish", InputDiagnostics); Keys();
            Assert.That(Get<bool>(player, "Crouching"), Is.True); Assert.That(controller.height, Is.LessThan(height * .7f));
            Assert.That(player.transform.position.y, Is.EqualTo(feet).Within(.06f));
            Assert.That(Get<Camera>(player, "eyes").transform.localPosition.y, Is.LessThan(1.2f));
            var ceiling = Cube("CloudQA low ceiling", player.transform.position + Vector3.up * 1.45f, new Vector3(2, .15f, 2)); Physics.SyncTransforms();
            Assert.That((bool)Call(player, "TrySetCrouching", false), Is.False);
            ceiling.SetActive(false); Physics.SyncTransforms();
            Assert.That((bool)Call(player, "TrySetCrouching", false), Is.True); Assert.That(controller.height, Is.EqualTo(height).Within(.001f));
            var feedback = Get<Component>(player, "Feedback"); int steps = Get<int>(feedback, "FootstepsPlayed");
            yield return KeysObserved(Key.LeftShift); yield return Delay(.4f); Keys();
            Assert.That(Get<int>(feedback, "FootstepsPlayed"), Is.EqualTo(steps));
            yield return KeysObserved(Key.W); yield return Wait(() => Get<int>(feedback, "FootstepsPlayed") > steps, 2, "Real walking emitted no foot contact"); Keys();
            Assert.That(Get<float>(player, "FootstepNoiseRadius"), Is.EqualTo(4f));
            Call(player, "TrySetCrouching", true); steps = Get<int>(feedback, "FootstepsPlayed");
            yield return KeysObserved(Key.W, Key.LeftShift); yield return Wait(() => Get<int>(feedback, "FootstepsPlayed") > steps, 2, "Crouched movement emitted no foot contact"); Keys();
            Assert.That(Get<bool>(player, "Running"), Is.False); Assert.That(Get<float>(player, "FootstepNoiseRadius"), Is.EqualTo(1.75f));
            Call(shell, "Pause"); float remaining = Get<float>(player, "FootstepNoiseRemaining");
            yield return Delay(.2f); Assert.That(Get<float>(player, "FootstepNoiseRemaining"), Is.EqualTo(remaining));
            Call(shell, "Resume"); Call(player, "TrySetCrouching", false);
            float beforeWall = player.transform.position.z;
            Cube("CloudQA blocking wall", player.transform.position + Vector3.forward * .65f + Vector3.up * 1.5f, new Vector3(3, 3, .2f)); Physics.SyncTransforms();
            yield return KeysObserved(Key.W, Key.LeftShift);
            yield return Wait(() => player.transform.position.z > beforeWall + .05f && Get<float>(player, "ActualSpeed") < .12f, 3, "Player never reached the blocking wall");
            steps = Get<int>(feedback, "FootstepsPlayed");
            yield return Delay(.5f); Keys(); Assert.That(Get<int>(feedback, "FootstepsPlayed"), Is.EqualTo(steps), "Blocked sprint invented footsteps");
        }

        [UnityTest]
        public IEnumerator AuthoredNavigationReachesAllMandatoryRecords()
        {
            Begin(); yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing baked navigation");
            var leafTargets = new Dictionary<Transform, Vector3>();
            foreach (var door in Components("Interactable").Where(item => Get<object>(item, "kind").ToString() == "Door"))
            {
                var leaf = Get<Transform>(door, "movingLeaf"); Assert.That(leaf, Is.Not.Null, "Authored door is missing its moving leaf");
                Vector3 offset = Get<Vector3>(door, "openOffset"); leafTargets.Add(leaf, leaf.localPosition + offset);
                var secondary = Get<Transform>(door, "secondaryLeaf");
                if (secondary) leafTargets.Add(secondary, secondary.localPosition - offset);
                Call(door, "Use", player);
            }
            Assert.That(leafTargets.Count, Is.GreaterThan(0));
            yield return Wait(() => leafTargets.All(pair => Vector3.Distance(pair.Key.localPosition, pair.Value) < .02f), 10, "Authored doors did not finish opening");
            yield return null; Physics.SyncTransforms();
            var targets = new[] { "register", "ribbon", "record", "restore", "music-roster", "archive-record", "nursery-tag" }.Select(Record).ToList();
            var exits = Components("Interactable").Where(item => Get<object>(item, "kind").ToString() == "Exit").ToArray();
            Assert.That(exits.Length, Is.EqualTo(1)); targets.Add(exits[0]);
            Assert.That(targets.Count, Is.EqualTo(8));
            foreach (var target in targets)
                Assert.That(ReachableInteraction(target), Is.True, "No clear, reachable interaction approach for " + target.name + " at " + target.transform.position);
        }
        bool ReachableInteraction(Component item)
        {
            foreach (var collider in item.GetComponentsInChildren<Collider>())
            {
                Vector3 aim = collider.bounds.center;
                for (int i = 0; i < 96; i++)
                {
                    float angle = i % 32 * Mathf.PI / 16, radius = .65f + i / 32 * .45f;
                    var candidate = aim + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 2.2f, NavMesh.AllAreas)) continue;
                    Vector3 at = hit.position;
                    if (aim.y - at.y < -.25f || aim.y - at.y > 2.1f) continue;
                    if (Physics.CheckCapsule(at + Vector3.up * .4f, at + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 eye = at + Vector3.up * 1.6f, delta = aim - eye;
                    if (delta.magnitude > 2.2f || !Physics.Raycast(eye, delta.normalized, out var sight, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        sight.collider.GetComponentInParent(RequireType("Interactable")) != item) continue;
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(player.transform.position, at, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) return true;
                }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator RealFootstepsDriveHearingAndRecognitionRespectsCover()
        {
            IsolateThreats(); Begin(); yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing baked navigation");
            // The old -6.5 anchor minus 2.4 put the capsule inside the west wall
            // and exit plaque at the floor edge. NavMesh sampling only validated the
            // enemy anchor, not that offset player placement. Use the real interior
            // corridor and prove floor support/body clearance across the entire lane.
            Assert.That(NavMesh.SamplePosition(new Vector3(-4.5f, 0, 0), out var anchor, .25f, NavMesh.AllAreas), Is.True,
                "Hearing lane has no authored NavMesh anchor");
            Vector3 origin = anchor.position, start = origin - Vector3.right * 2.4f, supportedStart = start;
            var floors = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BoxCollider>())
                .Where(collider => collider.name == "Corridor floor").ToArray();
            Assert.That(floors.Length, Is.EqualTo(1), "Expected one real authored corridor floor");
            var floor = floors[0]; var controller = player.GetComponent<CharacterController>();
            Assert.That(floor.enabled && !floor.isTrigger, Is.True);
            Physics.SyncTransforms();
            for (int i = 0; i <= 32; i++)
            {
                Vector3 point = Vector3.Lerp(start, origin + Vector3.right * 4, i / 32f);
                Assert.That(Physics.Raycast(point + Vector3.up, Vector3.down, out var support, 2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "Hearing lane has no physical support at " + point);
                Assert.That(support.collider, Is.SameAs(floor), "Hearing lane is not supported by the authored floor at " + point);
                Assert.That(support.normal.y, Is.GreaterThan(.99f));
                Vector3 feet = support.point + Vector3.up * .02f;
                float margin = controller.radius + controller.skinWidth;
                Assert.That(feet.x, Is.InRange(floor.bounds.min.x + margin, floor.bounds.max.x - margin));
                Assert.That(feet.z, Is.InRange(floor.bounds.min.z + margin, floor.bounds.max.z - margin));
                Vector3 center = feet + controller.center;
                float half = controller.height * .5f - controller.radius;
                Assert.That(Physics.CheckCapsule(center - Vector3.up * half, center + Vector3.up * half,
                    controller.radius - .02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False,
                    "Hearing lane overlaps authored geometry at " + feet);
                if (i == 0) supportedStart = feet;
            }
            Assert.That(NavMesh.Raycast(origin, supportedStart, out _, NavMesh.AllAreas), Is.False,
                "Hearing lane crosses an authored navigation boundary");
            int movementBeforePlacement = Get<int>(player, "MovementUpdates");
            PlacePlayer(supportedStart); player.transform.rotation = Quaternion.Euler(0, 90, 0);
            var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
            // isGrounded describes the previous Move. Require fresh real physics
            // moves and the expected floor height instead of accepting stale contact.
            yield return Wait(() => Get<int>(player, "MovementUpdates") >= movementBeforePlacement + 3 &&
                Get<bool>(player, "Grounded") && Mathf.Abs(player.transform.position.y - floor.bounds.max.y) < .12f,
                3, "Authored corridor did not freshly ground the player", InputDiagnostics);
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.False, "Hearing setup must place player behind enemy");
            Assert.That((bool)Call(enemy, "HearNoise", origin + Vector3.up * 5, 2f), Is.False);
            Call(shell, "Pause"); Assert.That((bool)Call(enemy, "HearNoise", origin + Vector3.right, 2f), Is.False); Call(shell, "Resume");
            int accepted = Get<int>(enemy, "FootstepNoisesAccepted");
            Vector3 beforeStep = player.transform.position;
            int footstepsBefore = Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed");
            yield return KeysObserved(Key.W);
            yield return Wait(() => player.transform.position.x > beforeStep.x + .1f && Get<bool>(player, "Grounded"), 2, "W reached the keyboard but the grounded motor did not move forward", InputDiagnostics);
            yield return Wait(() => Get<int>(enemy, "FootstepNoisesAccepted") > accepted, 2, "Actual player foot contact never reached enemy hearing", () => InputDiagnostics() + ", accepted=" + Get<int>(enemy, "FootstepNoisesAccepted") + ", enemyState=" + Get<object>(enemy, "state")); Keys();
            Assert.That(Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed"), Is.GreaterThan(footstepsBefore));
            Assert.That(Get<bool>(player, "Grounded"), Is.True);
            Assert.That(player.transform.position.y, Is.EqualTo(floor.bounds.max.y).Within(.12f));
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Investigate"));
            ((Behaviour)player).enabled = false; PlacePlayer(origin + Vector3.right * 4, false);
            Get<Light>(player, "flashlight").enabled = true;
            yield return Wait(() => Get<float>(enemy, "Awareness") > 0, 1, "Visible target accumulated no awareness");
            Assert.That(Get<float>(enemy, "Awareness"), Is.LessThan(1));
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "Recognition never became chase");
            player.GetComponent<CharacterController>().enabled = true;
            Assert.That((bool)Call(player, "TrySetCrouching", true), Is.True);
            Assert.That(Get<bool>(player, "Crouching"), Is.True);
            Get<Light>(player, "flashlight").enabled = false; yield return Delay(.2f);
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Chase"), "Crouch/light toggle erased existing pursuit");
            Cube("CloudQA sight barrier", origin + Vector3.right * 2 + Vector3.up * 1.5f, new Vector3(.2f, 3, 2)); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.False, "Opaque wall did not block physical sight");
            Assert.That((bool)Call(enemy, "HearNoise", origin - Vector3.right, 2f), Is.False, "A decoy canceled established chase");
        }

        [UnityTest]
        public IEnumerator AttackWarningPausesAndDodgeWorksBeforeLethalContact()
        {
            IsolateThreats(); Begin(); yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing baked navigation");
            Vector3 origin = MainCorridorPoint(); ((Behaviour)player).enabled = false; PlacePlayer(origin + Vector3.right * 1.2f, false);
            var enemy = StalkerAt(origin); Set(enemy, "state", "Chase");
            yield return Wait(() => Get<bool>(enemy, "AttackActive"), 2, "No attack warning began");
            yield return Delay(.15f); float windup = Get<float>(enemy, "AttackWindup");
            Assert.That(Get<bool>(session, "Finished"), Is.False); Call(shell, "Pause");
            yield return Delay(.2f); Assert.That(Get<float>(enemy, "AttackWindup"), Is.EqualTo(windup).Within(.001f));
            Call(shell, "Resume"); PlacePlayer(origin + Vector3.right * 4, false);
            yield return Wait(() => Get<bool>(session, "Finished") || Get<float>(enemy, "AttackRecovery") > 0, 3, "Dodged attack never reached recovery");
            Assert.That(Get<bool>(session, "Finished"), Is.False); Assert.That(Get<float>(enemy, "AttackRecovery"), Is.GreaterThan(0));
            yield return Wait(() => !Get<bool>(enemy, "AttackActive"), 2, "Attack recovery never completed");
            PlacePlayer(origin + Vector3.right * 1.2f, false);
            yield return Wait(() => Get<bool>(session, "Finished"), 2, "Stationary close contact was never lethal");
            Assert.That(Get<bool>(session, "Escaped"), Is.False); Assert.That(Get<string>(session, "DefeatSource"), Is.EqualTo(enemy.name));
        }
    }
}
