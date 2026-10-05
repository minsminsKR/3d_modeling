using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit native-player audit. Every route action enters through virtual
    // keyboard/mouse events; production motors, focus, interactions and AI act normally.
    public sealed class NativeSchoolPlayAudit : MonoBehaviour
    {
        const float WallBudget=170,GameBudget=150;
        string output,failure="",stage="startup",destination="";
        GameSession session;PlayerMotor player;GameShell shell;MemoryChapter chapter;
        Keyboard keyboard,oldKeyboard;Mouse mouse,oldMouse;
        InputSettings.BackgroundBehavior oldBackground;
        bool oldRunInBackground,oldCursorVisible,settingsSaved,restored,ready,expectingEscape,sprintReady=true;
        CursorLockMode oldCursorLock;
        StalkerBrain[] stalkers;Vector2[] speeds;
        RouteAudioCapture capture;
        float wallStart,routeWallStart,routeGameStart,minY,maxY,travel,ungrounded,progressWall;
        double routeDspStart;
        Vector3 previousPosition;int movementStart,monitorFrame=-1,groundedFrames,stairLegs,maxActive;
        int lastSteps,lastInteraction,lastRecovered,lastContacts,lastRecognition;
        readonly Dictionary<StalkerFootsteps,Vector2Int> enemyCueCounts=new Dictionary<StalkerFootsteps,Vector2Int>();
        bool portraitImage,nurseryImage;
        readonly List<float> frameSeconds=new List<float>();
        readonly List<string> errors=new List<string>(),milestones=new List<string>(),images=new List<string>();
        readonly List<CueEvent> events=new List<CueEvent>();
        PauseProbe pauseProbe;

        [Serializable] sealed class CueEvent
        {
            public string kind,owner,clip,detail;
            public float gameSeconds,wallSeconds;public double dspSeconds;
            public int records,steps,contacts,recognition;public Vector3 position;
            public bool recordedClip;
        }
        [Serializable] sealed class PauseProbe
        {
            public bool listenerPaused,positionFrozen,staminaFrozen,gameClockFrozen;
            public int callbacksBefore,callbacksAfter;
            public double dspAdvance;public float wallSeconds;
        }
        [Serializable] sealed class Report
        {
            public string status,failure,stage,destination,unity,graphics,device,audioDirectory;
            public string scope="Native rendered Windows player using natural listener callbacks and actual keyboard/mouse input through the full five-memory route. No teleport/Use/Collect calls, enemy suppression/speed edits, forced audio rendering or capture clock. This is one automated strategy and pre-device mixer evidence, not device listening, human fear or first-player difficulty certification.";
            public bool escaped,chapter,portraitWitnessed,portraitCompleted,nurseryReleased,inputDevicesRestored;
            public int records,movementUpdates,groundedFrames,stairLegs,maximumActiveStalkers,footsteps,contacts,recognition,frames;
            public float gameSeconds,wallSeconds,physicalMeters,minY,maxY,meanFrameMs,p95FrameMs,worstFrameMs;
            public double routeDspSeconds,dspToGameRatio,dspToWallRatio;
            public float userVolume,userFov,userSensitivity;public bool userReducedMotion,userSubtitles;
            public string[] errors,milestones,images;public CueEvent[] cueEvents;
            public PauseProbe pause;public RouteAudioCapture.Report audio;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-school-play-output");
            if(index<0 || index+1>=args.Length)return;
            var audit=new GameObject("Explicit natural-clock school play audit").AddComponent<NativeSchoolPlayAudit>();
            audit.output=Path.GetFullPath(args[index+1]);
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;RestoreSettings();}
        void Log(string message,string trace,LogType type)
        {
            if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert)&&errors.Count<64)errors.Add(message);
        }
        IEnumerator Start()
        {
            wallStart=Time.realtimeSinceStartup;Directory.CreateDirectory(output);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            try
            {
                while(stack.Count>0 && string.IsNullOrEmpty(failure))
                {
                    var iterator=stack.Peek();bool more=false;object current=null;
                    try{more=iterator.MoveNext();if(more)current=iterator.Current;}
                    catch(Exception error){failure=error.GetType().Name+": "+error.Message;}
                    if(!string.IsNullOrEmpty(failure))break;
                    if(!more){stack.Pop();(iterator as IDisposable)?.Dispose();continue;}
                    if(current is IEnumerator nested){stack.Push(nested);continue;}
                    yield return current;
                }
            }
            finally
            {
                while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
                Keys();RestoreSettings();
                var audio=capture?capture.Complete():null;
                Save(audio);Debug.Log("HAPPYTOY_NATIVE_SCHOOL_PLAY_"+(string.IsNullOrEmpty(failure)?"PASS":"FAIL"));
                Application.Quit(string.IsNullOrEmpty(failure)?0:2);
            }
        }
        IEnumerator Run()
        {
            yield return null;yield return null;
            session=GameSession.Current;Require(session&&session.player&&session.Shell,"Authored session/player/shell unavailable");
            player=session.player;shell=session.Shell;
            Require(Application.platform==RuntimePlatform.WindowsPlayer,"Audit requires the native Windows player");
            Require(Time.captureDeltaTime==0,"A forced capture clock is active");
            Require(shell.Screen==GameShell.Page.Title,"CLI audit must enter a fresh title school");
            // All artificial results/saves belong here, never the user's profile.
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile",Guid.NewGuid().ToString("N")));
            oldBackground=InputSystem.settings.backgroundBehavior;oldRunInBackground=Application.runInBackground;
            oldCursorLock=Cursor.lockState;oldCursorVisible=Cursor.visible;
            oldKeyboard=Keyboard.current;oldMouse=Mouse.current;settingsSaved=true;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>("Native school audit keyboard");mouse=InputSystem.AddDevice<Mouse>("Native school audit mouse");
            keyboard.MakeCurrent();mouse.MakeCurrent();
            capture=RouteAudioCapture.Attach(Path.Combine(output,"audio"));
            shell.BeginChapter();Require(session.ChapterMode,"Native BeginChapter did not prepare the school");chapter=session.Chapter;
            stalkers=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(actor=>actor.gameObject.scene==session.gameObject.scene).ToArray();
            speeds=stalkers.Select(actor=>new Vector2(actor.patrolSpeed,actor.chaseSpeed)).ToArray();
            Require(stalkers.Length==4 && stalkers.All(actor=>!actor.gameObject.activeInHierarchy),"Opening chapter actor gate changed");
            foreach(var steps in FindObjectsByType<StalkerFootsteps>(FindObjectsInactive.Include,FindObjectsSortMode.None))enemyCueCounts[steps]=Vector2Int.zero;
            previousPosition=player.transform.position;minY=maxY=previousPosition.y;movementStart=player.MovementUpdates;
            ready=true;
            yield return Await(()=>player.Grounded&&capture.HasCallbacks&&capture.Snapshot().nonSilent,8,"Native listener supplied no non-silent raw callbacks");
            Milestone("Raw listener callback confirmed before route");Frame("native-school-entrance.png");
            // An actual Escape input supplies pause evidence before threats awaken.
            yield return Pulse(Key.Escape);Require(shell.Screen==GameShell.Page.Pause,"Escape did not pause native gameplay");
            float game=session.ElapsedPlayTime,stamina=player.Stamina;Vector3 position=player.transform.position;
            double dsp=AudioSettings.dspTime;float pauseStart=Time.realtimeSinceStartup;
            pauseProbe=new PauseProbe {listenerPaused=AudioListener.pause,callbacksBefore=capture.CallbackCount};
            yield return new WaitForSecondsRealtime(.45f);
            pauseProbe.callbacksAfter=capture.CallbackCount;pauseProbe.dspAdvance=AudioSettings.dspTime-dsp;
            pauseProbe.wallSeconds=Time.realtimeSinceStartup-pauseStart;pauseProbe.positionFrozen=player.transform.position==position;
            pauseProbe.staminaFrozen=player.Stamina==stamina;pauseProbe.gameClockFrozen=session.ElapsedPlayTime==game;
            Require(pauseProbe.listenerPaused&&pauseProbe.positionFrozen&&pauseProbe.staminaFrozen&&pauseProbe.gameClockFrozen,"Actual native pause failed to freeze gameplay");
            yield return Pulse(Key.Escape);Require(shell.Screen==GameShell.Page.Playing,"Escape did not resume native gameplay");
            routeWallStart=Time.realtimeSinceStartup;routeDspStart=AudioSettings.dspTime;routeGameStart=session.ElapsedPlayTime;progressWall=routeWallStart;
            yield return Walk(new Vector3(-4.5f,0,0),false);yield return OpenDoor("WASHROOM");yield return Flashlight(false);
            yield return Take("chapter-memory-0");Require(stalkers.Count(actor=>actor.gameObject.activeInHierarchy)==1,"First memory did not release one authored stalker");
            yield return Take("chapter-memory-1");Require(chapter.Mannequin.gameObject.activeInHierarchy&&!chapter.Mask.gameObject.activeInHierarchy,"Second memory actor gate failed");
            yield return Walk(new Vector3(29.8f,0,10),true);yield return Stair(new Vector3(29.8f,5,22),"upper ascent",false);
            yield return Take("chapter-memory-2");Require(chapter.Mask.gameObject.activeInHierarchy,"Third memory did not release the upper mask");
            yield return Rest();var fourth=Record("chapter-memory-3");stage="mandatory portrait";
            yield return Approach(fourth,true);yield return Interact(fourth);
            Require(session.RecordsRecovered==3&&chapter.Portrait.Triggered,"Fourth memory bypassed its authored portrait reveal");
            yield return Walk(new Vector3(34.7f,5,32.2f),true);Keys();float witnessStart=GameTime;
            while(!chapter.Portrait.ChapterWitnessed)
            {Check();Require(GameTime-witnessStart<12,"Actual native camera never witnessed portrait");Steer(chapter.Portrait.spawn+Vector3.up*.9f,false);yield return null;}
            yield return Walk(new Vector3(34.7f,5,23.2f),true);yield return Walk(new Vector3(25.2f,5,22.8f),true);yield return Walk(new Vector3(25.2f,5,32.8f),true);
            yield return Await(()=>chapter.Portrait.Completed,8,"Portrait never completed after actual escape loop");yield return Take("chapter-memory-3");
            yield return Walk(new Vector3(29.8f,5,22),true);yield return Stair(new Vector3(29.8f,0,10),"upper descent",false);
            yield return Walk(new Vector3(13.8f,0,-10),true);yield return Stair(new Vector3(13.8f,-5,-22),"basement descent",false);
            yield return Await(()=>chapter.Nursery.Released,12,"Actual basement entry never released the baby");yield return Take("chapter-memory-4");
            yield return Walk(new Vector3(13.8f,-5,-22),true);yield return Stair(new Vector3(13.8f,0,-10),"basement ascent",true);
            yield return Walk(new Vector3(-4.5f,0,0),true);var exit=Items().Single(item=>item.kind==Interactable.Kind.Exit);stage="chapter exit";
            yield return Approach(exit,true);expectingEscape=true;yield return Interact(exit);
            Require(session.Escaped&&session.Finished&&session.RecordsRecovered==5,"Actual native E input did not finish the five-memory escape");
            Require(stairLegs==4&&minY< -4.8f&&maxY>4.8f&&travel>100,"Native full route lacks four physical stair legs/three floors/travel");
            yield return new WaitForSecondsRealtime(.2f);
            var audio=capture.Snapshot();Require(audio.callbacks>10&&audio.channels==2&&audio.nonSilent&&!audio.truncated&&audio.nonfiniteSamples==0,"Native raw audio capture is incomplete/invalid");
            Require(audio.stages.All(segment=>segment.clipped==0),"Natural native combined mix clips");
            Require(audio.floors.All(segment=>segment.samples>audio.sampleRate&&segment.rms>.00002),"Native mix missing an audible physical floor");
            Require(audio.stages.All(segment=>segment.samples>0),"Natural listener stream missed a chapter progression stage");
            Require(audio.pause.steadySamples==0 || audio.pause.steadyPeak<.00001,"Steady actual listener pause leaks PCM");
            Require(errors.Count==0,"Native runtime logged an error");Milestone("Escaped all five memories through real native input");
        }
        float GameTime=>session?session.ElapsedPlayTime-routeGameStart:0;
        void Update()
        {
            if(!ready || !session)return;
            if(session.InputAllowed)frameSeconds.Add(Time.unscaledDeltaTime);
            ObserveCues();
            if(chapter.Portrait.ChapterWitnessed&&!portraitImage){portraitImage=true;Frame("native-school-portrait-witnessed.png");}
            if(player.transform.position.y< -4.6f&&!nurseryImage){nurseryImage=true;Frame("native-school-nursery.png");}
        }
        void ObserveCues()
        {
            var feedback=player.Feedback;var tension=player.GetComponent<PerceivedTension>();
            if(feedback.FootstepsPlayed!=lastSteps){lastSteps=feedback.FootstepsPlayed;Event("player-step",player.name,feedback.LastFootstepClip,feedback.LastFootstepSurface);}
            if(feedback.InteractionCuesPlayed!=lastInteraction){lastInteraction=feedback.InteractionCuesPlayed;Event("player-interaction",player.name,feedback.LastInteractionClip,"");}
            if(session.RecordsRecovered!=lastRecovered){lastRecovered=session.RecordsRecovered;Event("memory",session.CurrentObjectiveId,feedback.LastInteractionClip,"recovered="+lastRecovered);}
            if(tension&&tension.ContactEvents!=lastContacts){lastContacts=tension.ContactEvents;Event("perceived-contact",player.name,null,"count="+lastContacts);}
            if(tension&&tension.RecognitionEvents!=lastRecognition){lastRecognition=tension.RecognitionEvents;Event("witnessed-recognition",player.name,null,"count="+lastRecognition);}
            foreach(var pair in enemyCueCounts.ToArray())
            {
                var steps=pair.Key;if(!steps)continue;var counts=new Vector2Int(steps.StepsPlayed,steps.AttackCuesPlayed);
                if(counts.x!=pair.Value.x)Event("enemy-step",steps.name,steps.MovementClip,steps.Profile.ToString());
                if(counts.y!=pair.Value.y)Event("enemy-attack",steps.name,steps.AttackClip,"count="+counts.y);
                enemyCueCounts[steps]=counts;
            }
        }
        void Event(string kind,string owner,AudioClip clip,string detail)
        {
            if(events.Count>=4096)return;
            events.Add(new CueEvent {kind=kind,owner=owner,clip=clip?clip.name:"",detail=detail,
                recordedClip=clip&&clip.name.StartsWith("External ",StringComparison.Ordinal),gameSeconds=session.ElapsedPlayTime,
                wallSeconds=Time.realtimeSinceStartup-wallStart,dspSeconds=AudioSettings.dspTime,records=session.RecordsRecovered,
                steps=player.Feedback.FootstepsPlayed,contacts=lastContacts,recognition=lastRecognition,position=player.transform.position});
        }
        void Check()
        {
            Require(Time.realtimeSinceStartup-wallStart<WallBudget,"Native route wall deadline reached");
            Require(GameTime<GameBudget,"Native route game deadline reached");
            Require(!session.Finished || expectingEscape&&session.Escaped,"Native survival route ended in defeat: "+session.DefeatSource);
            if(session.Finished)return;
            Require(session.InputAllowed&&player.enabled,"Actual gameplay lost input/motor");Require(Time.timeScale==1&&Time.captureDeltaTime==0,"Natural simulation clock changed");
            Require(Keyboard.current==keyboard&&Mouse.current==mouse,"Native virtual input device ownership changed");
            Require(chapter.Portrait.enabled&&chapter.Nursery.enabled,"Authored encounter owner was disabled");
            for(int i=0;i<stalkers.Length;i++)Require(new Vector2(stalkers[i].patrolSpeed,stalkers[i].chaseSpeed)==speeds[i],"Authored threat speed changed");
            if(monitorFrame==Time.frameCount)return;monitorFrame=Time.frameCount;
            Vector3 at=player.transform.position;Require(StealthRules.Finite(at.x)&&StealthRules.Finite(at.y)&&StealthRules.Finite(at.z)&&at.y>=-5.5f&&at.y<=5.6f,"Player left real authored floors");
            minY=Mathf.Min(minY,at.y);maxY=Mathf.Max(maxY,at.y);travel+=Vector3.Distance(at,previousPosition);previousPosition=at;
            if(player.Grounded){groundedFrames++;ungrounded=0;}else ungrounded+=Time.deltaTime;
            Require(ungrounded<1.5f,"Native controller lost physical floor contact");maxActive=Mathf.Max(maxActive,stalkers.Count(actor=>actor.isActiveAndEnabled));
            if(Time.realtimeSinceStartup-progressWall>15){progressWall=Time.realtimeSinceStartup;Milestone("Route progress "+stage);}
        }
        IEnumerator Await(Func<bool> condition,float seconds,string why)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!condition()){if(session&&session.InputAllowed)Check();Require(Time.realtimeSinceStartup<until,why);yield return null;}
        }
        void Keys(params Key[] held)
        {if(keyboard!=null&&keyboard.added)InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));}
        IEnumerator Pulse(Key key)
        {
            Keys();yield return null;
            if(shell.Screen==GameShell.Page.Playing)Check();Keys(key);yield return null;Keys();yield return null;
            if(shell.Screen==GameShell.Page.Playing)Check();
        }
        void Steer(Vector3 point,bool level)
        {
            Vector3 delta=point-player.eyes.transform.position;
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            float pitch=level?0:Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-77,77);
            Require(player.sensitivity>0,"Native mouse sensitivity invalid");
            var pixels=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),-40,40),
                -Mathf.Clamp(Mathf.DeltaAngle(player.eyes.transform.localEulerAngles.x,pitch),-30,30))/player.sensitivity;
            InputSystem.QueueDeltaStateEvent(mouse.delta,pixels);
        }
        bool Sprint(bool requested)
        {if(player.Stamina<.15f)sprintReady=false;if(player.Stamina>.85f)sprintReady=true;return requested&&sprintReady&&!player.SprintExhausted;}
        static float Horizontal(Vector3 value)=>new Vector2(value.x,value.z).magnitude;
        IEnumerator Walk(Vector3 target,bool sprint)
        {
            Check();destination=target.ToString("F2");var path=new NavMeshPath();
            Require(NavMesh.CalculatePath(player.transform.position,target,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete&&path.corners.Length>0,"No complete authored route");
            foreach(var corner in path.corners.Skip(1)){Require(Mathf.Abs(corner.y-target.y)<1,"Unexpected floor transition on level leg");yield return MoveTo(corner,sprint,false);}
            Keys();yield return null;Check();Require(Horizontal(player.transform.position-target)<.35f&&Mathf.Abs(player.transform.position.y-target.y)<.4f,"Native walking missed its real destination");
        }
        IEnumerator Stair(Vector3 target,string label,bool sprint)
        {
            stage=label;destination=target.ToString("F2");Vector3 from=player.transform.position;var path=new NavMeshPath();
            Require(Mathf.Abs(from.y-target.y)>4.5f&&Mathf.Abs(from.x-target.x)<.4f,"Invalid authored stair start");
            Require(NavMesh.CalculatePath(from,target,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Authored stairs disconnected");
            yield return MoveTo(target,sprint,true);Keys();yield return Await(()=>player.Grounded,2,"Actual stair landing never grounded");
            Require(Mathf.Abs(player.transform.position.y-target.y)<.4f,"Wrong native stair height");stairLegs++;Milestone(label+" physically completed");
        }
        IEnumerator MoveTo(Vector3 target,bool sprint,bool stair)
        {
            float began=Time.realtimeSinceStartup,best=Horizontal(target-player.transform.position),lastProgress=began;
            while(true)
            {
                Check();Vector3 delta=target-player.transform.position;float distance=Horizontal(delta);
                if(distance<.18f&&Mathf.Abs(delta.y)<.4f)break;
                if(distance<best-.04f){best=distance;lastProgress=Time.realtimeSinceStartup;}
                Require(Time.realtimeSinceStartup-began<(stair?25:45)&&Time.realtimeSinceStartup-lastProgress<8,"No physical route progress toward "+target);
                Require(distance>.025f||Mathf.Abs(delta.y)<.4f,"Arrived horizontally at wrong stair height");
                Steer(player.eyes.transform.position+new Vector3(delta.x,0,delta.z),true);
                float angle=Vector3.Angle(new Vector3(player.transform.forward.x,0,player.transform.forward.z),new Vector3(delta.x,0,delta.z));
                Keys(angle>12?Array.Empty<Key>():Sprint(sprint&&distance>.75f)?new[]{Key.W,Key.LeftShift}:new[]{Key.W});yield return null;
            }
        }
        Interactable[] Items()=>FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(item=>item.gameObject.scene==session.gameObject.scene).ToArray();
        Interactable Record(string id)=>Items().Single(item=>item.stableId==id);
        IEnumerator Approach(Interactable item,bool sprint)
        {Require(FindApproach(item,out var position),"No ray-clear physical approach for "+item.name);yield return Walk(position,sprint);}
        bool FindApproach(Interactable item,out Vector3 position)
        {
            float best=float.PositiveInfinity;position=Vector3.zero;var controller=player.GetComponent<CharacterController>();
            Vector3 eyeOffset=player.eyes.transform.position-player.transform.position;
            foreach(var collider in item.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||collider.isTrigger)continue;Vector3 center=collider.bounds.center;
                for(int i=0;i<96;i++)
                {
                    float angle=i%32*Mathf.PI/16,radius=.65f+i/32*.45f;
                    if(!NavMesh.SamplePosition(center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius,out var hit,2.2f,NavMesh.AllAreas))continue;
                    Vector3 at=hit.position;if(center.y-at.y<-.25f||center.y-at.y>2.1f)continue;
                    Vector3 capsule=at+controller.center;float half=controller.height*.5f-controller.radius;
                    if(Physics.CheckCapsule(capsule-Vector3.up*half,capsule+Vector3.up*half,controller.radius-.015f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                    Vector3 eye=at+eyeOffset,ray=center-eye;
                    if(ray.magnitude>2.15f||!Physics.Raycast(eye,ray.normalized,out var sight,2.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||sight.collider.GetComponentInParent<Interactable>()!=item)continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(player.transform.position,at,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    float length=0;for(int n=1;n<path.corners.Length;n++)length+=Vector3.Distance(path.corners[n-1],path.corners[n]);
                    if(length>=best)continue;best=length;position=at;
                }
            }
            return !float.IsPositiveInfinity(best);
        }
        IEnumerator Interact(Interactable item)
        {
            Keys();float began=Time.realtimeSinceStartup;
            while(player.Focus!=item)
            {
                Check();Require(Time.realtimeSinceStartup-began<4,"Actual E focus never reached "+item.name);
                var collider=item.GetComponentsInChildren<Collider>().Where(value=>value.enabled&&!value.isTrigger).OrderBy(value=>Vector3.Distance(value.bounds.center,player.eyes.transform.position)).First();
                Steer(collider.bounds.center,false);yield return null;
            }
            yield return Pulse(Key.E);
        }
        IEnumerator OpenDoor(string room)
        {
            stage="open "+room;var door=Items().Single(item=>item.kind==Interactable.Kind.Door&&item.name.StartsWith(room));
            Require(!door.IsOpen,"Native route expects original closed door");Vector3 target=door.movingLeaf.localPosition+door.openOffset;
            Vector3 second=door.secondaryLeaf?door.secondaryLeaf.localPosition-door.openOffset:Vector3.zero;
            yield return Interact(door);Require(door.IsOpen,"Actual E did not open door");
            yield return Await(()=>Vector3.Distance(door.movingLeaf.localPosition,target)<.02f&&(!door.secondaryLeaf||Vector3.Distance(door.secondaryLeaf.localPosition,second)<.02f)&&!door.obstacle.enabled,4,"Physical door/nav carving never opened");
            Milestone(room+" opened through E");
        }
        IEnumerator Take(string id)
        {
            stage=id;Require(session.CurrentObjectiveId==id,"Native route objective order changed");var item=Record(id);int before=session.RecordsRecovered;
            yield return Approach(item,session.StoryStep>=2);yield return Interact(item);
            Require(session.RecordsRecovered==before+1&&!item.gameObject.activeSelf,"Actual E did not recover exactly one memory: "+id);Milestone("Recovered "+id);
        }
        IEnumerator Rest()
        {Keys();yield return Await(()=>player.Stamina>=.98f&&!player.SprintExhausted,12,"Real stamina failed to recover");sprintReady=true;}
        IEnumerator Flashlight(bool on)
        {Require(player.flashlight,"Actual flashlight missing");if(player.flashlight.enabled!=on)yield return Pulse(Key.F);Require(player.flashlight.enabled==on,"Actual F did not toggle flashlight");}
        void Require(bool condition,string why)
        {if(!condition)throw new InvalidOperationException(why+"; stage="+stage+", destination="+destination+", records="+(session?session.RecordsRecovered:-1)+", at="+(player?player.transform.position.ToString("F2"):"missing"));}
        void Milestone(string message)
        {string line=(session?session.ElapsedPlayTime:0).ToString("F2")+"s "+message;milestones.Add(line);Debug.Log("HAPPYTOY_NATIVE_SCHOOL_PROGRESS "+line);}
        void Frame(string name)
        {
            var target=new RenderTexture(640,360,24);target.Create();var old=RenderTexture.active;Texture2D frame=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;frame=new Texture2D(640,360,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,640,360),0,0);frame.Apply();
                File.WriteAllBytes(Path.Combine(output,name),frame.EncodeToPNG());images.Add(name);
            }
            finally{RenderTexture.active=old;if(frame)Destroy(frame);target.Release();Destroy(target);}
        }
        void RestoreSettings()
        {
            if(restored)return;restored=true;
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            if(settingsSaved){InputSystem.settings.backgroundBehavior=oldBackground;Application.runInBackground=oldRunInBackground;Cursor.lockState=oldCursorLock;Cursor.visible=oldCursorVisible;}
        }
        void Save(RouteAudioCapture.Report audio)
        {
            if(string.IsNullOrEmpty(failure)&&(!session||!session.Escaped))failure="Native route did not prove escape";
            if(string.IsNullOrEmpty(failure)&&(audio==null || audio.truncated || !string.IsNullOrEmpty(audio.writerError)))failure="Native audio files were not retained completely";
            var sorted=frameSeconds.OrderBy(value=>value).ToArray();float game=session?session.ElapsedPlayTime-routeGameStart:0,wall=routeWallStart>0?Time.realtimeSinceStartup-routeWallStart:0;
            double dsp=routeWallStart>0?AudioSettings.dspTime-routeDspStart:0;var tension=player?player.GetComponent<PerceivedTension>():null;
            var report=new Report {status=string.IsNullOrEmpty(failure)?"PASS":"FAIL",failure=failure,stage=stage,destination=destination,
                unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,
                audioDirectory=capture?capture.OutputDirectory:"",escaped=session&&session.Escaped,chapter=session&&session.ChapterMode,
                portraitWitnessed=chapter&&chapter.Portrait.ChapterWitnessed,portraitCompleted=chapter&&chapter.Portrait.Completed,nurseryReleased=chapter&&chapter.Nursery.Released,
                records=session?session.RecordsRecovered:0,movementUpdates=player?player.MovementUpdates-movementStart:0,groundedFrames=groundedFrames,
                stairLegs=stairLegs,maximumActiveStalkers=maxActive,footsteps=player&&player.Feedback?player.Feedback.FootstepsPlayed:0,
                contacts=tension?tension.ContactEvents:0,recognition=tension?tension.RecognitionEvents:0,frames=sorted.Length,
                gameSeconds=game,wallSeconds=wall,routeDspSeconds=dsp,dspToGameRatio=game>0?dsp/game:0,dspToWallRatio=wall>0?dsp/wall:0,
                physicalMeters=travel,minY=minY,maxY=maxY,meanFrameMs=sorted.Length>0?sorted.Average()*1000:0,
                p95FrameMs=sorted.Length>0?sorted[Math.Min(sorted.Length-1,Mathf.FloorToInt(sorted.Length*.95f))]*1000:0,worstFrameMs=sorted.Length>0?sorted.Last()*1000:0,
                userVolume=shell?shell.Volume:0,userFov=shell?shell.FieldOfView:0,userSensitivity=shell?shell.Sensitivity:0,
                userReducedMotion=shell&&shell.ReducedMotion,userSubtitles=shell&&shell.Subtitles,inputDevicesRestored=restored,
                errors=errors.ToArray(),milestones=milestones.ToArray(),images=images.ToArray(),cueEvents=events.ToArray(),pause=pauseProbe,audio=audio};
            File.WriteAllText(Path.Combine(output,"native-school-play.json"),JsonUtility.ToJson(report,true));
        }
    }
}
