using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IEnumerator CheatTap(Key key)
        {
            Keys(key); yield return Delay(.08f);
            Keys(); yield return Delay(.08f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CheatF1CollectsOnePerPressAndCannotDuplicateOrFinishTheCorridor()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreateCorridor", 73); Begin(); yield return null;
            Call(session, "ToggleCheatSafeMode");
            var memories = Components("Interactable").Where(x => Get<object>(x, "kind").ToString() == "CorridorMemory").ToArray();
            Call(memories[2], "Use", player);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(1));
            Keys(Key.F1); yield return Delay(.4f);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(2), "Holding F1 must not repeat");
            Keys(); yield return Delay(.08f);
            for (int count = 3; count <= 5; count++)
            {
                yield return CheatTap(Key.F1);
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(count));
            }
            yield return CheatTap(Key.F1);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(5));
            Assert.That(memories.All(x => !x.gameObject.activeSelf), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(shell, "Pause");
            Assert.DoesNotThrow(() => Call(session, "CaptureCheckpoint"));
            Keys(Key.F1, Key.F2); yield return Delay(.1f); Keys(); yield return null;
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.True, "Pause must ignore cheat keys");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CheatF2CancelsPursuitBlocksDeathSightAndSoundAndCanBeTurnedOff()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreateCorridor", 73); Begin(); yield return null;
            var brain = Components("StalkerBrain").First(x => x.gameObject.activeInHierarchy);
            var chase = System.Enum.Parse(Get<object>(brain, "state").GetType(), "Chase");
            Set(brain, "state", chase);
            brain.GetType().GetMethod("BeginAttack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(brain, new object[] { false, session });
            Assert.That(Get<bool>(brain, "AttackActive"), Is.True);
            yield return CheatTap(Key.F2);
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.True);
            Assert.That(Get<object>(brain, "state").ToString(), Is.EqualTo("Patrol"));
            Assert.That(Get<bool>(brain, "AttackActive"), Is.False);
            Assert.That((bool)Call(brain, "CanSeePlayer"), Is.False);
            Assert.That((bool)Call(brain, "HearNoise", player.transform.position, 3f), Is.False);
            var mask = Get<Component>(Get<Component>(session, "Corridor"), "Mask");
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.False);
            Assert.That((bool)Call(mask, "HearNoise", player.transform.position, 3f), Is.False);
            Assert.That((bool)Call(session, "TryDefeat", "cheat regression", "test"), Is.False);
            Call(session, "Finish", false);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Keys(Key.F2); yield return Delay(.4f);
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.False, "Holding F2 toggles only once");
            Keys(); yield return null;
            Assert.That((bool)Call(session, "TryDefeat", "cheat regression", "test"), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CheatF1SkipsSchoolRevealGatesAndKeepsCheckpointValid()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return null;
            yield return CheatTap(Key.F2);
            var chapter = Get<Component>(session, "Chapter");
            for (int count = 1; count <= 5; count++)
            {
                yield return CheatTap(Key.F1);
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(count));
                Assert.That(Get<bool>(Get<Component>(chapter, "FirstAppearances"), "CameraOwned"), Is.False);
            }
            Assert.That(Get<bool>(Get<Component>(chapter, "Portrait"), "Completed"), Is.True);
            Assert.That(Get<bool>(Get<Component>(chapter, "Portrait"), "ChapterWitnessed"), Is.True);
            Assert.That(Get<bool>(Get<Component>(chapter, "Nursery"), "Released"), Is.True);
            Assert.That(Get<Component[]>(chapter, "Memories").All(x => !x.gameObject.activeSelf), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(shell, "Pause");
            Assert.DoesNotThrow(() => Call(session, "CaptureChapterCheckpoint"));
        }
    }
}
