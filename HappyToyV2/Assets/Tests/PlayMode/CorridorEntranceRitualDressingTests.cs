using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(90000)]
        public IEnumerator EntranceRitualArtKeepsSupportedFloorClearRouteAndReleasesOwnedResources()
        {
            yield return PrepareEntranceCavern();
            var run=Get<Component>(session,"Corridor");var entrance=Get<Component>(run,"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var cavern=Get<Component>(art,"Cavern");var dressing=Get<Component>(art,"Dressing");var vapour=Get<Component>(art,"Vapour");
            Assert.That(Get<int>(art,"RockInstances"),Is.EqualTo(23),"The existing cave collision assembly changed");
            Assert.That(Get<bool>(cavern,"UsesPendingFloorFallback"),Is.False,"The final entrance still substitutes a wall rock for the generated broken floor");
            Assert.That(Get<bool>(dressing,"UsesPendingGuardianFallback"),Is.False,"The generated stone guardians are absent");
            Assert.That(Get<int>(dressing,"CandleCount"),Is.GreaterThanOrEqualTo(24));
            Assert.That(Get<int>(dressing,"SealCount"),Is.GreaterThanOrEqualTo(30));
            Assert.That(Get<int>(dressing,"GuardianCount"),Is.EqualTo(2));
            Assert.That(Get<int>(dressing,"VisualBatches"),Is.InRange(1,64),"Fixed ritual detail is not bounded into spatial material batches");
            Assert.That(dressing.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(dressing.GetComponentsInChildren<NavMeshObstacle>(true),Is.Empty);
            Assert.That(dressing.GetComponentsInChildren(RequireType("Interactable"),true),Is.Empty,"Art created a new pickup or E target");
            var tiles=cavern.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="Wet broken cavern floor tile"||x.name=="Broken stone through the cave passage").ToArray();
            Assert.That(tiles.Length,Is.GreaterThanOrEqualTo(12));
            foreach(var tile in tiles)
            {
                Assert.That(tile.GetComponentsInChildren<Collider>(true),Is.Empty,"Visual relief changed physical support");
                var bounds=tile.GetComponentsInChildren<MeshRenderer>(true).Select(x=>x.bounds).ToArray();
                Assert.That(bounds,Is.Not.Empty);
                Assert.That(bounds.Max(x=>x.max.y),Is.LessThanOrEqualTo(.026f),"The visual floor floats above the player's actual support");
                Assert.That(bounds.Max(x=>x.max.y)-bounds.Min(x=>x.min.y),Is.LessThanOrEqualTo(.141f),"Runtime placement stretched the capped generated floor relief");
                Assert.That(tile.GetComponentInChildren<LODGroup>(true),Is.Not.Null,"Generated floor lost its authored LODs");
            }
            var start=Get<Vector3>(entrance,"StartPoint");var approach=Get<Vector3>(entrance,"MazeApproach");
            AssertEntrancePath(start,approach);
            foreach(var delta in new[]{Vector3.zero,Vector3.left*1.8f,Vector3.right*1.8f,Vector3.back*6,Vector3.forward*10})
            {
                var feet=entrance.transform.position+Vector3.up*.03f+delta;
                Assert.That(Physics.Raycast(feet+Vector3.up*.4f,Vector3.down,out var support,1,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(support.point.y,Is.EqualTo(0).Within(.03f));
            }
            var sourceMaterials=new Dictionary<Material,(Texture,Texture)>();
            foreach(string key in new[]{"temple-gate","cave-rock","ritual-floor","guardian-statue"})
            {
                var prefab=Resources.Load<GameObject>("CorridorEntrance/"+key+"-visual");Assert.That(prefab,Is.Not.Null);
                foreach(var material in prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials).Distinct())
                {
                    var maps=(material.GetTexture("_BaseMap"),material.GetTexture("_MetallicGlossMap"));
                    Assert.That(maps.Item1&&maps.Item2,Is.True,"Generated model's external PBR maps are missing: "+key);
                    sourceMaterials[material]=maps;
                }
            }
            var ownedMeshes=art.transform.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh)
                .Where(x=>x&&(x.name.StartsWith("Owned ")||x.name.StartsWith("Graphics metre box")))
                .Concat(vapour.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh)).Where(x=>x).Distinct().ToArray();
            var ownMaterials=dressing.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials)
                .Where(x=>x&&(x.name.StartsWith("Owned ")||x.name.StartsWith("PBR ")))
                .Concat(vapour.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials)).Distinct().ToArray();
            var wetTextures=ownMaterials.Where(x=>x.name=="Owned shallow cave water sheen").Select(x=>x.GetTexture("_MetallicGlossMap")).ToArray();
            Assert.That(ownedMeshes.Length,Is.GreaterThan(8));Assert.That(ownMaterials.Length,Is.GreaterThan(5));
            int physicsCount=entrance.GetComponentsInChildren<Collider>(true).Length;
            Object.Destroy(art);yield return null;yield return null;yield return null;
            Assert.That(!dressing&&!cavern&&!vapour,Is.True,"Removing the assembly leaks collaborators");
            Assert.That(ownedMeshes.All(x=>!x)&&ownMaterials.All(x=>!x)&&wetTextures.All(x=>!x),Is.True,"Runtime art meshes/materials/maps survived their owner");
            Assert.That(entrance.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(physicsCount),"Art teardown changed chamber-owned physics");
            foreach(var pair in sourceMaterials)
            {Assert.That(pair.Key,Is.Not.Null);Assert.That(pair.Key.GetTexture("_BaseMap"),Is.SameAs(pair.Value.Item1));Assert.That(pair.Key.GetTexture("_MetallicGlossMap"),Is.SameAs(pair.Value.Item2));}
            Debug.Log("HAPPYTOY_ENTRANCE_RITUAL_OWNERSHIP_PASS real generated floor/guardians, visual relief/support, clear NavMesh, bounded batches, shared PBR retention and owned teardown");
        }

        [Serializable] sealed class EntranceRitualView
        {
            public string image;public int width,height,changedArtPixels,changedFloorPixels,changedSealsPixels,changedInkPixels,changedCandlesPixels,changedGuardianPixels,exposedBackgroundPixels;
            public float fieldOfView;public Vector3 feet,cameraPosition;public Quaternion cameraRotation;
        }
        [Serializable] sealed class EntranceRitualCameraReport
        {
            public string scope="Actual production start position/orientation and user camera FOV with URP; generated assets, local warm lights and original grading. Threats isolated for art review; camera projection uses 16:9/4:3/21:9 offscreen targets, not native window performance. Renderer-off probes prove visible contributions, not subjective realism.";
            public EntranceRitualView[] views;
        }
        static Texture2D EntranceRitualFrame(Camera camera,int width,int height)
        {
            var target=new RenderTexture(width,height,24);target.Create();var previous=camera.targetTexture;float aspect=camera.aspect;
            try
            {
                camera.targetTexture=target;camera.aspect=width/(float)height;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                return CloudExperienceTests.Read(target);
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;target.Release();Object.Destroy(target);}
        }
        static int EntranceRendererContribution(Camera camera,Texture2D original,IEnumerable<Renderer> renderers)
        {
            var states=renderers.Where(x=>x).Distinct().ToDictionary(x=>x,x=>x.enabled);Texture2D absent=null;
            try
            {
                foreach(var pair in states)pair.Key.enabled=false;
                absent=EntranceRitualFrame(camera,original.width,original.height);return HauntedChanged(original,absent);
            }
            finally{foreach(var pair in states)if(pair.Key)pair.Key.enabled=pair.Value;if(absent)Object.Destroy(absent);}
        }
        static int EntranceBackgroundExposure(Texture2D original,Texture2D probe)
        {
            // Detect the diagnostic magenta itself. Ordinary frame differences
            // also count animated orange candle flames and are not sky evidence.
            var before=original.GetPixels32();var after=probe.GetPixels32();int count=0;
            for(int i=0;i<after.Length;i++)
            {
                var a=after[i];var b=before[i];
                if(a.r>76&&a.b>76&&a.r>a.g*2+38&&a.b>a.g*2+38&&
                    Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>50)count++;
            }
            return count;
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator EntranceFloorStaysDarkWhenBackingAcrossTheCorridorMouth()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            bool oldPlayer=((Behaviour)player).enabled,oldTorch=torch.enabled;
            var means=new List<float>();
            try
            {
                ((Behaviour)player).enabled=false;torch.enabled=false;
                foreach(float z in new[]{8f,12f,14.6f,15.2f,17.6f,20f})
                {
                    PlacePlayer(entrance.transform.TransformPoint(new Vector3(0,.03f,z)),false);
                    player.transform.rotation=Quaternion.Euler(0,180,0);camera.transform.localRotation=Quaternion.identity;
                    yield return Delay(3.7f);
                    var frame=EntranceRitualFrame(camera,960,540);
                    try
                    {
                        CloudExperienceTests.Artifact("entrance-floor-boundary-"+z.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture).Replace('.','p')+".png",frame.EncodeToPNG());
                        float sum=0;int count=0;
                        for(int y=54;y<243;y++)for(int x=144;x<816;x++)
                        {var p=frame.GetPixel(x,y);sum+=p.grayscale;count++;}
                        means.Add(sum/count);
                        Debug.Log("ENTRANCE_FLOOR_BOUNDARY z="+z+" mean="+means.Last()+" ready="+Get<bool>(graphics,"ReflectionReady")+" probe="+Get<ReflectionProbe>(graphics,"OwnedProbe").transform.position);
                    }
                    finally{Object.Destroy(frame);}
                }
                Assert.That(means.Max(),Is.LessThan(.18f),"The dark wet cave floor picks up bright outdoor reflections near the corridor boundary");
                Assert.That(means.Max()-means.Min(),Is.LessThan(.09f),"Backing across the portal abruptly changes the whole floor brightness");
                var room=Get<ReflectionProbe>(graphics,"EntranceProbe");Assert.That(room,Is.Not.Null);
                Assert.That(room.IsFinishedRendering(Get<int>(graphics,"EntranceReflectionRenderId")),Is.True);
                Assert.That(room.bounds.Contains(entrance.transform.position+new Vector3(0,.025f,18.9f)),Is.True,"Persistent reflections miss the connecting passage floor");
            }
            finally{torch.enabled=oldTorch;((Behaviour)player).enabled=oldPlayer;}
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator EntranceRitualActualCameraShowsWetFloorSealsCandlesAndGuardiansAcrossAspectRatios()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var dressing=Get<Component>(art,"Dressing");var cavern=Get<Component>(art,"Cavern");var camera=Get<Camera>(player,"eyes");
            var torch=Get<Light>(player,"flashlight");bool oldTorch=torch.enabled;var oldRotation=camera.transform.rotation;
            bool oldPlayer=((Behaviour)player).enabled;((Behaviour)player).enabled=false;
            var details=dressing.GetComponentsInChildren<Renderer>(true).Where(x=>x.enabled).ToArray();
            var floor=cavern.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="Wet broken cavern floor tile")
                .SelectMany(x=>x.GetComponentsInChildren<Renderer>(true)).Where(x=>x.enabled).ToArray();
            var seals=details.Where(x=>x.sharedMaterials.Any(m=>m&&m.name=="Owned aged talisman paper")).ToArray();
            var ink=details.Where(x=>x.sharedMaterials.Any(m=>m&&m.name=="Owned curved talisman ink")).ToArray();
            var candles=details.Where(x=>x.sharedMaterials.Any(m=>m&&(m.name.Contains("wax-tallow")||m.name=="Owned photographic candle flame"))).ToArray();
            var guardians=details.Where(x=>x.sharedMaterials.Any(m=>m&&m.name.IndexOf("jangseung",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            Assert.That(seals,Is.Not.Empty);Assert.That(ink,Is.Not.Empty);Assert.That(candles,Is.Not.Empty);Assert.That(guardians,Is.Not.Empty);
            var views=new List<EntranceRitualView>();
            try
            {
                torch.enabled=false;
                // Keep the genuine start composition rather than centring the gate.
                yield return Delay(.35f);
                var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
                var roomProbe=Get<ReflectionProbe>(graphics,"EntranceProbe");
                // Time-sliced cubemaps must finish before renderer/background
                // comparisons, otherwise their initial sky-to-room transition
                // is incorrectly counted as a visible geometry gap.
                yield return Wait(()=>Get<bool>(graphics,"ReflectionReady")&&roomProbe.IsFinishedRendering(Get<int>(graphics,"EntranceReflectionRenderId")),12,"Entrance reflection captures did not finish before the camera comparison");
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(960,720),new Vector2Int(1680,720)})
                {
                    var frame=EntranceRitualFrame(camera,size.x,size.y);
                    try
                    {
                        string image="entrance-ritual-"+size.x+"x"+size.y+"-ambient.png";
                        CloudExperienceTests.Artifact(image,frame.EncodeToPNG());Assert.That(CloudExperienceTests.HasContent(frame),Is.True);
                        var view=new EntranceRitualView {image=image,width=size.x,height=size.y,
                            fieldOfView=camera.fieldOfView,feet=player.transform.position,cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,
                            changedArtPixels=EntranceRendererContribution(camera,frame,details),changedFloorPixels=EntranceRendererContribution(camera,frame,floor),
                            changedSealsPixels=EntranceRendererContribution(camera,frame,seals),changedInkPixels=EntranceRendererContribution(camera,frame,ink),changedCandlesPixels=EntranceRendererContribution(camera,frame,candles),
                            changedGuardianPixels=EntranceRendererContribution(camera,frame,guardians)};
                        var clear=camera.clearFlags;var background=camera.backgroundColor;Texture2D skyProbe=null;
                        try
                        {
                            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;
                            skyProbe=EntranceRitualFrame(camera,size.x,size.y);
                            view.exposedBackgroundPixels=EntranceBackgroundExposure(frame,skyProbe);
                            if(view.exposedBackgroundPixels>=30)CloudExperienceTests.Artifact("entrance-onward-background-probe.png",skyProbe.EncodeToPNG());
                            Assert.That(view.exposedBackgroundPixels,Is.LessThan(30),"The onward view sees outdoor sky through the cave-to-maze ceiling joint");
                        }
                        finally{camera.clearFlags=clear;camera.backgroundColor=background;if(skyProbe)Object.Destroy(skyProbe);}
                        Assert.That(view.changedArtPixels,Is.GreaterThan(500),"Ritual detail is absent from the actual start view");
                        Assert.That(view.changedFloorPixels,Is.GreaterThan(2000),"Broken floor is absent or unlit in the real ambient view");
                        Assert.That(view.changedSealsPixels,Is.GreaterThan(25),"Suspended seals never reach the actual raster");
                        Assert.That(view.changedInkPixels,Is.GreaterThan(8),"Hanging seals are blank in the actual camera view");
                        Assert.That(view.changedCandlesPixels,Is.GreaterThan(20),"Candle gatherings are invisible");
                        Assert.That(view.changedGuardianPixels,Is.GreaterThan(30),"Generated passage guardians are hidden or unlit");views.Add(view);
                    }
                    finally{Object.Destroy(frame);}
                }
                CloudExperienceTests.Artifact("entrance-ritual-camera.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(new EntranceRitualCameraReport {views=views.ToArray()},true)));
            }
            finally{torch.enabled=oldTorch;camera.transform.rotation=oldRotation;((Behaviour)player).enabled=oldPlayer;}
            Debug.Log("HAPPYTOY_ENTRANCE_RITUAL_CAMERA_PASS actual ambient 16:9/4:3/21:9 projections and independent floor/seal/candle/guardian raster contributions");
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator EntranceReflectionUsesActualCavernBoxAndPauseRetainsTheOwnedScope()
        {
            yield return PrepareEntranceCavern();
            // Existing corridor atmosphere already owns its global fog scope.
            var oldGlobal=(RenderSettings.fog,RenderSettings.fogColor,RenderSettings.fogDensity);
            var run=Get<Component>(session,"Corridor");var entrance=Get<Component>(run,"Entrance");
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));var probe=Get<ReflectionProbe>(graphics,"OwnedProbe");
            var roomProbe=Get<ReflectionProbe>(graphics,"EntranceProbe");Assert.That(roomProbe,Is.Not.Null);
            var roomPosition=roomProbe.transform.position;int roomCapture=Get<int>(graphics,"EntranceReflectionRenderId");
            var vapour=Get<Component>(entrance.GetComponent(RequireType("CorridorEntrancePresentation")),"Vapour");
            yield return Wait(()=>Vector3.Distance(probe.transform.position,entrance.transform.position+Vector3.up*2)<.01f,12,"Cave spawn chose the nearest maze-cell reflection instead of the actual room");
            Assert.That(probe.size,Is.EqualTo(new Vector3(28,12,30)));Assert.That(probe.center,Is.EqualTo(Vector3.zero));
            Assert.That(probe.boxProjection,Is.True);Assert.That(probe.resolution,Is.EqualTo(128));
            Assert.That(probe.refreshMode,Is.EqualTo(ReflectionProbeRefreshMode.ViaScripting));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                Assert.That(probe.bounds.Contains(entrance.transform.position+new Vector3(x*13.9f,.025f,z*14.9f)),Is.True,"The real wet floor extends beyond its reflection box");
            var budget=session.GetComponent(RequireType("LocalShadowBudget"));Call(budget,"RefreshNow");AssertAtlas(budget);
            var lights=Get<IReadOnlyList<Light>>(entrance.GetComponent(RequireType("CorridorEntrancePresentation")),"Lanterns");
            Assert.That(lights.Count,Is.LessThanOrEqualTo(9));Assert.That(lights.Count(x=>x.shadows!=LightShadows.None),Is.LessThanOrEqualTo(2));
            Assert.That(lights.All(x=>x.type==LightType.Point&&x.range<=25),Is.True);
            Assert.That(Get<int>(vapour,"PlumeCount"),Is.EqualTo(2));
            Assert.That(vapour.GetComponent<MeshFilter>(),Is.Null,"The removed grazing fog sheets survived on the local owner");
            Assert.That(vapour.GetComponentsInChildren(RequireType("IncenseSmokeTrail"),true).Length,Is.EqualTo(2));
            Assert.That(vapour.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(2),"Only the two bounded incense plumes may own smoke geometry");
            yield return Wait(()=>Get<float>(vapour,"SimulationTime")>.1f,3,"Local incense failed to advance");
            Call(shell,"Pause");float time=Get<float>(vapour,"SimulationTime");var position=probe.transform.position;int requests=Get<int>(graphics,"ReflectionCaptures");
            PlacePlayer((Vector3)Call(run,"CellPosition",0),false);yield return new WaitForSecondsRealtime(3.3f);
            Assert.That(Get<float>(vapour,"SimulationTime"),Is.EqualTo(time));Assert.That(probe.transform.position,Is.EqualTo(position));
            Assert.That(Get<int>(graphics,"ReflectionCaptures"),Is.EqualTo(requests),"Pause issued new reflection work");
            Call(shell,"Resume");yield return Wait(()=>Vector3.Distance(probe.transform.position,(Vector3)Call(run,"CellPosition",0)+Vector3.up*1.5f)<.01f,12,"Leaving the cave lost original maze reflection planning");
            Assert.That(probe.size,Is.EqualTo(new Vector3(5.5f,3,5.5f)));
            Assert.That(roomProbe.transform.position,Is.EqualTo(roomPosition));
            Assert.That(Get<int>(graphics,"EntranceReflectionRenderId"),Is.EqualTo(roomCapture),"Leaving the cave recaptured its fixed lantern reflection");
            Assert.That((RenderSettings.fog,RenderSettings.fogColor,RenderSettings.fogDensity),Is.EqualTo(oldGlobal),"Local atmosphere changed global fog");
            Call(RequireType("GraphicsLightingPresentation"),"EndMode",session);yield return null;yield return null;
            Assert.That(!probe&&!roomProbe,Is.True,"Reflection scope did not release both owned probes");
            Debug.Log("HAPPYTOY_ENTRANCE_REFLECTION_PASS actual cave floor box, unchanged bounded capture schedule, local lights, paused bounded smoke/probe and original maze plan/owned teardown");
        }
    }
}
