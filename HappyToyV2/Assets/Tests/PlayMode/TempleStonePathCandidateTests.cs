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
        public IEnumerator TempleGeneratedStoneCandidatePreservesPathAndCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            IsolateLegacyStoneReview(world.transform);
            var old = world.transform.Find("Authored forest stone approach");
            var bed = world.transform.Find("Recessed forest stone bed");
            var approach = world.transform.Find("Stone approach").GetComponent<Renderer>();
            var colliderCount = world.GetComponentsInChildren<Collider>(true).Length;
            var component = world.gameObject.AddComponent(RequireType("TempleStonePathCandidate"));
            Call(component, "Prepare"); yield return null;
            var root = world.transform.Find("Candidate generated stone approach");
            Assert.That(root, Is.Not.Null);
            Assert.That(Get<int>(component, "TileCount"), Is.EqualTo(33));
            Assert.That(root.childCount, Is.EqualTo(33));
            Assert.That(old.gameObject.activeSelf, Is.False);
            Assert.That(bed.gameObject.activeSelf, Is.True);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(world.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));
            Assert.That(approach.enabled, Is.True);
            Assert.That(approach.forceRenderingOff, Is.True);
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(33));
            Assert.That(groups.All(group => group.lodCount == 2), Is.True);
            var renderers = groups.SelectMany(group => group.GetLODs()).SelectMany(lod => lod.renderers).ToArray();
            Assert.That(renderers.Length, Is.EqualTo(66));
            Assert.That(renderers.All(renderer => renderer.sharedMaterial && renderer.sharedMaterial.enableInstancing), Is.True);
            Assert.That(renderers.Select(renderer => renderer.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
            var meshes = renderers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
            Assert.That(meshes.Length, Is.EqualTo(2));
            Assert.That(meshes.All(mesh => mesh.GetIndexCount(0) / 3 >= 19000 && mesh.GetIndexCount(0) / 3 <= 24500), Is.True);
            var groundTop = world.transform.Find("Forest ground").GetComponent<Renderer>().bounds.max.y;
            var bounds = groups.Select(group => {
                var lods = group.GetLODs(); var value = lods[0].renderers[0].bounds;
                foreach (var lod in lods) foreach (var renderer in lod.renderers) value.Encapsulate(renderer.bounds);
                return value;
            }).ToArray();
            var footprint = approach.bounds;
            Assert.That(bounds.All(value => value.min.x >= footprint.min.x - .001f && value.max.x <= footprint.max.x + .001f &&
                value.min.z >= footprint.min.z - .001f && value.max.z <= footprint.max.z + .001f), Is.True);
            Assert.That(bounds.All(value => value.min.y > groundTop + .003f && value.max.y < groundTop + .047f), Is.True,
                "Candidate must not intersect the recessed stone substrate");
            for (int i = 0; i < bounds.Length; i++) for (int j = i + 1; j < bounds.Length; j++)
            {
                float gapX = Mathf.Max(bounds[i].min.x - bounds[j].max.x, bounds[j].min.x - bounds[i].max.x);
                float gapZ = Mathf.Max(bounds[i].min.z - bounds[j].max.z, bounds[j].min.z - bounds[i].max.z);
                Assert.That(Mathf.Max(gapX, gapZ), Is.GreaterThan(.003f), "Candidate tiles overlap and may fight for depth");
            }
            Assert.That((bool)Call(world, "IsStoneApproach", new Vector3(500, 0, 495)), Is.True);
            Assert.That((bool)Call(world, "IsStoneApproach", new Vector3(503, 0, 495)), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => Call(component, "Prepare"));
            Object.Destroy(component); yield return null; yield return null;
            Assert.That(old.gameObject.activeSelf, Is.True, "Candidate did not restore the ordinary relief");
            Assert.That((bool)root, Is.False, "Candidate world objects were not released");
        }
    }
}
