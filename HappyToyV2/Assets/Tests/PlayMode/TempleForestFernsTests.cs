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

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleFernFamiliesShareMutedDirectLightMaterialWithoutChangingTheSource()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var finish = world.GetComponent(RequireType("TempleForestFoliageFinish"));
            Assert.That(finish, Is.Not.Null);
            var source = Resources.Load<Material>("TempleForest/fern-clump/surface");
            var accents = world.transform.Find("Forest fern accents");
            var undergrowth = world.transform.Find("Forest undergrowth");
            Assert.That(source && accents && undergrowth, Is.True);
            var renderers = accents.GetComponentsInChildren<Renderer>(true)
                .Concat(undergrowth.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(renderers.Length, Is.GreaterThan(8));
            var material = renderers[0].sharedMaterial;
            Assert.That(material, Is.Not.SameAs(source));
            Assert.That(renderers.All(renderer => renderer.sharedMaterial == material), Is.True);
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(source.GetTexture("_BaseMap")));
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Null);
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.False);
            Assert.That(material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.True);
            Assert.That(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), Is.False);
            Assert.That(material.GetFloat("_Metallic"), Is.Zero);
            Assert.That(material.GetFloat("_Smoothness"), Is.InRange(.2f, .35f));
            var tint = material.GetColor("_BaseColor");
            Assert.That(Mathf.Max(tint.r, tint.g, tint.b), Is.LessThan(1));
            Assert.That(material.enableInstancing, Is.True);
            Assert.That(source.GetTexture("_MetallicGlossMap"), Is.Not.Null,
                "The imported authored material must stay unchanged");
            Assert.That(source.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(source.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"), Is.False);
            Assert.That(source.GetFloat("_Metallic"), Is.EqualTo(1));
            Assert.Throws<System.InvalidOperationException>(() => Call(finish, "Prepare"));
            Object.Destroy(finish); yield return null; yield return null;
            Assert.That(material == null, Is.True, "Runtime foliage material leaked");
            Assert.That(renderers.All(renderer => renderer && renderer.sharedMaterial == source), Is.True,
                "Removing the finish must restore both fern families");
        }
    }
}
