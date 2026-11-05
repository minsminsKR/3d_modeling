using System.Collections;
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
        public IEnumerator TempleForestPuddlesStayOnSoilWithoutChangingCollision()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var puddles = world.GetComponent(RequireType("TempleForestPuddles"));
            Assert.That(puddles, Is.Not.Null);
            Assert.That(Get<int>(puddles, "PuddleCount"), Is.EqualTo(12));
            var ground = world.transform.Find("Forest ground");
            var path = world.transform.Find("Stone approach");
            Assert.That(ground.GetComponent<Collider>().enabled, Is.True);
            var root = world.transform.Find("Shallow forest water pockets");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var renderer = root.GetComponent<MeshRenderer>();
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(480));
            Assert.That(mesh.GetIndexCount(0) / 3, Is.EqualTo(780));
            Assert.That(mesh.bounds.size.y, Is.GreaterThan(.003f));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(renderer.sharedMaterial.GetFloat("_Metallic"), Is.Zero);
            Assert.That(renderer.sharedMaterial.GetFloat("_Smoothness"), Is.InRange(.65f, .8f));
            Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.True);
            Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), Is.False);
            Assert.That(renderer.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Off));
            float soilTop = ground.GetComponent<Renderer>().bounds.max.y;
            var pathBounds = path.GetComponent<Renderer>().bounds;
            foreach (var vertex in mesh.vertices)
            {
                var position = root.TransformPoint(vertex);
                Assert.That(position.y, Is.GreaterThan(soilTop + .002f));
                Assert.That(position.x < pathBounds.min.x - .05f || position.x > pathBounds.max.x + .05f, Is.True,
                    "A water pocket overlaps the playable stone strip");
            }
            var origin = new Vector3(500, 0, 500);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(0, 0, -5)), Is.True);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(2.35f, 0, -5)), Is.False);
            var ownedMaterial = renderer.sharedMaterial;
            Call(session, "RetirePrologue"); yield return null; yield return null;
            Assert.That(mesh == null, Is.True, "Owned puddle geometry survived prologue retirement");
            Assert.That(ownedMaterial == null, Is.True, "Owned puddle material survived prologue retirement");
        }
    }
}
