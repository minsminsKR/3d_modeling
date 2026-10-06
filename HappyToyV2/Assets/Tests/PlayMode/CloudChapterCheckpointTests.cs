using System;
using System.Collections;
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
        object SchoolCheckpointCopy(object data) => Call(RequireType("ChapterCheckpoint"),"FromJson",Call(data,"ToJson"));
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolContinueRehydratesOpenDoorCarvingBeforeRestoringPlayerInsideTheDoorway()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school");
            var door=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && Get<Transform>(x,"secondaryLeaf"));
            Assert.That((bool)Call(door,"OpenForPursuer"),Is.True); yield return Delay(1);
            PlacePlayer(door.transform.position+Vector3.up*.02f); yield return null;
            var pose=player.transform.position; Call(shell,"Pause");
            var data=Call(session,"CaptureChapterCheckpoint");
            Assert.That((bool)Call(player,"CanRestoreChapterProgress",Get<object>(data,"player")),Is.True,"Fixture must stand on real navigation in the open doorway");
            var previous=session; yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory); yield return RecoveryClick("continue-chapter");
            Assert.That(Get<string>(shell,"ReloadError"),Is.Empty); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Vector3.Distance(player.transform.position,pose),Is.LessThan(.001f));
            Assert.That((bool)Call(player,"CanRestoreChapterProgress",Get<object>(data,"player")),Is.True);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero);
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator SchoolCheckpointRestoresCompletedAuthoredPortraitAndNurseryWithoutReplayingReveals()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return null;
            var chapter=Get<Component>(session,"Chapter"); var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);yield return ChapterAwaitAppearance();
            Call(memories[1],"Use",player);yield return ChapterAwaitAppearance();((Behaviour)player).enabled=false;
            PlacePlayer(new Vector3(25.8f,5.02f,24.5f)); Call(memories[2],"Use",player);
            var portrait=Get<Component>(chapter,"Portrait"); var nursery=Get<Component>(chapter,"Nursery");
            var camera=Get<Camera>(player,"eyes"); PlacePlayer(new Vector3(31.3f,5.02f,32.5f));
            Call(memories[3],"Use",player); Assert.That(Get<bool>(portrait,"Triggered"),Is.True);
            Call(shell,"Pause");
            Assert.Throws<InvalidOperationException>(()=>Call(session,"CaptureChapterCheckpoint"),"In-flight portrait cannot become a completed saved reveal");
            Call(shell,"Resume"); PlacePlayer(new Vector3(34.7f,5.02f,32.2f));
            camera.transform.rotation=Quaternion.LookRotation(Get<Vector3>(portrait,"spawn")+Vector3.up*.9f-camera.transform.position);
            yield return Wait(()=>Get<bool>(portrait,"ChapterWitnessed") && Get<bool>(portrait,"Completed"),10,"Original portrait reveal did not complete");
            Call(memories[3],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(4));
            PlacePlayer(new Vector3(13.8f,-4.98f,-22)); yield return null;
            Assert.That(Get<bool>(nursery,"Triggered"),Is.True); Call(shell,"Pause");
            Assert.Throws<InvalidOperationException>(()=>Call(session,"CaptureChapterCheckpoint"),"In-flight nursery cannot skip its warning on restore");
            Call(shell,"Resume"); yield return Wait(()=>Get<bool>(nursery,"Released"),8,"Original nursery reveal did not complete");
            Call(memories[4],"Use",player); Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(5));
            Call(shell,"Pause"); var data=Call(session,"CaptureChapterCheckpoint");
            var previous=session; yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory); yield return RecoveryClick("continue-chapter");
            Assert.That(RecoveryPage,Is.EqualTo("Pause")); Assert.That(Get<string>(shell,"ReloadError"),Is.Empty);
            chapter=Get<Component>(session,"Chapter"); portrait=Get<Component>(chapter,"Portrait"); nursery=Get<Component>(chapter,"Nursery");
            Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(5));
            Assert.That(Get<bool>(portrait,"Completed"),Is.True); Assert.That(Get<bool>(portrait,"ChapterWitnessed"),Is.True);
            Assert.That(Get<bool>(nursery,"Released"),Is.True); Assert.That(Get<string>(portrait,"Phase"),Is.EqualTo("done"));
            Assert.That(Get<Component>(portrait,"angry").gameObject.activeSelf,Is.True);
            Assert.That(Get<Component>(nursery,"monster").gameObject.activeSelf,Is.True);
            Assert.That(Vector3.Distance(Get<Component>(portrait,"angry").transform.position,Get<Vector3>(Get<object>(data,"portraitActor"),"position")),Is.LessThan(.01f));
            Assert.That(Get<bool>(session,"EncountersResolved"),Is.False);
            yield return RecoveryClick("resume"); yield return Delay(.1f);
            Assert.That(Get<string>(portrait,"Phase"),Is.EqualTo("done")); Assert.That(Get<string>(nursery,"Phase"),Is.EqualTo("released"));
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator SchoolSuspendAndContinueRestoreRealTwoLeafDoorsPlayerMemoriesAndActorsOnce()
        {
            string directory=NewRecordFixture(); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return null;
            var chapter=Get<Component>(session,"Chapter"); var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);yield return ChapterAwaitAppearance();
            Call(memories[1],"Use",player);yield return ChapterAwaitAppearance();
            Get<Light>(player,"flashlight").enabled=false;
            Assert.That((bool)Call(player,"TrySetCrouching",true),Is.True); yield return Delay(.15f);
            var mannequin=Get<Component>(chapter,"Mannequin");
            if(Get<bool>(mannequin,"Triggered") && !Get<bool>(mannequin,"Released"))
                yield return Wait(()=>Get<bool>(mannequin,"Released"),4,"Actual first-sight mannequin reveal never finished");
            var door=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && Get<Transform>(x,"secondaryLeaf"));
            Assert.That((bool)Call(door,"OpenForPursuer"),Is.True);
            yield return Delay(.15f); yield return RecoveryPulse(Key.Escape);
            var data=Call(session,"CaptureChapterCheckpoint");
            var savedDoor=Get<Array>(data,"doors").Cast<object>().Single(x=>Get<string>(x,"id")==Get<string>(door,"stableId"));
            var leaf=Get<Vector3>(savedDoor,"leaf"); var secondary=Get<Vector3>(savedDoor,"secondary");
            var pose=player.transform.position; float seconds=Get<float>(session,"ElapsedPlayTime");
            var previous=session; yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(File.Exists(Path.Combine(directory,"chapter-suspend-v1.json")),Is.True);
            Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json")),Is.False);
            Assert.That(Get<VisualElement>(One("GameShellView"),"Root").Q<Button>("continue-chapter"),Is.Not.Null);
            yield return RecoveryClick("continue-chapter");
            Assert.That(Get<string>(shell,"ReloadError"),Is.Empty); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True); Assert.That(Get<bool>(session,"CorridorMode"),Is.False);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(2)); Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.EqualTo(seconds));
            Assert.That(Vector3.Distance(player.transform.position,pose),Is.LessThan(.001f));
            Assert.That(Get<bool>(player,"Crouching"),Is.True); Assert.That(Get<Light>(player,"flashlight").enabled,Is.False);
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(2));
            chapter=Get<Component>(session,"Chapter"); memories=Get<Component[]>(chapter,"Memories");
            Assert.That(memories.Take(2).All(x=>!x.gameObject.activeSelf),Is.True); Assert.That(memories.Skip(2).All(x=>x.gameObject.activeSelf),Is.True);
            Assert.That(Get<Component>(chapter,"Cyclopse").gameObject.activeSelf,Is.True);
            Assert.That(Get<Component>(chapter,"Mannequin").gameObject.activeSelf,Is.True);
            Assert.That(Get<Component>(chapter,"Mask").gameObject.activeSelf,Is.False);
            var actualDoor=Components("Interactable").Single(x=>Get<string>(x,"stableId")==Get<string>(savedDoor,"id"));
            Assert.That(Get<bool>(actualDoor,"IsOpen"),Is.True);
            Assert.That(Get<Transform>(actualDoor,"movingLeaf").localPosition,Is.EqualTo(leaf));
            Assert.That(Get<Transform>(actualDoor,"secondaryLeaf").localPosition,Is.EqualTo(secondary));
            Assert.That(File.Exists(Path.Combine(directory,"chapter-suspend-v1.json")),Is.False);
            Assert.That(File.Exists(Path.Combine(directory,"chapter-suspend-v1.json.used")),Is.True);
            Assert.That(Get<bool>(Get<object>(session,"ChapterSuspension"),"HasRun"),Is.False);
            yield return RecoveryClick("resume"); yield return Delay(.12f);
            Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.GreaterThan(seconds));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolCheckpointRejectsCorruptGeometryBeforeMutatingFreshChapter()
        {
            Call(shell,"BeginChapter"); yield return null; Call(shell,"Pause");
            var data=Call(session,"CaptureChapterCheckpoint");
            var previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous); Call(session,"CreateChapter");
            var chapter=Get<Component>(session,"Chapter"); var pose=player.transform.position;
            var invalid=SchoolCheckpointCopy(data); Set(invalid,"version",999);
            Assert.Throws<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",invalid));
            var badDoor=SchoolCheckpointCopy(data); Set(Get<Array>(badDoor,"doors").GetValue(0),"secondary",new Vector3(100,0,0));
            Assert.Throws<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",badDoor));
            var impossible=SchoolCheckpointCopy(data); Set(Get<object>(impossible,"player"),"position",new Vector3(90,0,90));
            Assert.Throws<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",impossible));
            Assert.That(Get<int>(chapter,"Recovered"),Is.Zero); Assert.That(player.transform.position,Is.EqualTo(pose));
            Assert.That(Get<Component[]>(chapter,"Memories").All(x=>x.gameObject.activeSelf),Is.True);
            Assert.That(Get<Component>(chapter,"Cyclopse").gameObject.activeSelf,Is.False);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator SchoolSuspendWriteFailureKeepsLivePausedChapterAndAllowsActualRetry()
        {
            string directory=Path.Combine(NewRecordFixture(),"blocked-school-slot");
            File.WriteAllText(directory,"owned school write failure fixture"); Call(session,"ConfigureRecordDirectory",directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return Delay(.15f);
            yield return RecoveryPulse(Key.Escape); var previous=session; var pose=player.transform.position;
            yield return RecoveryClick("suspend-chapter");
            Assert.That(session,Is.SameAs(previous)); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<bool>(shell,"IsReloading"),Is.False); Assert.That(Get<string>(shell,"ReloadError"),Is.Not.Empty);
            Assert.That(player.transform.position,Is.EqualTo(pose));
            Assert.That(Get<bool>(Get<object>(session,"ChapterSuspension"),"HasRun"),Is.False);
            Assert.That(File.ReadAllText(directory),Is.EqualTo("owned school write failure fixture"));
            File.Delete(directory); yield return RecoveryClick("suspend-chapter"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(Get<bool>(Get<object>(session,"ChapterSuspension"),"HasRun"),Is.True);
            yield return RecoveryClick("continue-chapter"); Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero); Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
        }
    }
}
