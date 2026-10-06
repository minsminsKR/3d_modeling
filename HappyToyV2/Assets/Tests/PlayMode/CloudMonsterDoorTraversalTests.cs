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
        // These are controlled actors on actual school/corridor geometry and the
        // production NavMesh. No substitute door, motion time scale or AI warp is
        // used after the fixture's initial supported placement.
        Component TraversalDoor(bool corridor)
        {
            foreach (var door in Components("Interactable").Where(x =>
                Get<object>(x, "kind").ToString() == "Door" && Get<Transform>(x, "movingLeaf") &&
                (corridor ? x.name == "Corridor sliding door" : Get<Transform>(x, "secondaryLeaf")) &&
                Mathf.Abs(x.transform.position.y) < .25f))
            {
                var normal = Get<Vector3>(door, "DoorNormal");
                var a = door.transform.position - normal * 1.8f; a.y = .03f;
                var b = door.transform.position + normal * 2.1f; b.y = .03f;
                if (!NavMesh.SamplePosition(a, out var near, .3f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(b, out var far, .3f, NavMesh.AllAreas) ||
                    Vector3.Distance(near.position, a) > .3f || Vector3.Distance(far.position, b) > .3f) continue;
                var route = new NavMeshPath();
                if (!NavMesh.CalculatePath(near.position, far.position, NavMesh.AllAreas, route) ||
                    route.status != NavMeshPathStatus.PathComplete) continue;
                if (string.IsNullOrEmpty(Get<string>(door, "stableId"))) Set(door, "stableId", "school-door-traversal-fixture");
                return door;
            }
            Assert.Fail("No two-sided real closed door route was found in " + (corridor ? "corridor" : "school"));
            return null;
        }

        Vector3 SupportedDoorSide(Component door, float side, float lateral = 0)
        {
            var slide = door.transform.TransformVector(Get<Vector3>(door, "openOffset")).normalized;
            var point = door.transform.position + Get<Vector3>(door, "DoorNormal") * side + slide * lateral;
            point.y = .03f;
            Assert.That(NavMesh.SamplePosition(point, out var hit, .3f, NavMesh.AllAreas), Is.True, "No native floor at " + point);
            Assert.That(Vector3.Distance(hit.position, point), Is.LessThan(.3f));
            return hit.position;
        }
        void DoorBodyClear(Component door, Component brain)
        {
            var agent = brain.GetComponent<NavMeshAgent>();
            float radius = agent.radius - .015f;
            var feet = brain.transform.position;
            var overlaps = Physics.OverlapCapsule(feet + Vector3.up * (radius + .09f),
                feet + Vector3.up * (agent.height - radius), radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var primary = Get<Transform>(door, "movingLeaf");
            var secondary = Get<Transform>(door, "secondaryLeaf");
            Assert.That(overlaps.Any(x => x.transform.IsChildOf(primary) || secondary && x.transform.IsChildOf(secondary)),
                Is.False, "Native actor penetrated moving door: " + PursuitDiagnostics(brain));
        }

        IEnumerator RealDoorCycle(Component door, string state, Vector3 initial, Vector3 initialSecondary)
        {
            var start = SupportedDoorSide(door, -1.8f);
            var destination = SupportedDoorSide(door, 2.1f);
            var normal = Get<Vector3>(door, "DoorNormal");
            var primary = Get<Transform>(door, "movingLeaf");
            var secondary = Get<Transform>(door, "secondaryLeaf");
            Assert.That(Get<bool>(door, "IsOpen"), Is.False);
            Assert.That(Vector3.Distance(primary.localPosition,initial),Is.LessThan(.00002f),"Next cycle began before the original closed endpoint");
            if(secondary)Assert.That(Vector3.Distance(secondary.localPosition,initialSecondary),Is.LessThan(.00002f));
            var oldAudio = door.GetComponent(RequireType("InteractionAudio"));
            int before = oldAudio ? Get<int>(oldAudio, "CuesPlayed") : 0;
            var brain = MovingStalker(start, normal);
            var marker = new GameObject("CloudQA real door destination"); marker.transform.position = destination;
            Set(brain, "patrol", new[] { marker.transform });
            if (state != "Patrol")
            {
                Assert.That((bool)Call(brain, "HearNoise", destination, 20f), Is.True,
                    "An operable closed door rejected native evidence routing");
                Set(brain, "state", state);
            }
            float end = Time.realtimeSinceStartup + 13;
            bool opened = false, crossed = false;
            var previous = brain.transform.position;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                Physics.SyncTransforms(); DoorBodyClear(door, brain);
                Assert.That(Vector3.Distance(previous, brain.transform.position),
                    Is.LessThan(brain.GetComponent<NavMeshAgent>().speed * Mathf.Max(Time.deltaTime, .016f) + .15f),
                    "Door traversal used a position jump");
                previous = brain.transform.position;
                opened |= Get<bool>(door, "IsOpen");
                crossed |= Vector3.Dot(brain.transform.position - door.transform.position, normal) > .8f;
                if (opened && crossed && !Get<bool>(door, "IsOpen") &&
                    Vector3.Distance(primary.localPosition,initial)<.00001f &&
                    (!secondary || Vector3.Distance(secondary.localPosition,initialSecondary)<.00001f)) break;
            }
            Assert.That(opened, Is.True, "No " + state + " opening: " + PursuitDiagnostics(brain));
            Assert.That(crossed, Is.True, "No " + state + " crossing: " + PursuitDiagnostics(brain));
            Assert.That(Get<bool>(door, "IsOpen"), Is.False, "No " + state + " close behind the actor");
            Assert.That(Vector3.Distance(primary.localPosition,initial),Is.LessThan(.00002f));
            if (secondary) Assert.That(Vector3.Distance(secondary.localPosition,initialSecondary),Is.LessThan(.00002f));
            var audio = door.GetComponent(RequireType("InteractionAudio"));
            Assert.That(Get<int>(audio, "CuesPlayed"), Is.EqualTo(before + 2), "Shared leaf needs one opening and one closing handle cue");
            Assert.That(Get<bool>(audio, "LastOpening"), Is.False);
            Object.Destroy(brain.gameObject); Object.Destroy(marker); yield return null; yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MonsterDoorTraversalRealCorridorOpensAndClosesInEveryMovementState()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(true);
            var closedPrimary=Get<Transform>(door,"movingLeaf").localPosition;
            var secondary=Get<Transform>(door,"secondaryLeaf");var closedSecondary=secondary?secondary.localPosition:Vector3.zero;
            foreach (string state in new[] { "Patrol", "Investigate", "Chase" }) yield return RealDoorCycle(door, state,closedPrimary,closedSecondary);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MonsterDoorTraversalRealSchoolOpensAndClosesBothLeavesInEveryMovementState()
        {
            Call(shell, "BeginChapter"); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(false);
            var obstacle = Get<NavMeshObstacle>(door, "obstacle");
            Assert.That(obstacle.enabled, Is.True, "The closed route lost its physical obstruction");
            Assert.That(obstacle.carving, Is.False, "An operable school edge still rejects approach routes");
            var closedPrimary=Get<Transform>(door,"movingLeaf").localPosition;
            var closedSecondary=Get<Transform>(door,"secondaryLeaf").localPosition;
            foreach (string state in new[] { "Patrol", "Investigate", "Chase" }) yield return RealDoorCycle(door, state,closedPrimary,closedSecondary);
            ((Behaviour)door).enabled = false;
            Assert.That(obstacle.carving, Is.True, "Disabling runtime door ownership did not restore authored navigation");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MonsterDoorTraversalTwoActorsShareOneOpeningAndLastActorCloses()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(true); var normal = Get<Vector3>(door, "DoorNormal");
            var actors = new[] { MovingStalker(SupportedDoorSide(door, -1.8f, -.5f), normal),
                MovingStalker(SupportedDoorSide(door, -1.8f, .5f), normal) };
            for (int i = 0; i < actors.Length; i++)
                Assert.That((bool)Call(actors[i], "HearNoise", SupportedDoorSide(door, 2.1f, i == 0 ? -.5f : .5f), 20f), Is.True);
            bool opened = false; float end = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null; Physics.SyncTransforms();
                foreach (var actor in actors) DoorBodyClear(door, actor);
                opened |= Get<bool>(door, "IsOpen");
                bool crossed = actors.All(actor => Vector3.Dot(actor.transform.position - door.transform.position, normal) > .8f);
                if (opened && crossed && !Get<bool>(door, "IsOpen") && Get<bool>(door, "AtRequestedDoorPose")) break;
            }
            Assert.That(actors.All(actor => Vector3.Dot(actor.transform.position - door.transform.position, normal) > .8f), Is.True);
            Assert.That(Get<bool>(door, "IsOpen"), Is.False);
            Assert.That(Get<bool>(door, "AtRequestedDoorPose"), Is.True);
            Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")), "CuesPlayed"), Is.EqualTo(2));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MonsterDoorTraversalPlayerBlocksClosingAndInFlightLeafReopensWithoutDuplicateCue()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(true); var leaf = Get<Transform>(door, "movingLeaf");
            Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
            Assert.That((bool)Call(door, "OpenForPursuer"), Is.False);
            Call(door, "Use", player); // simultaneous opposite request cannot stack another cue
            Assert.That(Get<bool>(door, "IsOpen"), Is.True);
            yield return Wait(() => Get<bool>(door, "AtRequestedDoorPose"), 4, "Actual leaf failed to open");
            var audio = door.GetComponent(RequireType("InteractionAudio"));
            PlacePlayer(door.transform.position + Vector3.up * .03f, false);
            Call(door, "Use", player); Assert.That(Get<bool>(door, "IsOpen"), Is.True);
            Assert.That(Get<int>(audio, "CuesPlayed"), Is.EqualTo(1));
            PlacePlayer(new Vector3(200, 5, 200), false); yield return null;
            Call(door, "Use", player); Assert.That(Get<bool>(door, "IsOpen"), Is.False);
            yield return Delay(.2f);
            PlacePlayer(door.transform.position + Vector3.up * .03f, false); Physics.SyncTransforms();
            yield return null; yield return null;
            Assert.That(Get<bool>(door, "IsOpen"), Is.True, "A closing leaf did not yield to an entering body");
            Assert.That(Get<int>(audio, "CuesPlayed"), Is.EqualTo(3), "Reversal stacked or repeated handle cues");
            yield return Delay(.2f); Assert.That(Get<int>(audio, "CuesPlayed"), Is.EqualTo(3));
            Call(shell, "Pause"); var pose = leaf.localPosition;
            yield return Delay(.2f); Assert.That(leaf.localPosition, Is.EqualTo(pose));
            Call(shell, "Resume");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MonsterDoorTraversalBlockedPatrolSkipsUnreachableMarkerWithoutCrossing()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(true); var normal = Get<Vector3>(door, "DoorNormal");
            var brain = MovingStalker(SupportedDoorSide(door, -1.8f), normal);
            var blocked = new GameObject("CloudQA blocked patrol marker"); blocked.transform.position = SupportedDoorSide(door, 2.1f);
            var reachable = new GameObject("CloudQA reachable patrol marker"); reachable.transform.position = SupportedDoorSide(door, -2.6f, .7f);
            Set(brain, "patrol", new[] { blocked.transform, reachable.transform });
            ((Behaviour)door).enabled = false;
            var leaf = Get<Transform>(door, "movingLeaf"); var initial = leaf.localPosition;
            float end = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < end && Vector3.Distance(brain.transform.position, reachable.transform.position) > .6f)
            {
                yield return null; Physics.SyncTransforms(); DoorBodyClear(door, brain);
                Assert.That(Vector3.Dot(brain.transform.position - door.transform.position, normal), Is.LessThan(-.3f));
                Assert.That(leaf.localPosition, Is.EqualTo(initial));
            }
            Assert.That(Vector3.Distance(brain.transform.position, reachable.transform.position), Is.LessThan(.6f),
                "Blocked patrol never selected its reachable alternate marker: " + PursuitDiagnostics(brain));
            Assert.That(Get<bool>(door, "IsOpen"), Is.False);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MonsterDoorTraversalArrivalScanKeepsPassageAndNextNoiseClosesBehind()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door=TraversalDoor(true); var normal=Get<Vector3>(door,"DoorNormal");
            var leaf=Get<Transform>(door,"movingLeaf"); var closed=leaf.localPosition;
            var brain=MovingStalker(SupportedDoorSide(door,-1.8f),normal);
            // Controlled lost-sight state on a genuine accepted evidence route.
            // Production Chase itself enters Search within .65m of this point.
            Assert.That((bool)Call(brain,"HearNoise",SupportedDoorSide(door,.9f),20f),Is.True);
            Set(brain,"state","Chase");
            yield return Wait(() =>
            {
                var progress=Call(brain,"CaptureProgress");
                return Get<object>(progress,"state").ToString()=="Search" &&
                    Get<bool>(progress,"searchArrived") && Get<int>(progress,"visited")>0;
            },10,"Near-door evidence never reached its actual arrival scan",()=>PursuitDiagnostics(brain));
            var arrived=Call(brain,"CaptureProgress");
            Assert.That(Get<string>(arrived,"door"),Is.EqualTo(Get<string>(door,"stableId")));
            Assert.That(Get<bool>(arrived,"passingDoor"),Is.True);
            Assert.That(Vector3.Dot(brain.transform.position-door.transform.position,normal),
                Is.LessThan(brain.GetComponent<NavMeshAgent>().radius+.5f),"Fixture already cleared the close-behind threshold");
            var audio=door.GetComponent(RequireType("InteractionAudio"));
            Assert.That(Get<int>(audio,"CuesPlayed"),Is.EqualTo(1));
            var at=brain.transform.position;
            yield return Delay(.65f); // longer than the .5s shared-passage lease
            Physics.SyncTransforms(); DoorBodyClear(door,brain);
            var scanning=Call(brain,"CaptureProgress");
            Assert.That(Get<bool>(scanning,"searchArrived"),Is.True);
            Assert.That(Vector3.Distance(brain.transform.position,at),Is.LessThan(.02f));
            Assert.That(Get<string>(scanning,"door"),Is.EqualTo(Get<string>(door,"stableId")),"Arrival discarded the open passage before body clearance");
            Assert.That(Get<bool>(scanning,"passingDoor"),Is.True);
            Assert.That(Get<bool>(door,"IsOpen"),Is.True);
            Assert.That(Get<int>(audio,"CuesPlayed"),Is.EqualTo(1),"The stationary scan issued a new handle request");
            // A new legitimate noise is permitted to retarget the observed route;
            // no private door method or actor position is used to finish passage.
            Assert.That((bool)Call(brain,"HearNoise",SupportedDoorSide(door,2.1f),3f),Is.True);
            yield return Wait(() => Vector3.Dot(brain.transform.position-door.transform.position,normal)>.8f &&
                !Get<bool>(door,"IsOpen") && Vector3.Distance(leaf.localPosition,closed)<.00001f,
                8,"The resumed actual route lost its close-behind passage",()=>PursuitDiagnostics(brain));
            Physics.SyncTransforms(); DoorBodyClear(door,brain);
            Assert.That(Get<int>(audio,"CuesPlayed"),Is.EqualTo(2));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MonsterDoorTraversalRestoredPassageKeepsEntrySideAndClosesAfterCrossing()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            yield return null; yield return null;
            var door = TraversalDoor(true); var normal = Get<Vector3>(door, "DoorNormal");
            var brain = MovingStalker(SupportedDoorSide(door, -1.8f), normal);
            Assert.That((bool)Call(brain, "HearNoise", SupportedDoorSide(door, 2.1f), 20f), Is.True);
            yield return Wait(() => Get<bool>(door, "IsOpen"), 6, "No actual opening before save fixture");
            Call(shell, "Pause");
            var captured = Call(brain, "CaptureProgress");
            var saved = JsonUtility.FromJson(JsonUtility.ToJson(captured), captured.GetType());
            Assert.That(Get<bool>(saved, "passingDoor"), Is.True);
            Assert.That(Mathf.Abs(Get<float>(saved, "doorEntrySide")), Is.EqualTo(1));
            brain.gameObject.SetActive(false);
            var restored = MovingStalker(Get<Vector3>(saved, "position"), normal);
            var doors = Array.CreateInstance(RequireType("Interactable"), 1); doors.SetValue(door, 0);
            Call(restored, "RestoreProgress", saved, doors);
            var leaf = Get<Transform>(door, "movingLeaf"); var pose = leaf.localPosition;
            yield return Delay(.2f); Assert.That(leaf.localPosition, Is.EqualTo(pose));
            Call(shell, "Resume");
            yield return Wait(() => Vector3.Dot(restored.transform.position - door.transform.position, normal) > .8f &&
                !Get<bool>(door, "IsOpen") && Get<bool>(door, "AtRequestedDoorPose"), 8,
                "Restored passage forgot its approach side or close-behind", () => PursuitDiagnostics(restored));
            Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")), "CuesPlayed"), Is.EqualTo(2),
                "Restoring an in-flight door replayed its opening sound");
        }
    }
}
