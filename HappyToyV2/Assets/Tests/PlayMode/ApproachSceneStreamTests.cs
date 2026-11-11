using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class ApproachSceneStreamTests
    {
        [UnityTest]
        public IEnumerator VillageLoadsAdditivelyAndRetiresWithoutDuplicatingPlayerOrCameras()
        {
            var owner = new GameObject("Stream test owner");
            var world = new GameObject("Stream test village");
            var player = new GameObject("Stream test player");
            var stream = owner.AddComponent(RequireType("ApproachSceneStream"));
            var original = player.scene;
            bool finished = false;
            string error = null;
            Call(stream, "LoadVillage", world.transform, new Action<string>(result => { error = result; finished = true; }));
            float deadline = Time.realtimeSinceStartup + 30;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(finished, Is.True, "Village async load timed out");
            Assert.That(error, Is.Null);
            var village = SceneManager.GetSceneByPath("Assets/Approach/Village.unity");
            Assert.That(village.IsValid() && village.isLoaded, Is.True);
            Assert.That(world.scene, Is.EqualTo(village));
            Assert.That(player.scene, Is.EqualTo(original));
            Assert.That(village.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)), Is.Empty);
            Assert.That(village.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AudioListener>(true)), Is.Empty);
            Call(stream, "Retire");
            while (village.isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(!village.IsValid() || !village.isLoaded, Is.True, "Village was not unloaded");
            Assert.That(player && player.scene == original, Is.True);
            Object.Destroy(owner); Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator DestroyedOwnerRetiresVillageEvenWhenLoadCompletesLater()
        {
            var owner = new GameObject("Retired stream test owner");
            var world = new GameObject("Retired stream test village");
            var stream = owner.AddComponent(RequireType("ApproachSceneStream"));
            var pending = (AsyncOperation)Call(stream, "LoadVillage", world.transform, new Action<string>(_ => Assert.Fail("Retired owner accepted an async load")));
            Assert.That(pending, Is.Not.Null);
            Object.DestroyImmediate(owner);
            float deadline = Time.realtimeSinceStartup + 30;
            // Wait for the scene operation to complete and its unload to settle;
            // the callback deliberately still runs after owner destruction.
            while (!pending.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(pending.isDone, Is.True, "Retired load never completed");
            var scene = SceneManager.GetSceneByPath("Assets/Approach/Village.unity");
            while (scene.IsValid() && scene.isLoaded && Time.realtimeSinceStartup < deadline)
            { yield return null; scene = SceneManager.GetSceneByPath("Assets/Approach/Village.unity"); }
            Assert.That(!scene.IsValid() || !scene.isLoaded, Is.True, "Retired async load leaked a scene");
            Object.Destroy(world);
        }

        [UnityTest]
        public IEnumerator MountainConnectorKeepsFullCollisionWidthAtTheSeam()
        {
            var parent = new GameObject("Mountain connector physics test");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                Call(RequireType("TempleApproachConnector"), "Create", world.transform);
                var mountain = world.transform.Find("Walkable mountain soil").GetComponent<MeshCollider>();
                var connector = world.transform.Find("Mountain to temple connector").GetComponent<MeshCollider>();
                Physics.SyncTransforms();
                foreach (float x in new[] { 489f, 500f, 511f })
                {
                    Assert.That(mountain.Raycast(new Ray(new Vector3(x, 10, 447.99f), Vector3.down), out var uphill, 20), Is.True,
                        "Mountain has no collision at x=" + x);
                    Assert.That(connector.Raycast(new Ray(new Vector3(x, 10, 448.01f), Vector3.down), out var downhill, 20), Is.True,
                        "Connector has no collision at x=" + x);
                    Assert.That(Mathf.Abs(uphill.point.y - downhill.point.y), Is.LessThan(.02f), "The seam has a step at x=" + x);
                }
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ContinuousApproachHasOneMatchingSlopeAndNoFakeTemple()
        {
            var parent = new GameObject("Continuous temple approach test");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                Call(RequireType("TempleApproachConnector"), "Create", world.transform);
                Assert.That(world.transform.Find("Distant temple landmark"), Is.Null,
                    "The pre-teleport decorative hall must not sit in the walkable route");
                Assert.That(world.transform.Find("Distant temple ground"), Is.Null,
                    "The old flat backdrop must not cover the descending collision slope");
                Assert.That(world.GetComponentsInChildren<Transform>(true).Any(node => node.name == "Temple facade warm light"), Is.False,
                    "The removed decorative hall must not retain its lights");

                var connector = world.transform.Find("Mountain to temple connector");
                Assert.That(connector, Is.Not.Null);
                var filter = connector.GetComponent<MeshFilter>();
                var collider = connector.GetComponent<MeshCollider>();
                Assert.That(filter, Is.Not.Null); Assert.That(collider, Is.Not.Null);
                Assert.That(filter.sharedMesh, Is.SameAs(collider.sharedMesh),
                    "Visible and physical connector ground must use the same geometry");
                Physics.SyncTransforms();
                foreach (float z in new[] { 448.01f, 452f, 458f, 462f, 466f, 469.9f })
                {
                    Assert.That(collider.Raycast(new Ray(new Vector3(500, 10, z), Vector3.down), out var hit, 20), Is.True,
                        "The connector is missing collision at z=" + z);
                    float expectedY = Mathf.Lerp(1.53f, 0, (z - 448f) / 22f);
                    Assert.That(hit.point.y, Is.EqualTo(expectedY).Within(.002f),
                        "The visible and walkable descent must reach the authored temple ground");
                }
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }
    }
}
