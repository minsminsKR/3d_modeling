using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // This position is on the old physical prayer-table top and overlaps
        // the newly added note volume. Disable only that new volume to check
        // the old-world clearance before building the legacy snapshot.
        IEnumerator RestoreLegacyPoseAtTempleNote(int phase = 0)
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var snapshot = Call(world, "Capture", 0f);
            var progress = Get<object>(snapshot, "player");
            var feet = new Vector3(500.76f, .845f, 507.46f);
            Set(progress, "position", feet); Set(progress, "crouched", false);
            var note = world.transform.Find("이름이 비어 있는 쪽지").GetComponent<Collider>();
            note.enabled = false;
            try
            {
                Physics.SyncTransforms();
                Assert.That((bool)Call(player, "CanRestoreTempleProgress", progress), Is.True,
                    "The chosen pose must be valid before the new note volume is considered");
            }
            finally { note.enabled = true; Physics.SyncTransforms(); }

            Set(snapshot, "version", 2); Set(snapshot, "storyVersion", 0);
            Set(snapshot, "phase", phase); Set(snapshot, "incensePlaced", phase == 2);
            Set(snapshot, "firstClueRead", false); Set(snapshot, "secondClueRead", false);
            string legacy = JsonUtility.ToJson(snapshot);
            legacy = Regex.Replace(legacy, ",\\\"storyVersion\\\":-?[0-9]+", "");
            legacy = Regex.Replace(legacy, ",\\\"legacyClueExemptMask\\\":-?[0-9]+", "");
            legacy = Regex.Replace(legacy, ",\\\"firstClueRead\\\":(true|false)", "");
            legacy = Regex.Replace(legacy, ",\\\"secondClueRead\\\":(true|false)", "");
            Assert.That(legacy, Does.Not.Contain("storyVersion"));
            var legacySnapshot = Call(RequireType("TemplePrologueCheckpoint"), "FromJson", legacy);
            Call(session, "RetirePrologue"); yield return null;
            Assert.DoesNotThrow(() => Call(session, "CreatePrologueForCheckpoint", legacySnapshot),
                "The new observation target must not reject an old valid pose");
            Assert.That(Vector3.Distance(player.transform.position, feet), Is.LessThan(.001f));
            Physics.SyncTransforms();
            var newWorld = Get<Component>(session, "Prologue");
            var newNote = newWorld.transform.Find("이름이 비어 있는 쪽지").GetComponent<Collider>();
            var controller = player.GetComponent<CharacterController>();
            var centre = controller.transform.TransformPoint(controller.center);
            float half = Mathf.Max(0, controller.height * .5f - controller.radius);
            var overlaps = Physics.OverlapCapsule(centre - Vector3.up * half, centre + Vector3.up * half,
                controller.radius - .02f, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(overlaps.Contains(newNote), Is.True,
                "The fixture must genuinely overlap the new note, not merely test a nearby pose");
            Assert.That(Physics.GetIgnoreCollision(controller, newNote), Is.True,
                "The observation collider must not physically trap a restored player");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleLegacyNoteOverlapDoesNotRejectSavedPose()
        {
            yield return RestoreLegacyPoseAtTempleNote();
            Assert.That(Get<bool>(session, "PrologueMode"), Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleNoteRemainsNonblockingAfterActualNextDayTransition()
        {
            yield return RestoreLegacyPoseAtTempleNote(2);
            var world = Get<Component>(session, "Prologue");
            var note = world.transform.Find("이름이 비어 있는 쪽지").GetComponent<Collider>();
            var controller = player.GetComponent<CharacterController>();
            // Enter the actual first-day departure boundary. No shell callback
            // is registered by this fixture, so it can inspect the masked state.
            var spawn = Get<Vector3>(world, "Spawn");
            Call(player, "RelocateWorld", spawn, Quaternion.identity);
            yield return null;
            Assert.That(Get<bool>(world, "TransitionRequested"), Is.True);
            Assert.That(Get<object>(Get<Component>(world, "Director"), "Phase").ToString(), Is.EqualTo("SecondApproach"));
            Call(world, "BeginSecondDay");
            Assert.That(Get<bool>(world, "TransitionRequested"), Is.False);
            Call(world, "ReadStoryClue"); yield return null; yield return null;
            var narration = world.GetComponent(RequireType("StoryNarration"));
            var text = Get<string>(narration, "CurrentText");
            Assert.That(text, Does.Contain("집 그림이 있다"));
            Assert.That(text, Does.Not.Contain("생겼다"), "A legacy save must not assert an unobserved comparison");
            Assert.That(Physics.GetIgnoreCollision(controller, note), Is.True,
                "Next-day relocation must restore the note's nonblocking collision pair");
            // Move directly only after the real transition, avoiding a second
            // controller toggle that this lifecycle test does not claim covers.
            player.transform.position = new Vector3(500.76f, .845f, 507.46f);
            Physics.SyncTransforms();
            float before = player.transform.position.x;
            controller.Move(Vector3.right * .05f);
            Assert.That(player.transform.position.x - before, Is.GreaterThan(.04f),
                "The observation target must not impede player motion after next day");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleNoteOverlapDoesNotBlockStandingButRealObstaclesStillDo()
        {
            yield return RestoreLegacyPoseAtTempleNote();
            Assert.That((bool)Call(player, "TrySetCrouching", true), Is.True);
            Assert.That((bool)Call(player, "TrySetCrouching", false), Is.True,
                "An observation-only note must not block the player's stance query");
            Assert.That(Get<bool>(player, "StandingBlocked"), Is.False);

            Assert.That((bool)Call(player, "TrySetCrouching", true), Is.True);
            var controller = player.GetComponent<CharacterController>();
            var blocker = new GameObject("Real low ceiling for stance test");
            try
            {
                blocker.transform.position = player.transform.position + Vector3.up * (controller.height + .15f);
                var box = blocker.AddComponent<BoxCollider>(); box.size = new Vector3(.4f, .2f, .4f);
                Physics.SyncTransforms();
                Assert.That((bool)Call(player, "TrySetCrouching", false), Is.False,
                    "The note exemption must not ignore real blocking colliders");
                Assert.That(Get<bool>(player, "StandingBlocked"), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(blocker); Physics.SyncTransforms(); }
            Assert.That((bool)Call(player, "TrySetCrouching", false), Is.True);
        }
    }
}
