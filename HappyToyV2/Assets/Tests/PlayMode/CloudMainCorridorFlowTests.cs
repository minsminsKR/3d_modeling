using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        void AssertMainCorridor()
        {
            Assert.That(Get<bool>(session,"CorridorMode"),Is.True);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.False);
            Assert.That(Get<int>(session,"TotalRecords"),Is.EqualTo(5));
            Assert.That(CheckpointThreats().Length,Is.EqualTo(3),"Main entry must create Cyclops, Uncat and Baby stalkers");
            var mask=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            Assert.That(mask,Is.Not.Null,"Main entry must retain the actual lantern mask encounter");
            Assert.That(mask.GetType(),Is.EqualTo(RequireType("LanternMaskEncounter")));
            Assert.That(CheckpointItems("CorridorMemory").Length,Is.EqualTo(5));
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(1));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MainCorridorRealPointerEntryLargeTitleJournalPauseAndResultRestartsStayInCorridor()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); Call(shell,"RestoreDefaultSettings"); Call(shell,"ToggleLargeText"); yield return null;
            var view=One("GameShellView"); var errors=new List<string>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"main-corridor-title-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            var root=Get<VisualElement>(view,"Root");
            Assert.That(((VisualElement)root.panel.focusController.focusedElement).name,Is.EqualTo("begin"));
            Assert.That(root.Q<Button>("begin").text,Does.Contain("회랑"));
            Assert.That(root.Q<Button>("begin-school").text,Does.Contain("폐교의 기억"));
            Assert.That(root.Q<Label>("corridor-record-summary").text,Does.Contain("회랑 탐색 0회"));
            Assert.That(root.Q<Label>("school-record-summary").text,Does.Contain("학교 탐색 0회"));
            yield return RecoveryClick("begin"); AssertMainCorridor();
            yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<VisualElement>(view,"Root").Q<Button>("suspend-run"),Is.Not.Null);
            Assert.That(Get<VisualElement>(view,"Root").Q<Button>("suspend-chapter"),Is.Null);
            yield return RecoveryClick("journal"); root=Get<VisualElement>(view,"Root");
            Assert.That(root.Query<Label>().ToList().Count(x=>x.name.StartsWith("corridor-memory-")),Is.EqualTo(5));
            Assert.That(root.Q<Label>("record-0"),Is.Null,"School's required records leaked into the corridor journal");
            Assert.That(root.Q<Label>("corridor-memory-0").text,Does.Contain("아직 회수하지 못했습니다"));
            yield return CaptureMenu(view,"main-corridor-journal-large.png",1280,720,errors);
            yield return RecoveryClick("back"); var previous=session;
            yield return RecoveryClick("restart"); yield return RecoveryRebind(previous); AssertMainCorridor();
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero); Assert.That(RecoveryPage,Is.EqualTo("Playing"));
            Call(session,"Finish",false); yield return null; Assert.That(RecoveryPage,Is.EqualTo("Result"));
            yield return CaptureMenu(One("GameShellView"),"main-corridor-result-large.png",1280,720,errors);
            previous=session; yield return RecoveryClick("restart"); yield return RecoveryRebind(previous); AssertMainCorridor();
            Assert.That(Get<bool>(session,"Finished"),Is.False); Assert.That(RecoveryPage,Is.EqualTo("Playing"));
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator MainCorridorFocusedEnterBeginsOnceThroughRealKeyboardInput()
        {
            yield return RecoveryUiReady();
            var root=Get<VisualElement>(One("GameShellView"),"Root");
            Assert.That(((VisualElement)root.panel.focusController.focusedElement).name,Is.EqualTo("begin"));
            root.Q<Button>("settings").Focus(); yield return null;
            Keys(Key.Enter); yield return Delay(.6f);
            Assert.That(RecoveryPage,Is.EqualTo("Settings"),"Held Enter submitted again into the newly focused Back control");
            Keys(); yield return Delay(.1f); yield return RecoveryPulse(Key.Enter);
            Assert.That(RecoveryPage,Is.EqualTo("Title"));
            root=Get<VisualElement>(One("GameShellView"),"Root");
            Assert.That(((VisualElement)root.panel.focusController.focusedElement).name,Is.EqualTo("begin"));
            // Use the actual Input System keyboard and UI Toolkit submit path.
            // GameShell.Begin is intentionally reserved for original annex audits.
            yield return RecoveryPulse(Key.Enter); AssertMainCorridor();
            Assert.That(RecoveryPage,Is.EqualTo("Playing"));
            var run=Get<Component>(session,"Corridor");
            Keys(Key.Enter); yield return Delay(.3f); Keys(); yield return null;
            Assert.That(Get<Component>(session,"Corridor"),Is.SameAs(run));
            Assert.That(Components("CorridorRun").Length,Is.EqualTo(1));
            Assert.That(Get<bool>(shell,"IsReloading"),Is.False);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MainCorridorAndSchoolRealEntriesKeepBothSuspendSlotsAndRecordsSeparate()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school");
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True); Call(shell,"Pause");
            var previous=session; yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            string schoolPath=Path.Combine(directory,"chapter-suspend-v1.json"); string schoolBytes=File.ReadAllText(schoolPath);
            yield return RecoveryClick("begin"); AssertMainCorridor();
            Assert.That(File.ReadAllText(schoolPath),Is.EqualTo(schoolBytes),"Starting a new corridor consumed the separate school slot");
            // Controlled memory setup tests storage/navigation, not survival balance.
            foreach(var memory in CheckpointItems("CorridorMemory").Take(2)) Call(memory,"Use",player);
            Call(shell,"Pause"); previous=session; yield return RecoveryClick("suspend-run"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            string corridorPath=Path.Combine(directory,"corridor-suspend-v1.json"); string corridorBytes=File.ReadAllText(corridorPath);
            Assert.That(File.ReadAllText(schoolPath),Is.EqualTo(schoolBytes));
            Call(shell,"RestoreDefaultSettings"); Call(shell,"ToggleLargeText"); yield return null;
            var errors=new List<string>(); var view=One("GameShellView");
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"main-corridor-both-slots-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            var root=Get<VisualElement>(view,"Root");
            Assert.That(root.Q<Button>("continue-run"),Is.Not.Null); Assert.That(root.Q<Button>("continue-run").text,Does.Contain("기억 2/5"));
            Assert.That(root.Q<Button>("continue-chapter"),Is.Not.Null);
            Assert.That(((VisualElement)root.panel.focusController.focusedElement).name,Is.EqualTo("begin"));
            yield return RecoveryClick("continue-chapter"); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True); Assert.That(Get<bool>(session,"CorridorMode"),Is.False);
            Assert.That(File.ReadAllText(corridorPath),Is.EqualTo(corridorBytes));
            yield return RecoveryClick("resume"); Call(session,"Finish",false); yield return null;
            Assert.That(Get<int>(Get<object>(Get<object>(session,"ChapterRecords"),"Snapshot"),"attempts"),Is.EqualTo(1));
            Assert.That(Get<int>(Get<object>(Get<object>(session,"Records"),"Snapshot"),"attempts"),Is.Zero);
            previous=session; yield return RecoveryClick("title"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            Assert.That(Get<bool>(Get<object>(session,"ChapterSuspension"),"HasRun"),Is.False);
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.True);
            yield return RecoveryClick("continue-run"); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session,"CorridorMode"),Is.True); Assert.That(Get<bool>(session,"ChapterMode"),Is.False);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(2));
            Assert.That(File.Exists(corridorPath),Is.False);
            yield return RecoveryClick("resume"); Call(session,"Finish",false); yield return null;
            Assert.That(Get<int>(Get<object>(Get<object>(session,"Records"),"Snapshot"),"attempts"),Is.EqualTo(1));
            Assert.That(Get<int>(Get<object>(Get<object>(session,"ChapterRecords"),"Snapshot"),"attempts"),Is.EqualTo(1));
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MainCorridorSelectionReplacesPreparedTitleSchoolThroughRealReload()
        {
            // A prepared title can remain after a failed resume. Explicit mode
            // selection must not silently reuse that chapter or erase its slot.
            Call(session,"CreateChapter"); yield return RecoveryUiReady();
            Assert.That(RecoveryPage,Is.EqualTo("Title")); Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
            var previous=session; yield return RecoveryClick("begin"); yield return RecoveryRebind(previous); AssertMainCorridor();
            Assert.That(RecoveryPage,Is.EqualTo("Playing"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MainTitleDamagedSchoolAndCorridorSlotsFitLargeTextAndPreserveBothRawFiles()
        {
            string directory=NewRecordFixture();
            string schoolPath=Path.Combine(directory,"chapter-suspend-v1.json"),corridorPath=Path.Combine(directory,"corridor-suspend-v1.json");
            const string damagedSchool="{\"version\":1,\"owned\":\"damaged school snapshot 원문\"}";
            const string damagedCorridor="{\"version\":1,\"owned\":\"damaged corridor snapshot 원문\"}";
            File.WriteAllText(schoolPath,damagedSchool); File.WriteAllText(corridorPath,damagedCorridor);
            Call(session,"ConfigureRecordDirectory",directory); yield return RecoveryUiReady();
            Call(shell,"Settings"); yield return null; Call(shell,"Back"); Call(shell,"RestoreDefaultSettings"); Call(shell,"ToggleLargeText"); yield return null;
            var root=Get<VisualElement>(One("GameShellView"),"Root"); var notice=root.Q<Label>("checkpoint-status");
            Assert.That(notice,Is.Not.Null); Assert.That(notice.text,Does.Contain("회랑·폐교")); Assert.That(notice.text,Does.Contain("원문을 보존"));
            Assert.That(root.Q<Button>("begin").enabledInHierarchy,Is.True); Assert.That(root.Q<Button>("begin-school").enabledInHierarchy,Is.True);
            Assert.That(root.Q<Button>("continue-run"),Is.Null); Assert.That(root.Q<Button>("continue-chapter"),Is.Null);
            var errors=new List<string>(); var view=One("GameShellView");
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
            {
                yield return CaptureMenu(view,"main-corridor-damaged-slots-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
                root=Get<VisualElement>(view,"Root"); notice=root.Q<Label>("checkpoint-status");
                Assert.That(notice.worldBound.yMax,Is.LessThan(root.Q<Button>("begin").worldBound.yMin),"Checkpoint notice overlaps primary navigation");
                Assert.That(notice.worldBound.yMax,Is.LessThan(root.Query<Label>().ToList().Single(x=>x.text=="HAPPY TOY  /  FORGOTTEN CORRIDOR").worldBound.yMin),"Checkpoint notice overlaps footer");
            }
            yield return RecoveryClick("begin"); AssertMainCorridor(); Call(shell,"Pause"); yield return null;
            Assert.That(Get<VisualElement>(view,"Root").Q<Button>("suspend-run").enabledInHierarchy,Is.False);
            Assert.That(File.ReadAllText(schoolPath),Is.EqualTo(damagedSchool)); Assert.That(File.ReadAllText(corridorPath),Is.EqualTo(damagedCorridor));
            var previous=session; yield return RecoveryClick("title"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            yield return RecoveryClick("begin-school"); Assert.That(Get<bool>(session,"ChapterMode"),Is.True); Call(shell,"Pause"); yield return null;
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Button>("suspend-chapter").enabledInHierarchy,Is.False);
            Assert.That(File.ReadAllText(schoolPath),Is.EqualTo(damagedSchool)); Assert.That(File.ReadAllText(corridorPath),Is.EqualTo(damagedCorridor));
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolPortraitHintDistinguishesCompletedUnwitnessedRevealAndNurseryWaitsForRelease()
        {
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school");
            var chapter=Get<Component>(session,"Chapter"); var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player); Call(memories[1],"Use",player);
            Get<Component>(chapter,"Cyclopse").gameObject.SetActive(false); Get<Component>(chapter,"Mannequin").gameObject.SetActive(false);
            ((Behaviour)player).enabled=false; PlacePlayer(new Vector3(34.7f,5.02f,32.2f)); Call(memories[2],"Use",player);
            Get<Component>(chapter,"Mask").gameObject.SetActive(false);
            var camera=Get<Camera>(player,"eyes"); camera.transform.rotation=Quaternion.LookRotation(Vector3.right);
            var portrait=Get<Component>(chapter,"Portrait"); Call(memories[3],"Use",player);
            yield return Wait(()=>Get<bool>(portrait,"Completed"),12,"Actual portrait reveal never completed");
            Assert.That(Get<bool>(portrait,"ChapterWitnessed"),Is.False,"Fixture accidentally looked at the revealed actor");
            // Freeze only contact after the real reveal, to isolate guidance and
            // its existing visual gate rather than certify this setup's survival.
            var angry=Get<Component>(portrait,"angry"); ((Behaviour)angry).enabled=false;
            Call(memories[3],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));
            Assert.That(Get<string>(session,"Objective"),Does.Contain("잠시 바라보세요"));
            Assert.That(Get<string>(session,"Notice"),Does.Not.Contain("액자가 반응했습니다"));
            camera.transform.rotation=Quaternion.LookRotation(angry.transform.position+Vector3.up*.9f-camera.transform.position);
            yield return Wait(()=>Get<bool>(portrait,"ChapterWitnessed"),3,"Looking at the real actor did not satisfy the original sight gate");
            Assert.That(Get<string>(session,"Objective"),Does.Contain("모습을 확인했습니다"));
            Call(memories[3],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(4));
            Assert.That(Get<string>(session,"Objective"),Does.Contain("1층에 돌아온 뒤"));
            PlacePlayer(new Vector3(13.8f,-4.98f,-22)); yield return null;
            var nursery=Get<Component>(chapter,"Nursery"); Assert.That(Get<bool>(nursery,"Triggered"),Is.True);
            Assert.That(Get<Component>(nursery,"monster").gameObject.activeSelf,Is.True); Assert.That(Get<bool>(nursery,"Released"),Is.False);
            Call(memories[4],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(4));
            Assert.That(Get<string>(session,"Notice"),Does.Contain("움직이기 시작하면"));
            yield return Wait(()=>Get<bool>(nursery,"Released"),8,"Original nursery warning did not complete");
            Call(memories[4],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(5));
        }
    }
}
