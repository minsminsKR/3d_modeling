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
        static float CabinetFlatDistance(Vector3 first, Vector3 second)
        { first.y = second.y = 0; return Vector3.Distance(first, second); }
        string CabinetAttackDiagnostics(Component cabinet, Component enemy) =>
            "enemy=" + enemy.transform.position.ToString("F3") + ", entrance=" +
            Get<Transform>(cabinet, "outside").position.ToString("F3") + ", state=" + Get<object>(enemy, "state") +
            ", witness=" + Get<bool>(enemy, "SawHiding") + ", attacks=" + Get<int>(enemy, "AttacksStarted") +
            ", progress=" + Get<float>(enemy, "AttackWindup") + ", finished=" + Get<bool>(session, "Finished");
        IEnumerator CabinetMustStayAliveFor(float gameSeconds)
        {
            float deadline = Time.time + gameSeconds, timeout = Time.realtimeSinceStartup + gameSeconds + 5;
            while (Time.time < deadline && Time.realtimeSinceStartup < timeout)
            {
                Assert.That(Get<bool>(session, "Finished"), Is.False, "An old or remote strike bypassed the fresh cabinet warning");
                yield return null;
            }
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(deadline), "The cabinet warning stalled or paused unexpectedly");
        }
        IEnumerator RiskyCabinetAttackReachesOriginalEntrance(Component cabinet, Component enemy)
        {
            var entrance = Get<Transform>(cabinet, "outside").position;
            var start = enemy.transform.position;
            Assert.That(CabinetFlatDistance(start, entrance), Is.GreaterThan(2.5f));
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True, "This entry must be physically witnessed before hiding");
            var route = new NavMeshPath();
            Assert.That(enemy.GetComponent<NavMeshAgent>().CalculatePath(entrance, route), Is.True);
            Assert.That(route.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Original cabinet entrance lacks a real approach route");
            Set(player, "HidingRandomSample", (Func<float>)(() => .95f));
            Set(enemy, "chaseSpeed", 3.5f);
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Defeated"));
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.False, "A risky roll cannot kill at entry before physical approach");
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False);
            float timeout = Time.realtimeSinceStartup + 12;
            while (Get<int>(enemy, "AttacksStarted") == 0 && Time.realtimeSinceStartup < timeout)
            {
                Assert.That(Get<bool>(session, "Finished"), Is.False, "Remote pursuit killed a hidden player before reaching the cabinet");
                if (CabinetFlatDistance(enemy.transform.position, entrance) >= .85f)
                    Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False, "Door warning started outside its physical reach");
                yield return null;
            }
            Assert.That(Get<int>(enemy, "AttacksStarted"), Is.EqualTo(1), CabinetAttackDiagnostics(cabinet, enemy));
            Assert.That(Get<bool>(enemy, "AttackActive"), Is.True);
            Assert.That(CabinetFlatDistance(start, enemy.transform.position), Is.GreaterThan(1.5f), "Enemy never physically approached");
            Assert.That(CabinetFlatDistance(enemy.transform.position, entrance), Is.LessThan(.85f));
            Assert.That((bool)Call(RequireType("EnemyNavigation"), "SameActorFloor", enemy.GetComponent<NavMeshAgent>(),
                entrance, Get<float>(enemy, "HomeFloorY")), Is.True);
            Assert.That((bool)Call(RequireType("EnemyNavigation"), "ClearSight", enemy.transform.position + Vector3.up * 1.1f,
                entrance + Vector3.up * 1.1f, null), Is.True);
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.True, "Arrival must give a cabinet door warning before capture");
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
        public IEnumerator MultipleOccludedChasersShareOneSurvivalRollAndFailedNewEntryCannotKillRemotely()
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
            Assert.That(Get<bool>(player, "Hidden"), Is.True, "Risk selection must follow successful real entry");
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Defeated"));
            Assert.That(Get<bool>(session, "Finished"), Is.False, "The failed roll cannot defeat a player whose entry was unseen");
            Assert.That(Get<bool>(first, "SawHiding"), Is.False); Assert.That(Get<bool>(second, "SawHiding"), Is.False);
            Assert.That(draws, Is.EqualTo(2)); Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(2));
            Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(2));
            yield return CabinetMustStayAliveFor(1.8f);
            Assert.That(Get<int>(first, "AttacksStarted"), Is.Zero); Assert.That(Get<int>(second, "AttacksStarted"), Is.Zero);
            Assert.That(draws, Is.EqualTo(2), "An unseen failed entry was rerolled during hidden frames");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WitnessedFailedCabinetEntryRequiresActualApproachAndFullDoorWarning()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); PlacePlayer(Get<Transform>(cabinet, "outside").position);
            var enemy = CabinetChaser(cabinet);
            yield return RiskyCabinetAttackReachesOriginalEntrance(cabinet, enemy);
            Call(shell, "Pause");
            float pausedProgress = Get<float>(enemy, "AttackWindup"), pausedTime = Time.time;
            var pausedPosition = enemy.transform.position;
            yield return Delay(.25f);
            Assert.That(Get<float>(enemy, "AttackWindup"), Is.EqualTo(pausedProgress));
            Assert.That(Time.time, Is.EqualTo(pausedTime));
            Assert.That(enemy.transform.position, Is.EqualTo(pausedPosition));
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(shell, "Resume");
            float began = Time.time;
            float remainingWarning = .75f * (1 - Get<float>(enemy, "AttackWindup"));
            Assert.That(remainingWarning, Is.GreaterThan(.5f), "Physical arrival did not begin a fresh complete warning");
            yield return CabinetMustStayAliveFor(remainingWarning - .1f);
            yield return Wait(() => Get<bool>(session, "Finished"), 3, "Reached and witnessed cabinet never resolved its actual strike",
                () => CabinetAttackDiagnostics(cabinet, enemy));
            Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(remainingWarning - .06f), "Capture shortened the actual .75 second warning");
            Assert.That(Get<bool>(session, "Escaped"), Is.False);
            Assert.That(Get<string>(session, "DefeatHint"), Does.Contain("은신 성공 75% / 발각 위험 25%"));
            Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(1));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WitnessedCabinetStrikeCannotCommitThroughNewOpaqueCover()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var entrance = Get<Transform>(cabinet, "outside").position;
            PlacePlayer(entrance); var enemy = CabinetChaser(cabinet);
            yield return RiskyCabinetAttackReachesOriginalEntrance(cabinet, enemy);
            // Explicit dynamic cover fixture added after genuine approach. It tests
            // the strike's live sight recheck; it never alters authored cabinet geometry.
            var cover = Cube("CloudQA cabinet strike occlusion", Vector3.Lerp(enemy.transform.position, entrance, .5f) + Vector3.up * 1.1f,
                new Vector3(2, 2.2f, .15f));
            var direction = entrance - enemy.transform.position; direction.y = 0;
            cover.transform.rotation = Quaternion.LookRotation(direction); Physics.SyncTransforms();
            Assert.That((bool)Call(RequireType("EnemyNavigation"), "ClearSight", enemy.transform.position + Vector3.up * 1.1f,
                entrance + Vector3.up * 1.1f, null), Is.False);
            yield return CabinetMustStayAliveFor(1.05f);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<int>(enemy, "AttacksStarted"), Is.EqualTo(1));
            Assert.That(Get<float>(enemy, "AttackRecovery"), Is.GreaterThan(0), "Covered strike never reached its real impact/recovery frame");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LeavingAndReenteringSameCabinetCannotInheritPreviousDoorStrike()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); PlacePlayer(Get<Transform>(cabinet, "outside").position);
            var enemy = CabinetChaser(cabinet);
            yield return RiskyCabinetAttackReachesOriginalEntrance(cabinet, enemy);
            yield return Wait(() => Get<float>(enemy, "AttackWindup") > .65f, 3, "Original cabinet strike never advanced to late windup");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            int firstEntry = Get<int>(player, "HidingEntryId");
            Assert.That(CabinetExitBlockers(cabinet), Is.Empty, "Original cabinet exit is obstructed: " + CabinetExitBlockerDetails(cabinet));
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True, "Fresh same-cabinet entry must also be witnessed");
            Call(cabinet, "Use", player);
            Assert.That(Get<int>(player, "HidingEntryId"), Is.EqualTo(firstEntry + 1));
            Assert.That(Get<bool>(enemy, "AttackActive"), Is.False, "Fresh entry retained the previous cabinet's nearly resolved strike");
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False, "Fresh entry inherited the previous door warning");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            yield return Wait(() => Get<int>(enemy, "AttacksStarted") == 2, 3, "New witnessed entry did not start its own physical door warning");
            Assert.That(Get<float>(enemy, "AttackWindup"), Is.LessThan(.2f), "New entry began with an advanced strike clock");
            yield return CabinetMustStayAliveFor(.45f);
            Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(2));
            yield return Wait(() => Get<bool>(session, "Finished"), 3, "New valid cabinet attack failed to complete");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CabinetEntryCancelsPendingVisibleBodyStrikeAndStartsFreshDoorWarning()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing real navigation");
            ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var entrance = Get<Transform>(cabinet, "outside").position;
            var inside = Get<Transform>(cabinet, "inside").position;
            var outward = entrance - inside; outward.y = 0; outward.Normalize();
            PlacePlayer(entrance);
            Assert.That(NavMesh.SamplePosition(entrance + outward * .7f, out var anchor, .2f, NavMesh.AllAreas), Is.True);
            Assert.That(CabinetFlatDistance(anchor.position, entrance), Is.LessThan(.85f));
            var enemy = StalkerAt(anchor.position); Set(enemy, "state", "Chase");
            enemy.transform.rotation = Quaternion.LookRotation(entrance - anchor.position); Physics.SyncTransforms();
            yield return Wait(() => Get<int>(enemy, "AttacksStarted") == 1 && Get<float>(enemy, "AttackWindup") > .65f,
                3, "Nearby visible-body strike did not reach late windup");
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Set(player, "HidingRandomSample", (Func<float>)(() => .95f));
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.True);
            Assert.That(Get<bool>(enemy, "AttackActive"), Is.False, "Visible-body attack was carried inside the cabinet");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            yield return Wait(() => Get<int>(enemy, "AttacksStarted") == 2, 3, "Physical cabinet approach did not start a fresh warning");
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.True);
            yield return CabinetMustStayAliveFor(.45f);
            Assert.That(Get<int>(player, "HidingRolls"), Is.EqualTo(1));
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
