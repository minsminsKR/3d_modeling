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
        public IEnumerator TempleUndergrowthUsesSharedFernsOutsideThePath()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var component = world.GetComponent(RequireType("TempleForestUndergrowth"));
            Assert.That(component, Is.Not.Null);
            Assert.That(Get<int>(component, "PlantCount"), Is.EqualTo(32));
            var root = world.transform.Find("Forest undergrowth");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(32));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var plants = root.Cast<Transform>().ToArray();
            Assert.That(plants.All(plant => Mathf.Abs(plant.localPosition.x) >= 2.4f), Is.True);
            // The two reviewed foreground plants stay outside the central
            // approach; all plants remain visual-only under the checks above.
            Assert.That(plants.Count(plant => (plant.localPosition - new Vector3(-3.8f, -.01f, 7.2f)).sqrMagnitude < .0001f), Is.EqualTo(1));
            Assert.That(plants.Count(plant => (plant.localPosition - new Vector3(3.5f, -.01f, 6.6f)).sqrMagnitude < .0001f), Is.EqualTo(1));
            Assert.That(plants.All(plant => plant.localPosition.z >= -23 && plant.localPosition.z <= 15), Is.True);
            Assert.That(plants.All(plant => plant.localScale.x >= .6f && plant.localScale.x <= 1.4f), Is.True);
            Assert.That(plants.Select(plant => plant.localRotation).Distinct().Count(), Is.GreaterThan(10));
            var groups = plants.Select(plant => plant.GetComponent<LODGroup>()).ToArray();
            Assert.That(groups.All(group => group && group.lodCount == 2), Is.True);
            var renderers = groups.SelectMany(group => group.GetLODs()).SelectMany(level => level.renderers).ToArray();
            Assert.That(renderers.Length, Is.EqualTo(64));
            Assert.That(renderers.All(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off), Is.True);
            Assert.That(renderers.All(renderer => renderer.sharedMaterial && renderer.sharedMaterial.enableInstancing), Is.True);
            Assert.That(renderers.Select(renderer => renderer.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
            var meshes = renderers.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
            Assert.That(meshes.Length, Is.EqualTo(2));
            Assert.That(meshes.Select(mesh => mesh.GetIndexCount(0)/3).Min(), Is.InRange(15000, 16000));
            Assert.That(meshes.Select(mesh => mesh.GetIndexCount(0)/3).Max(), Is.InRange(34000, 35000));
            Assert.Throws<System.InvalidOperationException>(() => Call(component, "Prepare"));
        }
    }
}
