using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class TempleStoryEvidenceTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator AuthoredSheetChangesInkAndReleasesItsOwnedResources()
        {
            var root = new GameObject("Isolated temple story evidence");
            try
            {
                var evidence = root.AddComponent(RequireType("TempleStoryEvidence"));
                Call(evidence, "Prepare");
                var paper = Get<Transform>(evidence, "Paper");
                var ink = Get<Renderer>(evidence, "InkRenderer");
                Assert.That(paper && ink, Is.True, "A visible sheet and ink surface are required");
                Assert.That(Get<bool>(evidence, "SecondDay"), Is.False);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Presentation must not add any physics or interaction target");
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);

                var prefab = Resources.Load<GameObject>("CorridorDetails/exercise-leaf-lod0");
                Assert.That(prefab, Is.Not.Null);
                var originalMeshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
                var paperMeshes = paper.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray();
                Assert.That(paperMeshes.Length, Is.GreaterThan(0));
                Assert.That(paperMeshes.All(mesh => mesh && originalMeshes.Contains(mesh)), Is.True,
                    "The paper must keep the authored sheet's actual mesh");
                var paperBounds = paper.GetComponentInChildren<Renderer>().bounds;
                var incenseInteraction = new Bounds(new Vector3(0, 1.02f, 7.2f), new Vector3(.8f, .75f, .55f));
                Assert.That(paperBounds.Intersects(incenseInteraction), Is.False,
                    "The sheet must not overlap the existing incense interaction area");
                Assert.That(paperBounds.min.x, Is.GreaterThanOrEqualTo(-.9f));
                Assert.That(paperBounds.max.x, Is.LessThanOrEqualTo(.9f));
                Assert.That(paperBounds.min.z, Is.GreaterThanOrEqualTo(6.975f));
                Assert.That(paperBounds.max.z, Is.LessThanOrEqualTo(7.725f));
                Assert.That(paperBounds.min.y, Is.GreaterThanOrEqualTo(.845f - .001f));

                var inkMaterial = ink.sharedMaterial;
                var projectedMesh = ink.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(inkMaterial && projectedMesh, Is.True);
                Assert.That(projectedMesh.vertexCount, Is.GreaterThan(0));
                var firstInk = inkMaterial.GetTexture("_BaseMap");
                Assert.That(firstInk, Is.Not.Null);
                Call(evidence, "SetSecondDay", true);
                Assert.That(Get<bool>(evidence, "SecondDay"), Is.True);
                var secondInk = inkMaterial.GetTexture("_BaseMap");
                Assert.That(secondInk, Is.Not.Null.And.Not.SameAs(firstInk));
                Call(evidence, "SetSecondDay", false);
                Assert.That(inkMaterial.GetTexture("_BaseMap"), Is.SameAs(firstInk),
                    "Restoring the first-day state must remove the added drawing");

                Object.Destroy(evidence); yield return null; yield return null;
                Assert.That(paper == null, Is.True);
                Assert.That(ink == null, Is.True);
                Assert.That(inkMaterial == null, Is.True);
                Assert.That(projectedMesh == null, Is.True);
                Assert.That(firstInk == null && secondInk == null, Is.True);
                Assert.That(originalMeshes.All(mesh => mesh), Is.True,
                    "Destroying the evidence must not destroy shared source meshes");
            }
            finally { Object.Destroy(root); }
        }
    }
}
