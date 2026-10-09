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
            public int renderers, triangles, candles, litCandles, shadowLights, shadowFaces, sourceCameraStack, shadowAtlas, torchTile, localTile, reflectionCaptures, reflectionTextureWidth, captureMsaa;
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
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v4-surface-diagnostic")>=0)
                result.scope="Seven-frame surface diagnosis, one presented-window screenshot and actual mesh triangle intersections. AO/shadow controls retain their release shader keywords and change only positive effect strength. Actors frozen; no complete art inventory, gameplay, survival, listening or FPS certification.";
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
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v4-surface-diagnostic")>=0)
            {
                yield return SurfaceDiagnostic(session);
                yield break;
            }
            var corridorWorld=session.Corridor.Presentation.transform;
            var floorEvidence=new System.Text.StringBuilder("Renderer\tEnabled\tMaterial\tBounds\tTopY\n");
            var sample=new Vector3(207,0,201);
            foreach(var filter in corridorWorld.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
                var bounds=renderer.bounds;
                if(bounds.min.x>sample.x||bounds.max.x<sample.x||bounds.min.z>sample.z||bounds.max.z<sample.z||bounds.min.y>.08f||bounds.max.y<-.1f)continue;
                float top=float.NegativeInfinity;
                foreach(var vertex in filter.sharedMesh.vertices)top=Mathf.Max(top,filter.transform.TransformPoint(vertex).y);
                floorEvidence.AppendLine(HierarchyPath(filter.transform)+"\t"+renderer.enabled+"\t"+renderer.sharedMaterial.name+"\t"+bounds+"\t"+top.ToString("R"));
            }
            File.WriteAllText(Path.Combine(output,"floor-geometry.tsv"),floorEvidence.ToString());
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
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var rendererField=typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var rendererData=rendererField?.GetValue(pipeline) as ScriptableRendererData[];
            Require(rendererData!=null&&rendererData.Length>0,"Owned renderer unavailable for art noise comparison");
            var ao=rendererData[0].rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            var settings=typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ao);
            var intensityField=settings.GetType().GetField("Intensity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            float originalIntensity=(float)intensityField.GetValue(settings);
            try
            {intensityField.SetValue(settings,.0001f);yield return Capture(session,"corridor-floor-ceiling-ao-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));}
            finally {intensityField.SetValue(settings,originalIntensity);}
            yield return ShadowStrengthControl(session,"corridor-floor-ceiling-shadow-control");
            var groups=corridorWorld.GetComponentsInChildren<LODGroup>(true);
            var lowRenderers=groups.SelectMany(x=>x.GetLODs().Skip(1)).SelectMany(x=>x.renderers).Where(x=>x).Distinct().ToArray();
            var lowEnabled=lowRenderers.Select(x=>x.enabled).ToArray();
            try
            {foreach(var group in groups)group.ForceLOD(0);foreach(var renderer in lowRenderers)renderer.enabled=false;
                yield return Capture(session,"corridor-floor-ceiling-lod-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));}
            finally {for(int i=0;i<lowRenderers.Length;i++)if(lowRenderers[i])lowRenderers[i].enabled=lowEnabled[i];foreach(var group in groups)if(group)group.ForceLOD(-1);}
            var neutral=new Material(Resources.Load<Material>("GraphicsPbr/wood-aged/material"));
            neutral.SetColor("_BaseColor",new Color(.55f,.55f,.55f));neutral.SetTexture("_BaseMap",Texture2D.whiteTexture);
            neutral.SetTexture("_BumpMap",null);neutral.DisableKeyword("_NORMALMAP");
            neutral.SetTexture("_OcclusionMap",null);neutral.DisableKeyword("_OCCLUSIONMAP");
            neutral.SetTexture("_MetallicGlossMap",null);neutral.DisableKeyword("_METALLICSPECGLOSSMAP");neutral.SetFloat("_Metallic",0);neutral.SetFloat("_Smoothness",.15f);
            var architecture=corridorWorld.GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.GetComponentInParent<LODGroup>()!=null&&x.GetComponentInParent<StalkerBrain>()==null).ToArray();
            var originals=architecture.Select(x=>x.sharedMaterials).ToArray();
            try
            {foreach(var renderer in architecture)renderer.sharedMaterial=neutral;yield return Capture(session,"corridor-floor-ceiling-material-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));}
            finally {for(int i=0;i<architecture.Length;i++)if(architecture[i])architecture[i].sharedMaterials=originals[i];Destroy(neutral);}
            var cabinet=corridorItems.First(x=>x.kind==Interactable.Kind.HidingPlace);
            yield return CaptureTarget(session,"corridor-cabinet",cabinet.transform.position+Vector3.up*.7f,2.7f,true,cabinet.transform);
            var memory=corridorItems.First(x=>x.kind==Interactable.Kind.CorridorMemory);
            yield return CaptureTarget(session,"corridor-seal-room",memory.transform.position,2.4f,true,memory.transform);
            var classroom=session.Corridor.AltarRoomRoot;
            Require(classroom&&session.Corridor.AltarChamber,"Final classroom unavailable for complete corridor art review");
            session.player.flashlight.enabled=true;
            yield return Capture(session,"corridor-final-classroom-entry",classroom.TransformPoint(new Vector3(0,.08f,-3.5f)),classroom.TransformPoint(new Vector3(0,1.8f,3.8f)));
            yield return Capture(session,"corridor-final-classroom-joinery",classroom.TransformPoint(new Vector3(-4.4f,.08f,1)),classroom.TransformPoint(new Vector3(-5.89f,1.6f,2.7f)));
            yield return CaptureTarget(session,"corridor-final-classroom-altar",session.Corridor.AltarChamber.OfferingAim,2.4f,true,session.Corridor.AltarChamber.Offering.transform);
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
            Require(frames.Count==27&&frames.Select(x=>x.file).Distinct(StringComparer.Ordinal).Count()==27,
                "Controlled views incomplete: expected 20 original views, 3 final classroom views and 4 rendering controls; got "+frames.Count);
        }
        IEnumerator SurfaceDiagnostic(GameSession session)
        {
            // A separate, explicitly narrow diagnostic. It does not replace the
            // complete chapter inventory or certify the production appearance.
            session.player.flashlight.enabled=true;
            yield return Capture(session,"surface-production",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"surface-presented.png"));
            yield return null;yield return null;
            var evidence=new System.Text.StringBuilder("Renderer\tEnabled\tPlaneY\tNormalY\n");
            var sample=new Vector3(207.043f,0,201.071f);
            foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
                if(!renderer||!mesh||!mesh.isReadable)continue;
                var bounds=renderer.bounds;
                if(sample.x<bounds.min.x||sample.x>bounds.max.x||sample.z<bounds.min.z||sample.z>bounds.max.z)continue;
                var vertices=mesh.vertices;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[indices[i]]);
                    var b=filter.transform.TransformPoint(vertices[indices[i+1]]);
                    var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                    float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(Mathf.Abs(determinant)<1e-9f)continue;
                    float u=((b.z-c.z)*(sample.x-c.x)+(c.x-b.x)*(sample.z-c.z))/determinant;
                    float v=((c.z-a.z)*(sample.x-c.x)+(a.x-c.x)*(sample.z-c.z))/determinant;
                    if(u<0||v<0||u+v>1)continue;
                    float y=u*a.y+v*b.y+(1-u-v)*c.y;
                    if(y<-.3f||y>3.3f)continue;
                    evidence.AppendLine(HierarchyPath(filter.transform)+"\t"+renderer.enabled+"\t"+y.ToString("R")+"\t"+Vector3.Cross(b-a,c-a).normalized.y.ToString("R"));
                }
            }
            File.WriteAllText(Path.Combine(output,"surface-triangle-intersections.tsv"),evidence.ToString());
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var rendererField=typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var rendererData=(ScriptableRendererData[])rendererField.GetValue(pipeline);
            var ao=rendererData[0].rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            var settings=typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ao);
            var intensityField=settings.GetType().GetField("Intensity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            float intensityBefore=(float)intensityField.GetValue(settings);
            try
            {intensityField.SetValue(settings,.0001f);yield return Capture(session,"surface-ao-strength-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));}
            finally {intensityField.SetValue(settings,intensityBefore);}
            yield return ShadowStrengthControl(session,"surface-shadow-strength-control");
            var architecture=session.Corridor.Presentation.GetComponentsInChildren<LODGroup>(true);
            var low=architecture.SelectMany(x=>x.GetLODs().Skip(1)).SelectMany(x=>x.renderers).Where(x=>x).Distinct().ToArray();
            var wasEnabled=low.Select(x=>x.enabled).ToArray();
            try
            {
                foreach(var group in architecture)group.ForceLOD(0);
                foreach(var renderer in low)renderer.enabled=false;
                yield return Capture(session,"surface-high-only",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
                // Reflectance is the remaining shared input after texture,
                // occlusion and shadow controls; omit only local probe radiance.
                var graphics=session.GetComponent<GraphicsLightingPresentation>();float intensity=graphics.OwnedProbe.intensity;
                float ambientReflection=RenderSettings.reflectionIntensity;
                try
                {
                    graphics.OwnedProbe.intensity=0;RenderSettings.reflectionIntensity=0;
                    yield return Capture(session,"surface-reflection-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
                }
                finally {graphics.OwnedProbe.intensity=intensity;RenderSettings.reflectionIntensity=ambientReflection;}
                var shader=Resources.Load<Shader>("GraphicsUpgrade/Shaders/SurfaceDiagnostic");
                Require(shader&&shader.isSupported,"Explicit uniform diagnostic shader unavailable");
                var renderers=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);var uniform=new List<Material>();
                var materials=renderers.Select(x=>x.sharedMaterials).ToArray();
                try
                {
                    for(int i=0;i<renderers.Length;i++)
                    {
                        // Distinct colours expose any interleaved coplanar renderers;
                        // smooth geometric normals expose mesh-only surface changes.
                        var material=new Material(shader);material.SetColor("_BaseColor",Color.HSVToRGB((i*.618034f)%1,.6f,.7f));uniform.Add(material);
                        renderers[i].sharedMaterials=Enumerable.Repeat(material,renderers[i].sharedMaterials.Length).ToArray();
                    }
                    yield return Capture(session,"surface-unlit-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
                }
                finally {for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=materials[i];foreach(var material in uniform)Destroy(material);}
                var matte=new Dictionary<Material,Material>();
                try
                {
                    foreach(var renderer in renderers)
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
                        {
                            if(!matte.TryGetValue(source,out var copy))
                            {
                                copy=new Material(source);matte.Add(source,copy);
                                // Keep the imported shader variants. Zero strengths
                                // do not depend on stripped keyword combinations.
                                copy.SetFloat("_Smoothness",0);copy.SetFloat("_BumpScale",0);copy.SetFloat("_OcclusionStrength",0);
                            }
                            return copy;
                        }).ToArray();
                    }
                    yield return Capture(session,"surface-matte-control",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
                }
                finally {for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=materials[i];foreach(var material in matte.Values)Destroy(material);}
            }
            finally {for(int i=0;i<low.Length;i++)if(low[i])low[i].enabled=wasEnabled[i];foreach(var group in architecture)if(group)group.ForceLOD(-1);}
            Require(frames.Count==7,"Surface diagnostic requires seven controlled camera frames");
        }
        IEnumerator ShadowStrengthControl(GameSession session,string name)
        {
            var lights=FindObjectsByType<Light>(FindObjectsSortMode.None);var strengths=lights.Select(x=>x.shadowStrength).ToArray();
            try
            {foreach(var light in lights)light.shadowStrength=.0001f;yield return Capture(session,name,new Vector3(201,.08f,200),new Vector3(211,1.45f,201));}
            finally {for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].shadowStrength=strengths[i];}
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
            {Require(Time.realtimeSinceStartup<deadline,"Local reflection did not finish for "+name+
                " request="+graphics.ReflectionRenderId+" globalEnabled="+QualitySettings.realtimeReflectionProbes+
                " probeEnabled="+graphics.OwnedProbe.isActiveAndEnabled+" captures="+graphics.ReflectionCaptures+
                " input="+session.InputAllowed+" quality="+QualitySettings.GetQualityLevel());yield return null;}
            session.GetComponent<LocalShadowBudget>()?.RefreshNow();
            var sourceData=player.eyes.GetUniversalAdditionalCameraData();
            Require(sourceData.renderType==CameraRenderType.Base&&sourceData.cameraStack.Count==0,"Controlled single-camera review cannot omit an authored overlay stack");
            var go=new GameObject("Controlled graphics camera");var camera=go.AddComponent<Camera>();
            camera.CopyFrom(player.eyes);camera.transform.SetPositionAndRotation(player.eyes.transform.position,player.eyes.transform.rotation);
            camera.enabled=false;camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=name=="surface-unlit-control"?false:sourceData.renderPostProcessing;data.volumeLayerMask=sourceData.volumeLayerMask;
            data.volumeTrigger=camera.transform;data.antialiasing=sourceData.antialiasing;data.antialiasingQuality=sourceData.antialiasingQuality;
            data.renderShadows=sourceData.renderShadows;
            data.requiresDepthOption=sourceData.requiresDepthOption;data.requiresColorOption=sourceData.requiresColorOption;
            data.stopNaN=sourceData.stopNaN;data.dithering=sourceData.dithering;
            // URP17.6 inherits an external target's format for intermediate colour.
            // Keep floating-point HDR here; sRGB encoding happens after tone mapping.
            var capturePipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            rt.antiAliasing=camera.allowMSAA?capturePipeline.msaaSampleCount:1;rt.Create();
            var old=RenderTexture.active;Texture2D linear=null,texture=null;
            try
            {
                Require(rt.IsCreated()&&!rt.sRGB,"Linear HDR capture target unavailable");
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                if(rt.antiAliasing>1)rt.ResolveAntiAliasedSurface();
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
                    hdrCamera=camera.allowHDR,hdrPipeline=pipeline.supportsHDR,captureSrgb=rt.sRGB,postProcessing=data.renderPostProcessing,captureMsaa=rt.antiAliasing,
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
