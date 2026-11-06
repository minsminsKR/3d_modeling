using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorFirstStoryIsShownForAnyRelicAndSurvivesSuspendWithoutInventingOrder()
        {
            Call(session, "CreateCorridor", 73); Begin(); Call(session, "ToggleCheatSafeMode");
            Call(session, "Inspect", "ordinary-record", "먼저 조사한 주변 기록");
            var run = Get<Component>(session, "Corridor");
            var memories = CheckpointItems("CorridorMemory");
            var narration = Get<Component>(run, "StoryNarration");
            Call(narration, "Enqueue", "남은 장 안내", 10f); yield return null;
            Call(memories[3], "Use", player); yield return null; yield return null;
            string clue = Get<string>(narration, "CurrentText");
            Assert.That(clue, Does.Contain("작은 집")); Assert.That(clue, Does.Contain("이름칸"));
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            Assert.That(root.Q<Label>("temple-narration").text, Is.EqualTo(clue));
            Call(memories[1], "Use", player); yield return null;
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(2));
            Call(shell, "Pause"); yield return null; yield return null;
            Assert.That(Get<string>(narration, "CurrentText"), Is.EqualTo(clue), "Pause must hold story timing");
            var save = Call(session, "CaptureCheckpoint");
            Call(shell, "Journal"); yield return null; yield return null;
            root = Get<VisualElement>(One("GameShellView"), "Root");
            Assert.That(root.Q<Label>("corridor-memory-0").text, Is.EqualTo(clue));
            Assert.That(root.Q<Label>("corridor-memory-1").text, Does.Contain("2/4"));
            Assert.That(root.Q<Label>("inspection-0").text, Is.EqualTo("먼저 조사한 주변 기록"));
            yield return RestoreCheckpointInFreshScene(save);
            run = Get<Component>(session, "Corridor");
            Assert.That(Get<int>(run, "Recovered"), Is.EqualTo(2));
            Assert.That((string)Call(run, "MemoryJournalEntry", 0), Is.EqualTo(clue));
            Assert.That((string)Call(run, "MemoryJournalEntry", 1), Does.Contain("2/4"));
            Assert.That((string)Call(run, "MemoryJournalEntry", 2), Is.Null);
            narration = Get<Component>(run, "StoryNarration");
            Begin(); yield return null; yield return null;
            Assert.That(Get<string>(narration, "CurrentText"), Is.Empty, "Restoring must not replay a clue");
            Call(shell, "Journal"); yield return null; yield return null;
            root = Get<VisualElement>(One("GameShellView"), "Root");
            Assert.That(root.Q<Label>("corridor-memory-0").text, Is.EqualTo(clue));
            Assert.That(root.Q<Label>("corridor-memory-1").text, Does.Contain("2/4"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RealTempleEntryShowsTheCorridorChapterStory()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            yield return RecoveryAdvanceTempleToCorridor();
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            yield return Wait(() => root.Q<Label>("temple-narration")?.text.Contains("제1장 — 잊힌 회랑") == true,
                5, "A completed temple transition must show the corridor chapter story");
            Assert.That(Get<bool>(session, "CorridorMode"), Is.True);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LegacyFiveMemoryCheckpointReconstructsTheStoryWithoutNewSaveFields()
        {
            var saved = Call(RequireType("CorridorCheckpoint"), "FromJson",
                System.IO.File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"));
            Call(session, "CreateCorridorForCheckpoint", saved); Call(session, "ApplyCheckpoint", saved);
            var run = Get<Component>(session, "Corridor");
            Assert.That(Get<int>(run, "RequiredMemories"), Is.EqualTo(5));
            Assert.That(Get<int>(run, "Recovered"), Is.EqualTo(2));
            Assert.That((string)Call(run, "MemoryJournalEntry", 0), Does.Contain("작은 집"));
            Assert.That((string)Call(run, "MemoryJournalEntry", 1), Does.Contain("2/5"));
            Assert.That((string)Call(run, "MemoryJournalEntry", 2), Is.Null);
            Begin(); yield return null; yield return null;
            Assert.That(Get<string>(Get<Component>(run, "StoryNarration"), "CurrentText"), Is.Empty,
                "A legacy restore must not queue chapter or first-discovery narration");
        }
    }
}
