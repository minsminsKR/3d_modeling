using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Explicit opt-in only. Both layers are rendered to textures; this
        // never changes the desktop display, focuses a window or sends input.
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleStoryEvidenceUiOffscreenAspectReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-story-ui-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length), "Review output is required");
            Assert.That(Application.isBatchMode && args.Contains("-v2-audit-no-desktop-interaction") && args.Contains("-noaudio"), Is.True,
                "This offscreen review requires batch mode, desktop isolation and muted output");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Actual UI and camera rendering are required");
            string builds = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(builds + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Never reuse a prior review");
            Directory.CreateDirectory(output);
            TestContext.Out.WriteLine("Temple story UI output: " + output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var view = One("GameShellView");
            Call(player, "RelocateWorld", new Vector3(500.5f, .04f, 505.8f), Quaternion.identity);
            ((Behaviour)player).enabled = false; // Deterministic view, no simulated input.
            var eyes = Get<Camera>(player, "eyes");
            eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500.65f, .85f, 507.46f) - eyes.transform.position);
            Call(world, "ReadStoryClue");
            var narration = world.GetComponent(RequireType("StoryNarration"));
            yield return Wait(() => Get<string>(narration, "CurrentText").Contains("이름칸"), 5, "First-day note did not appear");
            ((Behaviour)narration).enabled = false; // Freeze a real active line during the offscreen review.
            CaptureHorizonComparison(eyes, world, output);
            CaptureMistComparison(eyes, world, output);
            yield return CaptureHorizonEdges(eyes, world, output);
            yield return CaptureTempleStoryUiAspects(view, eyes, world, output, "first");

            var snapshot = Call(world, "Capture", 0f);
            Set(snapshot, "phase", 3); // Restore a valid second-day approach with the observed first clue.
            Set(snapshot, "incensePlaced", false); Set(snapshot, "gateVisible", false);
            Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", snapshot); yield return null;
            world = Get<Component>(session, "Prologue");
            eyes = Get<Camera>(player, "eyes");
            ((Behaviour)player).enabled = false;
            eyes.transform.rotation = Quaternion.LookRotation(new Vector3(500.65f, .85f, 507.46f) - eyes.transform.position);
            Call(world, "ReadStoryClue");
            narration = world.GetComponent(RequireType("StoryNarration"));
            yield return Wait(() => Get<string>(narration, "CurrentText").Contains("집 그림"), 5, "Second-day note did not appear");
            ((Behaviour)narration).enabled = false;
            yield return CaptureTempleStoryUiAspects(view, eyes, world, output, "second");
            Call(view, "RestoreDisplayOutput");
        }

        // Compare the same first-day eye camera with the entire terrain surface
        // hidden, including its far haze. Trees and colliders remain untouched;
        // restore both rendering flags even if readback fails.
        void CaptureHorizonComparison(Camera eyes, Component world, string output)
        {
            var horizon = world.transform.Find("Forest horizon terrain");
            Assert.That(horizon, Is.Not.Null, "The authored horizon must exist");
            var terrain = horizon.GetComponent<MeshRenderer>();
            Assert.That(terrain, Is.Not.Null, "The horizon must own a terrain renderer");
            bool previousForceOff = terrain.forceRenderingOff;
            var haze = horizon.Find("Far forest terrain haze")?.GetComponent<MeshRenderer>();
            Assert.That(haze, Is.Not.Null, "The terrain haze must exist for this GPU review");
            bool previousHazeForceOff = haze.forceRenderingOff;
            Assert.That(previousForceOff || previousHazeForceOff, Is.False, "The baseline must show the terrain and haze");
            Texture2D baseline = null, hidden = null;
            try
            {
                baseline = SchoolHorrorFrame(eyes, 1280, 720);
                File.WriteAllBytes(Path.Combine(output, "first-world-horizon-on.png"), baseline.EncodeToPNG());
                terrain.forceRenderingOff = true;
                haze.forceRenderingOff = true;
                hidden = SchoolHorrorFrame(eyes, 1280, 720);
                File.WriteAllBytes(Path.Combine(output, "first-world-horizon-off.png"), hidden.EncodeToPNG());
            }
            finally
            {
                terrain.forceRenderingOff = previousForceOff;
                haze.forceRenderingOff = previousHazeForceOff;
                if (baseline) Object.Destroy(baseline);
                if (hidden) Object.Destroy(hidden);
            }
        }

        // Compare the same rendered scene with only the five mist volumes
        // hidden. This verifies that the production shader actually affects
        // the world instead of merely existing in the hierarchy.
        void CaptureMistComparison(Camera eyes, Component world, string output)
        {
            var volumes = world.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.sharedMaterial && r.sharedMaterial.shader &&
                    r.sharedMaterial.shader.name == "HappyToy/TempleForestAtmosphereMist").ToArray();
            Assert.That(volumes.Length, Is.EqualTo(5));
            var saved = volumes.Select(r => r.forceRenderingOff).ToArray();
            Texture2D visible = null, hidden = null;
            try
            {
                visible = SchoolHorrorFrame(eyes, 1280, 720);
                File.WriteAllBytes(Path.Combine(output, "first-world-mist-on.png"), visible.EncodeToPNG());
                foreach (var volume in volumes) volume.forceRenderingOff = true;
                hidden = SchoolHorrorFrame(eyes, 1280, 720);
                File.WriteAllBytes(Path.Combine(output, "first-world-mist-off.png"), hidden.EncodeToPNG());
                var a = visible.GetPixels32(); var b = hidden.GetPixels32();
                int changed = 0, mistErrorPixels = 0, baselineErrorPixels = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= 3)
                        changed++;
                    // Error shader magenta can otherwise look like a valid
                    // on/off difference, even when isSupported is true before
                    // lazy D3D compilation. Compare with the same mist-off frame.
                    if (a[i].r >= 220 && a[i].b >= 220 && a[i].g <= 64) mistErrorPixels++;
                    if (b[i].r >= 220 && b[i].b >= 220 && b[i].g <= 64) baselineErrorPixels++;
                }
                Assert.That(mistErrorPixels, Is.LessThanOrEqualTo(baselineErrorPixels + 128),
                    "The mist introduced magenta error-shader pixels");
                Assert.That(changed, Is.GreaterThan(200), "The mist must visibly affect the actual camera scene");
            }
            finally
            {
                for (int i = 0; i < volumes.Length; i++) if (volumes[i]) volumes[i].forceRenderingOff = saved[i];
                if (visible) Object.Destroy(visible);
                if (hidden) Object.Destroy(hidden);
            }
        }

        // Offscreen eye-camera views from inside the established physical
        // bounds; no desktop window, OS input or save data is touched.
        IEnumerator CaptureHorizonEdges(Camera eyes, Component world, string output)
        {
            var feet = new[] { new Vector3(-21, .04f, -2), new Vector3(21, .04f, -2),
                new Vector3(0, .04f, -26), new Vector3(0, .04f, 28),
                new Vector3(-21, .04f, -26), new Vector3(21, .04f, -26),
                new Vector3(-21, .04f, 28), new Vector3(21, .04f, 28) };
            var look = new[] { new Vector3(-45, 1.4f, -2), new Vector3(45, 1.4f, -2),
                new Vector3(0, 1.4f, -50), new Vector3(0, 1.4f, 54),
                new Vector3(-45, 1.4f, -50), new Vector3(45, 1.4f, -50),
                new Vector3(-45, 1.4f, 54), new Vector3(45, 1.4f, 54) };
            string[] names = { "west", "east", "south", "north", "southwest", "southeast", "northwest", "northeast" };
            var terrain = world.transform.Find("Forest horizon terrain");
            Assert.That(terrain, Is.Not.Null);
            var nearerRow = terrain.Cast<Transform>().Where(child => child.name.StartsWith("Distant forest pine 1-"))
                .Select(child => child.GetComponent<LODGroup>()).ToArray();
            Assert.That(nearerRow.Length, Is.EqualTo(16), "The review must reuse all sixteen existing second-row trees");
            Assert.That(nearerRow.All(group => group && group.lodCount == 2 && group.GetLODs()[0].renderers.Length > 0), Is.True);
            Vector3 oldPosition = player.transform.position;
            Quaternion oldRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            try
            {
                for (int i = 0; i < feet.Length; i++)
                {
                    var heading = look[i] - feet[i]; heading.y = 0;
                    Call(player, "RelocateWorld", world.transform.position + feet[i], Quaternion.LookRotation(heading));
                    eyes.transform.rotation = Quaternion.LookRotation(world.transform.position + look[i] - eyes.transform.position);
                    Physics.SyncTransforms();
                    var shadow = session.GetComponent(RequireType("LocalShadowBudget"));
                    if (shadow) Call(shadow, "RefreshNow");
                    yield return null;
                    var frame = SchoolHorrorFrame(eyes, 1280, 720);
                    try { File.WriteAllBytes(Path.Combine(output, "horizon-" + names[i] + ".png"), frame.EncodeToPNG()); }
                    finally { Object.Destroy(frame); }
                    // Same pose, same frame, and only the existing second row
                    // changes detail. This is a visual comparison, not a GPU
                    // performance measurement or an applied production change.
                    try
                    {
                        foreach (var group in nearerRow) group.ForceLOD(0);
                        var detailed = SchoolHorrorFrame(eyes, 1280, 720);
                        try { File.WriteAllBytes(Path.Combine(output, "horizon-lod1-" + names[i] + ".png"), detailed.EncodeToPNG()); }
                        finally { Object.Destroy(detailed); }
                    }
                    finally
                    {
                        foreach (var group in nearerRow) if (group) group.ForceLOD(-1);
                    }
                }
            }
            finally
            {
                foreach (var group in nearerRow) if (group) group.ForceLOD(-1);
                Call(player, "RelocateWorld", oldPosition, oldRotation);
                eyes.transform.rotation = oldEyeRotation;
                Physics.SyncTransforms();
            }
        }

        IEnumerator CaptureTempleStoryUiAspects(Component view, Camera eyes, Component world, string output, string day)
        {
            var evidence = world.GetComponent(RequireType("TempleStoryEvidence"));
            var actualInk = Get<Renderer>(evidence, "InkRenderer").sharedMaterial.GetTexture("_BaseMap");
            Assert.That(actualInk, Is.Not.Null);
            var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(1680, 720) };
            foreach (bool large in new[] { false, true })
            {
                if (Get<bool>(shell, "LargeText") != large) { Call(shell, "ToggleLargeText"); yield return null; yield return null; }
                foreach (var size in sizes)
                {
                    Call(view, "SetCaptureSize", size.x, size.y);
                    Call(shell, "ShowCaption", "[숲의 바람]", 12f, 100);
                    // A fresh target can need several rendered frames. Wait
                    // for actual UI paint, with a bounded real-time deadline.
                    bool painted = false;
                    float until = Time.realtimeSinceStartup + 8;
                    for (int frame = 0; frame < 180 && Time.realtimeSinceStartup < until; frame++)
                    {
                        yield return null;
                        var probe = CloudExperienceTests.Read(Get<RenderTexture>(view, "CaptureTarget"));
                        try { painted = CloudExperienceTests.HasContent(probe); }
                        finally { Object.Destroy(probe); }
                        if (painted && frame >= 3) break;
                    }
                    Assert.That(painted, Is.True, "UI target did not paint");
                    var root = Get<VisualElement>(view, "Root");
                    var panel = root.Q<VisualElement>("temple-narration-panel");
                    var text = root.Q<Label>("temple-narration");
                    var paper = root.Q<VisualElement>("temple-evidence-paper");
                    var image = root.Q<Image>("temple-evidence-ink");
                    var caption = root.Q<Label>("sound-caption");
                    var crosshair = root.Q<VisualElement>("crosshair");
                    var focus = root.Q<VisualElement>("focus-panel");
                    var notice = root.Q<VisualElement>("temple-story-notice-panel");
                    Assert.That(panel, Is.Not.Null); Assert.That(text, Is.Not.Null); Assert.That(paper, Is.Not.Null);
                    Assert.That(image, Is.Not.Null); Assert.That(caption, Is.Not.Null); Assert.That(crosshair, Is.Not.Null);
                    Assert.That(panel.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(paper.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(image.image, Is.SameAs(actualInk), "Reading must use the actual world paper ink");
                    Assert.That(text.text, Does.Contain(day == "first" ? "이름칸" : "집 그림"));
                    AssertStoryFontSize(text, large);
                    Assert.That(caption.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly,
                        0, VisualElement.MeasureMode.Undefined).y, Is.LessThanOrEqualTo(text.contentRect.height + 1), "Transcript text is clipped");
                    Assert.That(paper.worldBound.xMax, Is.LessThanOrEqualTo(text.worldBound.xMin + 1));
                    Assert.That(panel.worldBound.yMin, Is.GreaterThanOrEqualTo(crosshair.worldBound.yMax));
                    Assert.That(panel.worldBound.yMax, Is.LessThanOrEqualTo(caption.worldBound.yMin));
                    if (focus != null && focus.resolvedStyle.display != DisplayStyle.None)
                        Assert.That(panel.worldBound.yMax, Is.LessThanOrEqualTo(focus.worldBound.yMin));
                    if (notice != null && notice.resolvedStyle.display != DisplayStyle.None)
                        Assert.That(notice.worldBound.yMax, Is.LessThanOrEqualTo(panel.worldBound.yMin));
                    Assert.That(panel.worldBound.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1));
                    Assert.That(panel.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1));
                    Assert.That(panel.worldBound.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin - 1));
                    Assert.That(panel.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1));

                    // Actual eye camera plus UI Toolkit target. The blend follows
                    // the existing PauseMenuOverActualSchoolCamera review.
                    var background = SchoolHorrorFrame(eyes, size.x, size.y);
                    var overlay = CloudExperienceTests.Read(Get<RenderTexture>(view, "CaptureTarget"));
                    var composite = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                    try
                    {
                        Assert.That(CloudExperienceTests.HasContent(overlay), Is.True, "UI target was blank");
                        var pixels = background.GetPixels32(); var ui = overlay.GetPixels32();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            float alpha = ui[i].a / 255f, remain = 1 - alpha;
                            pixels[i] = new Color32((byte)Mathf.Clamp(ui[i].r + pixels[i].r * remain, 0, 255),
                                (byte)Mathf.Clamp(ui[i].g + pixels[i].g * remain, 0, 255),
                                (byte)Mathf.Clamp(ui[i].b + pixels[i].b * remain, 0, 255), 255);
                        }
                        composite.SetPixels32(pixels); composite.Apply();
                        string filename = day + (large ? "-large-" : "-normal-") + size.x + "x" + size.y + ".png";
                        File.WriteAllBytes(Path.Combine(output, filename), composite.EncodeToPNG());
                    }
                    finally { Object.Destroy(background); Object.Destroy(overlay); Object.Destroy(composite); }
                }
            }
        }
    }
}
