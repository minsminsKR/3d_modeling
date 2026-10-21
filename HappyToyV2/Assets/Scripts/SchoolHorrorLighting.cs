using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    /// <summary>School-only lighting, bounded reflection captures and reversible camera atmosphere.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(220)]
    public sealed class SchoolHorrorLighting : MonoBehaviour
    {
        struct Environment
        {
            public Camera camera;public AmbientMode ambientMode;public Color ambient,sky,equator,ground,fogColor;
            public float ambientIntensity,fogDensity,fogStart,fogEnd;public bool fog;public FogMode fogMode;
            public SphericalHarmonicsL2 harmonics;
            public bool reflectionOverride;public DefaultReflectionMode reflectionMode;public Texture reflection;
            public void Restore()
            {
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;
                RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
                RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.ambientProbe=harmonics;
                RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;
                RenderSettings.fogDensity=fogDensity;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;
                if(reflectionOverride){RenderSettings.defaultReflectionMode=reflectionMode;RenderSettings.customReflectionTexture=reflection;}
            }
        }
        readonly Stack<Environment> renders=new Stack<Environment>();
        readonly List<ReflectionProbe> probes=new List<ReflectionProbe>();
        readonly List<Light> windowLights=new List<Light>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<GameObject> additions=new List<GameObject>();
        SchoolLoopWorld world;GameSession session;Transform root;
        Volume volume;VolumeProfile profile;Bounds cameraBounds;
        Cubemap pendingReflection;
        SchoolPlanarReflection planar;
        int preparedFrame,nextProbe,renderId=-1;bool subscribed;
        public bool Prepared {get;private set;}
        public Volume OwnedVolume=>volume;
        public IReadOnlyList<ReflectionProbe> OwnedProbes=>probes;
        public IReadOnlyList<Light> WindowLights=>windowLights;
        public SchoolPlanarReflection PlanarReflection=>planar;
        public bool ReflectionReady=>Prepared&&nextProbe>=probes.Count&&(renderId<0||probes[probes.Count-1].IsFinishedRendering(renderId));

        public static Light Fixture(SchoolLoopWorld world,Transform parent,Vector3 at,bool warm,float intensity)
        {
            var source=Resources.Load<GameObject>("CorridorAltarChamber/fluorescent-fixture");
            if(!source)throw new InvalidOperationException("Authored school fluorescent fixture missing");
            bool corridor=Mathf.Abs(at.x)<.2f;
            int index=Mathf.RoundToInt((at.z-2.2f)/4.4f);
            bool live=!corridor||index==0||index==2||index==5||index==7;
            bool connecting=Mathf.Abs(at.x)>2.2f&&Mathf.Abs(at.x)<5.1f;
            float power=live?(corridor?(index==0?7.2f:index==2?6.3f:index==5?4.9f:2.6f):connecting?1.6f:intensity*.56f):0;
            var holder=new GameObject(live?"Failing school fluorescent fixture":"Burned-out school fluorescent fixture").transform;
            holder.SetParent(parent,false);holder.localPosition=at;
            var model=UnityEngine.Object.Instantiate(source,holder,false);
            var casing=world.Surfaces.Get("metal-rust",new Color(.76f,.70f,.56f));
            var dark=world.Surfaces.Get("wax-tallow",new Color(.33f,.35f,.29f));
            var color=corridor?new Color(.96f,.94f,.79f):warm?new Color(.72f,.82f,.85f):new Color(.60f,.74f,.86f);
            Color glow=live?color*(corridor?3.8f:2.4f):Color.black;
            var tube=new Material(world.Surfaces.Get("painted-metal",new Color(.65f,.68f,.58f),new Color(.1f,.1f,.1f)))
                {name="Owned school tube phosphor",enableInstancing=true};
            tube.SetTexture("_BaseMap",Texture2D.whiteTexture);tube.SetTexture("_EmissionMap",Texture2D.whiteTexture);
            tube.SetColor("_BaseColor",live?new Color(.74f,.72f,.58f):new Color(.18f,.20f,.18f));
            tube.SetColor("_EmissionColor",glow);tube.SetOverrideTag("GraphicsSurface","");world.owned.Add(tube);
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string slot=materials[i]?materials[i].name:"";
                    bool phosphor=renderer.name.IndexOf("tube",StringComparison.OrdinalIgnoreCase)>=0;
                    materials[i]=phosphor?tube:slot=="CA_DeadGlass"?dark:casing;
                }
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.On;
                renderer.gameObject.layer=8;
            }
            var lamp=new GameObject("School loop fluorescent pool").AddComponent<Light>();lamp.transform.SetParent(holder,false);
            lamp.transform.localPosition=new Vector3(0,-.20f,0);lamp.type=corridor?LightType.Spot:LightType.Point;lamp.color=color;
            // A folded reflector sends the tube output into the aisle, not through its back onto the ceiling.
            if(corridor){lamp.transform.localRotation=Quaternion.Euler(90,0,0);lamp.spotAngle=132;lamp.innerSpotAngle=100;}
            lamp.intensity=power;lamp.range=corridor?8.0f:connecting?6:6.2f;
            lamp.shadows=live&&corridor?LightShadows.Soft:LightShadows.None;
            lamp.shadowNearPlane=.08f;lamp.bounceIntensity=.5f;world.fixtures.Add(lamp);
            Light backscatter=null;
            if(live&&corridor)
            {
                // Small diffuser/reflector backscatter reveals this physical housing and adjacent ceiling.
                // Its short reach cannot fill the long corridor like an omnidirectional main lamp.
                backscatter=new GameObject("School fluorescent local diffuser backscatter").AddComponent<Light>();
                backscatter.transform.SetParent(holder,false);backscatter.transform.localPosition=new Vector3(0,-.12f,0);
                backscatter.type=LightType.Point;backscatter.color=color;backscatter.intensity=.68f;
                backscatter.range=3.15f;backscatter.shadows=LightShadows.None;backscatter.bounceIntensity=0;
            }
            holder.gameObject.AddComponent<SchoolFluorescent>().Configure(lamp,tube,glow,index*1.71f+at.x*.37f,backscatter);
            return lamp;
        }

        public void Prepare(SchoolLoopWorld owner)
        {
            if(Prepared||!owner||!owner.Root||!owner.Centre)throw new InvalidOperationException("Built school required once");
            world=owner;session=owner.GetComponent<GameSession>();
            if(!session||!session.player)throw new InvalidOperationException("School atmosphere needs its player camera");
            root=new GameObject("Owned derelict school light and mist scope").transform;root.SetParent(world.Centre,false);
            profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Owned derelict school cinematic profile";owned.Add(profile);
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.value=.10f;grade.contrast.value=10;grade.saturation.value=-18;
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.25f;bloom.intensity.value=.12f;bloom.scatter.value=.48f;bloom.maxIterations.value=4;bloom.clamp.value=8;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.12f;vignette.smoothness.value=.65f;
            volume=root.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=55;volume.sharedProfile=profile;
            pendingReflection=new Cubemap(8,TextureFormat.RGBAHalf,true){name="Owned black reflection during school capture initialization"};
            for(int mip=0;mip<4;mip++)
            {
                int pixels=Mathf.Max(1,8>>mip);var colors=new Color[pixels*pixels];
                for(int face=0;face<6;face++)pendingReflection.SetPixels(colors,(CubemapFace)face,mip);
            }
            pendingReflection.Apply(false,true);owned.Add(pendingReflection);
            foreach(Transform chunk in world.Root)
            {
                if(!chunk.name.StartsWith("School loop section",StringComparison.Ordinal))continue;
                AddWindows(chunk);AddAlarm(chunk);
            }
            for(int i=0;i<2;i++)
            {
                var probe=new GameObject("School wet-floor reflection zone "+i).AddComponent<ReflectionProbe>();
                probe.transform.SetParent(root,false);probe.transform.localPosition=new Vector3(0,1.55f,i==0?9.6f:28.8f);
                probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.hdr=true;
                // The floor must be INSIDE the influence volume, not within its fading lower edge.
                // Otherwise Forward+ gives a wet surface almost entirely the default sky reflection.
                probe.boxProjection=true;probe.size=new Vector3(4.9f,4.1f,23.2f);probe.blendDistance=.18f;probe.importance=65;
                probe.intensity=.85f;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=Color.black;
                probe.cullingMask=session.player.eyes.cullingMask;probe.shadowDistance=12;probe.nearClipPlane=.08f;probe.farClipPlane=25;
                probes.Add(probe);
            }
            AddHaze();
            cameraBounds=new Bounds(world.Root.position+new Vector3(-3,1.6f,19.2f),new Vector3(55,8,115));
            preparedFrame=Time.frameCount;Prepared=true;Subscribe();
            // Subscribe after this component: the reflected view inherits the same reversible school environment.
            planar=world.Root.gameObject.AddComponent<SchoolPlanarReflection>();planar.Prepare(world);
            session.GetComponent<LocalShadowBudget>()?.RefreshNow();
        }

        void AddWindows(Transform chunk)
        {
            var sourceRoot=new GameObject("Owned school cold-window light scope");
            sourceRoot.transform.SetParent(chunk,false);additions.Add(sourceRoot);
            foreach(var room in SchoolLoopPlan.Rooms)
            {
                float middle=(room.Near+room.Far)*.5f;
                // A broad cold exterior spill reaches the visible furniture through real window openings.
                for(int i=0;i<2;i++)
                {
                    float z=Mathf.Lerp(room.WindowNear,room.WindowFar,i==0?.22f:.78f);
                    var light=new GameObject("Cold overcast classroom window spill").AddComponent<Light>();
                    light.transform.SetParent(sourceRoot.transform,false);light.transform.localPosition=new Vector3(-8.7f,2.35f,z);
                    light.type=LightType.Spot;light.transform.localRotation=Quaternion.Euler(6,90,0);
                    light.color=new Color(.42f,.59f,.78f);light.intensity=2.7f;light.range=9;light.spotAngle=105;light.innerSpotAngle=65;
                    light.shadows=LightShadows.Soft;light.shadowNearPlane=.08f;windowLights.Add(light);
                }
                // Dim room bounce is explicit local illumination; no emissive wall or opaque blue overlay.
                var bounce=new GameObject("Cold classroom indirect fill").AddComponent<Light>();bounce.transform.SetParent(sourceRoot.transform,false);
                bounce.transform.localPosition=new Vector3(-5.6f,2.45f,middle);bounce.type=LightType.Point;
                bounce.color=new Color(.38f,.54f,.71f);bounce.intensity=.72f;bounce.range=6.3f;bounce.shadows=LightShadows.None;windowLights.Add(bounce);
                var transmission=new GameObject("Cold light transmitted through classroom glazing").AddComponent<Light>();
                transmission.transform.SetParent(sourceRoot.transform,false);transmission.transform.localPosition=new Vector3(-2.58f,2.35f,middle);
                transmission.type=LightType.Point;transmission.color=new Color(.39f,.57f,.78f);transmission.intensity=.60f;
                transmission.range=3.8f;transmission.shadows=LightShadows.None;windowLights.Add(transmission);
            }
        }
        void AddAlarm(Transform chunk)
        {
            var sourceRoot=new GameObject("Owned school alarm light scope");
            sourceRoot.transform.SetParent(chunk,false);additions.Add(sourceRoot);
            var red=new GameObject("Distant failed school alarm indicator").AddComponent<Light>();red.transform.SetParent(sourceRoot.transform,false);
            red.transform.localPosition=new Vector3(1.96f,2.64f,35.4f);red.type=LightType.Point;
            red.color=new Color(.8f,.016f,.008f);red.intensity=.68f;red.range=2.35f;red.shadows=LightShadows.None;
            var glass=world.Surfaces.Get("painted-metal",new Color(.28f,.023f,.012f),new Color(1.8f,.018f,.006f));
            world.Box(sourceRoot.transform,"Small red alarm lens",new Vector3(2.035f,2.64f,35.4f),new Vector3(.055f,.07f,.15f),glass);
        }
        void AddHaze()
        {
            var shader=Resources.Load<Shader>("SchoolHorror/SchoolLightHaze");
            if(!shader)throw new InvalidOperationException("School localized light haze shader missing");
            var material=new Material(shader){name="Owned school depth-clipped suspended moisture",enableInstancing=true};owned.Add(material);
            foreach(var lamp in world.Fixtures)
            {
                if(!lamp||lamp.intensity<2.8f||Mathf.Abs(lamp.transform.parent.localPosition.x)>.2f)continue;
                var holder=new GameObject("Local fluorescent dust and moisture volume",typeof(MeshFilter),typeof(MeshRenderer));
                additions.Add(holder);
                holder.layer=8;holder.transform.SetParent(lamp.transform.parent.parent,false);
                holder.transform.localPosition=lamp.transform.parent.localPosition-new Vector3(0,1.12f,0);holder.transform.localScale=new Vector3(4.1f,2.10f,5.5f);
                holder.GetComponent<MeshFilter>().sharedMesh=world.Surfaces.MetreBoxMesh(Vector3.one,0,1);
                var renderer=holder.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                lamp.GetComponentInParent<SchoolFluorescent>().SetHaze(renderer);
            }
        }
        void LateUpdate()
        {
            if(!Prepared||Time.frameCount<=preparedFrame+2||nextProbe>=probes.Count)return;
            var graphics=session.GetComponent<GraphicsLightingPresentation>();if(!graphics||!graphics.Prepared)return;
            if(nextProbe>0&&!probes[nextProbe-1].IsFinishedRendering(renderId))return;
            renderId=probes[nextProbe].RenderProbe();nextProbe++;
        }
        void Subscribe()
        {
            if(subscribed)return;subscribed=true;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;
        }
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!Prepared||!session||!session.ChapterMode||!session.Chapter||session.Chapter.Loop!=world||!camera||
                camera!=session.player.eyes&&camera.cameraType!=CameraType.Reflection||!cameraBounds.Contains(camera.transform.position))return;
            bool pending=!ReflectionReady;
            renders.Push(new Environment{camera=camera,ambientMode=RenderSettings.ambientMode,ambient=RenderSettings.ambientLight,
                sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,
                ambientIntensity=RenderSettings.ambientIntensity,harmonics=RenderSettings.ambientProbe,fog=RenderSettings.fog,
                fogMode=RenderSettings.fogMode,fogColor=RenderSettings.fogColor,fogDensity=RenderSettings.fogDensity,
                fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,
                reflectionOverride=pending,reflectionMode=RenderSettings.defaultReflectionMode,reflection=pending?RenderSettings.customReflectionTexture:null});
            if(pending){RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=pendingReflection;}
            var ambient=new Color(.028f,.040f,.058f);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=ambient;
            RenderSettings.ambientSkyColor=RenderSettings.ambientEquatorColor=RenderSettings.ambientGroundColor=ambient;
            RenderSettings.ambientIntensity=.65f;var harmonics=new SphericalHarmonicsL2();harmonics.AddAmbientLight(ambient*.65f);RenderSettings.ambientProbe=harmonics;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(.006f,.010f,.017f);RenderSettings.fogDensity=.0135f;
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {if(renders.Count>0&&renders.Peek().camera==camera)renders.Pop().Restore();}
        void OnDisable()
        {
            if(subscribed){RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;subscribed=false;}
            while(renders.Count>0)renders.Pop().Restore();
            if(volume)volume.enabled=false;
            if(planar)planar.enabled=false;
            foreach(var probe in probes)if(probe)probe.enabled=false;
            foreach(var addition in additions)if(addition)addition.SetActive(false);
        }
        void OnEnable()
        {
            if(!Prepared)return;Subscribe();if(volume)volume.enabled=true;
            if(planar)planar.enabled=true;
            foreach(var probe in probes)if(probe)probe.enabled=true;
            foreach(var addition in additions)if(addition)addition.SetActive(true);
        }
        void OnDestroy()
        {
            OnDisable();Prepared=false;
            if(planar)GraphicsSurfaceLibrary.DestroyOwned(planar);planar=null;
            foreach(var addition in additions)if(addition)GraphicsSurfaceLibrary.DestroyOwned(addition);
            additions.Clear();windowLights.Clear();probes.Clear();
            if(root)GraphicsSurfaceLibrary.DestroyOwned(root.gameObject);
            foreach(var item in owned)GraphicsSurfaceLibrary.DestroyOwned(item);owned.Clear();
        }
    }
}
