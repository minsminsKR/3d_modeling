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
        public IEnumerator TempleIncenseVesselMatchesThePlacedIncenseAndPreservesPhysics()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var vessel = world.transform.Find("Incense vessel");
            Assert.That(vessel, Is.Not.Null);
            Assert.That(vessel.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(vessel.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var group = vessel.GetComponentInChildren<LODGroup>(true);
            Assert.That(group, Is.Not.Null);
            var levels = group.GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels[0].renderers.Length, Is.EqualTo(1));
            Assert.That(levels[1].renderers.Length, Is.EqualTo(1));
            var near = levels[0].renderers[0];
            var far = levels[1].renderers[0];
            Assert.That(near.sharedMaterial, Is.SameAs(far.sharedMaterial));
            Assert.That(near.sharedMaterial.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(.4f));
            Assert.That(near.sharedMaterial.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.True);
            Assert.That(world.transform.Find("Placed incense"), Is.Null,
                "The vessel must not include a stick before the player places it");
            var originalTop = world.transform.Find("Prayer table top").GetComponent<BoxCollider>();
            Assert.That(originalTop && originalTop.enabled, Is.True);
            var bounds = near.bounds;
            var min = world.transform.InverseTransformPoint(bounds.min);
            var max = world.transform.InverseTransformPoint(bounds.max);
            Assert.That(min.y, Is.InRange(.84f, .85f));
            Assert.That(max.y, Is.InRange(1.055f, 1.075f));
            // Use the real source-derived mesh, not an invented ash height.
            // This temporary test collider is removed before the next frame.
            var probe = near.gameObject.AddComponent<MeshCollider>();
            try
            {
                probe.sharedMesh = near.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms();
                var ray = new Ray(world.transform.TransformPoint(new Vector3(0, 1.5f, 7.2f)), Vector3.down);
                Assert.That(probe.Raycast(ray, out var hit, 1), Is.True, "Central ash surface is missing");
                float ashY = world.transform.InverseTransformPoint(hit.point).y;
                Assert.That(ashY, Is.InRange(1.00f, 1.025f));
                Assert.That(max.y - ashY, Is.GreaterThan(.025f), "The vessel must have an open raised rim");
                Assert.That(Mathf.Abs(1.02f - ashY), Is.LessThan(.02f), "Existing incense base must meet the ash");
            }
            finally { Object.Destroy(probe); }
            yield return null;
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Call(presentation, "RestoreIncense", true, player);
            var placed = world.transform.Find("Placed incense");
            Assert.That(placed, Is.Not.Null);
            Assert.That(Vector3.Distance(placed.localPosition, new Vector3(0, 1.02f, 7.2f)), Is.LessThan(.0001f));
            Assert.That(originalTop.enabled, Is.True);
            Assert.That(vessel.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
