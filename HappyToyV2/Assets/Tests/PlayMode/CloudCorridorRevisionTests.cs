using System;
using System.Collections;
using System.Linq;
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
        IEnumerator RevisionPrepare()
        { Call(session,"CreateCorridor",73); IsolateThreats(); Begin(); yield return null; yield return null; }
        IEnumerator AimAtDoorway(Component door)
        {
            var previous=Mouse.current; var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                float until=Time.realtimeSinceStartup+5;
                while(Get<Component>(player,"Focus")!=door && Time.realtimeSinceStartup<until)
                {
                    var camera=Get<Camera>(player,"eyes"); var delta=door.transform.position+Vector3.up*1.18f-camera.transform.position;
                    float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                    float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
                    var pixels=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),-40,40),
                        -Mathf.Clamp(Mathf.DeltaAngle(camera.transform.localEulerAngles.x,pitch),-30,30))/Get<float>(player,"sensitivity");
                    InputSystem.QueueDeltaStateEvent(mouse.delta,pixels); yield return null;
                }
                Assert.That(Get<Component>(player,"Focus"),Is.SameAs(door));
            }
            finally { InputSystem.RemoveDevice(mouse); if(previous!=null&&previous.added) previous.MakeCurrent(); }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator OpenedSlidingDoorClosesWithRealEFromItsEmptyDoorwayAndCoverStillBlocksFocus()
        {
            yield return RevisionPrepare();
            var door=Components("CorridorShelterRoom").Select(room=>Get<Component>(room,"EntranceDoor")).First();
            var normal=Get<Vector3>(door,"DoorNormal");
            PlacePlayer(door.transform.position-normal*1.15f+Vector3.up*.03f); yield return null;
            Call(door,"Use",player);
            yield return Wait(()=>Get<bool>(door,"IsOpen")&&Get<bool>(door,"AtRequestedDoorPose"),4,"Door did not open");
            yield return AimAtDoorway(door); yield return KeysObserved(Key.E); Keys();
            yield return Wait(()=>!Get<bool>(door,"IsOpen")&&Get<bool>(door,"AtRequestedDoorPose"),4,"Real E could not close the opened sliding door");
            Assert.That((bool)Call(door,"OpenForPursuer"),Is.True);
            yield return Wait(()=>Get<bool>(door,"AtRequestedDoorPose"),4,"Reopening failed");
            var cover=Cube("CloudQA opaque doorway cover",door.transform.position-normal*.52f+Vector3.up*1.2f,new Vector3(2.4f,2.4f,.12f));
            cover.transform.rotation=Quaternion.LookRotation(normal); Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That(Get<Component>(player,"Focus"),Is.Not.SameAs(door),"Door query bypassed opaque cover");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator CorridorRunnerRejectsFootstepsAndDecoysAndRequiresFrontalUnoccludedSight()
        {
            yield return RevisionPrepare(); ((Behaviour)player).enabled=false;
            var run=Get<Component>(session,"Corridor"); var mask=Get<Component>(run,"Mask");
            mask.gameObject.SetActive(true); ((Behaviour)mask).enabled=true;
            var agent=mask.GetComponent<NavMeshAgent>(); agent.enabled=true;
            var layout=Get<object>(run,"Layout"); int maskBits=Get<int[]>(layout,"Connections")[0];
            int heading=Enumerable.Range(0,4).First(d=>(maskBits&(1<<d))!=0);
            var forward=new Vector3(new[]{0,1,0,-1}[heading],0,new[]{1,0,-1,0}[heading]);
            var centre=(Vector3)Call(run,"CellPosition",0);
            Assert.That(agent.Warp(centre),Is.True); mask.transform.rotation=Quaternion.LookRotation(forward);
            PlacePlayer(centre-forward*2.2f,false);
            agent.updateRotation=false; yield return null;
            Assert.That(((Behaviour)mask).isActiveAndEnabled,Is.True);
            int accepted=Get<int>(mask,"FootstepNoisesAccepted");
            // Controlled production callbacks isolate hearing from movement and
            // recognition. The player's physical footstep pipeline is tested separately.
            var footsteps=mask.GetType().GetMethod("HearFootstep",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(footsteps,Is.Not.Null); footsteps.Invoke(mask,new object[]{centre+forward*2,28f});
            Assert.That((bool)Call(mask,"HearNoise",centre+forward*2,8f),Is.False);
            Assert.That(Get<int>(mask,"FootstepNoisesAccepted"),Is.EqualTo(accepted));
            Assert.That(Get<object>(mask,"State").ToString(),Is.EqualTo("Wander"));
            ((Behaviour)mask).enabled=false; agent.isStopped=true; yield return null;
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.False,"Runner detected a player behind its back");
            PlacePlayer(centre+forward*2.2f,false); Physics.SyncTransforms();
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.True,"Frontal actual player was not visible");
            var visual=mask.GetComponent(RequireType("MaskHorrorVisual")); var head=Get<Transform>(visual,"HeadSocket");
            var target=player.transform.position+Vector3.up*Get<float>(player,"SightTargetHeight");
            var cover=Cube("CloudQA corner blind spot",(head.position+target)*.5f,new Vector3(1.4f,2.4f,.16f));
            cover.transform.rotation=Quaternion.LookRotation(forward); Physics.SyncTransforms();
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.False,"Runner detected through corner cover");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator ShelterRoomsHaveFiniteShelfLootCabinetsReachableCentresAndRealDoorCover()
        {
            yield return RevisionPrepare(); ((Behaviour)player).enabled=false;
            var run=Get<Component>(session,"Corridor"); var rooms=Components("CorridorShelterRoom");
            Assert.That(Get<int>(Get<object>(run,"Layout"),"Version"),Is.EqualTo(5));
            Assert.That(rooms.Length,Is.GreaterThanOrEqualTo(2));
            var route=new NavMeshPath(); var start=(Vector3)Call(run,"CellPosition",0);
            foreach(var room in rooms)
            {
                Assert.That(NavMesh.CalculatePath(start,room.transform.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete,Is.True);
                Assert.That(Get<Component>(room,"EntranceDoor"),Is.Not.Null);
            }
            Assert.That(Components("CorridorFurniturePlacement").Any(item=>Get<string>(item,"PropKey")=="archive-shelf"&&rooms.Any(room=>Get<int>(room,"Cell")==Get<int>(item,"Cell"))),Is.True);
            Assert.That(Components("Interactable").Any(item=>Get<object>(item,"kind").ToString()=="HidingPlace"&&rooms.Any(room=>Vector3.Distance(room.transform.position,item.transform.position)<3)),Is.True);
            var selected=rooms.First(room=>!Get<bool>(room,"HasSeparateExit")); var door=Get<Component>(selected,"EntranceDoor");
            var inward=selected.transform.position-door.transform.position; inward.y=0; inward.Normalize();
            var mask=Get<Component>(run,"Mask"); mask.gameObject.SetActive(true); ((Behaviour)mask).enabled=false;
            var agent=mask.GetComponent<NavMeshAgent>(); agent.enabled=true;
            Assert.That(agent.Warp(door.transform.position-inward*2),Is.True); agent.isStopped=true;
            mask.transform.rotation=Quaternion.LookRotation(inward); PlacePlayer(selected.transform.position,false); yield return null;
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.False,"Closed room did not block detection");
            Assert.That((bool)Call(door,"OpenForPursuer"),Is.True);
            yield return Wait(()=>Get<bool>(door,"AtRequestedDoorPose"),4,"Room entrance could not be opened by a pursuer");
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.True,"Opened room unexpectedly remained invisible");
            mask.gameObject.SetActive(false); Call(shell,"Pause"); yield return null;
            var checkpoint=Call(run,"CaptureCheckpoint"); Call(checkpoint,"Validate");
            Assert.That(Get<int>(checkpoint,"simulationVersion"),Is.EqualTo(5));
            Assert.That(Get<Array>(checkpoint,"doors").Length,Is.GreaterThanOrEqualTo(rooms.Length));
        }

        Component[] ShelterDoors(Component room) => ((IEnumerable)Get<object>(room,"Doors")).Cast<Component>().ToArray();
        IEnumerator ShelterWalkTo(Vector3 destination)
        {
            var path=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(player.transform.position,destination,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,Is.True);
            var previous=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                foreach(var corner in path.corners.Skip(1))
                {
                    float deadline=Time.realtimeSinceStartup+12;
                    while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(corner.x,corner.z))>.18f)
                    {
                        Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Actual W movement blocked at "+player.transform.position+" toward "+corner);
                        var camera=Get<Camera>(player,"eyes");var delta=corner-player.transform.position;delta.y=0;
                        float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                        var pixels=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),-40,40),
                            Mathf.Clamp(Mathf.DeltaAngle(camera.transform.localEulerAngles.x,0),-30,30))/Get<float>(player,"sensitivity");
                        InputSystem.QueueDeltaStateEvent(mouse.delta,pixels);
                        Keys(Vector3.Angle(player.transform.forward,delta)<12?new[]{Key.W}:Array.Empty<Key>());yield return null;
                    }
                }
            }
            finally { Keys();InputSystem.RemoveDevice(mouse);if(previous!=null&&previous.added)previous.MakeCurrent(); }
            yield return null;
        }
        IEnumerator ShelterToggleWithE(Component door,bool open)
        {
            Keys();yield return null;yield return AimAtDoorway(door);
            yield return KeysObserved(Key.E);Keys();
            yield return Wait(()=>Get<bool>(door,"IsOpen")==open&&Get<bool>(door,"AtRequestedDoorPose"),4,"Actual E could not change shelter door to "+open);
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator TwoDoorShelterAllowsRealInputEntryClosingBehindAndEscapeThroughSeparateExit()
        {
            yield return RevisionPrepare();
            var room=Components("CorridorShelterRoom").First(item=>Get<bool>(item,"HasSeparateExit"));
            var doors=ShelterDoors(room);Assert.That(doors,Has.Length.EqualTo(2));Assert.That(doors[0],Is.Not.SameAs(doors[1]));
            var inward=(room.transform.position-doors[0].transform.position).normalized;
            PlacePlayer(doors[0].transform.position-inward*1.55f+Vector3.up*.03f);yield return null;
            yield return ShelterToggleWithE(doors[0],true);
            yield return ShelterWalkTo(doors[0].transform.position+inward*1.5f);
            yield return ShelterToggleWithE(doors[0],false);
            Assert.That(Get<bool>(doors[1],"IsOpen"),Is.False,"Operating the entrance changed the separate exit");
            yield return ShelterWalkTo(room.transform.position);
            var outward=(doors[1].transform.position-room.transform.position).normalized;
            yield return ShelterWalkTo(doors[1].transform.position-outward*1.5f);
            yield return ShelterToggleWithE(doors[1],true);
            yield return ShelterWalkTo(doors[1].transform.position+outward*1.7f);
            Assert.That(Vector3.Dot(player.transform.position-doors[1].transform.position,outward),Is.GreaterThan(1.3f));
            Assert.That(Get<bool>(doors[0],"IsOpen"),Is.False,"Escape required reopening the entrance");
            Assert.That(Physics.Raycast(player.transform.position+Vector3.up*.15f,Vector3.down,.6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator TwoDoorShelterPursuersOpenCrossAndCloseBothPhysicalDoors()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var room=Components("CorridorShelterRoom").First(item=>Get<bool>(item,"HasSeparateExit"));var doors=ShelterDoors(room);
            var inward=(room.transform.position-doors[0].transform.position).normalized;
            var outward=(doors[1].transform.position-room.transform.position).normalized;
            var start=doors[0].transform.position-inward*2.3f;var end=doors[1].transform.position+outward*3.1f;
            foreach(string key in new[]{"Cyclopse","Uncat"})
            {
                var actor=ReadabilityActor(key);actor.gameObject.SetActive(true);((Behaviour)actor).enabled=true;
                var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=true;
                Assert.That(NavMesh.SamplePosition(start,out var sample,.6f,NavMesh.AllAreas)&&agent.Warp(sample.position),Is.True);
                actor.transform.rotation=Quaternion.LookRotation(inward);Set(actor,"state","Patrol");
                Assert.That((bool)Call(actor,"HearNoise",end,30f),Is.True);
                bool[] opened=new bool[2];float deadline=Time.realtimeSinceStartup+26;
                while(Vector3.Distance(actor.transform.position,end)>.8f || doors.Any(door=>Get<bool>(door,"IsOpen")||!Get<bool>(door,"AtRequestedDoorPose")))
                {
                    Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),key+" failed two-door room traversal at "+actor.transform.position);
                    for(int i=0;i<doors.Length;i++){opened[i]|=Get<bool>(doors[i],"IsOpen");AdvancedDoorBodyClear(doors[i],actor);}
                    yield return null;
                }
                Assert.That(opened.All(value=>value),Is.True,key+" bypassed one of the two actual doors");actor.gameObject.SetActive(false);
            }
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator SeparateExitDoorStatesRestoreIndependentlyAndSavedVersionFourRetainsOneDoorRooms()
        {
            yield return RevisionPrepare();
            var room=Components("CorridorShelterRoom").First(item=>Get<bool>(item,"HasSeparateExit"));var doors=ShelterDoors(room);
            int cell=Get<int>(room,"Cell");var ids=doors.Select(door=>Get<string>(door,"stableId")).ToArray();
            Assert.That((bool)Call(doors[1],"OpenForPursuer"),Is.True);
            yield return Wait(()=>Get<bool>(doors[1],"AtRequestedDoorPose"),4,"Separate exit did not finish opening");
            Call(shell,"Pause");yield return null;var saved=CheckpointCopy(Call(session,"CaptureCheckpoint"));
            Assert.That(Get<int>(saved,"simulationVersion"),Is.EqualTo(5));
            yield return RestoreCheckpointInFreshScene(saved);
            room=Components("CorridorShelterRoom").Single(item=>Get<int>(item,"Cell")==cell);doors=ShelterDoors(room);
            Assert.That(doors.Select(door=>Get<string>(door,"stableId")),Is.EqualTo(ids));
            Assert.That(Get<bool>(doors[0],"IsOpen"),Is.False);Assert.That(Get<bool>(doors[1],"IsOpen"),Is.True);
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            CreateLegacyCorridorFixture(73,4);yield return null;
            Assert.That(Get<int>(Get<object>(Get<Component>(session,"Corridor"),"Layout"),"Version"),Is.EqualTo(4));
            Assert.That(Components("CorridorShelterRoom"),Has.Length.EqualTo(2));
            Assert.That(Components("CorridorShelterRoom").All(item=>ShelterDoors(item).Length==1&&!Get<bool>(item,"HasSeparateExit")),Is.True);
            IsolateThreats();Begin();yield return null;Call(shell,"Pause");yield return null;
            var legacy=CheckpointCopy(Call(session,"CaptureCheckpoint"));
            yield return RestoreCheckpointInFreshScene(legacy);
            Assert.That(Get<int>(Get<object>(Get<Component>(session,"Corridor"),"Layout"),"Version"),Is.EqualTo(4));
            Assert.That(Components("CorridorShelterRoom").All(item=>ShelterDoors(item).Length==1),Is.True);
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorCreaturesNavigateARealCornerWithoutWalkingInPlaceAtMarkers()
        {
            yield return RevisionPrepare(); ((Behaviour)player).enabled=false; PlacePlayer(new Vector3(200,5,200),false);
            var run=Get<Component>(session,"Corridor"); var bits=Get<int[]>(Get<object>(run,"Layout"),"Connections");
            int cell=Enumerable.Range(1,80).First(i=>new[]{3,6,9,12}.Contains(bits[i]));
            var directions=Enumerable.Range(0,4).Where(d=>(bits[cell]&(1<<d))!=0).ToArray();
            Vector3 Direction(int d)=>new Vector3(new[]{0,1,0,-1}[d],0,new[]{1,0,-1,0}[d]);
            var centre=(Vector3)Call(run,"CellPosition",cell);
            var start=centre+Direction(directions[0])*3.9f; var target=centre+Direction(directions[1])*3.9f;
            foreach(var door in Components("Interactable").Where(item=>Get<object>(item,"kind").ToString()=="Door")) Call(door,"OpenForPursuer");
            yield return Delay(2);
            foreach(string key in new[]{"Cyclopse","Uncat"})
            {
                var actor=ReadabilityActor(key); actor.gameObject.SetActive(true); ((Behaviour)actor).enabled=true;
                var query=actor.transform.Find("Rendered body clearance query").GetComponent<BoxCollider>();
                TestContext.WriteLine(key+" body centre="+query.center+" size="+query.size+" scale="+actor.transform.lossyScale);
                var agent=actor.GetComponent<NavMeshAgent>(); agent.enabled=true;
                Assert.That(NavMesh.SamplePosition(start,out var sample,.7f,NavMesh.AllAreas)&&agent.Warp(sample.position),Is.True);
                actor.transform.rotation=Quaternion.LookRotation(-Direction(directions[0]));
                Set(actor,"state",Enum.Parse(RequireType("StalkerBrain+State"),"Patrol"));
                Assert.That((bool)Call(actor,"HearNoise",target,20f),Is.True);
                var began=actor.transform.position;
                yield return Wait(()=>Vector3.Distance(actor.transform.position,target)<1,18,key+" remained blocked at the corner/candle route",
                    ()=>"position="+actor.transform.position+" target="+target+" state="+Get<object>(actor,"state")+" velocity="+agent.velocity+
                    " remaining="+agent.remainingDistance+" guide="+Get<Vector3>(actor.GetComponent(RequireType("CorridorRouteGuide")),"IntermediateTarget")+
                    " corrections="+Get<int>(actor.GetComponent(RequireType("V1MonsterMotion")),"BodyClearanceCorrections")+
                    " queryCentre="+query.center+" querySize="+query.size);
                Assert.That(Vector3.Distance(actor.transform.position,began),Is.GreaterThan(4)); actor.gameObject.SetActive(false);
            }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator UncatActualWalkingBellsAccelerateAfterRecognitionAndStopDuringPause()
        {
            yield return RevisionPrepare(); ((Behaviour)player).enabled=false;
            PlacePlayer(new Vector3(200,5,200),false);
            var run=Get<Component>(session,"Corridor");
            var centre=(Vector3)Call(run,"CellPosition",0);
            int bits=Get<int[]>(Get<object>(run,"Layout"),"Connections")[0];
            int d=Enumerable.Range(0,4).First(i=>(bits&(1<<i))!=0);
            var forward=new Vector3(new[]{0,1,0,-1}[d],0,new[]{1,0,-1,0}[d]);
            var actor=ReadabilityActor("Uncat"); var agent=actor.GetComponent<NavMeshAgent>();
            var bell=actor.GetComponent(RequireType("UncatBellVoice"));
            Assert.That(bell,Is.Not.Null);
            var target=new GameObject("CloudQA bell patrol endpoint").transform; target.position=centre+forward*6;
            var home=new GameObject("CloudQA bell patrol home").transform; home.position=centre;
            try
            {
                Set(actor,"patrol",new[]{target,home}); Set(actor,"patrolSpeed",1f); Set(actor,"chaseSpeed",1f);
                Set(actor,"state","Patrol"); actor.gameObject.SetActive(true); ((Behaviour)actor).enabled=true;
                agent.enabled=true; Assert.That(agent.Warp(centre),Is.True);
                actor.transform.rotation=Quaternion.LookRotation(forward);
                yield return Delay(1.9f);
                int soft=Get<int>(bell,"RingsPlayed");
                Assert.That(soft,Is.InRange(1,3),"Actual walking must produce sparse bells");
                Assert.That(Get<bool>(bell,"FastRhythm"),Is.False);
                PlacePlayer(centre+forward*6,false); Physics.SyncTransforms();
                yield return Wait(()=>Get<bool>(bell,"FastRhythm"),3,"Visible player did not start Uncat pursuit");
                int fast=Get<int>(bell,"RingsPlayed"); yield return Delay(.95f);
                Assert.That(Get<int>(bell,"RingsPlayed")-fast,Is.GreaterThanOrEqualTo(3),"Actual pursuit bells stayed slow");
                Call(shell,"Pause"); yield return null;
                int paused=Get<int>(bell,"RingsPlayed"); yield return new WaitForSecondsRealtime(.5f);
                Assert.That(Get<int>(bell,"RingsPlayed"),Is.EqualTo(paused));
                Assert.That(Get<AudioSource>(bell,"BellSource").isPlaying,Is.False);
            }
            finally { UnityEngine.Object.Destroy(target.gameObject); UnityEngine.Object.Destroy(home.gameObject); }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator PursuitExtinguishesCandlesAtFiveMetresAndRestoresRememberedLightAfterEscape()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled=false;
            var actor=CandleDangerOwnedBrain(); var candle=CandleDangerMarks()[0];
            Call(candle,"Restore",true); int ignitions=Get<int>(candle,"Ignitions");
            CandleDangerPlaceActor(actor,CandleDangerPoint(player.transform.position,5.5f,null,false));
            Call(LightRun,"RefreshDanger");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False,"A distant calm enemy should not black out the room");
            Set(actor,"state","Chase"); Call(LightRun,"RefreshDanger");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.True,"Recognition blackout retained the old two metre range");
            Assert.That(Get<bool>(candle,"Lit"),Is.False);
            CandleDangerPlaceActor(actor,CandleDangerPoint(player.transform.position,8f,null,false));
            Set(actor,"state","Chase"); Call(LightRun,"RefreshDanger");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False);
            Assert.That(Get<bool>(candle,"Lit"),Is.True);
            Assert.That(Get<int>(candle,"Ignitions"),Is.EqualTo(ignitions));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator GeneratedKoreanPropsHaveImportedPbrMeshesAndLeaveEveryRoomCentreReachable()
        {
            yield return RevisionPrepare();
            var props=Components("KoreanCorridorProp"); Assert.That(props.Length,Is.GreaterThanOrEqualTo(6));
            Assert.That(props.Select(prop=>Get<string>(prop,"Key")).Distinct(),Is.EquivalentTo(new[]{"jangseung","onggi","dokkaebi-tile"}));
            foreach(var prop in props)
            {
                var meshes=prop.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Sum(mesh=>mesh.sharedMesh.vertexCount),Is.GreaterThan(1000));
                Assert.That(prop.GetComponentsInChildren<MeshRenderer>().All(renderer=>renderer.sharedMaterial.mainTexture),Is.True);
            }
            foreach(var room in Components("CorridorShelterRoom"))
                Assert.That(NavMesh.SamplePosition(room.transform.position,out _,.4f,NavMesh.AllAreas),Is.True);
        }
    }
}
