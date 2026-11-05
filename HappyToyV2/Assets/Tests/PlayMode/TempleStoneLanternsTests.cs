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
        public IEnumerator TempleStoneLanternsUseSharedLodsOutsideThePhysicalRoute()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var component = world.GetComponent(RequireType("TempleStoneLanterns"));
            Assert.That(component, Is.Not.Null);
            var root = world.transform.Find("Temple weathered stone lanterns");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(2));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var prefab = Resources.Load<GameObject>("TempleForest/stone-lantern-visual");
            Assert.That(prefab, Is.Not.Null);
            var reference = prefab.GetComponent<LODGroup>().GetLODs();
            Assert.That(reference.Length, Is.EqualTo(2));
            foreach (Transform holder in root)
            {
                var levels = holder.GetComponentInChildren<LODGroup>(true).GetLODs();
                Assert.That(levels.Length, Is.EqualTo(2));
                Assert.That(levels[0].renderers.Length, Is.EqualTo(1));
                Assert.That(levels[1].renderers.Length, Is.EqualTo(1));
                var bounds = levels[0].renderers[0].bounds;
                var localCentre = world.transform.InverseTransformPoint(bounds.center);
                Assert.That(Mathf.Abs(localCentre.x) - bounds.extents.x, Is.GreaterThan(2.8f),
                    "Lantern footprint entered the primary approach route");
                Assert.That(localCentre.z, Is.InRange(9.5f, 11.8f));
                Assert.That(world.transform.InverseTransformPoint(bounds.min).y, Is.InRange(-.03f, .03f));
                for (int level = 0; level < 2; level++)
                {
                    var actual = levels[level].renderers[0]; var source = reference[level].renderers[0];
                    Assert.That(actual.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(source.GetComponent<MeshFilter>().sharedMesh));
                    Assert.That(actual.sharedMaterial, Is.SameAs(source.sharedMaterial));
                }
            }
            Object.Destroy(component); yield return null; yield return null;
            Assert.That(world.transform.Find("Temple weathered stone lanterns"), Is.Null);
            Assert.That(prefab, Is.Not.Null);
        }
    }
}
