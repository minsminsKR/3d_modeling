using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
        IEnumerator CandleDangerPrepare(bool corridor = true)
        {
            Call(session, corridor ? "CreateCorridor" : "CreateChapter", corridor ? new object[] { 73 } : Array.Empty<object>());
            IsolateThreats(); Begin(); yield return null;
            Get<Light>(player, "flashlight").enabled = false;
            Call(shell, "RestoreDefaultSettings");
            Assert.That(Get<bool>(LightRun, "Prepared"), Is.True);
        }

        Component CandleDangerOwnedBrain()
        {
            if (!Get<bool>(session, "CorridorMode"))
                return Get<Component>(Get<Component>(session, "Chapter"), "Cyclopse");
            var world = Get<Transform>(LightRun, "Root").parent;
            return Components("StalkerBrain").Single(actor => actor.transform.IsChildOf(world) &&
                Get<object>(actor, "corridorRole").ToString() == "Watchman");
        }

        Component[] CandleDangerMarks() => LightTargets("Candle")
            .Select(target => target.GetComponent(RequireType("WaymarkCandle"))).ToArray();

        void CandleDangerArmAll()
        {
            foreach (var candle in CandleDangerMarks()) Call(candle, "Restore", true);
        }

        Vector3 CandleDangerPoint(Vector3 origin, float distance, Camera retainedCamera = null, bool requireSight = true)
        {
            for (int ray = 0; ray < 96; ray++)
            {
                float angle = ray * Mathf.PI / 48;
                var desired = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(desired, out var hit, .22f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - origin.y) > .2f ||
                    Mathf.Abs(Vector3.Distance(hit.position, origin) - distance) > .25f) continue;
                var eye = Get<Camera>(player, "eyes").transform.position;
                if (requireSight && Physics.Linecast(eye, hit.position + Vector3.up * 1.7f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (retainedCamera)
                {
                    var viewport = retainedCamera.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                    if (viewport.z <= retainedCamera.nearClipPlane || viewport.x < .1f || viewport.x > .9f ||
                        viewport.y < .1f || viewport.y > .9f) continue;
                }
                return hit.position;
            }
            Assert.Fail("No real same-floor NavMesh sample near distance " + distance + " from " + origin);
            return Vector3.zero;
        }

        void CandleDangerPlaceActor(Component actor, Vector3 at, bool facePlayer = false)
        {
            // This is the real mode-owned controller. Its brain remains enabled while observed;
            // only startup placement is isolated, and its real NavMeshAgent must be ready.
            actor.gameObject.SetActive(false);
            var agent = actor.GetComponent<NavMeshAgent>();
            Assert.That(agent, Is.Not.Null);
            agent.enabled = false;
            foreach (var startup in actor.GetComponents(RequireType("NavMeshStartup"))) ((Behaviour)startup).enabled = false;
            actor.transform.position = at;
            Set(actor, "player", player); Set(actor, "patrolSpeed", 0f); Set(actor, "chaseSpeed", 0f);
            Set(actor, "state", "Patrol"); ((Behaviour)actor).enabled = true;
            agent.updateRotation = false; actor.gameObject.SetActive(true); agent.enabled = true;
            Assert.That(agent.Warp(at), Is.True, "Actual mode-owned enemy did not bind to navigation");
            agent.ResetPath(); agent.isStopped = false; agent.velocity = Vector3.zero;
            var away = actor.transform.position - player.transform.position; away.y = 0;
            actor.transform.rotation = Quaternion.LookRotation(facePlayer ? -away : away);
            Physics.SyncTransforms();
            Assert.That(((Behaviour)actor).isActiveAndEnabled && agent.enabled && agent.isOnNavMesh, Is.True);
            Assert.That(Get<float>(actor, "HomeFloorY"), Is.EqualTo(at.y).Within(.2f));
            if (!facePlayer) Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False, "Proximity fixture accidentally detected the player");
        }

        bool CandleDangerPlayerSees(Component actor)
        {
            var method = LightRun.GetType().GetMethod("PlayerSees", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(LightRun, new object[] { actor });
        }

        void CandleDangerObserveFacingCameraOnly(Component actor)
        {
            var camera = Get<Camera>(player, "eyes");
            camera.transform.rotation = Quaternion.LookRotation(actor.transform.position + Vector3.up * 1.7f - camera.transform.position);
        }
        void CandleDangerObserve(Component actor)
        {
            var camera = Get<Camera>(player, "eyes");
            var renderer = actor.GetComponentsInChildren<Renderer>().FirstOrDefault(item =>
                item.enabled && (item is MeshRenderer || item is SkinnedMeshRenderer));
            Assert.That(renderer, Is.Not.Null, "Owned actor has no real visible monster geometry");
            camera.transform.rotation = Quaternion.LookRotation(renderer.bounds.center - camera.transform.position);
            Physics.SyncTransforms();
            Assert.That(CandleDangerPlayerSees(actor), Is.True,
                "Production camera/geometry/LOS did not actually see the owned monster");
        }

        void CandleDangerExpectSafe()
        {
            Call(LightRun, "RefreshDanger");
            Assert.That(Get<float>(LightRun, "Danger"), Is.EqualTo(0).Within(.0001f));
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
        }

        void CandleDangerExpectExtinguished()
        {
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.True);
            foreach (var candle in CandleDangerMarks())
            {
                Assert.That(Get<bool>(candle, "Lit"), Is.False, "Global blackout left a lit route marker");
                Assert.That(Get<bool>(candle, "IgnitionBlocked"), Is.True);
                Assert.That(Get<Light>(candle, "LocalLight").enabled, Is.False);
                Assert.That(Get<Transform>(candle, "Flame").gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleProximityFlickersAndTemporarilyBlackoutsOnlyPreviouslyLitMarkers()
        {
            yield return CandleDangerPrepare();
            var target = LightTargets("Candle")[0]; var candle = target.GetComponent(RequireType("WaymarkCandle"));
            PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(1));
            ((Behaviour)player).enabled = false; PlacePlayer(player.transform.position, false);
            var actor = CandleDangerOwnedBrain();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 14.5f, null, false));
            Get<Camera>(player, "eyes").transform.rotation = Quaternion.LookRotation(player.transform.position - actor.transform.position);
            CandleDangerExpectSafe();
            CandleDangerArmAll();
            var untouched = CandleDangerMarks().Last(); Call(untouched, "Restore", false);
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit") == (mark != untouched)), Is.True);
            Call(shell, "ToggleReducedMotion"); yield return null;
            float safeBrightness = Get<Light>(candle, "LocalLight").intensity;
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 8)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); yield return null;
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThan(0).And.LessThan(1));
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(Get<Light>(candle, "LocalLight").intensity, Is.LessThan(safeBrightness));
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit") == (mark != untouched) && Get<float>(mark, "Danger") > 0), Is.True);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 1.7f));
            Call(LightRun, "RefreshDanger"); CandleDangerExpectExtinguished();
            Assert.That(Get<bool>(target,"CanFocus"),Is.True,"Blackout candle did not expose its existing danger warning");
            Assert.That(Get<bool>(candle,"HasBeenLit"),Is.True,"Focus availability erased durable manual ignition");
            Assert.That((bool)Call(candle, "TryIgnite", player), Is.False);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(1), "Blocked ignition changed the event count");
            actor.gameObject.SetActive(false); CandleDangerExpectSafe(); yield return Delay(.15f);
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit") == (mark != untouched)), Is.True,
                "Safe distance must relight only previously ignited candles");
            Assert.That(CandleDangerMarks().All(mark => !Get<AudioSource>(mark, "IgnitionSource").isPlaying), Is.True,
                "Automatic recovery replayed the match strike");
            Assert.That(Get<bool>(candle, "IgnitionBlocked"), Is.False);
            Assert.That(Get<bool>(target,"CanFocus"),Is.False,"Automatically recovered candle kept an interaction prompt");
            ((Behaviour)player).enabled = true; PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target,false);
            Assert.That(Get<bool>(candle, "Lit"), Is.True);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(1), "Automatic recovery or redundant E counted a new ignition");
            Assert.That(Get<bool>(untouched, "HasBeenLit"), Is.False);
            Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "Lit")), Is.EqualTo(CandleDangerMarks().Length - 1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleActualFarSightStrengthensFlickerBeforeEnemyUpdateAndPhysicalCoverPreventsIt()
        {
            yield return CandleDangerPrepare();
            ((Behaviour)player).enabled = false;
            var world = Get<Transform>(LightRun, "Root").parent;
            var doors = LightTargets("Door").Where(door => door.transform.IsChildOf(world)).ToArray();
            foreach (var door in doors) Call(door, "OpenForPursuer");
            yield return Wait(() => doors.All(door => Get<bool>(door, "AtRequestedDoorPose")), 4, "Real corridor doors did not open for the LOS fixture");
            var run = Get<Component>(session, "Corridor"); var candidates = new System.Collections.Generic.List<Vector3>();
            for (int cell = 0; cell < 81; cell++)
                foreach (var offset in new[] { Vector3.zero, Vector3.right * 1.5f, Vector3.left * 1.5f, Vector3.forward * 1.5f, Vector3.back * 1.5f })
                    if (NavMesh.SamplePosition((Vector3)Call(run, "CellPosition", cell) + offset, out var hit, .35f, NavMesh.AllAreas)) candidates.Add(hit.position);
            var actor = CandleDangerOwnedBrain(); float sight = Get<float>(Get<object>(actor, "Senses"), "SightRange");
            Vector3 enemyPoint = Vector3.zero, playerPoint = Vector3.zero; bool found = false;
            foreach (var a in candidates)
            {
                foreach (var b in candidates)
                {
                    float distance = Vector3.Distance(a, b);
                    if (distance < 13 || distance > sight - .5f || Mathf.Abs(a.y - b.y) > .2f) continue;
                    if (Physics.Linecast(a + Vector3.up * 1.7f, b + Get<Camera>(player, "eyes").transform.localPosition,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    enemyPoint = a; playerPoint = b; found = true; break;
                }
                if (found) break;
            }
            Assert.That(found, Is.True, "No actual corridor LOS pair beyond12m inside the Watchman's real sight range");
            PlacePlayer(playerPoint, false); CandleDangerPlaceActor(actor, enemyPoint);
            Assert.That(Vector3.Distance(actor.transform.position, player.transform.position), Is.GreaterThan(12.5f));
            CandleDangerArmAll();
            var direction = playerPoint - enemyPoint; direction.y = 0;
            var cover = Cube("CandleDanger actual sight occluder", (playerPoint + enemyPoint) * .5f + Vector3.up * 1.5f, new Vector3(.3f, 3, 5));
            cover.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
            CandleDangerObserveFacingCameraOnly(actor); Physics.SyncTransforms();
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False);
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Patrol"));
            CandleDangerExpectSafe(); Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
            cover.SetActive(false); Physics.SyncTransforms();
            CandleDangerObserve(actor);
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False, "The visible enemy should still be looking away from the player");
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Patrol"), "Fixture allowed an AI Update before the immediate E guard");
            Call(LightRun, "RefreshDanger");
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThanOrEqualTo(.45f),
                "Seeing a real monster beyond12m must start a visible warning before its AI detects the player");
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False,
                "Detection at range must leave the candle available to visibly flicker");
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
            actor.transform.rotation = Quaternion.LookRotation(direction); Physics.SyncTransforms();
            yield return Wait(() => Get<object>(actor, "state").ToString() == "Chase", 2, "Actual source LOS did not start chase");
            var chase = new CandleFlickerWindow { label = "actual-far-sight-chase",
                enemyDistance = Vector3.Distance(enemyPoint, playerPoint), danger = Get<float>(LightRun, "Danger") };
            yield return CandleCaptureLiveFlicker(CandleDangerMarks()[0], chase);
            Assert.That(chase.depth, Is.GreaterThan(.5f), "Real pursuit suppressed the candle warning instead of making it visibly fluctuate");
            CloudExperienceTests.Artifact("candle-danger-far-sight-chase.json",
                System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(chase, true)));
            Object.Destroy(cover);
        }

        IEnumerator CandleNaturalMovement(Component actor, Vector3 destination)
        {
            var steps = AudioSteps(actor); int before = Get<int>(steps, "StepsPlayed");
            Set(actor, "patrolSpeed", 1.45f); Set(actor, "chaseSpeed", 1.45f);
            Assert.That((bool)Call(actor, "HearNoise", destination, 30f), Is.True,
                "Actual mode-owned walker did not accept the controlled investigation destination");
            yield return Wait(() => Get<int>(steps, "StepsPlayed") > before, 4,
                "Real NavMesh travel emitted no natural production movement contact");
            yield return TensionAdmissionFrames();
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleActualUnseenMovementStartsWarningAndRejectsUnheardContacts()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled = false;
            var target = LightTargets("Candle")[0]; PlacePlayer(LightApproach(target), false);
            var actor = CandleDangerOwnedBrain();
            Vector3 start = Vector3.zero, end = Vector3.zero, away = Vector3.zero; bool walkFound = false;
            var path = new NavMeshPath();
            for (int ray = 0; ray < 96; ray++)
            {
                float angle = ray * Mathf.PI / 48;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                if (!NavMesh.SamplePosition(player.transform.position + direction * 3, out var a, .22f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(player.transform.position + direction * 5.2f, out var b, .22f, NavMesh.AllAreas) ||
                    Mathf.Abs(a.position.y - player.transform.position.y) > .2f ||
                    Mathf.Abs(b.position.y - player.transform.position.y) > .2f ||
                    !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;
                start = a.position; end = b.position; away = direction; walkFound = true; break;
            }
            Assert.That(walkFound, Is.True, "No real near-player NavMesh path for naturally emitted movement");
            var cover = Cube("Candle hearing physical wall", (start + player.transform.position) * .5f + Vector3.up * 1.5f,
                new Vector3(.3f, 3, 6));
            cover.transform.rotation = Quaternion.FromToRotation(Vector3.right, away);
            CandleDangerPlaceActor(actor, start); CandleDangerObserveFacingCameraOnly(actor); Physics.SyncTransforms();
            Assert.That(CandleDangerPlayerSees(actor), Is.False);
            CandleDangerExpectSafe(); CandleDangerArmAll();
            var steps = AudioSteps(actor); var source = Get<AudioSource>(steps, "MovementSource");
            var acoustics = Get<Component>(steps, "MovementAcoustics");
            yield return Delay(.4f);
            Assert.That(Get<int>(steps, "StepsPlayed"), Is.Zero, "Silent stationary fixture emitted movement");
            CandleDangerExpectSafe();
            yield return CandleNaturalMovement(actor, end);
            Assert.That(Get<bool>(acoustics, "Occluded"), Is.True, "Footsteps were not behind real physical cover");
            Assert.That((bool)Call(LightRun, "HeardMovement", actor), Is.True,
                "Accepted naturally emitted unseen footsteps did not create actor-specific candle perception");
            Call(LightRun, "RefreshDanger");
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThanOrEqualTo(.45f));
            Assert.That(CandleDangerPlayerSees(actor), Is.False);
            // The active brain can refresh its navigation destination every Update.
            // Stop the real contact emitter explicitly instead of assuming isStopped
            // persists against that controller; retain actor perception eligibility.
            actor.GetComponent<NavMeshAgent>().isStopped = true;
            ((Behaviour)steps).enabled = false; source.Stop();
            Get<Camera>(player, "eyes").transform.rotation = Quaternion.LookRotation(-away);
            Assert.That(CandleDangerPlayerSees(actor), Is.False, "Memory expiry must have no continuing visual evidence");
            Call(shell, "Pause"); float frozen = Get<float>(LightRun, "Danger");
            yield return Delay(1.6f);
            Assert.That((bool)Call(LightRun, "HeardMovement", actor), Is.True, "Pause consumed perceived sound memory");
            Assert.That(Get<float>(LightRun, "Danger"), Is.EqualTo(frozen));
            Call(shell, "Resume");
            yield return Wait(() => !(bool)Call(LightRun, "HeardMovement", actor), 2, "Stopped cue perception did not expire");
            CandleDangerExpectSafe();

            ((Behaviour)steps).enabled = true;
            var movementVoice = ((IEnumerable)acoustics.GetType().GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(acoustics)).Cast<object>()
                .Single(voice => Get<AudioSource>(voice, "source") == source);
            float voiceGain = Get<float>(movementVoice, "gain"), range = source.maxDistance;
            foreach (string rejection in new[] { "Source mute", "Master zero", "Out of range", "Below audible gain" })
            {
                CandleDangerPlaceActor(actor, start); source.Stop();
                source.mute = rejection == "Source mute";
                if (rejection == "Master zero") Call(shell, "AdjustSettings", -1f, 0f);
                if (rejection == "Out of range") source.maxDistance = 2.1f;
                // Keep natural playback and real occlusion, with an explicitly
                // controlled physical source gain below the admission threshold.
                if (rejection == "Below audible gain") Set(movementVoice, "gain", .001f);
                yield return CandleNaturalMovement(actor, end);
                Assert.That((bool)Call(LightRun, "HeardMovement", actor), Is.False, rejection + " invented candle perception");
                CandleDangerExpectSafe(); source.Stop(); source.mute = false;
                source.maxDistance = range; Set(movementVoice, "gain", voiceGain);
                Call(shell, "RestoreDefaultSettings");
            }
            CandleDangerPlaceActor(actor, start); ((Behaviour)steps).enabled = false;
            yield return Delay(.3f); source.Stop(); CandleDangerExpectSafe();
            Assert.That((bool)Call(LightRun, "HeardMovement", actor), Is.False, "An unplayed source invented warning");
            actor.gameObject.SetActive(false); CandleDangerExpectSafe(); Object.Destroy(cover);
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            yield return CandleDangerPrepare();
            Assert.That(Get<float>(LightRun, "Danger"), Is.Zero, "Retry carried old sensory candle warning");
            Debug.Log("HAPPYTOY_CANDLE_PERCEPTION_PASS actual owned natural movement behind physical cover starts warning; silent, mute, master-zero, stopped/unplayed, inactive rejection; pause memory freeze and retry reset");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleDangerRejectsInactiveUnreadyWrongModeAndRealOtherFloorActors()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled = false;
            PlacePlayer(LightApproach(LightTargets("Candle")[0]), false);
            var actor = CandleDangerOwnedBrain(); var nearby = CandleDangerPoint(player.transform.position, 1.7f);
            CandleDangerPlaceActor(actor, nearby); ((Behaviour)actor).enabled = false; CandleDangerExpectSafe();
            Assert.That(actor.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, "Inactive-brain control also lost readiness");
            ((Behaviour)actor).enabled = true; actor.GetComponent<NavMeshAgent>().enabled = false; CandleDangerExpectSafe();
            Assert.That(((Behaviour)actor).isActiveAndEnabled, Is.True, "Unready-agent control also disabled its controller");
            actor.gameObject.SetActive(false); CandleDangerExpectSafe();
            var world = Get<Transform>(LightRun, "Root").parent;
            var authored = Components("StalkerBrain").First(brain => !brain.transform.IsChildOf(world) &&
                Get<object>(brain, "corridorRole").ToString() == "Authored");
            CandleDangerPlaceActor(authored, nearby); CandleDangerExpectSafe(); authored.gameObject.SetActive(false);
            // Find actual authored support on both floors. The enemy is within the warning
            // distance and NavMesh-ready; only its genuine other home floor excludes it.
            var vertices = NavMesh.CalculateTriangulation().vertices;
            var lower = vertices.Where(point => point.x < 100 && Mathf.Abs(point.y) < .2f).ToArray();
            var upper = vertices.Where(point => point.x < 100 && Mathf.Abs(point.y - 5) < .2f).ToArray();
            Vector3 low = Vector3.zero, high = Vector3.zero; bool found = false;
            foreach (var a in lower)
            {
                foreach (var b in upper)
                    if (Vector3.Distance(a, b) < 10 && NavMesh.SamplePosition(a, out var ah, .15f, NavMesh.AllAreas) &&
                        NavMesh.SamplePosition(b, out var bh, .15f, NavMesh.AllAreas))
                    { low = ah.position; high = bh.position; found = true; break; }
                if (found) break;
            }
            Assert.That(found, Is.True, "Need actual protected ground/upper navigation within12m for the floor-negative control");
            PlacePlayer(low, false); CandleDangerPlaceActor(actor, high);
            Get<Camera>(player, "eyes").transform.rotation = Quaternion.LookRotation(player.transform.position - actor.transform.position);
            Assert.That(Mathf.Abs(Get<float>(actor, "HomeFloorY") - player.transform.position.y), Is.GreaterThan(3));
            Assert.That(Vector3.Distance(actor.transform.position, player.transform.position), Is.LessThan(12));
            CandleDangerExpectSafe();
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleDormantSchoolMaskAndUnreleasedMannequinStayHarmlessWithReadyNavigation()
        {
            yield return CandleDangerPrepare(false); ((Behaviour)player).enabled = false;
            var chapter = Get<Component>(session, "Chapter");
            foreach (string name in new[] { "Mask", "Mannequin" })
            {
                var actor = Get<Component>(chapter, name); var agent = actor.GetComponent<NavMeshAgent>();
                Assert.That(actor && agent, Is.True);
                float originalHome = actor.transform.position.y;
                Assert.That(NavMesh.SamplePosition(actor.transform.position, out var anchor, 3, NavMesh.AllAreas), Is.True);
                PlacePlayer(CandleDangerPoint(anchor.position, 1.7f, null, false), false);
                Assert.That(Mathf.Abs(player.transform.position.y - originalHome), Is.LessThan(1.6f),
                    "Dormant negative control must share the actor's actual original floor");
                actor.gameObject.SetActive(true); ((Behaviour)actor).enabled = true;
                agent.enabled = true;
                Assert.That(agent.Warp(anchor.position), Is.True);
                Physics.SyncTransforms();
                Assert.That(agent.isOnNavMesh && ((Behaviour)actor).isActiveAndEnabled, Is.True);
                Assert.That(Vector3.Distance(actor.transform.position, player.transform.position), Is.LessThan(2));
                if (name == "Mask")
                {
                    Assert.That(Get<bool>(actor, "IntroCompleted"), Is.False);
                    Assert.That(Get<object>(actor, "State").ToString(), Is.EqualTo("Dormant"));
                }
                else Assert.That(Get<bool>(actor, "Released"), Is.False);
                CandleDangerExpectSafe(); actor.gameObject.SetActive(false);
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleDangerPauseFreezesThreatStateAndReducedMotionKeepsSteadyDimming()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled = false;
            PlacePlayer(LightApproach(LightTargets("Candle")[0]), false);
            var actor = CandleDangerOwnedBrain();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 3)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll(); Call(shell, "ToggleReducedMotion");
            yield return null; yield return null;
            var candle = CandleDangerMarks()[0]; var light = Get<Light>(candle, "LocalLight"); var flame = Get<Transform>(candle, "Flame");
            var flameRenderer = flame.GetComponentInChildren<Renderer>(); var flameProperties = new MaterialPropertyBlock();
            int emission = Shader.PropertyToID("_Emission"), opacity = Shader.PropertyToID("_Opacity");
            flameRenderer.GetPropertyBlock(flameProperties);
            float steadyEmission = flameProperties.GetFloat(emission), steadyOpacity = flameProperties.GetFloat(opacity);
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThan(0).And.LessThan(1));
            Assert.That(light.intensity, Is.GreaterThan(0).And.LessThan(.68f));
            float steady = light.intensity; var position = flame.localPosition; var rotation = flame.localRotation; var scale = flame.localScale;
            yield return Delay(.3f);
            Assert.That(light.intensity, Is.EqualTo(steady).Within(.00001f), "ReducedMotion still flashed the warning");
            Assert.That(flame.localPosition, Is.EqualTo(position)); Assert.That(flame.localRotation, Is.EqualTo(rotation));
            Assert.That(flame.localScale, Is.EqualTo(scale), "Comfort warning did not keep its captured dimmed scale steady");
            flameRenderer.GetPropertyBlock(flameProperties);
            Assert.That(flameProperties.GetFloat(emission), Is.EqualTo(steadyEmission)); Assert.That(flameProperties.GetFloat(opacity), Is.EqualTo(steadyOpacity));
            Call(shell, "Pause"); yield return null;
            float frozenDanger = Get<float>(LightRun, "Danger");
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 1.7f));
            Call(LightRun, "RefreshDanger"); yield return Delay(.2f);
            Assert.That(Get<float>(LightRun, "Danger"), Is.EqualTo(frozenDanger)); Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(Get<bool>(candle, "Lit"), Is.True); Assert.That(light.intensity, Is.EqualTo(steady));
            Assert.That(flame.localPosition, Is.EqualTo(position)); Assert.That(flame.localRotation, Is.EqualTo(rotation)); Assert.That(flame.localScale, Is.EqualTo(scale));
            Call(shell, "Resume");
            yield return Wait(() => Get<bool>(LightRun, "Blackout"), 2, "Resuming did not evaluate the actual nearby enemy");
            CandleDangerExpectExtinguished();
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator CandleTemporaryBlackoutPreservesIgnitionThroughBothModeCheckpointRoundTrips()
        {
            foreach (bool corridor in new[] { true, false })
            {
                yield return CandleDangerPrepare(corridor);
                var target = LightTargets("Candle")[0]; var candle = target.GetComponent(RequireType("WaymarkCandle"));
                PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
                ((Behaviour)player).enabled = false; PlacePlayer(player.transform.position, false);
                var actor = CandleDangerOwnedBrain();
                CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 1.7f));
                Call(LightRun, "RefreshDanger"); CandleDangerExpectExtinguished();
                var blackoutLighting = Call(LightRun, "Capture");
                Assert.That(((IEnumerable)Get<object>(blackoutLighting, "candles")).Cast<object>().Count(mark => Get<bool>(mark, "lit")), Is.EqualTo(1),
                    "Temporary darkness erased the durable ignition from the checkpoint");
                actor.gameObject.SetActive(false); CandleDangerExpectSafe(); Call(shell, "Pause");
                var saved = Call(session, corridor ? "CaptureCheckpoint" : "CaptureChapterCheckpoint");
                var lighting = Get<object>(saved, "lighting");
                Assert.That(((IEnumerable)Get<object>(lighting, "candles")).Cast<object>().Count(mark => Get<bool>(mark, "lit")), Is.EqualTo(1));
                var parsed = corridor ? CheckpointCopy(saved) : SchoolCheckpointCopy(saved);
                var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
                Call(session, corridor ? "CreateCorridor" : "CreateChapter", corridor ? new object[] { 73 } : Array.Empty<object>());
                Call(session, corridor ? "ApplyCheckpoint" : "ApplyChapterCheckpoint", parsed);
                Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "HasBeenLit")), Is.EqualTo(1));
                Assert.That(CandleDangerMarks().All(mark => Get<int>(mark, "Ignitions") == 0 &&
                    !Get<AudioSource>(mark, "IgnitionSource").isPlaying), Is.True, "Checkpoint replayed a manual ignition");
                IsolateThreats(); Begin(); yield return null; CandleDangerExpectSafe();
                Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "Lit")), Is.EqualTo(1), "Safe restored frame did not recover the lit marker");
                target = LightTargets("Candle")[0]; ((Behaviour)player).enabled = true;
                PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target,false);
                Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "Lit")), Is.EqualTo(1));
                Assert.That(Get<int>(target.GetComponent(RequireType("WaymarkCandle")), "Ignitions"), Is.EqualTo(0));
                if (corridor)
                { previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous); }
            }
        }
    }
}
