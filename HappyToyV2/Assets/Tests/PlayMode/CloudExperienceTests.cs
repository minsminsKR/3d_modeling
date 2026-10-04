using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Real engine outputs, embedded in UTF output so failed builds retain evidence.
    // No mock screenshots, synthetic audio replacement or target-hardware FPS claim.
    internal static class CloudExperienceTests
    {
        internal static void Artifact(string name, byte[] bytes)
        {
            Assert.That(bytes.Length, Is.InRange(1, 1500000), "Evidence exceeds bounded log envelope: " + name);
            string directory = Path.GetFullPath(Path.Combine("Temp", "HappyToyCloudEvidence"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
            string sha;
            using (var hash = SHA256.Create()) sha = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            string payload = Convert.ToBase64String(bytes);
            int chunks = (payload.Length + 4095) / 4096;
            TestContext.Out.WriteLine("HAPPYTOY_ARTIFACT_BEGIN " + name + " " + bytes.Length + " " + sha + " " + chunks);
            for (int i = 0; i < chunks; i++)
                TestContext.Out.WriteLine("HAPPYTOY_ARTIFACT_CHUNK " + name + " " + i + " " + payload.Substring(i * 4096, Math.Min(4096, payload.Length - i * 4096)));
            TestContext.Out.WriteLine("HAPPYTOY_ARTIFACT_END " + name);
        }

        internal static Texture2D Read(RenderTexture target)
        {
            Assert.That(target && target.IsCreated(), Is.True, "Engine render target is not available");
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture;
            }
            catch { Object.Destroy(texture); throw; }
            finally { RenderTexture.active = previous; }
        }
        internal static bool HasContent(Texture2D texture)
        {
            float min = 1, max = 0;
            int visible = 0;
            for (int y = 0; y < texture.height; y += 9)
                for (int x = 0; x < texture.width; x += 9)
                {
                    var color = texture.GetPixel(x, y);
                    float value = Mathf.Max(color.r, color.g, color.b);
                    min = Mathf.Min(min, value); max = Mathf.Max(max, value);
                    if (value > .08f) visible++;
                }
            return max - min > .06f && visible > 30;
        }
        internal static void SaveFrame(string name, RenderTexture target)
        {
            var frame = Read(target);
            try
            {
                Artifact(name, frame.EncodeToPNG()); // Save even if the content assertion fails.
                Assert.That(HasContent(frame), Is.True, "Real rendered frame is blank/flat: " + name);
            }
            finally { Object.Destroy(frame); }
        }
        internal static string[] Layout(VisualElement root)
        {
            Rect bounds = root.worldBound;
            Assert.That(bounds.width, Is.GreaterThan(0)); Assert.That(bounds.height, Is.GreaterThan(0));
            var failures = new List<string>();
            Action<VisualElement> check = element =>
            {
                if (element.resolvedStyle.display == DisplayStyle.None || !element.visible) return;
                var rect = element.worldBound;
                if (float.IsNaN(rect.x) || float.IsNaN(rect.y) || rect.xMin < bounds.xMin - 1 || rect.yMin < bounds.yMin - 1 ||
                    rect.xMax > bounds.xMax + 1 || rect.yMax > bounds.yMax + 1)
                    failures.Add(element.name + ": " + rect + " outside " + bounds);
                if (element is TextElement label && !string.IsNullOrWhiteSpace(label.text))
                {
                    var size = label.MeasureTextSize(label.text, label.contentRect.width, VisualElement.MeasureMode.Exactly,
                        0, VisualElement.MeasureMode.Undefined);
                    if (size.y > label.contentRect.height + 1)
                        failures.Add("Text clipped: " + label.text + " requires " + size.y + " but has " + label.contentRect.height);
                    if (label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap)
                    {
                        var unwrapped = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                        if (unwrapped.x > label.contentRect.width + 1)
                            failures.Add("Unwrapped text clipped: " + label.text + " requires " + unwrapped.x + " but has " + label.contentRect.width);
                    }
                }
            };
            root.Query<Button>().ForEach(button => check(button));
            root.Query<Label>().ForEach(label => check(label));
            return failures.ToArray();
        }
        internal static byte[] Wave(List<float> values, int rate, int channels)
        {
            using (var stream = new MemoryStream())
            using (var file = new BinaryWriter(stream))
            {
                file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + values.Count * 2);
                file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16); file.Write((short)1); file.Write((short)channels);
                file.Write(rate); file.Write(rate * channels * 2); file.Write((short)(channels * 2)); file.Write((short)16);
                file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(values.Count * 2);
                foreach (float value in values) file.Write((short)(Mathf.Clamp(value, -1, 1) * 32767));
                return stream.ToArray();
            }
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest]
        public IEnumerator KoreanMenusRenderAcrossSupportedAspectRatios()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "UBA graphics device is required for rendered evidence");
            var view = One("GameShellView");
            var layoutErrors = new List<string>();
            Call(shell, "RestoreDefaultSettings");
            yield return CaptureMenu(view, "title-720p.png", 1280, 720, layoutErrors);
            Call(shell, "Settings"); Call(shell, "ToggleLargeText"); Call(shell, "ToggleHighContrast");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                yield return CaptureMenu(view, "settings-large-" + size.x + "x" + size.y + ".png", size.x, size.y, layoutErrors);
            Call(shell, "Back");
            yield return CaptureMenu(view, "title-large-720p.png", 1280, 720, layoutErrors);
            Begin(); Call(shell, "Pause");
            yield return CaptureMenu(view, "pause-large-4x3.png", 1024, 768, layoutErrors);
            Call(shell, "Resume");
            yield return CaptureMenu(view, "hud-large-720p.png", 1280, 720, layoutErrors);
            Call(shell, "Journal");
            yield return CaptureMenu(view, "journal-1080p.png", 1920, 1080, layoutErrors);
            Call(shell, "Back");
            Assert.That(layoutErrors, Is.Empty, string.Join("\n", layoutErrors));
        }

        [UnityTest]
        public IEnumerator RealAuthoredCameraProducesRenderAndTimingEvidence()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "UBA graphics device is required for rendered evidence");
            Begin();
            var camera = Get<Camera>(player, "eyes");
            var target = new RenderTexture(960, 540, 24);
            target.Create();
            var samples = new List<double>();
            try
            {
                // Preserved starting position, actual camera and authored renderer/materials.
                for (int i = 0; i < 6; i++)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var frame = CloudExperienceTests.Read(target); // Synchronous readback included explicitly.
                    timer.Stop();
                    try
                    {
                        if (i == 0) CloudExperienceTests.Artifact("authored-start-camera.png", frame.EncodeToPNG());
                        Assert.That(CloudExperienceTests.HasContent(frame), Is.True, "Authored starting camera rendered blank");
                    }
                    finally { Object.Destroy(frame); }
                    samples.Add(timer.Elapsed.TotalMilliseconds);
                    yield return null;
                }
                TestContext.Out.WriteLine("HAPPYTOY_RENDER_METRICS device=" + SystemInfo.graphicsDeviceName +
                    "; api=" + SystemInfo.graphicsDeviceType + "; cpu=" + SystemInfo.processorType +
                    "; resolution=960x540; offscreenRenderAndFullReadbackMs=" + string.Join(",", samples.Select(value => value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))) +
                    "; firstSampleIncludesWarmup=true; presentedFps=false; targetHardwareCertification=false; managedBytes=" + GC.GetTotalMemory(false));
                // Preserve the real diagnostic image even if a sign contract fails.
                CloudSignRenderingTests.AssertPhysicalWorldSigns();
            }
            finally { target.Release(); Object.Destroy(target); }
        }
        IEnumerator CaptureMenu(Component view, string name, int width, int height, List<string> layoutErrors)
        {
            Call(view, "SetCaptureSize", width, height);
            // Prove a new paint, not the old page or undefined new-target memory.
            var oldTarget = RenderTexture.active;
            try
            {
                RenderTexture.active = Get<RenderTexture>(view, "CaptureTarget");
                GL.Clear(true, true, Color.clear);
            }
            finally { RenderTexture.active = oldTarget; }
            // WaitForEndOfFrame is unsupported in batch Editor; use bounded rendered-frame polling.
            bool visible = false;
            float until = Time.realtimeSinceStartup + 8;
            for (int frame = 0; frame < 180 && Time.realtimeSinceStartup < until; frame++)
            {
                yield return null;
                var image = CloudExperienceTests.Read(Get<RenderTexture>(view, "CaptureTarget"));
                try { visible = CloudExperienceTests.HasContent(image); }
                finally { Object.Destroy(image); }
                if (visible && frame >= 3) break;
            }
            CloudExperienceTests.SaveFrame(name, Get<RenderTexture>(view, "CaptureTarget"));
            Assert.That(visible, Is.True, "UI Toolkit failed to render in this cloud graphics environment");
            layoutErrors.AddRange(CloudExperienceTests.Layout(Get<VisualElement>(view, "Root")).Select(error => name + ": " + error));
            var font = Get<Font>(shell, "ShellFont");
            Assert.That(font, Is.Not.Null, "Korean font must be loaded");
            font.RequestCharactersInTexture("마지막출석환경설정조사기록낮은자세손전등", 24);
            foreach (char c in "마지막출석환경설정조사기록낮은자세손전등")
                Assert.That(font.HasCharacter(c), Is.True, "Missing Korean glyph " + c);
        }

        [UnityTest]
        public IEnumerator RealListenerMixCapturesDuringFlashlightInputAndPauseSilence()
        {
            Call(shell, "RestoreDefaultSettings"); Begin();
            yield return null;
            Assert.That(AudioSettings.speakerMode, Is.EqualTo(AudioSpeakerMode.Stereo), "Capture channel contract requires the configured stereo mix");
            int rate = AudioSettings.outputSampleRate;
            Assert.That(rate, Is.InRange(8000, 192000));
            float oldCaptureDeltaTime = Time.captureDeltaTime;
            bool started = false;
            var active = new List<float>(); var paused = new List<float>();
            try
            {
                // Follow Unity Recorder's constant-rate capture path before starting
                // AudioRenderer. This isolated DSP fixture tests that supported path;
                // it does not change the survival route or claim realtime performance.
                Time.captureDeltaTime = 1f / 60f;
                yield return null;
                started = AudioRenderer.Start();
                Assert.That(started, Is.True, "Cloud worker cannot start Unity's actual main-output audio recorder");
                var flashlight = Get<Light>(player, "flashlight"); bool wasOn = flashlight.enabled;
                Keys(Key.F);
                yield return CaptureAudio(active, rate * 2, 8);
                Keys();
                CloudExperienceTests.Artifact("listener-flashlight-ambience.wav", CloudExperienceTests.Wave(active, rate, 2));
                Assert.That(flashlight.enabled, Is.EqualTo(!wasOn), "F key did not toggle real flashlight");
                Call(shell, "Pause");
                // Flush one mixer buffer before observing pause silence.
                var flush = new List<float>(); yield return CaptureAudio(flush, rate / 5 * 2, 4);
                yield return CaptureAudio(paused, rate / 4 * 2, 4);
                CloudExperienceTests.Artifact("listener-paused.wav", CloudExperienceTests.Wave(paused, rate, 2));
                double peak = active.Max(value => Math.Abs(value));
                double rms = Math.Sqrt(active.Average(value => (double)value * value));
                double pausePeak = paused.Max(value => Math.Abs(value));
                int clipped = active.Count(value => Math.Abs(value) >= 1);
                TestContext.Out.WriteLine("HAPPYTOY_AUDIO_METRICS capture=UnityAudioRendererMainOutput; rate=" + rate +
                    "; channels=2; fixedCaptureRate=60; activeSamples=" + active.Count + "; peak=" + peak + "; rms=" + rms +
                    "; clippedSamples=" + clipped + "; pausePeak=" + pausePeak + "; deviceListeningCertification=false");
                Assert.That(peak, Is.GreaterThan(.0001), "Real engine audio mix was silent");
                Assert.That(clipped, Is.Zero, "Actual mixed sample clips");
                Assert.That(pausePeak, Is.LessThan(.00001), "Paused game leaks audible mixer output");
            }
            finally
            {
                try { Keys(); }
                finally
                {
                    try { if (started) AudioRenderer.Stop(); }
                    finally { Time.captureDeltaTime = oldCaptureDeltaTime; }
                }
            }
        }
        static IEnumerator CaptureAudio(List<float> samples, int target, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (samples.Count < target && Time.realtimeSinceStartup < until)
            {
                yield return null;
                int count = AudioRenderer.GetSampleCountForCaptureFrame();
                Assert.That(count, Is.InRange(0, AudioSettings.outputSampleRate * 2), "Unbounded capture-frame sample count");
                if (count == 0) continue;
                using (var buffer = new NativeArray<float>(count * 2, Allocator.Temp))
                {
                    Assert.That(AudioRenderer.Render(buffer), Is.True, "Actual Unity audio rendering failed");
                    for (int i = 0; i < buffer.Length && samples.Count < target; i++)
                    {
                        float value = buffer[i];
                        if (float.IsNaN(value) || float.IsInfinity(value)) Assert.Fail("Nonfinite audio sample");
                        samples.Add(value);
                    }
                }
            }
            Assert.That(samples.Count, Is.EqualTo(target), "No complete actual audio capture before cloud watchdog; " +
                "captureDeltaTime=" + Time.captureDeltaTime + ", captureFramerate=" + Time.captureFramerate +
                ", deltaTime=" + Time.deltaTime + ", unscaledDeltaTime=" + Time.unscaledDeltaTime +
                ", dspTime=" + AudioSettings.dspTime + ", listenerPause=" + AudioListener.pause +
                ", timeScale=" + Time.timeScale + ", driverCapabilities=" + AudioSettings.driverCapabilities);
        }
    }
}
