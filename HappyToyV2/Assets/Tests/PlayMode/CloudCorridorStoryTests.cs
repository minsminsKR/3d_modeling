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

        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorBottomHintsReturnAfterOrdinaryDialogue()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreateCorridor", 73); Begin(); Call(session, "ToggleCheatSafeMode");
            var run = Get<Component>(session, "Corridor");
            var narration = Get<Component>(run, "StoryNarration");
            Call(narration, "Clear"); yield return null; yield return null;
            var view = One("GameShellView");
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var hudField = view.GetType().GetField("hud", fields);
            Assert.That(hudField, Is.Not.Null);
            var hud = hudField.GetValue(view);
            Assert.That(hud, Is.Not.Null);
            var inventoryField = hud.GetType().GetField("meter", fields);
            var controlsField = hud.GetType().GetField("controlsFooter", fields);
            Assert.That(inventoryField, Is.Not.Null); Assert.That(controlsField, Is.Not.Null);
            var inventory = (Label)inventoryField.GetValue(hud);
            var controls = (Label)controlsField.GetValue(hud);
            Assert.That(inventory, Is.Not.Null); Assert.That(controls, Is.Not.Null);
            Assert.That(inventory.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Corridor inventory must normally be visible");
            Assert.That(controls.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Control hints must normally be visible");

            Call(narration, "Replace", "회랑의 기억이 떠오른다.", 60f);
            yield return Wait(() => Get<string>(narration, "CurrentText") == "회랑의 기억이 떠오른다.", 5, "Ordinary dialogue did not start");
            yield return null; yield return null;
            Assert.That(inventory.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Inventory must not overlap bottom dialogue");
            Assert.That(controls.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Control hints must not overlap bottom dialogue");

            var ink = new Texture2D(2, 2);
            try
            {
                Call(narration, "ReplaceEvidence", "기록을 읽는다.", 60f, ink);
                yield return Wait(() => Get<Texture2D>(narration, "CurrentInk") == ink, 5, "Evidence reading did not start");
                yield return null; yield return null;
                Assert.That(inventory.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Inventory must return for the raised evidence reading");
                Assert.That(controls.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Controls must return for the raised evidence reading");
                Call(narration, "Replace", "다시 떠오르는 기억.", 60f); yield return null; yield return null;
                Assert.That(inventory.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(controls.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Call(narration, "Clear"); yield return null; yield return null;
                Assert.That(inventory.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Inventory must return when dialogue ends");
                Assert.That(controls.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), "Controls must return when dialogue ends");
            }
            finally { Object.Destroy(ink); }
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
