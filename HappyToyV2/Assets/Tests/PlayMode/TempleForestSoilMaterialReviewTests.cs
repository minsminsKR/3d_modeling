using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class SoilReviewSample
        {
            public string view;
            public int examinedPixels, changedPixels;
            public float texturedLuma, neutralLuma, texturedLocalContrast;
        }

        // Optional offscreen material comparison. The neutral material is an
        // owned clone and never changes an asset or the visible user's desktop.
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleForestSoilMaterialOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-story-ui-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string verification = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string storyDirectory = Path.GetFullPath(args[option + 1]);
            Assert.That(storyDirectory.StartsWith(verification + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.True);
            string output = Path.Combine(Path.GetDirectoryName(storyDirectory), "soil-material-review");
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var ground = world.transform.Find("Forest horizon terrain");
            Assert.That(ground, Is.Not.Null);
            var renderer = ground.GetComponent<MeshRenderer>();
            Assert.That(renderer && renderer.enabled && !renderer.forceRenderingOff, Is.True);
            var original = renderer.sharedMaterial;
            Assert.That(original && original.GetTexture("_BaseMap") && original.GetTexture("_BumpMap"), Is.True,
                "Production soil maps must be present before comparison");
            var neutral = new Material(original) { name = "Temporary neutral soil comparison" };
            neutral.SetTexture("_BaseMap", Texture2D.grayTexture);
            neutral.SetTexture("_BumpMap", null);
            neutral.DisableKeyword("_NORMALMAP");
            var eyes = Get<Camera>(player, "eyes");
            var motor = (Behaviour)player;
            bool wasEnabled = motor.enabled;
            Vector3 oldPosition = player.transform.position;
            Quaternion oldRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            var views = new[] { "forest-footing", "temple-approach" };
            var positions = new[] { new Vector3(503.5f, .04f, 490f), new Vector3(500f, .04f, 503.5f) };
            var pitch = new[] { 38f, 19f };
            try
            {
                motor.enabled = false;
                for (int n = 0; n < views.Length; n++)
                {
                    PlacePlayer(positions[n]);
                    eyes.transform.rotation = Quaternion.Euler(pitch[n], 0, 0);
                    Physics.SyncTransforms();
                    yield return null;
                    Texture2D textured = null, flat = null;
                    try
                    {
                        renderer.sharedMaterial = original;
                        textured = SchoolHorrorFrame(eyes, 1280, 720);
                        renderer.sharedMaterial = neutral;
                        flat = SchoolHorrorFrame(eyes, 1280, 720);
                        Assert.That(CloudExperienceTests.HasContent(textured) && CloudExperienceTests.HasContent(flat), Is.True);
                        File.WriteAllBytes(Path.Combine(output, views[n] + "-textured.png"), textured.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(output, views[n] + "-neutral.png"), flat.EncodeToPNG());
                        var a = textured.GetPixels32(); var b = flat.GetPixels32();
                        int width = textured.width, height = textured.height, changed = 0, examined = 0;
                        double lumaA = 0, lumaB = 0, contrast = 0;
                        // Lower image half concentrates on ground while keeping
                        // the identical camera and lighting in both renders.
                        for (int y = 0; y < height / 2; y++)
                        for (int x = 1; x < width; x++)
                        {
                            int i = y * width + x; var c = a[i]; var d = b[i];
                            float la = .2126f * c.r + .7152f * c.g + .0722f * c.b;
                            float lb = .2126f * d.r + .7152f * d.g + .0722f * d.b;
                            var previous = a[i - 1];
                            float lp = .2126f * previous.r + .7152f * previous.g + .0722f * previous.b;
                            lumaA += la; lumaB += lb; contrast += Math.Abs(la - lp); examined++;
                            if (Mathf.Max(Mathf.Abs(c.r - d.r), Mathf.Abs(c.g - d.g), Mathf.Abs(c.b - d.b)) >= 4) changed++;
                        }
                        var sample = new SoilReviewSample { view = views[n], examinedPixels = examined,
                            changedPixels = changed, texturedLuma = (float)(lumaA / examined),
                            neutralLuma = (float)(lumaB / examined), texturedLocalContrast = (float)(contrast / examined) };
                        File.WriteAllText(Path.Combine(output, views[n] + "-metrics.json"), JsonUtility.ToJson(sample, true));
                        TestContext.Out.WriteLine(JsonUtility.ToJson(sample));
                        Assert.That(changed, Is.GreaterThan(1000), "The visible ground should respond to its authored texture");
                    }
                    finally
                    {
                        renderer.sharedMaterial = original;
                        if (textured) Object.Destroy(textured);
                        if (flat) Object.Destroy(flat);
                    }
                }
            }
            finally
            {
                renderer.sharedMaterial = original;
                PlacePlayer(oldPosition); player.transform.rotation = oldRotation;
                eyes.transform.rotation = oldEyeRotation; motor.enabled = wasEnabled;
                Object.Destroy(neutral);
            }
            Assert.That(renderer.sharedMaterial, Is.SameAs(original), "Original ground material must be restored");
        }
    }
}
