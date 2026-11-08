using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Independently sample the generated triangles in XZ to check that a
        // tree's root is seated on the actual rendered surface, not just the
        // generation formula. No test collider is added to the scene.
        static float TempleHorizonTriangleHeight(Mesh mesh, Vector3 point)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            for (int n = 0; n < triangles.Length; n += 3)
            {
                var a = vertices[triangles[n]]; var b = vertices[triangles[n + 1]]; var c = vertices[triangles[n + 2]];
                float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(determinant) < .00001f) continue;
                float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / determinant;
                float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / determinant;
                float w = 1 - u - v;
                if (u >= -.0001f && v >= -.0001f && w >= -.0001f)
                    return u * a.y + v * b.y + w * c.y;
            }
            Assert.Fail("A distant tree base fell outside the visual ground mesh");
            return float.NaN;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestHorizonExtendsAllFourSidesWithoutCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var ground = world.transform.Find("Forest ground");
            var horizon = world.transform.Find("Forest horizon terrain");
            Assert.That(ground && horizon, Is.True);
            Assert.That(horizon.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(horizon.GetComponentsInChildren<LODGroup>(true).Length, Is.EqualTo(32));
            var edge = world.transform.Find("Forest edge undergrowth");
            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.childCount, Is.EqualTo(32));
            Assert.That(edge.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(ground.GetComponent<Renderer>().forceRenderingOff, Is.True);
            var depth = world.transform.Find("Natural distant forest depth");
            Assert.That(depth && depth.childCount == 64, Is.True);
            Assert.That(depth.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(depth.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var depthMesh = horizon.GetComponent<MeshFilter>().sharedMesh;
            foreach (Transform tree in depth)
            {
                var position = tree.localPosition; var centre = ground.localPosition; var size = ground.localScale;
                float x = (position.x - centre.x) / (size.x * .5f), z = (position.z - centre.z) / (size.z * .5f);
                Assert.That(Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)), Is.GreaterThan(1f));
                Assert.That(position.y, Is.EqualTo(TempleHorizonTriangleHeight(depthMesh, position) - .07f).Within(.01f));
                var lod = tree.GetComponent<LODGroup>().GetLODs();
                Assert.That(lod.Length, Is.EqualTo(2));
                Assert.That(lod[0].screenRelativeTransitionHeight, Is.EqualTo(.46f).Within(.001f));
                Assert.That(tree.GetComponentsInChildren<Renderer>(true).All(r => r.shadowCastingMode == ShadowCastingMode.Off), Is.True);
            }
            var renderer = horizon.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterial, Is.SameAs(ground.GetComponent<Renderer>().sharedMaterial));
            var haze = horizon.Find("Far forest terrain haze");
            Assert.That(haze, Is.Not.Null, "Far terrain should blend toward the horizon sky");
            Assert.That(haze.GetComponentsInChildren<Collider>(true), Is.Empty);
            var hazeMesh = haze.GetComponent<MeshFilter>().sharedMesh;
            var hazeMaterial = haze.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(hazeMesh.vertexCount, Is.EqualTo(128 * 3));
            Assert.That(hazeMesh.triangles.Length, Is.EqualTo(128 * 12));
            var alpha = hazeMesh.colors;
            Assert.That(alpha.Take(128).All(c => c.a == 0), Is.True);
            Assert.That(alpha.Skip(128).Take(128).All(c => c.a > 0 && c.a < 1), Is.True);
            Assert.That(alpha.Skip(256).All(c => c.a > .9f && c.a <= 1), Is.True);
            var bounds = renderer.bounds;
            var floor = ground.GetComponent<Renderer>().bounds;
            Assert.That(bounds.min.x, Is.LessThan(floor.min.x - 30));
            Assert.That(bounds.max.x, Is.GreaterThan(floor.max.x + 30));
            Assert.That(bounds.min.z, Is.LessThan(floor.min.z - 30));
            Assert.That(bounds.max.z, Is.GreaterThan(floor.max.z + 30));
            var mesh = horizon.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(32 * 4 * 5 + 1));
            Assert.That(mesh.triangles.Length, Is.EqualTo(32 * 4 * (4 * 6 + 3)));
            Assert.That(mesh.normals.All(normal => normal.y > 0), Is.True);
            var vertices = mesh.vertices;
            foreach (int ring in new[] { 3, 4 })
            {
                var heights = vertices.Skip(ring * 128).Take(128).Select(vertex => vertex.y).ToArray();
                Assert.That(heights.Max() - heights.Min(), Is.GreaterThan(ring == 3 ? 1f : 2f));
                Assert.That(Mathf.Abs(heights[0] - heights[127]), Is.LessThan(1.5f), "The closed ridge must not jump at the seam");
            }
            var source = Resources.Load<GameObject>("TemplePrologue/pine-far-branches-visual");
            Assert.That(source, Is.Not.Null);
            var sourceLevels = source.GetComponent<LODGroup>().GetLODs();
            Assert.That(sourceLevels.Length, Is.EqualTo(3));
            var sourceHigh = sourceLevels[0].renderers.Select(piece => piece.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var sourceMiddle = sourceLevels[1].renderers.Select(piece => piece.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var sourceFar = sourceLevels[2].renderers.Select(piece => piece.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var sideCounts = new int[4];
            var treeRoots = horizon.Cast<Transform>().Where(child => child.name.StartsWith("Distant forest pine ")).ToArray();
            Assert.That(treeRoots.Length, Is.EqualTo(32));
            foreach (var tree in treeRoots)
            {
                var position = tree.localPosition; var centre = ground.localPosition; var size = ground.localScale;
                float x = (position.x - centre.x) / (size.x * .5f), z = (position.z - centre.z) / (size.z * .5f);
                float distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                bool nearRow = tree.name.StartsWith("Distant forest pine 1-");
                Assert.That(distance, Is.InRange(nearRow ? 1.26f : 1.43f, nearRow ? 1.40f : 1.57f),
                    "Every backdrop tree must stay outside the old playable ground");
                sideCounts[Mathf.Abs(x) >= Mathf.Abs(z) ? (x >= 0 ? 1 : 3) : (z >= 0 ? 2 : 0)]++;
                Assert.That(position.y, Is.EqualTo(TempleHorizonTriangleHeight(mesh, position) - .06f).Within(.01f),
                    "A tree should sit on the visual soil rather than float above or sink beneath it");
                var group = tree.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null);
                var levels = group.GetLODs();
                Assert.That(levels.Length, Is.EqualTo(2));
                Assert.That(levels[0].screenRelativeTransitionHeight, Is.EqualTo(.70f).Within(.0001f));
                Assert.That(levels[1].screenRelativeTransitionHeight, Is.EqualTo(.015f).Within(.0001f));
                Assert.That(levels[0].renderers.Select(piece => piece.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(sourceMiddle));
                Assert.That(levels[1].renderers.Select(piece => piece.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(sourceFar));
                Assert.That(levels.SelectMany(level => level.renderers).All(piece => piece.enabled), Is.True);
                var originalHigh = tree.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => sourceHigh.Contains(filter.sharedMesh)).Select(filter => filter.GetComponent<Renderer>()).ToArray();
                Assert.That(originalHigh.Length, Is.EqualTo(sourceHigh.Length));
                Assert.That(originalHigh.All(piece => piece && !piece.enabled), Is.True,
                    "A dropped original LOD0 renderer would otherwise draw outside LODGroup control");
                Assert.That(originalHigh.All(piece => !levels.SelectMany(level => level.renderers).Contains(piece)), Is.True);
                Assert.That(tree.GetComponentsInChildren<Renderer>(true).All(piece => piece.shadowCastingMode == ShadowCastingMode.Off), Is.True);
            }
            Assert.That(sideCounts.All(count => count >= 6), Is.True, "Both rows must cover all four sides after subdivision");
            var owner = world.GetComponent(RequireType("TempleForestHorizon"));
            Object.Destroy(owner); yield return null; yield return null;
            Assert.That(horizon == null, Is.True);
            Assert.That(hazeMesh == null && hazeMaterial == null, Is.True, "Owned terrain haze must be destroyed");
            Assert.That(edge == null, Is.True);
            var originalGround = ground.GetComponent<Renderer>();
            Assert.That(originalGround.forceRenderingOff, Is.False);
            Assert.That(ground.GetComponent<Collider>(), Is.Not.Null);
            // Also preserve a pre-existing hidden rendering state, rather than
            // assuming the original renderer was always visible.
            originalGround.forceRenderingOff = true;
            var replacementOwner = world.gameObject.AddComponent(RequireType("TempleForestHorizon"));
            try
            {
                Call(replacementOwner, "Prepare", originalGround);
                Object.Destroy(replacementOwner); yield return null; yield return null;
                Assert.That(originalGround.forceRenderingOff, Is.True, "The previous force-off state must be restored");
            }
            finally
            {
                if (replacementOwner) Object.Destroy(replacementOwner);
                if (originalGround) originalGround.forceRenderingOff = false;
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestBanksReuseRockLodsOutsideTheWalkableApproach()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var finish = world.GetComponent(RequireType("TempleForestBanks"));
            var root = world.transform.Find("Natural forest banks");
            Assert.That(finish && root, Is.True);
            Assert.That(root.childCount, Is.EqualTo(20));
            Assert.That(root.Cast<Transform>().Count(child => child.name.StartsWith("Mossy low bank")), Is.EqualTo(8));
            Assert.That(root.Cast<Transform>().Count(child => child.name.StartsWith("Scattered weathered stones")), Is.EqualTo(12));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(20));
            Assert.That(groups.All(group => group.lodCount == 2), Is.True);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(40));
            Assert.That(renderers.All(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off), Is.True);
            var path = world.transform.Find("Stone approach").GetComponent<Renderer>().bounds;
            var ground = world.transform.Find("Forest ground").GetComponent<Renderer>().bounds.max.y;
            foreach (Transform holder in root)
            {
                var pieces = holder.GetComponentsInChildren<Renderer>(true);
                var bounds = pieces[0].bounds;
                foreach (var piece in pieces) bounds.Encapsulate(piece.bounds);
                Assert.That(bounds.max.x < path.min.x - .25f || bounds.min.x > path.max.x + .25f, Is.True,
                    "A decorative bank enters the original approach");
                Assert.That(bounds.min.y, Is.InRange(ground - .07f, ground));
                Assert.That(bounds.max.y, Is.InRange(ground + .05f, ground + .52f));
            }
            var sourceRock = Resources.Load<GameObject>("CorridorEntrance/cave-rock-visual");
            var sourceStones = Resources.Load<GameObject>("SchoolRenewal/loose-stones/loose-stones-visual");
            Assert.That(sourceRock && sourceStones, Is.True);
            var originals = sourceRock.GetComponentsInChildren<MeshFilter>(true)
                .Concat(sourceStones.GetComponentsInChildren<MeshFilter>(true)).Select(filter => filter.sharedMesh).ToArray();
            Assert.That(root.GetComponentsInChildren<MeshFilter>(true).All(filter => originals.Contains(filter.sharedMesh)), Is.True,
                "Forest banks must reuse existing authored geometry");
            Assert.Throws<System.InvalidOperationException>(() => Call(finish, "Prepare"));
            Object.Destroy(finish); yield return null; yield return null;
            Assert.That(root == null, Is.True, "Visual bank objects survived component removal");
            Assert.That(originals.All(mesh => mesh), Is.True, "Shared authored geometry was destroyed");
        }
    }
}
