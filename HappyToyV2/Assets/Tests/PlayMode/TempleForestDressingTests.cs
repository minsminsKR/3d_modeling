using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestOfferingsUseAuthoredVisualsWithoutBlockingThePath()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var dressing = world.transform.Find("Forest side offerings");
            Assert.That(dressing, Is.Not.Null);
            Assert.That(dressing.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(dressing.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var children = dressing.Cast<Transform>().ToArray();
            Assert.That(children.Count(x => x.name == "Weathered forest guardian"), Is.EqualTo(2));
            var guardians = children.Where(x => x.name == "Weathered forest guardian").ToArray();
            var guardianSource = Resources.Load<GameObject>("CorridorEntrance/guardian-statue-visual");
            Assert.That(guardianSource, Is.Not.Null);
            var sourceMeshes = guardianSource.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            foreach (var guardian in guardians)
            {
                Assert.That(Mathf.Abs(guardian.localPosition.x), Is.GreaterThan(3f));
                Assert.That(((Bounds)RequireType("GraphicsPropLibrary").GetMethod("LocalBounds").Invoke(null, new object[] { guardian })).size.y * guardian.localScale.y, Is.EqualTo(3.5f).Within(.01f));
                Assert.That(guardian.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh), Is.EqualTo(sourceMeshes));
            }
            Assert.That(children.Count(x => x.name == "Loose forest stones"), Is.EqualTo(8));
            Assert.That(children.Count(x => x.name == "Faded forest talisman"), Is.EqualTo(10));
            Assert.That(dressing.Find("Weathered forest ritual cords").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(children.Where(x => x.name == "Loose forest stones")
                .All(x => Mathf.Abs(x.localPosition.x) > 1.8f && x.GetComponentInChildren<LODGroup>()), Is.True);
            var trees = Enumerable.Range(0, 16).Select(i => world.transform.Find("Pine " + i)).ToArray();
            Assert.That(trees.All(x => x && x.GetComponent<CapsuleCollider>()), Is.True);
            var visuals = trees.Select(tree => tree.Find("Ancient forest tree") ?? tree.Find("Reviewed forest pine")).ToArray();
            Assert.That(visuals.All(visual => visual), Is.True);
            Assert.That(visuals.Select(visual => visual.lossyScale).Distinct().Count(), Is.GreaterThan(4));
        }
    }
}
