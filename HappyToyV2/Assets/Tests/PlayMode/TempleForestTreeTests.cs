using System.Collections;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using UnityEngine.TestTools;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleProductionAncientTreesKeepTheOriginalTrunkCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 2");
            var collider = tree.GetComponent<CapsuleCollider>();
            var original = tree.GetComponent<LODGroup>();
            var forest = world.GetComponent(RequireType("TempleForestTrees"));
            Assert.That(Get<int>(forest, "AncientCount"), Is.EqualTo(2));
            Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(collider));
            Assert.That(collider.enabled, Is.True);
            Assert.That(original.enabled, Is.False);
            var replacement = tree.Find("Ancient forest tree");
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.GetComponentInChildren<Collider>(true), Is.Null);
            var levels = replacement.GetComponent<LODGroup>().GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels[0].renderers.Length, Is.EqualTo(1));
            Assert.That(levels[1].renderers.Length, Is.EqualTo(1));
            var near = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var far = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(near.GetIndexCount(0) / 3, Is.InRange(470000, 490000));
            Assert.That(far.GetIndexCount(0) / 3, Is.InRange(135000, 145000));
            Assert.That(levels[0].renderers[0].sharedMaterial, Is.SameAs(levels[1].renderers[0].sharedMaterial));
            var opposite = world.transform.Find("Pine 3");
            Assert.That(opposite.GetComponent<CapsuleCollider>().enabled, Is.True);
            Assert.That(opposite.Find("Ancient forest tree"), Is.Not.Null);
            Assert.Throws<System.InvalidOperationException>(() => Call(forest, "Prepare"));
        }
    }
}
