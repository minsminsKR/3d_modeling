using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class MountainForkSafetyTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SideTrailKeepsGroundAndMainRouteWhileRestoringVisualBank()
        {
            var parent = new GameObject("Mountain fork safety fixture");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                var ground = world.transform.Find("Walkable mountain soil").GetComponent<MeshCollider>();
                var originalGround = ground.sharedMesh.vertices;
                var bank = world.transform.Find("Mountain raised soil bank 1").GetComponent<MeshFilter>().sharedMesh;
                var originalBank = bank.vertices;
                var originalRenderers = world.GetComponentsInChildren<Renderer>(true);
                var rendererStates = new bool[originalRenderers.Length];
                for (int i = 0; i < originalRenderers.Length; i++) rendererStates[i] = originalRenderers[i].enabled;
                var fork = (Component)Call(RequireType("MountainForkDressing"), "Create", world.transform);
                Physics.SyncTransforms();

                var changedBank = bank.vertices;
                float maxChange = 0;
                for (int i = 0; i < originalBank.Length; i++)
                    maxChange = Mathf.Max(maxChange, Mathf.Abs(originalBank[i].y - changedBank[i].y));
                Assert.That(maxChange, Is.GreaterThan(.03f), "The side trail did not clear the visual bank");
                CollectionAssert.AreEqual(originalGround, ground.sharedMesh.vertices,
                    "The existing walkable ground collider was modified");

                var samples = new[] {
                    new Vector3(501f, 8, 429.1f), new Vector3(503.3f, 8, 431.1f),
                    new Vector3(504.5f, 8, 432.2f), new Vector3(505.6f, 8, 433.2f),
                    new Vector3(506.7f, 8, 434.1f) };
                foreach (var point in samples)
                {
                    Assert.That(ground.Raycast(new Ray(point, Vector3.down), out var hit, 20), Is.True,
                        "Side trail lost support at " + point);
                    Assert.That(hit.normal.y, Is.GreaterThan(.99f), "Side trail ground is unexpectedly steep");
                }
                var fences = fork.GetComponentsInChildren<BoxCollider>();
                Assert.That(fences, Has.Length.EqualTo(3));
                foreach (var fence in fences)
                    Assert.That(fence.bounds.min.x, Is.GreaterThan(505f), "Side fence intrudes into the main route");

                Object.Destroy(fork.gameObject); yield return null;
                var restoredBank = bank.vertices;
                for (int i = 0; i < originalBank.Length; i++)
                    Assert.That(restoredBank[i], Is.EqualTo(originalBank[i]), "Removing the fork did not restore the bank");
                for (int i = 0; i < originalRenderers.Length; i++)
                    if (originalRenderers[i]) Assert.That(originalRenderers[i].enabled, Is.EqualTo(rendererStates[i]),
                        "Removing the fork did not restore a fern's prior visibility");
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DestroyingMountainDoesNotReactivateObjectsDuringTeardown()
        {
            var parent = new GameObject("Mountain fork unload fixture");
            try
            {
                var world = (Component)Call(RequireType("MountainApproachWorld"), "Create", parent.transform);
                var before = world.GetComponentsInChildren<Renderer>(true);
                var enabled = new bool[before.Length];
                for (int i = 0; i < before.Length; i++) enabled[i] = before[i].enabled;
                Call(RequireType("MountainForkDressing"), "Create", world.transform);
                int hidden = 0;
                for (int i = 0; i < before.Length; i++) if (enabled[i] && !before[i].enabled) hidden++;
                Assert.That(hidden, Is.GreaterThan(0), "The teardown fixture must include a hidden fern renderer");

                Object.Destroy(parent); yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (parent) Object.Destroy(parent); }
        }
    }
}
