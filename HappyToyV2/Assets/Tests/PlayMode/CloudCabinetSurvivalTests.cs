using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component SchoolCabinet()
        {
            var chapter = Get<Component>(session, "Chapter");
            bool campus = chapter && Get<int>(chapter, "LayoutVersion") >= 2;
            // SchoolCampusRooms creates a real upper classroom cabinet; the
            // old annex music-room interaction is an inactive source template.
            return Components("Interactable", false).Single(item => campus ?
                Get<string>(item, "stableId") == "campus-hide-u-class-a" : item.name == "음악실 은신함");
        }
        Component CabinetChaser(Component cabinet)
        {
            var inside = Get<Transform>(cabinet, "inside").position;
            var outside = Get<Transform>(cabinet, "outside").position;
            var outward = outside - inside; outward.y = 0; outward.Normalize();
            Assert.That(NavMesh.SamplePosition(outside + outward * 3, out var anchor, .5f, NavMesh.AllAreas), Is.True);
            var enemy = StalkerAt(anchor.position);
            Set(enemy, "state", "Chase"); enemy.transform.rotation = Quaternion.LookRotation(outside - anchor.position);
            Physics.SyncTransforms(); return enemy;
        }
        void AssertCabinetEntryReady(Component cabinet, Component enemy)
        {
            Assert.That(Get<bool>(session, "InputAllowed"), Is.True);
            Assert.That(Get<bool>(player, "Paused"), Is.False, "A protected camera or paused menu must not block this entry fixture");
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Assert.That(Get<bool>(cabinet, "InteractionAvailable"), Is.True, "The fixture selected a retired or disabled cabinet");
            Assert.That(((Behaviour)enemy).isActiveAndEnabled, Is.True);
            Assert.That(Get<Component>(enemy, "player"), Is.SameAs(player));
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Chase"));
            Assert.That((bool)Call(RequireType("EnemyNavigation"), "SameActorFloor", enemy.GetComponent<NavMeshAgent>(),
                player.transform.position, Get<float>(enemy, "HomeFloorY")), Is.True, "The pursuit fixture must be on the player's actual floor");
        }
        Collider[] CabinetExitBlockers(Component cabinet)
        {
            // Match the real standing exit volume when selecting a local fixture;
            // no collider, cabinet marker or production clearance rule is changed.
            var controller = player.GetComponent<CharacterController>();
            var feet = Get<Transform>(cabinet, "outside").position;
            var centre = feet + player.transform.TransformVector(controller.center);
            float half = Mathf.Max(0, controller.height * .5f - controller.radius);
            return Physics.OverlapCapsule(centre - player.transform.up * half, centre + player.transform.up * half,
                Mathf.Max(.01f, controller.radius - .02f), ~0, QueryTriggerInteraction.Ignore)
                .Where(collider => !collider.transform.IsChildOf(player.transform)).ToArray();
        }
        string CabinetExitBlockerDetails(Component cabinet) => string.Join(", ", CabinetExitBlockers(cabinet)
            .Select(collider => collider.name + " parent=" + (collider.transform.parent ? collider.transform.parent.name : "scene") +
                " position=" + collider.transform.position.ToString("F3") + " rotation=" + collider.transform.eulerAngles.ToString("F3") +
                " scale=" + collider.transform.lossyScale.ToString("F3") + " boundsMin=" + collider.bounds.min.ToString("F3") +
                " boundsMax=" + collider.bounds.max.ToString("F3")));
        Component CheckpointCabinetFixture(bool corridor)
        {
            Physics.SyncTransforms();
            var rejected = new List<string>();
            var pose = Call(player, "CaptureProgress");
            foreach (var cabinet in Components("Interactable", false).Where(item =>
                Get<object>(item, "kind").ToString() == "HidingPlace" && Get<bool>(item, "InteractionAvailable") &&
                (corridor ? item.name == "Corridor hiding cabinet" :
                    Get<string>(item, "stableId") == "campus-hide-u-class-a"))
                .OrderBy(item => Get<string>(item, "stableId"), StringComparer.Ordinal))
            {
                var outside = Get<Transform>(cabinet, "outside").position;
                string label = cabinet.name + " at=" + outside.ToString("F3");
                var blockers = CabinetExitBlockerDetails(cabinet);
                if (blockers.Length > 0)
                {
                    string obstruction = label + " blocked by " + blockers;
                    rejected.Add(obstruction); Debug.Log("CABINET_CHECKPOINT_EXIT_BLOCKED " + obstruction);
                }
                // The school regression must use this original cabinet. It may
                // never hide a layout defect by falling back to another room.
                if (!corridor) Assert.That(CabinetExitBlockers(cabinet), Is.Empty,
                    "The original upper classroom cabinet exit must remain clear: " + blockers);
                if (blockers.Length > 0) continue;
                Set(pose, "position", outside);
                if (!(bool)Call(player, corridor ? "CanRestoreProgress" : "CanRestoreChapterProgress", pose))
                { rejected.Add(label + " lacks a supported checkpoint pose"); continue; }
                var outward = outside - Get<Transform>(cabinet, "inside").position; outward.y = 0; outward.Normalize();
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(outside + outward * 3, out var anchor, .5f, NavMesh.AllAreas) ||
                    Mathf.Abs(anchor.position.y - outside.y) > .15f ||
                    !NavMesh.CalculatePath(outside, anchor.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                { rejected.Add(label + " lacks a same-floor pursuit approach"); continue; }
                Debug.Log("CABINET_CHECKPOINT_FIXTURE selected=" + label + " rejected=" + string.Join("; ", rejected));
                return cabinet;
            }
            Assert.Fail("No existing active cabinet has a safe original exit and real checkpoint/pursuit navigation: " + string.Join("; ", rejected));
            return null;
        }
        void LeaveCabinetForCheckpoint(Component cabinet, Component enemy)
        {
            // End the controlled chase before attempting the public exit action.
            enemy.gameObject.SetActive(false); Physics.SyncTransforms();
            Assert.That(Get<bool>(player, "Paused"), Is.False);
            Assert.That(CabinetExitBlockers(cabinet), Is.Empty, "Real exit is obstructed: " + CabinetExitBlockerDetails(cabinet));
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.False, "Public cabinet exit failed before checkpoint capture");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator QuietCabinetEntryNeverDrawsAndRejectsDuplicatePausedOrBlockedTransitions()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var outside = Get<Transform>(cabinet, "outside").position;
            PlacePlayer(outside); int draws = 0;
            Set(player, "HidingRandomSample", (Func<float>)(() => { draws++; return .95f; }));
            // Disabled actors and actors chasing on another floor cannot create entry risk.
            var disabled = CabinetChaser(cabinet); ((Behaviour)disabled).enabled = false;
            var otherFloor = CabinetChaser(cabinet); ((Behaviour)otherFloor).enabled = false;
            otherFloor.GetComponent<NavMeshAgent>().enabled = false; otherFloor.transform.position += Vector3.up * 5;
            ((Behaviour)otherFloor).enabled = true; // OnEnable binds its real home floor at the new height.
            Call(cabinet, "Use", player);
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Quiet"));
            Assert.That(Get<bool>(player, "HidingProtected"), Is.True); Assert.That(draws, Is.Zero);
            int entry = Get<int>(player, "HidingEntryId");
            Call(player, "Hide", cabinet, Get<Transform>(cabinet, "inside").position, outside);
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(entry));
            Call(shell, "Pause"); Call(cabinet, "Use", player);
            Assert.Throws<InvalidOperationException>(() => Call(player, "CaptureProgress"));
            yield return Delay(.2f);
            Assert.That(Get<bool>(player, "Hidden"), Is.True); Assert.That(draws, Is.Zero);
            Call(shell, "Resume");
            var blocker = Cube("CloudQA cabinet blocked exit", outside + Vector3.up * .9f, new Vector3(.7f, 1.8f, .7f));
            Physics.SyncTransforms(); Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True); Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(entry));
            blocker.SetActive(false); Physics.SyncTransforms(); Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            otherFloor.gameObject.SetActive(false); disabled.gameObject.SetActive(false);
            Call(cabinet, "Use", player);
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(entry + 1)); Assert.That(draws, Is.Zero);
            Assert.That(Get<int>(player, "HidingRolls"), Is.Zero);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MultipleOccludedChasersShareOneSurvivalRollAndOnlyANewEntryCanDrawDeath()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var inside = Get<Transform>(cabinet, "inside").position;
            var outside = Get<Transform>(cabinet, "outside").position; PlacePlayer(outside);
            var first = CabinetChaser(cabinet); var second = CabinetChaser(cabinet);
            var outward = outside - inside; outward.y = 0;
            var cover = Cube("CloudQA cabinet pursuit cover", Vector3.Lerp(outside, first.transform.position, .5f) + Vector3.up * 1.5f,
                new Vector3(2, 3, .2f)); cover.transform.rotation = Quaternion.LookRotation(outward); Physics.SyncTransforms();
            Assert.That((bool)Call(first, "CanSeePlayer"), Is.False); Assert.That((bool)Call(second, "CanSeePlayer"), Is.False);
            int draws = 0;
            Set(player, "HidingRandomSample", (Func<float>)(() => { draws++; return draws == 1 ? .1f : .9f; }));
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Survived"));
            Assert.That(Get<bool>(first, "SawHiding"), Is.False); Assert.That(Get<bool>(second, "SawHiding"), Is.False);
            Assert.That(draws, Is.EqualTo(1)); Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(1));
            for (int repeat = 0; repeat < 20; repeat++)
            {
                Call(first, "ObserveHiding", outside); Call(second, "ObserveHiding", outside);
                Call(player, "Hide", cabinet, inside, outside); yield return null;
            }
            Call(shell, "Pause");
            Assert.Throws<InvalidOperationException>(() => Call(player, "CaptureProgress"));
            yield return Delay(.25f); Call(shell, "Resume"); yield return Delay(1);
            Assert.That(Get<bool>(session, "Finished"), Is.False); Assert.That(draws, Is.EqualTo(1));
            Assert.That(Get<int>(first, "AttacksStarted"), Is.Zero); Assert.That(Get<int>(second, "AttacksStarted"), Is.Zero);
            Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Set(first, "state", "Chase"); Set(second, "state", "Chase");
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True, "Death selection must follow successful real entry");
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Defeated"));
            Assert.That(Get<bool>(session, "Finished"), Is.True); Assert.That(Get<bool>(session, "Escaped"), Is.False);
            Assert.That(draws, Is.EqualTo(2)); Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(2));
            Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(2));
            Assert.That(Get<string>(session, "DefeatHint"), Does.Contain("생존 75% / 사망 25%"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CabinetEntryDecisionSurvivesRealSchoolAndCorridorCheckpointRoundTrips()
        {
            // Both mode schemas use PlayerMotor.Progress. Hidden suspension stays explicitly blocked.
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school");
            IsolateThreats(); ((Behaviour)player).enabled = false;
            var cabinet = CheckpointCabinetFixture(false); PlacePlayer(Get<Transform>(cabinet, "outside").position);
            Assert.That(Get<string>(cabinet, "stableId"), Is.EqualTo("campus-hide-u-class-a"));
            var enemy = CabinetChaser(cabinet); int draws = 0;
            Set(player, "HidingRandomSample", (Func<float>)(() => { draws++; return .2f; }));
            AssertCabinetEntryReady(cabinet, enemy);
            Call(cabinet, "Use", player); Assert.That(draws, Is.EqualTo(1));
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Call(shell, "Pause"); Assert.Throws<InvalidOperationException>(() => Call(session, "CaptureChapterCheckpoint"));
            Call(shell, "Resume"); LeaveCabinetForCheckpoint(cabinet, enemy); Call(shell, "Pause");
            var school = Call(session, "CaptureChapterCheckpoint"); var saved = Get<object>(school, "player");
            var parsed = SchoolCheckpointCopy(school);
            Assert.That(Get<int>(Get<object>(parsed, "player"), "hidingEntry"), Is.EqualTo(1));
            Assert.That(Get<object>(Get<object>(parsed, "player"), "hidingOutcome").ToString(), Is.EqualTo("Survived"));
            Call(player, "RestoreChapterProgress", saved);
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(1)); Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(1));
            Assert.That(draws, Is.EqualTo(1), "Restore consumed RNG");
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return Delay(.15f);
            ((Behaviour)player).enabled = false;
            cabinet = CheckpointCabinetFixture(true);
            PlacePlayer(Get<Transform>(cabinet, "outside").position); enemy = CabinetChaser(cabinet); draws = 0;
            Set(player, "HidingRandomSample", (Func<float>)(() => { draws++; return .2f; }));
            AssertCabinetEntryReady(cabinet, enemy);
            Call(cabinet, "Use", player); Call(shell, "Pause");
            Assert.That(Get<bool>(player, "Hidden"), Is.True); Assert.That(draws, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Call(session, "CaptureCheckpoint"));
            Call(shell, "Resume"); LeaveCabinetForCheckpoint(cabinet, enemy); Call(shell, "Pause");
            var corridor = Call(session, "CaptureCheckpoint"); saved = Get<object>(CheckpointCopy(corridor), "player");
            Assert.That(Get<int>(saved, "hidingRolls"), Is.EqualTo(1));
            Call(player, "RestoreProgress", saved);
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(1));
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Survived"));
            Assert.That(draws, Is.EqualTo(1), "Corridor restore consumed RNG");
            Call(shell, "Resume"); enemy.gameObject.SetActive(true); Set(enemy, "state", "Chase");
            AssertCabinetEntryReady(cabinet, enemy);
            Call(cabinet, "Use", player); Assert.That(draws, Is.EqualTo(2));
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(2), "A new post-restore entry reused the old draw");
        }
    }
}
