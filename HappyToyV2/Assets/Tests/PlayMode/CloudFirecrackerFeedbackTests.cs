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
            Begin(); Call(inventory, "SetRunStock", 2); // Isolate two denial/cooldown attempts from packet rules.
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
            AssertItemFeedback(inventory, "남은 1발", 1);
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
            AssertItemFeedback(inventory, "남은 0발", 0);
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
            Call(shell, "RestoreDefaultSettings"); Call(shell, "ToggleReducedMotion");
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(500, 0, 500), false);
            Cube("CloudQA snapper floor", player.transform.position - Vector3.up * .2f, new Vector3(30, .4f, 30));
            Physics.SyncTransforms();
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            var fire = Get<Component>(inventory, "LastThrown");
            Call(fire, "Launch", Vector3.zero);
            var body = fire.transform.GetChild(0); Quaternion orientation = body.localRotation;
            var source = fire.GetComponent<AudioSource>();
            Assert.That(source.ignoreListenerPause, Is.False);
            Assert.That(source.ignoreListenerVolume, Is.False);
            Assert.That(source.dopplerLevel, Is.Zero);
            yield return new WaitForSeconds(.15f);
            Assert.That(Quaternion.Angle(body.localRotation, orientation), Is.LessThan(.01f));
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.Zero);
            yield return Wait(() => Get<bool>(fire, "Exploded"), 3, "Snapper did not pop on actual floor contact");
            Assert.That(Get<bool>(fire, "HasFirstContact"), Is.True);
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
            Assert.That(body.gameObject.activeSelf, Is.False);
            var glow = fire.GetComponentInChildren<Light>(true);
            Assert.That(glow, Is.Not.Null); Assert.That(glow.intensity, Is.Zero);
            Assert.That(Get<string>(shell, "Caption"), Does.Contain("콩알탄"));
            Call(shell, "ToggleSubtitles"); Assert.That(Get<bool>(shell, "CaptionVisible"), Is.False);
            Call(shell, "AdjustSettings", -1f, 0f); Assert.That(AudioListener.volume, Is.Zero);
            Call(shell, "Pause"); Vector3 pausedAt = fire.transform.position;
            yield return Delay(.7f);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(fire.transform.position, Is.EqualTo(pausedAt));
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1));
            Assert.That(glow.intensity, Is.Zero);
            Call(shell, "Resume"); yield return new WaitForSeconds(.2f);
            Assert.That(Get<int>(fire, "PopsPlayed"), Is.EqualTo(1), "Resume must not repeat a snapper report");
            Call(session, "Finish", false);
            yield return Wait(() => Components("FirecrackerProjectile").Length == 0, 2, "Result left snapper effects alive");
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Debug.Log("HAPPYTOY_ITEM_PASS snapper: first-contact single report, global mute/pause, subtitles, reduced flash/spin and result cleanup");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator FirecrackerRestartDestroysEffectsAndResetsItsFeedback()
        {
            IsolateThreats(); Begin();
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True);
            var fire = Get<Component>(inventory, "LastThrown");
            var source = fire.GetComponent<AudioSource>();
            var ownedClip = Get<AudioClip>(fire, "crack");
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.True);
            var previous = session;
            Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous, 20,
                "Firecracker restart did not replace its scene");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            inventory = Get<Component>(player, "Firecrackers");
            Assert.That(fire == null, Is.True); Assert.That(source == null, Is.True); Assert.That(ownedClip == null, Is.True);
            Assert.That(Components("FirecrackerProjectile"), Is.Empty);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(12));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
            Assert.That(Get<string>(inventory, "ActionFeedback"), Is.Empty);
            Assert.That(Get<float>(inventory, "FeedbackRemaining"), Is.Zero);
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True, "Restart inherited the previous cooldown");
            Debug.Log("HAPPYTOY_ITEM_PASS restart: live effect/source/owned clip removed; two six-shot packets, empty feedback and ready cooldown restored");
        }
    }
}
