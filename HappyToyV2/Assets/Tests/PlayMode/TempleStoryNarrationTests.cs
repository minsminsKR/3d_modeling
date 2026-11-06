using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest]
        public IEnumerator NarrativeLinesWaitForSimulationAndShowInOrder()
        {
            var go = new GameObject("Narration timing test");
            var narration = (Behaviour)go.AddComponent(RequireType("StoryNarration"));
            narration.enabled = false; // Advance deterministic simulation intervals below.
            bool held = true;
            Call(narration, "Prepare", (Func<bool>)(() => held));
            var advance = narration.GetType().GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(advance, Is.Not.Null);
            void Step(float dt) => advance.Invoke(narration, new object[] { dt });
            Call(narration, "Enqueue", "  첫 번째 문장  ", .5f);
            Call(narration, "Enqueue", "두 번째 문장", .25f);
            Step(2); Assert.That(Get<string>(narration, "CurrentText"), Is.Empty, "A held transition must not start a line");
            held = false; Step(2); Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("첫 번째 문장"), "A slow frame must not consume the new line");
            Step(.2f); held = true; Step(10); Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("첫 번째 문장"));
            held = false; Step(.31f); Assert.That(Get<string>(narration, "CurrentText"), Is.Empty);
            Step(.02f); Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("두 번째 문장"));
            Step(.26f); Assert.That(Get<string>(narration, "CurrentText"), Is.Empty);
            Step(1); Assert.That(Get<string>(narration, "CurrentText"), Is.Empty);
            Call(narration, "Enqueue", "오래된 안내", 1f); Step(0);
            Call(narration, "Enqueue", "오래된 다음 안내", 1f);
            held = true; Call(narration, "Replace", "쪽지", .5f);
            Call(narration, "Replace", "쪽지", .5f); Step(5);
            Assert.That(Get<string>(narration, "CurrentText"), Is.Empty, "A replacement must wait while held");
            held = false; Step(0); Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("쪽지"));
            Assert.Throws<ArgumentException>(() => Call(narration, "Replace", " ", .5f));
            Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("쪽지"), "Invalid replacement must keep the current line");
            Step(.6f); Step(0); Assert.That(Get<string>(narration, "CurrentText"), Is.Empty, "Repeated rereads must not stack");
            Call(narration, "Enqueue", "버릴 안내", 1f); Call(narration, "Clear"); Step(0);
            Assert.That(Get<string>(narration, "CurrentText"), Is.Empty);
            var ink = new Texture2D(2, 2);
            try
            {
                held = true;
                Call(narration, "ReplaceEvidence", "쪽지 판독문", .5f, ink);
                Call(narration, "Enqueue", "일반 대사", .5f);
                Step(4); Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.Null);
                held = false; Step(0);
                Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("쪽지 판독문"));
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.SameAs(ink));
                held = true; Step(10);
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.SameAs(ink), "Pause must retain the active evidence");
                Assert.Throws<ArgumentNullException>(() => Call(narration, "ReplaceEvidence", "빈 쪽지", 1f, null));
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.SameAs(ink), "Invalid evidence must keep the active line");
                held = false; Step(.6f);
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.Null, "Expired evidence must release its borrowed reference");
                Step(0); Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo("일반 대사"));
                Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.Null, "Ordinary narration must not inherit the previous ink");
                Call(narration, "ReplaceEvidence", "다시 보기", 1f, ink); Step(0);
                Call(narration, "Clear"); Assert.That(Get<Texture2D>(narration, "CurrentInk"), Is.Null);
                Assert.That(ink.width, Is.EqualTo(2), "Clearing narration must not destroy the evidence owner's texture");
            }
            finally { UnityEngine.Object.Destroy(ink); }
            UnityEngine.Object.Destroy(go); yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleNarrationRemainsReadableWithSoundCaptionsOffAndLargeText()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            if (Get<bool>(shell, "Subtitles")) Call(shell, "ToggleSubtitles");
            if (!Get<bool>(shell, "LargeText")) Call(shell, "ToggleLargeText");
            yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var narration = world.GetComponent(RequireType("StoryNarration"));
            Assert.That(narration, Is.Not.Null);
            const string line = "어젯밤에 받은 쪽지의 이름칸은 비어 있었다.\n누군가를 잊어버린 걸까?";
            var evidence = world.GetComponent(RequireType("TempleStoryEvidence"));
            var inkRenderer = Get<Renderer>(evidence, "InkRenderer");
            var ink = inkRenderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.That(ink, Is.Not.Null);
            Call(narration, "ReplaceEvidence", line, 15f, ink);
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            yield return Wait(() => root.Q<Label>("temple-narration")?.text == line, 5, "Story text was not displayed");
            yield return null; yield return null;
            var label = root.Q<Label>("temple-narration");
            var panel = root.Q("temple-narration-panel");
            var notice = root.Q("temple-story-notice-panel");
            var paper = root.Q("temple-evidence-paper");
            var inkImage = root.Q<Image>("temple-evidence-ink");
            var heading = root.Q<Label>("temple-evidence-heading");
            Assert.That(paper, Is.Not.Null); Assert.That(inkImage, Is.Not.Null); Assert.That(heading, Is.Not.Null);
            Assert.That(paper.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(inkImage.image, Is.SameAs(ink), "The reading image must use the actual world ink texture");
            Assert.That(heading.text, Is.EqualTo("쪽지 판독"));
            Assert.That(heading.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(Get<bool>(shell, "Subtitles"), Is.False);
            Assert.That(panel.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(27));
            Assert.That(label.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.Normal));
            Assert.That(panel.worldBound.yMin, Is.GreaterThan(notice.worldBound.yMax));
            Assert.That(panel.worldBound.yMax, Is.LessThan(panel.parent.worldBound.yMin + panel.parent.worldBound.height * (688f / 900f)), "Story panel must stay above the sound-caption position");
            Assert.That(label.worldBound.height, Is.LessThanOrEqualTo(panel.worldBound.height));
            Assert.That(paper.worldBound.xMax, Is.LessThanOrEqualTo(label.worldBound.xMin), "Ink must not cover the transcript");
            Assert.That(paper.worldBound.yMin, Is.GreaterThanOrEqualTo(panel.worldBound.yMin));
            Assert.That(paper.worldBound.yMax, Is.LessThanOrEqualTo(panel.worldBound.yMax));
            var reticle = root.Q("crosshair");
            Assert.That(panel.worldBound.yMin, Is.GreaterThan(reticle.worldBound.yMax), "Evidence must not cover the aiming reticle");
            Call(narration, "Replace", "일반 대사", 15f);
            yield return Wait(() => label.text == "일반 대사", 5, "Normal narration was not displayed");
            yield return null; yield return null;
            Assert.That(paper.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(heading.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(inkImage.image, Is.Null);
            Call(narration, "Clear"); yield return null; yield return null;
            Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void NarrativeRejectsInvalidOrUnpreparedRequests()
        {
            var go = new GameObject("Narration validation test");
            try
            {
                var narration = (Behaviour)go.AddComponent(RequireType("StoryNarration"));
                Assert.Throws<InvalidOperationException>(() => Call(narration, "Enqueue", "text", 1f));
                Assert.Throws<ArgumentNullException>(() => Call(narration, "Prepare", (object)null));
                Call(narration, "Prepare", (Func<bool>)(() => false));
                Assert.Throws<InvalidOperationException>(() => Call(narration, "Prepare", (Func<bool>)(() => false)));
                Assert.Throws<ArgumentException>(() => Call(narration, "Enqueue", " ", 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => Call(narration, "Enqueue", "text", 0f));
                Assert.Throws<ArgumentOutOfRangeException>(() => Call(narration, "Enqueue", "text", float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => Call(narration, "Enqueue", "text", float.PositiveInfinity));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
