using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolHorrorSurfacesKeepRealPbrInputsAndReleaseOnlyOwnedCopies()
        {
            var original=Resources.Load<Material>("GraphicsPbr/plaster-damp/material");
            var originalState=(original.GetTexture("_BaseMap"),original.GetTexture("_BumpMap"),original.GetColor("_BaseColor"));
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var surfaces=Get<object>(world,"HorrorSurfaces");
            var imported=Get<Transform>(world,"Root").GetComponentsInChildren<MeshFilter>()
                .Where(f=>f.sharedMesh&&!f.sharedMesh.isReadable&&!f.GetComponentInParent<LODGroup>()).ToArray();
            Assert.That(imported.Length,Is.GreaterThan(0),"The retained furnishings are absent from the real school");
            Assert.That(imported.All(f=>f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().enabled),Is.True,
                "Batching discarded an imported furnishing whose CPU mesh data is unavailable");
            var materials=new List<Material>();
            foreach(string name in new[]{"Wall","WallAlternate","LowerWall","Ceiling","CeilingAlternate","Floor"})
            {
                var material=Get<Material>(surfaces,name);materials.Add(material);
                Assert.That(material&&material.shader&&material.shader.isSupported,Is.True,name+" has an unavailable shader");
                Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null.And.Not.SameAs(Texture2D.whiteTexture),name+" lost the scanned colour surface");
                Assert.That(material.GetTexture("_BumpMap"),Is.Not.Null,name+" lost its physical normal map");
                Assert.That(material.GetTexture("_MetallicGlossMap"),Is.Not.Null,name+" lost its roughness/smoothness map");
                Assert.That(material,Is.Not.SameAs(original));
            }
            Assert.That((original.GetTexture("_BaseMap"),original.GetTexture("_BumpMap"),original.GetColor("_BaseColor")),Is.EqualTo(originalState));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            foreach(var material in materials)Assert.That(!material,Is.True,"A school-only surface survived chapter unload");
            Assert.That((original.GetTexture("_BaseMap"),original.GetTexture("_BumpMap"),original.GetColor("_BaseColor")),Is.EqualTo(originalState));
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolHorrorLightingFreezesOnPauseAndDoesNotLeakRenderSettings()
        {
            yield return BeginObservationSchool();
            var camera=Get<Camera>(player,"eyes");
            var initialReflection=(RenderSettings.defaultReflectionMode,RenderSettings.customReflectionTexture);
            var earlyFrame=EntranceRitualFrame(camera,320,180);Object.Destroy(earlyFrame);
            Assert.That((RenderSettings.defaultReflectionMode,RenderSettings.customReflectionTexture),Is.EqualTo(initialReflection),
                "The pending school reflection fallback leaked to other cameras");
            yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var root=Get<Transform>(world,"Root");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            Assert.That(lighting,Is.Not.Null);Assert.That(Get<bool>(lighting,"Prepared"),Is.True);
            var probes=Get<IReadOnlyList<ReflectionProbe>>(lighting,"OwnedProbes").ToArray();
            Assert.That(probes.Length,Is.EqualTo(2));
            Assert.That(probes.All(p=>p.resolution<=128&&p.boxProjection&&p.refreshMode==ReflectionProbeRefreshMode.ViaScripting),Is.True);
            Call(shell,"Pause");yield return null;
            var lights=root.GetComponentsInChildren<Light>();var before=lights.Select(l=>l.intensity).ToArray();
            yield return Delay(.4f);
            CollectionAssert.AreEqual(before,lights.Select(l=>l.intensity).ToArray(),"Paused fluorescent lights continued flickering");
            var environment=(RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.fog,
                RenderSettings.fogMode,RenderSettings.fogColor,RenderSettings.fogDensity,RenderSettings.reflectionIntensity);
            var reflection=(RenderSettings.defaultReflectionMode,RenderSettings.customReflectionTexture);
            var frame=EntranceRitualFrame(camera,640,360);
            Object.Destroy(frame);
            Assert.That((RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.fog,
                RenderSettings.fogMode,RenderSettings.fogColor,RenderSettings.fogDensity,RenderSettings.reflectionIntensity),Is.EqualTo(environment),
                "The school render scope changed the environment seen by another camera");
            Assert.That((RenderSettings.defaultReflectionMode,RenderSettings.customReflectionTexture),Is.EqualTo(reflection));
            var volume=Get<Volume>(lighting,"OwnedVolume");var profile=volume.sharedProfile;
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(!volume&&!profile&&probes.All(p=>!p),Is.True,"School-only render resources survived restart");
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWetFloorReflectsTheActualViewAndReleasesItsPrivateCamera()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var root=Get<Transform>(world,"Root");
            var planar=root.GetComponent(RequireType("SchoolPlanarReflection"));
            Assert.That(planar,Is.Not.Null,"The wet floor has no real reflected scene camera");
            Assert.That(Get<bool>(planar,"Prepared"),Is.True);
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            Call(shell,"Pause");PlacePlayer(LoopOrigin+new Vector3(.8f,.03f,6.9f),false);
            var camera=Get<Camera>(player,"eyes");
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(0,.15f,10.5f)-camera.transform.position);
            Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
            bool culling=GL.invertCulling;
            var globals=(Shader.GetGlobalTexture("_SchoolPlanarTexture"),Shader.GetGlobalMatrix("_SchoolPlanarVP"),
                Shader.GetGlobalFloat("_SchoolPlanarAvailable"),Shader.GetGlobalFloat("_SchoolPlanarCapture"));
            Texture2D reflected=null,without=null;RenderTexture target=null;Camera ownedCamera=null;
            try
            {
                reflected=EntranceRitualFrame(camera,960,540);
                target=Get<RenderTexture>(planar,"OwnedTexture");ownedCamera=Get<Camera>(planar,"OwnedCamera");
                Assert.That(target&&target.IsCreated()&&ownedCamera,Is.True);
                Assert.That(target.width,Is.LessThanOrEqualTo(640));Assert.That(target.height,Is.LessThanOrEqualTo(360));
                Assert.That(Get<int>(planar,"CaptureCount"),Is.GreaterThan(0));
                Assert.That(Get<bool>(planar,"IsCapturing"),Is.False,"A nested render did not finish");
                Assert.That(ownedCamera.enabled,Is.False,"A reflection camera acquired a backbuffer view");
                Assert.That(GL.invertCulling,Is.EqualTo(culling),"The reflection leaked mirrored face culling");
                Assert.That((Shader.GetGlobalTexture("_SchoolPlanarTexture"),Shader.GetGlobalMatrix("_SchoolPlanarVP"),
                    Shader.GetGlobalFloat("_SchoolPlanarAvailable"),Shader.GetGlobalFloat("_SchoolPlanarCapture")),Is.EqualTo(globals),
                    "School reflection bindings leaked outside the production camera render");
                AssertSchoolReflectedFixture(root,planar,camera,target);
                ((Behaviour)planar).enabled=false;
                without=EntranceRitualFrame(camera,960,540);
                var on=reflected.GetPixels32();var off=without.GetPixels32();int changed=0;
                for(int y=0;y<270;y++)for(int x=0;x<960;x++)
                {
                    int i=y*960+x;
                    if(Mathf.Abs(on[i].r-off[i].r)+Mathf.Abs(on[i].g-off[i].g)+Mathf.Abs(on[i].b-off[i].b)>10)changed++;
                }
                Assert.That(changed,Is.GreaterThan(500),"The real planar scene made no visible contribution to the wet floor");
            }
            finally
            {
                if(reflected)Object.Destroy(reflected);if(without)Object.Destroy(without);
                if(planar)((Behaviour)planar).enabled=true;
            }
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(!ownedCamera&&!target,Is.True,"The school's private reflection camera/target survived chapter unload");
        }
        static void AssertSchoolReflectedFixture(Transform root,Component planar,Camera camera,RenderTexture target)
        {
            var fixture=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.sharedMaterials.Any(m=>m&&
                m.name.StartsWith("Owned school tube phosphor",StringComparison.Ordinal)))
                .OrderBy(r=>(r.bounds.center-(LoopOrigin+new Vector3(0,3.08f,11))).sqrMagnitude).FirstOrDefault();
            Assert.That(fixture,Is.Not.Null,"The real illuminated fluorescent mesh is absent");
            float plane=Get<float>(planar,"PlaneHeight");var lightPoint=fixture.bounds.center;
            var mirror=new Vector3(lightPoint.x,2*plane-lightPoint.y,lightPoint.z);var eye=camera.transform.position;
            var hit=Vector3.LerpUnclamped(eye,mirror,(plane-eye.y)/(mirror.y-eye.y));
            var clip=Get<Matrix4x4>(planar,"LastReflectionProjection")*new Vector4(hit.x,hit.y,hit.z,1);
            var uv=new Vector2(clip.x,clip.y)/clip.w*.5f+Vector2.one*.5f;
            Assert.That(uv.x,Is.InRange(.02f,.98f));Assert.That(uv.y,Is.InRange(.02f,.98f));
            var previous=RenderTexture.active;Texture2D readback=null;
            try
            {
                RenderTexture.active=target;readback=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
                readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply(false,false);
                int x=Mathf.RoundToInt(uv.x*(target.width-1)),y=Mathf.RoundToInt(uv.y*(target.height-1));float peak=0;
                for(int row=-4;row<=4;row++)for(int col=-4;col<=4;col++)
                    peak=Mathf.Max(peak,readback.GetPixel(Mathf.Clamp(x+col,0,target.width-1),Mathf.Clamp(y+row,0,target.height-1)).maxColorComponent);
                Assert.That(peak,Is.GreaterThan(.5f),
                    "The physical floor reflection samples the wrong texture row instead of the actual fluorescent tube; UV="+uv+" HDR peak="+peak);
                Debug.Log("HAPPYTOY_SCHOOL_REFLECTED_FIXTURE_PASS uv="+uv+" hdrPeak="+peak);
            }
            finally{RenderTexture.active=previous;if(readback)Object.Destroy(readback);}
        }

        static Texture2D SchoolHorrorFrame(Camera camera,int width,int height)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=camera.targetTexture;float aspect=camera.aspect;
            try
            {
                Assert.That(target.Create(),Is.True);camera.targetTexture=target;camera.aspect=width/(float)height;
                // StandardRequest enters the same volume update and camera-stack path as normal play.
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                return CloudExperienceTests.Read(target);
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;target.Release();Object.Destroy(target);}
        }
        static float SchoolDisplayLuma(Texture2D frame,Rect region)
        {
            var pixels=frame.GetPixels32();double sum=0;int count=0;
            for(int y=(int)(frame.height*region.yMin);y<(int)(frame.height*region.yMax);y++)
                for(int x=(int)(frame.width*region.xMin);x<(int)(frame.width*region.xMax);x++)
                {var p=pixels[y*frame.width+x];sum+=(p.r*.2126+p.g*.7152+p.b*.0722)/255;count++;}
            return (float)(sum/Math.Max(1,count));
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            ((Behaviour)player).enabled=false;torch.enabled=false;Call(shell,"Pause");
            PlacePlayer(LoopOrigin+new Vector3(.72f,.03f,6.3f),false);
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(-.15f,1.58f,36)-camera.transform.position);
            Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
            Texture2D normal=null;
            try
            {
                // Model the hand camera/another camera leaving a different Volume stack behind.
                // The preview must update the school's own stack, rather than reuse that state.
                VolumeManager.instance.Update(camera.transform,0);
                normal=SchoolHorrorFrame(camera,960,540);
                float normalMean=SchoolDisplayLuma(normal,new Rect(0,0,1,1));
                float wall=SchoolDisplayLuma(normal,new Rect(.08f,.23f,.24f,.23f));
                var tone=VolumeManager.instance.stack.GetComponent<Tonemapping>();
                var grade=VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
                Debug.Log("HAPPYTOY_SCHOOL_NORMAL_BRIGHTNESS normal="+normalMean+" wall="+wall+
                    " tone="+tone.mode.value+" exposure="+grade.postExposure.value+" contrast="+grade.contrast.value);
                var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
                var lighting=Get<Transform>(world,"Root").GetComponent(RequireType("SchoolHorrorLighting"));
                var profile=Get<Volume>(lighting,"OwnedVolume").sharedProfile;
                Assert.That(profile.TryGet<Tonemapping>(out var configuredTone),Is.True);
                Assert.That(tone.mode.value,Is.EqualTo(configuredTone.mode.value),"Preview reused a stale unrelated camera Volume stack");
                string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR");
                if(!string.IsNullOrWhiteSpace(output))
                {
                    output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                    Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                    Directory.CreateDirectory(output);
                    File.WriteAllBytes(Path.Combine(output,"brightness-normal-stack.png"),normal.EncodeToPNG());
                }
                // Approved preview: weighted sRGB luma .04579; wall region .08486, torch off.
                // Match the native review's existing upper limit: completed probes and
                // the bounded fluorescent voltage phase can lift the mean slightly.
                // The dark missing-cluster regression is .004, well below this lower bound.
                Assert.That(normalMean,Is.InRange(.038f,.057f),"Normal play must retain the approved preview's readable shadows");
                Assert.That(wall,Is.InRange(.065f,.115f),"The nearby moulded corridor wall must retain the preview's readable detail");
            }
            finally{if(normal)Object.Destroy(normal);}
        }

        [Serializable] sealed class SchoolHorrorView
        {
            public string id,file;public Vector3 eye;public Quaternion rotation;
            public float meanDisplayLuma,visibleFraction,flashlightIntensity;public int magentaPixels;
            public bool flashlight,torchAlignedToPausedCamera;
        }
        [Serializable] sealed class SchoolHorrorReview
        {
            public string unity,device,api,createdUtc;
            public string scope="Actual production URP camera, school v3, paused actors, torch disabled except explicitly named torch view. Images are unfiltered game renders. This is a visual review, not a manual playthrough or displayed FPS claim.";
            public int width=1920,height=1080,staticBatches,enabledRenderers,activeLights,planarCaptures,planarWidth,planarHeight;public SchoolHorrorView[] views;
        }
        [Serializable] sealed class SchoolReflectedCameraReview
        {
            public string scope="Actual school planar render-target readback. EXR retains linear HDR pixels. The PNG is a Reinhard/sRGB diagnostic preview, not the production camera image or its final grade.";
            public Vector3 eye;public Quaternion rotation;public Matrix4x4 worldToCamera,projection;
            public Vector4 clipPlane;public int width,height,captures;public float maximumLinearChannel;
        }
        static void ExportSchoolReflectedCamera(Component planar,string output,string id)
        {
            var target=Get<RenderTexture>(planar,"OwnedTexture");var camera=Get<Camera>(planar,"OwnedCamera");
            if(!target||!target.IsCreated()||!camera)return;
            var previous=RenderTexture.active;Texture2D raw=null,preview=null;
            try
            {
                RenderTexture.active=target;raw=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
                raw.ReadPixels(new Rect(0,0,target.width,target.height),0,0);raw.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,id+"-reflected-camera.exr"),raw.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
                var colours=raw.GetPixels();float maximum=0;
                for(int i=0;i<colours.Length;i++)
                {
                    var c=colours[i];maximum=Mathf.Max(maximum,c.maxColorComponent);
                    float r=Mathf.Max(0,c.r),g=Mathf.Max(0,c.g),b=Mathf.Max(0,c.b);
                    colours[i]=new Color(Mathf.LinearToGammaSpace(r/(1+r)),Mathf.LinearToGammaSpace(g/(1+g)),Mathf.LinearToGammaSpace(b/(1+b)),1);
                }
                preview=new Texture2D(target.width,target.height,TextureFormat.RGB24,false,false);preview.SetPixels(colours);preview.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,id+"-reflected-camera.png"),preview.EncodeToPNG());
                File.WriteAllText(Path.Combine(output,id+"-reflected-camera.json"),JsonUtility.ToJson(new SchoolReflectedCameraReview {
                    eye=camera.transform.position,rotation=camera.transform.rotation,worldToCamera=camera.worldToCameraMatrix,
                    projection=camera.projectionMatrix,clipPlane=Get<Vector4>(planar,"LastClipPlane"),width=target.width,height=target.height,
                    captures=Get<int>(planar,"CaptureCount"),maximumLinearChannel=maximum},true));
            }
            finally{RenderTexture.active=previous;if(raw)Object.Destroy(raw);if(preview)Object.Destroy(preview);}
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator SchoolHorrorActualCameraShowsReadableDecayRoomsAndWetCorridor()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var lighting=Get<Transform>(world,"Root").GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"The fixed school reflection zones never completed their initial captures");
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            ((Behaviour)player).enabled=false;torch.enabled=false;Call(shell,"Pause");
            var views=new List<SchoolHorrorView>();
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True,"Review frames belong inside Builds");
                Directory.CreateDirectory(output);
            }
            var shots=new[]{
                ("corridor-reference",new Vector3(.72f,.03f,6.3f),new Vector3(-.15f,1.58f,36)),
                ("study-through-glass",new Vector3(.95f,.03f,18.8f),new Vector3(-5.9f,1.3f,20.5f)),
                ("music-through-glass",new Vector3(.1f,.03f,29.9f),new Vector3(-6.0f,1.35f,30.4f)),
                ("corridor-reverse",new Vector3(-.55f,.03f,32),new Vector3(.25f,1.5f,5.3f)),
                ("wall-floor-close",new Vector3(.9f,.03f,12.3f),new Vector3(1.98f,.45f,15.8f)),
                ("leak-pool-close",new Vector3(.8f,.03f,6.9f),new Vector3(0,.15f,10.5f)),
                ("ceiling-breach",new Vector3(-.2f,.03f,8.4f),new Vector3(-.55f,3.4f,10.1f)),
                ("peeling-wall-close",new Vector3(.7f,.03f,10.4f),new Vector3(2.1f,.62f,11.3f)),
                ("corridor-with-torch",new Vector3(.72f,.03f,6.3f),new Vector3(-.15f,1.58f,36))};
            foreach(var shot in shots)
            {
                torch.enabled=shot.Item1=="corridor-with-torch";
                PlacePlayer(LoopOrigin+shot.Item2,false);
                camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+shot.Item3-camera.transform.position);
                var previousTorchPosition=torch.transform.position;var previousTorchRotation=torch.transform.rotation;
                // The paused hand rig keeps its previous aim. Align its actual lamp
                // with this explicit diagnostic pose without changing its power/cookie.
                if(torch.enabled)torch.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
                Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
                var frame=SchoolHorrorFrame(camera,1920,1080);
                try
                {
                    long sum=0;int visible=0,magenta=0;var pixels=frame.GetPixels32();
                    foreach(var c in pixels)
                    {
                        int luma=(c.r*54+c.g*183+c.b*19)/256;sum+=luma;
                        if(luma>8)visible++;
                        if(c.r>180&&c.b>180&&c.g<50)magenta++;
                    }
                    var view=new SchoolHorrorView {id=shot.Item1,file=shot.Item1+".jpg",eye=camera.transform.position,rotation=camera.transform.rotation,
                        meanDisplayLuma=sum/(255f*pixels.Length),visibleFraction=visible/(float)pixels.Length,magentaPixels=magenta,
                        flashlight=torch.enabled,flashlightIntensity=torch.intensity,torchAlignedToPausedCamera=torch.enabled};
                    views.Add(view);
                    if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(Path.Combine(output,view.file),frame.EncodeToJPG(96));
                    if(!string.IsNullOrWhiteSpace(output)&&(shot.Item1=="corridor-reference"||shot.Item1=="leak-pool-close"))
                        ExportSchoolReflectedCamera(Get<Transform>(world,"Root").GetComponent(RequireType("SchoolPlanarReflection")),output,shot.Item1);
                    Assert.That(magenta,Is.LessThan(40),shot.Item1+" contains an error shader");
                    Assert.That(view.visibleFraction,Is.GreaterThan(.12f),shot.Item1+" is crushed into unreadable black");
                }
                finally{Object.Destroy(frame);torch.transform.SetPositionAndRotation(previousTorchPosition,previousTorchRotation);}
                yield return null;
            }
            if(!string.IsNullOrWhiteSpace(output))
            {
                var planar=Get<Transform>(world,"Root").GetComponent(RequireType("SchoolPlanarReflection"));
                var target=Get<RenderTexture>(planar,"OwnedTexture");
                File.WriteAllText(Path.Combine(output,"review-manifest.json"),JsonUtility.ToJson(new SchoolHorrorReview {
                    unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                    createdUtc=DateTime.UtcNow.ToString("o"),staticBatches=Get<int>(world,"StaticBatches"),
                    enabledRenderers=Get<Transform>(world,"Root").GetComponentsInChildren<Renderer>().Count(r=>r.enabled),
                    activeLights=Get<Transform>(world,"Root").GetComponentsInChildren<Light>().Count(l=>l.isActiveAndEnabled&&l.intensity>0),
                    planarCaptures=Get<int>(planar,"CaptureCount"),planarWidth=target?target.width:0,planarHeight=target?target.height:0,
                    views=views.ToArray()},true));
            }
            Debug.Log("HAPPYTOY_SCHOOL_HORROR_CAMERA_PASS "+JsonUtility.ToJson(new SchoolHorrorReview {views=views.ToArray()}));
        }
    }
}
