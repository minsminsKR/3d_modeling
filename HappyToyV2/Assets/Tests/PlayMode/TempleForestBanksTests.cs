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
            var renderer = horizon.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterial, Is.SameAs(ground.GetComponent<Renderer>().sharedMaterial));
            var bounds = renderer.bounds;
            var floor = ground.GetComponent<Renderer>().bounds;
            Assert.That(bounds.min.x, Is.LessThan(floor.min.x - 30));
            Assert.That(bounds.max.x, Is.GreaterThan(floor.max.x + 30));
            Assert.That(bounds.min.z, Is.LessThan(floor.min.z - 30));
            Assert.That(bounds.max.z, Is.GreaterThan(floor.max.z + 30));
            var mesh = horizon.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.normals.All(normal => normal.y > 0), Is.True);
            var owner = world.GetComponent(RequireType("TempleForestHorizon"));
            Object.Destroy(owner); yield return null; yield return null;
            Assert.That(horizon == null, Is.True);
            Assert.That(ground.GetComponent<Collider>(), Is.Not.Null);
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
