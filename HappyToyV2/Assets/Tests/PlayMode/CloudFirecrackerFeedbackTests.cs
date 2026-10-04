using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using static HappyToy.V2.CloudTests.CloudFirecrackerFeedbackTests;

namespace HappyToy.V2.CloudTests
{
    public static class CloudFirecrackerFeedbackTests
    {
        public static void AssertItemFeedback(Component inventory, string fragment, int count)
        {
            Assert.That(Get<string>(inventory, "ActionFeedback"), Does.Contain(fragment));
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.True);
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.GreaterThan(0));
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(count));
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator FirecrackerDenialsStayLocalAndNeverOverwriteRecords()
        {
            IsolateThreats();
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.Empty);
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.Zero);
            Assert.That(Components("FirecrackerProjectile"), Is.Empty);
            Begin();
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            var cabinet = Components("Interactable").Single(item => item.name == "음악실 은신함");
            PlacePlayer(Get<Transform>(cabinet, "outside").position);
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            string record = Get<string>(session, "Notice");
            int revision = Get<int>(session, "NoticeRevision");
            yield return KeysObserved(Key.Q);
            yield return Wait(() => Get<bool>(inventory, "FeedbackVisible"), 2, "Hidden Q had no item feedback");
            Keys(); yield return null;
            AssertItemFeedback(inventory, "숨어", 2);
            Assert.That(Components("FirecrackerProjectile"), Is.Empty);
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            AssertItemFeedback(inventory, "남은 1개", 1);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            AssertItemFeedback(inventory, "준비 중", 1);
            string feedback = Get<string>(inventory, "ActionFeedback");
            float remaining = Get<float>(inventory, "FeedbackRemaining");
            Call(shell, "Pause");
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            yield return Delay(.6f);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.EqualTo(feedback));
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.EqualTo(remaining));
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            Call(shell, "Resume");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False, "Paused wall time must not advance cooldown");
            yield return new WaitForSeconds(.55f);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            AssertItemFeedback(inventory, "남은 0개", 0);
            for (int i = 0; i < 4; i++) Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            AssertItemFeedback(inventory, "모두 사용", 0);
            Assert.That(Components("FirecrackerProjectile").Length, Is.EqualTo(2));
            Assert.That(Get<string>(session, "Notice"), Is.EqualTo(record));
            Assert.That(Get<int>(session, "NoticeRevision"), Is.EqualTo(revision));
            yield return new WaitForSeconds(1.9f);
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.Empty);
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.Zero);
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
            Assert.That((bool)Call(session, "Collect", "record"), Is.True);
            foreach (string id in new[] { "music-roster", "archive-record", "nursery-tag" }) Inspect(id);
            Assert.That((bool)Call(session, "Collect", "restore"), Is.True);
            record = Get<string>(session, "Notice"); revision = Get<int>(session, "NoticeRevision");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            AssertItemFeedback(inventory, "복원", 0);
            Assert.That(Get<string>(session, "Notice"), Is.EqualTo(record));
            Assert.That(Get<int>(session, "NoticeRevision"), Is.EqualTo(revision));
            yield return Wait(() => Components("FirecrackerProjectile").Length == 0, 2, "Restoration left a live firecracker");
            Debug.Log("HAPPYTOY_ITEM_PASS feedback: title/pause silent, hiding/cooldown/empty/restored explained, finite inventory, record unchanged, feedback expiry");
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator FirecrackerFirstPopIsSingleAndComfortSettingsApplyWhilePaused()
        {
            IsolateThreats(); Begin();
            Call(shell, "RestoreDefaultSettings");
            Call(shell, "ToggleReducedMotion");
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(500, 0, 500), false);
            Cube("CloudQA firecracker floor", player.transform.position - Vector3.up * .2f, new Vector3(30, .4f, 30));
            Physics.SyncTransforms();
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            var fire = Get<Component>(inventory, "LastThrown");
            // Controlled stationary launch isolates the real fuse, collision, sound
            // emission and comfort behavior; normal Q trajectory is covered separately.
            Call(fire, "Launch", Vector3.zero);
            var body = fire.transform.GetChild(0);
            Quaternion orientation = body.localRotation;
            var source = fire.GetComponent<AudioSource>();
            var glow = fire.GetComponent<Light>();
            Assert.That(source.ignoreListenerPause, Is.False);
            Assert.That(source.ignoreListenerVolume, Is.False);
            Assert.That(source.dopplerLevel, Is.Zero);
            yield return new WaitForSeconds(.3f);
            Assert.That(Quaternion.Angle(body.localRotation, orientation), Is.LessThan(.01f), "Reduced motion kept fuse spinning");
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.Zero);
            yield return Wait(() => Get<bool>(fire, "Exploded"), 4, "Real fuse did not reach the first pulse");
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1), "First explosion emitted overlapping pops");
            Assert.That(body.gameObject.activeSelf, Is.False);
            Assert.That(glow.intensity, Is.EqualTo(.45f).Within(.001f));
            Assert.That(Get<string>(shell, "Caption"), Is.EqualTo("[폭죽 터지는 소리]"));
            Assert.That(Get<bool>(shell, "CaptionVisible"), Is.True);
            Call(shell, "ToggleSubtitles");
            Assert.That(Get<bool>(shell, "CaptionVisible"), Is.False);
            Call(shell, "AdjustSettings", -1f, 0f);
            Assert.That(AudioListener.volume, Is.Zero, "Item audio must use the global listener volume");
            Call(shell, "Pause");
            Vector3 pausedAt = fire.transform.position;
            yield return Delay(.7f);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(fire.transform.position, Is.EqualTo(pausedAt));
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1), "Pause emitted another pop");
            Call(shell, "Resume");
            yield return Wait(() => Get<int>(fire, "PopsPlayed") >= 2, 2, "Resume never reached next pulse");
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(2));
            Assert.That(glow.intensity, Is.EqualTo(.45f).Within(.001f));
            Assert.That(Get<bool>(shell, "CaptionVisible"), Is.False);
            Call(shell, "ToggleReducedMotion");
            float brightDeadline = Time.realtimeSinceStartup + 2;
            while ((Get<int>(fire, "PopsPlayed") < 3 || glow.intensity <= 1) && Time.realtimeSinceStartup < brightDeadline)
                yield return null;
            // Observe and pause the real bright pulse in this same coroutine step.
            // A nested Wait could return after another Update decayed its exact peak.
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.GreaterThanOrEqualTo(3));
            Assert.That(glow.intensity, Is.InRange(1.00001f, 4.00001f), "No genuinely bright ordinary pulse was observed");
            int pausedPops = Get<int>(fire, "PopsPlayed");
            Call(shell, "Pause"); Call(shell, "ToggleReducedMotion");
            yield return null;
            Assert.That(glow.intensity, Is.EqualTo(.45f).Within(.001f), "Paused settings left a bright flash frozen");
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(pausedPops));
            Call(shell, "Resume");
            Call(session, "Finish", false);
            yield return Wait(() => Components("FirecrackerProjectile").Length == 0, 2, "Result screen left firecracker effects alive");
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.Empty);
            Debug.Log("HAPPYTOY_ITEM_PASS effects: one first pop, finite pulse cadence, global mute/pause, sound-caption option, reduced spin/flash, paused comfort change, result cleanup");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator FirecrackerRestartDestroysEffectsAndResetsItsFeedback()
        {
            IsolateThreats(); Begin();
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            var fire = Get<Component>(inventory, "LastThrown");
            var source = fire.GetComponent<AudioSource>();
            var material = fire.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.True);
            var previous = session;
            Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20,
                "Firecracker restart did not replace its scene");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            inventory = Get<Component>(player, "Firecrackers");
            Assert.That(fire == null, Is.True); Assert.That(source == null, Is.True); Assert.That(material == null, Is.True);
            Assert.That(Components("FirecrackerProjectile"), Is.Empty);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.Empty);
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.Zero);
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True, "Restart inherited the previous cooldown");
            Debug.Log("HAPPYTOY_ITEM_PASS restart: live effect/source/material removed; two items, empty feedback and ready cooldown restored");
        }
    }
}
