using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator FpsSettingUsesRealControlsPersistsAcrossReloadAndShowsOnlyDuringGameplay()
        {
            Call(shell, "RestoreDefaultSettings");
            Assert.That(Get<bool>(shell, "ShowFps"), Is.False);
            yield return RecoveryUiReady();
            yield return RecoveryClick("settings");
            yield return RecoveryClick("show-fps");
            Assert.That(Get<bool>(shell, "ShowFps"), Is.True);
            var view = One("GameShellView");
            Assert.That(Get<VisualElement>(view, "Root").Q<Button>("show-fps").text, Does.Contain("켜짐"));
            yield return RecoveryClick("back");
            Assert.That(PlayerPrefs.GetInt("v2.showFps", -1), Is.EqualTo(1));
            Begin(); IsolateThreats(); yield return null;
            yield return Delay(.7f);
            var root = Get<VisualElement>(view, "Root");
            Assert.That(root.Q<Label>("fps-counter").text, Does.Match(@"^FPS \d+$"));
            Assert.That(root.Q<Label>("fps-counter").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            float scale = Time.timeScale;
            try
            {
                // Item-wheel slow time and supernatural time stop must not scale the FPS counter.
                foreach (float slowed in new[] { .25f, 0f })
                {
                    Time.timeScale = slowed;
                    yield return Delay(.65f);
                    Assert.That(root.Q<Label>("fps-counter").text, Does.Match(@"^FPS \d+$"));
                }
            }
            finally { Time.timeScale = scale; }
            Call(shell, "Pause"); yield return null;
            Assert.That(root.Q<Label>("fps-counter"), Is.Null, "Pause/menu pages must not retain a gameplay counter");
            var previous = session;
            yield return RecoveryClick("title"); yield return RecoveryRebind(previous);
            Assert.That(Get<bool>(shell, "ShowFps"), Is.True, "New shell did not load the saved preference");
            yield return RecoveryUiReady();
            yield return RecoveryClick("begin-school"); IsolateThreats();
            yield return Delay(.7f);
            view = One("GameShellView"); root = Get<VisualElement>(view, "Root");
            Assert.That(Get<bool>(session, "ChapterMode"), Is.True);
            Assert.That(root.Query<Label>("fps-counter").ToList().Count, Is.EqualTo(1));
            Assert.That(root.Q<Label>("fps-counter").text, Does.Match(@"^FPS \d+$"));
            Call(shell, "Pause"); yield return null;
            yield return RecoveryClick("settings"); yield return RecoveryClick("show-fps");
            yield return RecoveryClick("back"); yield return RecoveryClick("resume");
            root = Get<VisualElement>(view, "Root");
            Assert.That(root.Q<Label>("fps-counter").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(PlayerPrefs.GetInt("v2.showFps", -1), Is.Zero);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FpsCounterStaysAboveMemoryIconsAndSettingsFitAllSupportedSizes()
        {
            Call(shell, "RestoreDefaultSettings"); Call(shell, "ToggleFps");
            IsolateThreats(); Begin();
            var view = One("GameShellView"); var errors = new List<string>();
            foreach (bool large in new[] { false, true })
            {
                if (large) { Call(shell, "ToggleLargeText"); Call(shell, "ToggleHighContrast"); }
                foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080) })
                {
                    yield return CaptureMenu(view, $"fps-hud-{large}-{size.x}.png", size.x, size.y, errors);
                    var root = Get<VisualElement>(view, "Root"); var fps = root.Q<Label>("fps-counter");
                    var memory = root.Q("memory-progress");
                    Assert.That(fps.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(fps.pickingMode, Is.EqualTo(PickingMode.Ignore));
                    Assert.That(fps.worldBound.xMax, Is.LessThan(root.worldBound.center.x));
                    Assert.That(fps.worldBound.yMax, Is.LessThanOrEqualTo(memory.worldBound.yMin));
                    Call(shell, "Pause"); Call(shell, "Settings");
                    yield return CaptureMenu(view, $"fps-settings-{large}-{size.x}.png", size.x, size.y, errors);
                    root = Get<VisualElement>(view, "Root");
                    var toggle = root.Q<Button>("show-fps");
                    var motion = root.Q<Button>("reduced-motion");
                    Assert.That(toggle.text, Does.Contain("켜짐"));
                    Assert.That(toggle.worldBound.yMax, Is.LessThan(motion.worldBound.yMin));
                    Call(shell, "Back"); Call(shell, "Resume"); yield return null;
                }
            }
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Call(shell, "Pause"); Call(shell, "Settings");
            yield return null; yield return RecoveryClick("defaults");
            Assert.That(Get<bool>(shell, "ShowFps"), Is.False);
            yield return RecoveryClick("back"); yield return RecoveryClick("resume");
            Assert.That(Get<VisualElement>(view, "Root").Q<Label>("fps-counter").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
    }
}
