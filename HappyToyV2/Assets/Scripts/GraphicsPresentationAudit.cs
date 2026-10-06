using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Opt-in controlled CAMERA/material review. This explicitly freezes actors
    // and places observers; it never represents a survival/input/performance gate.
    public sealed class GraphicsPresentationAudit : MonoBehaviour
    {
        string output, failure;Transform captureSubject;
        readonly List<string> errors = new List<string>();
        readonly List<FrameProof> frames = new List<FrameProof>();
        [Serializable] sealed class FrameProof
        {
            public string subject,subjectPath,supportFloor;public int supportFloorLayer;public float supportFloorY;public bool subjectInsideCorridor;
            public string file, mode, graphicsDevice, pipeline, captureFormat, projectColorSpace, sourceCameraType, captureCameraType, antialiasing, toneMapping, reflectionTexture;
            public Vector3 eye, look, reflectionCenter, reflectionSize;
            public ActorProof actor;public int visibleLitCandles;
            public int renderers, triangles, candles, litCandles, shadowLights, shadowFaces, sourceCameraStack, shadowAtlas, torchTile, localTile, reflectionCaptures, reflectionTextureWidth;
            public long selectedShadowPixels, observedShadowPixels;
            public bool postProcessing, hdrCamera, hdrPipeline, captureSrgb, reflectionReady, bloomActive, gpuTimingFeatureEnabled;
            public float ambientIntensity, meanRed, meanGreen, meanBlue, bloomIntensity, reflectionSettleSeconds;
        }
        [Serializable] sealed class ActorProof
        {
            public string name,clip,floor;
            public bool motionEnabled,refinedVisual,agentEnabled,agentOnNavMesh,floorObserved;
            public float clipTime,motionGroundGap,limbContactGap,agentBaseOffset,rootY,floorY,bakedWithoutScaleWorldMinY,bakedWithScaleWorldMinY,timeScale;
            public Vector3 modelLocalPosition;
            public string skinMeasurement="Independent BakeMesh(false)+TransformPoint world minimum and BakeMesh(true)+TransformPoint production-convention minimum; minima of all skin vertices, not a claim that every foot/hand touches the floor.";
        }
        [Serializable] sealed class Result
        {
            public string status, failure, unity, device;
            public string scope="Controlled native art review of actual runtime worlds/materials with supported camera positions. Actors are explicitly frozen and observer poses set; no survival, input route, resource balance, device listening, human fear or hardware FPS certification.";
            public string captureEncoding="Linear ARGBHalf HDR rendering and matching RGBAHalf CPU readback. Already tone-mapped linear pixels are explicitly encoded to sRGB, clamped only for 8-bit SDR PNG retention. These PNGs do not certify an HDR display or hardware FPS. Per-pose probe settling uses natural frames and existing three-second production cadence.";
            public string[] errors; public FrameProof[] frames;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v3-graphics-output");
            if(i<0 || i+1>=args.Length || FindFirstObjectByType<GraphicsPresentationAudit>())return;
            var go=new GameObject("Explicit controlled native graphics review");
            DontDestroyOnLoad(go);go.AddComponent<GraphicsPresentationAudit>().output=Path.GetFullPath(args[i+1]);
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string trace,LogType kind)
        {if((kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)&&errors.Count<64)errors.Add(message);}
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);float auditStarted=Time.realtimeSinceStartup;
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0 && string.IsNullOrEmpty(failure))
            {
                var iterator=stack.Peek();bool more=false;object current=null;
                try{more=iterator.MoveNext();if(more)current=iterator.Current;}
                catch(Exception e){failure=e.ToString();}
                if(!string.IsNullOrEmpty(failure))break;
                if(!more){stack.Pop();(iterator as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current;
                if(Time.realtimeSinceStartup-auditStarted>180)failure="Controlled art review exceeded the 180-second wall deadline";
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            if(errors.Count>0 && string.IsNullOrEmpty(failure))failure="Runtime errors during controlled art review";
            var result=new Result{status=string.IsNullOrEmpty(failure)?"PASS":"FAIL",failure=failure,
                unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,errors=errors.ToArray(),frames=frames.ToArray()};
            File.WriteAllText(Path.Combine(output,"graphics-review.json"),JsonUtility.ToJson(result,true));
            Debug.Log("HAPPYTOY_GRAPHICS_CAMERA_REVIEW_"+result.status);Application.Quit(result.status=="PASS"?0:2);
        }
        IEnumerator Run()
        {
            yield return null;yield return null;
            var session=GameSession.Current;
            Require(session && session.Shell,"Session unavailable");
            Require(QualitySettings.activeColorSpace==ColorSpace.Linear,"Controlled HDR readback requires the authored Linear project colour space");
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile",Guid.NewGuid().ToString("N")));
            session.CreateCorridor(73);session.Shell.Begin();
            yield return new WaitForSecondsRealtime(1.2f);Freeze(session);
            Require(session.Corridor.Presentation,"Actual corridor world unavailable");
            var corridorWorld=session.Corridor.Presentation.transform;
            var corridorItems=CorridorItems(session);
            var corridorCandles=corridorWorld.GetComponentsInChildren<WaymarkCandle>(true);
            var candle=corridorCandles.OrderBy(x=>x.GetComponent<Interactable>().stableId,StringComparer.Ordinal).First();
            session.player.flashlight.enabled=true;
            yield return Capture(session,"corridor-candle-unlit",new Vector3(199.25f,.08f,198.82f),candle.transform.position+Vector3.up*.12f);
            Require(candle.TryIgnite(session.player),"Candle actual ignition failed");
            yield return null;session.player.flashlight.enabled=false;
            yield return Capture(session,"corridor-candle-lit",new Vector3(199.25f,.08f,198.82f),candle.transform.position+Vector3.up*.12f);
            yield return CaptureTarget(session,"corridor-candle-close",candle.transform.position+Vector3.up*.13f,.72f,true,candle.transform);
            var battery=corridorItems.First(x=>x.kind==Interactable.Kind.FlashlightBattery);
            yield return CaptureTarget(session,"corridor-battery-close",battery.transform.position+Vector3.up*.1f,.85f,true,battery.transform);
            var door=corridorItems.First(x=>x.kind==Interactable.Kind.Door);
            yield return CaptureTarget(session,"corridor-door-joinery",DoorLeafAim(door),2.5f,true,door.transform);
            var lamp=corridorWorld.GetComponentsInChildren<Light>().Where(x=>x.name=="Corridor lamp").OrderBy(x=>x.transform.position.x).ThenBy(x=>x.transform.position.z).First();
            yield return CaptureTarget(session,"corridor-lantern",lamp.transform.position-Vector3.up*.14f,2,true,lamp.transform);
            yield return Capture(session,"corridor-floor-ceiling",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
            var cabinet=corridorItems.First(x=>x.kind==Interactable.Kind.HidingPlace);
            yield return CaptureTarget(session,"corridor-cabinet",cabinet.transform.position+Vector3.up*.7f,2.7f,true,cabinet.transform);
            var memory=corridorItems.First(x=>x.kind==Interactable.Kind.CorridorMemory);
            yield return CaptureTarget(session,"corridor-seal-room",memory.transform.position,2.4f,true,memory.transform);
            foreach(var mark in corridorCandles)mark.Restore(true);
            session.player.flashlight.enabled=false;
            yield return Capture(session,"corridor-many-candles",new Vector3(202,.08f,247.8f),new Vector3(202.8f,1.25f,242.2f));
            int enemyIndex=0;
            foreach(var actor in corridorWorld.GetComponentsInChildren<StalkerBrain>(true)
                .Where(x=>x.gameObject.scene==session.gameObject.scene&&x.corridorRole!=CorridorThreatRole.Authored&&x.GetComponentInChildren<SkinnedMeshRenderer>(true))
                .OrderBy(x=>(int)x.corridorRole).ToArray())
            {
                actor.gameObject.SetActive(true);actor.enabled=false;
                var agent=actor.GetComponent<NavMeshAgent>();
                var at=session.Corridor.CellPosition(0)+new Vector3(0,0,-1);
                if(agent && agent.enabled && agent.isOnNavMesh){agent.Warp(at);agent.isStopped=true;}
                else actor.transform.position=at;
                actor.transform.rotation=Quaternion.identity;
                yield return null;yield return null;
                session.player.flashlight.enabled=true;
                captureSubject=actor.transform;
                yield return Capture(session,"corridor-enemy-"+(++enemyIndex),at+new Vector3(0,0,2.4f),at+Vector3.up*.95f);
                captureSubject=null;actor.gameObject.SetActive(false);
            }
            // Fresh scene removes the complete corridor ownership tree.
            var load=SceneManager.LoadSceneAsync("SchoolAnnex",LoadSceneMode.Single);
            Require(load!=null,"School reload unavailable");yield return load;
            yield return null;yield return null;
            session=GameSession.Current;
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-school",Guid.NewGuid().ToString("N")));
            session.Shell.BeginChapter();yield return new WaitForSecondsRealtime(1.2f);Freeze(session);
            foreach(var mark in FindObjectsByType<WaymarkCandle>(FindObjectsSortMode.None))mark.Restore(true);
            yield return Capture(session,"school-ground-hall",new Vector3(0,.08f,0),new Vector3(18,1.5f,0));
            yield return Capture(session,"school-washroom",new Vector3(-5.65f,.08f,-2.15f),new Vector3(-3.65f,1.05f,-4.3f));
            yield return Capture(session,"school-music",new Vector3(24,.08f,11),new Vector3(23.4f,1,13.4f));
            yield return Capture(session,"school-upper",new Vector3(33,5.08f,23),new Vector3(35,6.4f,31));
            yield return Capture(session,"school-basement",new Vector3(14,-4.92f,-23),new Vector3(15,-3.6f,-28));
            var schoolCandle=FindObjectsByType<WaymarkCandle>(FindObjectsSortMode.None).First();
            yield return CaptureTarget(session,"school-candle-close",schoolCandle.transform.position+Vector3.up*.13f,.72f,true,schoolCandle.transform);
            Require(enemyIndex==4,"Expected the four actual corridor clone models");
            Require(frames.Count==20,"Controlled views incomplete: expected exactly 20, got "+frames.Count);
        }
        void Freeze(GameSession session)
        {
            session.player.enabled=false;
            foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))actor.enabled=false;
            foreach(var actor in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(actor is AnnexEncounter||actor is LanternMaskEncounter||actor is WeepingAngelEncounter||actor is StoryDirector||actor is V1HwacatEvent||actor is UncatAnnexEvent)actor.enabled=false;
            foreach(var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))if(agent.isOnNavMesh)agent.isStopped=true;
        }
        IEnumerator CaptureTarget(GameSession session,string name,Vector3 target,float radius,bool torch,Transform subject=null)
        {
            for(int i=0;i<24;i++)
            {
                if(subject&&session.CorridorMode)Require(subject.IsChildOf(session.Corridor.Presentation.transform),"Capture subject outside actual corridor world: "+name);
                float angle=(i+.5f)*Mathf.PI/12;
                var position=target+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                position.y=target.y-1.1f;
                if(!NavMesh.SamplePosition(position,out var hit,2,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-position.y)>1.8f)continue;
                var eye=hit.position+Vector3.up*1.6f;
                if(Physics.Linecast(eye,target,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                    Vector3.Distance(obstruction.point,target)>.3f &&
                    !obstruction.collider.GetComponentInParent<Interactable>() &&
                    !obstruction.collider.GetComponentInParent<StalkerBrain>())continue;
                session.player.flashlight.enabled=torch;captureSubject=subject;yield return Capture(session,name,hit.position,target);captureSubject=null;yield break;
            }
            throw new InvalidOperationException("No supported camera for "+name);
        }
        IEnumerator Capture(GameSession session,string name,Vector3 feet,Vector3 look)
        {
            Require(NavMesh.SamplePosition(feet,out var support,1.2f,NavMesh.AllAreas)&&Mathf.Abs(support.position.y-feet.y)<.7f,"Unsupported observer "+name);
            var player=session.player;player.transform.position=support.position;
            var yaw=look-player.transform.position;yaw.y=0;if(yaw.sqrMagnitude>.001f)player.transform.rotation=Quaternion.LookRotation(yaw);
            player.eyes.transform.rotation=Quaternion.LookRotation(look-player.eyes.transform.position);
            Physics.SyncTransforms();
            var graphics=session.GetComponent<GraphicsLightingPresentation>();
            Require(graphics&&graphics.Prepared,"Production lighting scope unavailable for "+name);
            float settlingStarted=Time.realtimeSinceStartup,deadline=settlingStarted+9;
            // Moving the observer must allow the production LateUpdate to select its
            // real room/cell and finish time-sliced faces through its existing cadence.
            yield return new WaitForSecondsRealtime(3.1f);
            yield return null;yield return null;
            while(!graphics.ReflectionReady)
            {Require(Time.realtimeSinceStartup<deadline,"Local reflection did not finish for "+name);yield return null;}
            session.GetComponent<LocalShadowBudget>()?.RefreshNow();
            var sourceData=player.eyes.GetUniversalAdditionalCameraData();
            Require(sourceData.renderType==CameraRenderType.Base&&sourceData.cameraStack.Count==0,"Controlled single-camera review cannot omit an authored overlay stack");
            var go=new GameObject("Controlled graphics camera");var camera=go.AddComponent<Camera>();
            camera.CopyFrom(player.eyes);camera.transform.SetPositionAndRotation(player.eyes.transform.position,player.eyes.transform.rotation);
            camera.enabled=false;camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=sourceData.renderPostProcessing;data.volumeLayerMask=sourceData.volumeLayerMask;
            data.volumeTrigger=camera.transform;data.antialiasing=sourceData.antialiasing;data.antialiasingQuality=sourceData.antialiasingQuality;
            data.renderShadows=sourceData.renderShadows;data.requiresDepthOption=sourceData.requiresDepthOption;data.requiresColorOption=sourceData.requiresColorOption;
            data.stopNaN=sourceData.stopNaN;data.dithering=sourceData.dithering;
            // URP17.6 inherits an external target's format for intermediate colour.
            // Keep floating-point HDR here; sRGB encoding happens after tone mapping.
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);rt.Create();
            var old=RenderTexture.active;Texture2D linear=null,texture=null;
            try
            {
                Require(rt.IsCreated()&&!rt.sRGB,"Linear HDR capture target unavailable");
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;linear=new Texture2D(1600,900,TextureFormat.RGBAHalf,false,true);
                linear.ReadPixels(new Rect(0,0,1600,900),0,0);linear.Apply();
                var pixels=EncodeToneMappedLinearToSrgb(linear.GetPixels());
                texture=new Texture2D(1600,900,TextureFormat.RGB24,false,false);texture.SetPixels32(pixels);texture.Apply();
                long r=0,g=0,b=0;foreach(var p in pixels){r+=p.r;g+=p.g;b+=p.b;}
                Require(r+g+b>pixels.Length*3L,"Blank camera "+name);
                File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
                var lights=FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.gameObject.scene==session.gameObject.scene&&x.isActiveAndEnabled&&x.intensity>.01f&&x.shadows!=LightShadows.None&&(x.type==LightType.Point||x.type==LightType.Spot)).ToArray();
                var budget=session.GetComponent<LocalShadowBudget>();var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Require(budget&&pipeline,"Native owned pipeline/shadow selector unavailable");
                long observedPixels=0;
                foreach(var light in lights)
                {
                    var lightData=light.GetComponent<UniversalAdditionalLightData>();Require(lightData,"Actual punctual shadow caster outside the complete budget");
                    int tier=lightData.additionalLightsShadowResolutionTier;
                    int tile=tier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh?pipeline.additionalLightsShadowResolutionTierHigh:
                        tier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium?pipeline.additionalLightsShadowResolutionTierMedium:pipeline.additionalLightsShadowResolutionTierLow;
                    observedPixels+=(long)tile*tile*(light.type==LightType.Point?6:1);
                }
                Require(observedPixels==budget.RequestedAtlasPixels&&observedPixels<=(long)budget.AtlasResolution*budget.AtlasResolution,"Observed punctual caster tile area differs from the atlas plan");
                var probe=graphics.OwnedProbe;Require(probe&&probe.texture,"Native local reflection texture unavailable");
                int visibleCandles=VisibleLitCandles(camera,session);
                if(name=="corridor-many-candles")Require(visibleCandles>=2,"Many-candle fixture must actually show two unobstructed lit flames");
                if(name=="school-washroom")
                {
                    var placement=GraphicsLightingPresentation.PlanSchoolReflection(player.transform.position);
                    Require(placement.Floor&&placement.Floor.name.ToLowerInvariant().Contains("washroom"),"Washroom observer is outside the actual tiled floor");
                }
                var supportPlan=GraphicsLightingPresentation.PlanSchoolReflection(player.transform.position);
                var stack=VolumeManager.instance.stack;var bloom=stack.GetComponent<Bloom>();var tone=stack.GetComponent<Tonemapping>();
                frames.Add(new FrameProof{actor=ObserveActor(captureSubject),file=name+".png",mode=session.CorridorMode?"corridor":"school",eye=camera.transform.position,look=look,
                    subject=captureSubject?captureSubject.name:null,subjectPath=captureSubject?HierarchyPath(captureSubject):null,
                    subjectInsideCorridor=captureSubject&&session.CorridorMode&&captureSubject.IsChildOf(session.Corridor.Presentation.transform),visibleLitCandles=visibleCandles,
                    supportFloor=supportPlan.Floor?supportPlan.Floor.name:null,supportFloorLayer=supportPlan.Floor?supportPlan.Floor.gameObject.layer:-1,supportFloorY=supportPlan.SupportY,
                    graphicsDevice=SystemInfo.graphicsDeviceName,pipeline=pipeline.name,captureFormat=rt.graphicsFormat.ToString(),projectColorSpace=QualitySettings.activeColorSpace.ToString(),
                    sourceCameraType=sourceData.renderType.ToString(),captureCameraType=data.renderType.ToString(),sourceCameraStack=sourceData.cameraStack.Count,antialiasing=data.antialiasing.ToString(),
                    hdrCamera=camera.allowHDR,hdrPipeline=pipeline.supportsHDR,captureSrgb=rt.sRGB,postProcessing=data.renderPostProcessing,
                    toneMapping=tone.mode.value.ToString(),bloomActive=bloom.IsActive(),bloomIntensity=bloom.intensity.value,
                    gpuTimingFeatureEnabled=FrameTimingManager.IsFeatureEnabled(),
                    shadowAtlas=budget.AtlasResolution,torchTile=budget.TorchTileResolution,localTile=budget.LocalTileResolution,
                    selectedShadowPixels=budget.RequestedAtlasPixels,observedShadowPixels=observedPixels,
                    reflectionReady=graphics.ReflectionReady,reflectionCenter=probe.transform.position,reflectionSize=probe.size,reflectionCaptures=graphics.ReflectionCaptures,
                    reflectionTexture=probe.texture.name,reflectionTextureWidth=probe.texture.width,reflectionSettleSeconds=Time.realtimeSinceStartup-settlingStarted,
                    renderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length,
                    triangles=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(x=>x.sharedMesh).Sum(x=>
                        Enumerable.Range(0,x.sharedMesh.subMeshCount).Sum(s=>(int)x.sharedMesh.GetIndexCount(s)/3)),
                    candles=FindObjectsByType<WaymarkCandle>(FindObjectsSortMode.None).Length,litCandles=FindObjectsByType<WaymarkCandle>(FindObjectsSortMode.None).Count(x=>x.Lit),
                    shadowLights=lights.Length,shadowFaces=lights.Sum(x=>x.type==LightType.Point?6:1),ambientIntensity=RenderSettings.ambientIntensity,
                    meanRed=r/(float)pixels.Length,meanGreen=g/(float)pixels.Length,meanBlue=b/(float)pixels.Length});
            }
            finally{RenderTexture.active=old;rt.Release();Destroy(rt);if(linear)Destroy(linear);if(texture)Destroy(texture);Destroy(go);}
        }
        static ActorProof ObserveActor(Transform subject)
        {
            var actor=subject?subject.GetComponent<StalkerBrain>():null;if(!actor)return null;
            var motion=actor.GetComponent<V1MonsterMotion>();var agent=actor.GetComponent<NavMeshAgent>();
            var result=new ActorProof{name=actor.name,rootY=actor.transform.position.y,timeScale=Time.timeScale,
                motionEnabled=motion&&motion.enabled,refinedVisual=motion&&motion.RefinedVisual,clip=motion?motion.CurrentClip:null,
                clipTime=motion?motion.ClipTime:0,motionGroundGap=motion?motion.GroundGap:0,limbContactGap=motion?motion.LimbContactGap:0,
                modelLocalPosition=motion&&motion.model?motion.model.localPosition:Vector3.zero,
                agentEnabled=agent&&agent.enabled,agentOnNavMesh=agent&&agent.enabled&&agent.isOnNavMesh,agentBaseOffset=agent?agent.baseOffset:0};
            if(Physics.Raycast(actor.transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {result.floorObserved=true;result.floor=floor.collider.name;result.floorY=floor.point.y;}
            var baked=new Mesh{name="Owned controlled art skin sample"};float without=float.PositiveInfinity,with=float.PositiveInfinity;
            try
            {
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.BakeMesh(baked,false);foreach(var vertex in baked.vertices)without=Mathf.Min(without,skin.transform.TransformPoint(vertex).y);
                    skin.BakeMesh(baked,true);foreach(var vertex in baked.vertices)with=Mathf.Min(with,skin.transform.TransformPoint(vertex).y);
                }
                Require(StealthRules.Finite(without)&&StealthRules.Finite(with),"Actual actor skin sample empty/nonfinite");
                result.bakedWithoutScaleWorldMinY=without;result.bakedWithScaleWorldMinY=with;
            }
            finally{Destroy(baked);}
            return result;
        }
        public static Interactable[] CorridorItems(GameSession session)
        {
            Require(session&&session.CorridorMode&&session.Corridor.Presentation,"Actual corridor world unavailable for subjects");
            return session.Corridor.Presentation.GetComponentsInChildren<Interactable>(true).Where(x=>x.gameObject.activeInHierarchy)
                .OrderBy(x=>x.stableId,StringComparer.Ordinal).ThenBy(x=>x.transform.position.x).ThenBy(x=>x.transform.position.z).ToArray();
        }
        public static Vector3 DoorLeafAim(Interactable door)
        {
            Require(door&&door.movingLeaf,"Capture door lacks its actual moving leaf");
            var renderers=door.movingLeaf.GetComponentsInChildren<Renderer>(true).Where(x=>x.enabled&&x.gameObject.activeInHierarchy).ToArray();
            Require(renderers.Length>0,"Capture door leaf has no active visual bounds");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            return bounds.center;
        }
        static string HierarchyPath(Transform node)
        {return node.parent?HierarchyPath(node.parent)+"/"+node.name:node.name;}
        static int VisibleLitCandles(Camera camera,GameSession session)
        {
            var lighting=session.CorridorMode?session.Corridor.Lighting:session.Chapter.Lighting;int count=0;
            foreach(var mark in lighting.Candles)
            {
                if(!mark||!mark.Lit||!mark.Flame||!mark.Flame.gameObject.activeInHierarchy)continue;
                var point=mark.Flame.position+Vector3.up*.02f;var screen=camera.WorldToViewportPoint(point);
                if(screen.z<=0||screen.x<0||screen.x>1||screen.y<0||screen.y>1)continue;
                if(Physics.Linecast(camera.transform.position,point,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&obstruction.collider.GetComponentInParent<WaymarkCandle>()!=mark)continue;
                count++;
            }
            return count;
        }
        public static Color32[] EncodeToneMappedLinearToSrgb(Color[] linear)
        {
            if(linear==null)throw new ArgumentNullException(nameof(linear));
            var encoded=new Color32[linear.Length];
            for(int i=0;i<linear.Length;i++)
            {
                var value=linear[i];
                if(!StealthRules.Finite(value.r)||!StealthRules.Finite(value.g)||!StealthRules.Finite(value.b))
                    throw new InvalidOperationException("Non-finite native HDR camera pixel");
                encoded[i]=new Color(Mathf.Clamp01(Mathf.LinearToGammaSpace(value.r)),Mathf.Clamp01(Mathf.LinearToGammaSpace(value.g)),Mathf.Clamp01(Mathf.LinearToGammaSpace(value.b)),1);
            }
            return encoded;
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
