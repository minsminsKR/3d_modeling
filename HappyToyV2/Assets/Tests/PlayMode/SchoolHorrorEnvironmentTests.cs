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
            foreach(string name in new[]{"Wall","LowerWall","Ceiling","Floor"})
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

        [Serializable] sealed class SchoolHorrorView
        {
            public string id,file;public Vector3 eye;public Quaternion rotation;
            public float meanDisplayLuma,visibleFraction;public int magentaPixels;
        }
        [Serializable] sealed class SchoolHorrorReview
        {
            public string unity,device,api,createdUtc;
            public string scope="Actual production URP camera, school v3, paused actors, torch disabled except explicitly named torch view. Images are unfiltered game renders. This is a visual review, not a manual playthrough or displayed FPS claim.";
            public int width=1920,height=1080,staticBatches,enabledRenderers,activeLights;public SchoolHorrorView[] views;
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
                ("corridor-with-torch",new Vector3(.72f,.03f,6.3f),new Vector3(-.15f,1.58f,36))};
            foreach(var shot in shots)
            {
                torch.enabled=shot.Item1=="corridor-with-torch";
                PlacePlayer(LoopOrigin+shot.Item2,false);
                camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+shot.Item3-camera.transform.position);
                var frame=EntranceRitualFrame(camera,1920,1080);
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
                        meanDisplayLuma=sum/(255f*pixels.Length),visibleFraction=visible/(float)pixels.Length,magentaPixels=magenta};
                    views.Add(view);
                    if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(Path.Combine(output,view.file),frame.EncodeToJPG(96));
                    Assert.That(magenta,Is.LessThan(40),shot.Item1+" contains an error shader");
                    Assert.That(view.visibleFraction,Is.GreaterThan(.12f),shot.Item1+" is crushed into unreadable black");
                }
                finally{Object.Destroy(frame);}
                yield return null;
            }
            if(!string.IsNullOrWhiteSpace(output))
                File.WriteAllText(Path.Combine(output,"review-manifest.json"),JsonUtility.ToJson(new SchoolHorrorReview {
                    unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                    createdUtc=DateTime.UtcNow.ToString("o"),staticBatches=Get<int>(world,"StaticBatches"),
                    enabledRenderers=Get<Transform>(world,"Root").GetComponentsInChildren<Renderer>().Count(r=>r.enabled),
                    activeLights=Get<Transform>(world,"Root").GetComponentsInChildren<Light>().Count(l=>l.isActiveAndEnabled&&l.intensity>0),
                    views=views.ToArray()},true));
            Debug.Log("HAPPYTOY_SCHOOL_HORROR_CAMERA_PASS "+JsonUtility.ToJson(new SchoolHorrorReview {views=views.ToArray()}));
        }
    }
}
