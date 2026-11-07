using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        VisualElement HudReviewRoot() => Get<VisualElement>(One("GameShellView"), "Root");

        static void AssertHudLabelFits(Label label, string description)
        {
            Assert.That(label, Is.Not.Null, description + " label is missing");
            Assert.That(label.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), description + " is hidden");
            Assert.That(label.contentRect.width, Is.GreaterThan(1), description + " has no text width");
            var measured = label.MeasureTextSize(label.text, label.contentRect.width, VisualElement.MeasureMode.Exactly,
                0, VisualElement.MeasureMode.Undefined);
            Assert.That(measured.y, Is.LessThanOrEqualTo(label.contentRect.height + 1.5f), description + " is clipped");
        }

        static void AssertOrdinaryStoryAtScreenBottom(VisualElement root, VisualElement story)
        {
            Assert.That(story.worldBound.yMax, Is.GreaterThanOrEqualTo(root.worldBound.yMin + root.worldBound.height * .92f),
                "Ordinary dialogue should sit at the bottom of the screen");
            Assert.That(story.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1),
                "Ordinary dialogue must stay within the screen");
        }

        static void AssertStoryFontSize(Label label, bool large)
        {
            Assert.That(label.resolvedStyle.fontSize, Is.InRange(large ? 26f : 19f, large ? 30f : 22f),
                "Dialogue text should be compact while retaining a larger accessible option");
        }

        void AssertHudElementsDoNotOverlap()
        {
            var root = HudReviewRoot();
            var focusPanel = root.Q<VisualElement>("focus-panel");
            var key = root.Q<Label>("interaction-key");
            var action = root.Q<Label>("interaction-prompt");
            var story = root.Q<VisualElement>("temple-narration-panel");
            var caption = root.Q<Label>("sound-caption");
            var crosshair = root.Q<VisualElement>("crosshair");
            Assert.That(focusPanel, Is.Not.Null); Assert.That(story, Is.Not.Null);
            Assert.That(caption, Is.Not.Null); Assert.That(crosshair, Is.Not.Null);
            Assert.That(focusPanel.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(story.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(caption.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            AssertHudLabelFits(key, "Interaction key"); AssertHudLabelFits(action, "Interaction text");
            Assert.That(key.worldBound.Overlaps(action.worldBound), Is.False, "Key must not cover the action");
            Assert.That(focusPanel.worldBound.Overlaps(story.worldBound), Is.False, "Interaction must not cover story text");
            Assert.That(focusPanel.worldBound.Overlaps(caption.worldBound), Is.False, "Interaction must not cover sound captions");
            Assert.That(focusPanel.worldBound.Overlaps(crosshair.worldBound), Is.False, "Interaction must not cover the reticle");
            Assert.That(story.worldBound.Overlaps(caption.worldBound), Is.False, "Story text must not cover sound captions");
            AssertOrdinaryStoryAtScreenBottom(root, story);
            Assert.That(story.worldBound.yMin, Is.GreaterThanOrEqualTo(focusPanel.worldBound.yMax - 1), "Interaction must be above ordinary dialogue");
            Assert.That(story.worldBound.yMin, Is.GreaterThanOrEqualTo(caption.worldBound.yMax - 1), "Sound captions must be above ordinary dialogue");
            AssertStoryFontSize(root.Q<Label>("temple-narration"), true);
            Assert.That(story.worldBound.Overlaps(crosshair.worldBound), Is.False, "Story text must not cover the reticle");
            Assert.That(focusPanel.worldBound.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1));
            Assert.That(focusPanel.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1));
            Assert.That(focusPanel.worldBound.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin - 1));
            Assert.That(focusPanel.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HudInteractionUsesTheActualKeyboardAndGamepadWithoutOverlappingStory()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings");
            if (!Get<bool>(shell, "LargeText")) Call(shell, "ToggleLargeText");
            if (!Get<bool>(shell, "HighContrast")) Call(shell, "ToggleHighContrast");
            yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var narration = world.GetComponent(RequireType("StoryNarration"));
            Call(narration, "Replace", "숲길 너머에서 잊고 있던 이름이 떠오를 듯하다.", 60f);
            Call(shell, "ShowCaption", "[멀리서 들리는 발소리]", 60f, 100);
            var eyes = Get<Camera>(player, "eyes").transform;
            var targetObject = new GameObject("HUD physical focus fixture");
            try
            {
                targetObject.layer = 8; targetObject.transform.SetParent(eyes, false);
                targetObject.transform.localPosition = new Vector3(0, 0, 1.25f);
                targetObject.AddComponent<BoxCollider>().size = new Vector3(.4f, .4f, .12f);
                var target = targetObject.AddComponent(RequireType("Interactable"));
                Set(target, "kind", "Inspect"); Set(target, "label", "쪽지 살펴보기");
                Set(target, "inspectionText", "HUD input fixture"); Set(target, "stableId", "hud-keyboard-fixture");
                Physics.SyncTransforms();
                yield return Wait(() => Get<Component>(player, "Focus") == target, 5, "Physical interaction focus was not acquired");
                AddAuditPad(); yield return null;
                Keys(Key.F12); yield return null; Keys(); yield return null;
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "E" &&
                    HudReviewRoot().Q<Label>("interaction-prompt")?.text == "쪽지 살펴보기", 5, "Keyboard prompt was not shown");
                yield return null; yield return null; AssertHudElementsDoNotOverlap();
                yield return KeysObserved(Key.E); Keys(); yield return null;
                yield return Wait(() => (bool)Call(session, "HasInspected", "hud-keyboard-fixture"), 5,
                    "The displayed keyboard key did not activate the focused object");
                Set(target, "label", "수첩에 남겨진 오래된 이름을 자세히 살펴보기");
                Set(target, "stableId", "hud-gamepad-fixture");
                yield return PadPulse(GamepadButton.DpadRight);
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "아래 버튼" &&
                    HudReviewRoot().Q<Label>("interaction-prompt")?.text == "수첩에 남겨진 오래된 이름을 자세히 살펴보기",
                    5, "Gamepad prompt did not replace the keyboard key");
                yield return null; yield return null; AssertHudElementsDoNotOverlap();
                yield return PadPulse(GamepadButton.South);
                yield return Wait(() => (bool)Call(session, "HasInspected", "hud-gamepad-fixture"), 5,
                    "The displayed gamepad button did not activate the focused object");
                Keys(Key.F12); yield return null; Keys(); yield return null;
                yield return Wait(() => HudReviewRoot().Q<Label>("interaction-key")?.text == "E", 5,
                    "Returning to the keyboard left the gamepad hint visible");
                Set(target, "label", "쪽지 살펴보기"); yield return null; yield return null;
                AssertHudElementsDoNotOverlap();
            }
            finally
            {
                Keys();
                if (targetObject) UnityEngine.Object.Destroy(targetObject);
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HudStoryEvidenceSurvivesAccessibleSettingsAndPause()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings");
            if (!Get<bool>(shell, "LargeText")) Call(shell, "ToggleLargeText");
            if (!Get<bool>(shell, "HighContrast")) Call(shell, "ToggleHighContrast");
            if (Get<bool>(shell, "Subtitles")) Call(shell, "ToggleSubtitles");
            yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var narration = world.GetComponent(RequireType("StoryNarration"));
            var evidence = world.GetComponent(RequireType("TempleStoryEvidence"));
            var ink = Get<Renderer>(evidence, "InkRenderer").sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.That(ink, Is.Not.Null);
            const string reading = "오늘과 내일 향을 피워 주세요.\n빈 이름칸 아래의 쪽지를 읽는다.";
            Call(narration, "ReplaceEvidence", reading, 30f, ink);
            yield return Wait(() => HudReviewRoot().Q<Label>("temple-narration")?.text == reading, 5,
                "Evidence story was not displayed with subtitles disabled");
            yield return null; yield return null;
            var root = HudReviewRoot(); var text = root.Q<Label>("temple-narration");
            var image = root.Q<Image>("temple-evidence-ink"); var paper = root.Q("temple-evidence-paper");
            Assert.That(Get<bool>(shell, "HighContrast") && Get<bool>(shell, "LargeText"), Is.True);
            Assert.That(Get<bool>(shell, "Subtitles"), Is.False);
            AssertStoryFontSize(text, true);
            AssertHudLabelFits(text, "Evidence reading");
            Assert.That(paper.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(image.image, Is.SameAs(ink), "The actual in-world ink must remain visible");
            var remaining = narration.GetType().GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(remaining, Is.Not.Null);
            Call(shell, "Pause"); yield return null;
            float pausedRemaining = (float)remaining.GetValue(narration);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That((float)remaining.GetValue(narration), Is.EqualTo(pausedRemaining).Within(.001f), "Pause must hold reading time");
            Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.SameAs(ink));
            Call(shell, "Resume"); yield return null; yield return null;
            yield return Wait(() => HudReviewRoot().Q<Label>("temple-narration")?.text == reading, 5,
                "The same reading did not return after pause");
            Assert.That(HudReviewRoot().Q<Image>("temple-evidence-ink").image, Is.SameAs(ink));
            Call(narration, "Replace", "그 아이의 이름을 떠올려야 한다.", 30f);
            yield return Wait(() => HudReviewRoot().Q<Label>("temple-narration")?.text == "그 아이의 이름을 떠올려야 한다.", 5,
                "Ordinary story did not replace the evidence reading");
            yield return null; yield return null;
            Assert.That(HudReviewRoot().Q("temple-evidence-paper").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(HudReviewRoot().Q<Image>("temple-evidence-ink").image, Is.Null);
            AssertHudLabelFits(HudReviewRoot().Q<Label>("temple-narration"), "Ordinary story");
            AssertOrdinaryStoryAtScreenBottom(root, root.Q("temple-narration-panel"));
            Call(narration, "Clear"); yield return null; yield return null;
            Assert.That(HudReviewRoot().Q("temple-narration-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
    }
}
