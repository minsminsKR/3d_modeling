using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Controlled real-camera presentation review and physical/lifecycle invariants.
    // These views do not certify subjective art quality or first-time-player balance.
    public sealed partial class CloudPlayModeTests
    {
        [System.Serializable] sealed class SchoolStandCloseup
        {
            public string image, approachSide, material, texture;
            public Vector3 camera, target, memory, physicalCenter, physicalSize;
            public Color materialColor;
            public float cameraDistance, physicalTop, flashlightIntensity, flashlightSpotAngle;
            public bool inputAllowed, actualFlashlightOn, crouched;
        }
        [System.Serializable] sealed class SchoolStandCloseupReport
        {
            public string scope="Controlled crouched production-player camera placement for art review, with real C/F keyboard input and the actual attached flashlight. Not an input survival-route claim.";
            public SchoolStandCloseup[] views;
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator SchoolWayfindingRetainsCeramicCollisionAndRestoresOriginalPresentationOnRemoval()
        {
            var roomSigns=Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include)
                .Where(label=>label.gameObject.scene==session.gameObject.scene &&
                    (label.name.StartsWith("CLASSROOM sign") || label.name.StartsWith("WASHROOM sign") || label.name.StartsWith("INFIRMARY sign"))).ToArray();
            Assert.That(roomSigns.Length,Is.EqualTo(6));
            var originals=roomSigns.ToDictionary(label=>label,label=>new {label.text,label.color,scale=label.transform.localScale});
            var floor=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Single(renderer=>renderer.name=="WASHROOM floor");
            var floorMaterial=floor.sharedMaterial;var tileTexture=floorMaterial.GetTexture("_BaseMap");
            Assert.That(tileTexture,Is.Not.Null,"Fixture must use the actual authored washroom ceramic texture");
            var floorCollider=floor.GetComponent<Collider>();var floorPosition=floor.transform.position;var floorScale=floor.transform.lossyScale;
            var physical=Object.FindObjectsByType<Collider>(FindObjectsInactive.Include)
                .Where(collider=>collider.gameObject.scene==session.gameObject.scene &&
                    (collider.name.ToLowerInvariant().Contains("floor") || collider.name=="Stair tread"))
                .ToDictionary(collider=>collider,collider=>new {position=collider.transform.position,scale=collider.transform.lossyScale,collider.enabled});
            Call(shell,"BeginChapter");yield return null;yield return null;yield return null;
            var atmosphere=session.GetComponent(RequireType("ChapterAtmosphere"));var wayfinding=Get<Component>(atmosphere,"Wayfinding");
            Assert.That(Get<int>(wayfinding,"LocalizedRoomSigns"),Is.EqualTo(6));Assert.That(Get<int>(wayfinding,"MountedNotices"),Is.EqualTo(6));
            Assert.That(Get<int>(atmosphere,"PreservedTileFloors"),Is.GreaterThanOrEqualTo(1));
            Assert.That(floor.sharedMaterial.GetTexture("_BaseMap"),Is.SameAs(tileTexture),"School patina replaced ceramic with a wooden floor");
            Assert.That(floorCollider.enabled,Is.True);Assert.That(floor.transform.position,Is.EqualTo(floorPosition));
            Assert.That(floor.transform.lossyScale,Is.EqualTo(floorScale));
            foreach(var pair in physical)
            {
                Assert.That(pair.Key,Is.Not.Null,"School dressing destroyed actual floor/stair collision");
                Assert.That(pair.Key.enabled,Is.EqualTo(pair.Value.enabled));Assert.That(pair.Key.transform.position,Is.EqualTo(pair.Value.position));
                Assert.That(pair.Key.transform.lossyScale,Is.EqualTo(pair.Value.scale));
            }
            var additions=Get<Transform>(wayfinding,"AdditionRoot");
            Assert.That(additions.GetComponentsInChildren<Collider>(true),Is.Empty,"Wayfinding blocks physical passage");
            Assert.That(additions.GetComponentsInChildren<NavMeshObstacle>(true),Is.Empty,"Visual notice creates a navigation blocker");
            var font=Resources.Load<Font>("Fonts/Korean");
            foreach(var label in roomSigns)
            {
                string expected=label.name.StartsWith("CLASSROOM")?"1학년 2반":label.name.StartsWith("WASHROOM")?"화장실":"보건실";
                Assert.That(label.text,Is.EqualTo(expected));Assert.That(label.font,Is.SameAs(font));
                Assert.That(label.GetComponent<MeshRenderer>().sharedMaterial.shader.name,Is.EqualTo("HappyToy/WorldSignText"));
            }
            var root=Get<Transform>(atmosphere,"DressingRoot");var appliedFloor=floor.sharedMaterial;
            // The school surface owner retains its cached ceramic material. The
            // chapter's temporary patina clones must still all be released.
            var ownedChapterSurfaces=Resources.FindObjectsOfTypeAll<Material>()
                .Where(material=>material.name=="Chapter retained washroom ceramic"||material.name=="Chapter damp surface").ToArray();
            Assert.That(ownedChapterSurfaces,Is.Not.Empty);
            Assert.That(appliedFloor,Is.SameAs(floorMaterial));
            var signsCreated=additions.GetComponentsInChildren<TextMesh>(true);
            Assert.That(signsCreated.All(label=>label.text.Any(character=>character>='가' && character<='힣')),Is.True);
            Object.Destroy(atmosphere);yield return null;yield return null;yield return null;
            Assert.That(root==null,Is.True,"Removing chapter dressing leaks visual/collision children");
            Assert.That(additions==null && wayfinding==null,Is.True,"Removing chapter dressing leaks wayfinding");
            Assert.That(ownedChapterSurfaces.All(material=>material==null),Is.True,"Chapter-owned patina material leaked");
            Assert.That(appliedFloor,Is.SameAs(floorMaterial));Assert.That(floor.sharedMaterial,Is.SameAs(floorMaterial));
            Assert.That(tileTexture && floorMaterial,Is.True,"Removing dressing destroyed the shared imported tile texture/material");
            foreach(var pair in originals)
            {
                Assert.That(pair.Key.text,Is.EqualTo(pair.Value.text));Assert.That(pair.Key.color,Is.EqualTo(pair.Value.color));
                Assert.That(pair.Key.transform.localScale,Is.EqualTo(pair.Value.scale));
            }
            Assert.That(Object.FindObjectsByType<Light>().Any(light=>light.name=="Memory reflected glimmer"),Is.False,
                "Memory light reparented outside the visual root leaked after removal");
            Debug.Log("HAPPYTOY_SCHOOL_PRESENTATION_PASS physical school floors/stairs retained; authored ceramic retained; six localized room faces; notices have no collision/NavMesh obstacles; removal restores original signs/materials and releases children");
        }

        static Texture2D SchoolCameraFrame(Camera camera,out Matrix4x4 viewProjection,out float aspect,out Rect viewport)
        {
            var target=new RenderTexture(1280,720,24);target.Create();
            var originalTarget=camera.targetTexture;
            try
            {
                // URP temporarily binds request.destination and restores targetTexture.
                // Capture the actual target's projection while bound, not the restored Game View aspect.
                camera.targetTexture=target;
                viewProjection=camera.projectionMatrix*camera.worldToCameraMatrix;aspect=camera.aspect;viewport=camera.rect;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                return CloudExperienceTests.Read(target);
            }
            finally {camera.targetTexture=originalTarget;target.Release();Object.Destroy(target);}
        }
        static Rect SchoolScreenBounds(Matrix4x4 viewProjection,Rect viewport,Bounds bounds)
        {
            var minimum=new Vector2(1,1);var maximum=Vector2.zero;
            foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1})
            {
                Vector3 world=bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                Vector4 clip=viewProjection*new Vector4(world.x,world.y,world.z,1);
                Assert.That(clip.w,Is.GreaterThan(0),"Room sign is behind the actual entrance view");
                var point=new Vector2(viewport.x+(clip.x/clip.w*.5f+.5f)*viewport.width,
                    viewport.y+(clip.y/clip.w*.5f+.5f)*viewport.height);
                minimum=Vector2.Min(minimum,point);maximum=Vector2.Max(maximum,point);
            }
            return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator SchoolRealCameraViewsRenderKoreanRoomWritingAndRetainThreeFloorApproaches()
        {
            Call(shell,"BeginChapter");yield return null;yield return null;IsolateThreats();
            foreach(var door in Components("Interactable").Where(item=>Get<object>(item,"kind").ToString()=="Door")) Call(door,"OpenForPursuer");
            yield return Delay(1.4f);
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;
            var chapter=Get<Component>(session,"Chapter");var memories=Get<Component[]>(chapter,"Memories");
            Assert.That(NavMesh.SamplePosition(player.transform.position,out var start,.5f,NavMesh.AllAreas),Is.True);
            foreach(var memory in memories)
            {
                float floor=memory==memories[4]?-5:memory==memories[2]||memory==memories[3]?5:0;
                var at=memory.transform.position+Vector3.back*.85f;at.y=floor;
                Assert.That(NavMesh.SamplePosition(at,out var destination,1.3f,NavMesh.AllAreas),Is.True,"No actual school approach: "+memory.name);
                Assert.That(Mathf.Abs(destination.position.y-floor),Is.LessThan(.2f));
                var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(start.position,destination.position,NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Presentation disconnected a memory approach: "+memory.name);
            }
            // Controlled same-camera positions on the real loaded school, including upper and flooded basement rooms.
            var positions=new[]{new Vector3(-7.8f,.02f,0),new Vector3(-4.5f,.02f,-3.2f),new Vector3(25.3f,5.02f,22.8f),new Vector3(13.8f,-4.98f,-23)};
            var targets=new[]{new Vector3(0,1.6f,0),new Vector3(-4.5f,2.35f,-1.3f),new Vector3(26.8f,6.7f,32.4f),new Vector3(13.8f,-3.4f,-31)};
            var names=new[]{"school-presentation-entrance.png","school-presentation-washroom.png","school-presentation-upper.png","school-presentation-basement.png"};
            for(int index=0;index<positions.Length;index++)
            {
                PlacePlayer(positions[index],false);camera.transform.rotation=Quaternion.LookRotation(targets[index]-camera.transform.position);
                yield return Delay(.3f);
                float gameViewAspect=camera.aspect;
                var frame=SchoolCameraFrame(camera,out var captureProjection,out float captureAspect,out var captureViewport);
                try
                {
                    CloudExperienceTests.Artifact(names[index],frame.EncodeToPNG());
                    if(index!=0) continue;
                    var sign=Object.FindObjectsByType<TextMesh>().Single(label=>label.name=="CLASSROOM sign");
                    var renderer=sign.GetComponent<MeshRenderer>();var screen=SchoolScreenBounds(captureProjection,captureViewport,renderer.bounds);
                    Assert.That(screen.xMin,Is.InRange(0,1));Assert.That(screen.xMax,Is.InRange(0,1));
                    Assert.That(screen.yMin,Is.InRange(0,1));Assert.That(screen.yMax,Is.InRange(0,1));
                    Assert.That(sign.text,Is.EqualTo("1학년 2반"));
                    renderer.enabled=false;var absent=SchoolCameraFrame(camera,out _,out _,out _);renderer.enabled=true;
                    try
                    {
                        var before=frame.GetPixels32();var after=absent.GetPixels32();int changed=0,totalChanged=0;
                        for(int pixel=0;pixel<before.Length;pixel++)
                            if(Mathf.Abs(before[pixel].r-after[pixel].r)+Mathf.Abs(before[pixel].g-after[pixel].g)+Mathf.Abs(before[pixel].b-after[pixel].b)>24)totalChanged++;
                        int x0=Mathf.Max(0,Mathf.FloorToInt(screen.xMin*frame.width)-2),x1=Mathf.Min(frame.width-1,Mathf.CeilToInt(screen.xMax*frame.width)+2);
                        int y0=Mathf.Max(0,Mathf.FloorToInt(screen.yMin*frame.height)-2),y1=Mathf.Min(frame.height-1,Mathf.CeilToInt(screen.yMax*frame.height)+2);
                        for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                        {int pixel=y*frame.width+x;if(Mathf.Abs(before[pixel].r-after[pixel].r)+Mathf.Abs(before[pixel].g-after[pixel].g)+Mathf.Abs(before[pixel].b-after[pixel].b)>24)changed++;}
                        Debug.Log("HAPPYTOY_SCHOOL_KOREAN_RASTER sign="+sign.text+", changedGlyphPixels="+changed+
                            ", totalChanged="+totalChanged+", gameViewAspect="+gameViewAspect+", captureAspect="+captureAspect+", screen="+screen);
                        Assert.That(changed,Is.GreaterThan(12),"Localized Korean room text contributes no readable glyph pixels to the true entrance view");
                    }
                    finally {Object.Destroy(absent);renderer.enabled=true;}
                }
                finally {Object.Destroy(frame);}
            }
            // Bring the real player stance/light into a close inspection. The original four views remain above.
            var upperMemory=memories[2];
            PlacePlayer(new Vector3(upperMemory.transform.position.x,5.02f,upperMemory.transform.position.z-1.19f));
            ((Behaviour)player).enabled=true;Keys();yield return null;
            yield return Wait(()=>Get<bool>(player,"Grounded"),3,"Close inspection did not settle on the actual upper floor");
            if(!Get<bool>(player,"Crouching"))
            {
                yield return KeysObserved(Key.C);Keys();yield return null;
                Assert.That(Get<bool>(player,"Crouching"),Is.True,"Actual C input did not lower the production camera for inspection");
            }
            var flashlight=Get<Light>(player,"flashlight");var feedback=Get<Component>(player,"Feedback");
            int switches=Get<int>(feedback,"InteractionCuesPlayed");bool originallyLit=flashlight.enabled;
            if(originallyLit)
            {
                yield return KeysObserved(Key.F);Keys();yield return null;
                Assert.That(flashlight.enabled,Is.False,"Actual F input did not switch the flashlight off before inspection");
            }
            yield return KeysObserved(Key.F);Keys();yield return null;
            Assert.That(flashlight.isActiveAndEnabled,Is.True,"Actual F input did not switch the production flashlight on");
            Assert.That(Get<int>(feedback,"InteractionCuesPlayed"),Is.EqualTo(switches+(originallyLit?2:1)));
            Assert.That(flashlight.type,Is.EqualTo(LightType.Spot));Assert.That(flashlight.transform.IsChildOf(camera.transform),Is.True);
            ((Behaviour)player).enabled=false;
            var closeups=new List<SchoolStandCloseup>();
            foreach(int memoryIndex in new[]{2,4})
            {
                var memory=memories[memoryIndex];Vector3 pose=memory.transform.position;float floor=memoryIndex==2?5:-5;
                var physical=Object.FindObjectsByType<BoxCollider>().Single(collider=>collider.name=="Memory support — "+memoryIndex);
                var originalBounds=physical.bounds;float side=memoryIndex==2?-1:1;
                // Upper approach is from the south; the basement's genuine entrance approach sees the opposite (+Z) face.
                Vector3 target=new Vector3(pose.x,originalBounds.center.y+.10f,pose.z+side*.19f);
                PlacePlayer(new Vector3(pose.x,floor+.02f,target.z+side),false);
                camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);yield return Delay(.35f);
                camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);
                float distance=Vector3.Distance(camera.transform.position,target);
                Assert.That(distance,Is.InRange(.8f,1.2f),"Near review is not within the stated actual camera distance");
                Assert.That(Get<bool>(session,"InputAllowed") && flashlight.isActiveAndEnabled && Get<bool>(player,"Crouching"),Is.True);
                Assert.That(physical.bounds.center,Is.EqualTo(originalBounds.center));Assert.That(physical.bounds.size,Is.EqualTo(originalBounds.size));
                Assert.That(memory.transform.position,Is.EqualTo(pose),"Art review moved the actual memory");
                Assert.That(physical.bounds.max.y,Is.EqualTo(pose.y-.09f).Within(.001f),"Support's original physical top moved");
                var body=Object.FindObjectsByType<MeshRenderer>().Single(renderer=>renderer.name=="Timber memory stand body" &&
                    renderer.gameObject.scene==session.gameObject.scene && Vector3.Distance(renderer.bounds.center,originalBounds.center)<.08f);
                Assert.That(body.sharedMaterial.GetTexture("_BaseMap"),Is.SameAs(Resources.Load<Texture2D>("Corridor/aged-floor-v1")));
                string name=memoryIndex==2?"school-memory-stand-upper-flashlight-close.png":"school-memory-stand-basement-flashlight-close.png";
                var frame=SchoolCameraFrame(camera,out _,out _,out _);
                try {CloudExperienceTests.Artifact(name,frame.EncodeToPNG());}
                finally {Object.Destroy(frame);}
                closeups.Add(new SchoolStandCloseup {image=name,approachSide=memoryIndex==2?"south-minusZ":"north-plusZ",camera=camera.transform.position,
                    target=target,memory=pose,physicalCenter=originalBounds.center,physicalSize=originalBounds.size,physicalTop=originalBounds.max.y,
                    cameraDistance=distance,inputAllowed=Get<bool>(session,"InputAllowed"),actualFlashlightOn=flashlight.isActiveAndEnabled,
                    crouched=Get<bool>(player,"Crouching"),flashlightIntensity=flashlight.intensity,flashlightSpotAngle=flashlight.spotAngle,
                    material=body.sharedMaterial.name,materialColor=body.sharedMaterial.GetColor("_BaseColor"),texture=body.sharedMaterial.GetTexture("_BaseMap").name});
            }
            CloudExperienceTests.Artifact("school-memory-stand-closeups.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new SchoolStandCloseupReport {views=closeups.ToArray()},true)));
            Debug.Log("HAPPYTOY_SCHOOL_PRESENTATION_PASS four real world-camera views; visible Korean glyph raster in original entrance; complete real NavMesh approach to all five memories across three floors. Controlled rendered review; separate actual-input route covers survival.");
        }
    }
}
