using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // These fixtures isolate gate, physical geometry, storage and chapter
        // integration. Controlled placement and isolated threats do not certify
        // survival balance or a player's ability to complete the seeded route.
        void AltarIsolateFixture(Component run)
        {
            ((Behaviour)run).enabled = false; // Prevent threshold-based rerelease.
            IsolateThreats();
        }

        Component AltarOffering(Component run)
        {
            var chamber = Get<Component>(run, "AltarChamber");
            Assert.That(chamber, Is.Not.Null);
            Assert.That(Get<bool>(chamber, "Prepared"), Is.True);
            var offering = Get<Component>(chamber, "Offering");
            Assert.That(offering, Is.Not.Null);
            Assert.That(Get<object>(offering, "kind").ToString(), Is.EqualTo("Exit"));
            Assert.That(Get<string>(offering, "stableId"), Is.EqualTo("corridor-offering"));
            return offering;
        }

        static string[] AltarColliderSnapshot(Transform root)
        {
            string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            string Point(Vector3 value) => Float(value.x) + "," + Float(value.y) + "," + Float(value.z);
            return root.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.enabled && !collider.isTrigger)
                .Select(collider => collider.GetType().Name + ":" + collider.name + ":" +
                    Point(collider.bounds.center) + ":" + Point(collider.bounds.size))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        void AltarAssertReachable(Component run)
        {
            Vector3 entrance = (Vector3)Call(run, "CellPosition", 0);
            Vector3 approach = Get<Vector3>(run, "AltarApproach");
            Assert.That(NavMesh.SamplePosition(entrance, out var start, .35f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(approach, out var end, .35f, NavMesh.AllAreas), Is.True,
                "The real offering approach is absent from the furniture-inclusive NavMesh");
            Assert.That(Vector3.Distance(end.position, approach), Is.LessThan(.35f));
            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
            string diagnostic = calculated && path.status == NavMeshPathStatus.PathComplete ? string.Empty :
                AltarRouteDiagnostic(run, path, start.position, end.position);
            if (!string.IsNullOrEmpty(diagnostic)) TestContext.Out.WriteLine(diagnostic);
            Assert.That(calculated, Is.True, diagnostic);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                "The exterior chamber/connector is disconnected from the corridor entrance\n" + diagnostic);
            var pose = Call(player, "CaptureProgress");
            Set(pose, "position", approach); Set(pose, "crouched", false);
            Assert.That((bool)Call(player, "CanRestoreProgress", pose), Is.True,
                "The offering approach lacks standing capsule clearance or physical floor support");
        }

        string AltarRouteDiagnostic(Component run, NavMeshPath path, Vector3 start, Vector3 end)
        {
            var room = Get<Transform>(run, "AltarRoomRoot");
            var layout = Get<object>(run, "Layout");
            var report = new StringBuilder();
            report.AppendLine("ALTAR_ROUTE_DIAGNOSTIC seed=" + Get<int>(run, "Seed") +
                " cell=" + Get<int>(layout, "AltarCell") + " direction=" + Get<int>(layout, "AltarDirection") +
                " roomPosition=" + room.position.ToString("F3") + " roomEuler=" + room.eulerAngles.ToString("F3") +
                " roomQuaternion=" + room.rotation.ToString("F4"));
            report.AppendLine("route status=" + path.status + " start=" + start.ToString("F3") +
                " destination=" + end.ToString("F3") + " corners=" + path.corners.Length +
                " endpoint=" + (path.corners.Length == 0 ? "none" : path.corners.Last().ToString("F3")));
            report.AppendLine("route corners=" + string.Join(" -> ", path.corners.Select(point => point.ToString("F3"))));
            float[] stations = { -12, -10, -9, -8, -6, -5, -4, -2, 0, 1.1f };
            var controller = player.GetComponent<CharacterController>();
            float radius = controller.radius - .02f;
            float half = Mathf.Max(0, controller.height * .5f - controller.radius);
            var points = stations.Select(z => room.TransformPoint(new Vector3(0, .03f, z))).ToArray();
            for (int index = 0; index < stations.Length; index++)
            {
                Vector3 point = points[index];
                bool sampled = NavMesh.SamplePosition(point, out var sample, .35f, NavMesh.AllAreas);
                report.Append("centerline z=" + stations[index].ToString("R", CultureInfo.InvariantCulture) +
                    " world=" + point.ToString("F3") + " sampled=" + sampled);
                if (sampled)
                {
                    var branch = new NavMeshPath();
                    bool branchCalculated = NavMesh.CalculatePath(start, sample.position, NavMesh.AllAreas, branch);
                    report.Append(" sample=" + sample.position.ToString("F3") + " offset=" + sample.distance.ToString("F3") +
                        " fromEntranceCalculated=" + branchCalculated + " fromEntranceStatus=" + branch.status);
                }
                report.AppendLine();
                // Physics detail is emitted only for the failing partial route.
                // These probes never move or open any production geometry.
                if (path.status != NavMeshPathStatus.PathPartial) continue;
                Vector3 center = point + controller.center;
                var overlaps = Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half,
                    radius, ~0, QueryTriggerInteraction.Ignore).Where(collider => !collider.transform.IsChildOf(player.transform));
                foreach (var collider in overlaps.OrderBy(value => value.name, StringComparer.Ordinal))
                    report.AppendLine("  standingOverlap name=" + collider.name + " type=" + collider.GetType().Name +
                        " layer=" + collider.gameObject.layer + " bounds=" + collider.bounds);
                if (index + 1 >= stations.Length) continue;
                Vector3 segment = points[index + 1] - point;
                var hits = Physics.CapsuleCastAll(center - Vector3.up * half, center + Vector3.up * half,
                    radius, segment.normalized, segment.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => !hit.collider.transform.IsChildOf(player.transform)).OrderBy(hit => hit.distance);
                foreach (var hit in hits)
                    report.AppendLine("  standingSweep toZ=" + stations[index + 1].ToString("R", CultureInfo.InvariantCulture) +
                        " hit=" + hit.collider.name + " layer=" + hit.collider.gameObject.layer +
                        " distance=" + hit.distance.ToString("F3") + " point=" + hit.point.ToString("F3") +
                        " bounds=" + hit.collider.bounds);
            }
            return report.ToString();
        }

        void AltarAimFromApproach(Component run)
        {
            PlacePlayer(Get<Vector3>(run, "AltarApproach"));
            var camera = Get<Camera>(player, "eyes");
            var offering = AltarOffering(run);
            var collider = offering.GetComponentsInChildren<Collider>()
                .Where(value => value.enabled && !value.isTrigger)
                .OrderBy(value => Vector3.Distance(value.bounds.center, camera.transform.position)).First();
            Vector3 delta = collider.bounds.center - camera.transform.position;
            var pose = Call(player, "CaptureProgress");
            Set(pose, "yaw", Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360));
            Set(pose, "pitch", -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
            Call(player, "RestoreProgress", pose);
            Physics.SyncTransforms();
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator CorridorAltarRoomIsDistantReachableAndRestoresTheSamePhysicalSeededChamber()
        {
            Call(session, "CreateCorridor", 73); Begin();
            var run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            yield return null; yield return null;
            var layout = Get<object>(run, "Layout");
            Assert.That(Get<int>(layout, "Version"), Is.EqualTo(3));
            int cell = Get<int>(layout, "AltarCell");
            Assert.That(cell, Is.InRange(1, 80), "The offering room must not occupy the entrance");
            Assert.That(cell % 9 == 8 || cell / 9 == 8, Is.True);
            Assert.That(Get<int[]>(layout, "Distance")[cell], Is.GreaterThanOrEqualTo(6));
            var room = Get<Transform>(run, "AltarRoomRoot");
            Assert.That(room, Is.Not.Null);
            Assert.That(Vector3.Distance(room.position, (Vector3)Call(run, "CellPosition", 0)), Is.GreaterThan(30));
            var exits = run.GetComponentsInChildren(RequireType("Interactable"), true).Cast<Component>()
                .Where(item => Get<object>(item, "kind").ToString() == "Exit").ToArray();
            Assert.That(exits, Has.Length.EqualTo(1)); Assert.That(exits[0], Is.SameAs(AltarOffering(run)));
            AltarAssertReachable(run);
            var colliders = AltarColliderSnapshot(room);
            Assert.That(colliders.Length, Is.GreaterThan(5), "Chamber shell did not contribute physical geometry");
            int[] connections = Get<int[]>(layout, "Connections").ToArray();
            Vector3 approach = Get<Vector3>(run, "AltarApproach");
            foreach (var memory in CheckpointItems("CorridorMemory").Take(4)) Call(memory, "Use", player);
            AltarAimFromApproach(run);
            yield return Wait(() => Get<Component>(player, "Focus") == AltarOffering(run), 3,
                "The reachable offering cannot be focused through the production camera ray");
            var camera = Get<Camera>(player, "eyes");
            var frame = SchoolCameraFrame(camera, out _, out _, out _);
            try
            {
                CloudExperienceTests.Artifact("corridor-altar-native-camera.png", frame.EncodeToPNG());
                Assert.That(CloudExperienceTests.HasContent(frame), Is.True);
            }
            finally { Object.Destroy(frame); }
            Call(shell, "Pause");
            var saved = Call(session, "CaptureCheckpoint");
            Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(3));
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(4));
            Vector3 position = Get<Vector3>(Get<object>(saved, "player"), "position");
            Assert.That(position.x > 251 || position.z > 251, Is.True,
                "Fixture must exercise the expanded checkpoint bounds in the exterior chamber");
            yield return RestoreCheckpointInFreshScene(saved);
            run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            Assert.That(Get<int>(Get<object>(run, "Layout"), "Version"), Is.EqualTo(3));
            Assert.That(Get<int[]>(Get<object>(run, "Layout"), "Connections"), Is.EqualTo(connections));
            Assert.That(Get<int>(Get<object>(run, "Layout"), "AltarCell"), Is.EqualTo(cell));
            Assert.That(Get<Vector3>(run, "AltarApproach"), Is.EqualTo(approach));
            Assert.That(AltarColliderSnapshot(Get<Transform>(run, "AltarRoomRoot")), Is.EqualTo(colliders));
            Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(.001f));
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(4));
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            AltarAssertReachable(run);
            Begin(); AltarAimFromApproach(run);
            yield return Wait(() => Get<Component>(player, "Focus") == AltarOffering(run), 3,
                "Restored offering lost its physical interaction");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CorridorAltarRequiresAllFiveMemoriesAndRealOfferingInputContinuesIntoSchoolWithoutConsumingItsSave()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            // Produce a valid independent school save before starting the corridor.
            Call(shell, "BeginChapter"); yield return null; Call(shell, "Pause");
            var school = Call(session, "CaptureChapterCheckpoint");
            Assert.That((bool)Call(Get<object>(session, "ChapterSuspension"), "Save", school), Is.True);
            string schoolPath = Path.Combine(directory, "chapter-suspend-v1.json");
            string schoolBytes = File.ReadAllText(schoolPath);
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateCorridor", 211); Begin();
            var run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            var offering = AltarOffering(run); var memories = CheckpointItems("CorridorMemory");
            AltarAimFromApproach(run);
            for (int count = 0; count < 5; count++)
            {
                Call(offering, "Use", player);
                Assert.That(Get<bool>(session, "Finished"), Is.False, "Offering accepted only " + count + " memories");
                Assert.That(Get<bool>(shell, "IsReloading"), Is.False);
                Assert.That(RecoveryPage, Is.EqualTo("Playing"));
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(count));
                Call(memories[count], "Use", player);
            }
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(5));
            Assert.That(Get<string>(session, "Objective"), Does.Contain("제단"));
            Assert.That(Get<string>(offering, "DisplayLabel"), Does.Contain("기억"),
                "The offering prompt must describe returning memories");
            Assert.That(Get<string>(offering, "DisplayLabel"), Does.Not.Contain("봉인된 회랑 문"),
                "Legacy exit-door text leaked into the offering interaction");
            PlacePlayer((Vector3)Call(run, "CellPosition", 0)); Call(session, "TryEscape");
            Assert.That(Get<bool>(session, "Finished"), Is.False, "Entrance position bypassed the offering proximity gate");
            AltarAimFromApproach(run); Call(shell, "Pause"); Call(offering, "Use", player);
            Assert.That(Get<bool>(session, "Finished"), Is.False, "Paused offering accepted gameplay input");
            Call(shell, "Resume");
            yield return Wait(() => Get<Component>(player, "Focus") == offering, 3,
                "Actual camera focus cannot reach the five-memory offering");
            var errors = new List<string>();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                yield return CaptureMenu(One("GameShellView"), "corridor-altar-ready-ui-" + size.x + "x" + size.y + ".png", size.x, size.y, errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            var previousView = One("GameShellView");
            var previousCapture = Get<RenderTexture>(previousView, "CaptureTarget");
            Assert.That(previousCapture && previousCapture.IsCreated(), Is.True);
            previous = session;
            Keys(Key.E);
            yield return Wait(() => RecoveryPage == "ChapterTransition", 1,
                "Real E input did not activate the focused five-memory offering");
            Keys();
            // Observe the short transition before waiting for its automatic reload.
            Assert.That(Get<bool>(session, "Escaped"), Is.True);
            Assert.That(RecoveryPage, Is.EqualTo("ChapterTransition"));
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            Call(shell, "BeginSchoolFromOffering"); // Reentry must not queue another reload.
            yield return RecoveryRebind(previous);
            Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            Assert.That(Get<bool>(session, "ChapterMode"), Is.True);
            Assert.That(Get<bool>(session, "CorridorMode"), Is.False);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.Zero);
            Assert.That(Get<int>(session, "TotalRecords"), Is.EqualTo(5));
            Assert.That(Get<Component>(session, "Corridor"), Is.Null);
            var chapter = Get<Component>(session, "Chapter");
            Assert.That(Get<Component[]>(chapter, "Memories").Select(memory => Get<string>(memory, "stableId")),
                Is.EqualTo(Enumerable.Range(0, 5).Select(index => "chapter-memory-" + index)));
            Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(2));
            Assert.That(File.ReadAllText(schoolPath), Is.EqualTo(schoolBytes),
                "Automatic continuation consumed or rewrote the independent school suspension slot");
            Assert.That(File.Exists(schoolPath + ".used"), Is.False);
            // A fresh view creates CaptureTarget only when SetCaptureSize is
            // requested. Zero global capture objects immediately after reload is
            // expected, so test the actual old/new view lifecycle instead.
            Assert.That(previousView == null, Is.True, "Chapter transition retained the old runtime UI view");
            Assert.That(previousCapture == null, Is.True, "Chapter transition retained the old UI capture texture");
            yield return RecoveryUiReady();
            var chapterView = One("GameShellView");
            var chapterCapture = Get<RenderTexture>(chapterView, "CaptureTarget");
            Assert.That(chapterCapture && chapterCapture.IsCreated(), Is.True,
                "New school view did not create its requested capture target");
            Assert.That(chapterCapture.width, Is.EqualTo(1280)); Assert.That(chapterCapture.height, Is.EqualTo(720));
            Assert.That(Components("GameShell").Length, Is.EqualTo(1));
            Assert.That(Components("PlayerMotor").Length, Is.EqualTo(1));
            Assert.That(Components("DetectionFeedback").Length, Is.EqualTo(1));
            yield return Delay(1.5f);
            Assert.That(One("GameSession"), Is.SameAs(session), "Offering reentry triggered a second chapter reload");
            Assert.That(Get<bool>(session, "ChapterMode"), Is.True);
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator CorridorAltarFailedChapterReloadKeepsVisibleErrorAndSchoolSaveAndClearsTransitionOverrides()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Call(shell, "BeginChapter"); yield return null; Call(shell, "Pause");
            var school = Call(session, "CaptureChapterCheckpoint");
            Assert.That((bool)Call(Get<object>(session, "ChapterSuspension"), "Save", school), Is.True);
            string schoolPath = Path.Combine(directory, "chapter-suspend-v1.json");
            string schoolBytes = File.ReadAllText(schoolPath);
            string schoolToken = Get<string>(school, "token");
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateCorridor", 73); Begin();
            var run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            foreach (var memory in CheckpointItems("CorridorMemory")) Call(memory, "Use", player);
            AltarAimFromApproach(run);
            var offering = AltarOffering(run);
            yield return Wait(() => Get<Component>(player, "Focus") == offering, 3,
                "Failure fixture could not focus the actual five-memory offering");
            yield return RecoveryUiReady(); var view = One("GameShellView");
            var original = session.gameObject.scene;
            var temporary = SceneManager.CreateScene("Owned altar transition unloadable scene");
            try
            {
                // Only the owned runtime host moves. The real loaded school,
                // player, colliders and camera stay intact, but its host scene
                // has no streamed path, exercising the production failure gate.
                SceneManager.MoveGameObjectToScene(session.gameObject, temporary);
                Assert.That(session.gameObject.scene, Is.EqualTo(temporary));
                Assert.That(session.gameObject.scene.path, Is.Empty);
                Keys(Key.E);
                yield return Wait(() => RecoveryPage == "ChapterTransition", 1,
                    "Real E input did not activate the offering in the unloadable host scene");
                Keys();
                Assert.That(Get<bool>(session, "Escaped"), Is.True);
                yield return Wait(() => RecoveryPage == "Result" && !Get<bool>(shell, "IsReloading"), 5,
                    "Failed chapter reload did not return to the usable result screen");
                string error = Get<string>(shell, "ReloadError");
                Assert.That(error, Does.Contain("현재 장면을 다시 열 수 없습니다"),
                    "Transition fallback erased the actual missing-scene reload diagnostic");
                Assert.That(Get<bool>(session, "ChapterMode"), Is.False);
                Assert.That(Get<bool>(session, "Finished"), Is.True);
                Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
                yield return Wait(() => Get<UnityEngine.UIElements.VisualElement>(view, "Root")
                    .Query<UnityEngine.UIElements.Label>().ToList().Any(label => label.text == error), 3,
                    "Preserved reload error was not rendered on the result screen");
                var root = Get<UnityEngine.UIElements.VisualElement>(view, "Root");
                Assert.That(root.Q<UnityEngine.UIElements.Button>("restart").enabledInHierarchy, Is.True);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                foreach (string name in new[] { "chapterFromOffering", "chapterRestart", "corridorRestart" })
                {
                    var field = shell.GetType().GetField(name, flags);
                    Assert.That(field, Is.Not.Null);
                    Assert.That((bool)field.GetValue(null), Is.False, "Failed transition left a stale static override: " + name);
                }
                flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                foreach (string name in new[] { "requestChapterOnReload", "requestCorridorOnReload" })
                {
                    var field = shell.GetType().GetField(name, flags);
                    Assert.That(field, Is.Not.Null);
                    Assert.That((bool)field.GetValue(shell), Is.False, "Failed transition left a stale mode request: " + name);
                }
                Assert.That(File.ReadAllText(schoolPath), Is.EqualTo(schoolBytes));
                Assert.That(File.Exists(schoolPath + ".used"), Is.False);
                var slot = Get<object>(session, "ChapterSuspension");
                Assert.That(Get<bool>(slot, "HasRun"), Is.True);
                Assert.That(Get<string>(Get<object>(slot, "Snapshot"), "token"), Is.EqualTo(schoolToken));
            }
            finally
            {
                Keys();
                if (session && original.isLoaded) SceneManager.MoveGameObjectToScene(session.gameObject, original);
                if (temporary.isLoaded) SceneManager.UnloadSceneAsync(temporary);
            }
            yield return Wait(() => !temporary.isLoaded, 10, "Owned failure scene did not unload after restoring the runtime host");
            Assert.That(session.gameObject.scene, Is.EqualTo(original));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorAltarLegacyVersionOneJsonRestoresAndKeepsItsSealedEntranceResult()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            var saved = JsonUtility.FromJson(File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"), RequireType("CorridorCheckpoint"));
            Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(1));
            Call(session, "CreateCorridorForCheckpoint", saved); Call(session, "ApplyCheckpoint", saved);
            var run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            Assert.That(Get<int>(Get<object>(run, "Layout"), "Version"), Is.EqualTo(1));
            Assert.That(Get<Transform>(run, "AltarRoomRoot"), Is.Null);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(2));
            Assert.That(player.transform.position, Is.EqualTo(Get<Vector3>(Get<object>(saved, "player"), "position")));
            Begin();
            foreach (var memory in CheckpointItems("CorridorMemory").Where(memory => memory.gameObject.activeSelf)) Call(memory, "Use", player);
            var exit = run.GetComponentsInChildren(RequireType("Interactable"), true).Cast<Component>()
                .Single(item => Get<object>(item, "kind").ToString() == "Exit");
            Assert.That(exit.name, Is.EqualTo("Sealed entrance"));
            PlacePlayer((Vector3)Call(run, "CellPosition", 0)); Call(exit, "Use", player);
            Assert.That(Get<bool>(session, "Escaped"), Is.True); Assert.That(RecoveryPage, Is.EqualTo("Result"));
            yield return Delay(1.5f);
            Assert.That(Get<bool>(session, "ChapterMode"), Is.False); Assert.That(RecoveryPage, Is.EqualTo("Result"));
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator CorridorAltarLegacyVersionTwoRoundTripKeepsOriginalGeometryAndEntranceFlow()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            // The old fixture supplies a valid schema to request version two;
            // its version-one poses are not applied to different geometry.
            var request = JsonUtility.FromJson(File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"), RequireType("CorridorCheckpoint"));
            Set(request, "simulationVersion", 2);
            Call(session, "CreateCorridorForCheckpoint", request); Begin();
            var run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            Assert.That(Get<int>(Get<object>(run, "Layout"), "Version"), Is.EqualTo(2));
            Assert.That(Get<Transform>(run, "AltarRoomRoot"), Is.Null);
            foreach (var memory in CheckpointItems("CorridorMemory").Take(2)) Call(memory, "Use", player);
            Call(shell, "Pause"); var saved = Call(session, "CaptureCheckpoint");
            Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(2));
            string[] ids = Get<Array>(saved, "doors").Cast<object>().Select(door => Get<string>(door, "id")).ToArray();
            int[] connections = Get<int[]>(Get<object>(run, "Layout"), "Connections").ToArray();
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreateCorridorForCheckpoint", saved); Call(session, "ApplyCheckpoint", CheckpointCopy(saved));
            run = Get<Component>(session, "Corridor"); AltarIsolateFixture(run);
            Assert.That(Get<int>(Get<object>(run, "Layout"), "Version"), Is.EqualTo(2));
            Assert.That(Get<Transform>(run, "AltarRoomRoot"), Is.Null);
            Assert.That(Get<int[]>(Get<object>(run, "Layout"), "Connections"), Is.EqualTo(connections));
            Assert.That(CheckpointItems("Door").Select(door => Get<string>(door, "stableId")), Is.EqualTo(ids));
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(2));
            Begin();
            foreach (var memory in CheckpointItems("CorridorMemory").Where(memory => memory.gameObject.activeSelf)) Call(memory, "Use", player);
            PlacePlayer((Vector3)Call(run, "CellPosition", 0)); Call(session, "TryEscape");
            Assert.That(Get<bool>(session, "Escaped"), Is.True); Assert.That(RecoveryPage, Is.EqualTo("Result"));
            yield return Delay(1.5f);
            Assert.That(Get<bool>(session, "ChapterMode"), Is.False); Assert.That(RecoveryPage, Is.EqualTo("Result"));
        }
    }
}
