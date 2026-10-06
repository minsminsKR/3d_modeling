using System;
using System.Collections;
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

        Vector3 CandleDangerPoint(Vector3 origin, float distance)
        {
            for (int ray = 0; ray < 96; ray++)
            {
                float angle = ray * Mathf.PI / 48;
                var desired = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(desired, out var hit, .22f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - origin.y) > .2f ||
                    Mathf.Abs(Vector3.Distance(hit.position, origin) - distance) > .25f) continue;
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
            agent.ResetPath(); agent.velocity = Vector3.zero;
            var away = actor.transform.position - player.transform.position; away.y = 0;
            actor.transform.rotation = Quaternion.LookRotation(facePlayer ? -away : away);
            Physics.SyncTransforms();
            Assert.That(((Behaviour)actor).isActiveAndEnabled && agent.enabled && agent.isOnNavMesh, Is.True);
            Assert.That(Get<float>(actor, "HomeFloorY"), Is.EqualTo(at.y).Within(.2f));
            if (!facePlayer) Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False, "Proximity fixture accidentally detected the player");
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
        public IEnumerator CandleProximityDimsAllLitMarkersBlackoutsAndRequiresActualManualRelight()
        {
            yield return CandleDangerPrepare();
            var target = LightTargets("Candle")[0]; var candle = target.GetComponent(RequireType("WaymarkCandle"));
            PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(1));
            ((Behaviour)player).enabled = false; PlacePlayer(player.transform.position, false);
            var actor = CandleDangerOwnedBrain();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 14.5f));
            CandleDangerExpectSafe();
            CandleDangerArmAll();
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
            Call(shell, "ToggleReducedMotion"); yield return null;
            float safeBrightness = Get<Light>(candle, "LocalLight").intensity;
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 8));
            Call(LightRun, "RefreshDanger"); yield return null;
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThan(0).And.LessThan(1));
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(Get<Light>(candle, "LocalLight").intensity, Is.LessThan(safeBrightness));
            Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit") && Get<float>(mark, "Danger") > 0), Is.True);
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 1.7f));
            Call(LightRun, "RefreshDanger"); CandleDangerExpectExtinguished();
            Assert.That((bool)Call(candle, "TryIgnite", player), Is.False);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(1), "Blocked ignition changed the event count");
            actor.gameObject.SetActive(false); CandleDangerExpectSafe(); yield return Delay(.15f);
            Assert.That(CandleDangerMarks().All(mark => !Get<bool>(mark, "Lit")), Is.True, "Safe distance silently relit extinguished candles");
            Assert.That(Get<bool>(candle, "IgnitionBlocked"), Is.False);
            ((Behaviour)player).enabled = true; PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
            Assert.That(Get<bool>(candle, "Lit"), Is.True);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(2), "Actual E did not produce one new manual ignition");
            Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "Lit")), Is.EqualTo(1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleActualFarSightBlackoutsBeforeEnemyUpdateAndPhysicalCoverPreventsIt()
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
            actor.transform.rotation = Quaternion.LookRotation(direction); Physics.SyncTransforms();
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False);
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Patrol"));
            CandleDangerExpectSafe(); Assert.That(CandleDangerMarks().All(mark => Get<bool>(mark, "Lit")), Is.True);
            cover.SetActive(false); Physics.SyncTransforms();
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.True, "The real cone/range/eye physics did not see the player");
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Patrol"), "Fixture allowed an AI Update before the immediate E guard");
            Call(LightRun, "RefreshDanger"); CandleDangerExpectExtinguished();
            Assert.That((bool)Call(CandleDangerMarks()[0], "TryIgnite", player), Is.False);
            yield return Wait(() => Get<object>(actor, "state").ToString() == "Chase", 2, "Actual source LOS did not start chase");
            Object.Destroy(cover);
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
                PlacePlayer(CandleDangerPoint(anchor.position, 1.7f), false);
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
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 8));
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll(); Call(shell, "ToggleReducedMotion");
            yield return null; yield return null;
            var candle = CandleDangerMarks()[0]; var light = Get<Light>(candle, "LocalLight"); var flame = Get<Transform>(candle, "Flame");
            Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThan(0).And.LessThan(1));
            Assert.That(light.intensity, Is.GreaterThan(0).And.LessThan(.68f));
            float steady = light.intensity; var position = flame.localPosition; var rotation = flame.localRotation; var scale = flame.localScale;
            yield return Delay(.3f);
            Assert.That(light.intensity, Is.EqualTo(steady).Within(.00001f), "ReducedMotion still flashed the warning");
            Assert.That(flame.localPosition, Is.EqualTo(position)); Assert.That(flame.localRotation, Is.EqualTo(rotation));
            Assert.That(flame.localScale, Is.EqualTo(scale), "Comfort warning did not keep its captured dimmed scale steady");
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
        public IEnumerator CandleBlackoutPersistsExtinguishedThroughBothModeCheckpointRoundTrips()
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
                actor.gameObject.SetActive(false); CandleDangerExpectSafe(); Call(shell, "Pause");
                var saved = Call(session, corridor ? "CaptureCheckpoint" : "CaptureChapterCheckpoint");
                var lighting = Get<object>(saved, "lighting");
                Assert.That(((IEnumerable)Get<object>(lighting, "candles")).Cast<object>().All(mark => !Get<bool>(mark, "lit")), Is.True);
                var parsed = corridor ? CheckpointCopy(saved) : SchoolCheckpointCopy(saved);
                var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
                Call(session, corridor ? "CreateCorridor" : "CreateChapter", corridor ? new object[] { 73 } : Array.Empty<object>());
                Call(session, corridor ? "ApplyCheckpoint" : "ApplyChapterCheckpoint", parsed);
                Assert.That(CandleDangerMarks().All(mark => !Get<bool>(mark, "Lit") && Get<int>(mark, "Ignitions") == 0 &&
                    !Get<AudioSource>(mark, "IgnitionSource").isPlaying && !Get<Light>(mark, "LocalLight").enabled), Is.True,
                    "Checkpoint relit or replayed an extinguished candle");
                IsolateThreats(); Begin(); yield return null; CandleDangerExpectSafe();
                Assert.That(CandleDangerMarks().All(mark => !Get<bool>(mark, "Lit")), Is.True, "First safe restored frame auto-relit markers");
                target = LightTargets("Candle")[0]; ((Behaviour)player).enabled = true;
                PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
                Assert.That(CandleDangerMarks().Count(mark => Get<bool>(mark, "Lit")), Is.EqualTo(1));
                Assert.That(Get<int>(target.GetComponent(RequireType("WaymarkCandle")), "Ignitions"), Is.EqualTo(1));
                if (corridor)
                { previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous); }
            }
        }
    }
}
