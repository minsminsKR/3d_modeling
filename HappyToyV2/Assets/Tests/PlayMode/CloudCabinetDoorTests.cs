using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator CabinetTransitionsPlayOnlyDistinctRecordedOpeningAndClosingDoors()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            var cabinet = Components("Interactable").First(item => Get<object>(item, "kind").ToString() == "HidingPlace" &&
                Mathf.Abs(item.transform.position.y) < .2f);
            var feedback = Get<Component>(player, "Feedback");
            var source = Get<AudioSource>(feedback, "InteractionSource");
            ((Behaviour)player).enabled = false;
            PlacePlayer(Get<Transform>(cabinet, "outside").position); yield return null;
            int before = Get<int>(feedback, "InteractionCuesPlayed");
            Call(cabinet, "Use", player); yield return null;
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            var close = Get<AudioClip>(feedback, "LastInteractionClip");
            Assert.That(CloudExternalAudioTests.MatchesFamily(close, "cabinet-close", 1), Is.True);
            Assert.That(source.clip, Is.SameAs(close)); Assert.That(source.isPlaying, Is.True);
            Assert.That(source.pitch, Is.EqualTo(1));
            Assert.That(Get<int>(feedback, "InteractionCuesPlayed"), Is.EqualTo(before + 1));
            Assert.That(Get<string>(shell, "Caption"), Is.EqualTo("[캐비닛 문 닫힘]"));
            Call(shell, "Pause"); yield return Delay(.06f);
            float sample = source.timeSamples; yield return Delay(.12f);
            Assert.That(source.timeSamples, Is.EqualTo(sample)); Call(shell, "Resume");
            Call(cabinet, "Use", player); yield return null;
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            var open = Get<AudioClip>(feedback, "LastInteractionClip");
            Assert.That(CloudExternalAudioTests.MatchesFamily(open, "cabinet-open", 1), Is.True);
            Assert.That(source.clip, Is.SameAs(open)); Assert.That(source.pitch, Is.EqualTo(1));
            Assert.That(CloudExternalAudioTests.Fingerprint(open), Is.Not.EqualTo(CloudExternalAudioTests.Fingerprint(close)));
            Assert.That(Get<string>(shell, "Caption"), Is.EqualTo("[캐비닛 문 열림]"));
            Assert.That(Get<int>(feedback, "InteractionCuesPlayed"), Is.EqualTo(before + 2));
            // A fast repeated transition replaces the prior door, rather than
            // stacking an opening, a closing and the old clothing one-shots.
            Call(cabinet, "Use", player); yield return null;
            Assert.That(source.clip, Is.SameAs(close));
            Assert.That(source.timeSamples, Is.LessThan(Mathf.CeilToInt(source.clip.frequency * .2f)));
            var shared = CloudExternalAudioTests.Shared("cabinet-open");
            Object.Destroy(feedback); yield return null; yield return null;
            Assert.That(open == null && close == null && source == null, Is.True);
            Assert.That(shared != null, Is.True, "Removing the player destroyed the shared recorded door");
            Debug.Log("HAPPYTOY_CABINET_DOORS_PASS actual hide/leave transitions, one door clip at a time, no cloth/extra impact layer, pause, replacement, owned cleanup");
        }
    }
}
