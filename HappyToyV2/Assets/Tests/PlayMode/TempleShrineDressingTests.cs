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
        // In the authored normalized paper frame y=0 is the hanging edge and
        // y=-1 is its torn lower edge. Two bends must produce two separate
        // rightward peaks, not a single > shaped chevron.
        static void AssertTwoSidedRitualZigzag(MeshFilter filter, Transform holder)
        {
            var matrix = holder.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var points = filter.sharedMesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            float Centre(float depth)
            {
                var row = points.Where(x => Mathf.Abs(x.y + depth) < .035f).ToArray();
                Assert.That(row.Length, Is.GreaterThan(0), "Authored paper row is missing at " + depth);
                return (row.Min(x => x.x) + row.Max(x => x.x)) * .5f;
            }
            float centre = Centre(.5f);
            Assert.That(Centre(.2f) - centre, Is.GreaterThan(.35f), "Upper bend is missing");
            Assert.That(Centre(.8f) - centre, Is.GreaterThan(.35f), "Lower bend is missing");
            var facing = filter.sharedMesh.normals.Select(normal =>
                Vector3.Dot(filter.transform.TransformDirection(normal).normalized, holder.forward)).ToArray();
            Assert.That(facing.Any(dot => dot > .05f) && facing.Any(dot => dot < -.05f), Is.True,
                "Folded paper must retain front and back normals");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleShrineDressingUsesMaskedLanternsAndKeepsTheRouteClear()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var dressing = world.GetComponent(RequireType("TempleShrineDressing"));
            Assert.That(dressing, Is.Not.Null);
            var root = world.transform.Find("Temple exterior weathering");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(root.Cast<Transform>().Count(child => child.name == "Weathered eave talisman"), Is.EqualTo(4));
            var seals = root.Cast<Transform>().Where(child => child.name == "Intruding shrine seal").ToArray();
            Assert.That(seals.Length, Is.EqualTo(7));
            Assert.That(seals.Select(x => x.localScale.y).Distinct().Count(), Is.GreaterThan(3), "Paper lengths should not form a repeated row");
            Assert.That(root.GetComponentsInChildren<Transform>(true).Count(x => x.name == "Intruding shrine brush marks"), Is.EqualTo(4));
            var foldedFilters = root.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh && x.sharedMesh.name == "Owned shrine folded paper").ToArray();
            var folded = foldedFilters.Select(x => x.sharedMesh).ToArray();
            Assert.That(folded.Length, Is.EqualTo(3));
            Assert.That(folded.All(x => x.vertexCount > 0 && x.uv.Length == x.vertexCount && x.normals.Length == x.vertexCount), Is.True);
            foreach (var filter in foldedFilters)
            {
                var holder = filter.transform;
                while (holder && holder.name != "Intruding shrine seal") holder = holder.parent;
                Assert.That(holder, Is.Not.Null);
                AssertTwoSidedRitualZigzag(filter, holder);
            }
            var marks = root.GetComponentsInChildren<Transform>(true).First(x => x.name == "Intruding shrine brush marks").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(marks && marks.vertexCount > 0, Is.True);
            var cord = root.Find("Intruding shrine cord").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(cord && cord.vertexCount > 0, Is.True);
            var jars = root.Cast<Transform>().Where(child => child.name == "Weathered exterior offering jar").ToArray();
            Assert.That(jars.Length, Is.EqualTo(2));
            Assert.That(jars.All(jar => Mathf.Abs(jar.localPosition.x) > 3.2f), Is.True);
            var lanterns = world.transform.Cast<Transform>().Where(child => child.name == "Temple lantern").ToArray();
            Assert.That(lanterns.Length, Is.EqualTo(4));
            var renderers = lanterns.SelectMany(lantern => lantern.GetComponentsInChildren<Renderer>(true)).ToArray();
            var source = Resources.Load<Material>("CorridorEntrance/buddhist-lantern/CE_buddhist-lantern_0");
            Assert.That(source && source.GetTexture("_EmissionMap"), Is.True);
            var material = renderers[0].sharedMaterial;
            Assert.That(material, Is.Not.SameAs(source));
            Assert.That(renderers.All(renderer => renderer.sharedMaterial == material), Is.True);
            Assert.That(material.GetTexture("_EmissionMap"), Is.SameAs(source.GetTexture("_EmissionMap")));
            Assert.That(material.GetColor("_BaseColor").r, Is.LessThan(source.GetColor("_BaseColor").r));
            Object.Destroy(dressing); yield return null; yield return null;
            Assert.That(renderers.All(renderer => renderer.sharedMaterial == source), Is.True);
            Assert.That((bool)material, Is.False, "Owned lantern material was not released");
            Assert.That((bool)cord, Is.False, "Owned intrusion cord was not released");
            Assert.That((bool)marks, Is.False, "Owned shrine ink was not released");
            Assert.That(folded.All(mesh => !mesh), Is.True, "Owned folded paper meshes were not released");
            Assert.That(source, Is.Not.Null, "Shared authored material was destroyed");
        }
    }
}
