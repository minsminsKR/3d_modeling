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
        public IEnumerator TemplePineFarCandidateRestoresTheOriginalFarLodAndPreservesCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 1");
            var old = tree.Find("Reviewed forest pine");
            var capsule = tree.GetComponent<CapsuleCollider>();
            var oldLods = old.GetComponent<LODGroup>().GetLODs();
            Assert.That(oldLods.Length, Is.EqualTo(3));
            var oldNear = oldLods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var oldMiddle = oldLods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var oldFar = oldLods[2].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var oldMaterial = oldLods[0].renderers[0].sharedMaterial;
            var colliderCount = world.GetComponentsInChildren<Collider>(true).Length;
            var component = world.gameObject.AddComponent(RequireType("TemplePineFarCandidate"));
            Call(component, "Prepare"); yield return null;
            var candidate = tree.Find("Candidate original far forest pine");
            Assert.That(candidate && !old.gameObject.activeSelf, Is.True);
            Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(capsule));
            Assert.That(world.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));
            Assert.That(candidate.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(candidate.localPosition, Is.EqualTo(old.localPosition));
            Assert.That(Quaternion.Angle(candidate.localRotation, old.localRotation), Is.LessThan(.05f));
            Assert.That(candidate.localScale, Is.EqualTo(old.localScale));
            var levels = candidate.GetComponent<LODGroup>().GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels[0].screenRelativeTransitionHeight, Is.EqualTo(oldLods[0].screenRelativeTransitionHeight));
            Assert.That(levels[1].screenRelativeTransitionHeight, Is.EqualTo(oldLods[2].screenRelativeTransitionHeight));
            var near = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var far = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(near, Is.SameAs(oldNear));
            Assert.That(far, Is.SameAs(oldFar));
            Assert.That(far, Is.Not.SameAs(oldMiddle));
            Assert.That(far.GetIndexCount(0) / 3, Is.EqualTo(40697));
            Assert.That(levels.All(level => level.renderers[0].sharedMaterial == oldMaterial), Is.True);
            Assert.Throws<System.InvalidOperationException>(() => Call(component, "Prepare"));
            Object.Destroy(component); yield return null; yield return null;
            Assert.That(old.gameObject.activeSelf, Is.True);
            Assert.That((bool)candidate, Is.False);

            var all = world.gameObject.AddComponent(RequireType("TemplePineFarCandidate"));
            Call(all, "PrepareAll"); yield return null;
            Assert.That(Get<int>(all, "Count"), Is.EqualTo(14));
            Assert.That(world.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));
            for (int i = 0; i < 16; i++)
            {
                if (i == 2 || i == 3) continue;
                var parent = world.transform.Find("Pine " + i);
                var previous = parent.Find("Reviewed forest pine");
                var review = parent.Find("Candidate original far forest pine");
                Assert.That(previous && review && !previous.gameObject.activeSelf, Is.True);
                Assert.That(review.localPosition, Is.EqualTo(previous.localPosition));
                Assert.That(Quaternion.Angle(review.localRotation, previous.localRotation), Is.LessThan(.05f));
                Assert.That(review.localScale, Is.EqualTo(previous.localScale));
                Assert.That(review.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(review.GetComponent<LODGroup>().GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(previous.GetComponent<LODGroup>().GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh));
                Assert.That(review.GetComponent<LODGroup>().GetLODs()[1].renderers[0].GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(previous.GetComponent<LODGroup>().GetLODs()[2].renderers[0].GetComponent<MeshFilter>().sharedMesh));
            }
            Object.Destroy(all); yield return null; yield return null;
            for (int i = 0; i < 16; i++)
            {
                if (i == 2 || i == 3) continue;
                var parent = world.transform.Find("Pine " + i);
                Assert.That(parent.Find("Reviewed forest pine").gameObject.activeSelf, Is.True);
                Assert.That(parent.Find("Candidate original far forest pine"), Is.Null);
            }
        }
    }
}
