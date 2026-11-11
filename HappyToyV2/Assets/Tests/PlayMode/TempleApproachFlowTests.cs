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
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleApproachPickupEnablesRealSnapperAndProtectsOldCheckpointFormat()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var temple = Get<Component>(session, "Prologue");
            bool arrived = false;
            Call(temple, "BeginApproach", new Action(() => arrived = true));
            Begin(); yield return null;
            Assert.That(Get<bool>(temple, "ApproachActive"), Is.True);
            Assert.That(player.transform.position.z, Is.InRange(385f, 387f));
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That(Get<int>(inventory, "Count"), Is.Zero);
            Assert.That((bool)Call(AnimatedActions, "Request", AnimatedItem("Snapper")), Is.False);
            yield return Wait(() => Get<bool>(temple, "ApproachVillageReady"), 20, "Village did not load");
            var checkpoint = Call(temple, "Capture", 0f);
            Assert.That(Get<int>(checkpoint, "version"), Is.EqualTo(4));
            Assert.That(Get<bool>(checkpoint, "approachActive"), Is.True);
            Assert.That(Get<bool>(checkpoint, "approachAcquired"), Is.False);
            var bypass = Call(checkpoint, "Copy");
            Set(bypass, "approachBypassed", true); Set(bypass, "approachCleared", true);
            Set(Get<object>(bypass, "player"), "position", new Vector3(500, .04f, 407));
            Call(bypass, "Validate");
            var bypassCopy = Call(bypass, "Copy");
            Assert.That(Get<bool>(bypassCopy, "approachBypassed"), Is.True);
            Assert.That(Get<bool>(bypassCopy, "approachAcquired"), Is.False);
            var malformed = Call(checkpoint, "Copy");
            Set(Get<object>(malformed, "player"), "stock", 1);
            Assert.Throws<ArgumentException>(() => Call(malformed, "Validate"), "Unacquired stock may not duplicate pickup on restore");
            string savedJson = (string)Call(checkpoint, "ToJson");
            string nullFlag = System.Text.RegularExpressions.Regex.Replace(savedJson,
                "\"approachAcquired\"\\s*:\\s*false", "\"approachAcquired\":null");
            Assert.That(nullFlag, Is.Not.EqualTo(savedJson));
            Assert.Throws<ArgumentException>(() => Call(RequireType("TemplePrologueCheckpoint"), "FromJson", nullFlag));
            Assert.That((bool)Call(RequireType("TemplePrologueCheckpoint"), "ApproachPoint", new Vector3(489f, .6f, 430f)), Is.True);
            var encounter = session.GetComponentsInChildren<Component>(true).First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            var target = Get<Vector3>(encounter, "GroundTarget");
            Assert.That((bool)Call(encounter.GetType(), "ValidGroundImpact", target, Vector3.up, target), Is.True);
            Assert.That((bool)Call(encounter.GetType(), "ValidGroundImpact", target, Vector3.back, target), Is.False);
            Assert.That((bool)Call(encounter.GetType(), "ValidGroundImpact", target + Vector3.forward * 3, Vector3.up, target), Is.False);
            var pickup = encounter.transform.Find("Alley snapper supply");
            Assert.That(pickup, Is.Not.Null);
            var interact = pickup.GetComponent(RequireType("Interactable"));
            Call(interact, "Use", player);
            Assert.That(Get<bool>(encounter, "Acquired"), Is.True, "Supply and event state must commit in the same frame");
            var sameFrame = Call(temple, "Capture", 0f);
            Assert.That(Get<bool>(sameFrame, "approachAcquired"), Is.True);
            Assert.That(Get<int>(Get<object>(sameFrame, "player"), "stock"), Is.EqualTo(6));
            yield return null;
            Assert.That(Get<bool>(encounter, "Acquired"), Is.True);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            Assert.That(Get<bool>(temple, "ApproachSnapperAllowed"), Is.True);
            Assert.That((bool)Call(AnimatedActions, "Request", AnimatedItem("Snapper")), Is.True);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > 0, 10, "도입 콩알탄 발사가 확정되지 않음");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(5));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Not.Null);
            Assert.That(arrived, Is.False);
        }
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleApproachSuspendAndContinuePreservesStockAndOpenPassage()
        {
            string directory = NewRecordFixture();
            Call(session, "ConfigureRecordDirectory", directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            var world = Get<Component>(session, "Prologue");
            yield return Wait(() => Get<bool>(world, "ApproachVillageReady"), 20, "Village did not load");
            yield return Wait(() => Get<bool>(player, "Grounded"), 5, "Player was not grounded for a resumable save");
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            // Explicit fixture setup: the separate actual-route test verifies
            // real E input, wrong impact and the successful ground throw.
            var pickup = encounter.transform.Find("Alley snapper supply");
            Call(pickup.GetComponent(RequireType("Interactable")), "Use", player);
            var inventory = Get<Component>(player, "Firecrackers");
            Call(inventory, "SetRunStock", 4);
            Call(encounter, "Restore", true, true, 4, false);
            Vector3 savedPosition = player.transform.position;
            var previous = session;
            Call(shell, "Pause"); yield return RecoveryClick("suspend-temple");
            yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(directory, "temple-suspend-v1.json")), Is.True);
            yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            world = Get<Component>(session, "Prologue");
            Assert.That(Get<bool>(world, "ApproachActive"), Is.True);
            encounter = session.GetComponentsInChildren<Component>(true)
                .First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            Assert.That(Get<bool>(encounter, "Acquired"), Is.True);
            Assert.That(Get<bool>(encounter, "Cleared"), Is.True);
            Assert.That(encounter.transform.Find("Beast passage blocker").gameObject.activeSelf, Is.False);
            Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(4));
            Assert.That(Vector3.Distance(player.transform.position, savedPosition), Is.LessThan(.02f));
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(directory, "temple-suspend-v1.json")), Is.False);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(directory, "temple-suspend-v1.json.used")), Is.True);
        }
        [UnityTest, Timeout(240000)]
        public IEnumerator TempleApproachFailedRestorePreservesOriginalCheckpointToken()
        {
            string directory = NewRecordFixture();
            Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreatePrologue"); IsolateThreats();
            var world = Get<Component>(session, "Prologue");
            Call(world, "BeginApproach", new Action(() => { })); Begin();
            yield return Wait(() => Get<bool>(world, "ApproachVillageReady"), 20, "Village did not load");
            yield return Wait(() => Get<bool>(player, "Grounded"), 5, "Player not grounded");
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            var pickup = encounter.transform.Find("Alley snapper supply");
            Call(pickup.GetComponent(RequireType("Interactable")), "Use", player);
            Call(Get<Component>(player, "Firecrackers"), "SetRunStock", 4);
            Call(encounter, "Restore", true, true, 4, false);
            var checkpoint = Call(world, "Capture", 0f);
            // A schema-valid external save with no floor under the pose must be
            // rejected after streaming, before its one-use token is consumed.
            Set(Get<object>(checkpoint, "player"), "position", new Vector3(500, 3, 430));
            Assert.That((bool)Call(Get<object>(session, "TempleSuspension"), "Save", checkpoint), Is.True);
            string path = System.IO.Path.Combine(directory, "temple-suspend-v1.json");
            string original = System.IO.File.ReadAllText(path);
            var old = session; Call(shell, "Restart", false); yield return RecoveryRebind(old);
            yield return RefreshSlotTitle(directory);
            old = session; Call(shell, "ContinueTemple");
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != old,
                60, "Failed asynchronous restore did not return to the title");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            yield return Wait(() => !Get<bool>(shell, "IsReloading"), 30, "Failure return did not finish");
            Assert.That(System.IO.File.Exists(path), Is.True, "Failure consumed the original checkpoint");
            Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo(original));
            Assert.That(System.IO.File.Exists(path + ".used"), Is.False);
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleApproachMissingAnimalDuringRetreatFailsOpenAndCanBeSaved()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var world = Get<Component>(session, "Prologue");
            Call(world, "BeginApproach", new Action(() => { })); Begin();
            yield return Wait(() => Get<bool>(world, "ApproachVillageReady"), 20, "Village did not load");
            yield return Wait(() => Get<bool>(player, "Grounded"), 5, "Player not grounded");
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            var blocker = encounter.transform.Find("Beast passage blocker");
            Assert.That(blocker && blocker.gameObject.activeSelf, Is.True);
            var type = encounter.GetType();
            type.GetProperty("Reacting").GetSetMethod(true).Invoke(encounter, new object[] { true });
            type.GetField("beast", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(encounter, null);
            yield return null;
            Assert.That(Get<bool>(encounter, "Reacting"), Is.False);
            Assert.That(Get<bool>(encounter, "Cleared"), Is.True);
            Assert.That(Get<bool>(encounter, "Bypassed"), Is.True);
            Assert.That(blocker.gameObject.activeSelf, Is.False, "Missing animal left an invisible blocker");
            var saved = Call(world, "Capture", 0f);
            Assert.That(Get<bool>(saved, "approachBypassed"), Is.True);
            Assert.That(Get<bool>(saved, "approachCleared"), Is.True);
            Assert.That(Get<bool>(saved, "approachAcquired"), Is.False);
        }
    }
}
