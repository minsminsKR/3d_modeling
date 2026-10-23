using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static readonly Vector3 LoopOrigin=new Vector3(0,0,45);
        IEnumerator BeginObservationSchool()
        {Call(shell,"BeginSchoolLoop");yield return null;yield return null;}
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolLoopMenuLoadsOnlyTheNewCorridorAndSealsAllThreeRooms()
        {
            yield return RecoveryUiReady();yield return RecoveryClick("begin-school");
            yield return Wait(()=>Get<bool>(session,"ChapterMode")&&RecoveryPage=="Playing",30,"School menu failed to enter v3");
            var chapter=Get<Component>(session,"Chapter");var world=Get<Component>(chapter,"Loop");
            Assert.That(Get<int>(chapter,"LayoutVersion"),Is.EqualTo(3));Assert.That(world,Is.Not.Null);
            Assert.That(Get<Array>(chapter,"Memories").Length,Is.Zero);
            Assert.That(Components("StalkerBrain").All(actor=>!actor.gameObject.activeInHierarchy),Is.True,"Retired school monster is still active");
            Assert.That(Components("LanternMaskEncounter").All(actor=>!actor.gameObject.activeInHierarchy),Is.True);
            var doors=Get<IReadOnlyList<Component>>(world,"LockedDoors");Assert.That(doors.Count,Is.EqualTo(4));
            Assert.That(doors.Count(door=>door.name.Contains("study")),Is.EqualTo(2),"Study room must have both locked doors from the reference plan");
            foreach(var door in doors)
            {
                Assert.That(Get<object>(door,"kind").ToString(),Is.EqualTo("Inspect"));
                var collider=door.GetComponent<Collider>();Assert.That(collider.enabled,Is.True);
                Assert.That(collider.bounds.size.y,Is.GreaterThan(2.4f));
            }
            foreach(float z in new[]{8.5f,18.5f,29.5f})
            {
                Assert.That(Physics.Raycast(LoopOrigin+new Vector3(0,1.65f,z),Vector3.left,out var hit,4),Is.True);
                Assert.That(hit.collider.name,Does.Contain("Closed classroom window glass"));
                var path=new NavMeshPath();
                bool route=NavMesh.CalculatePath(LoopOrigin+new Vector3(0,.03f,z),LoopOrigin+new Vector3(-5,.03f,z),NavMesh.AllAreas,path);
                Assert.That(route&&path.status==NavMeshPathStatus.PathComplete,Is.False,"A locked classroom has a navigation entrance");
            }
            var ui=Get<VisualElement>(One("GameShellView"),"Root");
            Assert.That(ui.Q("memory-progress").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolLoopRealWalkingReturnsFromEitherEndToTheSameLeftRoomEntrance()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            PlacePlayer(LoopOrigin+new Vector3(6.6f,.03f,45.85f));player.transform.rotation=Quaternion.identity;
            Keys(Key.W);yield return Delay(.6f);Keys();yield return null;
            var local=player.transform.position-LoopOrigin;
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(1));Assert.That(local.x,Is.EqualTo(-6.6f).Within(.08f));
            Assert.That(local.z,Is.InRange(-8f,-5f));Assert.That(local.y,Is.GreaterThan(-.10f));
            Assert.That(Mathf.DeltaAngle(player.transform.eulerAngles.y,0),Is.EqualTo(0).Within(.01f));
            PlacePlayer(LoopOrigin+new Vector3(-6.6f,.03f,-7.45f));player.transform.rotation=Quaternion.Euler(0,180,0);
            Keys(Key.W);yield return Delay(.6f);Keys();yield return null;
            local=player.transform.position-LoopOrigin;
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(2));Assert.That(local.x,Is.EqualTo(-6.6f).Within(.08f));
            Assert.That(local.z,Is.InRange(-8f,-5f));Assert.That(local.y,Is.GreaterThan(-.10f));
            Assert.That(Mathf.DeltaAngle(player.transform.eulerAngles.y,0),Is.EqualTo(0).Within(.01f));
            foreach(float z in new[]{8.5f,18.5f,29.5f})
            {
                // The route after either departure is entered northbound. All
                // three real classroom fronts are on that traveller's left.
                Assert.That(Physics.Raycast(LoopOrigin+new Vector3(0,1.65f,z),Vector3.left,out var hit,4),Is.True);
                Assert.That(hit.collider.name,Does.Contain("Closed classroom window glass"));
            }
            Call(shell,"Pause");PlacePlayer(LoopOrigin+new Vector3(6.6f,.03f,46.5f));yield return Delay(.1f);
            Assert.That(player.transform.position.z,Is.EqualTo(LoopOrigin.z+46.5f).Within(.01f),"Paused school teleported the player");
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator SchoolLoopF2StillAllowsRealThrowsAndTrapPlacementWithZeroRawStock()
        {
            yield return BeginObservationSchool();PlacePlayer(LoopOrigin+new Vector3(.2f,.03f,16));
            var shots=Get<Component>(player,"Firecrackers");var traps=Get<Component>(player,"Traps");
            Call(shots,"SetRunStock",0);Call(traps,"Restore",0,0f);
            Assert.That(Get<bool>(session,"EncountersResolved"),Is.False,"Observation mode must not masquerade as a completed run");
            yield return RecoveryPulse(Key.F2);
            Assert.That(Get<bool>(Get<Component>(player,"Items"),"Unlimited"),Is.True);
            Assert.That((bool)Call(shots,"TryThrow"),Is.True,"New school blocked the existing unlimited item action");
            Assert.That(Get<int>(shots,"Count"),Is.Zero);
            Assert.That((bool)Call(traps,"TryPlace"),Is.True,"New school has no valid trap world/support");
            Assert.That(Get<int>(traps,"Count"),Is.Zero);Assert.That(Get<Component>(traps,"LastPlaced"),Is.Not.Null);
            yield return RecoveryPulse(Key.F2);
            Assert.That(Get<bool>(Get<Component>(player,"Items"),"Unlimited"),Is.False);
            Assert.That(Get<int>(shots,"Count"),Is.Zero);Assert.That(Get<int>(traps,"Count"),Is.Zero);
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolLoopSuspendRestoresTheNewMapAndRejectsAClassroomPose()
        {
            string directory=NewRecordFixture();Call(session,"ConfigureRecordDirectory",directory);
            yield return BeginObservationSchool();var chapter=Get<Component>(session,"Chapter");var world=Get<Component>(chapter,"Loop");
            Call(world,"RestoreTraversal",3);PlacePlayer(LoopOrigin+new Vector3(-6.6f,.03f,-6));Call(shell,"Pause");
            var snapshot=Call(session,"CaptureChapterCheckpoint");Assert.That(Get<int>(snapshot,"simulationVersion"),Is.EqualTo(3));
            string json=(string)Call(snapshot,"ToJson");Assert.That(json,Does.Contain("\"loop\""));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"ConfigureRecordDirectory",directory);Call(session,"CreateChapterForCheckpoint",snapshot);
            Call(session,"ApplyChapterCheckpoint",snapshot);chapter=Get<Component>(session,"Chapter");world=Get<Component>(chapter,"Loop");
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(3));Assert.That(Vector3.Distance(player.transform.position,LoopOrigin+new Vector3(-6.6f,.03f,-6)),Is.LessThan(.05f));
            var copy=Call(snapshot,"Copy");Set(Get<object>(copy,"player"),"position",LoopOrigin+new Vector3(-6,.03f,29));
            Assert.Throws<ArgumentException>(()=>Call(copy,"Validate"));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolMusicRoomHasRealGeneratedLodsAndIsVisibleThroughTheClosedCorridorWindow()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var music=Get<Transform>(world,"MusicRoom");var piano=music.Find("Music room grand-piano");
            Assert.That(piano,Is.Not.Null);Assert.That(music.GetComponentsInChildren<LODGroup>().Length,Is.EqualTo(6));
            Assert.That(music.GetComponentsInChildren<Transform>().Count(t=>t.name=="Music room classroom-chair"),Is.EqualTo(12));
            foreach(string key in new[]{"grand-piano","music-stand"})
            {
                var source=Resources.Load<GameObject>("SchoolLoop/"+key+"-visual");
                var sourceMeshes=source.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
                var sourceMaps=source.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Select(m=>m.GetTexture("_BaseMap")).ToArray();
                foreach(var prop in music.GetComponentsInChildren<Transform>().Where(t=>t.name=="Music room "+key))
                {
                    CollectionAssert.AreEquivalent(sourceMeshes,prop.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray());
                    Assert.That(prop.GetComponentsInChildren<Collider>(),Is.Empty);
                    foreach(var material in prop.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials))CollectionAssert.Contains(sourceMaps,material.GetTexture("_BaseMap"));
                }
            }
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            PlacePlayer(LoopOrigin+new Vector3(.05f,.03f,29.9f),false);
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(-6.0f,1.35f,30.4f)-camera.transform.position);
            var frame=EntranceRitualFrame(camera,1440,900);
            try
            {
                EntranceBrightnessEvidence("school-loop-music-window",frame);
                Assert.That(EntranceRendererContribution(camera,frame,piano.GetComponentsInChildren<Renderer>()),Is.GreaterThan(1000),"Actual generated piano is not visible through the window");
            }
            finally{Object.Destroy(frame);}
            PlacePlayer(LoopOrigin+new Vector3(.15f,.03f,4.9f),false);camera.transform.rotation=Quaternion.LookRotation(Vector3.forward);
            frame=EntranceRitualFrame(camera,1440,900);try{EntranceBrightnessEvidence("school-loop-long-corridor",frame);}finally{Object.Destroy(frame);}
            var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var neighbours=root.Cast<Transform>().Where(t=>t!=centre).ToArray();
            var roofs=centre.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.name.ToLowerInvariant().Contains("ceiling")).ToArray();
            var clear=camera.clearFlags;var background=camera.backgroundColor;bool ortho=camera.orthographic;float size=camera.orthographicSize;var pose=(camera.transform.position,camera.transform.rotation);
            GameObject mapLight=null;bool fog=RenderSettings.fog;
            try
            {
                foreach(var neighbour in neighbours)neighbour.gameObject.SetActive(false);foreach(var roof in roofs)roof.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.016f,.018f);camera.orthographic=true;camera.orthographicSize=29;
                camera.transform.SetPositionAndRotation(LoopOrigin+new Vector3(-.3f,35,19.2f),Quaternion.Euler(90,0,0));
                // Readable plan-only illumination; production lighting is not edited.
                mapLight=new GameObject("Temporary plan cutaway inspection light");
                var fill=mapLight.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.35f;fill.shadows=LightShadows.None;
                fill.transform.rotation=Quaternion.Euler(65,-25,0);RenderSettings.fog=false;
                frame=EntranceRitualFrame(camera,900,1500);try{EntranceBrightnessEvidence("school-loop-actual-map-cutaway",frame);}finally{Object.Destroy(frame);}
            }
            finally
            {
                if(mapLight)Object.Destroy(mapLight);RenderSettings.fog=fog;
                foreach(var roof in roofs)if(roof)roof.enabled=true;foreach(var neighbour in neighbours)if(neighbour)neighbour.gameObject.SetActive(true);
                camera.clearFlags=clear;camera.backgroundColor=background;camera.orthographic=ortho;camera.orthographicSize=size;camera.transform.SetPositionAndRotation(pose.Item1,pose.Item2);
            }
        }
    }
}
