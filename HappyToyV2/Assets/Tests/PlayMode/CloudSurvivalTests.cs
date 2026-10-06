using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(750000)]
        public IEnumerator AuthoredSevenRecordRouteSurvivesWithRealMovementAndInteraction()
        {
            // Title/menu submit is covered separately. This gate starts the public game
            // API, then every action is an actual keyboard or mouse input. No fixture
            // placement, direct Use/Collect/Hide call, speed change or enemy isolation.
            using (var route = new CloudSurvivalTests(session, player, shell, Keys, InputDiagnostics))
            {
                Begin();
                yield return route.Run();
            }
        }
        [UnityTest, Timeout(750000)]
        public IEnumerator ChapterFiveMemoryRouteSurvivesThroughActualInputAndFourStairLegs()
        {
            Call(shell,"BeginChapter");
            using(var route=new CloudSurvivalTests(session,player,shell,Keys,InputDiagnostics))
                yield return route.Run();
        }
    }

    /// <summary>
    /// Deterministic, input-driven survival route through the unchanged authored scene.
    /// Navigation plans where to walk; only PlayerMotor moves the player. This proves
    /// this strategy, not first-time-player balance, every encounter, or visual quality.
    /// </summary>
    public sealed class CloudSurvivalTests : IDisposable
    {
        const float GameBudget = 300, WallBudget = 720;
        readonly Component session, player, shell;
        readonly Camera eyes;
        readonly CharacterController controller;
        readonly Action<Key[]> keys;
        readonly Func<string> inputDiagnostics;
        readonly Mouse mouse, previousMouse;
        readonly Component[] stalkers;
        readonly Component nursery, upper, uncat, lantern, mannequin;
        readonly Behaviour[] encounterOwners;
        readonly Dictionary<Component, Vector2> originalSpeeds = new Dictionary<Component, Vector2>();
        readonly List<string> milestones = new List<string>();
        readonly float wallStart;
        readonly bool chapterStrategy;
        float gameStart, progressTime, minY, maxY, distanceWalked, ungroundedFor;
        Vector3 previousPosition;
        int monitorFrame = -1, movementStart, groundedFrames, maximumLiveStalkers, throws, hidingEntries, stairLegs;
        bool sprintReady = true, expectingEscape, passed, disposed, previouslyHidden;
        string stage = "startup", destination = "";

        public CloudSurvivalTests(Component session, Component player, Component shell,
            Action<Key[]> keys, Func<string> inputDiagnostics)
        {
            this.session = session; this.player = player; this.shell = shell;
            chapterStrategy=Get<bool>(session,"ChapterMode");
            this.keys = keys; this.inputDiagnostics = inputDiagnostics;
            eyes = Get<Camera>(player, "eyes"); controller = player.GetComponent<CharacterController>();
            stalkers = Components("StalkerBrain");
            nursery = One("AnnexEncounter"); upper = One("V1HwacatEvent"); uncat = One("UncatAnnexEvent");
            lantern = One("LanternMaskEncounter"); mannequin = One("WeepingAngelEncounter");
            encounterOwners = new[] { One("StoryDirector"), nursery, upper, uncat, lantern, mannequin }
                .Cast<Behaviour>().ToArray();
            foreach (var enemy in stalkers)
                originalSpeeds.Add(enemy, new Vector2(Get<float>(enemy, "patrolSpeed"), Get<float>(enemy, "chaseSpeed")));
            wallStart = Time.realtimeSinceStartup;
            previousPosition = player.transform.position; minY = maxY = previousPosition.y;
            movementStart = Get<int>(player, "MovementUpdates");
            // Do not install global input until every assertion-prone lookup succeeds.
            previousMouse = Mouse.current; mouse = InputSystem.AddDevice<Mouse>();
        }

        float GameTime => Get<float>(session, "ElapsedPlayTime") - gameStart;
        float WallTime => Time.realtimeSinceStartup - wallStart;
        bool Hidden => Get<bool>(player, "Hidden");
        bool Finished => Get<bool>(session, "Finished");
        bool Escaped => Get<bool>(session, "Escaped");

        public IEnumerator Run()
        {
            if(chapterStrategy) {yield return RunChapter();yield break;}
            gameStart = Get<float>(session, "ElapsedPlayTime");
            try
            {
                Assert.That(stalkers.Length, Is.EqualTo(4), "The four authored stalker actors must remain present");
                Assert.That(Get<bool>(session, "requireAnnexRecords"), Is.True);
                Assert.That(Get<int>(session, "StoryStep"), Is.Zero);
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.Zero);
                Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(2));
                yield return Await(() => NavMesh.CalculateTriangulation().vertices.Length > 0 &&
                    Components("NavMeshStartup").All(item => Get<bool>(item, "Ready")) &&
                    Get<int>(player, "MovementUpdates") >= movementStart + 3 && Get<bool>(player, "Grounded"),
                    5, 30, "Authored navigation/physical spawn never became ready");
                Milestone("Started at the authored spawn with every encounter enabled");

                // Open the escape passage before waking threats. These are actual E
                // interactions and the moving leaves must physically finish opening.
                yield return Walk(new Vector3(-4.5f, 0, 0), false);
                yield return OpenDoor("CLASSROOM");
                yield return OpenDoor("WASHROOM");
                yield return RecoverStamina();
                yield return TakeRecord("register");
                yield return Walk(new Vector3(-4.5f, 0, 0), false);
                yield return SetFlashlight(false);
                yield return TakeRecord("ribbon");

                // The authored stairs are continuous physical legs, not teleported
                // fixture starts or flattened NavMesh corner heights.
                yield return Walk(new Vector3(29.8f, 0, 10), true);
                yield return Stair(new Vector3(29.8f, 5, 22), "upper ascent", false);
                yield return Approach(Record("record"), false);
                yield return RecoverStamina();
                yield return TakeRecord("record", false);
                yield return Walk(new Vector3(29.8f, 5, 22), true);
                yield return Stair(new Vector3(29.8f, 0, 10), "upper descent", false);

                yield return TakeRecord("music-roster");
                // A real cabinet entry provides a bounded stamina break. Survival
                // and a usable exit are required; no awareness state is reset here.
                yield return RestInMusicCabinet();
                yield return Approach(Record("archive-record"), true);
                // Put sound on the eastern archive side before the emergence, while
                // the player leaves by the western loop. A decoy cannot cancel chase.
                yield return ThrowToward(new Vector3(27, .1f, -11.5f), "archive diversion");
                yield return TakeRecord("archive-record", false);
                yield return Walk(new Vector3(13.8f, 0, -10), true);
                yield return Stair(new Vector3(13.8f, -5, -22), "basement descent", false);

                // Stay outside the 4.5 m nursery trigger while recovering. The live
                // five-second warning is then enough for a deliberate in/out route;
                // a western decoy remains available when the baby is released.
                yield return RecoverStamina();
                yield return ThrowToward(new Vector3(8.8f, -4.9f, -30), "nursery diversion");
                yield return Walk(new Vector3(15, -5, -24), true);
                yield return Await(() => Get<bool>(nursery, "Triggered"), 1, 10, "Physical nursery entry did not trigger its warning");
                Milestone("Nursery warning triggered by actual entry");
                yield return TakeRecord("nursery-tag");
                // Avoid the central support piers and the baby's release point.
                yield return Walk(new Vector3(18.4f, -5, -22.5f), true);
                yield return Walk(new Vector3(13.8f, -5, -22), true);
                yield return Stair(new Vector3(13.8f, 0, -10), "basement ascent", true);

                yield return Walk(new Vector3(-4.5f, 0, 0), true);
                yield return TakeRecord("restore");
                yield return Await(() => Get<bool>(One("StoryDirector"), "RestorationChairCompleted"),
                    3, 20, "Restoration chair did not finish its authored cue");
                Assert.That(Get<int>(One("StoryDirector"), "RestorationChairCues"), Is.EqualTo(1));
                Assert.That(Get<int>(session, "StoryStep"), Is.EqualTo(4));
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(7));
                Assert.That(Get<bool>(session, "AnnexRecordsComplete"), Is.True);
                Assert.That(Get<bool>(upper, "Completed"), Is.True, "Upper-floor reveal never handed off to its live actor");
                Assert.That(Get<bool>(uncat, "Triggered"), Is.True);
                Assert.That(Get<bool>(uncat, "Released"), Is.True);
                Assert.That(Get<bool>(nursery, "Released"), Is.True, "Basement encounter never released its live actor");
                Assert.That(stairLegs, Is.EqualTo(4));
                Assert.That(minY, Is.LessThan(-4.8f)); Assert.That(maxY, Is.GreaterThan(4.8f));
                Assert.That(hidingEntries, Is.EqualTo(1)); Assert.That(throws, Is.EqualTo(2));
                Assert.That(distanceWalked, Is.GreaterThan(100), "Full route lacks physical travel");
                Assert.That(groundedFrames, Is.GreaterThan(100));
                Assert.That(Get<int>(player, "MovementUpdates") - movementStart, Is.GreaterThan(500));
                Assert.That(Get<int>(Get<Component>(player, "Feedback"), "FootstepsPlayed"), Is.GreaterThan(30));

                var exit = Components("Interactable").Single(item => Get<object>(item, "kind").ToString() == "Exit");
                stage = "exit"; yield return Approach(exit, false);
                expectingEscape = true;
                yield return Interact(exit);
                Assert.That(Escaped && Finished, Is.True, "Final E did not escape\n" + Diagnostics());
                Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Result"));
                Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
                Assert.That(Time.timeScale, Is.Zero);
                passed = true; Milestone("Escaped with all seven records through real input");
            }
            finally
            {
                keys(Array.Empty<Key>());
                Debug.Log("HAPPYTOY_SURVIVAL_RESULT " + JsonUtility.ToJson(new Report
                {
                    passed = passed, stage = stage, destination = destination, gameSeconds = GameTime,
                    wallSeconds = WallTime, records = Get<int>(session, "RecordsRecovered"),
                    movementUpdates = Get<int>(player, "MovementUpdates") - movementStart,
                    physicalMeters = distanceWalked, groundedFrames = groundedFrames, stairLegs = stairLegs,
                    minY = minY, maxY = maxY, hidingEntries = hidingEntries, firecrackersThrown = throws,
                    maximumLiveStalkers = maximumLiveStalkers, milestones = milestones.ToArray(), diagnostic = Diagnostics()
                }));
            }
        }

        IEnumerator RunChapter()
        {
            gameStart=Get<float>(session,"ElapsedPlayTime");
            try
            {
                Assert.That(stalkers.All(x=>!x.gameObject.activeInHierarchy),Is.True,"Chapter started with a monster");
                yield return Await(()=>Get<bool>(player,"Grounded"),3,20,"Opening floor never grounded");
                yield return Walk(new Vector3(-4.5f,0,0),false);
                yield return OpenDoor("WASHROOM");
                yield return SetFlashlight(false);
                yield return TakeRecord("chapter-memory-0");
                Assert.That(stalkers.Count(x=>x.gameObject.activeInHierarchy),Is.EqualTo(1));
                yield return TakeRecord("chapter-memory-1");
                Assert.That(mannequin.gameObject.activeInHierarchy,Is.True);Assert.That(lantern.gameObject.activeInHierarchy,Is.False);
                yield return Walk(new Vector3(29.8f,0,10),true);
                yield return Stair(new Vector3(29.8f,5,22),"chapter upper ascent",false);
                yield return TakeRecord("chapter-memory-2");
                Assert.That(lantern.gameObject.activeInHierarchy,Is.True);
                yield return RecoverStamina();
                var fourth=Record("chapter-memory-3");stage="mandatory portrait";
                yield return Approach(fourth,true);yield return Interact(fourth);
                Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(3));
                Assert.That(Get<bool>(upper,"Triggered"),Is.True);
                // Watch the actual stand-up from the room's eastern escape route.
                yield return Walk(new Vector3(34.7f,5,32.2f),true);
                keys(Array.Empty<Key>());
                float started=GameTime;
                while(!Get<bool>(upper,"ChapterWitnessed"))
                {
                    Check();Require(GameTime-started<12,"Portrait could not be witnessed from its physical viewing route");
                    Steer(Get<Vector3>(upper,"spawn")+Vector3.up*.9f,false);yield return null;
                }
                // Continue through the two partition passages while the reveal finishes;
                // standing still with the newly released mask is unsafe.
                yield return Walk(new Vector3(34.7f,5,23.2f),true);
                yield return Walk(new Vector3(25.2f,5,22.8f),true);
                yield return Walk(new Vector3(25.2f,5,32.8f),true);
                yield return Await(()=>Get<bool>(upper,"Completed"),3,15,"Portrait did not finish after the physical escape loop");
                Milestone("Witnessed the portrait and crossed both escape passages");
                yield return TakeRecord("chapter-memory-3");
                yield return Walk(new Vector3(29.8f,5,22),true);
                yield return Stair(new Vector3(29.8f,0,10),"chapter upper descent",false);
                yield return Walk(new Vector3(13.8f,0,-10),true);
                yield return Stair(new Vector3(13.8f,-5,-22),"chapter basement descent",false);
                yield return Await(()=>Get<bool>(nursery,"Released"),8,25,"Real basement entry did not release the baby");
                yield return TakeRecord("chapter-memory-4");
                yield return Walk(new Vector3(13.8f,-5,-22),true);
                yield return Stair(new Vector3(13.8f,0,-10),"chapter basement ascent",true);
                yield return Walk(new Vector3(-4.5f,0,0),true);
                var exit=Components("Interactable").Single(x=>Get<object>(x,"kind").ToString()=="Exit");stage="chapter exit";
                yield return Approach(exit,true);expectingEscape=true;yield return Interact(exit);
                Assert.That(Escaped&&Finished,Is.True);Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(5));
                Assert.That(stairLegs,Is.EqualTo(4));Assert.That(minY,Is.LessThan(-4.8f));Assert.That(maxY,Is.GreaterThan(4.8f));
                Assert.That(distanceWalked,Is.GreaterThan(100));passed=true;Milestone("Escaped five-memory chapter through real input");
            }
            finally
            {
                keys(Array.Empty<Key>());
                Debug.Log("HAPPYTOY_CHAPTER_INPUT_RESULT "+JsonUtility.ToJson(new Report {passed=passed,stage=stage,destination=destination,gameSeconds=GameTime,wallSeconds=WallTime,records=Get<int>(session,"RecordsRecovered"),movementUpdates=Get<int>(player,"MovementUpdates")-movementStart,physicalMeters=distanceWalked,groundedFrames=groundedFrames,stairLegs=stairLegs,minY=minY,maxY=maxY,maximumLiveStalkers=maximumLiveStalkers,milestones=milestones.ToArray(),diagnostic=Diagnostics()}));
            }
        }

        [Serializable]
        sealed class Report
        {
            public bool passed;
            public string stage, destination, diagnostic;
            public string[] milestones;
            public float gameSeconds, wallSeconds, physicalMeters, minY, maxY;
            public int records, movementUpdates, groundedFrames, stairLegs, hidingEntries, firecrackersThrown, maximumLiveStalkers;
        }

        void Check()
        {
            Require(WallTime < WallBudget, "Survival wall-clock budget exceeded");
            Require(GameTime < GameBudget, "Survival game-time budget exceeded");
            Require(!Finished || expectingEscape && Escaped, "Real survival route ended in defeat");
            if (Finished) return;
            Require(Get<bool>(session, "InputAllowed"), "Route lost gameplay input");
            Assert.That(Time.timeScale, Is.EqualTo(1), "Survival may not alter game time");
            Assert.That(Time.captureDeltaTime, Is.Zero, "Survival may not force the simulation clock");
            Assert.That(Get<object>(player, "HidingRandomSample"), Is.Null, "Full route may not force a hiding outcome");
            Assert.That(((Behaviour)player).enabled, Is.True);
            Assert.That(Mouse.current, Is.SameAs(mouse), "Route mouse is not current");
            foreach (var owner in chapterStrategy?new[]{(Behaviour)nursery,(Behaviour)upper}:encounterOwners)
                Assert.That(owner && owner.enabled && owner.gameObject.activeInHierarchy, Is.True,
                    "An authored encounter owner was disabled: " + (owner ? owner.name : "missing"));
            foreach (var enemy in stalkers)
            {
                Assert.That(enemy, Is.Not.Null, "An authored actor disappeared");
                Assert.That(new Vector2(Get<float>(enemy, "patrolSpeed"), Get<float>(enemy, "chaseSpeed")),
                    Is.EqualTo(originalSpeeds[enemy]), "Authored threat speed changed");
            }
            Assert.That(controller.enabled || Hidden, Is.True, "Physical controller disabled outside real hiding");
            if (monitorFrame == Time.frameCount) return;
            monitorFrame = Time.frameCount;
            Vector3 at = player.transform.position;
            Assert.That(float.IsNaN(at.x) || float.IsInfinity(at.x) || float.IsNaN(at.y) || float.IsInfinity(at.y) ||
                float.IsNaN(at.z) || float.IsInfinity(at.z), Is.False, "Non-finite physical position");
            Require(at.y >= -5.5f && at.y <= 5.6f, "Player left authored floors");
            if (!Hidden)
            {
                minY = Mathf.Min(minY, at.y); maxY = Mathf.Max(maxY, at.y);
                var delta = at - previousPosition; delta.y = 0;
                // Exclude gameplay's legitimate cabinet exit snap from evidence of walking.
                if (!previouslyHidden) distanceWalked += delta.magnitude;
                if (Get<bool>(player, "Grounded")) { groundedFrames++; ungroundedFor = 0; }
                else ungroundedFor += Time.deltaTime;
                Require(ungroundedFor < 1.5f, "Sustained loss of physical floor contact");
            }
            previousPosition = at; previouslyHidden = Hidden;
            maximumLiveStalkers = Mathf.Max(maximumLiveStalkers, stalkers.Count(item => ((Behaviour)item).isActiveAndEnabled));
            if (GameTime - progressTime >= 15)
            {
                progressTime = GameTime;
                Debug.Log("HAPPYTOY_SURVIVAL_PROGRESS " + Diagnostics());
            }
        }

        IEnumerator Await(Func<bool> condition, float gameSeconds, float wallSeconds, string failure)
        {
            float startGame = GameTime, startWall = WallTime;
            while (!condition())
            {
                Check();
                Require(GameTime - startGame < gameSeconds && WallTime - startWall < wallSeconds, failure);
                yield return null;
            }
            Check();
        }

        IEnumerator Pulse(Key key)
        {
            keys(Array.Empty<Key>()); yield return null; Check();
            keys(new[] { key }); yield return null;
            keys(Array.Empty<Key>()); yield return null; Check();
        }

        void Steer(Vector3 point, bool level)
        {
            Vector3 delta = point - eyes.transform.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = level ? 0 : Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -77, 77);
            float sensitivity = Get<float>(player, "sensitivity");
            Assert.That(sensitivity, Is.GreaterThan(0));
            var pixels = new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), -40, 40),
                -Mathf.Clamp(Mathf.DeltaAngle(eyes.transform.localEulerAngles.x, pitch), -30, 30)) / sensitivity;
            InputSystem.QueueDeltaStateEvent(mouse.delta, pixels);
        }

        bool Sprint(bool requested)
        {
            float stamina = Get<float>(player, "Stamina");
            if (stamina < .15f) sprintReady = false;
            if (stamina > .85f) sprintReady = true;
            return requested && sprintReady && !Get<bool>(player, "SprintExhausted");
        }

        IEnumerator Walk(Vector3 target, bool sprint)
        {
            Check(); destination = target.ToString("F2");
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(player.transform.position, target, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete, Is.True, "No complete authored route\n" + Diagnostics());
            Assert.That(path.corners.Length, Is.GreaterThan(0));
            // Only same-floor travel uses generic corners. Stairs have measured,
            // continuous input legs with an independently verified final height.
            foreach (var corner in path.corners.Skip(1))
            {
                Assert.That(Mathf.Abs(corner.y - target.y), Is.LessThan(1),
                    "Unexpected floor transition in a same-floor leg\n" + Diagnostics());
                yield return MoveTo(corner, sprint, false);
            }
            keys(Array.Empty<Key>()); yield return null; Check();
            Assert.That(Horizontal(player.transform.position - target), Is.LessThan(.35f), "NavMesh route missed its destination");
            Assert.That(Mathf.Abs(player.transform.position.y - target.y), Is.LessThan(.4f), "Route reached the wrong floor");
        }

        IEnumerator Stair(Vector3 target, string label, bool sprint)
        {
            stage = label; destination = target.ToString("F2");
            var from = player.transform.position; var path = new NavMeshPath();
            Assert.That(Mathf.Abs(from.y - target.y), Is.GreaterThan(4.5f));
            Assert.That(Mathf.Abs(from.x - target.x), Is.LessThan(.4f));
            Assert.That(NavMesh.CalculatePath(from, target, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                Is.True, "Authored staircase has no complete route\n" + Diagnostics());
            yield return MoveTo(target, sprint, true);
            keys(Array.Empty<Key>());
            yield return Await(() => Get<bool>(player, "Grounded"), 1, 10, "Stair landing did not physically ground");
            Assert.That(Mathf.Abs(player.transform.position.y - target.y), Is.LessThan(.4f));
            stairLegs++; Milestone(label + " physically completed");
        }

        Component SchoolDoorOnRoute(Vector3 direction, float distance)
        {
            var body = player.GetComponent<CharacterController>();
            var center = player.transform.position + body.center + Vector3.up * .08f;
            float half = Mathf.Max(0, body.height * .5f - body.radius);
            if (!Physics.CapsuleCast(center - Vector3.up * half, center + Vector3.up * half, body.radius + .02f,
                direction.normalized, out var hit, Mathf.Min(2.05f, distance + .1f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
            var door = hit.collider.GetComponentInParent(RequireType("Interactable"));
            return door && Get<object>(door, "kind").ToString() == "Door" ? door : null;
        }
        IEnumerator OpenSchoolPhysicalDoor(Component door)
        {
            keys(Array.Empty<Key>()); float end = Time.realtimeSinceStartup + 5;
            while (!Get<bool>(door, "IsOpen") || !Get<bool>(door, "AtRequestedDoorPose"))
            {
                Check(); Require(Time.realtimeSinceStartup < end, "Actual school leaf never cleared: " + door.name);
                if (!Get<bool>(door, "IsOpen")) yield return Interact(door);
                else yield return null;
            }
            Milestone("School door clear through actual E/leaf motion: " + door.name);
        }

        IEnumerator MoveTo(Vector3 target, bool sprint, bool stair)
        {
            float startGame = GameTime, startWall = WallTime, best = Horizontal(target - player.transform.position);
            float lastProgressGame = GameTime, lastProgressWall = WallTime;
            while (true)
            {
                Check();
                Vector3 delta = target - player.transform.position;
                float horizontal = Horizontal(delta);
                if (horizontal < .18f && Mathf.Abs(delta.y) < .4f) break;
                if (horizontal < best - .04f)
                { best = horizontal; lastProgressGame = GameTime; lastProgressWall = WallTime; }
                Require(GameTime - startGame < (stair ? 20 : 45) && WallTime - startWall < 180,
                    "Physical leg timed out toward " + target);
                Require(GameTime - lastProgressGame < 5 && WallTime - lastProgressWall < 30,
                    "No forward physical progress toward " + target);
                Require(horizontal > .025f || Mathf.Abs(delta.y) < .4f,
                    "Horizontal arrival on the wrong stair/floor height");
                var blockingDoor = SchoolDoorOnRoute(new Vector3(delta.x, 0, delta.z), horizontal);
                if (blockingDoor)
                { yield return OpenSchoolPhysicalDoor(blockingDoor); lastProgressGame=GameTime; lastProgressWall=WallTime; continue; }
                Steer(eyes.transform.position + new Vector3(delta.x, 0, delta.z), true);
                float facing = Vector3.Angle(new Vector3(player.transform.forward.x, 0, player.transform.forward.z),
                    new Vector3(delta.x, 0, delta.z));
                keys(facing > 12 ? Array.Empty<Key>() : Sprint(sprint && horizontal > .75f) ? new[] { Key.W, Key.LeftShift } : new[] { Key.W });
                yield return null;
            }
        }

        static float Horizontal(Vector3 vector) => new Vector2(vector.x, vector.z).magnitude;
        Component Record(string id) => Components("Interactable").Single(item => Get<string>(item, "stableId") == id);

        IEnumerator Approach(Component item, bool sprint)
        {
            Assert.That(FindApproach(item, out var at, out _), Is.True, "No physical, ray-clear record approach: " + item.name + "\n" + Diagnostics());
            yield return Walk(at, sprint);
        }

        bool FindApproach(Component item, out Vector3 position, out Vector3 aim)
        {
            float best = float.PositiveInfinity; position = aim = Vector3.zero;
            int blockedCapsules=0,blockedRays=0,blockedPaths=0;string firstBlocker="";
            Vector3 eyeOffset = eyes.transform.position - player.transform.position;
            foreach (var collider in item.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector3 center = collider.bounds.center;
                for (int i = 0; i < 96; i++)
                {
                    float angle = i % 32 * Mathf.PI / 16, radius = .65f + i / 32 * .45f;
                    if (!NavMesh.SamplePosition(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius,
                        out var sample, 2.2f, NavMesh.AllAreas)) continue;
                    Vector3 at = sample.position;
                    if (center.y - at.y < -.25f || center.y - at.y > 2.1f) continue;
                    Vector3 capsuleCenter = at + controller.center;
                    float half = controller.height * .5f - controller.radius;
                    if (Physics.CheckCapsule(capsuleCenter - Vector3.up * half, capsuleCenter + Vector3.up * half,
                        controller.radius - .015f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {blockedCapsules++;continue;}
                    Vector3 eye = at + eyeOffset, ray = center - eye;
                    if (ray.magnitude > 2.15f || !Physics.Raycast(eye, ray.normalized, out var hit, 2.2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        hit.collider.GetComponentInParent(RequireType("Interactable")) != item)
                    {blockedRays++;if(firstBlocker=="" && Physics.Raycast(eye,ray.normalized,out var blocker,2.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))firstBlocker=blocker.collider.name;continue;}
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(player.transform.position, at, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete) {blockedPaths++;continue;}
                    float length = 0;
                    for (int n = 1; n < path.corners.Length; n++) length += Vector3.Distance(path.corners[n - 1], path.corners[n]);
                    if (length >= best) continue;
                    best = length; position = at; aim = center;
                }
            }
            bool found=!float.IsPositiveInfinity(best);
            if(!found)Debug.Log("HAPPYTOY_RECORD_APPROACH_TRACE "+item.name+" at="+item.transform.position+" eyeOffset="+eyeOffset+
                " capsules="+blockedCapsules+" rays="+blockedRays+" paths="+blockedPaths+" firstRayBlocker="+firstBlocker);
            return found;
        }

        IEnumerator Interact(Component item)
        {
            keys(Array.Empty<Key>());
            float startGame = GameTime, startWall = WallTime;
            while (Get<Component>(player, "Focus") != item)
            {
                Check();
                Require(GameTime - startGame < 3 && WallTime - startWall < 20,
                    "Actual E focus never reached " + item.name);
                var collider = item.GetComponentsInChildren<Collider>().Where(value => value.enabled && !value.isTrigger)
                    .OrderBy(value => Vector3.Distance(value.ClosestPoint(eyes.transform.position), eyes.transform.position)).First();
                Steer(Vector3.Lerp(collider.ClosestPoint(eyes.transform.position), collider.bounds.center, .08f), false);
                yield return null;
            }
            Assert.That(Get<Component>(player, "Focus"), Is.SameAs(item));
            yield return Pulse(Key.E);
        }

        IEnumerator OpenDoor(string room)
        {
            stage = "open " + room;
            var door = Components("Interactable").Single(item => Get<object>(item, "kind").ToString() == "Door" && item.name.StartsWith(room));
            yield return OpenSchoolPhysicalDoor(door);
            Milestone(room + " opened by E");
        }

        IEnumerator TakeRecord(string id, bool travel = true)
        {
            stage = id;
            Assert.That(Get<string>(session, "CurrentObjectiveId"), Is.EqualTo(id), "Route must follow the authored objective order");
            var item = Record(id); int before = Get<int>(session, "RecordsRecovered");
            if (travel) yield return Approach(item, Get<int>(session, "StoryStep") >= 2);
            yield return Interact(item);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(before + 1), "E did not recover exactly one record: " + id + "\n" + Diagnostics());
            if (Get<object>(item, "kind").ToString() == "Inspect")
                Assert.That((bool)Call(session, "HasInspected", id), Is.True);
            else Assert.That(item.gameObject.activeSelf, Is.False, "Collected story object did not settle");
            Milestone("Recovered " + id);
            if(Get<bool>(session,"ChapterMode"))
            {
                var shots=Get<Component>(Get<Component>(session,"Chapter"),"FirstAppearances");
                yield return Await(()=>!Get<bool>(shots,"CameraOwned"),30,60,"School first appearance did not return control");
            }
        }

        IEnumerator RecoverStamina()
        {
            keys(Array.Empty<Key>());
            yield return Await(() => Get<float>(player, "Stamina") >= .98f && !Get<bool>(player, "SprintExhausted"),
                10, 45, "Rest did not recover real stamina");
            sprintReady = true;
        }

        IEnumerator SetFlashlight(bool on)
        {
            var light = Get<Light>(player, "flashlight"); Assert.That(light, Is.Not.Null);
            if (light.enabled != on) yield return Pulse(Key.F);
            Assert.That(light.enabled, Is.EqualTo(on), "F did not change the actual flashlight");
            Milestone("Flashlight " + (on ? "on" : "off") + " through F");
        }

        IEnumerator RestInMusicCabinet()
        {
            stage = "music cabinet";
            var cabinet = Components("Interactable").Single(item => item.name == "음악실 은신함");
            yield return Approach(cabinet, true);
            yield return Interact(cabinet);
            Assert.That(Hidden, Is.True, "E did not enter authored cabinet\n" + Diagnostics());
            hidingEntries++; previousPosition = player.transform.position; ungroundedFor = 0;
            Milestone("Entered music cabinet through E");
            yield return RecoverStamina();
            yield return Pulse(Key.E);
            Assert.That(Hidden, Is.False, "Real cabinet exit was blocked\n" + Diagnostics());
            previousPosition = player.transform.position;
            yield return Await(() => Get<bool>(player, "Grounded"), 1, 10, "Cabinet exit did not ground");
            Milestone("Left music cabinet through E");
        }

        IEnumerator ThrowToward(Vector3 point, string label)
        {
            keys(Array.Empty<Key>());
            float start = GameTime;
            while (Vector3.Angle(eyes.transform.forward, point - eyes.transform.position) > 2)
            {
                Check(); Require(GameTime - start < 3, "Could not aim firecracker");
                Steer(point, false); yield return null;
            }
            var inventory = Get<Component>(player, "Firecrackers"); int before = Get<int>(inventory, "Count");
            yield return Pulse(Key.Q);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(before - 1), "Q failed to consume one firecracker");
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Not.Null, "No physical firecracker was thrown");
            throws++; Milestone(label + " thrown through Q");
        }

        void Require(bool condition, string failure)
        {
            if (!condition) Assert.Fail(failure + "\n" + Diagnostics());
        }

        void Milestone(string text)
        {
            string line = GameTime.ToString("F2") + "s " + text + " at " + player.transform.position.ToString("F2");
            milestones.Add(line); Debug.Log("HAPPYTOY_SURVIVAL_MILESTONE " + line);
        }

        string Diagnostics()
        {
            string actors = string.Join("; ", stalkers.Where(item => item).Select(item => item.name +
                " active=" + ((Behaviour)item).isActiveAndEnabled + " state=" + Get<object>(item, "state") +
                " at=" + item.transform.position.ToString("F2") + " attacks=" + Get<int>(item, "AttacksStarted") +
                " awareness=" + Get<float>(item, "Awareness").ToString("F2")));
            return "stage=" + stage + ", destination=" + destination + ", game=" + GameTime.ToString("F2") +
                ", wall=" + WallTime.ToString("F2") + ", records=" + Get<int>(session, "RecordsRecovered") +
                ", defeat=" + Get<string>(session, "DefeatSource") + ", hint=" + Get<string>(session, "DefeatHint") +
                ", stamina=" + Get<float>(player, "Stamina").ToString("F2") + ", hidden=" + Hidden +
                ", focus=" + (Get<Component>(player, "Focus") ? Get<Component>(player, "Focus").name : "none") +
                ", collision=" + Get<CollisionFlags>(player, "LastCollision") + ", " + inputDiagnostics() +
                ", nursery=" + Get<bool>(nursery, "Triggered") + "/" + Get<bool>(nursery, "Released") +
                ", upper=" + Get<string>(upper, "Phase") + ", uncat=" + Get<bool>(uncat, "Triggered") + "/" + Get<bool>(uncat, "Released") +
                ", lantern=" + Get<object>(lantern, "State") + ", mannequin=" + Get<bool>(mannequin, "Triggered") + "; " + actors;
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { keys(Array.Empty<Key>()); }
            finally
            {
                try { if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); }
                finally { if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent(); }
            }
        }
    }
}
