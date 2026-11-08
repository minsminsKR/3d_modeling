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
        [Serializable] sealed class TempleHorizonBandReport
        {
            public string pose, note;
            public int controlDifference, skyBandDifference, terrainHazeDifference;
            public int bothDifference, worldFogDifference, mistVolumesDifference, restoredDifference;
        }

        // Opt-in diagnostic only. Both shader variants are created in memory
        // from the exact live source; the sole sky intervention removes its
        // horizon-band lerp. The remaining comparison masks are not object IDs.
        [UnityTest, Timeout(300000)]
        public IEnumerator TempleHorizonBandOffscreenReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-horizon-band-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
#if !UNITY_EDITOR
            Assert.Ignore("Dynamic source comparison is editor-only"); yield break;
#else
            Directory.CreateDirectory(output);
            const string originalName = "Shader \"HappyToy/TempleForestNightSky\"";
            const string band = "sky = lerp(sky, float3(.026, .035, .046), horizonHaze * .94);";
            string shaderPath = Path.Combine(Application.dataPath, "Resources/TempleForestNightSky.shader");
            string source = File.ReadAllText(shaderPath);
            Assert.That(source.Split(new[] { originalName }, StringSplitOptions.None).Length, Is.EqualTo(2));
            Assert.That(source.Split(new[] { band }, StringSplitOptions.None).Length, Is.EqualTo(2), "Review must edit exactly the known band expression");
            string controlSource = source.Replace(originalName, "Shader \"Hidden/TempleHorizonBandControl\"");
            string noBandSource = source.Replace(originalName, "Shader \"Hidden/TempleHorizonBandDisabled\"")
                .Replace(band, "sky = lerp(sky, float3(.026, .035, .046), horizonHaze * 0.0);");
            Shader controlShader = null, noBandShader = null;
            Material controlSky = null, noBandSky = null;
            Camera eyes = null; Light flashlight = null; Behaviour motor = null;
            Vector3 oldPosition = Vector3.zero;
            Quaternion oldPlayerRotation = Quaternion.identity, oldEyeRotation = Quaternion.identity;
            float oldFov = 0, oldAspect = 0;
            bool oldMotor = false, oldFlashlight = false, paused = false, havePlayer = false;
            bool oldFog = RenderSettings.fog;
            Material oldSkybox = null;
            Renderer groundHaze = null;
            bool oldHazeOff = false;
            Renderer[] volumes = null;
            bool[] oldVolumeOff = null;
            try
            {
                controlShader = UnityEditor.ShaderUtil.CreateShaderAsset(controlSource);
                noBandShader = UnityEditor.ShaderUtil.CreateShaderAsset(noBandSource);
                Assert.That(controlShader && noBandShader && controlShader.isSupported && noBandShader.isSupported, Is.True);
                Assert.That(UnityEditor.ShaderUtil.ShaderHasError(controlShader) || UnityEditor.ShaderUtil.ShaderHasError(noBandShader), Is.False);
                Call(session, "ConfigureRecordDirectory", NewRecordFixture());
                Call(session, "CreatePrologue"); Begin(); yield return null;
                Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
                var world = Get<Component>(session, "Prologue");
                var horizon = world.transform.Find("Forest horizon terrain");
                var haze = horizon ? horizon.Find("Far forest terrain haze") : null;
                groundHaze = haze ? haze.GetComponent<Renderer>() : null;
                Assert.That(groundHaze && groundHaze.sharedMaterial &&
                    groundHaze.sharedMaterial.shader == Resources.Load<Shader>("TempleForestTerrainHaze"), Is.True);
                oldHazeOff = groundHaze.forceRenderingOff;
                Assert.That(oldHazeOff, Is.False, "Production terrain haze must be visible in the baseline");
                var atmosphere = world.GetComponent(RequireType("TempleForestAtmosphere"));
                Assert.That(atmosphere, Is.Not.Null);
                volumes = atmosphere.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.sharedMaterial && r.sharedMaterial.shader == Resources.Load<Shader>("TempleForestAtmosphereMist")).ToArray();
                Assert.That(volumes.Length, Is.EqualTo(5));
                oldVolumeOff = volumes.Select(r => r.forceRenderingOff).ToArray();
                Assert.That(oldVolumeOff.Any(x => x), Is.False);
                oldSkybox = RenderSettings.skybox; oldFog = RenderSettings.fog;
                Assert.That(oldFog && oldSkybox && oldSkybox.shader == Resources.Load<Shader>("TempleForestNightSky"), Is.True);
                controlSky = new Material(oldSkybox) { shader = controlShader, name = "Temple horizon band control" };
                noBandSky = new Material(oldSkybox) { shader = noBandShader, name = "Temple horizon band disabled" };
                eyes = Get<Camera>(player, "eyes"); flashlight = Get<Light>(player, "flashlight"); motor = (Behaviour)player;
                Assert.That(eyes && flashlight && motor, Is.True);
                oldPosition = player.transform.position; oldPlayerRotation = player.transform.rotation; oldEyeRotation = eyes.transform.rotation;
                oldFov = eyes.fieldOfView; oldAspect = eyes.aspect; oldMotor = motor.enabled; oldFlashlight = flashlight.enabled; havePlayer = true;
                motor.enabled = false; flashlight.enabled = false;
                eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                PlacePlayer(new Vector3(500f, .04f, 503.5f)); Physics.SyncTransforms();
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                var names = new[] { "front", "right", "back", "left" };
                var directions = new[] { new Vector3(0, .05f, 1), Vector3.right, Vector3.back, Vector3.left };
                for (int i = 0; i < names.Length; i++)
                {
                    eyes.transform.rotation = Quaternion.LookRotation(directions[i], Vector3.up);
                    var reference = TempleHorizonBandFrame(eyes, output, names[i] + "-baseline");
                    RenderSettings.skybox = controlSky;
                    var control = TempleHorizonBandFrame(eyes, output, names[i] + "-control");
                    int controlDifference = TempleHorizonBandDifference(reference, control);
                    Assert.That(controlDifference, Is.LessThanOrEqualTo(8), "A renamed, otherwise identical shader must match production before isolating the band");
                    RenderSettings.skybox = noBandSky;
                    var noSky = TempleHorizonBandFrame(eyes, output, names[i] + "-sky-band-off");
                    RenderSettings.skybox = oldSkybox; groundHaze.forceRenderingOff = true;
                    var noTerrain = TempleHorizonBandFrame(eyes, output, names[i] + "-terrain-haze-off");
                    RenderSettings.skybox = noBandSky;
                    var noBoth = TempleHorizonBandFrame(eyes, output, names[i] + "-both-off");
                    RenderSettings.skybox = oldSkybox; groundHaze.forceRenderingOff = oldHazeOff;
                    RenderSettings.fog = false;
                    var noFog = TempleHorizonBandFrame(eyes, output, names[i] + "-world-fog-off");
                    RenderSettings.fog = oldFog;
                    foreach (var renderer in volumes) renderer.forceRenderingOff = true;
                    var noVolumes = TempleHorizonBandFrame(eyes, output, names[i] + "-mist-volumes-off");
                    for (int j = 0; j < volumes.Length; j++) volumes[j].forceRenderingOff = oldVolumeOff[j];
                    var restored = TempleHorizonBandFrame(eyes, output, names[i] + "-restored");
                    var report = new TempleHorizonBandReport {
                        pose = names[i], controlDifference = controlDifference,
                        skyBandDifference = TempleHorizonBandDifference(reference, noSky),
                        terrainHazeDifference = TempleHorizonBandDifference(reference, noTerrain),
                        bothDifference = TempleHorizonBandDifference(reference, noBoth),
                        worldFogDifference = TempleHorizonBandDifference(reference, noFog),
                        mistVolumesDifference = TempleHorizonBandDifference(reference, noVolumes),
                        restoredDifference = TempleHorizonBandDifference(reference, restored),
                        note = "Frozen 1280x720 editor camera; changed pixels differ by at least 3 in an RGB channel. Differences may include occlusion, transparency and postprocessing; compare the images to locate the actual band. Not a performance or visual-quality certification."
                    };
                    File.WriteAllText(Path.Combine(output, names[i] + "-metrics.json"), JsonUtility.ToJson(report, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                    Assert.That(report.restoredDifference, Is.LessThanOrEqualTo(8), "The untouched baseline must be restored");
                }
            }
            finally
            {
                if (groundHaze) groundHaze.forceRenderingOff = oldHazeOff;
                if (volumes != null && oldVolumeOff != null)
                    for (int i = 0; i < volumes.Length; i++) if (volumes[i]) volumes[i].forceRenderingOff = oldVolumeOff[i];
                if (oldSkybox) RenderSettings.skybox = oldSkybox;
                RenderSettings.fog = oldFog;
                if (havePlayer)
                {
                    PlacePlayer(oldPosition); player.transform.rotation = oldPlayerRotation; eyes.transform.rotation = oldEyeRotation;
                    eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; motor.enabled = oldMotor; flashlight.enabled = oldFlashlight;
                    Physics.SyncTransforms();
                }
                if (paused) Call(shell, "Resume");
                if (controlSky) Object.Destroy(controlSky); if (noBandSky) Object.Destroy(noBandSky);
                if (controlShader) Object.Destroy(controlShader); if (noBandShader) Object.Destroy(noBandShader);
            }
#endif
        }

        static Color32[] TempleHorizonBandFrame(Camera camera, string output, string name)
        {
            var frame = SchoolHorrorFrame(camera, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }

        static int TempleHorizonBandDifference(Color32[] a, Color32[] b)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length)); int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= 3) n++;
            return n;
        }
    }
}
