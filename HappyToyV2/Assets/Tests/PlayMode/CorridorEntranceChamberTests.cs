using System;
using System.Collections;
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
        IEnumerator PrepareEntranceCavern()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin();
            yield return null; yield return null;
            Assert.That(Get<Component>(Get<Component>(session, "Corridor"), "Entrance"), Is.Not.Null);
        }
        static void AssertNoEntranceGuide(Component run)
        {
            Assert.That(run.GetComponentsInChildren<TextMesh>(true).Any(sign => sign.name == "Memory-return door clue" ||
                sign.text.Contains("돌아갈 수 없는 입구") || sign.text.Contains("기억은 붉은 교실로")), Is.False,
                "The removed entrance instruction is still authored in the physical world");
        }
        static void AssertEntrancePath(Vector3 from, Vector3 to)
        {
            Assert.That(NavMesh.SamplePosition(from, out var start, .5f, NavMesh.AllAreas), Is.True, "Entrance route has no starting floor: " + from);
            Assert.That(NavMesh.SamplePosition(to, out var end, .5f, NavMesh.AllAreas), Is.True, "Entrance route has no destination floor: " + to);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete, Is.True, "The real cavern is disconnected from the seeded maze");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EntranceCavernHasWideTallPhysicalClearanceAndRealInputReachesTheMaze()
        {
            yield return PrepareEntranceCavern();
            var run = Get<Component>(session, "Corridor"); var entrance = Get<Component>(run, "Entrance");
            var start = Get<Vector3>(entrance, "StartPoint"); var bounds = Get<Bounds>(entrance, "Bounds");
            Assert.That(bounds.size.x, Is.GreaterThan(20)); Assert.That(bounds.size.z, Is.GreaterThan(20));
            Assert.That(bounds.size.y, Is.GreaterThan(8));
            Assert.That(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(start.x, start.z)), Is.LessThan(.05f), "A fresh run still starts in the old six-metre maze cell");
            var towardMaze = Get<Vector3>(entrance, "MazeApproach") - start; towardMaze.y = 0;
            Assert.That(Vector3.Distance(start,Get<Vector3>(entrance,"GatePosition")),Is.LessThan(6),"A fresh run does not begin beside the sealed gate");
            Assert.That(Vector3.Dot(player.transform.forward, towardMaze.normalized), Is.GreaterThan(.99f), "The player does not initially face onward into the corridor");
            Assert.That(bounds.Contains(player.transform.position + Vector3.up), Is.True);
            // Physical rays, beyond metadata, prove the large usable floor and
            // high roof exist around the actual starting viewpoint.
            foreach (var offset in new[] { Vector3.zero, Vector3.left * 6, Vector3.right * 6, Vector3.forward * 6, Vector3.back * 6 })
            {
                Assert.That(Physics.Raycast(entrance.transform.position + offset + Vector3.up * .4f, Vector3.down, out var floor, 1,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "Large cave floor is missing at " + offset);
                Assert.That(floor.point.y, Is.EqualTo(start.y).Within(.15f));
            }
            Assert.That(Physics.Raycast(start + Vector3.up * 2, Vector3.up, out var roof, 16,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "The cave has no physical roof above its spawn");
            Assert.That(roof.point.y - start.y, Is.GreaterThan(8), "A corridor-height ceiling remains over the cave spawn");
            var progress = Call(player, "CaptureProgress");
            Assert.That((bool)Call(player, "CanRestoreProgress", progress), Is.True, "The actual spawn has no standing clearance or valid support");
            var chamberApproach = Get<Vector3>(entrance, "ChamberApproach"); var mazeApproach = Get<Vector3>(entrance, "MazeApproach");
            var maze = (Vector3)Call(run, "CellPosition", 0);
            AssertEntrancePath(start, chamberApproach); AssertEntrancePath(chamberApproach, mazeApproach); AssertEntrancePath(mazeApproach, maze);
            // Exercise the controller against the real floor/portal colliders.
            // A complete NavMesh path alone could hide an impassable doorway.
            yield return ShelterWalkTo(maze);
            Assert.That(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(maze.x, maze.z)), Is.LessThan(.3f));
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            AssertNoEntranceGuide(run);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator SealedTempleGateRespondsToRealEAndPhysicallyStopsThePlayer()
        {
            yield return PrepareEntranceCavern();
            var run = Get<Component>(session, "Corridor"); var entrance = Get<Component>(run, "Entrance");
            var gate = Get<Component>(entrance, "Gate"); var gatePosition = Get<Vector3>(entrance, "GatePosition");
            Assert.That(Get<string>(gate, "stableId"), Is.EqualTo("corridor-entrance"));
            Assert.That(Get<object>(gate, "kind").ToString(), Is.EqualTo("Inspect"));
            Assert.That(Get<string>(session, "Notice"), Does.Contain("기억 4개"), "Removing the physical plaque also removed the separate starting objective notice");
            var body = gate.GetComponent<Collider>(); Assert.That(body, Is.Not.Null);
            var front = body.bounds; // Measured leaf face, including the imported temple's local depth.
            PlacePlayer(new Vector3(front.center.x, gatePosition.y + .03f, front.max.z + 1.3f));
            player.transform.rotation = Quaternion.LookRotation(Vector3.back); Physics.SyncTransforms(); Keys();
            yield return Wait(() => Get<Component>(player, "Focus") == gate, 3,
                "The visible temple gate cannot be focused through its real interaction collider");
            int revision = Get<int>(session, "NoticeRevision");
            yield return KeysObserved(Key.E); Keys();
            yield return Wait(() => Get<int>(session, "NoticeRevision") > revision, 2, "Real E did not inspect the temple gate");
            Assert.That(Get<string>(session, "Notice"), Is.EqualTo("문은 굳게 닫혀있다"));
            Assert.That(Get<bool>(gate, "IsOpen"), Is.False); Assert.That(Get<bool>(session, "Finished"), Is.False);
            yield return KeysObserved(Key.W); yield return Delay(.8f); Keys(); yield return null;
            Assert.That(player.transform.position.z, Is.GreaterThan(front.max.z + .1f), "The sealed gate allows the player to walk through its actual closed leaf face");
            Assert.That(Get<bool>(gate, "IsOpen"), Is.False); Assert.That(Get<bool>(session, "Escaped"), Is.False);
            AssertNoEntranceGuide(run);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EntranceAndExistingMazePlayerPositionsBothRestoreInFreshScenes()
        {
            yield return PrepareEntranceCavern();
            var run = Get<Component>(session, "Corridor"); var entrance = Get<Component>(run, "Entrance");
            var cavePosition = Get<Vector3>(entrance, "StartPoint") + new Vector3(4, 0, 2);
            var mazePosition = (Vector3)Call(run, "CellPosition", 0);
            foreach (var position in new[] { cavePosition, mazePosition })
            {
                PlacePlayer(position); player.transform.rotation = Quaternion.Euler(0, 137, 0); yield return null;
                Call(shell, "Pause"); yield return null;
                var saved = CheckpointCopy(Call(session, "CaptureCheckpoint")); Call(saved, "Validate");
                var pose = Get<object>(saved, "player"); var actualPosition = Get<Vector3>(pose, "position");
                var doorIds = Get<Array>(saved, "doors").Cast<object>().Select(door => Get<string>(door, "id")).ToArray();
                Assert.That(Get<int>(saved, "version"), Is.EqualTo(1)); Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(6));
                yield return RestoreCheckpointInFreshScene(saved);
                Assert.That(Vector3.Distance(player.transform.position, actualPosition), Is.LessThan(.001f), "Restoring moved the player back to the new-run cave spawn");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 137)), Is.LessThan(.01f));
                run = Get<Component>(session, "Corridor"); entrance = Get<Component>(run, "Entrance");
                Assert.That(entrance, Is.Not.Null); AssertNoEntranceGuide(run);
                // ApplyCheckpoint intentionally leaves the prepared run at the
                // title screen. Enter and pause through the real shell before
                // requesting another suspend snapshot.
                IsolateThreats(); Begin(); yield return null; Call(shell, "Pause"); yield return null;
                Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Pause"));
                var restored = Call(run, "CaptureCheckpoint");
                Assert.That(Get<Array>(restored, "doors").Cast<object>().Select(door => Get<string>(door, "id")), Is.EqualTo(doorIds),
                    "Adding the permanent entrance gate changed saved operable-door identities");
                AssertEntrancePath(player.transform.position, (Vector3)Call(run, "CellPosition", 0));
                Call(shell, "Resume"); yield return null;
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LegacyVersionsOneAndFourRetainTheirOriginalSpawnAndEntranceProgression()
        {
            foreach (int version in new[] { 1, 4 })
            {
                if (Get<Component>(session, "Corridor"))
                {
                    var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
                }
                CreateLegacyCorridorFixture(73, version); IsolateThreats(); Begin(); yield return null;
                var run = Get<Component>(session, "Corridor"); var start = (Vector3)Call(run, "CellPosition", 0);
                Assert.That(Get<Component>(run, "Entrance"), Is.Null, "An existing saved maze version acquired the new cave geometry");
                Assert.That(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(start.x, start.z)), Is.LessThan(.05f));
                var gate = Components("Interactable").Single(item => Get<string>(item, "stableId") == "corridor-entrance");
                Assert.That(Vector3.Distance(gate.transform.position, start + new Vector3(-2.72f, 1.1f, 0)), Is.LessThan(.001f));
                Assert.That(Get<object>(gate, "kind").ToString(), Is.EqualTo(version == 1 ? "Exit" : "Inspect"));
                Call(gate, "Use", player); Assert.That(Get<bool>(session, "Finished"), Is.False);
                foreach (var memory in CheckpointItems("CorridorMemory")) Call(memory, "Use", player);
                Call(gate, "Use", player);
                Assert.That(Get<bool>(session, "Escaped"), Is.EqualTo(version == 1), "The old entrance progression changed with the new starting area");
                AssertNoEntranceGuide(run);
            }
        }
    }
}
