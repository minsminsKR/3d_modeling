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
        readonly List<InputDevice> isolatedDevices=new List<InputDevice>();
        InputSettings.BackgroundBehavior oldBackground;
        bool oldBackgroundRun,oldCursorVisible,settingsSaved,restored,ready;
        CursorLockMode oldCursorLock;int oldFrameRate;
        float began,routeWallStart,routeGameStart;double routeDspStart;
        int lastSteps,lastInteractions,lastRecovered,lastContacts,lastRecognition;
        readonly List<float> frames=new List<float>();
        readonly List<string> errors=new List<string>(),images=new List<string>();
        readonly List<CueEvent> cues=new List<CueEvent>();
        readonly Dictionary<StalkerFootsteps,Vector2Int> enemyCounts=new Dictionary<StalkerFootsteps,Vector2Int>();
        readonly Dictionary<InteractionAudio,int> doorCounts=new Dictionary<InteractionAudio,int>();
        PauseProof pause;
        bool firstCandleImage;
        int lastMeasuredTargetFrameRate=-1,lastMeasuredVSyncCount=-1;
        ClockPoint routeBeginClock, routeEndClock, settlingEndClock, captureEndClock;
        bool routeCompleted, audioSettlingRequested, audioSettlingCompleted;
        double writerWallSeconds; long writerStartedTicks, writerEndedTicks;
        [Serializable] sealed class ClockPoint
        {
            public string phase;
            public float gameplaySeconds, engineRealtimeSeconds;
            public double dspSeconds;
            public long stopwatchTicks;
        }
        [Serializable] sealed class TimingReport
        {
            public string scope="Active route, requested natural .2-second audio settling, capture cleanup and PCM disk retention are separate intervals. Clock values are observed, never forced.";
            public bool routeStarted, routeCompleted, audioSettlingRequested, audioSettlingCompleted;
            public long stopwatchFrequency, writerStartedTicks, writerEndedTicks;
            public ClockPoint routeBegin, routeEnd, settlingEnd, captureEnd;
            public double routeWallSeconds, routeDspSeconds, settlingWallSeconds, settlingDspSeconds, cleanupWallSeconds, cleanupDspSeconds, writerWallSeconds;
            public float routeGameSeconds, settlingGameSeconds, cleanupGameSeconds;
        }
        ClockPoint ReadClock(string phase) => new ClockPoint {
            phase=phase,gameplaySeconds=session?session.ElapsedPlayTime:0,
            engineRealtimeSeconds=Time.realtimeSinceStartup,dspSeconds=AudioSettings.dspTime,
            stopwatchTicks=System.Diagnostics.Stopwatch.GetTimestamp() };
        static double ClockWall(ClockPoint start,ClockPoint end) => start!=null && end!=null ?
            Math.Max(0,(end.stopwatchTicks-start.stopwatchTicks)/(double)System.Diagnostics.Stopwatch.Frequency):0;
        static float ClockGame(ClockPoint start,ClockPoint end) => start!=null && end!=null ?
            Math.Max(0,end.gameplaySeconds-start.gameplaySeconds):0;
        static double ClockDsp(ClockPoint start,ClockPoint end) => start!=null && end!=null ?
            Math.Max(0,end.dspSeconds-start.dspSeconds):0;
        void EndGameplayClock(string phase,bool completed)
        {
            if(routeEndClock!=null)return;
            routeEndClock=ReadClock(phase);routeCompleted=completed;
        }
        void SnapshotInterruptedRoute()
        {
            // Capture a failure at first entry to finally, before route Dispose
            // writes its evidence or native PCM retention blocks the main thread.
            EndGameplayClock(string.IsNullOrEmpty(failure)?"route-finalized":"route-failed: "+failure,false);
            if(audioSettlingRequested && settlingEndClock==null)
                settlingEndClock=ReadClock("audio-settling-interrupted: "+failure);
        }
        RouteAudioCapture.Report RetainCapturedAudio()
        {
            // This boundary immediately precedes Complete(), which freezes the
            // real listener tap and writes/hashes the remaining original PCM.
            captureEndClock=ReadClock("capture-before-PCM-retention");
            writerStartedTicks=System.Diagnostics.Stopwatch.GetTimestamp();
            try { return capture?capture.Complete():null; }
            catch(Exception error)
            {
                string retainedFailure="Native PCM retention failed: "+error.GetType().Name+": "+error.Message;
                failure=string.IsNullOrEmpty(failure)?retainedFailure:failure+" | "+retainedFailure;
                if(errors.Count<64)errors.Add(retainedFailure);
                Debug.LogError(retainedFailure);
                // Preserve the real counters/files already retained. No PCM or
                // callback is synthesized when final file persistence fails.
                return capture?capture.Snapshot():null;
            }
            finally
            {
                writerEndedTicks=System.Diagnostics.Stopwatch.GetTimestamp();
                writerWallSeconds=(writerEndedTicks-writerStartedTicks)/(double)System.Diagnostics.Stopwatch.Frequency;
            }
        }
        TimingReport ClockEvidence()
        {
            var cleanupStart=settlingEndClock??routeEndClock;
            return new TimingReport {
                routeStarted=routeBeginClock!=null,routeCompleted=routeCompleted,audioSettlingRequested=audioSettlingRequested,audioSettlingCompleted=audioSettlingCompleted,
                stopwatchFrequency=System.Diagnostics.Stopwatch.Frequency,writerStartedTicks=writerStartedTicks,writerEndedTicks=writerEndedTicks,
                routeBegin=routeBeginClock,routeEnd=routeEndClock,settlingEnd=settlingEndClock,captureEnd=captureEndClock,
                routeWallSeconds=ClockWall(routeBeginClock,routeEndClock),routeGameSeconds=ClockGame(routeBeginClock,routeEndClock),routeDspSeconds=ClockDsp(routeBeginClock,routeEndClock),
                settlingWallSeconds=audioSettlingRequested?ClockWall(routeEndClock,settlingEndClock):0,
                settlingGameSeconds=audioSettlingRequested?ClockGame(routeEndClock,settlingEndClock):0,
                settlingDspSeconds=audioSettlingRequested?ClockDsp(routeEndClock,settlingEndClock):0,
                cleanupWallSeconds=ClockWall(cleanupStart,captureEndClock),cleanupGameSeconds=ClockGame(cleanupStart,captureEndClock),cleanupDspSeconds=ClockDsp(cleanupStart,captureEndClock),
                writerWallSeconds=writerWallSeconds };
        }

        [Serializable] sealed class CueEvent
        {
            public string kind,owner,clip,detail;public bool recordedClip;
            public float gameSeconds,wallSeconds;public double dspTime;
            public int records;public Vector3 position;
        }
        [Serializable] sealed class PauseProof
        {public bool listenerPaused,gameFrozen,playerFrozen,staminaFrozen,flashlightFrozen;public int callbacksBefore,callbacksAfter;public double dspAdvance;}
        [Serializable] sealed class Report
        {
            public string status,failure,unity,graphics,device,cpu,profileDirectory,audioDirectory;
            public int width,height;public bool developmentBuild;
            public int lastMeasuredTargetFrameRate,lastMeasuredVSyncCount;
            public bool frameSettingsMeasured;
            public string frameSettingsScope="Frame settings are sampled with active audit Update intervals before restoration. Desktop vSyncCount != 0 ignores Application.targetFrameRate; requested target settings do not certify a frame cap, refresh rate or hardware presentation.";
            public string frameMeasurement="Active Update unscaled frame intervals including audit screenshots/audio/disk overhead; not GPU timings or hardware presentation timestamps. Audit requests Application.targetFrameRate=60; Desktop VSync can override it. originalTargetFrameRate records the prior property setting.";
            public string scope="Native rendered Windows seeded main corridor. Existing known-map nearest-goal stealth strategy uses only keyboard/mouse events after public level preparation. Natural listener DSP callbacks preserve the complete pre-device PCM stream in bounded 180-second WAV parts. No AI suppression/speed edits, teleport/Use/Collect calls or forced audio rendering/capture clock. Milestone screenshots add offscreen camera rendering and readback overhead. Audit overhead and concurrent external GPU load are not removed; no device listening, human fear or first-player difficulty certification.";
            public int seed,records,movementUpdates,footsteps,contacts,recognition,frames,isolatedInputDeviceCount,auditTargetFrameRate=60;
            public string inputIsolation="Pre-existing keyboard and mouse devices are disabled and restored only inside this diagnostic player; the native scripted devices drive the unchanged production motor. Desktop OS input remains usable.";
            public bool escaped,routePassed,inputDevicesRestored,externalGpuWorkloadUncontrolled=true;
            public float gameSeconds,wallSeconds,meanFrameMs,p95FrameMs,worstFrameMs,userVolume,userFov,userSensitivity,flashlightCharge;
            public int batteriesCollected,candlesIgnited,hidingRolls;
            public string hidingOutcome,hidingRng="production Unity RNG; no forced sample or seed";
            public bool userReducedMotion,userSubtitles;public int vSyncCount,originalTargetFrameRate;
            public double dspSeconds,dspToGameRatio,dspToWallRatio;
            public string[] errors,images;public CueEvent[] cues;public PauseProof pause;public RouteAudioCapture.Report audio;public TimingReport timing;
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
                SnapshotInterruptedRoute();
                while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
                route?.Dispose();Keys();Restore();var audio=RetainCapturedAudio();Save(audio);
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
            // App-local isolation keeps physical desktop mouse activity from
            // taking Mouse.current away from the scripted native input route.
            // OS devices stay usable by the user outside this diagnostic player.
            foreach(var device in InputSystem.devices.ToArray())
                if(device.enabled && (device is Mouse || device is Keyboard))
                {isolatedDevices.Add(device);InputSystem.DisableDevice(device);}
            keyboard=InputSystem.AddDevice<Keyboard>("Native corridor audit keyboard");keyboard.MakeCurrent();
            capture=RouteAudioCapture.Attach(Path.Combine(output,"audio"));
            session.CreateCorridor(seed);session.Shell.Begin();Require(session.CorridorMode&&session.Corridor.Seed==seed,"Public seeded main-game preparation failed");
            ready=true;
            foreach(var steps in FindObjectsByType<StalkerFootsteps>(FindObjectsInactive.Include,FindObjectsSortMode.None))enemyCounts[steps]=Vector2Int.zero;
            // A quiet entrance can legitimately be beyond all spatial room emitters.
            // Use an actual F contact as the native-audio positive control, retaining
            // the non-silent callback gate instead of requiring an omnipresent drone.
            yield return Pulse(Key.F);
            float deadline=Time.realtimeSinceStartup+8;
            while(!player.Grounded||!capture.HasCallbacks||!capture.Snapshot().nonSilent)
            {Require(Time.realtimeSinceStartup<deadline,"Native listener supplied no non-silent raw callbacks at the live entrance");yield return null;}
            Frame("native-corridor-entrance.png");Debug.Log("HAPPYTOY_NATIVE_CORRIDOR_CALLBACKS "+capture.CallbackCount);
            yield return Pulse(Key.Escape);Require(session.Shell.Screen==GameShell.Page.Pause,"Real Escape did not pause main game");
            float game=session.ElapsedPlayTime,stamina=player.Stamina,charge=player.FlashlightSystem.Charge;Vector3 position=player.transform.position;double dsp=AudioSettings.dspTime;
            pause=new PauseProof {listenerPaused=AudioListener.pause,callbacksBefore=capture.CallbackCount};yield return new WaitForSecondsRealtime(.45f);
            pause.callbacksAfter=capture.CallbackCount;pause.dspAdvance=AudioSettings.dspTime-dsp;pause.gameFrozen=session.ElapsedPlayTime==game;
            pause.playerFrozen=player.transform.position==position;pause.staminaFrozen=player.Stamina==stamina;pause.flashlightFrozen=player.FlashlightSystem.Charge==charge;
            Require(pause.listenerPaused&&pause.gameFrozen&&pause.playerFrozen&&pause.staminaFrozen&&pause.flashlightFrozen,"Actual main-game pause changed live player state");
            yield return Pulse(Key.Escape);Require(session.Shell.Screen==GameShell.Page.Playing,"Real Escape did not resume main game");
            routeWallStart=Time.realtimeSinceStartup;routeDspStart=AudioSettings.dspTime;routeGameStart=session.ElapsedPlayTime;
            routeBeginClock=ReadClock("active-route-begin");
            route=new NativeCorridorInputRoute(session,player,session.Shell,Keys,output);yield return route.Run();
            Require(route.Passed&&session.Escaped&&session.RecordsRecovered==5&&route.BatteriesCollected>=1&&route.CandlesIgnited>=1,
                "Natural native main route did not prove full escape and actual finite-light input use");
            EndGameplayClock("five-memory-input-route-complete",true);
            audioSettlingRequested=true;
            yield return new WaitForSecondsRealtime(.2f);
            settlingEndClock=ReadClock("natural-audio-settling-complete");audioSettlingCompleted=true;
            var audio=capture.Snapshot();
            Require(audio.callbacks>10&&audio.channels==2&&audio.nonSilent&&!audio.truncated&&audio.nonfiniteSamples==0,"Native main listener capture incomplete/invalid");
            Require(audio.stages.All(part=>part.samples>0&&part.clipped==0),"Full native main mix missed a memory stage or clipped");
            Require(audio.floors[0].rms>.00002&&audio.pause.steadyPeak<.00001,"Native main mix is silent or leaks during steady pause");
            Require(errors.Count==0,"Native main run logged runtime errors");
        }
        void Update()
        {
            if(!ready||!session)return;
            if(session.InputAllowed)
            {
                lastMeasuredTargetFrameRate=Application.targetFrameRate;
                lastMeasuredVSyncCount=QualitySettings.vSyncCount;
                frames.Add(Time.unscaledDeltaTime);
            }
            if(route!=null && route.CandlesIgnited>0 && !firstCandleImage)
            {
                firstCandleImage=true;
                // Capture the real current eye immediately after its first E candle ignition.
                // This retains native emission/local-light evidence without moving the camera.
                Frame("native-corridor-first-candle.png");
            }
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
                if(count.x!=pair.Value.x)Cue("enemy-step",step.name,step.MovementClip,step.Profile.ToString(),step.transform.position);
                if(count.y!=pair.Value.y)Cue("enemy-attack",step.name,step.AttackClip,"count="+count.y,step.transform.position);enemyCounts[step]=count;
            }
            foreach(var door in FindObjectsByType<InteractionAudio>(FindObjectsSortMode.None))
            {
                if(door.gameObject.scene!=session.gameObject.scene)continue;
                doorCounts.TryGetValue(door,out int before);
                if(door.CuesPlayed!=before)Cue("physical-door",door.name,door.LastClip,
                    (door.LastOpening?"opening":"closing")+" count="+door.CuesPlayed,
                    door.Source?door.Source.transform.position:door.transform.position);
                doorCounts[door]=door.CuesPlayed;
            }
        }
        void Cue(string kind,string owner,AudioClip clip,string detail,Vector3? sourcePosition=null)
        {
            if(cues.Count>=8192)return;
            cues.Add(new CueEvent{kind=kind,owner=owner,clip=clip?clip.name:"",detail=detail,recordedClip=clip&&clip.name.StartsWith("External ",StringComparison.Ordinal),
                gameSeconds=session.ElapsedPlayTime,wallSeconds=Time.realtimeSinceStartup-began,dspTime=AudioSettings.dspTime,records=session.RecordsRecovered,position=sourcePosition??player.transform.position});
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
            foreach(var device in isolatedDevices)if(device.added&&!device.enabled)InputSystem.EnableDevice(device);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            if(settingsSaved){InputSystem.settings.backgroundBehavior=oldBackground;Application.runInBackground=oldBackgroundRun;Application.targetFrameRate=oldFrameRate;Cursor.lockState=oldCursorLock;Cursor.visible=oldCursorVisible;}
        }
        void Save(RouteAudioCapture.Report audio)
        {
            if(string.IsNullOrEmpty(failure)&&(!session||!session.Escaped||audio==null||audio.truncated||!string.IsNullOrEmpty(audio.writerError)))failure="Native main completion/audio retention unproven";
            var sorted=frames.OrderBy(value=>value).ToArray();var clocks=ClockEvidence();
            float game=clocks.routeGameSeconds,wall=(float)clocks.routeWallSeconds;
            double dsp=clocks.routeDspSeconds;var shell=session?session.Shell:null;
            var report=new Report {status=string.IsNullOrEmpty(failure)?"PASS":"FAIL",failure=failure,seed=seed,unity=Application.unityVersion,
                graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                width=Screen.width,height=Screen.height,developmentBuild=Debug.isDebugBuild,profileDirectory=profileDirectory,audioDirectory=capture?capture.OutputDirectory:"",isolatedInputDeviceCount=isolatedDevices.Count,
                escaped=session&&session.Escaped,routePassed=route!=null&&route.Passed,records=session?session.RecordsRecovered:0,movementUpdates=player?player.MovementUpdates:0,
                flashlightCharge=player?player.FlashlightSystem.Charge:0,batteriesCollected=route!=null?route.BatteriesCollected:0,
                candlesIgnited=route!=null?route.CandlesIgnited:0,hidingRolls=player?player.HidingRolls:0,hidingOutcome=player?player.HidingOutcome.ToString():"",
                footsteps=lastSteps,contacts=lastContacts,recognition=lastRecognition,frames=sorted.Length,gameSeconds=game,wallSeconds=wall,dspSeconds=dsp,
                dspToGameRatio=game>0?dsp/game:0,dspToWallRatio=wall>0?dsp/wall:0,meanFrameMs=sorted.Length>0?sorted.Average()*1000:0,
                p95FrameMs=sorted.Length>0?sorted[Math.Min(sorted.Length-1,Mathf.FloorToInt(sorted.Length*.95f))]*1000:0,worstFrameMs=sorted.Length>0?sorted.Last()*1000:0,
                userVolume=shell?shell.Volume:0,userFov=shell?shell.FieldOfView:0,userSensitivity=shell?shell.Sensitivity:0,userReducedMotion=shell&&shell.ReducedMotion,
                userSubtitles=shell&&shell.Subtitles,vSyncCount=QualitySettings.vSyncCount,originalTargetFrameRate=oldFrameRate,inputDevicesRestored=restored,
                lastMeasuredTargetFrameRate=lastMeasuredTargetFrameRate,lastMeasuredVSyncCount=lastMeasuredVSyncCount,frameSettingsMeasured=sorted.Length>0,
                errors=errors.ToArray(),images=images.ToArray(),cues=cues.ToArray(),pause=pause,audio=audio,timing=clocks};
            File.WriteAllText(Path.Combine(output,"native-corridor-play.json"),JsonUtility.ToJson(report,true));
        }
    }
}
