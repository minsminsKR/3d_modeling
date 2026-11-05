using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static void AimAtForestPresence(Component actor, Component player)
        {
            var target = Get<Transform>(actor, "Visual");
            var feet = new Vector3(500, .04f, 487);
            var direction = target.position - feet; direction.y = 0;
            Call(player, "RelocateWorld", feet, Quaternion.LookRotation(direction));
            Get<Camera>(player, "eyes").transform.localRotation = Quaternion.identity;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestPresenceAppearsOnceWithoutPhysicsAndHoldsOnPause()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var actor = world.GetComponent(RequireType("TempleForestPresence"));
            Assert.That(actor, Is.Not.Null);
            var motion = shell.GetType().GetProperty("ReducedMotion", BindingFlags.Public | BindingFlags.Instance);
            bool savedMotion = (bool)motion.GetValue(shell);
            try
            {
                motion.SetValue(shell, false);
                var figure = Get<Transform>(actor, "Visual");
                Assert.That(figure, Is.Not.Null);
                Assert.That(figure.gameObject.activeSelf, Is.False, "A figure was exposed at the start");
                Assert.That(figure.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
                Assert.That(figure.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(figure.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(figure.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Call(shell, "Pause"); AimAtForestPresence(actor, player); yield return Delay(.3f);
                Assert.That(Get<bool>(actor, "Triggered"), Is.False, "Paused traversal started the appearance");
                Call(shell, "Resume");
                yield return Wait(() => Get<bool>(actor, "Triggered"), 2, "Progress did not start the distant presence");
                yield return Delay(.4f);
                Assert.That(Get<float>(actor, "Opacity"), Is.GreaterThan(0));
                Call(shell, "Pause"); float elapsed = Get<float>(actor, "Elapsed"), opacity = Get<float>(actor, "Opacity");
                yield return Delay(.3f);
                Assert.That(Get<float>(actor, "Elapsed"), Is.EqualTo(elapsed));
                Assert.That(Get<float>(actor, "Opacity"), Is.EqualTo(opacity));
                Call(shell, "Resume");
                yield return Wait(() => Get<bool>(actor, "Finished"), 9, "The presence did not finish");
                Assert.That(figure.gameObject.activeSelf, Is.False);
                AimAtForestPresence(actor, player); yield return Delay(.4f);
                Assert.That(figure.gameObject.activeSelf, Is.False, "The presence repeated in the same session");
            }
            finally { motion.SetValue(shell, savedMotion); }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestPresenceRespectsReducedMotionAndDoesNotReplayOnRestore()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var actor = world.GetComponent(RequireType("TempleForestPresence"));
            var motion = shell.GetType().GetProperty("ReducedMotion", BindingFlags.Public | BindingFlags.Instance);
            bool savedMotion = (bool)motion.GetValue(shell);
            try
            {
                motion.SetValue(shell, true); AimAtForestPresence(actor, player); yield return Delay(.4f);
                Assert.That(Get<bool>(actor, "Triggered"), Is.False);
                Assert.That(Get<Transform>(actor, "Visual").gameObject.activeSelf, Is.False);
                motion.SetValue(shell, false);
                yield return Wait(() => Get<bool>(actor, "Triggered"), 2, "The restored motion setting did not permit the event");
                Call(shell, "Pause"); motion.SetValue(shell, true); yield return null;
                Assert.That(Get<bool>(actor, "Finished"), Is.True);
                Assert.That(Get<Transform>(actor, "Visual").gameObject.activeSelf, Is.False);
                var checkpoint = Call(world, "Capture", 0f);
                Call(session, "RetirePrologue"); yield return null;
                Call(session, "CreatePrologueForCheckpoint", checkpoint); yield return null;
                var restored = Get<Component>(session, "Prologue").GetComponent(RequireType("TempleForestPresence"));
                Assert.That(restored, Is.Not.Null);
                Assert.That(Get<bool>(restored, "Finished"), Is.True);
                Assert.That(Get<Transform>(restored, "Visual"), Is.Null);
            }
            finally { motion.SetValue(shell, savedMotion); }
        }
    }
}
