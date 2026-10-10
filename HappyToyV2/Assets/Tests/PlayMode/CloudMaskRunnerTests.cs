using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator MaskReadableAttachmentBoundsExcludeEmptyRotatedLimbBoxCorners()
        {
            // A long crooked triangle has a real highest vertex below its rotated
            // renderer box corner. This counterfactual catches a face attaching
            // to empty space while keeping the new borrowed mesh immutable.
            var mask = Components("LanternMaskEncounter").First();
            var geometry = new GameObject("CloudQA crooked readable limb bounds");
            var mesh = new Mesh { name = "CloudQA immutable crooked limb" };
            try
            {
                var vertices = new[] { Vector3.zero, Vector3.right * 2, Vector3.up };
                mesh.vertices = vertices; mesh.triangles = new[] { 0, 1, 2 }; mesh.RecalculateBounds();
                geometry.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = geometry.AddComponent<MeshRenderer>();
                geometry.transform.SetPositionAndRotation(mask.transform.position, Quaternion.Euler(0, 0, 45));
                var highest = vertices.Max(vertex => geometry.transform.TransformPoint(vertex).y);
                Assert.That(renderer.bounds.max.y - highest, Is.GreaterThan(.3f), "Fixture lacks an empty conservative box corner");
                var method = mask.GetType().GetMethod("VisibleBounds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var arguments = new object[] { new Renderer[] { renderer }, new Bounds() };
                Assert.That((bool)method.Invoke(mask, arguments), Is.True);
                var actual = (Bounds)arguments[1];
                Assert.That(actual.max.y, Is.EqualTo(highest).Within(.00002f), "Mask attachment floats above real readable limb vertices");
                Assert.That(mesh.vertices, Is.EqualTo(vertices), "Attachment rewrote borrowed static mesh geometry");
                Assert.That(geometry.GetComponentsInChildren<Collider>(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(geometry); UnityEngine.Object.DestroyImmediate(mesh); }
            yield return null;
        }

        IEnumerator PrepareCorridorMaskRunner()
        {
            Call(session, "CreateCorridor", 73); Begin(); ((Behaviour)player).enabled = false;
            foreach (var actor in CheckpointThreats()) ((Behaviour)actor).enabled = false;
            var run = Get<Component>(session, "Corridor");
            for (int i = 0; i < 3; i++) Assert.That((bool)Call(run, "Collect", "memory-" + i), Is.True);
            var mask = Get<Component>(run, "Mask");
            yield return Wait(() => mask.gameObject.activeSelf && mask.GetComponent<NavMeshAgent>().isOnNavMesh, 3,
                "Actual third-memory corridor release did not bind the Mask runner");
            Assert.That(Get<bool>(mask, "CorridorRunner"), Is.True);
            Assert.That(Get<bool>(mask, "Transformed"), Is.True);
            Assert.That(Get<bool>(mask, "IntroCompleted"), Is.True);
            Assert.That(((Behaviour)mask.GetComponent(RequireType("StalkerFootsteps"))).enabled, Is.False,
                "Floating lantern movement voice duplicates the actual running contacts");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator MaskRunnerShattersLockedAndOpenRealDoorsRunsFastestAndRestoresBrokenGeometry()
        {
            yield return PrepareCorridorMaskRunner();
            var run = Get<Component>(session, "Corridor"); var mask = Get<Component>(run, "Mask");
            var agent = mask.GetComponent<NavMeshAgent>();
            var door = TraversalDoor(true); var normal = Get<Vector3>(door, "DoorNormal");
            var closed = Get<Transform>(door, "movingLeaf").localPosition;
            var destination = new GameObject("CloudQA real door runner patrol");
            destination.transform.position = SupportedDoorSide(door, 2.1f);
            Set(mask, "patrol", new[] { destination.transform, destination.transform });
            var original = Call(mask, "CaptureChapterProgress");
            float peakSpeed = 0;
            foreach (bool locked in new[] { true, false })
            {
                ((Behaviour)mask).enabled = false;
                Call(door, "RestoreDoor", false, closed); Set(door, "DoorLocked", locked);
                if (locked) Assert.That((bool)Call(door, "OpenForPursuer"), Is.False, "Ordinary pursuer opened a locked leaf");
                else
                {
                    Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
                    yield return Wait(() => Get<bool>(door, "AtRequestedDoorPose"), 3, "Actual open leaf never slid aside");
                }
                // Fixture positions only. After this native restore the actor
                // travels and impacts the actual scene door at authored speeds.
                PlacePlayer(new Vector3(200, 5, 200), false);
                var state = CopyAdvancedProgress(original);
                Set(state, "position", SupportedDoorSide(door, -2.1f)); Set(state, "rotation", Quaternion.LookRotation(normal));
                Set(state, "target", destination.transform.position); Set(state, "state", "Wander");
                Set(state, "memory", 0f); Set(state, "doorPassage", null);
                ((Behaviour)mask).enabled = true; Call(mask, "RestoreCorridorProgress", state);
                var previous = mask.transform.position; float limit = Time.realtimeSinceStartup + 8;
                while (Time.realtimeSinceStartup < limit && (!Get<bool>(door, "DoorBroken") ||
                    Vector3.Dot(mask.transform.position - door.transform.position, normal) < .9f))
                {
                    yield return null; Physics.SyncTransforms(); peakSpeed = Mathf.Max(peakSpeed, agent.velocity.magnitude);
                    Assert.That(Vector3.Distance(previous, mask.transform.position), Is.LessThan(7.2f * Mathf.Max(Time.deltaTime, .016f) + .15f),
                        "Mask jumped through the real door instead of running");
                    previous = mask.transform.position; AdvancedDoorBodyClear(door, mask);
                }
                Assert.That(Get<bool>(door, "DoorBroken"), Is.True, "Runner did not shatter " + (locked ? "locked" : "open") + " route door");
                Assert.That(Get<bool>(door, "IsOpen"), Is.True);
                Assert.That(Get<bool>(door, "DoorOperable"), Is.False);
                Assert.That(Get<bool>(door, "InteractionAvailable"), Is.False, "Broken door can still be closed by the player");
                Assert.That(Get<UnityEngine.AI.NavMeshObstacle>(door, "obstacle").enabled, Is.False);
                Assert.That(Get<Transform>(door, "movingLeaf").GetComponentsInChildren<Collider>().Any(x => x.enabled), Is.False);
                Assert.That(Vector3.Dot(mask.transform.position - door.transform.position, normal), Is.GreaterThan(.9f));
                Assert.That(agent.speed, Is.EqualTo(6.6f).Within(.0001f), "Patrolling Mask is still using a slow floating speed");
                Assert.That(Get<int>(door, "MaskImpacts"), Is.GreaterThanOrEqualTo(locked ? 1 : 2));
            }
            var ordinary = CheckpointThreats();
            Assert.That(ordinary.All(x => Get<float>(x, "patrolSpeed") < peakSpeed), Is.True,
                "Actual running speed never exceeded every corridor monster's authored patrol speed: " + peakSpeed);
            // Pause cannot admit a new physical impact. Broken geometry survives
            // the real JSON codec and a fresh scene, including collider/obstacle state.
            PlacePlayer((Vector3)Call(run, "CellPosition", 0) + Vector3.up * .03f, false);
            yield return Delay(.9f); Call(shell, "Pause");
            Assert.That((bool)Call(door, "BreakForMask", mask), Is.False);
            var data = Call(run, "CaptureCheckpoint"); var id = Get<string>(door, "stableId");
            var savedDoor = Get<Array>(data, "doors").Cast<object>().Single(x => Get<string>(x, "id") == id);
            Assert.That(Get<bool>(savedDoor, "broken"), Is.True);
            yield return RestoreCheckpointInFreshScene(data);
            door = CheckpointItems("Door").Single(x => Get<string>(x, "stableId") == id);
            Assert.That(Get<bool>(door, "DoorBroken"), Is.True);
            Assert.That(Get<Transform>(door, "movingLeaf").GetComponentsInChildren<Collider>().Any(x => x.enabled), Is.False);
            Assert.That(Get<UnityEngine.AI.NavMeshObstacle>(door, "obstacle").enabled, Is.False);
            Assert.That(Get<bool>(door, "InteractionAvailable"), Is.False);
            Debug.Log("HAPPYTOY_MASK_RUNNER_DOORS_PASS actual speed=" + peakSpeed + " locked/open impact, native crossing, JSON/fresh-scene broken geometry");
            UnityEngine.Object.Destroy(destination);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MaskRunnerNearWhistleSightStillRespectsRealRoomCoverAndHiding()
        {
            yield return PrepareCorridorMaskRunner();
            var mask = Get<Component>(Get<Component>(session, "Corridor"), "Mask");
            ((Behaviour)mask).enabled = false;
            CorridorMaskCameraAndBodySight(mask, 3);
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.True, "Near clear sight was not acquired");
            var midpoint = Vector3.Lerp(mask.transform.position, player.transform.position, .5f) + Vector3.up * 1.2f;
            var cover = Cube("CloudQA solid room-cover counterfactual", midpoint, new Vector3(3, 2.5f, .4f));
            cover.transform.rotation = Quaternion.LookRotation(player.transform.position - mask.transform.position);
            Physics.SyncTransforms();
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.False, "Close whistle range revealed a player through real opaque cover");
            cover.SetActive(false); Physics.SyncTransforms();
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.True);
            var cabinet = CheckpointCabinetFixture(true);
            PlacePlayer(Get<Transform>(cabinet, "outside").position, false);
            var outward = player.transform.position - Get<Transform>(cabinet, "inside").position; outward.y = 0; outward.Normalize();
            Assert.That(NavMesh.SamplePosition(player.transform.position + outward * 3, out var nearCabinet, .5f, NavMesh.AllAreas), Is.True);
            var state = CopyAdvancedProgress(Call(mask, "CaptureChapterProgress"));
            Set(state, "position", nearCabinet.position); Set(state, "state", "Wander"); Set(state, "memory", 0f);
            Call(mask, "RestoreCorridorProgress", state);
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.True, "Actual cabinet approach does not provide clear near-range player sight");
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True, "Actual corridor cabinet entry failed");
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.False, "Whistle-range Mask ignores actual player hiding");
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.False, "Actual cabinet exit failed");
            UnityEngine.Object.Destroy(cover); yield return null;
        }
    }
}
