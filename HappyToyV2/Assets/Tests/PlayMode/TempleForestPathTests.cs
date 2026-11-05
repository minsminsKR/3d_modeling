using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator TempleForestPathUsesShallowAuthoredStoneWithoutChangingPhysics()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            Assert.That(world.GetComponent(RequireType("TempleForestPath")), Is.Not.Null);
            var backing = world.transform.Find("Stone approach");
            Assert.That(backing, Is.Not.Null);
            Assert.That(backing.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(backing.GetComponent<Renderer>().forceRenderingOff, Is.True,
                "The straight backing should not obscure the natural edge");
            var root = world.transform.Find("Authored forest stone approach");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(25));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(25));
            Assert.That(groups.All(group => group.GetLODs().Length == 2), Is.True);
            foreach (var group in groups)
            {
                var scale = group.transform.lossyScale;
                float coverage = group.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                Assert.That(group.GetLODs().SelectMany(level => level.renderers)
                    .All(renderer => coverage >= Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z)), Is.True,
                    "The LOD group must cover its retained stone geometry");
            }
            var renderers = groups.SelectMany(group => group.GetLODs())
                .SelectMany(level => level.renderers).ToArray();
            Assert.That(renderers.Length, Is.EqualTo(50));
            Assert.That(renderers.All(renderer => renderer && renderer.sharedMaterial == renderers[0].sharedMaterial), Is.True);
            Assert.That(renderers[0].sharedMaterial.GetFloat("_Metallic"), Is.EqualTo(0).Within(.0001f));
            float groundTop = world.transform.Find("Forest ground").GetComponent<Renderer>().bounds.max.y;
            Assert.That(renderers.All(renderer => renderer.bounds.max.y < groundTop + .026f && renderer.bounds.min.y > groundTop - .006f), Is.True,
                "Both LODs must remain shallow and partly embedded in the ground");
            var derived = renderers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
            Assert.That(derived.Length, Is.EqualTo(8), "Four derived variants per authored LOD must be shared");
            var sourceGroup = Resources.Load<GameObject>("CorridorEntrance/ritual-floor-visual").GetComponent<LODGroup>();
            var original = sourceGroup.GetLODs().SelectMany(level => level.renderers)
                .Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).ToArray();
            Assert.That(original.Length, Is.EqualTo(2));
            foreach (var mesh in derived)
            {
                var match = original.Single(source => source.vertexCount == mesh.vertexCount);
                Assert.That(mesh, Is.Not.SameAs(match));
                Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0).And.LessThan(match.GetIndexCount(0)));
                CollectionAssert.AreEqual(match.uv, mesh.uv, "The source UV atlas changed");
                CollectionAssert.AreEqual(match.normals, mesh.normals, "The source normals changed");
            }
            // The two staggered columns do not overlap in x or z; adjacent
            // segments leave a thin soil seam instead of fighting for depth.
            var tileBounds = groups.Select(group =>
            {
                var levels = group.GetLODs(); var bounds = levels[0].renderers[0].bounds;
                foreach (var level in levels) foreach (var renderer in level.renderers) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }).ToArray();
            var footprint = backing.GetComponent<Renderer>().bounds;
            Assert.That(tileBounds.All(bounds => bounds.min.x >= footprint.min.x - .001f && bounds.max.x <= footprint.max.x + .001f &&
                bounds.min.z >= footprint.min.z - .001f && bounds.max.z <= footprint.max.z + .001f), Is.True,
                "Visible stone must stay within the existing footstep footprint");
            for (int i = 0; i < tileBounds.Length; i++) for (int j = i + 1; j < tileBounds.Length; j++)
            {
                var a = tileBounds[i]; var b = tileBounds[j];
                float gapX = Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x);
                float gapZ = Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z);
                Assert.That(Mathf.Max(gapX, gapZ), Is.GreaterThan(.015f),
                    "Stone segments overlap or risk z-fighting");
            }
            var left = tileBounds.Where(bounds => bounds.center.x < 500).OrderBy(bounds => bounds.center.z).ToArray();
            var right = tileBounds.Where(bounds => bounds.center.x > 500).OrderBy(bounds => bounds.center.z).ToArray();
            Assert.That(left.Length, Is.EqualTo(12)); Assert.That(right.Length, Is.EqualTo(13));
            Assert.That(Mathf.Abs(left[0].center.z - right[0].center.z), Is.GreaterThan(.5f),
                "The two columns must not share a straight transverse seam");
            Assert.That(left.Max(bounds => bounds.max.x) - left.Min(bounds => bounds.max.x), Is.GreaterThan(.35f),
                "The center seam must wander rather than form one long straight crack");

            // Check the imported geometry itself at the center and near both
            // ends of each tile. Bounds alone missed broad strips buried under
            // the original flat approach. These temporary colliders are not
            // used by the game and are removed before the next frame.
            var probes = new List<MeshCollider>();
            try
            {
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    Assert.That(filter && filter.sharedMesh, Is.True);
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh; probes.Add(collider);
                }
                Physics.SyncTransforms();
                for (int i = 0; i < renderers.Length; i++)
                {
                    var bounds = renderers[i].bounds; int hits = 0;
                    foreach (float u in new[] { .24f, .5f, .76f })
                    foreach (float v in new[] { .24f, .5f, .76f })
                    {
                        var ray = new Ray(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, u), 1,
                            Mathf.Lerp(bounds.min.z, bounds.max.z, v)), Vector3.down);
                        if (!probes[i].Raycast(ray, out var hit, 2)) continue;
                        Assert.That(hit.point.y, Is.GreaterThan(groundTop), "Imported stone is buried beneath the ground");
                        hits++;
                    }
                    Assert.That(hits, Is.GreaterThanOrEqualTo(5), "Missing stone in a tile or LOD");
                }
            }
            finally { foreach (var probe in probes) if (probe) Object.Destroy(probe); }
            var origin = new Vector3(500, 0, 500);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(0, 0, -5)), Is.True);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(3, 0, -5)), Is.False);
            yield return null;
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            var wetMaterial = renderers[0].sharedMaterial;
            Object.Destroy(world.GetComponent(RequireType("TempleForestPath")));
            yield return null; yield return null;
            Assert.That(backing.GetComponent<Renderer>().forceRenderingOff, Is.False);
            Assert.That(derived.All(mesh => !mesh), Is.True, "Derived stone meshes were not released");
            Assert.That((bool)wetMaterial, Is.False, "Owned stone material was not released");
            Assert.That(original.All(mesh => mesh), Is.True, "Shared authored meshes must remain alive");
        }
    }
}
