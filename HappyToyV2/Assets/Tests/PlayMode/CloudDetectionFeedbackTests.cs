using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public static class CloudDetectionFeedbackTests { }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(40000)]
        public IEnumerator ActualRecognitionStaticIsOcclusionBoundedAndPauseSafe()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            Vector3 origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 6, false);
            Get<Light>(player, "flashlight").enabled = true;
            var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var detection = player.GetComponent(RequireType("DetectionFeedback"));
            Assert.That(detection, Is.Not.Null);
            var cover = Cube("CloudQA recognition cover", origin + Vector3.right * 3 + Vector3.up * 1.5f, new Vector3(.25f, 3, 5));
            Physics.SyncTransforms(); yield return Delay(.5f);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.Zero, "A hidden enemy leaked a recognition effect through a wall");
            // Low cover blocks a waist-high ray but not the brain's actual eye ray.
            // A genuine recognition must not be discarded by a different guard.
            cover.transform.position = origin + Vector3.right * 3 + Vector3.up * .7f;
            cover.transform.localScale = new Vector3(.25f, 1.4f, 5); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True);
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "Real LOS did not acquire player");
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<bool>(detection, "Active"), Is.True);
            var texture = Get<Texture2D>(detection, "GrainTexture"); Assert.That(texture, Is.Not.Null);
            var source = (AudioSource)detection.GetType().GetField("source", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(detection);
            var clip = (AudioClip)detection.GetType().GetField("sting", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(detection);
            Assert.That(source.ignoreListenerPause, Is.False); Assert.That(source.ignoreListenerVolume, Is.False);
            var samples = new float[clip.samples]; Assert.That(clip.GetData(samples, 0), Is.True);
            Assert.That(samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Mathf.Abs(v) <= .58f), Is.True);
            Assert.That(samples.Any(v => Mathf.Abs(v) > .05f), Is.True);
            // UI-only evidence of the real running overlay. Cabinet/room tests use
            // actual world cameras separately; this is not a synthetic world view.
            var errors = new List<string>(); var view = One("GameShellView");
            yield return Wait(() => Get<float>(detection, "Strength") > .5f, 1, "Recognition edge never became visible");
            yield return CaptureFrozenHud(view, "recognition-static-ui.png", errors);
            float remaining = Get<float>(detection, "Remaining");
            Call(shell, "Pause");
            Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            yield return Delay(.3f);
            Assert.That(Get<float>(detection, "Remaining"), Is.EqualTo(remaining));
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<bool>(detection, "Active"), Is.False);
            Call(shell, "Resume");
            for (int i = 0; i < 5; i++) Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(1), "Concurrent observers stacked the sting");
            yield return new WaitForSeconds(.9f);
            Assert.That(Get<bool>(detection, "Active"), Is.False, "Static obscured play after its brief window");
            yield return new WaitForSeconds(3.2f);
            cover.transform.position = origin + Vector3.right * 3 + Vector3.up * 1.5f;
            cover.transform.localScale = new Vector3(.25f, 3, 5); cover.SetActive(true); Physics.SyncTransforms();
            Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(1), "Explicit stale recognition bypassed wall guard");
            cover.SetActive(false); PlacePlayer(origin + Vector3.right * 6 + Vector3.up * 5, false);
            Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(1), "Another floor leaked a cue");
            PlacePlayer(origin + Vector3.right * 6, false); Physics.SyncTransforms();
            Call(shell, "ToggleReducedMotion");
            // Controlled event injection isolates the comfort option after the
            // first recognition above came from actual brain/LOS integration.
            Call(RequireType("DetectionFeedback"), "Signal", session, enemy.transform);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.EqualTo(2));
            Assert.That(Get<bool>(detection, "Softened"), Is.True);
            Assert.That(Get<Texture2D>(detection, "GrainTexture"), Is.Null, "Reduced motion still animated grain");
            yield return Wait(() => Get<float>(detection, "Strength") > .5f, 1, "Softened recognition edge never became visible");
            yield return CaptureFrozenHud(view, "recognition-softened-ui.png", errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            var previous = session; Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20, "Recognition retry failed");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            Assert.That(detection == null && source == null && clip == null && texture == null, Is.True, "Old sting/texture leaked on retry");
            Assert.That(Get<int>(player.GetComponent(RequireType("DetectionFeedback")), "CuesPlayed"), Is.Zero);
            Debug.Log("HAPPYTOY_PRESENTATION_PASS recognition: actual sight event, no wall/floor oracle, brief static, no stacked stings, pause/comfort/retry");
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator FlashlightSwitchKeepsSoundAndControlWithoutStateText()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            var light = Get<Light>(player, "flashlight"); bool before = light.enabled;
            string caption = Get<string>(shell, "Caption");
            yield return KeysObserved(Key.F); Keys(); yield return null;
            Assert.That(light.enabled, Is.Not.EqualTo(before));
            Assert.That(Get<string>(shell, "Caption"), Is.EqualTo(caption), "Flashlight toggle still replaced the sound caption");
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            var labels = root.Query<Label>().ToList();
            Assert.That(labels.Any(l => l.text.Contains("손전등 켜짐") || l.text.Contains("손전등 꺼짐")), Is.False);
            Assert.That(labels.Any(l => l.text.Contains("F  빛")), Is.True, "The player still needs the F control hint");
            var feedback = player.GetComponent(RequireType("PlayerFeedback"));
            var click = (AudioClip)feedback.GetType().GetField("click", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);
            Assert.That(click && click.samples > 0, Is.True, "Switch sound was removed with its text");
            Debug.Log("HAPPYTOY_PRESENTATION_PASS flashlight: actual F toggles, no persistent/toggle state text, control hint and sound retained");
        }
    }
}
