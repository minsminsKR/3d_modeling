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
        public IEnumerator TempleForestDepthCandidateKeepsTheOriginalTreesAndRoute()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var original = world.transform.Find("Pine 2");
            Assert.That(original && original.GetComponent<CapsuleCollider>(), Is.True);
            var priorPosition = original.localPosition;
            var priorRotation = original.localRotation;
            var priorCapsule = original.GetComponent<CapsuleCollider>();
            var colliders = world.GetComponentsInChildren<Collider>(true);
            var originalMaterial = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review")
                .GetComponent<LODGroup>().GetLODs()[1].renderers[0].sharedMaterial;
            var originalColour = originalMaterial.GetColor("_BaseColor");
            var component = world.gameObject.AddComponent(RequireType("TempleForestDepthCandidate"));
            Call(component, "Prepare"); yield return null;
            var root = world.transform.Find("Candidate distant forest depth");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(12));
            Assert.That(Get<int>(component, "TreeCount"), Is.EqualTo(12));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            CollectionAssert.AreEquivalent(colliders, world.GetComponentsInChildren<Collider>(true));
            Assert.That(original.localPosition, Is.EqualTo(priorPosition));
            Assert.That(original.localRotation, Is.EqualTo(priorRotation));
            Assert.That(original.GetComponent<CapsuleCollider>(), Is.SameAs(priorCapsule));
            Assert.That(originalMaterial.GetColor("_BaseColor"), Is.EqualTo(originalColour));
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(12));
            var far = groups.Select(group => group.GetLODs()[1].renderers[0]).ToArray();
            Assert.That(far.All(renderer => renderer && renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), Is.True);
            Assert.That(far.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), Is.EqualTo(1));
            Assert.That(far[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3, Is.EqualTo(40697));
            Assert.That(far.Select(renderer => renderer.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
            Assert.That(far[0].sharedMaterial, Is.Not.SameAs(originalMaterial));
            Assert.That(far[0].sharedMaterial.enableInstancing, Is.True);
            CollectionAssert.AreEquivalent(originalMaterial.shaderKeywords, far[0].sharedMaterial.shaderKeywords);
            foreach (Transform tree in root)
            {
                Vector3 p = tree.localPosition;
                Assert.That(Mathf.Abs(p.x) > 22.3f || p.z > 29.15f, Is.True, "A trunk centre must be beyond an existing outer boundary");
                Assert.That(Mathf.Abs(p.x), Is.LessThan(24f));
                Assert.That(p.z, Is.InRange(-34f, 30f));
            }
            Assert.That((bool)Call(world, "IsStoneApproach", new Vector3(500, 0, 495)), Is.True);
            Assert.Throws<System.InvalidOperationException>(() => Call(component, "Prepare"));
            Object.Destroy(component); yield return null; yield return null;
            Assert.That((bool)root, Is.False);
            Assert.That(original && original.GetComponent<CapsuleCollider>() == priorCapsule, Is.True);
        }
    }
}
