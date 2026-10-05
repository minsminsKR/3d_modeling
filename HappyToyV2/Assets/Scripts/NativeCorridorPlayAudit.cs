using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Native main-game route driver. Seeded preparation uses the same public
    // run API as the title; every subsequent action is keyboard/mouse input.
    public sealed class NativeCorridorPlayAudit : MonoBehaviour
    {
        string output,failure="";int seed=73;
        GameSession session;PlayerMotor player;NativeCorridorInputRoute route;RouteAudioCapture capture;
        Keyboard keyboard,oldKeyboard;Mouse oldMouse;
        InputSettings.BackgroundBehavior oldBackground;
        bool oldBackgroundRun,oldCursorVisible,settingsSaved,restored,ready;
        CursorLockMode oldCursorLock;int oldFrameRate;
        float began,routeWallStart,routeGameStart;double routeDspStart;
        int lastSteps,lastInteractions,lastRecovered,lastContacts,lastRecognition;
        readonly List<float> frames=new List<float>();
        readonly List<string> errors=new List<string>(),images=new List<string>();
        readonly List<CueEvent> cues=new List<CueEvent>();
        readonly Dictionary<StalkerFootsteps,Vector2Int> enemyCounts=new Dictionary<StalkerFootsteps,Vector2Int>();
        PauseProof pause;
        [Serializable] sealed class CueEvent
        {
            public string kind,owner,clip,detail;public bool recordedClip;
            public float gameSeconds,wallSeconds;public double dspTime;
            public int records;public Vector3 position;
        }
        [Serializable] sealed class PauseProof
        {public bool listenerPaused,gameFrozen,playerFrozen,staminaFrozen;public int callbacksBefore,callbacksAfter;public double dspAdvance;}
        [Serializable] sealed class Report
        {
            public string status,failure,unity,graphics,device,profileDirectory,audioDirectory;
            public string scope="Native rendered Windows seeded main corridor. Existing known-map nearest-goal stealth strategy uses only keyboard/mouse events after public level preparation. Natural listener DSP callbacks preserve the complete pre-device PCM stream in bounded 180-second WAV parts. No AI suppression/speed edits, teleport/Use/Collect calls or forced rendering/capture clock. Audit overhead and concurrent external GPU load are not removed; no device listening, human fear or first-player difficulty certification.";
            public int seed,records,movementUpdates,footsteps,contacts,recognition,frames,auditTargetFrameRate=60;
            public bool escaped,routePassed,inputDevicesRestored,externalGpuWorkloadUncontrolled=true;
            public float gameSeconds,wallSeconds,meanFrameMs,p95FrameMs,worstFrameMs,userVolume,userFov,userSensitivity;
            public bool userReducedMotion,userSubtitles;public int vSyncCount,originalTargetFrameRate;
            public double dspSeconds,dspToGameRatio,dspToWallRatio;
            public string[] errors,images;public CueEvent[] cues;public PauseProof pause;public RouteAudioCapture.Report audio;
        }
        string profileDirectory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v2-corridor-play-output");if(at<0||at+1>=args.Length)return;
            var audit=new GameObject("Explicit natural-clock main corridor play audit").AddComponent<NativeCorridorPlayAudit>();audit.output=Path.GetFullPath(args[at+1]);
            int seedAt=Array.IndexOf(args,"-v2-audit-seed");
            if(seedAt>=0&&(seedAt+1>=args.Length||!int.TryParse(args[seedAt+1],out audit.seed)))audit.failure="Invalid explicit audit seed";
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;Restore();}
        void Log(string message,string trace,LogType kind)
        {if((kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)&&errors.Count<64)errors.Add(message);}
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;Directory.CreateDirectory(output);var stack=new Stack<IEnumerator>();stack.Push(Run());
            try
            {
                while(stack.Count>0&&string.IsNullOrEmpty(failure))
                {
                    var iterator=stack.Peek();bool more=false;object next=null;
                    try{more=iterator.MoveNext();if(more)next=iterator.Current;}
                    catch(Exception error){failure=error.GetType().Name+": "+error.Message;}
                    if(!string.IsNullOrEmpty(failure))break;
                    if(!more){stack.Pop();(iterator as IDisposable)?.Dispose();continue;}
                    if(next is IEnumerator nested){stack.Push(nested);continue;}
                    yield return next;
                    if(Time.realtimeSinceStartup-began>=600)failure="Native audit exceeded the 600-second wall deadline";
                }
            }
            finally
            {
                while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
                route?.Dispose();Keys();Restore();var audio=capture?capture.Complete():null;Save(audio);
                Debug.Log("HAPPYTOY_NATIVE_CORRIDOR_PLAY_"+(string.IsNullOrEmpty(failure)?"PASS":"FAIL"));Application.Quit(string.IsNullOrEmpty(failure)?0:2);
            }
        }
        IEnumerator Run()
        {
            yield return null;yield return null;session=GameSession.Current;Require(session&&session.player&&session.Shell,"Authored session unavailable");player=session.player;
            Require(Application.platform==RuntimePlatform.WindowsPlayer&&Time.captureDeltaTime==0,"Natural native Windows player required");
            Require(session.Shell.Screen==GameShell.Page.Title,"Native main audit must start at Title");
            profileDirectory=Path.Combine(output,"isolated-profile",Guid.NewGuid().ToString("N"));session.ConfigureRecordDirectory(profileDirectory);
            oldBackground=InputSystem.settings.backgroundBehavior;oldBackgroundRun=Application.runInBackground;oldFrameRate=Application.targetFrameRate;
            oldCursorLock=Cursor.lockState;oldCursorVisible=Cursor.visible;oldKeyboard=Keyboard.current;oldMouse=Mouse.current;settingsSaved=true;
            Application.runInBackground=true;Application.targetFrameRate=60;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>("Native corridor audit keyboard");keyboard.MakeCurrent();
            capture=RouteAudioCapture.Attach(Path.Combine(output,"audio"));
            session.CreateCorridor(seed);session.Shell.Begin();Require(session.CorridorMode&&session.Corridor.Seed==seed,"Public seeded main-game preparation failed");
            ready=true;
            foreach(var steps in FindObjectsByType<StalkerFootsteps>(FindObjectsInactive.Include,FindObjectsSortMode.None))enemyCounts[steps]=Vector2Int.zero;
            float deadline=Time.realtimeSinceStartup+8;
            while(!player.Grounded||!capture.HasCallbacks||!capture.Snapshot().nonSilent)
            {Require(Time.realtimeSinceStartup<deadline,"Native listener supplied no non-silent raw callbacks at the live entrance");yield return null;}
            Frame("native-corridor-entrance.png");Debug.Log("HAPPYTOY_NATIVE_CORRIDOR_CALLBACKS "+capture.CallbackCount);
            yield return Pulse(Key.Escape);Require(session.Shell.Screen==GameShell.Page.Pause,"Real Escape did not pause main game");
            float game=session.ElapsedPlayTime,stamina=player.Stamina;Vector3 position=player.transform.position;double dsp=AudioSettings.dspTime;
            pause=new PauseProof {listenerPaused=AudioListener.pause,callbacksBefore=capture.CallbackCount};yield return new WaitForSecondsRealtime(.45f);
            pause.callbacksAfter=capture.CallbackCount;pause.dspAdvance=AudioSettings.dspTime-dsp;pause.gameFrozen=session.ElapsedPlayTime==game;
            pause.playerFrozen=player.transform.position==position;pause.staminaFrozen=player.Stamina==stamina;
            Require(pause.listenerPaused&&pause.gameFrozen&&pause.playerFrozen&&pause.staminaFrozen,"Actual main-game pause changed live player state");
            yield return Pulse(Key.Escape);Require(session.Shell.Screen==GameShell.Page.Playing,"Real Escape did not resume main game");
            routeWallStart=Time.realtimeSinceStartup;routeDspStart=AudioSettings.dspTime;routeGameStart=session.ElapsedPlayTime;
            route=new NativeCorridorInputRoute(session,player,session.Shell,Keys,output);yield return route.Run();
            Require(route.Passed&&session.Escaped&&session.RecordsRecovered==5,"Natural native main route did not prove full escape");
            yield return new WaitForSecondsRealtime(.2f);var audio=capture.Snapshot();
            Require(audio.callbacks>10&&audio.channels==2&&audio.nonSilent&&!audio.truncated&&audio.nonfiniteSamples==0,"Native main listener capture incomplete/invalid");
            Require(audio.stages.All(part=>part.samples>0&&part.clipped==0),"Full native main mix missed a memory stage or clipped");
            Require(audio.floors[0].rms>.00002&&audio.pause.steadyPeak<.00001,"Native main mix is silent or leaks during steady pause");
            Require(errors.Count==0,"Native main run logged runtime errors");
        }
        void Update()
        {
            if(!ready||!session)return;
            if(session.InputAllowed)frames.Add(Time.unscaledDeltaTime);
            var feedback=player.Feedback;var tension=player.GetComponent<PerceivedTension>();
            if(feedback.FootstepsPlayed!=lastSteps){lastSteps=feedback.FootstepsPlayed;Cue("player-step",player.name,feedback.LastFootstepClip,feedback.LastFootstepSurface);}
            if(feedback.InteractionCuesPlayed!=lastInteractions){lastInteractions=feedback.InteractionCuesPlayed;Cue("player-interaction",player.name,feedback.LastInteractionClip,"");}
            if(session.RecordsRecovered!=lastRecovered)
            {
                lastRecovered=session.RecordsRecovered;Cue("memory",session.name,feedback.LastInteractionClip,"recovered="+lastRecovered);
                if(lastRecovered==2||lastRecovered==5)Frame("native-corridor-memory-"+lastRecovered+".png");
            }
            if(tension&&tension.ContactEvents!=lastContacts){lastContacts=tension.ContactEvents;Cue("perceived-contact",player.name,null,"count="+lastContacts);}
            if(tension&&tension.RecognitionEvents!=lastRecognition){lastRecognition=tension.RecognitionEvents;Cue("witnessed-recognition",player.name,null,"count="+lastRecognition);}
            foreach(var pair in enemyCounts.ToArray())
            {
                var step=pair.Key;if(!step)continue;var count=new Vector2Int(step.StepsPlayed,step.AttackCuesPlayed);
                if(count.x!=pair.Value.x)Cue("enemy-step",step.name,step.MovementClip,step.Profile.ToString());
                if(count.y!=pair.Value.y)Cue("enemy-attack",step.name,step.AttackClip,"count="+count.y);enemyCounts[step]=count;
            }
        }
        void Cue(string kind,string owner,AudioClip clip,string detail)
        {
            if(cues.Count>=8192)return;
            cues.Add(new CueEvent{kind=kind,owner=owner,clip=clip?clip.name:"",detail=detail,recordedClip=clip&&clip.name.StartsWith("External ",StringComparison.Ordinal),
                gameSeconds=session.ElapsedPlayTime,wallSeconds=Time.realtimeSinceStartup-began,dspTime=AudioSettings.dspTime,records=session.RecordsRecovered,position=player.transform.position});
        }
        void Keys(params Key[] held){if(keyboard!=null&&keyboard.added)InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));}
        IEnumerator Pulse(Key key){Keys();yield return null;Keys(key);yield return null;Keys();yield return null;}
        void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        void Frame(string name)
        {
            var target=new RenderTexture(640,360,24);target.Create();var previous=RenderTexture.active;Texture2D frame=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
                frame=new Texture2D(640,360,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,640,360),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(output,name),frame.EncodeToPNG());images.Add(name);
            }
            finally{RenderTexture.active=previous;if(frame)Destroy(frame);target.Release();Destroy(target);}
        }
        void Restore()
        {
            if(restored)return;restored=true;
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            if(settingsSaved){InputSystem.settings.backgroundBehavior=oldBackground;Application.runInBackground=oldBackgroundRun;Application.targetFrameRate=oldFrameRate;Cursor.lockState=oldCursorLock;Cursor.visible=oldCursorVisible;}
        }
        void Save(RouteAudioCapture.Report audio)
        {
            if(string.IsNullOrEmpty(failure)&&(!session||!session.Escaped||audio==null||audio.truncated||!string.IsNullOrEmpty(audio.writerError)))failure="Native main completion/audio retention unproven";
            var sorted=frames.OrderBy(value=>value).ToArray();float game=session?session.ElapsedPlayTime-routeGameStart:0,wall=routeWallStart>0?Time.realtimeSinceStartup-routeWallStart:0;
            double dsp=routeWallStart>0?AudioSettings.dspTime-routeDspStart:0;var shell=session?session.Shell:null;
            var report=new Report {status=string.IsNullOrEmpty(failure)?"PASS":"FAIL",failure=failure,seed=seed,unity=Application.unityVersion,
                graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,profileDirectory=profileDirectory,audioDirectory=capture?capture.OutputDirectory:"",
                escaped=session&&session.Escaped,routePassed=route!=null&&route.Passed,records=session?session.RecordsRecovered:0,movementUpdates=player?player.MovementUpdates:0,
                footsteps=lastSteps,contacts=lastContacts,recognition=lastRecognition,frames=sorted.Length,gameSeconds=game,wallSeconds=wall,dspSeconds=dsp,
                dspToGameRatio=game>0?dsp/game:0,dspToWallRatio=wall>0?dsp/wall:0,meanFrameMs=sorted.Length>0?sorted.Average()*1000:0,
                p95FrameMs=sorted.Length>0?sorted[Math.Min(sorted.Length-1,Mathf.FloorToInt(sorted.Length*.95f))]*1000:0,worstFrameMs=sorted.Length>0?sorted.Last()*1000:0,
                userVolume=shell?shell.Volume:0,userFov=shell?shell.FieldOfView:0,userSensitivity=shell?shell.Sensitivity:0,userReducedMotion=shell&&shell.ReducedMotion,
                userSubtitles=shell&&shell.Subtitles,vSyncCount=QualitySettings.vSyncCount,originalTargetFrameRate=oldFrameRate,inputDevicesRestored=restored,
                errors=errors.ToArray(),images=images.ToArray(),cues=cues.ToArray(),pause=pause,audio=audio};
            File.WriteAllText(Path.Combine(output,"native-corridor-play.json"),JsonUtility.ToJson(report,true));
        }
    }
}
