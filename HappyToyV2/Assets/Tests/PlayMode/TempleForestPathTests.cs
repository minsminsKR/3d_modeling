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
            var surfaceMaterial = renderers[0].sharedMaterial;
            Assert.That(surfaceMaterial.GetFloat("_Metallic"), Is.EqualTo(0).Within(.0001f));
            Assert.That(surfaceMaterial.GetFloat("_Smoothness"), Is.InRange(.39f, .45f));
            Assert.That(surfaceMaterial.GetFloat("_EnvironmentReflections"), Is.Zero);
            Assert.That(surfaceMaterial.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.True,
                "Sky reflections must not turn the stone path into a blue mirror");
            Assert.That(surfaceMaterial.GetFloat("_SpecularHighlights"), Is.EqualTo(1).Within(.0001f),
                "Moon and lanterns should retain their direct highlights");
            float groundTop = world.transform.Find("Forest ground").GetComponent<Renderer>().bounds.max.y;
            Assert.That(renderers.All(renderer => renderer.bounds.max.y < groundTop + .041f && renderer.bounds.min.y > groundTop + .009f), Is.True,
                "Both LODs must remain shallow and above the recessed stone bed");
            var bed = world.transform.Find("Recessed forest stone bed");
            Assert.That(bed, Is.Not.Null);
            Assert.That(bed.childCount, Is.EqualTo(12));
            Assert.That(bed.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(bed.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var bedGroups = bed.GetComponentsInChildren<LODGroup>(true);
            Assert.That(bedGroups.Length, Is.EqualTo(12));
            Assert.That(bedGroups.All(group => group.lodCount == 2), Is.True);
            var bedRenderers = bedGroups.SelectMany(group => group.GetLODs()).SelectMany(level => level.renderers).ToArray();
            Assert.That(bedRenderers.Length, Is.EqualTo(24));
            Assert.That(bedRenderers.All(renderer => renderer.bounds.min.y >= groundTop + .0009f &&
                renderer.bounds.max.y <= groundTop + .0031f), Is.True, "Bed must stay above soil and below stone relief");
            Assert.That(bedRenderers.All(renderer => renderer.sharedMaterial == bedRenderers[0].sharedMaterial), Is.True);
            Assert.That(bedRenderers[0].sharedMaterial, Is.Not.SameAs(renderers[0].sharedMaterial));
            Assert.That(bedRenderers[0].sharedMaterial.GetFloat("_Smoothness"), Is.InRange(.30f, .34f));
            Assert.That(bedRenderers[0].sharedMaterial.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.True);
            var tintDifference = surfaceMaterial.GetColor("_BaseColor").r - bedRenderers[0].sharedMaterial.GetColor("_BaseColor").r;
            Assert.That(tintDifference, Is.InRange(.05f, .12f), "Relief and bed must not read as disconnected slabs");
            Assert.That(bedRenderers[0].sharedMaterial.GetFloat("_Cull"), Is.EqualTo(2),
                "Backfaces on the squashed stone bed must remain culled");
            var bedMeshes = bedRenderers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
            Assert.That(bedMeshes.Length, Is.EqualTo(1), "All bed segments and LODs must share one trimmed surface");
            var bedMesh = bedMeshes[0];
            var nearSource = Resources.Load<GameObject>("CorridorEntrance/ritual-floor-visual")
                .GetComponent<LODGroup>().GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(bedMesh, Is.Not.SameAs(nearSource));
            Assert.That(bedMesh.GetIndexCount(0), Is.GreaterThan(0).And.LessThan(nearSource.GetIndexCount(0) / 2));
            CollectionAssert.AreEqual(nearSource.uv, bedMesh.uv);
            CollectionAssert.AreEqual(nearSource.normals, bedMesh.normals);
            // Every retained triangle faces up in the actual prefab transform;
            // underside and near-vertical layers must not fight its depth.
            var baseVertices = bedMesh.vertices;
            var baseTriangles = bedMesh.triangles;
            var baseMatrix = bedRenderers[0].transform.localToWorldMatrix;
            for (int i = 0; i < baseTriangles.Length; i += 3)
            {
                Vector3 a = baseVertices[baseTriangles[i]];
                Vector3 b = baseVertices[baseTriangles[i + 1]];
                Vector3 c = baseVertices[baseTriangles[i + 2]];
                Vector3 normal = Vector3.Cross(baseMatrix.MultiplyVector(b - a), baseMatrix.MultiplyVector(c - a));
                Assert.That(normal.y, Is.GreaterThan(0), "A retained substrate face is reversed");
            }
            var beds = bedGroups.Select(group => group.GetLODs()[0].renderers[0]).OrderBy(renderer => renderer.bounds.center.z).ToArray();
            Assert.That(beds.All(renderer => renderer.bounds.min.x < 500 && renderer.bounds.max.x > 500), Is.True,
                "The bed must cover the center of the original walkable route");
            var walkBounds = backing.GetComponent<Renderer>().bounds;
            Assert.That(beds.All(renderer => renderer.bounds.size.x >= walkBounds.size.x * .89f &&
                renderer.bounds.min.x >= walkBounds.min.x - .001f && renderer.bounds.max.x <= walkBounds.max.x + .001f), Is.True,
                "The underlying stone must cover the path without leaving its original footstep footprint");
            for (int i = 1; i < beds.Length; i++)
                Assert.That(beds[i].bounds.min.z - beds[i - 1].bounds.max.z, Is.InRange(0, .01f),
                    "A bed segment left a visible transverse gap or overlaps a neighbour");
            var derived = renderers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
            Assert.That(derived.Length, Is.EqualTo(16), "Eight derived variants per authored LOD must be shared");
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
                foreach (var renderer in beds)
                {
                    var collider = renderer.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    probes.Add(collider);
                }
                Physics.SyncTransforms();
                // A synthetic pair establishes that this geometry audit
                // detects a real distinct layer, independent of PhysX's
                // tolerance when starting a ray very near a triangle.
                var control = new[] {
                    new Vector3(-1, 0, -1), new Vector3(0, 0, 1), new Vector3(1, 0, -1),
                    new Vector3(-1, -.00005f, -1), new Vector3(0, -.00005f, 1), new Vector3(1, -.00005f, -1) };
                Assert.That(HasCloseProjectedBedLayers(control, new[] { 0, 1, 2, 3, 4, 5 }, 0, 0, .0001), Is.True,
                    "The substrate geometry audit did not detect two nearby faces");
                Assert.That(HasCloseProjectedBedLayers(control, new[] { 0, 1, 2 }, 0, 0, .0001), Is.False,
                    "The substrate geometry audit reported a single face twice");
                int nearCoincidentBedHits = 0, sampledBedHits = 0, projectedBedConflicts = 0;
                for (int i = 0; i < beds.Length; i++)
                {
                    var bounds = beds[i].bounds; int hits = 0;
                    var matrix = beds[i].transform.localToWorldMatrix;
                    var projected = new Vector3[baseVertices.Length];
                    for (int vertex = 0; vertex < projected.Length; vertex++)
                        projected[vertex] = matrix.MultiplyVector(baseVertices[vertex]);
                    foreach (float u in new[] { .25f, .5f, .75f })
                    foreach (float v in new[] { .25f, .5f, .75f })
                    {
                        var ray = new Ray(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, u), 1,
                            Mathf.Lerp(bounds.min.z, bounds.max.z, v)), Vector3.down);
                        if (!probes[i].Raycast(ray, out var hit, 2)) continue;
                        Assert.That(hit.point.y, Is.GreaterThan(groundTop));
                        sampledBedHits++;
                        if (HasCloseProjectedBedLayers(projected, baseTriangles,
                            (double)ray.origin.x - matrix.m03, (double)ray.origin.z - matrix.m23, .0001))
                            projectedBedConflicts++;
                        var below = new Ray(hit.point - Vector3.up * .000005f, Vector3.down);
                        if (probes[i].Raycast(below, out var second, .000095f) && second.triangleIndex != hit.triangleIndex)
                            nearCoincidentBedHits++;
                        hits++;
                    }
                    Assert.That(hits, Is.GreaterThanOrEqualTo(7), "Recessed stone bed has a missing area");
                }
                Assert.That(sampledBedHits, Is.GreaterThan(80));
                Assert.That(nearCoincidentBedHits, Is.LessThanOrEqualTo(1),
                    "Distinct near-coincident collider triangles can fight in the depth buffer");
                Assert.That(projectedBedConflicts, Is.LessThanOrEqualTo(1),
                    "Distinct projected substrate faces overlap within 0.1 mm");
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
                        if (!probes[beds.Length + i].Raycast(ray, out var hit, 2)) continue;
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
            var bedMaterial = bedRenderers[0].sharedMaterial;
            Object.Destroy(world.GetComponent(RequireType("TempleForestPath")));
            yield return null; yield return null;
            Assert.That(backing.GetComponent<Renderer>().forceRenderingOff, Is.False);
            Assert.That(derived.All(mesh => !mesh), Is.True, "Derived stone meshes were not released");
            Assert.That((bool)wetMaterial, Is.False, "Owned stone material was not released");
            Assert.That((bool)bedMaterial, Is.False, "Owned recessed-bed material was not released");
            Assert.That((bool)bedMesh, Is.False, "Owned trimmed bed mesh was not released");
            Assert.That((bool)bed, Is.False, "Owned recessed-bed objects were not released");
            Assert.That(original.All(mesh => mesh), Is.True, "Shared authored meshes must remain alive");
        }

        // Height intersections of the actual transformed mesh, evaluated in
        // double precision around a local origin so world x/z near 500 m do
        // not make a repeated PhysX hit look like a second face. Shared edge
        // contacts are excluded; overlapping triangle interiors are not.
        static bool HasCloseProjectedBedLayers(Vector3[] vertices, int[] triangles, double x, double z, double threshold)
        {
            var heights = new List<double>();
            const double edge = 1e-7;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                double denominator = ((double)b.z - c.z) * ((double)a.x - c.x) + ((double)c.x - b.x) * ((double)a.z - c.z);
                if (System.Math.Abs(denominator) < 1e-14) continue;
                double wa = (((double)b.z - c.z) * (x - c.x) + ((double)c.x - b.x) * (z - c.z)) / denominator;
                double wb = (((double)c.z - a.z) * (x - c.x) + ((double)a.x - c.x) * (z - c.z)) / denominator;
                double wc = 1 - wa - wb;
                if (wa <= edge || wb <= edge || wc <= edge) continue;
                double height = wa * a.y + wb * b.y + wc * c.y;
                foreach (double previous in heights)
                    if (System.Math.Abs(previous - height) < threshold) return true;
                heights.Add(height);
            }
            return false;
        }
    }
}
