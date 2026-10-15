using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        void AdvancedDoorBodyClear(Component door, Component actor)
        {
            var agent=actor.GetComponent<NavMeshAgent>(); float radius=agent.radius-.015f;
            var at=actor.transform.position;
            var primary=Get<Transform>(door,"movingLeaf"); var secondary=Get<Transform>(door,"secondaryLeaf");
            var overlaps=Physics.OverlapCapsule(at+Vector3.up*(radius+.09f),at+Vector3.up*(agent.height-radius),radius,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Assert.That(overlaps.Any(x=>x.transform.IsChildOf(primary)||secondary&&x.transform.IsChildOf(secondary)),Is.False,
                actor.GetType().Name+" penetrated a real school leaf at "+at);
        }
        string AdvancedDoorDiagnostics(Component actor)
        {
            var helper=actor.GetComponent(RequireType("StalkerDoorTraversal"));
            string gates=actor.GetType().Name=="LanternMaskEncounter"?
                " phase="+Get<object>(actor,"State")+" intro="+Get<bool>(actor,"IntroCompleted"):
                " released="+Get<bool>(actor,"Released")+" observed="+Get<bool>(actor,"Observed")+" moving="+Get<bool>(actor,"Moving");
            return "player="+player.transform.position+" light="+Get<Light>(player,"flashlight").enabled+gates+
                " helper="+(helper?Get<string>(helper,"Diagnostics"):"missing");
        }
        object CopyAdvancedProgress(object data)=>JsonUtility.FromJson(JsonUtility.ToJson(data),data.GetType());
        IEnumerator PrepareAdvancedSchoolActor(string actorKind)
        {
            Call(shell,"BeginChapter"); yield return null; yield return null;
            IntroSetup(actorKind);
            var chapter=Get<Component>(session,"Chapter"); var memories=Get<Component[]>(chapter,"Memories");
            var appearances=Get<Component>(chapter,"FirstAppearances");
            Call(memories[0],"Use",player);
            yield return Wait(()=>!Get<bool>(appearances,"CameraOwned"),30,"First memory appearance did not return player control");
            Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(1));
            Call(memories[1],"Use",player);
            yield return Wait(()=>!Get<bool>(appearances,"CameraOwned"),10,"Second memory appearance did not return player control");
            Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(2));
            if(actorKind=="LanternMaskEncounter")
            {
                Call(memories[2],"Use",player);
                Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));
            }
            foreach(var brain in Components("StalkerBrain")) brain.gameObject.SetActive(false);
            var other=Get<Component>(chapter,actorKind=="LanternMaskEncounter"?"Mannequin":"Mask"); other.gameObject.SetActive(false);
            var actor=Get<Component>(chapter,actorKind=="LanternMaskEncounter"?"Mask":"Mannequin");
            Assert.That(actor.gameObject.activeInHierarchy,Is.True,"Real chapter release did not activate the source actor");
            if(actorKind=="WeepingAngelEncounter")
            {
                // The school shot is discovered only at the actual junction.
                // Finish that public event before the separate door fixture.
                PlacePlayer(new Vector3(13.8f,.03f,.1f));
                IntroCamera.transform.rotation=Quaternion.LookRotation(actor.transform.position+Vector3.up*1.4f-IntroCamera.transform.position);
                yield return Wait(()=>Get<bool>(actor,"Triggered"),2,"Junction view did not start the real school mannequin introduction");
                yield return Wait(()=>!Get<bool>(appearances,"CameraOwned")&&Get<bool>(actor,"Released"),6,
                    "Real school mannequin shot did not return before the door fixture");
            }
            IntroPlace(actor.transform.position,actor.transform.position+Vector3.up*1.4f);
            float introRealStart=Time.realtimeSinceStartup,introGameStart=Time.time;
            yield return Wait(()=>Get<bool>(actor,actorKind=="LanternMaskEncounter"?"IntroCompleted":"Released"),5,
                "Actual first sight did not complete the harmless actor intro",()=>
                "actor="+actorKind+" enabled="+((Behaviour)actor).enabled+" step="+Get<int>(session,"EncounterStep")+
                " input="+Get<bool>(session,"InputAllowed")+" nav="+actor.GetComponent<NavMeshAgent>().isOnNavMesh+
                " introStage="+IntroPhase(actor)+" elapsed="+IntroClock(actor)+" scale="+Time.timeScale+
                " gameDelta="+(Time.time-introGameStart)+" realDelta="+(Time.realtimeSinceStartup-introRealStart)+
                " player="+player.transform.position+" actorPos="+actor.transform.position+
                " eyes="+IntroCamera.transform.position+" view="+IntroCamera.WorldToViewportPoint(actor.transform.position+Vector3.up*1.4f));
            PlacePlayer(new Vector3(200,.03f,200),false);
            Assert.That(actor.GetComponent(RequireType("StalkerDoorTraversal")),Is.Not.Null);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SchoolMaskShattersClosedLeavesInAllMovementStatesAndRestoresSilentRubble()
        {
            yield return PrepareAdvancedSchoolActor("LanternMaskEncounter");
            var chapter=Get<Component>(session,"Chapter");var actor=Get<Component>(chapter,"Mask");
            var original=Call(actor,"CaptureChapterProgress");
            var door=TraversalDoor(false,d=>Get<Transform>(d,"secondaryLeaf"));var normal=Get<Vector3>(door,"DoorNormal");
            var marker=new GameObject("CloudQA physical wraith smash patrol");marker.transform.position=SupportedDoorSide(door,2.1f);
            Set(actor,"patrol",new[]{marker.transform,marker.transform});
            var primary=Get<Transform>(door,"movingLeaf");var secondary=Get<Transform>(door,"secondaryLeaf");
            var closed=primary.localPosition;var second=secondary.localPosition;
            foreach(string phase in new[]{"Wander","Investigate","Chase"})
            {
                ((Behaviour)actor).enabled=false;Call(door,"RestoreSchoolDoor",false,closed,second);
                Set(door,"DoorLocked",phase=="Wander");
                var data=CopyAdvancedProgress(original);
                Set(data,"position",SupportedDoorSide(door,-2.1f));Set(data,"rotation",Quaternion.LookRotation(normal));
                Set(data,"target",marker.transform.position);Set(data,"state",phase);Set(data,"memory",8f);Set(data,"waypoint",0);Set(data,"doorPassage",null);
                ((Behaviour)actor).enabled=true;Call(actor,"RestoreChapterProgress",data);
                yield return Wait(()=>Get<bool>(door,"DoorBroken"),8,"School wraith did not break a closed door in "+phase,()=>AdvancedDoorDiagnostics(actor));
                var debris=door.GetComponentInChildren(RequireType("DoorShatterDebris"));
                Assert.That(Get<int>(debris,"PieceCount"),Is.GreaterThanOrEqualTo(16),"Both real leaves need modeled panels, rails and splinters");
                Assert.That(debris.GetComponentsInChildren<Collider>(),Is.Empty);
                var dust=debris.GetComponentInChildren(RequireType("DoorBreakDust"));
                Assert.That(dust,Is.Not.Null,"Actual break omitted its wood-dust cloud");
                Assert.That(dust.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.GreaterThan(0));
                Assert.That(dust.GetComponentsInChildren<Light>(),Is.Empty,"Wood dust must not add an impact flash");
                var voice=door.GetComponent(RequireType("DoorDestructionAudio"));
                Assert.That(Get<int>(voice,"CuesPlayed"),Is.EqualTo(phase=="Wander"?1:phase=="Investigate"?2:3));
                if(phase=="Investigate")
                {
                    Call(shell,"Pause");var pieces=debris.GetComponentsInChildren<MeshRenderer>();var positions=pieces.Select(p=>p.transform.position).ToArray();
                    var systems=dust.GetComponentsInChildren<ParticleSystem>();
                    var dustClocks=systems.Select(p=>p.time).ToArray();
                    var particles=systems.Select(p=>{var buffer=new ParticleSystem.Particle[p.main.maxParticles];int count=p.GetParticles(buffer);return buffer.Take(count).Select(v=>v.position).ToArray();}).ToArray();
                    yield return Delay(.2f);
                    for(int i=0;i<pieces.Length;i++)Assert.That(pieces[i].transform.position,Is.EqualTo(positions[i]));
                    Assert.That(systems.Select(p=>p.time).ToArray(),Is.EqualTo(dustClocks),"Pause advanced the dust clocks");
                    for(int i=0;i<systems.Length;i++)
                    {var buffer=new ParticleSystem.Particle[systems[i].main.maxParticles];int count=systems[i].GetParticles(buffer);Assert.That(buffer.Take(count).Select(v=>v.position).ToArray(),Is.EqualTo(particles[i]),"Paused dust continued drifting");}
                    Call(shell,"Resume");
                }
                yield return Wait(()=>Vector3.Dot(actor.transform.position-door.transform.position,normal)>.8f,8,"Wraith did not pass its broken doorway");
                yield return Wait(()=>Get<bool>(debris,"Settled"),2,"Fragments never settled");
                yield return Delay(1.6f);
                Assert.That(debris&&debris.gameObject.activeSelf,Is.True,"Rubble vanished after the old 1.4 second lifetime");
                Assert.That(debris.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1),"Settled rubble should batch into one renderer");
                yield return Wait(()=>!dust,4,"Transient dust survived after its plume faded");
                AdvancedDoorBodyClear(door,actor);
                Assert.That(Get<bool>(door,"DoorOperable"),Is.False);
            }
            // Restore the other progression-owned actors before a real school save.
            foreach(string name in new[]{"Cyclopse","Mannequin"})
            {var other=Get<Component>(chapter,name);other.gameObject.SetActive(true);((Behaviour)other).enabled=false;}
            PlacePlayer(SupportedDoorSide(door,-2.1f),false);
            Call(shell,"Pause");var saved=SchoolCheckpointCopy(Call(session,"CaptureChapterCheckpoint"));
            string id=Get<string>(door,"stableId");
            Assert.That(Get<bool>(Get<Array>(saved,"doors").Cast<object>().Single(d=>Get<string>(d,"id")==id),"broken"),Is.True);
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",saved);chapter=Get<Component>(session,"Chapter");
            Call(chapter,"PrepareCheckpointNavigation",saved);yield return null;yield return null;Call(session,"ApplyChapterCheckpoint",saved);
            door=Components("Interactable").Single(d=>Get<string>(d,"stableId")==id);
            Assert.That(Get<bool>(door,"DoorBroken"),Is.True);
            var restored=door.GetComponentInChildren(RequireType("DoorShatterDebris"));
            Assert.That(Get<bool>(restored,"Settled"),Is.True);
            Assert.That(restored.GetComponentInChildren(RequireType("DoorBreakDust")),Is.Null,"Save restoration replayed the transient dust");
            Assert.That(door.GetComponent(RequireType("DoorDestructionAudio")),Is.Null,"Save restoration replayed a break voice");
            Assert.That(Get<Transform>(door,"movingLeaf").GetComponentsInChildren<Collider>().Any(c=>c.enabled),Is.False);
            UnityEngine.Object.Destroy(marker);
        }


        [UnityTest, Timeout(90000)]
        public IEnumerator AdvancedDoorTraversalSchoolMannequinHonorsGazeLightPauseAndRestoresRealPassage()
        {
            yield return PrepareAdvancedSchoolActor("WeepingAngelEncounter");
            var actor=Get<Component>(Get<Component>(session,"Chapter"),"Mannequin");
            var door=TraversalDoor(false); var normal=Get<Vector3>(door,"DoorNormal");
            var original=CopyAdvancedProgress(Call(actor,"CaptureChapterProgress"));
            Set(original,"position",SupportedDoorSide(door,-1.8f)); Set(original,"rotation",Quaternion.LookRotation(normal));
            Set(original,"doorPassage",null); Call(actor,"RestoreChapterProgress",original);
            var camera=Get<Camera>(player,"eyes"); var light=Get<Light>(player,"flashlight");
            PlacePlayer(SupportedDoorSide(door,2.4f,.75f),false);
            camera.transform.rotation=Quaternion.LookRotation(normal); light.enabled=false; Physics.SyncTransforms();
            var start=actor.transform.position; yield return Delay(.35f);
            Assert.That(actor.transform.position,Is.EqualTo(start)); Assert.That(Get<bool>(door,"IsOpen"),Is.False,
                "A light-off mannequin remotely operated a door");
            light.enabled=true;
            yield return Wait(()=>Get<bool>(door,"IsOpen"),6,"Released unseen lit mannequin never physically opened the real door",()=>AdvancedDoorDiagnostics(actor));
            Call(shell,"Pause"); var pose=actor.transform.position; var leaf=Get<Transform>(door,"movingLeaf"); var leafPose=leaf.localPosition;
            var saved=CopyAdvancedProgress(Call(actor,"CaptureChapterProgress"));
            Assert.That(Get<bool>(Get<object>(saved,"doorPassage"),"passing"),Is.True);
            Call(actor,"RestoreChapterProgress",saved);
            Assert.That(Vector3.Distance(actor.transform.position,pose),Is.LessThan(.002f),"JSON/NavMesh restore changed the actor pose beyond numeric precision");
            pose=actor.transform.position; // subsequent pause/light assertions remain exact
            yield return Delay(.2f); Assert.That(actor.transform.position,Is.EqualTo(pose)); Assert.That(leaf.localPosition,Is.EqualTo(leafPose));
            Call(shell,"Resume"); light.enabled=false;
            yield return Delay(.3f); Assert.That(actor.transform.position,Is.EqualTo(pose),"Light-off gate advanced a pending passage");
            light.enabled=true;
            float end=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<end)
            {
                yield return null; Physics.SyncTransforms(); AdvancedDoorBodyClear(door,actor);
                if(Vector3.Dot(actor.transform.position-door.transform.position,normal)>.8f &&
                    !Get<bool>(door,"IsOpen") && Get<bool>(door,"AtRequestedDoorPose")) break;
            }
            Assert.That(Vector3.Dot(actor.transform.position-door.transform.position,normal),Is.GreaterThan(.8f));
            Assert.That(Get<bool>(door,"IsOpen"),Is.False); Assert.That(Get<bool>(door,"AtRequestedDoorPose"),Is.True);
            Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")),"CuesPlayed"),Is.EqualTo(2));
            camera.transform.rotation=Quaternion.LookRotation(actor.transform.position+Vector3.up*1.2f-camera.transform.position);
            yield return null; pose=actor.transform.position; yield return Delay(.2f);
            Assert.That(Get<bool>(actor,"Observed"),Is.True); Assert.That(Get<bool>(actor,"Moving"),Is.False);
            Assert.That(actor.transform.position,Is.EqualTo(pose),"Actual gaze failed to stop the mannequin after passage");
        }
    }
}
