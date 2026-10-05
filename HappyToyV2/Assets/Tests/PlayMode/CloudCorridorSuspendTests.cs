using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IEnumerator ClickStoredSlotRoot(VisualElement root,string id)
        {
            var button=root.Q<Button>(id); Assert.That(button,Is.Not.Null); Assert.That(button.enabledInHierarchy,Is.True);
            Vector2 at=button.worldBound.center; var target=root.panel.Pick(at);
            Assert.That(target==button||button.Contains(target),Is.True);
            using(var down=PointerDownEvent.GetPooled(new Event { type=EventType.MouseDown,mousePosition=at,button=0,clickCount=1 })) target.SendEvent(down);
            yield return Delay(.04f);
            using(var up=PointerUpEvent.GetPooled(new Event { type=EventType.MouseUp,mousePosition=at,button=0,clickCount=1 })) target.SendEvent(up);
            yield return Delay(.15f);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator SavedSlotIsConsumedWhenContinuingAfterSceneReloadFailure()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.2f); yield return RecoveryPulse(Key.Escape);
            var root=Get<VisualElement>(One("GameShellView"),"Root"); var original=session.gameObject.scene;
            var temporary=SceneManager.CreateScene("Owned checkpoint unloadable scene");
            SceneManager.MoveGameObjectToScene(session.gameObject,temporary);
            try
            {
                yield return ClickStoredSlotRoot(root,"suspend-run");
                Assert.That(RecoveryPage,Is.EqualTo("Pause")); Assert.That(Get<bool>(shell,"IsReloading"),Is.False);
                Assert.That(Get<string>(shell,"ReloadError"),Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json")),Is.True);
                yield return ClickStoredSlotRoot(root,"resume");
                Assert.That(RecoveryPage,Is.EqualTo("Playing"));
                Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json")),Is.False,"Continuing left a rewind checkpoint");
                Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.False);
            }
            finally
            {
                if(session&&original.isLoaded) SceneManager.MoveGameObjectToScene(session.gameObject,original);
                if(temporary.isLoaded) SceneManager.UnloadSceneAsync(temporary);
            }
        }
        IEnumerator RefreshSlotTitle(string directory)
        {
            Call(session,"ConfigureRecordDirectory",directory);
            Call(shell,"Settings"); yield return Delay(.1f); Call(shell,"Back"); yield return Delay(.1f);
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator ActualSuspendAndContinueButtonsRestoreOnceAndNewSchoolPreservesSlot()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.2f);
            foreach(var memory in CheckpointItems("CorridorMemory").Take(2)) Call(memory,"Use",player);
            Call(CheckpointItems("FirecrackerSupply")[0],"Use",player);
            yield return Delay(.2f); Call(shell,"ToggleLargeText"); yield return RecoveryPulse(Key.Escape);
            var source=Call(session,"CaptureCheckpoint"); var failures=new List<string>();
            foreach(var size in new[] {new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(One("GameShellView"),"suspend-pause-large-"+size.x+"x"+size.y+".png",size.x,size.y,failures);
            var previous=session; yield return RecoveryClick("suspend-run"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.True);
            Assert.That(Get<int>(Get<object>(Get<object>(session,"Records"),"Snapshot"),"attempts"),Is.Zero,"Suspension counted a finished run");
            foreach(var size in new[] {new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(One("GameShellView"),"suspend-title-large-"+size.x+"x"+size.y+".png",size.x,size.y,failures);
            yield return RecoveryClick("continue-run");
            Assert.That(RecoveryPage,Is.EqualTo("Pause")); Assert.That(Get<bool>(session,"InputAllowed"),Is.False);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(2));
            Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.EqualTo(Get<float>(source,"seconds")));
            Assert.That(Vector3.Distance(player.transform.position,Get<Vector3>(Get<object>(source,"player"),"position")),Is.LessThan(.001f));
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(2));
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.False);
            Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json")),Is.False);
            Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json.used")),Is.True);
            yield return RecoveryClick("resume"); yield return KeysObserved(Key.W); yield return Delay(.3f); Keys();
            Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.GreaterThan(Get<float>(source,"seconds")));
            yield return RecoveryPulse(Key.Escape); previous=session;
            yield return RecoveryClick("suspend-run"); yield return RecoveryRebind(previous); yield return RefreshSlotTitle(directory);
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.True);
            yield return RecoveryClick("begin-school");
            Assert.That(RecoveryPage,Is.EqualTo("Playing")); Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(2));
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.True,"New school chapter erased the earlier corridor checkpoint");
            Assert.That(failures,Is.Empty);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator ActualSuspendWriteFailurePreservesPausedRunAndCanRetry()
        {
            string root=NewRecordFixture(); string directory=Path.Combine(root,"blocked-slot");
            File.WriteAllText(directory,"owned test blocker"); Call(session,"ConfigureRecordDirectory",directory);
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.2f); Call(shell,"ToggleLargeText");
            yield return RecoveryPulse(Key.Escape); var original=session; var position=player.transform.position;
            yield return RecoveryClick("suspend-run");
            Assert.That(session,Is.SameAs(original)); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<bool>(shell,"IsReloading"),Is.False); Assert.That(player.transform.position,Is.EqualTo(position));
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.False);
            Assert.That(File.ReadAllText(directory),Is.EqualTo("owned test blocker"));
            var failures=new List<string>();
            foreach(var size in new[] {new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                yield return CaptureMenu(One("GameShellView"),"suspend-write-error-large-"+size.x+"x"+size.y+".png",size.x,size.y,failures);
            File.Delete(directory); yield return RecoveryClick("suspend-run"); yield return RecoveryRebind(original);
            yield return RefreshSlotTitle(directory);
            Assert.That(Get<bool>(Get<object>(session,"Suspension"),"HasRun"),Is.True);
            Assert.That(Get<int>(Get<object>(Get<object>(session,"Records"),"Snapshot"),"attempts"),Is.Zero);
            Assert.That(failures,Is.Empty);
        }
    }
}
