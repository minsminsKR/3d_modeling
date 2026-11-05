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
            Assert.That(source, Is.Not.Null, "Shared authored material was destroyed");
        }
    }
}
