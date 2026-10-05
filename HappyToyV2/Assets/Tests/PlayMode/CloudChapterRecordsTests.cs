using System;
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
        // Controlled fixtures below call real accepted interaction APIs. The
        // separate full-route test uses only actual keyboard/mouse gameplay.
        IEnumerator SchoolAcceptedActionsFixture()
        {
            yield return KeysObserved(Key.W); yield return Delay(.3f); Keys(); yield return null;
            ((Behaviour)player).enabled=false; var original=player.transform.position;
            var door=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && Get<Transform>(x,"secondaryLeaf"));
            Assert.That((bool)Call(door,"OpenForPursuer"),Is.True); yield return Delay(.8f);
            PlacePlayer(door.transform.position+Vector3.up*.02f); Call(door,"Use",player);
            Assert.That(Get<bool>(door,"IsOpen"),Is.True,"Closing on the player must still be refused");
            Assert.That(Get<int>(Call(session,"CaptureChapterMetrics"),"closedDoors"),Is.Zero);
            PlacePlayer(original); Call(door,"Use",player); Assert.That(Get<bool>(door,"IsOpen"),Is.False);
            PlacePlayer(new Vector3(-4.5f,.02f,0)); Get<Camera>(player,"eyes").transform.rotation=Quaternion.LookRotation(Vector3.right);
            var stock=Get<Component>(player,"Firecrackers"); Assert.That((bool)Call(stock,"TryThrow"),Is.True);
            Assert.That((bool)Call(stock,"TryThrow"),Is.False,"The same-frame cooldown must deny a second throw");
            var cabinet=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="HidingPlace" && Get<Transform>(x,"outside"));
            PlacePlayer(Get<Transform>(cabinet,"outside").position); Call(cabinet,"Use",player); Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Call(player,"Hide",cabinet,Get<Transform>(cabinet,"inside").position,Get<Transform>(cabinet,"outside").position);
            Call(cabinet,"Use",player); Assert.That(Get<bool>(player,"Hidden"),Is.False); PlacePlayer(original);
            var metrics=Call(session,"CaptureChapterMetrics");
            Assert.That(Get<int>(metrics,"throws"),Is.EqualTo(1)); Assert.That(Get<int>(metrics,"hides"),Is.EqualTo(1));
            Assert.That(Get<int>(metrics,"closedDoors"),Is.EqualTo(1)); Assert.That(Get<float>(metrics,"distance"),Is.GreaterThan(.05f));
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator SchoolResultCountsAcceptedActionsAndPersistsActualDefeatMetadataOnce()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return SchoolAcceptedActionsFixture();
            Call(shell,"Pause"); var before=Call(session,"CaptureChapterMetrics");
            Call(session,"NoteChapterAction",Enum.Parse(RequireType("ChapterAction"),"FirecrackerThrown"));
            Assert.That(Get<int>(Call(session,"CaptureChapterMetrics"),"throws"),Is.EqualTo(Get<int>(before,"throws")));
            Call(shell,"Resume"); Assert.That((bool)Call(session,"TryDefeat","Weeping mannequin","시선을 돌리거나 손전등을 꺼서 관절 움직임을 멈추세요."),Is.True);
            yield return null; Assert.That(RecoveryPage,Is.EqualTo("Result")); Assert.That(Get<bool>(session,"ChapterRecordSaved"),Is.True);
            Call(session,"Finish",false); Assert.That((bool)Call(session,"RetryChapterRecordSave"),Is.False);
            var summary=Get<object>(Get<object>(session,"ChapterRecords"),"Snapshot"); Assert.That(Get<int>(summary,"attempts"),Is.EqualTo(1));
            var report=Get<Array>(summary,"recent").GetValue(0); Assert.That(Get<string>(report,"defeatSource"),Is.EqualTo("Weeping mannequin"));
            Assert.That(Get<int>(report,"throws"),Is.EqualTo(1)); Assert.That(Get<int>(report,"hides"),Is.EqualTo(1)); Assert.That(Get<int>(report,"closedDoors"),Is.EqualTo(1));
            Assert.That(Get<int>(report,"firecrackersRemaining"),Is.EqualTo(1)); Assert.That(Get<bool>(report,"actionsComplete"),Is.True);
            Assert.That(File.Exists(Path.Combine(directory,"corridor-records-v1.json")),Is.False);
            var view=One("GameShellView"); var root=Get<VisualElement>(view,"Root");
            Assert.That(root.Q<Label>("school-result-details").text,Does.Contain("폭죽 1회"));
            Assert.That(root.Q<Label>("school-result-source").text,Does.Contain("Weeping mannequin"));
            if(!Get<bool>(shell,"LargeText")) Call(shell,"ToggleLargeText"); var errors=new List<string>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"school-result-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            yield return RecoveryClick("school-records"); Assert.That(RecoveryPage,Is.EqualTo("Records"));
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"school-history-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            Assert.That(errors,Is.Empty,string.Join("\n",errors)); yield return RecoveryClick("back"); Assert.That(RecoveryPage,Is.EqualTo("Result"));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolResultWriteFailureRetriesThroughRealMenuAndTitleShowsSchoolTotals()
        {
            string directory=Path.Combine(NewRecordFixture(),"blocked-school-profile"); File.WriteAllText(directory,"owned school profile blocker");
            Call(session,"ConfigureRecordDirectory",directory); yield return RecoveryUiReady(); yield return RecoveryClick("begin-school");
            yield return Delay(.2f); if(!Get<bool>(shell,"LargeText")) Call(shell,"ToggleLargeText"); Call(session,"Finish",false); yield return null;
            Assert.That(Get<bool>(session,"ChapterRecordSaved"),Is.False); Assert.That(RecoveryPage,Is.EqualTo("Result"));
            var errors=new List<string>();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(One("GameShellView"),"school-result-write-error-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            Assert.That(File.ReadAllText(directory),Is.EqualTo("owned school profile blocker")); File.Delete(directory);
            yield return RecoveryClick("retry-school-record-save"); Assert.That(Get<bool>(session,"ChapterRecordSaved"),Is.True);
            Assert.That((bool)Call(session,"RetryChapterRecordSave"),Is.False);
            var previous=session; yield return RecoveryClick("title"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            var root=Get<VisualElement>(One("GameShellView"),"Root"); Assert.That(root.Q<Label>("school-record-summary").text,Does.Contain("학교 탐색 1회"));
            Assert.That(root.Q<Label>("corridor-record-summary").text,Does.Contain("회랑 탐색 0회"));
            yield return RecoveryClick("school-records");
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Label>("school-history-run-0").text,Does.Contain("기억 0/5"));
            yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage,Is.EqualTo("Title")); Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator SchoolActionMetricsSurviveSuspendAndRawOldVersionOneWithoutMetricsResumesHonestly()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return SchoolAcceptedActionsFixture();
            yield return Wait(()=>Components("FirecrackerProjectile").Length==0,15,"Actual thrown firecracker never expired");
            Call(shell,"Pause"); var before=Call(session,"CaptureChapterMetrics"); var previous=session;
            yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            yield return RecoveryClick("continue-chapter"); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            var after=Call(session,"CaptureChapterMetrics"); Assert.That(Get<string>(after,"token"),Is.EqualTo(Get<string>(before,"token")));
            Assert.That(Get<int>(after,"throws"),Is.EqualTo(1)); Assert.That(Get<int>(after,"hides"),Is.EqualTo(1)); Assert.That(Get<int>(after,"closedDoors"),Is.EqualTo(1));
            Assert.That(Get<float>(after,"distance"),Is.EqualTo(Get<float>(before,"distance")).Within(.0001f));
            var old=Call(session,"CaptureChapterCheckpoint"); Set(old,"runProgress",null);
            string raw=(string)Call(old,"ToJson"); Assert.That(raw,Does.Not.Contain("\"runProgress\""));
            var parsed=Call(RequireType("ChapterCheckpoint"),"FromJson",raw);
            Assert.That((bool)Call(Get<object>(session,"ChapterSuspension"),"Save",parsed),Is.True);
            Assert.That(File.ReadAllText(Path.Combine(directory,"chapter-suspend-v1.json")),Does.Not.Contain("\"runProgress\""));
            previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            yield return RecoveryClick("continue-chapter"); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            after=Call(session,"CaptureChapterMetrics"); Assert.That(Get<bool>(after,"complete"),Is.False); Assert.That(Get<int>(after,"throws"),Is.Zero);
            Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.EqualTo(Get<float>(old,"seconds")));
            yield return RecoveryClick("resume"); Call(session,"Finish",false); yield return null;
            Assert.That(Get<bool>(session,"ChapterRecordSaved"),Is.True);
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Label>("school-result-scope").text,Does.Contain("이어하기 이후"));
        }
        [UnityTest, Timeout(750000)]
        public IEnumerator SchoolFullRealInputEscapePersistsCurrentSchoolBestAndTravel()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory); Call(shell,"BeginChapter");
            using(var route=new CloudSurvivalTests(session,player,shell,Keys,InputDiagnostics)) yield return route.Run();
            Assert.That(Get<bool>(session,"ChapterRecordSaved"),Is.True); var report=Get<object>(session,"ChapterResult");
            Assert.That(Get<bool>(report,"escaped"),Is.True); Assert.That(Get<int>(report,"recovered"),Is.EqualTo(5));
            Assert.That(Get<float>(report,"distance"),Is.GreaterThan(100)); Assert.That(Get<bool>(report,"actionsComplete"),Is.True);
            var summary=Get<object>(Get<object>(session,"ChapterRecords"),"Snapshot"); Assert.That(Get<int>(summary,"escapes"),Is.EqualTo(1));
            Assert.That(Get<float>(summary,"bestEscapeSeconds"),Is.EqualTo(Get<float>(report,"seconds")));
            var previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Label>("school-record-summary").text,Does.Contain("탈출 1회"));
            yield return RecoveryClick("school-records");
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Label>("school-history-run-0").text,Does.Contain("탈출 성공"));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolProtectedPrimaryTitleExplainsReadOnlyHistoryAndPreservesRawFiles()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); Call(session,"Finish",false); yield return null;
            string path=Path.Combine(directory,"school-records-v1.json"),good=File.ReadAllText(path);
            var previous=session; yield return RecoveryClick("title"); yield return RecoveryRebind(previous);
            const string damaged="owned damaged primary school profile"; File.WriteAllText(path+".bak",good); File.WriteAllText(path,damaged);
            Call(session,"ConfigureRecordDirectory",directory); yield return RecoveryClick("settings"); yield return RecoveryClick("back");
            Assert.That(Get<bool>(Get<object>(session,"ChapterRecords"),"Writable"),Is.False);
            var root=Get<VisualElement>(One("GameShellView"),"Root"); Assert.That(root.Q<Label>("school-profile-notice").text,Does.Contain("저장이 중지"));
            Assert.That(root.Q<Button>("school-records").enabledInHierarchy,Is.True);
            if(!Get<bool>(shell,"LargeText")) Call(shell,"ToggleLargeText"); var errors=new List<string>();
            yield return CaptureMenu(One("GameShellView"),"school-protected-title-large.png",1280,720,errors);
            yield return RecoveryClick("school-records"); Assert.That(RecoveryPage,Is.EqualTo("Records"));
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Label>("school-history-summary").text,Does.Contain("학교 탐색 1회"));
            yield return CaptureMenu(One("GameShellView"),"school-protected-history-large.png",1280,720,errors);
            Assert.That(errors,Is.Empty,string.Join("\n",errors)); Assert.That(File.ReadAllText(path),Is.EqualTo(damaged));
            Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(good)); yield return RecoveryClick("back"); Assert.That(RecoveryPage,Is.EqualTo("Title"));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolHistoryEightReportsPageThroughRealButtonsAndLargeTextAtThreeSizes()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); Call(session,"Finish",false); yield return null;
            // Populate bounded history using independently validated finished-run
            // metadata. This is a pagination fixture, not eight survival claims.
            var original=Get<object>(session,"ChapterResult"); var records=Get<object>(session,"ChapterRecords");
            for(int index=1;index<8;index++)
            {
                var report=JsonUtility.FromJson(JsonUtility.ToJson(original),RequireType("ChapterRunReport"));
                Set(report,"token",Guid.NewGuid().ToString("N")); Set(report,"recovered",index%5+1); Set(report,"seconds",70f+index);
                Assert.That((bool)Call(records,"Record",report),Is.True);
            }
            var previous=session; yield return RecoveryClick("title"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            if(!Get<bool>(shell,"LargeText")) Call(shell,"ToggleLargeText"); yield return RecoveryClick("school-records");
            var view=One("GameShellView"); var root=Get<VisualElement>(view,"Root"); var errors=new List<string>();
            Assert.That(root.Q<Label>("school-history-run-0"),Is.Not.Null); Assert.That(root.Q<Label>("school-history-run-3"),Is.Not.Null);
            Assert.That(root.Q<Label>("school-history-run-4"),Is.Null); Assert.That(root.Q<Button>("school-records-previous").enabledSelf,Is.False);
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"school-history-page-1-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            yield return RecoveryClick("school-records-next"); root=Get<VisualElement>(view,"Root");
            for(int index=4;index<8;index++) Assert.That(root.Q<Label>("school-history-run-"+index),Is.Not.Null);
            Assert.That(root.Q<Label>("school-history-run-0"),Is.Null); Assert.That(root.Q<Button>("school-records-next").enabledSelf,Is.False);
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(view,"school-history-page-2-large-"+size.x+"x"+size.y+".png",size.x,size.y,errors);
            yield return RecoveryClick("school-records-previous"); root=Get<VisualElement>(view,"Root");
            for(int index=0;index<4;index++) Assert.That(root.Q<Label>("school-history-run-"+index),Is.Not.Null);
            Assert.That(root.Q<Label>("school-history-run-4"),Is.Null); yield return RecoveryClick("back");
            Assert.That(RecoveryPage,Is.EqualTo("Title")); Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }
    }
}
