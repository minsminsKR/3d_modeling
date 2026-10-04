using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    // Called by the existing authored-camera test. No moved actors, replacement
    // camera, disabled scene geometry or separate mock rendering passes.
    internal static class CloudSignRenderingTests
    {
        internal static void AssertPhysicalWorldSigns()
        {
            var signs = Components("AnnexSignFont");
            Assert.That(signs.Length, Is.EqualTo(15), "Nine authored Korean signs plus six legacy exterior/interior room signs must be managed");
            var font = Resources.Load<Font>("Fonts/Korean");
            Assert.That(font, Is.Not.Null);
            foreach (var sign in signs)
            {
                Assert.That(sign.GetComponents(RequireType("AnnexSignFont")).Length, Is.EqualTo(1), sign.name);
                Assert.That(sign.GetComponent<TextMesh>().font, Is.SameAs(font), sign.name);
                var material = sign.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material, Is.Not.Null, sign.name);
                Assert.That(material.shader.name, Is.EqualTo("HappyToy/WorldSignText"), sign.name);
                Assert.That(material.shader.isSupported, Is.True, sign.name);
                Assert.That(material.GetInt("_Cull"), Is.EqualTo((int)CullMode.Back), sign.name);
                Assert.That(material.GetInt("_ZTest"), Is.EqualTo((int)CompareFunction.LessEqual), sign.name);
                Assert.That(material.mainTexture, Is.SameAs(font.material.mainTexture), sign.name);
                Assert.That(material, Is.Not.SameAs(font.material), "World depth/culling must not change the shell's shared GUI font");
            }
            Assert.That(font.material.shader.name, Is.EqualTo("GUI/Text Shader"));
            var legacy = signs.Where(sign => sign.name.EndsWith(" sign") || sign.name.EndsWith(" sign interior")).ToArray();
            Assert.That(legacy.Length, Is.EqualTo(6));
            foreach (string room in new[] { "CLASSROOM", "WASHROOM", "INFIRMARY" })
            {
                var outside = legacy.Single(sign => sign.name == room + " sign");
                var inside = legacy.Single(sign => sign.name == room + " sign interior");
                Assert.That(outside.GetComponent<TextMesh>().text, Is.EqualTo(room));
                Assert.That(inside.GetComponent<TextMesh>().text, Is.EqualTo(room));
                Assert.That(Vector3.Dot(outside.transform.forward, inside.transform.forward), Is.LessThan(-.99f));
                Assert.That(Vector3.Dot(inside.transform.position - outside.transform.position, outside.transform.forward), Is.EqualTo(.28f).Within(.001f));
            }
        }
    }
}
