using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // A reversible mode scope. It owns only its profile, cookie, probe and empty
    // render-only root; authored volumes, materials and imported resources survive.
    [DisallowMultipleComponent,DefaultExecutionOrder(210)]
    public sealed class GraphicsLightingPresentation : MonoBehaviour
    {
        static GraphicsLightingPresentation renderOwner;
        static readonly Color Bounce=new Color(.058f,.065f,.072f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwnership(){renderOwner=null;}
        struct LightState
        {
            public Light light;public Color color;public float intensity,range,temperature,spot,inner,near;
            public bool useTemperature;public Texture cookie;
        }
        readonly List<LightState> originals=new List<LightState>();
        GameSession session;Behaviour modeOwner;Camera camera;UniversalAdditionalCameraData cameraData;
        bool captured,applied,addedCameraData,oldHDR,oldMSAA,oldPost;
        LayerMask oldVolumeMask;AntialiasingMode oldAA;AntialiasingQuality oldAAQuality;
        CameraOverrideOption oldDepth;
        AmbientMode oldAmbientMode;Color oldAmbient,oldSky,oldEquator,oldGround;
        float oldAmbientIntensity,oldReflectionIntensity;int oldReflectionBounces;
        bool oldFog;FogMode oldFogMode;Color oldFogColor;float oldFogDensity,oldFogStart,oldFogEnd;
        Transform root;Volume volume;VolumeProfile profile;Texture2D cookie;ReflectionProbe probe;
        int probeRenderId=-1,lastCell=-1,lastSchoolX=int.MinValue,lastSchoolZ=int.MinValue,lastLitCandles=-1;float nextProbe,lastSchoolY=float.NaN;
        EntityId lastSchoolFloor=EntityId.None;bool lastTorchLit;
        public bool Prepared=>applied;
        public Volume OwnedVolume=>volume;
        public ReflectionProbe OwnedProbe=>probe;
        public int ReflectionCaptures {get;private set;}
        public bool ReflectionReady=>probe&&probeRenderId>=0&&probe.IsFinishedRendering(probeRenderId);
        public static void BeginTransition(GameSession owner)
        {
            var scope=owner.GetComponent<GraphicsLightingPresentation>();
            if(!scope)scope=owner.gameObject.AddComponent<GraphicsLightingPresentation>();
            scope.End();scope.session=owner;scope.enabled=true;scope.Capture();
        }
        public static void BeginMode(GameSession owner,Transform modeRoot)
        {
            var scope=owner.GetComponent<GraphicsLightingPresentation>();
            if(!scope) {BeginTransition(owner);scope=owner.GetComponent<GraphicsLightingPresentation>();}
            if(!scope.captured)scope.Capture();
            scope.Apply(modeRoot);
        }
        public static void EndMode(GameSession owner)
        {var scope=owner?owner.GetComponent<GraphicsLightingPresentation>():null;if(scope)scope.End();}
        void Capture()
        {
            camera=session.player.eyes;
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(light.gameObject.scene==gameObject.scene)originals.Add(new LightState {light=light,color=light.color,intensity=light.intensity,
                    range=light.range,temperature=light.colorTemperature,useTemperature=light.useColorTemperature,
                    spot=light.spotAngle,inner=light.innerSpotAngle,cookie=light.cookie,near=light.shadowNearPlane});
            oldHDR=camera.allowHDR;oldMSAA=camera.allowMSAA;
            addedCameraData=!camera.TryGetComponent<UniversalAdditionalCameraData>(out cameraData);
            if(addedCameraData)cameraData=camera.GetUniversalAdditionalCameraData();
            oldPost=cameraData.renderPostProcessing;oldVolumeMask=cameraData.volumeLayerMask;
            oldAA=cameraData.antialiasing;oldAAQuality=cameraData.antialiasingQuality;oldDepth=cameraData.requiresDepthOption;
            oldAmbientMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;
            oldSky=RenderSettings.ambientSkyColor;oldEquator=RenderSettings.ambientEquatorColor;oldGround=RenderSettings.ambientGroundColor;
            oldAmbientIntensity=RenderSettings.ambientIntensity;oldReflectionIntensity=RenderSettings.reflectionIntensity;
            oldReflectionBounces=RenderSettings.reflectionBounces;oldFog=RenderSettings.fog;oldFogMode=RenderSettings.fogMode;
            oldFogColor=RenderSettings.fogColor;oldFogDensity=RenderSettings.fogDensity;oldFogStart=RenderSettings.fogStartDistance;oldFogEnd=RenderSettings.fogEndDistance;
            captured=true;
        }
        void Apply(Transform modeRoot)
        {
            if(applied)throw new System.InvalidOperationException("Lighting mode already applied");
            modeOwner=session.CorridorMode?(Behaviour)session.Corridor:session.ChapterMode?(Behaviour)session.Chapter:null;
            root=new GameObject("Owned graphics light and reflection scope").transform;root.SetParent(modeRoot,false);
            profile=CreateProfile();
            volume=root.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=50;volume.weight=1;volume.sharedProfile=profile;
            camera.allowHDR=true;camera.allowMSAA=false;cameraData.renderPostProcessing=true;
            cameraData.volumeLayerMask=oldVolumeMask.value|1;cameraData.requiresDepthOption=CameraOverrideOption.On;
            cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cameraData.antialiasingQuality=AntialiasingQuality.Medium;
            // Low neutral bounce retains silhouettes and colour; local emitters provide contrast.
            renderOwner=this;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Bounce;
            RenderSettings.ambientIntensity=.65f;RenderSettings.reflectionIntensity=.6f;RenderSettings.reflectionBounces=1;
            var torch=session.player.flashlight;
            if(torch)
            {
                cookie=TorchCookie();torch.cookie=cookie;torch.useColorTemperature=true;torch.colorTemperature=4900;
                torch.color=Color.white;torch.intensity=5;torch.range=18;torch.spotAngle=52;torch.innerSpotAngle=22;torch.shadowNearPlane=.05f;
                // enabled/charge belong exclusively to PlayerFlashlight.
            }
            var probeObject=new GameObject("Local room reflection (128 HDR, time sliced)");probeObject.transform.SetParent(root,false);
            probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;
            probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution=128;probe.hdr=true;probe.boxProjection=true;probe.blendDistance=.65f;probe.intensity=.6f;
            probe.importance=50;probe.cullingMask=camera.cullingMask;probe.shadowDistance=16;probe.nearClipPlane=.08f;probe.farClipPlane=24;
            applied=true;lastCell=-1;lastSchoolX=lastSchoolZ=int.MinValue;lastSchoolFloor=EntityId.None;lastSchoolY=float.NaN;
            lastLitCandles=-1;lastTorchLit=false;nextProbe=0;ReflectionCaptures=0;
            var budget=session.GetComponent<LocalShadowBudget>();if(budget)budget.RefreshNow();
            PositionProbe(true);
        }
        public static VolumeProfile CreateProfile()
        {
            var result=ScriptableObject.CreateInstance<VolumeProfile>();result.name="Owned moderate horror HDR profile";
            var bloom=result.Add<Bloom>(true);bloom.threshold.value=1.1f;bloom.intensity.value=.16f;bloom.scatter.value=.55f;
            bloom.clamp.value=12;bloom.highQualityFiltering.value=false;bloom.maxIterations.value=4;
            bloom.dirtTexture.value=null;bloom.dirtIntensity.value=0;
            var tone=result.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
            var grade=result.Add<ColorAdjustments>(true);grade.postExposure.value=.1f;grade.contrast.value=3;grade.saturation.value=-3;
            // Static exposure only: no camera shake, blur, grain, vignette or adaptation flicker.
            return result;
        }
        static Texture2D TorchCookie()
        {
            const int side=128;var texture=new Texture2D(side,side,TextureFormat.RGBA32,false,true) {name="Owned soft lens transmission",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[side*side];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                float u=(x+.5f)/side*2-1,v=(y+.5f)/side*2-1,r=Mathf.Sqrt(u*u+v*v);
                float transmission=Mathf.SmoothStep(1,0,Mathf.InverseLerp(.55f,1,r));
                transmission*=.96f-.018f*Mathf.Sin(x*.27f)*Mathf.Sin(y*.19f);
                pixels[y*side+x]=new Color(transmission,transmission,transmission,1);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        void LateUpdate()
        {
            if(!applied)return;
            if(!session||!modeOwner||!modeOwner.isActiveAndEnabled){End();return;}
            // Static grading also respects the comfort contract: motion effects are absent.
            if(session.InputAllowed)PositionProbe(false);
        }
        void PositionProbe(bool force)
        {
            if(!probe||(!force&&Time.unscaledTime<nextProbe))return;
            if(probeRenderId>=0&&!probe.IsFinishedRendering(probeRenderId))return;
            var lighting=session.CorridorMode?session.Corridor.Lighting:session.Chapter.Lighting;
            int litCandles=0;if(lighting)foreach(var mark in lighting.Candles)if(mark&&mark.Lit)litCandles++;
            var torch=session.player.flashlight;bool torchLit=torch&&torch.isActiveAndEnabled&&torch.intensity>.01f;
            bool lightChanged=litCandles!=lastLitCandles||torchLit!=lastTorchLit;
            Vector3 center;Vector3 size;
            if(session.CorridorMode)
            {
                int cell=0;float best=float.PositiveInfinity;
                for(int i=0;i<CorridorLayout.Count;i++)
                {float d=(session.Corridor.CellPosition(i)-session.player.transform.position).sqrMagnitude;if(d<best){best=d;cell=i;}}
                if(!force&&!lightChanged&&cell==lastCell)return;lastCell=cell;
                center=session.Corridor.CellPosition(cell)+Vector3.up*1.5f;size=new Vector3(5.5f,3,5.5f);
            }
            else
            {
                var placement=PlanSchoolReflection(session.player.transform.position);
                EntityId floorId=placement.Floor?placement.Floor.GetEntityId():EntityId.None;
                if(!force&&!lightChanged&&floorId==lastSchoolFloor&&placement.CellX==lastSchoolX&&placement.CellZ==lastSchoolZ&&Mathf.Abs(placement.SupportY-lastSchoolY)<.35f)return;
                lastSchoolFloor=floorId;lastSchoolX=placement.CellX;lastSchoolZ=placement.CellZ;lastSchoolY=placement.SupportY;
                center=placement.Box.center;size=placement.Box.size;
            }
            lastLitCandles=litCandles;lastTorchLit=torchLit;
            probe.transform.position=center;probe.size=size;probe.center=Vector3.zero;
            probeRenderId=probe.RenderProbe();ReflectionCaptures++;nextProbe=Time.unscaledTime+3;
        }
        public readonly struct SchoolReflectionPlacement
        {
            public readonly Collider Floor;public readonly Bounds Box;public readonly float SupportY;public readonly int CellX,CellZ;
            public SchoolReflectionPlacement(Collider floor,Bounds box,float supportY,Vector3 feet)
            {Floor=floor;Box=box;SupportY=supportY;CellX=Mathf.FloorToInt(feet.x/4);CellZ=Mathf.FloorToInt(feet.z/4);}
        }
        public static SchoolReflectionPlacement PlanSchoolReflection(Vector3 feet)
        {
            // The preserved school's actual support colliders are layer0, not the
            // door/item layer9. Query real nearby support, with no physics edits.
            var hits=Physics.RaycastAll(feet+Vector3.up*.4f,Vector3.down,1.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            RaycastHit support=default;float nearest=float.PositiveInfinity;
            foreach(var hit in hits)
            {
                string name=hit.collider.name.ToLowerInvariant();
                bool floor=name.Contains("stair tread")||name.Contains("floor")&&!name.Contains("wall")&&!name.Contains("back")&&!name.Contains("side");
                if(!floor||hit.normal.y<.8f||Mathf.Abs(hit.point.y-feet.y)>.8f||hit.distance>=nearest)continue;
                support=hit;nearest=hit.distance;
            }
            float supportY=support.collider?support.point.y:feet.y;
            var box=new Bounds(feet+Vector3.up*1.5f,new Vector3(7,3,7));
            if(support.collider)
            {
                var bounds=support.collider.bounds;var size=new Vector3(Mathf.Clamp(bounds.size.x-.1f,2,16),3,Mathf.Clamp(bounds.size.z-.1f,2,16));
                // Clamp the LOCAL centre near the observer inside a large authored
                // hall rather than using its potentially distant whole-floor centre.
                float x=bounds.size.x>=size.x?Mathf.Clamp(feet.x,bounds.min.x+size.x*.5f,bounds.max.x-size.x*.5f):feet.x;
                float z=bounds.size.z>=size.z?Mathf.Clamp(feet.z,bounds.min.z+size.z*.5f,bounds.max.z-size.z*.5f):feet.z;
                box=new Bounds(new Vector3(x,supportY+1.5f,z),size);
            }
            return new SchoolReflectionPlacement(support.collider,box,supportY,feet);
        }
        void End()
        {
            if(volume)volume.enabled=false;
            if(probe)probe.enabled=false;
            if(captured)
            {
                foreach(var state in originals)
                {
                    if(!state.light)continue;
                    state.light.color=state.color;state.light.intensity=state.intensity;state.light.range=state.range;
                    state.light.useColorTemperature=state.useTemperature;state.light.colorTemperature=state.temperature;
                    state.light.spotAngle=state.spot;state.light.innerSpotAngle=state.inner;state.light.cookie=state.cookie;state.light.shadowNearPlane=state.near;
                }
                if(camera){camera.allowHDR=oldHDR;camera.allowMSAA=oldMSAA;}
                if(cameraData)
                {
                    cameraData.renderPostProcessing=oldPost;cameraData.volumeLayerMask=oldVolumeMask;
                    cameraData.antialiasing=oldAA;cameraData.antialiasingQuality=oldAAQuality;cameraData.requiresDepthOption=oldDepth;
                    if(addedCameraData)DestroyImmediate(cameraData);
                }
                // Loading another scene installs its globals before all old callbacks end.
                // Restore only our active group, never overwrite a newer scene/mode owner.
                if(renderOwner==this&&RenderSettings.ambientMode==AmbientMode.Flat&&RenderSettings.ambientLight==Bounce)
                {
                    RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientLight=oldAmbient;
                    RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;
                    RenderSettings.ambientIntensity=oldAmbientIntensity;RenderSettings.reflectionIntensity=oldReflectionIntensity;
                    RenderSettings.reflectionBounces=oldReflectionBounces;RenderSettings.fog=oldFog;RenderSettings.fogMode=oldFogMode;
                    RenderSettings.fogColor=oldFogColor;RenderSettings.fogDensity=oldFogDensity;RenderSettings.fogStartDistance=oldFogStart;RenderSettings.fogEndDistance=oldFogEnd;
                }
            }
            if(root)Destroy(root.gameObject);
            if(profile){foreach(var component in profile.components)if(component)Destroy(component);Destroy(profile);}
            if(cookie)Destroy(cookie);
            originals.Clear();root=null;volume=null;probe=null;profile=null;cookie=null;modeOwner=null;cameraData=null;
            if(renderOwner==this)renderOwner=null;
            captured=applied=false;addedCameraData=false;probeRenderId=-1;
        }
        void OnDisable(){End();}
        void OnDestroy(){End();}
    }
}
