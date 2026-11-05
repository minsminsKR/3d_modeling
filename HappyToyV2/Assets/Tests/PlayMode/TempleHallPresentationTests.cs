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
        public IEnumerator TempleHallPresentationPreservesPhysicalWallAndRestoresTheOldHall()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var shrine = world.transform.Find("Generated Korean temple");
            var wall = world.transform.Find("Temple front physical wall").GetComponent<BoxCollider>();
            var original = shrine.GetComponentsInChildren<Renderer>(true);
            var originalEnabled = Resources.Load<GameObject>("TemplePrologue/temple-visual")
                .GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.enabled).ToArray();
            Assert.That(originalEnabled.Length, Is.EqualTo(original.Length));
            var wallBounds = wall.bounds;
            var presentation = world.GetComponent(RequireType("TempleHallPresentation"));
            Assert.That(presentation, Is.Not.Null, "The production hall must be installed before the world is ready");
            var replacement = world.transform.Find("Temple hall exterior");
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(replacement.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(original.All(renderer => !renderer.enabled), Is.True);
            Assert.That(wall.enabled, Is.True);
            Assert.That(wall.bounds.center, Is.EqualTo(wallBounds.center));
            Assert.That(wall.bounds.size, Is.EqualTo(wallBounds.size));
            var group = replacement.GetComponentInChildren<LODGroup>(true);
            var levels = group.GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            Assert.That(levels.All(level => level.renderers.Length == 1), Is.True);
            Assert.That(levels[0].renderers[0].sharedMaterial, Is.SameAs(levels[1].renderers[0].sharedMaterial));
            var owned = levels[0].renderers[0].sharedMaterial;
            var source = Resources.Load<Material>("TempleForest/temple-hall-v2/surface");
            Assert.That(owned, Is.Not.SameAs(source));
            Assert.That(owned.GetTexture("_BaseMap"), Is.SameAs(source.GetTexture("_BaseMap")));
            var near = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var far = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(near.GetIndexCount(0) / 3, Is.InRange(159000, 160100));
            Assert.That(far.GetIndexCount(0) / 3, Is.InRange(84000, 85100));
            var bounds = levels[0].renderers[0].bounds;
            var minimum = world.transform.InverseTransformPoint(bounds.min);
            Assert.That(minimum.z, Is.GreaterThanOrEqualTo(13.81f), "Hall visually passes in front of the wall");
            Assert.That(minimum.y, Is.InRange(-.03f, .03f));
            var center = world.transform.InverseTransformPoint(bounds.center);
            Assert.That(center.z, Is.InRange(17.5f, 18.5f));
            Assert.That((float)Get<Vector3>(world, "IncensePoint").z, Is.LessThan(bounds.min.z - 3));
            ((Behaviour)presentation).enabled = false; yield return null;
            Assert.That(replacement.gameObject.activeSelf, Is.False);
            for (int i = 0; i < original.Length; i++) Assert.That(original[i].enabled, Is.EqualTo(originalEnabled[i]));
            ((Behaviour)presentation).enabled = true; yield return null;
            Assert.That(replacement.gameObject.activeSelf, Is.True);
            Assert.That(original.All(renderer => !renderer.enabled), Is.True);
            Object.Destroy(presentation); yield return null; yield return null;
            Assert.That(world.transform.Find("Temple hall exterior"), Is.Null);
            for (int i = 0; i < original.Length; i++) Assert.That(original[i].enabled, Is.EqualTo(originalEnabled[i]));
            Assert.That((bool)owned, Is.False);
            Assert.That(wall.enabled, Is.True);
        }
    }
}
