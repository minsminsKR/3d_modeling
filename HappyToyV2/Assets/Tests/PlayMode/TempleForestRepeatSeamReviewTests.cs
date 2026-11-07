using System;
using System.Collections;
using System.Collections.Generic;
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
        [Serializable] sealed class EarthSeamSample
        {
            public string view, lighting, material, maskLimitations;
            public int seamPairs, controlPairs, markerRejected, physicsRejected, outsideRejected;
            public float seamMean, seamP95, controlMean, controlP95;
            public EarthSeamProbe[] probes;
        }

        // Pixel coordinates use Unity's bottom-left viewport origin. Mask
        // samples identify whether the actual rendered pixel is ground rather
        // than merely finding the ground collider behind visual-only props.
        [Serializable] sealed class EarthSeamProbe
        {
            public int index, line;
            public float ahead, delta;
            public Vector2 aViewport, bViewport;
            public Color aColor, bColor, aMarker, bMarker;
            public string status;
        }

        static bool EarthSeamIsMarker(Color sample)
        {
            float primary = Mathf.Min(sample.r, sample.b);
            return primary > .25f && sample.g < .45f * primary && primary - sample.g > .18f;
        }

        static float EarthSeamDelta(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b))*255f;

        static float EarthSeamPercentile(List<float> values, float fraction)
        {
            values.Sort();
            return values[Mathf.Clamp(Mathf.CeilToInt(values.Count*fraction)-1, 0, values.Count-1)];
        }

        // Production camera/material, four distinct repeat seams. The sampled
        // pixel differences are observations, not a universal quality threshold.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleForestRepeatSeamOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-story-ui-review-output");
            if (option < 0 || args.Contains("-v2-forest-earth-original")) yield break;
            Assert.That(option+1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string verification = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string story = Path.GetFullPath(args[option+1]);
            Assert.That(story.StartsWith(verification+Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            string output = Path.Combine(Path.GetDirectoryName(story), "soil-repeat-seam-review");
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review directory");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var originalGround = world.transform.Find("Forest ground");
            var visualGround = world.transform.Find("Forest horizon terrain");
            Assert.That(originalGround && visualGround, Is.True);
            var support = originalGround.GetComponent<Collider>();
            var renderer = visualGround.GetComponent<MeshRenderer>();
            Assert.That(support && renderer && renderer.sharedMaterial, Is.True);
            var original = renderer.sharedMaterial;
            var color = original.GetTexture("_BaseMap") as Texture2D;
            var normal = original.GetTexture("_BumpMap") as Texture2D;
            Assert.That(color && normal && color.name == "forest-earth-candidate-albedo" &&
                color.wrapMode == TextureWrapMode.Repeat && normal.wrapMode == TextureWrapMode.Repeat, Is.True);
            var albedoOnly = new Material(original) { name = "Temporary seam albedo-only" };
            albedoOnly.SetTexture("_BumpMap", null); albedoOnly.DisableKeyword("_NORMALMAP");
            var normalOnly = new Material(original) { name = "Temporary seam normal-only" };
            normalOnly.SetTexture("_BaseMap", Texture2D.grayTexture);
            var maskShader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(maskShader && maskShader.isSupported, Is.True, "An unlit diagnostic ground mask is required");
            var groundMarker = new Material(maskShader) { name = "Temporary seam ground marker" };
            groundMarker.SetTexture("_BaseMap", Texture2D.whiteTexture);
            groundMarker.SetColor("_BaseColor", Color.magenta);
            var eyes = Get<Camera>(player, "eyes");
            var torch = Get<Light>(player, "flashlight");
            Assert.That(eyes && torch, Is.True);
            var motor = (Behaviour)player;
            bool oldMotor = motor.enabled, oldTorch = torch.enabled;
            var oldPosition = player.transform.position;
            var oldRotation = player.transform.rotation;
            var oldEyeRotation = eyes.transform.rotation;
            var oldTorchPosition = torch.transform.position;
            var oldTorchRotation = torch.transform.rotation;
            float oldAspect = eyes.aspect;
            try
            {
                motor.enabled = false;
                eyes.aspect = 1280f/720f;
                // Ground UV: U=(x/48+.5)*7, V=((z+2)/64+.5)*9.
                // Scan two distinct integer boundaries on each axis.
                for (int axis = 0; axis < 2; axis++)
                for (int variant = 0; variant < 2; variant++)
                {
                    float seam = axis == 0 ? 48f*((variant == 0 ? 4f : 5f)/7f-.5f) :
                        64f*((variant == 0 ? 3f : 4f)/9f-.5f)-2f;
                    Vector3 feet = axis == 0 ? new Vector3(seam, .04f, -10f) : new Vector3(4.3f, .04f, seam);
                    float yaw = axis == 0 ? 0 : 90;
                    PlacePlayer(world.transform.TransformPoint(feet));
                    eyes.transform.rotation = Quaternion.Euler(38, yaw, 0);
                    torch.transform.SetPositionAndRotation(eyes.transform.position, eyes.transform.rotation);
                    Physics.SyncTransforms(); yield return null; yield return null;
                    string view = (axis == 0 ? "u" : "v") + (axis == 0 ? variant+4 : variant+3);
                    for (int lit = 0; lit < 2; lit++)
                    {
                        torch.enabled = lit != 0;
                        yield return null;
                        Texture2D visibility = null;
                        try
                        {
                            renderer.sharedMaterial = groundMarker;
                            visibility = SchoolHorrorFrame(eyes, 1280, 720);
                            Assert.That(CloudExperienceTests.HasContent(visibility), Is.True, "Ground marker render is blank");
                            File.WriteAllBytes(Path.Combine(output, view + (lit == 0 ? "-moon" : "-flashlight") + "-ground-mask.png"), visibility.EncodeToPNG());
                            renderer.sharedMaterial = original;
                        foreach (var mode in new[] { "full", "albedo-only", "normal-only" })
                        {
                            renderer.sharedMaterial = mode == "full" ? original : mode == "albedo-only" ? albedoOnly : normalOnly;
                            Texture2D frame = null;
                            try
                            {
                                frame = SchoolHorrorFrame(eyes, 1280, 720);
                                Assert.That(CloudExperienceTests.HasContent(frame), Is.True);
                                string name = view + (lit == 0 ? "-moon-" : "-flashlight-") + mode;
                                File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG());
                                var edges = new List<float>(); var controls = new List<float>();
                                var probes = new List<EarthSeamProbe>();
                                int markerRejected = 0, physicsRejected = 0, outsideRejected = 0;
                                for (int j = 0; j < 30; j++)
                                {
                                    float ahead = .8f + j*.095f;
                                    for (int line = -1; line <= 1; line++)
                                    {
                                        float across = seam + line*.55f;
                                        Vector3 local = axis == 0 ? new Vector3(across, support.bounds.max.y+.02f, feet.z+ahead) :
                                            new Vector3(feet.x+ahead, support.bounds.max.y+.02f, across);
                                        Vector3 side = axis == 0 ? Vector3.right*.025f : Vector3.forward*.025f;
                                        // Transform x/z through the prologue root, retaining actual ground height.
                                        Vector3 point = world.transform.TransformPoint(new Vector3(local.x, 0, local.z));
                                        point.y = support.bounds.max.y+.015f;
                                        Vector3 a = point-side, b = point+side;
                                        Vector3 av = eyes.WorldToViewportPoint(a), bv = eyes.WorldToViewportPoint(b);
                                        var probe = new EarthSeamProbe { index = j, line = line, ahead = ahead,
                                            aViewport = new Vector2(av.x,av.y), bViewport = new Vector2(bv.x,bv.y) };
                                        probes.Add(probe);
                                        if (av.z <= 0 || bv.z <= 0 || av.x < 0 || av.x > 1 || av.y < 0 || av.y > 1 ||
                                            bv.x < 0 || bv.x > 1 || bv.y < 0 || bv.y > 1)
                                        { probe.status = "outside"; outsideRejected++; continue; }
                                        probe.aColor = frame.GetPixelBilinear(av.x,av.y);
                                        probe.bColor = frame.GetPixelBilinear(bv.x,bv.y);
                                        probe.aMarker = visibility.GetPixelBilinear(av.x,av.y);
                                        probe.bMarker = visibility.GetPixelBilinear(bv.x,bv.y);
                                        probe.delta = EarthSeamDelta(probe.aColor, probe.bColor);
                                        if (!EarthSeamGroundVisible(eyes, support, a) || !EarthSeamGroundVisible(eyes, support, b))
                                        { probe.status = "physics-occluded"; physicsRejected++; continue; }
                                        if (!EarthSeamIsMarker(probe.aMarker) || !EarthSeamIsMarker(probe.bMarker))
                                        { probe.status = "render-occluded"; markerRejected++; continue; }
                                        probe.status = "accepted";
                                        (line == 0 ? edges : controls).Add(probe.delta);
                                    }
                                }
                                var report = new EarthSeamSample { view = view, lighting = lit == 0 ? "moon" : "flashlight", material = mode,
                                    maskLimitations = "Color mask rejects visible opaque props even without colliders; translucent fog, puddles, glass, postprocessing and antialiased edges may mix with the marker and cannot be ruled out. Viewport coordinates use bottom-left origin. Deltas are max RGB channel differences on the readback, not perceptual or GPU normal-map measurements.",
                                    seamPairs = edges.Count, controlPairs = controls.Count, markerRejected = markerRejected,
                                    physicsRejected = physicsRejected, outsideRejected = outsideRejected,
                                    seamMean = edges.Count > 0 ? edges.Average() : -1, controlMean = controls.Count > 0 ? controls.Average() : -1,
                                    seamP95 = edges.Count > 0 ? EarthSeamPercentile(edges,.95f) : -1, controlP95 = controls.Count > 0 ? EarthSeamPercentile(controls,.95f) : -1, probes = probes.ToArray() };
                                // Preserve per-probe evidence even if there are too few visible samples.
                                File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(report,true));
                                TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                                Assert.That(edges.Count, Is.GreaterThanOrEqualTo(8), name + " insufficient unobstructed seam samples");
                                Assert.That(controls.Count, Is.GreaterThanOrEqualTo(8), name + " insufficient unobstructed comparison samples");
                            }
                            finally { if (frame) Object.Destroy(frame); renderer.sharedMaterial = original; }
                        }
                        }
                        finally { if (visibility) Object.Destroy(visibility); renderer.sharedMaterial = original; }
                    }
                }
            }
            finally
            {
                renderer.sharedMaterial = original;
                PlacePlayer(oldPosition); player.transform.rotation = oldRotation;
                eyes.transform.rotation = oldEyeRotation; eyes.aspect = oldAspect;
                torch.transform.SetPositionAndRotation(oldTorchPosition, oldTorchRotation); torch.enabled = oldTorch;
                motor.enabled = oldMotor; Object.Destroy(albedoOnly); Object.Destroy(normalOnly); Object.Destroy(groundMarker);
            }
        }

        static bool EarthSeamGroundVisible(Camera camera, Collider ground, Vector3 point)
        {
            Vector3 direction = point-camera.transform.position;
            if (Physics.Raycast(camera.transform.position, direction.normalized, out var hit, direction.magnitude+.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.collider == ground;
            return false;
        }
    }
}
