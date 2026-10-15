using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component PlaceTestTrapThroughInventory()
        {
            var inventory=Get<Component>(player,"Traps");
            var mesh=NavMesh.CalculateTriangulation();
            var origin=player.transform.position;
            var sites=Enumerable.Range(0,mesh.indices.Length/3).Select(i=>
                (mesh.vertices[mesh.indices[i*3]]+mesh.vertices[mesh.indices[i*3+1]]+mesh.vertices[mesh.indices[i*3+2]])/3)
                .Where(p=>Mathf.Abs(p.y-origin.y)<.25f && Vector3.Distance(p,origin)<25)
                .OrderBy(p=>Vector3.Distance(p,origin)).Take(120);
            foreach(var site in sites)
            {
                if(!NavMesh.FindClosestEdge(site,out var edge,NavMesh.AllAreas)||edge.distance<.5f)continue;
                PlacePlayer(site,false);
                foreach(float yaw in new[]{0,90,180,270})
                {
                    player.transform.rotation=Quaternion.Euler(0,yaw,0);
                    Get<Camera>(player,"eyes").transform.localRotation=Quaternion.identity;
                    int before=Get<int>(inventory,"Count");
                    if((bool)Call(inventory,"TryPlace"))
                    {
                        Assert.That(Get<int>(inventory,"Count"),Is.EqualTo(before-1));
                        return Get<Component>(inventory,"LastPlaced");
                    }
                    Assert.That(Get<int>(inventory,"Count"),Is.EqualTo(before),"Invalid ground consumed a trap");
                }
            }
            Assert.Fail("No supported clear production footprint accepted a trap");return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TrapPlacementUsesSupportedGroundAndPausedInputNeverConsumesStock()
        {
            Call(session,"CreateChapter");Begin();IsolateThreats();
            var inventory=Get<Component>(player,"Traps");Assert.That(Get<int>(inventory,"Count"),Is.EqualTo(1));
            var trap=PlaceTestTrapThroughInventory();
            var look=Get<Camera>(player,"eyes").transform.rotation;
            Assert.That(Get<bool>(trap,"Armed"),Is.True);
            Assert.That(trap.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThanOrEqualTo(3));
            Assert.That(trap.GetComponentsInChildren<Transform>().Count(t=>t.name=="JawFront"||t.name=="JawBack"),Is.EqualTo(2));
            var cue=trap.GetComponent(RequireType("TrapAudio"));
            Assert.That(Get<string>(cue,"LastCue"),Is.EqualTo("trap-place"));Assert.That(Get<int>(cue,"CuesPlayed"),Is.EqualTo(1));
            Assert.That((bool)Call(inventory,"AddSupply"),Is.True);int stock=Get<int>(inventory,"Count");
            Call(shell,"Pause"); Keys(Key.R);yield return Delay(.15f);
            Assert.That(Get<int>(inventory,"Count"),Is.EqualTo(stock));
            Assert.That((bool)Call(inventory,"TryPlace"),Is.False);
            Keys();yield return null;Call(shell,"Resume");yield return Delay(.55f);
            Get<Camera>(player,"eyes").transform.rotation=look;
            Assert.That((bool)Call(inventory,"TryPlace"),Is.False,"The occupied footprint must reject a duplicate trap");
            Assert.That(Get<int>(inventory,"Count"),Is.EqualTo(stock));
            Object.Destroy(trap.gameObject);yield return null;
            yield return KeysObserved(Key.R);
            yield return Wait(()=>Get<int>(inventory,"Count")==stock-1,1,"Actual R input did not install the trap",()=>Get<string>(inventory,"Feedback")+"; "+InputDiagnostics());
            Keys();yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AllSixMonstersAreRestrainedFiveSecondsWithoutAttackingAndKeepCaptureFaces()
        {
            IsolateThreats();Begin();((Behaviour)player).enabled=false;
            var cabinet=SchoolCabinet();var entrance=Get<Transform>(cabinet,"outside").position;
            foreach(string key in new[]{"Cyclopse","Uncat","Baby","Hwacat","Mask","Mannequin"})
            {
                PlacePlayer(entrance);var actor=CabinetActor(key,cabinet);
                Get<Camera>(player,"eyes").transform.rotation=Quaternion.LookRotation(player.transform.position-actor.transform.position);
                yield return CabinetActorPursuing(key,actor);
                yield return Wait(()=>Get<bool>(actor,"AttackActive"),10,key+" never reached its genuine physical attack warning");
                var agent=actor.GetComponent<NavMeshAgent>();
                var trap=(Component)Call(RequireType("PlacedMonsterTrap"),"Create",session.transform,actor.transform.position,Quaternion.identity,false);
                yield return Wait(()=>Get<bool>(trap,"Triggered"),1,key+" did not step on its real trap footprint");
                var hold=Get<Component>(trap,"Captured");var position=actor.transform.position;var rotation=actor.transform.rotation;
                float warning=Get<float>(actor,"AttackWindup");
                float start=Time.time-(5-Get<float>(hold,"Remaining"));
                yield return Delay(.24f);
                var face=actor.GetComponent(RequireType("EnemyChaseFace"));
                if(face)Assert.That(Get<float>(face,"Weight"),Is.GreaterThan(.95f),key+" did not use its capture expression");
                if(key=="Cyclopse"||key=="Uncat")Assert.That(Get<float>(face,"CabinetWeight"),Is.GreaterThan(.95f));
                Assert.That(Get<float>(actor,"AttackWindup"),Is.EqualTo(warning),key+" advanced its paused attack");
                Call(shell,"Pause");float remaining=Get<float>(hold,"Remaining");yield return Delay(.18f);
                Assert.That(Get<float>(hold,"Remaining"),Is.EqualTo(remaining));Call(shell,"Resume");
                yield return Delay(3.8f);
                Assert.That(Get<bool>(hold,"Held"),Is.True,key+" released before five gameplay seconds");
                Assert.That(Vector3.Distance(actor.transform.position,position),Is.LessThan(.002f),key+" moved while caught");
                Assert.That(Quaternion.Angle(actor.transform.rotation,rotation),Is.LessThan(.01f));
                Assert.That(Get<bool>(session,"Finished"),Is.False,key+" killed the player while trapped");
                // Leave before release: resumed attacks must recheck actual contact.
                PlacePlayer(new Vector3(200,5,200),false);
                yield return Wait(()=>!Get<bool>(hold,"Held"),2,key+" never resumed after five seconds");
                Assert.That(Time.time-start,Is.InRange(4.98f,5.35f));
                yield return Wait(()=>Get<bool>(trap,"Spent"),.6f,key+" did not release its trap");
                var cue=trap.GetComponent(RequireType("TrapAudio"));
                Assert.That(Get<string>(cue,"LastCue"),Is.EqualTo("trap-release"));Assert.That(Get<int>(cue,"CuesPlayed"),Is.EqualTo(2));
                Assert.That(Get<bool>(session,"Finished"),Is.False);
                Assert.That(agent.updateRotation,Is.True,"Trap leaked its rotation lock");
                if(key=="Cyclopse"||key=="Uncat")
                    yield return Wait(()=>Get<float>(face,"CabinetWeight")==0,1,"Release did not restore the ordinary face");
                actor.gameObject.SetActive(false);Object.Destroy(trap.gameObject);yield return null;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement()
        {
            Call(session,"CreateCorridor",73);Begin();IsolateThreats();
            var trap=PlaceTestTrapThroughInventory();var position=trap.transform.position;
            var pickup=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="TrapSupply");
            string pickupId=Get<string>(pickup,"stableId");
            Call(pickup,"Use",player);Assert.That(pickup.gameObject.activeSelf,Is.False);
            int stock=Get<int>(Get<Component>(player,"Traps"),"Count");
            Call(shell,"Pause");var data=Call(session,"CaptureCheckpoint");
            var state=Get<object>(data,"traps");Assert.That(Get<Array>(state,"armed").Length,Is.EqualTo(1));
            var legacy=CheckpointCopy(data);Set(legacy,"traps",null);Call(legacy,"Validate");
            yield return RestoreCheckpointInFreshScene(data);
            Assert.That(Get<int>(Get<Component>(player,"Traps"),"Count"),Is.EqualTo(stock));
            var restored=Components("PlacedMonsterTrap").Single();
            Assert.That(Vector3.Distance(restored.transform.position,position),Is.LessThan(.003f));
            Assert.That(Get<bool>(restored,"Armed"),Is.True);
            Assert.That(Get<int>(restored.GetComponent(RequireType("TrapAudio")),"CuesPlayed"),Is.Zero,"Restoring a paused save replayed placement audio");
            Assert.That(Components("Interactable").Single(x=>Get<object>(x,"kind").ToString()=="TrapSupply" && Get<string>(x,"stableId")==pickupId).gameObject.activeSelf,Is.False);
        }
    }
}
