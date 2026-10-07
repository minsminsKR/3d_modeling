using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static T RecordedPrivate<T>(Component target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        [UnityTest, Timeout(45000)]
        public IEnumerator RealThrownFirecrackerUsesThreeRecordedTakesAndReleasesOnlyOwnedAudio()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            PlacePlayer(new Vector3(-7.8f, .02f, 0), false); yield return null;
            var feedback = Get<Component>(player, "Feedback");
            Assert.That(CloudExternalAudioTests.MatchesFamily(RecordedPrivate<AudioClip>(feedback, "breath"),
                "player-breath", 1), Is.True, "Exertion still generates noise instead of using recorded breathing");
            var stock = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(stock, "TryThrow"), Is.True);
            var projectile = Get<Component>(stock, "LastThrown");
            var bank = RecordedPrivate<AudioClip[]>(projectile, "crackTakes");
            Assert.That(bank.Length, Is.EqualTo(3));
            Assert.That(bank.Select(CloudExternalAudioTests.Fingerprint).Distinct().Count(), Is.EqualTo(3));
            var shared = Enumerable.Range(0, 3).Select(i => CloudExternalAudioTests.Shared("firecracker", i)).ToArray();
            for (int i = 0; i < 3; i++) Assert.That(bank[i], Is.Not.SameAs(shared[i]));
            yield return Wait(() => Get<int>(projectile, "PopsPlayed") == 1, 5, "Real fuse did not explode");
            Assert.That(RecordedPrivate<AudioClip>(projectile, "crack"), Is.SameAs(bank[0]));
            Call(shell, "Pause"); yield return Delay(.65f);
            Assert.That(Get<int>(projectile, "PopsPlayed"), Is.EqualTo(1), "Pause advanced physical explosion rhythm");
            Call(shell, "Resume");
            yield return Wait(() => Get<int>(projectile, "PopsPlayed") == 2, 2, "Second real pop did not resume");
            Assert.That(RecordedPrivate<AudioClip>(projectile, "crack"), Is.SameAs(bank[1]));
            yield return Wait(() => Get<int>(projectile, "PopsPlayed") == 3, 2, "Third real pop was skipped");
            Assert.That(RecordedPrivate<AudioClip>(projectile, "crack"), Is.SameAs(bank[2]));
            Object.Destroy(projectile.gameObject); yield return null; yield return null;
            Assert.That(bank.All(clip => clip == null), Is.True, "Projectile leaked owned recording copies");
            Assert.That(shared.All(clip => clip != null), Is.True, "Projectile destroyed shared recordings");
        }
    }
}
