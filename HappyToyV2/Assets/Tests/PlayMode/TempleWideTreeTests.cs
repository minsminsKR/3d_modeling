using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleWideTreeReviewRetainsTheOriginalTrunkCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 2");
            var collider = tree.GetComponent<CapsuleCollider>();
            var prior = tree.Find("Ancient forest tree");
            Call(RequireType("NativeTempleReview"), "InstallWideTreeReview", world);
            Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(collider));
            Assert.That(collider.enabled, Is.True);
            Assert.That(prior.GetComponent<LODGroup>().enabled, Is.False);
            var replacement = tree.Find("Wide tree review");
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.GetComponentInChildren<Collider>(true), Is.Null);
            Assert.That(replacement.localPosition, Is.EqualTo(prior.localPosition));
            Assert.That(replacement.localRotation, Is.EqualTo(prior.localRotation));
            Assert.That(replacement.localScale, Is.EqualTo(prior.localScale));
            var levels = replacement.GetComponent<LODGroup>().GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3,
                Is.InRange(985000, 995000));
            Assert.That(levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3,
                Is.InRange(980000, 995000));
            Assert.Throws<System.InvalidOperationException>(() => Call(RequireType("NativeTempleReview"), "InstallWideTreeReview", world));
        }
    }
}
