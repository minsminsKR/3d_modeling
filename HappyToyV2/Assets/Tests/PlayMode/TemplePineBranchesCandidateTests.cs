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
        public IEnumerator TemplePineBranchesCandidatePreservesOriginalCollisionAndFarLod()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 1");
            var old = tree.Find("Reviewed forest pine");
            var capsule = tree.GetComponent<CapsuleCollider>();
            var oldLod = old.GetComponent<LODGroup>().GetLODs();
            Assert.That(oldLod.Length, Is.EqualTo(3));
            var oldFar = oldLod[2].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var oldMaterial = oldLod[0].renderers[0].sharedMaterial;
            var count = world.GetComponentsInChildren<Collider>(true).Length;
            var oldPosition = old.localPosition; var oldRotation = old.localRotation; var oldScale = old.localScale;
            var component = world.gameObject.AddComponent(RequireType("TemplePineBranchesCandidate"));
            Call(component, "Prepare"); yield return null;
            var candidate = tree.Find("Candidate original forest pine");
            Assert.That(candidate && !old.gameObject.activeSelf, Is.True);
            Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(capsule));
            Assert.That(world.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(count));
            Assert.That(candidate.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(candidate.localPosition, Is.EqualTo(oldPosition));
            Assert.That(Quaternion.Angle(candidate.localRotation, oldRotation), Is.LessThan(.05f));
            Assert.That(candidate.localScale, Is.EqualTo(oldScale));
            var levels = candidate.GetComponent<LODGroup>().GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels[0].renderers.Length, Is.EqualTo(1));
            Assert.That(levels[1].renderers.Length, Is.EqualTo(1));
            Assert.That(levels[0].screenRelativeTransitionHeight, Is.EqualTo(oldLod[0].screenRelativeTransitionHeight));
            Assert.That(levels[1].screenRelativeTransitionHeight, Is.EqualTo(oldLod[2].screenRelativeTransitionHeight));
            var near = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var far = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(oldLod[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3, Is.InRange(250000, 260000));
            Assert.That(near.GetIndexCount(0) / 3, Is.InRange(180000, 200000));
            Assert.That(far, Is.SameAs(oldFar));
            Assert.That(levels.All(level => level.renderers[0].sharedMaterial == oldMaterial), Is.True);
            Assert.Throws<System.InvalidOperationException>(() => Call(component, "Prepare"));
            Object.Destroy(component); yield return null; yield return null;
            Assert.That(old.gameObject.activeSelf, Is.True);
            Assert.That((bool)candidate, Is.False);
            Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(capsule));

            var all = world.gameObject.AddComponent(RequireType("TemplePineBranchesCandidate"));
            Call(all, "PrepareAll"); yield return null;
            Assert.That(Get<int>(all, "Count"), Is.EqualTo(14));
            Assert.That(world.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(count));
            Assert.That(world.transform.Find("Pine 2/Ancient forest tree"), Is.Not.Null);
            Assert.That(world.transform.Find("Pine 3/Ancient forest tree"), Is.Not.Null);
            for (int i = 0; i < 16; i++)
            {
                if (i == 2 || i == 3) continue;
                var parent = world.transform.Find("Pine " + i);
                var previous = parent.Find("Reviewed forest pine");
                var review = parent.Find("Candidate original forest pine");
                Assert.That(previous && review && !previous.gameObject.activeSelf, Is.True);
                Assert.That(review.localPosition, Is.EqualTo(previous.localPosition));
                Assert.That(Quaternion.Angle(review.localRotation, previous.localRotation), Is.LessThan(.05f));
                Assert.That(review.localScale, Is.EqualTo(previous.localScale));
                Assert.That(review.GetComponentsInChildren<Collider>(true), Is.Empty);
                var reviewFar = review.GetComponent<LODGroup>().GetLODs()[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(reviewFar, Is.SameAs(previous.GetComponent<LODGroup>().GetLODs()[2].renderers[0].GetComponent<MeshFilter>().sharedMesh));
            }
            Object.Destroy(all); yield return null; yield return null;
            for (int i = 0; i < 16; i++)
            {
                if (i == 2 || i == 3) continue;
                var parent = world.transform.Find("Pine " + i);
                Assert.That(parent.Find("Reviewed forest pine").gameObject.activeSelf, Is.True);
                Assert.That(parent.Find("Candidate original forest pine"), Is.Null);
            }
        }
    }
}
