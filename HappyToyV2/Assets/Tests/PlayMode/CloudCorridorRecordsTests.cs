using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        string recordFixture;
        string NewRecordFixture()
        {
            recordFixture = Path.GetFullPath(Path.Combine("Temp", "CorridorRecordPlay", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(recordFixture); return recordFixture;
        }
        [TearDown] public void CleanupIsolatedRunRecords()
        {
            if (string.IsNullOrEmpty(recordFixture)) return;
            string parent = Path.GetFullPath("Temp/CorridorRecordPlay") + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(recordFixture).StartsWith(parent, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(recordFixture)) Directory.Delete(recordFixture, true);
            recordFixture = null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator CorridorResultPersistsExactlyOnceAndReappearsAfterRealSceneReload()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreateCorridor", 73); Begin(); yield return Delay(.3f);
            // Any-order completion fixture exercises finish/storage, not survival.
            foreach (var memory in Components("Interactable").Where(x => Get<object>(x, "kind").ToString() == "CorridorMemory")) Call(memory, "Use", player);
            PlacePlayer(Get<Vector3>(Get<Component>(session,"Corridor"),"AltarApproach"));
            Call(session, "TryEscape"); Assert.That(Get<bool>(session, "RecordSaved"), Is.True);
            Call(session, "Finish", true); Assert.That((bool)Call(session, "RetryRecordSave"), Is.False);
            var stored = Get<object>(Get<object>(session, "Records"), "Snapshot");
            Assert.That(Get<int>(stored, "attempts"), Is.EqualTo(1)); Assert.That(Get<int>(stored, "escapes"), Is.EqualTo(1));
            Assert.That(Get<int>(stored, "lastSeed"), Is.EqualTo(73));
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory);
            Call(shell, "Settings"); yield return Delay(.1f); Call(shell, "Back"); yield return Delay(.1f);
            var label = Get<VisualElement>(One("GameShellView"), "Root").Q<Label>("corridor-record-summary");
            Assert.That(label.text, Does.Contain("탈출 1회")); Assert.That(label.text, Does.Contain("최고 기억 5/5"));
            Call(shell, "ToggleLargeText"); yield return Delay(.1f);
            var errors = new List<string>();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                yield return CaptureMenu(One("GameShellView"), "corridor-records-title-large-" + size.x + "x" + size.y + ".png", size.x, size.y, errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator CorridorRecordWriteFailureCanBeRetriedThroughActualResultButtonWithoutDuplication()
        {
            string directory = NewRecordFixture(); string blocked = Path.Combine(directory, "profile");
            File.WriteAllText(blocked, "owned storage blocker");
            Call(session, "ConfigureRecordDirectory", blocked); Call(session, "CreateCorridor", 73); Begin();
            Call(shell, "ToggleLargeText"); yield return Delay(.3f); Call(session, "Finish", false); yield return Delay(.1f);
            Assert.That(Get<bool>(session, "RecordSaved"), Is.False);
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            Assert.That(root.Q<Button>("retry-record-save"), Is.Not.Null);
            var errors = new List<string>();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                yield return CaptureMenu(One("GameShellView"), "corridor-records-write-error-large-" + size.x + "x" + size.y + ".png", size.x, size.y, errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(File.ReadAllText(blocked), Is.EqualTo("owned storage blocker")); File.Delete(blocked);
            yield return RecoveryClick("retry-record-save");
            Assert.That(Get<bool>(session, "RecordSaved"), Is.True);
            root = Get<VisualElement>(One("GameShellView"), "Root"); Assert.That(root.Q<Button>("retry-record-save"), Is.Null);
            Assert.That((bool)Call(session, "RetryRecordSave"), Is.False);
            var stored = Get<object>(Get<object>(session, "Records"), "Snapshot");
            Assert.That(Get<int>(stored, "attempts"), Is.EqualTo(1)); Assert.That(Get<int>(stored, "escapes"), Is.Zero);
        }
    }
}
