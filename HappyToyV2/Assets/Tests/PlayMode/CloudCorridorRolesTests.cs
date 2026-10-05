using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component[] PrepareRoleFixture()
        {
            Call(session, "CreateCorridor", 73); Begin();
            var actors = Components("StalkerBrain").Where(x => x.name.EndsWith("— corridor")).ToArray();
            Assert.That(actors.Select(x => Get<object>(x, "corridorRole").ToString()),
                Is.EquivalentTo(new[] { "Watchman", "Listener", "Tracker", "Wanderer" }));
            foreach (var actor in actors) actor.gameObject.SetActive(false);
            foreach (var door in Components("Interactable").Where(x => x.name == "Corridor sliding door")) Call(door, "OpenForPursuer");
            return actors;
        }
        Vector3 RoleCell(int cell) => (Vector3)Call(Get<Component>(session, "Corridor"), "CellPosition", cell);
        void StartRoleActor(Component actor, string role, Vector3 at, Vector3 facing)
        {
            actor.gameObject.SetActive(false);
            Assert.That(NavMesh.SamplePosition(at, out var floor, 1.5f, NavMesh.AllAreas), Is.True);
            at = floor.position;
            Set(actor, "corridorRole", role); Set(actor, "state", "Patrol"); Set(actor, "patrol", new Transform[0]);
            actor.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing));
            actor.gameObject.SetActive(true);
            var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = true; Assert.That(agent.Warp(at), Is.True);
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorWatchmanSeesLongLaneAndListenerHearsRealQuietWalking()
        {
            var actors = PrepareRoleFixture(); yield return Delay(1.8f);
            Vector3 source = Vector3.zero, target = Vector3.zero; bool found = false;
            for (int a = 0; a < 81 && !found; a++) for (int b = 0; b < 81 && !found; b++)
            {
                var from = RoleCell(a); var to = RoleCell(b); float distance = Vector3.Distance(from, to);
                if (distance < 15 || distance > 18 || Physics.Linecast(from + Vector3.up * 1.7f,
                    to + Vector3.up * 1.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var insideRange = to - (to - from).normalized * .8f;
                if (!NavMesh.SamplePosition(from, out var start, .2f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(insideRange, out var end, .2f, NavMesh.AllAreas)) continue;
                source = start.position; target = end.position; found = true;
            }
            Assert.That(found, Is.True, "No genuine long visible corridor lane in seed 73");
            var actor = actors[0]; var direction = (target - source).normalized;
            ((Behaviour)player).enabled = false; PlacePlayer(target, false);
            StartRoleActor(actor, "Watchman", source, direction);
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.True, "Watchman missed unobstructed long lane");
            Set(actor, "corridorRole", "Authored");
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False, "Original school sight changed");
            Set(actor, "corridorRole", "Listener");
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False);
            // A wall must still defeat the longest visual profile.
            Set(actor, "corridorRole", "Watchman");
            var blocker = Cube("CloudQA role sight wall", (source + target) * .5f + Vector3.up,
                new Vector3(2, 3, .25f)); blocker.transform.rotation = Quaternion.LookRotation(direction);
            Physics.SyncTransforms(); Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False);
            Object.Destroy(blocker); yield return null;
            Get<Light>(player, "flashlight").enabled = false;
            ((Behaviour)player).enabled = true;
            foreach (string role in new[] { "Authored", "Listener" })
            {
                Keys(); StartRoleActor(actor, role, source, -direction);
                PlacePlayer(source + direction * 5.9f); player.transform.rotation = Quaternion.LookRotation(-direction);
                yield return Wait(() => Get<bool>(player, "Grounded"), 2, "Role hearing lane did not ground the player");
                Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False, "Footstep evidence must come from behind the actor");
                int accepted = Get<int>(actor, "FootstepNoisesAccepted");
                var feedback = Get<Component>(player, "Feedback"); int steps = Get<int>(feedback, "FootstepsPlayed");
                yield return KeysObserved(Key.W);
                yield return Wait(() => Get<int>(feedback, "FootstepsPlayed") > steps, 2, "Actual walking emitted no foot contact");
                Keys(); yield return null;
                Assert.That(Vector3.Distance(player.transform.position, actor.transform.position), Is.GreaterThan(4.1f),
                    "Comparison wandered inside the original hearing radius");
                Assert.That(Get<float>(player, "FootstepNoiseRadius"), Is.EqualTo(4), "Fixture did not produce dry walking");
                Assert.That(Get<int>(actor, "FootstepNoisesAccepted") > accepted, Is.EqualTo(role == "Listener"),
                    "Role hearing did not distinguish the same physical walking stimulus");
                Call(shell, "Pause"); int pausedCount = Get<int>(actor, "FootstepNoisesAccepted");
                yield return Delay(.2f); Assert.That(Get<int>(actor, "FootstepNoisesAccepted"), Is.EqualTo(pausedCount));
                Call(shell, "Resume");
            }
        }
        [UnityTest, Timeout(30000)]
        public IEnumerator CorridorTrackerSearchesObservedEvidenceLongerWithoutTrackingHiddenPlayer()
        {
            var actors = PrepareRoleFixture(); yield return Delay(1.8f);
            ((Behaviour)player).enabled = false; PlacePlayer(RoleCell(0) + Vector3.up * 5, false);
            for (int i = 0; i < 2; i++)
            {
                var at = RoleCell(i == 0 ? 0 : 40);
                StartRoleActor(actors[i], i == 0 ? "Authored" : "Tracker", at, Vector3.forward);
                Assert.That((bool)Call(actors[i], "HearNoise", actors[i].transform.position, 5f), Is.True);
                Set(actors[i], "state", "Chase");
            }
            yield return Wait(() => actors.Take(2).All(x => Get<object>(x, "state").ToString() == "Search"), 2,
                "Actors failed to inspect their reached evidence");
            var origins = actors.Take(2).Select(x => Get<Vector3>(x, "SearchOrigin")).ToArray();
            PlacePlayer(RoleCell(80) + Vector3.up * 5, false);
            yield return Delay(7.5f);
            Assert.That(Get<object>(actors[0], "state").ToString(), Is.EqualTo("Patrol"));
            Assert.That(Get<object>(actors[1], "state").ToString(), Is.EqualTo("Search"));
            Assert.That(Get<Vector3>(actors[1], "SearchOrigin"), Is.EqualTo(origins[1]), "Search followed a new unseen player position");
            yield return Wait(() => Get<object>(actors[1], "state").ToString() == "Patrol", 4,
                "Tracker's longer search did not end");
        }
        [UnityTest, Timeout(30000)]
        public IEnumerator CorridorWandererPhysicallyDwellsLooksAndResumesPatrolAfterPause()
        {
            var actors = PrepareRoleFixture(); yield return Delay(1.8f);
            ((Behaviour)player).enabled = false; PlacePlayer(RoleCell(80) + Vector3.up * 5, false);
            var actor = actors[3]; var start = RoleCell(0);
            StartRoleActor(actor, "Wanderer", start, Vector3.forward);
            var markers = new[] { new GameObject("CloudQA arrived waypoint").transform, new GameObject("CloudQA next waypoint").transform };
            markers[0].position = start;
            var layout = Get<object>(Get<Component>(session, "Corridor"), "Layout");
            var edges = (int[])layout.GetType().GetField("Connections").GetValue(layout);
            int direction = Enumerable.Range(0, 4).First(x => (edges[0] & 1 << x) != 0);
            int next = (int)layout.GetType().GetMethod("Neighbor").Invoke(null, new object[] { 0, direction });
            markers[1].position = RoleCell(next); Set(actor, "patrol", markers);
            var before = actor.transform.rotation; yield return Delay(.7f);
            Assert.That(Vector3.Distance(actor.transform.position, start), Is.LessThan(.08f));
            Assert.That(Quaternion.Angle(before, actor.transform.rotation), Is.GreaterThan(20), "Dwell did not visibly inspect surroundings");
            Call(shell, "Pause"); var paused = actor.transform.rotation;
            yield return Delay(.3f); Assert.That(actor.transform.rotation, Is.EqualTo(paused)); Call(shell, "Resume");
            yield return Wait(() => Vector3.Distance(actor.transform.position, start) > .5f, 4,
                "Wanderer never resumed physical patrol");
            foreach (var marker in markers) Object.Destroy(marker.gameObject);
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorActualRoleDefeatHintFitsLargeTextResultAtThreeAspectRatios()
        {
            var actors = PrepareRoleFixture(); yield return Delay(1.8f);
            Call(shell, "RestoreDefaultSettings"); Call(shell, "ToggleLargeText");
            ((Behaviour)player).enabled = false; var start = RoleCell(0);
            PlacePlayer(start + Vector3.forward * .9f, false);
            StartRoleActor(actors[0], "Watchman", start, Vector3.forward);
            yield return Wait(() => Get<bool>(session, "Finished"), 5, "Actual role attack never reached the result screen");
            Assert.That(Get<string>(session, "DefeatHint"), Does.Contain("사이클롭스"));
            Assert.That(Get<string>(session, "DefeatHint"), Does.Contain("공격 예고"));
            var errors = new List<string>(); var view = One("GameShellView");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                yield return CaptureMenu(view, "corridor-role-result-large-" + size.x + "x" + size.y + ".png", size.x, size.y, errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }
        [UnityTest, Timeout(30000)]
        public IEnumerator CorridorAllCabinetExitsHavePhysicalStandingClearance()
        {
            PrepareRoleFixture(); yield return Delay(1.8f);
            ((Behaviour)player).enabled = false;
            foreach (var cabinet in Components("Interactable").Where(x => x.name == "Corridor hiding cabinet"))
            {
                var inside = Get<Transform>(cabinet, "inside").position;
                var outside = Get<Transform>(cabinet, "outside").position;
                PlacePlayer(outside);
                Call(player, "Hide", cabinet, inside, outside); Assert.That(Get<bool>(player, "Hidden"), Is.True);
                Call(player, "LeaveHiding");
                Assert.That(Get<bool>(player, "Hidden"), Is.False, "Static furniture trapped cabinet exit at " + outside);
                Assert.That(player.transform.position, Is.EqualTo(outside));
            }
        }
        [UnityTest, Timeout(30000)]
        public IEnumerator CorridorPatrolCanLeaveItsPhysicalMemoryAltarWaypoint()
        {
            var actors = PrepareRoleFixture(); yield return Delay(1.8f);
            ((Behaviour)player).enabled = false; PlacePlayer(RoleCell(80) + Vector3.up * 5, false);
            var actor = actors[0]; var markers = Get<Transform[]>(actor, "patrol");
            // This is the production relic waypoint beside the real altar,
            // followed by its production supply waypoint, not empty-room markers.
            StartRoleActor(actor, "Watchman", markers[1].position, Vector3.forward);
            Set(actor, "patrol", new[] { markers[1], markers[2] });
            var start = actor.transform.position;
            yield return Wait(() => Vector3.Distance(actor.transform.position, start) > 1, 4,
                "Patrol remained stuck at an altar waypoint whose raw centre is occupied by furniture");
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Patrol"));
            Assert.That(Mathf.Abs(actor.transform.position.y), Is.LessThan(.2f));
        }
    }
}
