using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class HauntedView
        {
            public string image;
            public Vector3 feet,camera,target;
            public bool flashlight;
        }
        [Serializable] sealed class HauntedCameraReport
        {
            public string scope="Controlled art views of the actual seeded runtime corridor using the production player camera. Doors are opened and threats isolated for review; the 14m visibility view repositions one unchanged authored monster with its behaviour disabled. Separate seed73/211 input runs certify survival.";
            public int seed,wallFaces,layeredPassages,shrines,lanterns,visualBatches,physicalColliders,obstacles,geometryChangedPixels,clueChangedPixels,visibleGuardPixels;
            public float fogDensity,contrastRetentionAt18m,guardDistance;
            public HauntedView[] views;
        }
        static Component HauntedRun(Component session)=>Get<Component>(session,"Corridor");
        static Vector3 HauntedCell(Component run,int cell)=>(Vector3)Call(run,"CellPosition",cell);
        static ulong HauntedNavigationHash()
        {
            var mesh=NavMesh.CalculateTriangulation();ulong hash=14695981039346656037UL;
            foreach(var vertex in mesh.vertices) foreach(float value in new[]{vertex.x,vertex.y,vertex.z})
                foreach(byte octet in BitConverter.GetBytes(value)) hash=unchecked((hash^octet)*1099511628211UL);
            foreach(int value in mesh.indices.Concat(mesh.areas)) foreach(byte octet in BitConverter.GetBytes(value))
                hash=unchecked((hash^octet)*1099511628211UL);
            return hash;
        }
        static void HauntedCompleteApproaches(Component run)
        {
            Vector3 entrance=HauntedCell(run,0);
            Assert.That(NavMesh.SamplePosition(entrance,out var start,.7f,NavMesh.AllAreas),Is.True);
            foreach(var item in run.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>()
                .Where(item=>Get<object>(item,"kind").ToString()=="CorridorMemory"))
            {
                Vector3 at=item.transform.position+Vector3.back;at.y=.03f;
                Assert.That(NavMesh.SamplePosition(at,out var end,.65f,NavMesh.AllAreas),Is.True,"Lost original altar approach "+item.name);
                var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Dressing disconnected "+item.name);
            }
        }
        [UnityTest,Timeout(90000)]
        public IEnumerator HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            var run=HauntedRun(session);var art=Get<Component>(run,"Presentation");
            Assert.That(Get<bool>(run,"Ready") && Get<bool>(art,"Prepared"),Is.True);
            Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True,"Presentation changed an original collider or obstacle during Build");
            var legacyFloor=Components("FloorAtmosphere").Single(component=>component.gameObject.scene==session.gameObject.scene);
            var legacyEmitter=legacyFloor.transform.Find("Basement distant water drops");
            Assert.That(legacyEmitter,Is.Not.Null,"Actual school atmosphere water emitter is missing");
            var legacyWater=legacyEmitter.GetComponent<AudioSource>();
            Assert.That(legacyWater && !legacyWater.isPlaying && legacyWater.volume==0,Is.True,"Legacy school water still runs in corridor mode");
            Color corridorFog=RenderSettings.fogColor;float corridorDensity=RenderSettings.fogDensity;
            Assert.That(corridorDensity,Is.EqualTo(.018f).Within(.00001f));
            Object.Destroy(legacyFloor);yield return null;yield return null;
            Assert.That(legacyWater==null,Is.True,"Removing floor atmosphere leaked its owned water emitter");
            Assert.That(RenderSettings.fogColor,Is.EqualTo(corridorFog));
            Assert.That(RenderSettings.fogDensity,Is.EqualTo(corridorDensity),"Legacy floor teardown overwrote live corridor atmosphere");
            Assert.That(Get<int>(art,"CladWallFaces"),Is.GreaterThan(200));
            Assert.That(Get<int>(art,"LayeredPassages"),Is.GreaterThanOrEqualTo(80));
            Assert.That(Get<int>(art,"SealShrines"),Is.EqualTo(5));
            var root=Get<Transform>(art,"AdditionRoot");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<NavMeshObstacle>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).Any(component=>component.GetType().Name=="NavMeshSurface"),Is.False);
            var items=run.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>().ToArray();
            var itemStates=items.ToDictionary(item=>item,item=>new {position=item.transform.position,rotation=item.transform.rotation,
                scale=item.transform.lossyScale,id=Get<string>(item,"stableId"),kind=Get<object>(item,"kind").ToString()});
            Assert.That(items.Count(item=>Get<object>(item,"kind").ToString()=="CorridorMemory"),Is.EqualTo(5));
            Assert.That(items.Count(item=>Get<object>(item,"kind").ToString()=="FirecrackerSupply"),Is.EqualTo(8));
            Assert.That(Get<int>(art,"TimberCabinets"),Is.EqualTo(items.Count(item=>Get<object>(item,"kind").ToString()=="HidingPlace")),
                "Some live hiding cabinets retained school enamel");
            var cabinetMeshes=items.Where(item=>Get<object>(item,"kind").ToString()=="HidingPlace")
                .SelectMany(item=>item.GetComponentsInChildren<MeshFilter>(true)).ToDictionary(filter=>filter,filter=>filter.sharedMesh);
            var cabinetSkins=items.Where(item=>Get<object>(item,"kind").ToString()=="HidingPlace")
                .SelectMany(item=>item.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer=>renderer.sharedMaterials.Any(material=>material && material.name.StartsWith("Corridor cabinet "))).ToArray();
            Assert.That(cabinetSkins,Is.Not.Empty);
            var altarRenderers=art.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.name=="Memory altar").ToArray();
            Assert.That(altarRenderers.Length,Is.EqualTo(5));
            Assert.That(altarRenderers.All(renderer=>!renderer.enabled),Is.True,"Original opaque altar still conceals its new timber skin");
            var physical=art.GetComponentsInChildren<Collider>(true).Where(collider=>!collider.GetComponentInParent(RequireType("StalkerBrain")))
                .ToDictionary(collider=>collider,collider=>new {collider.enabled,collider.bounds,position=collider.transform.position,
                    rotation=collider.transform.rotation,scale=collider.transform.lossyScale,layer=collider.gameObject.layer});
            Assert.That(physical.Count,Is.EqualTo(Get<int>(art,"PreservedColliders")),"New decoration introduced a physical collider");
            var obstacles=art.GetComponentsInChildren<NavMeshObstacle>(true).ToDictionary(obstacle=>obstacle,
                obstacle=>new {obstacle.enabled,obstacle.carving,obstacle.center,obstacle.size,obstacle.shape});
            Assert.That(obstacles.Count,Is.EqualTo(Get<int>(art,"PreservedObstacles")));
            var memoryRenderers=items.Where(item=>Get<object>(item,"kind").ToString()=="CorridorMemory")
                .Select(item=>item.GetComponent<MeshRenderer>()).ToArray();
            Assert.That(memoryRenderers.All(renderer=>!renderer.enabled),Is.True,"Old brass cubes still mask the folded seals");
            var folded=items.Where(item=>Get<object>(item,"kind").ToString()=="CorridorMemory")
                .Select(item=>item.transform.Find("Folded original memory seal")).ToArray();
            Assert.That(folded.All(child=>child && child.GetComponentsInChildren<Collider>(true).Length==0),Is.True);
            foreach(var door in items.Where(item=>Get<object>(item,"kind").ToString()=="Door"))
            {
                var leaf=Get<Transform>(door,"movingLeaf");var collision=leaf.GetComponent<BoxCollider>();
                foreach(var renderer in leaf.GetComponentsInChildren<MeshRenderer>().Where(renderer=>renderer.name.StartsWith("Moving ")))
                {
                    Assert.That(renderer.bounds.size.x,Is.LessThanOrEqualTo(collision.bounds.size.x+.005f));
                    Assert.That(renderer.bounds.size.z,Is.LessThanOrEqualTo(collision.bounds.size.z+.005f),
                        "Door dressing inherited cube scale instead of preserving metre-authored dimensions");
                }
            }
            HauntedCompleteApproaches(run);ulong navigation=HauntedNavigationHash();
            var meshes=root.GetComponentsInChildren<MeshFilter>().Select(filter=>filter.sharedMesh).Distinct().ToArray();
            var materials=root.GetComponentsInChildren<MeshRenderer>().Select(renderer=>renderer.sharedMaterial).Distinct().ToArray();
            var texture=Resources.Load<Texture2D>("Corridor/aged-floor-v1");var font=Resources.Load<Font>("Fonts/Korean");
            Assert.That(materials.Any(material=>material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap")==texture),Is.True);
            var lanterns=((IEnumerable<Light>)Get<object>(art,"Lanterns")).ToArray();
            Assert.That(lanterns.Length,Is.GreaterThanOrEqualTo(21));
            foreach(var lantern in lanterns) Assert.That(lantern.name,Is.EqualTo("Corridor lamp"));
            Object.Destroy(art);yield return null;yield return null;yield return null;
            Assert.That(root==null,Is.True);Assert.That(folded.All(child=>child==null),Is.True,"Collectible-child art leaked outside dressing root");
            Assert.That(meshes.All(mesh=>mesh==null),Is.True,"Owned room batch mesh leaked");
            Assert.That(materials.Where(material=>material && material.name!="World sign font").Any(),Is.False,"Owned art material leaked");
            Assert.That(texture && font,Is.True,"Removing corridor art destroyed an imported shared asset");
            Assert.That(memoryRenderers.All(renderer=>renderer.enabled),Is.True,"Removing art did not restore original memory renderers");
            Assert.That(altarRenderers.All(renderer=>renderer.enabled),Is.True,"Removing art did not restore original altar renderers");
            foreach(var pair in cabinetMeshes)Assert.That(pair.Key.sharedMesh,Is.SameAs(pair.Value),"Timber skin changed cabinet mesh identity");
            foreach(var renderer in cabinetSkins)Assert.That(renderer.sharedMaterials.Any(material=>material && material.name.StartsWith("Corridor cabinet ")),Is.False,
                "Removing dressing did not restore original cabinet material slots");
            foreach(var lantern in lanterns)
            {
                Assert.That(lantern.intensity,Is.EqualTo(3.4f));Assert.That(lantern.range,Is.EqualTo(7));
                Assert.That(lantern.color,Is.EqualTo(new Color(.95f,.68f,.36f)));
            }
            foreach(var pair in physical)
            {
                Assert.That(pair.Key && pair.Key.enabled==pair.Value.enabled,Is.True);Assert.That(pair.Key.bounds,Is.EqualTo(pair.Value.bounds));
                Assert.That(pair.Key.transform.position,Is.EqualTo(pair.Value.position));Assert.That(pair.Key.transform.rotation,Is.EqualTo(pair.Value.rotation));
                Assert.That(pair.Key.transform.lossyScale,Is.EqualTo(pair.Value.scale));Assert.That(pair.Key.gameObject.layer,Is.EqualTo(pair.Value.layer));
            }
            foreach(var pair in obstacles)
            {
                Assert.That(pair.Key && pair.Key.enabled==pair.Value.enabled,Is.True);Assert.That(pair.Key.carving,Is.EqualTo(pair.Value.carving));
                Assert.That(pair.Key.center,Is.EqualTo(pair.Value.center));Assert.That(pair.Key.size,Is.EqualTo(pair.Value.size));Assert.That(pair.Key.shape,Is.EqualTo(pair.Value.shape));
            }
            foreach(var pair in itemStates)
            {
                Assert.That(pair.Key.transform.position,Is.EqualTo(pair.Value.position));Assert.That(pair.Key.transform.rotation,Is.EqualTo(pair.Value.rotation));
                Assert.That(pair.Key.transform.lossyScale,Is.EqualTo(pair.Value.scale));Assert.That(Get<string>(pair.Key,"stableId"),Is.EqualTo(pair.Value.id));
                Assert.That(Get<object>(pair.Key,"kind").ToString(),Is.EqualTo(pair.Value.kind));
            }
            Assert.That(HauntedNavigationHash(),Is.EqualTo(navigation),"Presentation removal changed actual baked navigation");
            HauntedCompleteApproaches(run);
            Debug.Log("HAPPYTOY_HAUNTED_PRESENTATION_PASS original collider bounds/layers/doors/items/stableIDs and navigation preserved; metre-sized moving door skins; folded collectible-child art and owned meshes/materials released; shared textures/font retained");
        }

        static int HauntedChanged(Texture2D before,Texture2D after,Rect? region=null)
        {
            var a=before.GetPixels32();var b=after.GetPixels32();int count=0;
            int x0=region.HasValue?Mathf.Max(0,Mathf.FloorToInt(region.Value.xMin*before.width)-2):0;
            int x1=region.HasValue?Mathf.Min(before.width-1,Mathf.CeilToInt(region.Value.xMax*before.width)+2):before.width-1;
            int y0=region.HasValue?Mathf.Max(0,Mathf.FloorToInt(region.Value.yMin*before.height)-2):0;
            int y1=region.HasValue?Mathf.Min(before.height-1,Mathf.CeilToInt(region.Value.yMax*before.height)+2):before.height-1;
            for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
            {int pixel=y*before.width+x;if(Mathf.Abs(a[pixel].r-b[pixel].r)+Mathf.Abs(a[pixel].g-b[pixel].g)+Mathf.Abs(a[pixel].b-b[pixel].b)>24)count++;}
            return count;
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator HauntedCorridorRealCameraShowsTimberPaperLanternsSealsAndReadableExitWithFourteenMetreSight()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            var run=HauntedRun(session);var art=Get<Component>(run,"Presentation");
            Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True);
            HauntedCompleteApproaches(run);
            var camera=Get<Camera>(player,"eyes");var flashlight=Get<Light>(player,"flashlight");
            if(flashlight.enabled) {yield return KeysObserved(Key.F);Keys();yield return null;}
            Assert.That(flashlight.enabled,Is.False);
            var report=new HauntedCameraReport {seed=73,wallFaces=Get<int>(art,"CladWallFaces"),layeredPassages=Get<int>(art,"LayeredPassages"),
                shrines=Get<int>(art,"SealShrines"),lanterns=((IEnumerable<Light>)Get<object>(art,"Lanterns")).Count(),
                visualBatches=Get<int>(art,"VisualBatches"),physicalColliders=Get<int>(art,"PreservedColliders"),obstacles=Get<int>(art,"PreservedObstacles"),
                fogDensity=RenderSettings.fogDensity,contrastRetentionAt18m=Mathf.Exp(-Mathf.Pow(RenderSettings.fogDensity*18,2))};
            Assert.That(RenderSettings.fog && RenderSettings.fogMode==FogMode.ExponentialSquared,Is.True);
            Assert.That(report.fogDensity,Is.InRange(.014f,.020f));Assert.That(report.contrastRetentionAt18m,Is.GreaterThan(.87f));
            var views=new List<HauntedView>();Vector3 entrance=HauntedCell(run,0);
            ((Behaviour)player).enabled=false;
            PlacePlayer(entrance,false);camera.transform.rotation=Quaternion.LookRotation(Vector3.forward);
            yield return Delay(.3f);
            var first=SchoolCameraFrame(camera,out _,out _,out _);
            try
            {
                const string name="haunted-corridor-entrance-natural.png";CloudExperienceTests.Artifact(name,first.EncodeToPNG());
                views.Add(new HauntedView {image=name,feet=player.transform.position,camera=camera.transform.position,target=camera.transform.position+camera.transform.forward*6,flashlight=false});
                var renderers=Get<Transform>(art,"AdditionRoot").GetComponentsInChildren<Renderer>();
                var enabled=renderers.ToDictionary(renderer=>renderer,renderer=>renderer.enabled);
                foreach(var renderer in renderers) renderer.enabled=false;
                var absent=SchoolCameraFrame(camera,out _,out _,out _);
                foreach(var pair in enabled) pair.Key.enabled=pair.Value;
                try {report.geometryChangedPixels=HauntedChanged(first,absent);Assert.That(report.geometryChangedPixels,Is.GreaterThan(1500),"New corridor geometry does not reach the real entrance raster");}
                finally {Object.Destroy(absent);}
            }
            finally {Object.Destroy(first);}
            ((Behaviour)player).enabled=true;player.GetComponent<CharacterController>().enabled=true;
            yield return null;yield return KeysObserved(Key.F);Keys();yield return null;
            Assert.That(flashlight.isActiveAndEnabled,Is.True,"Real F input did not light the production-camera art review");
            ((Behaviour)player).enabled=false;
            foreach(var door in run.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>()
                .Where(item=>Get<object>(item,"kind").ToString()=="Door")) Call(door,"OpenForPursuer");
            yield return Delay(1.3f);
            var layout=Get<object>(run,"Layout");var connections=Get<int[]>(layout,"Connections");
            int longest=0,startCell=0,direction=0;
            for(int cell=0;cell<81;cell++) for(int d=0;d<4;d++)
            {
                int current=cell,length=0;
                while((connections[current]&(1<<d))!=0)
                {
                    int x=current%9+(d==1?1:d==3?-1:0),z=current/9+(d==0?1:d==2?-1:0);
                    if(x<0 || x>=9 || z<0 || z>=9)break;
                    current=z*9+x;length++;
                }
                if(length>longest){longest=length;startCell=cell;direction=d;}
            }
            Assert.That(longest,Is.GreaterThanOrEqualTo(2),"Seed fixture needs a genuine 14m open sightline");
            Vector3 forward=direction==0?Vector3.forward:direction==1?Vector3.right:direction==2?Vector3.back:Vector3.left;
            Vector3 hallFeet=HauntedCell(run,startCell)-forward*2;
            var memory=run.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>()
                .First(item=>Get<string>(item,"stableId")=="memory-0");
            Vector3 shrine=memory.transform.position;shrine.y=.03f;
            var positions=new[]{entrance+Vector3.back*1.4f,hallFeet,shrine+Vector3.back*2,entrance+Vector3.left*.9f};
            var targets=new[]{entrance+new Vector3(0,2.0f,2.7f),hallFeet+forward*16+Vector3.up*1.6f,shrine+Vector3.up*1.4f,
                Get<TextMesh>(art,"ReturnClue").transform.position};
            var names=new[]{"haunted-corridor-timber-paper-close.png","haunted-corridor-layered-passage.png","haunted-corridor-memory-seal.png","haunted-corridor-exit-clue.png"};
            for(int index=0;index<positions.Length;index++)
            {
                PlacePlayer(positions[index],false);camera.transform.rotation=Quaternion.LookRotation(targets[index]-camera.transform.position);
                yield return Delay(.3f);camera.transform.rotation=Quaternion.LookRotation(targets[index]-camera.transform.position);
                var frame=SchoolCameraFrame(camera,out var projection,out _,out var viewport);
                try
                {
                    CloudExperienceTests.Artifact(names[index],frame.EncodeToPNG());
                    views.Add(new HauntedView {image=names[index],feet=positions[index],camera=camera.transform.position,target=targets[index],flashlight=flashlight.enabled});
                    if(index!=3)continue;
                    var clue=Get<TextMesh>(art,"ReturnClue");Assert.That(clue.text,Is.EqualTo("기억을 돌려놓는 문\n봉인 0 / 5"));
                    Assert.That(clue.font,Is.SameAs(Resources.Load<Font>("Fonts/Korean")));
                    var renderer=clue.GetComponent<MeshRenderer>();var screen=SchoolScreenBounds(projection,viewport,renderer.bounds);
                    Assert.That(renderer.bounds.size.z,Is.LessThanOrEqualTo(1.052f),"Printed clue extends past its physical paper width");
                    Assert.That(renderer.bounds.size.y,Is.LessThanOrEqualTo(.602f),"Printed clue extends past its physical paper height");
                    Assert.That(screen.xMin,Is.InRange(0,1));Assert.That(screen.xMax,Is.InRange(0,1));
                    Assert.That(screen.yMin,Is.InRange(0,1));Assert.That(screen.yMax,Is.InRange(0,1));
                    renderer.enabled=false;var absent=SchoolCameraFrame(camera,out _,out _,out _);renderer.enabled=true;
                    try {report.clueChangedPixels=HauntedChanged(frame,absent,screen);Assert.That(report.clueChangedPixels,Is.GreaterThan(12),"Korean exit clue produced no visible glyph raster");}
                    finally {Object.Destroy(absent);}
                }
                finally {Object.Destroy(frame);}
            }
            // Calibrated sight fixture: unchanged authored silhouette at an actual
            // open 14m corridor distance; disable behaviour rather than changing speed.
            var guard=Components("StalkerBrain").First(brain=>brain.name.Contains("Cyclopse") && brain.name.EndsWith("— corridor"));
            foreach(var behaviour in guard.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            guard.GetComponent<NavMeshAgent>().enabled=false;
            Vector3 guardFeet=hallFeet+forward*14;
            Assert.That(NavMesh.SamplePosition(guardFeet,out var guardFloor,.5f,NavMesh.AllAreas),Is.True);
            guard.transform.SetPositionAndRotation(guardFloor.position,Quaternion.LookRotation(-forward));guard.gameObject.SetActive(true);
            PlacePlayer(hallFeet,false);camera.transform.rotation=Quaternion.LookRotation(forward);yield return Delay(.3f);
            report.guardDistance=Vector3.Distance(new Vector3(camera.transform.position.x,0,camera.transform.position.z),
                new Vector3(guard.transform.position.x,0,guard.transform.position.z));
            Assert.That(report.guardDistance,Is.InRange(13.5f,14.5f));
            var guardRenderers=guard.GetComponentsInChildren<Renderer>();Assert.That(guardRenderers,Is.Not.Empty);
            var guardBounds=guardRenderers[0].bounds;foreach(var renderer in guardRenderers.Skip(1))guardBounds.Encapsulate(renderer.bounds);
            var visible=SchoolCameraFrame(camera,out var guardProjection,out _,out var guardViewport);
            try
            {
                const string name="haunted-corridor-guard-14m-flashlight.png";CloudExperienceTests.Artifact(name,visible.EncodeToPNG());
                views.Add(new HauntedView {image=name,feet=hallFeet,camera=camera.transform.position,target=guard.transform.position+Vector3.up*1.6f,flashlight=true});
                var screen=SchoolScreenBounds(guardProjection,guardViewport,guardBounds);
                Assert.That(screen.xMin,Is.InRange(0,1));Assert.That(screen.xMax,Is.InRange(0,1));
                Assert.That(screen.yMin,Is.InRange(0,1));Assert.That(screen.yMax,Is.InRange(0,1));
                guard.gameObject.SetActive(false);var absent=SchoolCameraFrame(camera,out _,out _,out _);
                try {report.visibleGuardPixels=HauntedChanged(visible,absent,screen);Assert.That(report.visibleGuardPixels,Is.GreaterThan(30),"Moderate fog/light concealed the actual guard at 14m");}
                finally {Object.Destroy(absent);}
            }
            finally {Object.Destroy(visible);guard.gameObject.SetActive(false);}
            report.views=views.ToArray();CloudExperienceTests.Artifact("haunted-corridor-presentation.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
            Debug.Log("HAPPYTOY_HAUNTED_PRESENTATION_PASS controlled production-camera timber/paper/lamp/seal/exit renders; geometryChanged="+report.geometryChangedPixels+
                "; clueGlyphChanged="+report.clueChangedPixels+"; guard14mChanged="+report.visibleGuardPixels+"; no physics/navigation tuning");
        }
    }
}
