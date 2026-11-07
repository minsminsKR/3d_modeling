using System;
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
        public IEnumerator TempleForestEarthCandidateKeepsScaleAndNormalAligned()
        {
            bool candidate = Array.IndexOf(Environment.GetCommandLineArgs(), "-v2-forest-earth-original") < 0;
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var ground = world.transform.Find("Forest ground");
            Assert.That(ground, Is.Not.Null);
            var renderer = ground.GetComponent<Renderer>();
            var collider = ground.GetComponent<Collider>();
            Assert.That(renderer && collider && collider.enabled, Is.True, "Ground must retain its physical support");
            var material = renderer.sharedMaterial;
            var color = material.GetTexture("_BaseMap") as Texture2D;
            var normal = material.GetTexture("_BumpMap") as Texture2D;
            Assert.That(color && normal, Is.True);
            string expected = candidate ? "forest-earth-candidate-" : "forest-earth-";
            Assert.That(color.name, Is.EqualTo(expected + "albedo"));
            Assert.That(normal.name, Is.EqualTo(expected + "height"));
            int resolution = candidate ? 2048 : 1024;
            Assert.That(color.width, Is.EqualTo(resolution)); Assert.That(color.height, Is.EqualTo(resolution));
            Assert.That(normal.width, Is.EqualTo(resolution)); Assert.That(normal.height, Is.EqualTo(resolution));
            var wrap = candidate ? TextureWrapMode.Repeat : TextureWrapMode.Mirror;
            Assert.That(color.wrapMode, Is.EqualTo(wrap));
            Assert.That(normal.wrapMode, Is.EqualTo(wrap));
            Assert.That(color.mipmapCount, Is.GreaterThan(1)); Assert.That(normal.mipmapCount, Is.GreaterThan(1));
            var tiles = candidate ? new Vector2(7, 9) : new Vector2(14, 18);
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(tiles));
            Assert.That(material.GetTextureScale("_BumpMap"), Is.EqualTo(tiles));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(.38f).Within(.001f));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(.22f).Within(.001f));
            Assert.That(material.GetFloat("_Metallic"), Is.Zero);
        }
    }
}
