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
        public IEnumerator TempleForestFernsRemainSparseAndOutsideThePath()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var root = world.transform.Find("Forest fern accents");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(4));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var plants = root.Cast<Transform>().ToArray();
            Assert.That(plants.All(plant => Mathf.Abs(plant.localPosition.x) >= 3.6f), Is.True);
            Assert.That(plants.All(plant => plant.localScale.x >= .5f && plant.localScale.x <= .8f), Is.True);
            Assert.That(plants.Select(plant => plant.localScale.x).Distinct().Count(), Is.EqualTo(4));
            var groups = plants.Select(plant => plant.GetComponent<LODGroup>()).ToArray();
            Assert.That(groups.All(group => group && group.lodCount == 2), Is.True);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(8));
            Assert.That(renderers.All(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off), Is.True);
            Assert.That(renderers.All(renderer => renderer.sharedMaterial && renderer.sharedMaterial.enableInstancing), Is.True);
            Assert.That(renderers.Select(renderer => renderer.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
        }
    }
}
