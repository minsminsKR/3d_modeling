using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        object CheckpointCopy(object data) => JsonUtility.FromJson(JsonUtility.ToJson(data),RequireType("CorridorCheckpoint"));
        Component[] CheckpointThreats() => Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor")).ToArray();
        Component[] CheckpointItems(string kind) => Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()==kind && (kind!="Door" || x.name=="Corridor sliding door"))
            .OrderBy(x=>Get<string>(x,"stableId"),StringComparer.Ordinal).ToArray();
        IEnumerator RestoreCheckpointInFreshScene(object data)
        {
            var previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous);
            Call(session,"CreateCorridor",Get<int>(data,"seed")); Call(session,"ApplyCheckpoint",CheckpointCopy(data));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorCheckpointKeepsPartialRecognitionAndOngoingObservedSearch()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.15f);
            var brain=CheckpointThreats().First(x=>x.gameObject.activeSelf);
            foreach(var other in CheckpointThreats()) if(other!=brain) other.gameObject.SetActive(false);
            var direction=player.transform.forward; var at=player.transform.position+direction*3.7f;
            var agent=brain.GetComponent<NavMeshAgent>();
            Assert.That(NavMesh.SamplePosition(at,out var hit,.3f,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(hit.position),Is.True); brain.transform.rotation=Quaternion.LookRotation(-direction);
            Get<Light>(player,"flashlight").enabled=true;
            yield return Wait(()=>Get<float>(brain,"Awareness")>.08f,2,"No real partial visual recognition");
            Call(shell,"Pause");
            Assert.That(Get<float>(brain,"Awareness"),Is.InRange(.08f,.8f));
            var partial=Call(session,"CaptureCheckpoint");
            float awareness=Get<float>(brain,"Awareness"); var pose=brain.transform.position;
            yield return RestoreCheckpointInFreshScene(partial);
            brain=CheckpointThreats().OrderBy(x=>Vector3.Distance(x.transform.position,pose)).First();
            Assert.That(Get<float>(brain,"Awareness"),Is.EqualTo(awareness).Within(.0001f),"Reload erased accumulated evidence");
            Assert.That(Get<object>(brain,"state").ToString(),Is.Not.EqualTo("Chase"));
            Begin(); yield return Wait(()=>Get<object>(brain,"state").ToString()=="Chase",2,"Restored recognition never completed");
            var run=Get<Component>(session,"Corridor");
            var away=(Vector3)Call(run,"CellPosition",80)+Vector3.right;
            PlacePlayer(away); yield return Wait(()=>Get<object>(brain,"state").ToString()=="Search",5,"Lost actual sight did not start local search");
            yield return Delay(.3f); Call(shell,"Pause");
            var state=Call(brain,"CaptureProgress");
            Assert.That(Get<bool>(state,"searchStarted"),Is.True); Assert.That(Get<float>(state,"searchDwell"),Is.GreaterThan(0));
            var data=Call(session,"CaptureCheckpoint"); pose=brain.transform.position;
            yield return RestoreCheckpointInFreshScene(data);
            brain=CheckpointThreats().OrderBy(x=>Vector3.Distance(x.transform.position,pose)).First();
            var restored=Call(brain,"CaptureProgress");
            Assert.That(Get<object>(restored,"state").ToString(),Is.EqualTo("Search"));
            Assert.That(Get<Vector3>(restored,"searchOrigin"),Is.EqualTo(Get<Vector3>(state,"searchOrigin")));
            Assert.That(Get<Vector3>(restored,"searchTarget"),Is.EqualTo(Get<Vector3>(state,"searchTarget")));
            Assert.That(Get<float>(restored,"memory"),Is.EqualTo(Get<float>(state,"memory")).Within(.0001f));
            Assert.That(Get<float>(restored,"searchDwell"),Is.EqualTo(Get<float>(state,"searchDwell")).Within(.0001f));
            Assert.That(Get<int>(restored,"visited"),Is.EqualTo(Get<int>(state,"visited")));
            Assert.That(Vector3.Distance(Get<Vector3>(restored,"searchOrigin"),player.transform.position),Is.GreaterThan(20));
            Begin(); yield return Wait(()=>Get<object>(brain,"state").ToString()=="Patrol",12,"Restored local search did not expire");
            Assert.That(Get<Vector3>(brain,"SearchOrigin"),Is.EqualTo(Get<Vector3>(state,"searchOrigin")));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.2f);
            var memories=CheckpointItems("CorridorMemory"); Call(memories[0],"Use",player); Call(memories[1],"Use",player);
            var supply=CheckpointItems("FirecrackerSupply")[0]; Call(supply,"Use",player);
            yield return Delay(.15f);
            yield return KeysObserved(Key.W,Key.LeftShift); yield return Delay(.25f); Keys(); yield return null;
            Assert.That(Get<float>(player,"Stamina"),Is.LessThan(1));
            Assert.That((bool)Call(player,"TrySetCrouching",true),Is.True);
            var light=Get<Light>(player,"flashlight"); light.enabled=false;
            var door=CheckpointItems("Door")[0]; Assert.That((bool)Call(door,"OpenForPursuer"),Is.True);
            yield return Delay(.12f);
            var brain=CheckpointThreats().First(x=>x.gameObject.activeSelf);
            Assert.That(NavMesh.SamplePosition(brain.transform.position+Vector3.forward*.7f,out var noise,.5f,NavMesh.AllAreas),Is.True);
            Assert.That((bool)Call(brain,"HearNoise",noise.position,6f),Is.True);
            Call(shell,"Pause"); yield return null;
            var data=Call(session,"CaptureCheckpoint"); var json=JsonUtility.ToJson(data,true);
            CloudExperienceTests.Artifact("corridor-checkpoint-state.json",Encoding.UTF8.GetBytes(json));
            var parsed=CheckpointCopy(data); var p=Get<object>(parsed,"player");
            Assert.That(Get<int>(p,"stock"),Is.EqualTo(2)); Assert.That(Get<bool>(p,"crouched"),Is.True);
            var savedDoor=Get<Array>(parsed,"doors").GetValue(0); var leaf=Get<Vector3>(savedDoor,"leaf");
            Assert.That(leaf.x,Is.InRange(.01f,2.7f),"Fixture did not capture a partly sliding leaf");
            float seconds=Get<float>(parsed,"seconds");
            var before=session; Call(shell,"Restart",false); yield return RecoveryRebind(before);
            Call(session,"CreateCorridor",73); Call(session,"ApplyCheckpoint",parsed);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(2)); Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.EqualTo(seconds));
            Assert.That(Get<bool>(session,"Finished"),Is.False); Assert.That(Get<bool>(session,"InputAllowed"),Is.False);
            Assert.That(Vector3.Distance(player.transform.position,Get<Vector3>(p,"position")),Is.LessThan(.001f));
            Assert.That(Get<float>(player,"Stamina"),Is.EqualTo(Get<float>(p,"stamina")).Within(.0001f));
            Assert.That(Get<bool>(player,"Crouching"),Is.True); Assert.That(Get<Light>(player,"flashlight").enabled,Is.False);
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(2));
            Assert.That(CheckpointItems("CorridorMemory").Take(2).All(x=>!x.gameObject.activeSelf),Is.True);
            Assert.That(CheckpointItems("FirecrackerSupply")[0].gameObject.activeSelf,Is.False);
            var restoredDoor=CheckpointItems("Door")[0]; Assert.That(Get<bool>(restoredDoor,"IsOpen"),Is.True);
            Assert.That(Vector3.Distance(Get<Transform>(restoredDoor,"movingLeaf").localPosition,leaf),Is.LessThan(.001f));
            var savedThreats=Get<Array>(parsed,"threats");
            var actual=CheckpointThreats();
            // Enumeration order is not used as identity; match saved active poses.
            foreach(var saved in savedThreats)
            {
                var match=actual.OrderBy(x=>Vector3.Distance(x.transform.position,Get<Vector3>(saved,"position"))).First();
                Assert.That(Vector3.Distance(match.transform.position,Get<Vector3>(saved,"position")),Is.LessThan(.01f));
                Assert.That(match.gameObject.activeSelf,Is.EqualTo(Get<bool>(saved,"active")));
                var restored=Call(match,"CaptureProgress");
                Assert.That(Get<object>(restored,"state").ToString(),Is.EqualTo(Get<object>(saved,"state").ToString()));
                Assert.That(Get<float>(restored,"memory"),Is.EqualTo(Get<float>(saved,"memory")).Within(.0001f));
                Assert.That(Get<float>(restored,"awareness"),Is.EqualTo(Get<float>(saved,"awareness")).Within(.0001f));
                Assert.That(Get<Vector3>(restored,"lastKnown"),Is.EqualTo(Get<Vector3>(saved,"lastKnown")));
                Assert.That(Get<int>(restored,"noises"),Is.EqualTo(Get<int>(saved,"noises")));
            }
            Begin(); yield return KeysObserved(Key.W); yield return Delay(.25f); Keys();
            Assert.That(Get<float>(session,"ElapsedPlayTime"),Is.GreaterThan(seconds));
            Assert.That(Vector3.Distance(player.transform.position,Get<Vector3>(p,"position")),Is.GreaterThan(.1f));
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(2));
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorCheckpointRejectsUnknownVersionAndBlockedPoseBeforeMutatingWorld()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.15f); Call(shell,"Pause");
            var run=Get<Component>(session,"Corridor"); var data=Call(session,"CaptureCheckpoint");
            var unknown=CheckpointCopy(data); Set(unknown,"version",2);
            Assert.Throws<ArgumentException>(()=>Call(run,"RestoreCheckpoint",unknown));
            var corrupt=CheckpointCopy(data); var p=Get<object>(corrupt,"player"); Set(p,"stamina",float.NaN);
            Assert.Throws<ArgumentException>(()=>Call(run,"RestoreCheckpoint",corrupt));
            var blocked=CheckpointCopy(data); Set(Get<object>(blocked,"player"),"position",new Vector3(197,.08f,200));
            Set(blocked,"recovered",Enumerable.Repeat(true,5).ToArray());
            var leaves=CheckpointItems("Door").Select(x=>Get<Transform>(x,"movingLeaf").localPosition).ToArray();
            var position=player.transform.position; var evidence=CheckpointThreats().Select(x=>Get<Vector3>(x,"LastKnownPosition")).ToArray();
            Assert.Throws<ArgumentException>(()=>Call(run,"RestoreCheckpoint",blocked));
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero); Assert.That(player.transform.position,Is.EqualTo(position));
            Assert.That(CheckpointItems("CorridorMemory").All(x=>x.gameObject.activeSelf),Is.True);
            Assert.That(CheckpointItems("Door").Select(x=>Get<Transform>(x,"movingLeaf").localPosition).ToArray(),Is.EqualTo(leaves));
            Assert.That(CheckpointThreats().Select(x=>Get<Vector3>(x,"LastKnownPosition")).ToArray(),Is.EqualTo(evidence));
            yield return null;
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorCheckpointRefusesHiddenPlayerAndLiveThrownItem()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.15f);
            var cabinet=CheckpointItems("HidingPlace").First(x=>x.name=="Corridor hiding cabinet");
            PlacePlayer(Get<Transform>(cabinet,"outside").position); Call(cabinet,"Use",player);
            Assert.That(Get<bool>(player,"Hidden"),Is.True); Call(shell,"Pause");
            Assert.Throws<InvalidOperationException>(()=>Call(session,"CaptureCheckpoint"));
            Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Call(shell,"Resume"); Call(cabinet,"Use",player); Assert.That(Get<bool>(player,"Hidden"),Is.False);
            PlacePlayer(new Vector3(200,.03f,200));
            yield return null; var stock=Get<Component>(player,"Firecrackers");
            Assert.That((bool)Call(stock,"TryThrow"),Is.True); Call(shell,"Pause");
            Assert.Throws<InvalidOperationException>(()=>Call(session,"CaptureCheckpoint"));
            Assert.That(Get<int>(stock,"Count"),Is.Zero); yield return null;
        }
    }
}
