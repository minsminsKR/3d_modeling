using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Explicit opt-in uses the existing isolated story UI profile. Captures
        // are composited offscreen and all input devices are virtual test devices.
        [UnityTest, Timeout(480000)]
        public IEnumerator TemplePhysicalFocusHudOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-story-ui-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-v2-audit-no-desktop-interaction") && args.Contains("-noaudio"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string rootDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string storyOutput = Path.GetFullPath(args[option + 1]);
            Assert.That(storyOutput.StartsWith(rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            string output = Path.Combine(Path.GetDirectoryName(storyOutput), "hud-physical-focus");
            Assert.That(Directory.Exists(output), Is.False, "A fresh output is required");
            Directory.CreateDirectory(output);
            TestContext.Out.WriteLine("Physical HUD review output: " + output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var note = world.transform.Find("이름이 비어 있는 쪽지");
            Assert.That(note, Is.Not.Null);
            var narration = world.GetComponent(RequireType("StoryNarration"));
            var evidence = world.GetComponent(RequireType("TempleStoryEvidence"));
            var actualInk = Get<Renderer>(evidence, "InkRenderer").sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.That(actualInk, Is.Not.Null);
            var view = One("GameShellView");
            PlacePlayer(new Vector3(500.68f, .04f, 505.8f)); yield return null;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // Use the real note collider, production raycast and virtual
                // Input System mouse, not a fabricated focus or an OS event.
                yield return TempleAimAt(note, mouse, false);
                Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(note.name));
                Keys(Key.F12); yield return null; Keys(); yield return null;
                const string line = "얼굴은 떠오르는데 이름만 기억나지 않는다.";
                Call(narration, "Replace", line, 120f);
                yield return Wait(() => Get<string>(narration, "CurrentText") == line, 5, "Ordinary narration never started");
                ((Behaviour)narration).enabled = false;
                yield return CapturePhysicalHudVariants(view, world, output, "ordinary-keyboard", "E", false, actualInk);
                AddAuditPad(); yield return PadPulse(GamepadButton.DpadRight);
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "아래 버튼", 5, "Gamepad hint did not appear");
                Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(note.name), "Changing devices lost physical note focus");
                yield return CapturePhysicalHudVariants(view, world, output, "ordinary-gamepad", "아래 버튼", false, actualInk);

                // The actual E interaction opens the real note; unfreeze the
                // sequence for that update and freeze only after it has started.
                ((Behaviour)narration).enabled = true;
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<Texture2D>(narration, "CurrentInk") != null, 5, "Real note interaction did not open evidence");
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.SameAs(actualInk));
                ((Behaviour)narration).enabled = false;
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "E", 5, "Keyboard hint did not return");
                Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(note.name));
                yield return CapturePhysicalHudVariants(view, world, output, "evidence-keyboard", "E", true, actualInk);
                yield return PadPulse(GamepadButton.DpadRight);
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "아래 버튼", 5, "Gamepad evidence hint did not appear");
                Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(note.name));
                yield return CapturePhysicalHudVariants(view, world, output, "evidence-gamepad", "아래 버튼", true, actualInk);
            }
            finally
            {
                if (narration) ((Behaviour)narration).enabled = true;
                if (view) Call(view, "RestoreDisplayOutput");
                Keys();
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                Call(shell, "RestoreDefaultSettings");
            }
        }

        IEnumerator CapturePhysicalHudVariants(Component view, Component world, string output,
            string context, string expectedKey, bool evidence, Texture2D actualInk)
        {
            var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(1680, 720) };
            var eyes = Get<Camera>(player, "eyes");
            foreach (bool large in new[] { false, true })
            foreach (bool contrast in new[] { false, true })
            {
                if (Get<bool>(shell, "LargeText") != large) Call(shell, "ToggleLargeText");
                if (Get<bool>(shell, "HighContrast") != contrast) Call(shell, "ToggleHighContrast");
                yield return null; yield return null; yield return null;
                foreach (var size in sizes)
                {
                    Call(view, "SetCaptureSize", size.x, size.y);
                    Call(shell, "ShowCaption", "[숲에서 들리는 발소리]", 60f, 100);
                    bool painted = false; float deadline = Time.realtimeSinceStartup + 8;
                    for (int frame = 0; frame < 180 && Time.realtimeSinceStartup < deadline; frame++)
                    {
                        yield return null;
                        var probe = CloudExperienceTests.Read(Get<RenderTexture>(view, "CaptureTarget"));
                        try { painted = CloudExperienceTests.HasContent(probe); }
                        finally { Object.Destroy(probe); }
                        if (painted && frame >= 3) break;
                    }
                    Assert.That(painted, Is.True, "UI render target did not paint");
                    Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("이름이 비어 있는 쪽지"), "The actual note focus was lost");
                    var root = Get<VisualElement>(view, "Root");
                    var key = root.Q<Label>("interaction-key");
                    var action = root.Q<Label>("interaction-prompt");
                    var focus = root.Q<VisualElement>("focus-panel");
                    var story = root.Q<VisualElement>("temple-narration-panel");
                    var storyText = root.Q<Label>("temple-narration");
                    var caption = root.Q<Label>("sound-caption");
                    var crosshair = root.Q<VisualElement>("crosshair");
                    var paper = root.Q<VisualElement>("temple-evidence-paper");
                    var image = root.Q<Image>("temple-evidence-ink");
                    Assert.That(key, Is.Not.Null); Assert.That(action, Is.Not.Null); Assert.That(focus, Is.Not.Null);
                    Assert.That(story, Is.Not.Null); Assert.That(storyText, Is.Not.Null); Assert.That(caption, Is.Not.Null);
                    Assert.That(crosshair, Is.Not.Null); Assert.That(paper, Is.Not.Null); Assert.That(image, Is.Not.Null);
                    Assert.That(focus.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(story.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(caption.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
                    Assert.That(key.text, Is.EqualTo(expectedKey)); Assert.That(action.text, Is.EqualTo("쪽지 살펴보기"));
                    Assert.That(paper.resolvedStyle.display == DisplayStyle.None, Is.EqualTo(!evidence));
                    if (evidence) Assert.That(image.image, Is.SameAs(actualInk));
                    else Assert.That(image.image, Is.Null);
                    foreach (var text in new[] { key, action, storyText, caption })
                    {
                        Assert.That(text.contentRect.width, Is.GreaterThan(1), text.name + " has no width");
                        var measured = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly,
                            0, VisualElement.MeasureMode.Undefined);
                        Assert.That(measured.y, Is.LessThanOrEqualTo(text.contentRect.height + 1.5f), text.name + " is clipped");
                    }
                    Assert.That(key.worldBound.Overlaps(action.worldBound), Is.False, "Key overlaps action");
                    Assert.That(focus.worldBound.Overlaps(story.worldBound), Is.False, "Focus overlaps story");
                    Assert.That(focus.worldBound.Overlaps(caption.worldBound), Is.False, "Focus overlaps caption");
                    Assert.That(story.worldBound.Overlaps(caption.worldBound), Is.False, "Story overlaps caption");
                    Assert.That(story.worldBound.Overlaps(crosshair.worldBound), Is.False, "Story overlaps reticle");
                    Assert.That(focus.worldBound.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1));
                    Assert.That(focus.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1));
                    Assert.That(focus.worldBound.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin - 1));
                    Assert.That(focus.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1));
                    var errors = CloudExperienceTests.Layout(root);
                    Assert.That(errors, Is.Empty, string.Join("\n", errors));

                    var background = SchoolHorrorFrame(eyes, size.x, size.y);
                    var overlay = CloudExperienceTests.Read(Get<RenderTexture>(view, "CaptureTarget"));
                    var composite = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                    try
                    {
                        var pixels = background.GetPixels32(); var ui = overlay.GetPixels32();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            float a = ui[i].a / 255f, inverse = 1 - a;
                            pixels[i] = new Color32((byte)Mathf.Clamp(ui[i].r + pixels[i].r * inverse, 0, 255),
                                (byte)Mathf.Clamp(ui[i].g + pixels[i].g * inverse, 0, 255),
                                (byte)Mathf.Clamp(ui[i].b + pixels[i].b * inverse, 0, 255), 255);
                        }
                        composite.SetPixels32(pixels); composite.Apply();
                        string name = context + (large ? "-large" : "-normal") + (contrast ? "-contrast-" : "-standard-") + size.x + "x" + size.y + ".png";
                        File.WriteAllBytes(Path.Combine(output, name), composite.EncodeToPNG());
                    }
                    finally { Object.Destroy(background); Object.Destroy(overlay); Object.Destroy(composite); }
                }
            }
        }
    }
}
