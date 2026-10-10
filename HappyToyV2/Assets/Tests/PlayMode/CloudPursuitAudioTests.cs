using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(40000)]
        public IEnumerator PlayerLocalPursuitMusicRejectsUnwitnessedChaseAndOwnsItsLifecycle()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings"); ((Behaviour)player).enabled = false;
            var origin = MainCorridorPoint(); PlacePlayer(origin + Vector3.right * 6, false);
            Get<Light>(player, "flashlight").enabled = true;
            var actor = StalkerAt(origin); actor.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var detection = player.GetComponent(RequireType("DetectionFeedback"));
            var voice = player.GetComponent(RequireType("DetectionPursuitAudio"));
            Assert.That(voice, Is.Not.Null);
            var cover = Cube("CloudQA unwitnessed pursuit cover", origin + Vector3.right * 3 + Vector3.up * 1.3f,
                new Vector3(.4f, 2.6f, 4)); Physics.SyncTransforms();
            var saved = Call(actor, "CaptureProgress");
            Set(saved, "state", "Chase"); Set(saved, "lastKnown", player.transform.position);
            Set(saved, "memory", 3f); Set(saved, "awareness", 1f);
            Call(actor, "RestoreChapterProgress", saved, Array.CreateInstance(RequireType("Interactable"), 0));
            yield return Delay(.3f);
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Chase"));
            Assert.That((bool)Call(actor, "CanSeePlayer"), Is.False);
            Assert.That(Get<int>(detection, "CuesPlayed"), Is.Zero);
            Call(voice, "OnRecognition");
            Assert.That(Get<int>(voice, "ImpactsPlayed"), Is.Zero, "Direct helper call invented recognition without its real owner event");
            Assert.That(Get<int>(voice, "LoopsStarted"), Is.Zero, "An unwitnessed Chase leaked hidden actor state into music");
            cover.SetActive(false); Physics.SyncTransforms();
            yield return Wait(() => Get<int>(voice, "ImpactsPlayed") == 1 && Get<float>(voice, "LoopGain") > .20f, 3,
                "Actual uncovered player sight did not earn discovery/pursuit voices");
            var music = Get<AudioSource>(voice, "PursuitSource"); var impact = Get<AudioSource>(voice, "ImpactSource");
            var ownedMusic = Get<AudioClip>(voice, "OwnedPursuitClip"); var ownedImpacts = Get<AudioClip[]>(voice, "OwnedImpactClips");
            var importedMusic = (AudioClip)Call(RequireType("ExternalAudio"), "Shared", "pursuit-loop", 0);
            Assert.That(ownedMusic, Is.Not.SameAs(importedMusic));
            Assert.That(music.spatialBlend, Is.Zero); Assert.That(impact.spatialBlend, Is.Zero);
            Assert.That(music.ignoreListenerPause || music.ignoreListenerVolume, Is.False);
            Assert.That(music.panStereo, Is.Zero);
            // Destroying the perception owner alone must remove this owned layer,
            // without destroying its player's ordinary controls/imported resource.
            UnityEngine.Object.Destroy(detection); yield return null; yield return null;
            Assert.That(voice == null && music == null && impact == null && ownedMusic == null && ownedImpacts.All(clip => !clip), Is.True,
                "Standalone perception teardown leaked owned pursuit sound or clips");
            Assert.That(importedMusic, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            UnityEngine.Object.Destroy(cover); UnityEngine.Object.Destroy(actor.gameObject);
        }
    }
}
