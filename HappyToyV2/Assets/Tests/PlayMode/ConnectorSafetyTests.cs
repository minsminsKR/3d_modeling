using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class ConnectorSafetyTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ConnectorStonesAreVisualOnlyAndBothMountainEdgesHaveContinuousGuards()
        {
            var parent = new GameObject("Connector safety fixture");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                Call(RequireType("TempleApproachConnector"), "Create", world.transform);
                var connector = world.transform.Find("Mountain to temple connector");
                Assert.That(connector, Is.Not.Null);
                var filter = connector.GetComponent<MeshFilter>();
                var ground = connector.GetComponent<MeshCollider>();
                Assert.That(ground.sharedMesh, Is.SameAs(filter.sharedMesh));
                var guards = connector.GetComponentsInChildren<BoxCollider>(true)
                    .Where(item => item.name == "Mountain lateral safety boundary").ToArray();
                Assert.That(guards, Has.Length.EqualTo(2));
                foreach (var guard in guards)
                    Assert.That(guard.bounds.max.y, Is.GreaterThan(6f),
                        "The guard must extend well above the highest raised bank");
                Physics.SyncTransforms();
                foreach (float z in new[] { 414.1f, 430f, 447.9f, 450f, 460f, 469.9f })
                foreach (int side in new[] { -1, 1 })
                {
                    var guard = guards.Single(item => Mathf.Sign(item.transform.position.x - 500f) == side);
                    Assert.That(guard.Raycast(new Ray(new Vector3(500, 2, z), Vector3.right * side), out var hit, 20),
                        Is.True, "Missing lateral ground guard at z=" + z + " side=" + side);
                    Assert.That(Mathf.Abs(hit.point.x - 500f), Is.InRange(11.8f, 12.1f));
                }
                var stones = connector.Find("Mountain connector stepping stones");
                Assert.That(stones, Is.Not.Null);
                Assert.That(stones.childCount, Is.EqualTo(34));
                Assert.That(stones.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Decorative stones may not alter route physics");
                Assert.That(stones.GetComponentsInChildren<LODGroup>(true), Has.Length.EqualTo(34));
                var first = stones.GetChild(0).GetComponentInChildren<Renderer>(true);
                var last = stones.GetChild(stones.childCount - 1).GetComponentInChildren<Renderer>(true);
                Assert.That(first && last, Is.True);
                Assert.That(first.bounds.center.z, Is.InRange(448.2f, 449.2f));
                Assert.That(last.bounds.center.z, Is.InRange(468.8f, 469.8f));
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RaisedBankColliderMatchesVisibleTerrainAndForkRecooksItsCarve()
        {
            var parent = new GameObject("Mountain bank collision fixture");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                var ground = world.transform.Find("Walkable mountain soil").GetComponent<MeshCollider>();
                var bank = world.transform.Find("Mountain raised soil bank 1");
                var filter = bank.GetComponent<MeshFilter>();
                var collider = bank.GetComponent<MeshCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
                Physics.SyncTransforms();
                var sideRay = new Ray(new Vector3(507, 10, 420), Vector3.down);
                Assert.That(ground.Raycast(sideRay, out var flat, 20), Is.True);
                Assert.That(collider.Raycast(sideRay, out var raised, 20), Is.True);
                Assert.That(raised.point.y - flat.point.y, Is.GreaterThan(.2f),
                    "The raised visible bank must physically support the player");

                var forkRay = new Ray(new Vector3(506.7f, 10, 434.1f), Vector3.down);
                Assert.That(collider.Raycast(forkRay, out var before, 20), Is.True);
                var fork = (Component)Call(RequireType("MountainForkDressing"), "Create", world.transform);
                Physics.SyncTransforms();
                Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
                Assert.That(collider.Raycast(forkRay, out var carved, 20), Is.True);
                Assert.That(ground.Raycast(forkRay, out var path, 20), Is.True);
                Assert.That(before.point.y - carved.point.y, Is.GreaterThan(.15f),
                    "The side trail must recook the physical bank after carving");
                Assert.That(carved.point.y, Is.LessThan(path.point.y + .2f),
                    "The carved side trail remains visually or physically buried");
                Object.Destroy(fork.gameObject); yield return null;
                Physics.SyncTransforms();
                Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
                Assert.That(collider.Raycast(forkRay, out var restored, 20), Is.True);
                Assert.That(restored.point.y, Is.EqualTo(before.point.y).Within(.002f),
                    "Removing the fork must restore the original physical bank");
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ConnectorRaisedBanksMeetTheMountainAndBlendIntoItsDescendingFloor()
        {
            var parent = new GameObject("Connector bank seam fixture");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                Call(RequireType("TempleApproachConnector"), "Create", world.transform);
                var connector = world.transform.Find("Mountain to temple connector");
                var flat = connector.GetComponent<MeshCollider>();
                var transition = connector.Find("Connector raised bank transitions");
                Assert.That(transition, Is.Not.Null);
                Physics.SyncTransforms();
                foreach (int side in new[] { -1, 1 })
                {
                    var original = world.transform.Find("Mountain raised soil bank " + side).GetComponent<MeshCollider>();
                    var extended = transition.Find("Connector raised bank " + side);
                    Assert.That(extended, Is.Not.Null);
                    var collider = extended.GetComponent<MeshCollider>();
                    Assert.That(collider.sharedMesh, Is.SameAs(extended.GetComponent<MeshFilter>().sharedMesh),
                        "The visible and physical descending bank must match");
                    foreach (float distance in new[] { 3.5f, 7f, 11.5f })
                    {
                        float x = 500f + side * distance;
                        Assert.That(original.Raycast(new Ray(new Vector3(x, 10, 447.99f), Vector3.down), out var before, 20), Is.True);
                        Assert.That(collider.Raycast(new Ray(new Vector3(x, 10, 448.01f), Vector3.down), out var after, 20), Is.True);
                        Assert.That(Mathf.Abs(before.point.y - after.point.y), Is.LessThan(.025f),
                            "A sudden drop remains at the bank seam, x=" + x);
                        Assert.That(collider.Raycast(new Ray(new Vector3(x, 10, 459.9f), Vector3.down), out var end, 20), Is.True);
                        Assert.That(flat.Raycast(new Ray(new Vector3(x, 10, 459.9f), Vector3.down), out var floor, 20), Is.True);
                        Assert.That(Mathf.Abs(end.point.y - floor.point.y), Is.LessThan(.025f),
                            "The bank did not smoothly blend into the connector, x=" + x);
                    }
                }
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }
    }
}
