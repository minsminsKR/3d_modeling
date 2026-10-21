using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit native shader-retention/art review. Ordinary play installs nothing.
    public sealed class SchoolHorrorReviewProbe : MonoBehaviour
    {
        const int Width=1280,Height=720;
        string output,phase="install";bool finished;float started;
        int reflectionPumpFrames;float reflectionWaitSeconds;
        readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
        readonly List<string> errors=new List<string>();
        readonly List<View> views=new List<View>();
        readonly List<ReflectionState> reflectionStates=new List<ReflectionState>();
        [Serializable] sealed class ProbeState
        {
            public string name,mode,timeSlicing,texture;public bool active,realtimeTextureCreated;
            public int textureWidth,realtimeTextureWidth;
        }
        [Serializable] sealed class ReflectionState
        {
            public string phase,createdUtc,screen;public int frame,pumpedFrames,quality,commonRenderId,commonCaptures;
            public float waitSeconds,timeScale;public bool batchMode,desktopInteractionSuppressed,lightingPrepared,lightingActive,
                reflectionReady,realtimeReflectionsEnabled,commonPrepared,commonReflectionReady;
            public ProbeState[] probes;
        }
        [Serializable] sealed class View
        {
            public string id,file;public Vector3 feet,eye,look;public Quaternion rotation;
            public int width=Width,height=Height,frame,visiblePixels,magentaPixels;
            public float meanDisplayLuma,fieldOfView;public bool flashlight;
        }
        [Serializable] sealed class Report
        {
            public string status,failure,phase,unity,device,api,createdUtc;
            public string scope="Diagnostic-only fresh isolated school-v3 run using the production URP camera. Batch mode pumps StandardRequest into a 160x90 offscreen target while awaiting actual reflection completion, with a 25-second bound. Reflection completion, phase and captured views are reported separately; no completed surface review is claimed on a failed initialization. Exported PNGs are unfiltered 1280x720 sRGB readbacks, gameplay paused and torch off except explicitly named torch view. This does not certify manual survival, subjective realism or displayed FPS.";
            public bool deviceSessionMuted,quietGuardActive,reviewWritesUserPreferences,reflectionReady,batchMode,desktopInteractionSuppressed;
            public int reflectionPumpFrames;public float listenerVolume,reflectionWaitSeconds;
            public View[] views;public string[] errors;public ReflectionState[] reflectionStates;
        }
        bool reflectionReady;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v2-school-horror-review-output");
            if(at>=0&&at+1<args.Length)new GameObject("Explicit silent school horror surface review")
                .AddComponent<SchoolHorrorReviewProbe>().output=args[at+1];
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message);}
        void Update()
        {if(!finished&&started>0&&Time.realtimeSinceStartup-started>180)Finish("Timed out after 180 seconds in phase "+phase);}
        IEnumerator Start()
        {
            Application.runInBackground=true;started=Time.realtimeSinceStartup;routines.Push(Run());
            while(!finished)
            {
                object next=null;bool moved=false;string failure=null;
                try{moved=routines.Peek().MoveNext();if(moved)next=routines.Peek().Current;}
                catch(Exception error){failure=error.ToString();}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved)
                {
                    (routines.Pop() as IDisposable)?.Dispose();
                    if(routines.Count==0){Finish(null);yield break;}
                    continue;
                }
                if(next is IEnumerator child){routines.Push(child);continue;}
                yield return next;
            }
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}
        IEnumerator Run()
        {
            phase="quiet-audio-guard";
            // The existing guard owns current-process mute and preference restoration.
            // A failed device guard may continue this visual-only review with listener zero.
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output,false);
            Require(DiagnosticAudioSilence.Enabled,"Explicit review did not activate the quiet guard");
            Require(DiagnosticAudioSilence.DeviceSessionMuted||AudioListener.volume==0,"Visual review has no verified silent output path");
            Require(DiagnosticDesktopPolicy.SuppressCursorWrites,
                "Native school review requires -batchmode and -v2-audit-no-desktop-interaction before gameplay creation");
            yield return null;yield return null;
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            var session=GameSession.Current;
            Require(session&&session.player&&session.Shell,"Production session/player is missing");
            Require(session.Shell.Screen==GameShell.Page.Title&&!session.Chapter&&!session.Corridor&&!session.Finished,
                "School review requires a fresh title session; exclude its flag from audit auto-start");
            phase="create-production-school";session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            session.CreateSchoolLoop();session.Shell.Begin();yield return null;yield return null;
            Require(session.ChapterMode&&session.Chapter.Loop,"Production school-v3 creation failed");
            var world=session.Chapter.Loop;
            var lighting=world.Root.GetComponent<SchoolHorrorLighting>();
            Require(lighting&&lighting.Prepared,"Production school lighting is missing");
            session.player.enabled=false;session.player.flashlight.enabled=false;
            yield return AwaitReflections(session,lighting);
            Require(lighting.ReflectionReady,"School reflection completion was lost before capture");
            reflectionReady=true;phase="pause-for-capture";session.Shell.Pause();yield return null;
            yield return Pose(session,"corridor-reference",new Vector3(.72f,.03f,6.3f),new Vector3(-.15f,1.58f,36));
            yield return Pose(session,"music-through-glass",new Vector3(.1f,.03f,29.9f),new Vector3(-6,1.35f,30.4f));
            yield return Pose(session,"wet-pool",new Vector3(.8f,.03f,6.9f),new Vector3(0,.15f,10.5f));
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-school-horror-review-torch")>=0)
            {
                session.player.flashlight.enabled=true;
                yield return Pose(session,"corridor-torch",new Vector3(.72f,.03f,6.3f),new Vector3(-.15f,1.58f,36));
                session.player.flashlight.enabled=false;
            }
            phase="review-complete";
        }
        ReflectionState RecordReflections(GameSession session,SchoolHorrorLighting lighting,string statePhase,float waitStarted)
        {
            reflectionWaitSeconds=Mathf.Max(0,Time.realtimeSinceStartup-waitStarted);
            var common=session.GetComponent<GraphicsLightingPresentation>();
            var probes=new List<ProbeState>();
            foreach(var probe in lighting.OwnedProbes)
            {
                if(!probe){probes.Add(new ProbeState{name="destroyed"});continue;}
                var texture=probe.texture;var realtime=probe.realtimeTexture;
                probes.Add(new ProbeState{name=probe.name,active=probe.isActiveAndEnabled,mode=probe.mode.ToString(),
                    timeSlicing=probe.timeSlicingMode.ToString(),texture=texture?texture.name:null,textureWidth=texture?texture.width:0,
                    realtimeTextureWidth=realtime?realtime.width:0,realtimeTextureCreated=realtime&&realtime.IsCreated()});
            }
            var state=new ReflectionState{phase=statePhase,createdUtc=DateTime.UtcNow.ToString("o"),frame=Time.frameCount,
                pumpedFrames=reflectionPumpFrames,waitSeconds=reflectionWaitSeconds,timeScale=Time.timeScale,
                batchMode=Application.isBatchMode,desktopInteractionSuppressed=DiagnosticDesktopPolicy.SuppressCursorWrites,
                lightingPrepared=lighting.Prepared,lightingActive=lighting.isActiveAndEnabled,reflectionReady=lighting.ReflectionReady,
                realtimeReflectionsEnabled=QualitySettings.realtimeReflectionProbes,quality=QualitySettings.GetQualityLevel(),
                commonPrepared=common&&common.Prepared,commonReflectionReady=common&&common.ReflectionReady,
                commonRenderId=common?common.ReflectionRenderId:-1,commonCaptures=common?common.ReflectionCaptures:0,
                screen=session.Shell.Screen.ToString(),probes=probes.ToArray()};
            reflectionStates.Add(state);
            File.WriteAllText(Path.Combine(output,"reflection-progress.json"),JsonUtility.ToJson(state,true));return state;
        }
        IEnumerator AwaitReflections(GameSession session,SchoolHorrorLighting lighting)
        {
            phase="reflection-initialization";
            float waitStarted=Time.realtimeSinceStartup,deadline=waitStarted+25,nextEvidence=waitStarted+5;
            var camera=session.player.eyes;
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            float previousAspect=camera.aspect;bool previousEnabled=camera.enabled;RenderTexture target=null;
            try
            {
                RecordReflections(session,lighting,"before-offscreen-pump",waitStarted);
                RenderPipeline.StandardRequest request=null;
                if(Application.isBatchMode&&!lighting.ReflectionReady)
                {
                    target=new RenderTexture(160,90,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                        {name="Diagnostic-only school reflection render pump"};
                    Require(target.Create(),"Could not allocate the offscreen reflection pump target");
                    request=new RenderPipeline.StandardRequest{destination=target};
                    Require(RenderPipeline.SupportsRenderRequest(camera,request),"Production pipeline does not support StandardRequest reflection pumping");
                    camera.enabled=false;camera.targetTexture=target;camera.aspect=160f/90;
                }
                while(!lighting.ReflectionReady)
                {
                    if(Time.realtimeSinceStartup>=deadline)
                    {
                        phase="reflection-timeout";
                        var state=RecordReflections(session,lighting,"reflection-timeout",waitStarted);
                        throw new InvalidOperationException("School reflections did not complete within 25 seconds: "+JsonUtility.ToJson(state));
                    }
                    if(request!=null)
                    {
                        // Batch execution may need an explicit render to advance its time-sliced probes.
                        // StandardRequest renders the normal camera stack into this small private target.
                        RenderPipeline.SubmitRenderRequest(camera,request);reflectionPumpFrames++;
                    }
                    if(reflectionPumpFrames==1||Time.realtimeSinceStartup>=nextEvidence)
                    {
                        RecordReflections(session,lighting,reflectionPumpFrames==1?"first-offscreen-render":"awaiting-reflections",waitStarted);
                        nextEvidence=Time.realtimeSinceStartup+5;
                    }
                    yield return null;
                }
                reflectionReady=true;RecordReflections(session,lighting,"reflections-complete",waitStarted);
            }
            finally
            {
                camera.targetTexture=previousTarget;camera.aspect=previousAspect;camera.enabled=previousEnabled;
                RenderTexture.active=previousActive;
                if(target){target.Release();Destroy(target);}
            }
        }
        IEnumerator Pose(GameSession session,string id,Vector3 feet,Vector3 look)
        {
            phase="capture-"+id;
            var player=session.player;var origin=SchoolLoopWorld.Origin;
            var heading=look-feet;heading.y=0;
            player.RelocateWorld(origin+feet,Quaternion.LookRotation(heading));
            var camera=player.eyes;camera.transform.rotation=Quaternion.LookRotation(origin+look-camera.transform.position);
            Physics.SyncTransforms();session.GetComponent<LocalShadowBudget>()?.RefreshNow();yield return null;
            var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            float previousAspect=camera.aspect;bool previousEnabled=camera.enabled;Texture2D frame=null;
            try
            {
                Require(target.Create(),"Could not allocate native review render target");
                camera.enabled=false;camera.targetTexture=target;camera.aspect=Width/(float)Height;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;frame=new Texture2D(Width,Height,TextureFormat.RGB24,false,false);
                frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply(false,false);
                var pixels=frame.GetPixels32();long sum=0;int visible=0,magenta=0;
                foreach(var pixel in pixels)
                {
                    int luma=(pixel.r*54+pixel.g*183+pixel.b*19)/256;sum+=luma;
                    if(luma>8)visible++;
                    if(pixel.r>180&&pixel.b>180&&pixel.g<50)magenta++;
                }
                string file=id+".png";File.WriteAllBytes(Path.Combine(output,file),frame.EncodeToPNG());
                views.Add(new View{id=id,file=file,feet=origin+feet,eye=camera.transform.position,look=origin+look,
                    rotation=camera.transform.rotation,frame=Time.frameCount,visiblePixels=visible,magentaPixels=magenta,
                    meanDisplayLuma=sum/(255f*pixels.Length),fieldOfView=camera.fieldOfView,flashlight=player.flashlight.enabled});
                Require(magenta<40,"Native review contains error shader pixels: "+id+" count="+magenta);
                Require(visible>pixels.Length*.12f,"Native review is unreadable black: "+id+" visible="+visible);
            }
            finally
            {
                camera.targetTexture=previousTarget;camera.aspect=previousAspect;camera.enabled=previousEnabled;
                RenderTexture.active=previousActive;if(frame)Destroy(frame);target.Release();Destroy(target);
            }
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;
            while(routines.Count>0)
            {
                try{(routines.Pop() as IDisposable)?.Dispose();}
                catch(Exception error){errors.Add("Review cleanup: "+error);}
            }
            var report=new Report{status=failure==null&&errors.Count==0&&reflectionReady&&views.Count>=3?"PASS":"FAIL",failure=failure,phase=phase,
                unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                createdUtc=DateTime.UtcNow.ToString("o"),deviceSessionMuted=DiagnosticAudioSilence.DeviceSessionMuted,
                quietGuardActive=DiagnosticAudioSilence.Enabled,listenerVolume=AudioListener.volume,reflectionReady=reflectionReady,
                batchMode=Application.isBatchMode,desktopInteractionSuppressed=DiagnosticDesktopPolicy.SuppressCursorWrites,
                reflectionPumpFrames=reflectionPumpFrames,reflectionWaitSeconds=reflectionWaitSeconds,reflectionStates=reflectionStates.ToArray(),
                reviewWritesUserPreferences=false,views=views.ToArray(),errors=errors.ToArray()};
            int exit=report.status=="PASS"?0:1;
            try
            {
                Directory.CreateDirectory(output);DiagnosticAudioSilence.WriteEvidence(output);
                File.WriteAllText(Path.Combine(output,"school-horror-review.json"),JsonUtility.ToJson(report,true));
                Debug.Log("HAPPYTOY_SCHOOL_HORROR_REVIEW_"+report.status);
            }
            catch(Exception error){Debug.LogError("Could not write school review report: "+error);exit=1;}
            Application.Quit(exit);
        }
    }
}
