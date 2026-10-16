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
        static T RecordedPrivate<T>(Component target, string name)
        {
            Assert.That(target, Is.Not.Null, "Missing recorded-audio owner for " + name);
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private audio field " + target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        static void ThrowRecordedSnapperVariant(Component stock, int variant)
        {
            // Select each real production random branch once. Only the synchronous
            // throw is seeded; unrelated actors keep their existing random state.
            var previous = Random.state;
            try
            {
                int seed;
                for (seed = 0; seed < 10000; seed++)
                {
                    Random.InitState(seed);
                    if (Random.Range(0, 3) == variant) break;
                }
                Assert.That(seed, Is.LessThan(10000), "No deterministic seed selected recording " + variant);
                Random.InitState(seed);
                Assert.That((bool)Call(stock, "TryThrow"), Is.True);
            }
            finally { Random.state = previous; }
        }
        [UnityTest, Timeout(45000)]
        public IEnumerator RealThrownFirecrackerUsesThreeRecordedTakesAndReleasesOnlyOwnedAudio()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(500, 0, 500), false);
            var floor = Cube("CloudQA recorded snapper impact floor", player.transform.position - Vector3.up * .2f,
                new Vector3(30, .4f, 30));
            Physics.SyncTransforms(); yield return null;
            var feedback = Get<Component>(player, "Feedback");
            Assert.That(CloudExternalAudioTests.MatchesFamily(RecordedPrivate<AudioClip>(feedback, "breath"),
                "player-breath", 1), Is.True, "Exertion still generates noise instead of using recorded breathing");
            var stock = Get<Component>(player, "Firecrackers");
            int originalStock = Get<int>(stock, "Count");
            var owned = new AudioClip[3];
            var shared = Enumerable.Range(0, 3).Select(i => CloudExternalAudioTests.Shared("firecracker", i)).ToArray();
            Assert.That(shared.Select(CloudExternalAudioTests.Fingerprint).Distinct().Count(), Is.EqualTo(3));
            var fingerprints = new string[3];
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    yield return Wait(() => Get<float>(stock, "Cooldown") <= 0, 2, "Natural snapper cooldown did not end");
                    ThrowRecordedSnapperVariant(stock, i);
                    var projectile = Get<Component>(stock, "LastThrown");
                    Call(projectile, "Launch", Vector3.zero);
                    var source = projectile.GetComponent<AudioSource>();
                    owned[i] = RecordedPrivate<AudioClip>(projectile, "crack");
                    fingerprints[i] = CloudExternalAudioTests.Fingerprint(owned[i]);
                    Assert.That(fingerprints[i], Is.EqualTo(CloudExternalAudioTests.Fingerprint(shared[i])),
                        "Actual single-pellet throw selected the wrong recorded take");
                    Assert.That(shared.All(clip => clip != owned[i]), Is.True, "Projectile owns a shared imported clip");
                    Assert.That(Get<int>(stock, "Count"), Is.EqualTo(originalStock - i - 1), "A pellet must consume exactly one shot");
                    Assert.That((bool)Call(stock, "TryThrow"), Is.False, "The same frame bypassed the finite-shot cooldown");
                    yield return Wait(() => Get<int>(projectile, "PopsPlayed") == 1, 5, "Real pellet never hit the floor");
                    Assert.That(Get<bool>(projectile, "HasFirstContact"), Is.True);
                    var effect = projectile.GetComponentInChildren(RequireType("SnapperImpactEffect"));
                    Assert.That(effect, Is.Not.Null, "Physical contact omitted the owned impact effect");
                    var particles = effect.GetComponentsInChildren<ParticleSystem>();
                    Assert.That(particles.Length, Is.EqualTo(2));
                    Assert.That(particles.All(system => !system.main.loop), Is.True);
                    Assert.That(particles.Sum(system => system.main.maxParticles), Is.EqualTo(17), "Impact dust/paper must remain finite");
                    var material = particles[0].GetComponent<ParticleSystemRenderer>().sharedMaterial;
                    Assert.That(source.ignoreListenerPause || source.ignoreListenerVolume, Is.False);
                    Call(shell, "Pause"); yield return null;
                    var clocks = particles.Select(system => system.time).ToArray();
                    var at = projectile.transform.position;
                    yield return Delay(.65f);
                    Assert.That(projectile.transform.position, Is.EqualTo(at));
                    Assert.That(particles.Select(system => system.time).ToArray(), Is.EqualTo(clocks), "Pause advanced impact particles");
                    Assert.That(Get<int>(projectile, "PopsPlayed"), Is.EqualTo(1), "Pause replayed a single-pellet report");
                    Assert.That(RecordedPrivate<AudioClip>(projectile, "crack"), Is.SameAs(owned[i]));
                    Call(shell, "Resume");
                    float end = Time.realtimeSinceStartup + 3;
                    while (projectile && Time.realtimeSinceStartup < end)
                    {
                        Assert.That(Get<int>(projectile, "PopsPlayed"), Is.EqualTo(1), "A pellet must never play a second report");
                        yield return null;
                    }
                    Assert.That(projectile == null, Is.True, "Single-pellet impact exceeded its bounded cleanup lifetime");
                    yield return null; yield return null;
                    Assert.That(source == null && effect == null && material == null, Is.True, "Pellet leaked its source or impact resources");
                    Assert.That(owned[i] == null, Is.True, "Projectile leaked its owned recording copy");
                    Assert.That(shared.All(clip => clip != null), Is.True, "Projectile destroyed shared recordings");
                }
                Assert.That(fingerprints.Distinct().Count(), Is.EqualTo(3), "Three actual pellets did not exercise all recorded takes");
                Assert.That(owned.All(clip => clip == null), Is.True);
            }
            finally { Object.Destroy(floor); }
        }
    }
}
