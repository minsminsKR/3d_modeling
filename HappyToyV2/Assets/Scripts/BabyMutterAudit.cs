using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Controlled legal post-chase restore and real slow navigation beside an
    // occupied cabinet. Source isolation changes audibility, never voice PCM.
    public sealed class BabyMutterAudit : MonoBehaviour
    {
        string output; bool finished;
        GameSession session; StalkerBrain brain; CorridorBabyBehaviour baby;
        readonly Dictionary<string,bool> checks = new Dictionary<string,bool>();
        readonly Dictionary<AudioSource,bool> mutes = new Dictionary<AudioSource,bool>();
        readonly List<CorridorAudioPhaseProbe.Phase> phases = new List<CorridorAudioPhaseProbe.Phase>();
        readonly List<string> errors = new List<string>();
        CorridorAudioPhaseProbe tap; RouteAudioCapture capture; float started;
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status, failure;
            public Check[] checks; public string[] errors;
            public CorridorAudioPhaseProbe.Phase[] phases; public RouteAudioCapture.Report audio;
            public string scope = "Controlled corridor Baby post-chase legal checkpoint restore, two local supported patrol markers and actual agent movement beside a real occupied cabinet. Other sources are muted, cabinet RNG is fixed only for this acoustic fixture, and pre-device natural listener DSP is retained. Speech is generated from the documented Korean text, not a recorded child. Not a natural full survival route or human listening certification.";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-v6-baby-mutter-output");
            if(index < 0 || index+1 >= args.Length) return;
            new GameObject("Quiet Korean Baby mutter audit").AddComponent<BabyMutterAudit>().output = Path.GetFullPath(args[index+1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message,string stack,LogType type) { if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert) errors.Add(message); }
        void Update()
        {
            if(finished) return;
            if(baby) foreach(var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if(source==baby.MutterSource) continue;
                if(!mutes.ContainsKey(source)) mutes.Add(source,source.mute);
                source.mute=true;
            }
            if(started>0 && Time.realtimeSinceStartup-started>90) Finish("Bounded voice fixture timed out");
        }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup; Application.runInBackground=true; Directory.CreateDirectory(output);
            var stack=new Stack<IEnumerator>(); stack.Push(Run());
            while(!finished)
            {
                bool moved=false; object next=null; string failure=null;
                try { moved=stack.Peek().MoveNext(); if(moved) next=stack.Peek().Current; }
                catch(Exception error) { failure=error.ToString(); }
                if(failure!=null) { Finish(failure); yield break; }
                if(!moved) { stack.Pop(); if(stack.Count==0) { Finish(null); yield break; } continue; }
                if(next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        void CheckValue(string name,bool passed) { checks[name]=passed; if(!passed) throw new InvalidOperationException(name); }
        IEnumerator Await(Func<bool> predicate,string failure)
        { float end=Time.realtimeSinceStartup+18; while(!predicate()&&Time.realtimeSinceStartup<end) yield return null; CheckValue(failure,predicate()); }
        IEnumerator Measure(string name,bool audible)
        {
            yield return new WaitForSecondsRealtime(.4f); tap.Begin(name,audible);
            yield return new WaitForSecondsRealtime(.65f); var phase=tap.End(); phases.Add(phase);
            CheckValue(name+" valid native PCM",phase.callbacks>=6&&phase.samples>=10000&&phase.nonfinite==0&&phase.clipped==0);
            CheckValue(name+" audible expectation",audible?phase.peak>.00001&&phase.rms>.000001:phase.peak<.00001);
        }
        Interactable Fixture(out Vector3 first,out Vector3 second)
        {
            foreach(var cabinet in session.Corridor.GetComponentsInChildren<Interactable>(true))
            {
                if(cabinet.kind!=Interactable.Kind.HidingPlace||!cabinet.InteractionAvailable||!cabinet.inside||!cabinet.outside) continue;
                var observer=session.player.CaptureProgress();observer.position=cabinet.outside.position;
                if(!session.player.CanRestoreProgress(observer))continue;
                var outward=cabinet.outside.position-cabinet.inside.position; outward.y=0; outward.Normalize();
                if(!NavMesh.SamplePosition(cabinet.outside.position+outward*2.8f,out var a,.45f,NavMesh.AllAreas)||
                    !NavMesh.SamplePosition(cabinet.outside.position+outward*4.6f,out var b,.45f,NavMesh.AllAreas)||
                    Vector3.Distance(a.position,b.position)<1||Mathf.Abs(a.position.y-b.position.y)>.1f) continue;
                var route=new NavMeshPath();
                if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete) continue;
                float length=0;var previous=a.position;foreach(var corner in route.corners){length+=Vector3.Distance(previous,corner);previous=corner;}
                if(length>5.5f)continue;
                first=a.position;second=b.position;return cabinet;
            }
            throw new InvalidOperationException("No supported local cabinet voice route");
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output); yield return null; yield return null;
            session=GameSession.Current; session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile",Guid.NewGuid().ToString("N")));
            session.CreateCorridor(211); session.Shell.Begin(); session.Shell.AdjustSettings(.8f-session.Shell.Volume,0);
            brain=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include).Single(actor=>actor.CorridorBaby);baby=brain.CorridorBaby;
            foreach(var actor in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if(actor is StalkerBrain&&actor!=brain||actor is LanternMaskEncounter||actor is WeepingAngelEncounter||actor is NavMeshStartup||
                    actor is StoryDirector||actor is AnnexEncounter||actor is UncatAnnexEvent||actor is V1HwacatEvent||actor is LovelyDollGuide)
                {actor.StopAllCoroutines();actor.enabled=false;}
            var agent=brain.GetComponent<NavMeshAgent>();foreach(var other in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include))if(other!=agent)other.enabled=false;
            var cabinet=Fixture(out var a,out var b);
            brain.patrol=new[]{new GameObject("Controlled local voice patrol A").transform,new GameObject("Controlled local voice patrol B").transform};
            brain.patrol[0].SetParent(session.Corridor.transform);brain.patrol[0].position=a;
            brain.patrol[1].SetParent(session.Corridor.transform);brain.patrol[1].position=b;
            var saved=brain.CaptureProgress();saved.active=true;saved.position=a;saved.state=StalkerBrain.State.Patrol;
            saved.waypoint=1;saved.patrolDwelling=false;saved.patrolRemaining=0;saved.awareness=0;saved.memory=0;
            saved.baby.phase=CorridorBabyMemory.Phase.WanderingCry;saved.baby.hasChased=true;saved.Validate();
            brain.RestoreProgress(saved,session.Corridor.GetComponentsInChildren<Interactable>(true));brain.enabled=true;
            session.player.enabled=false;session.player.GetComponent<CharacterController>().enabled=false;
            session.player.transform.position=cabinet.outside.position;session.player.HidingRandomSample=()=>0f;
            session.player.Hide(cabinet,cabinet.inside.position,cabinet.outside.position);Physics.SyncTransforms();
            CheckValue("actual occupied cabinet protects sight",session.player.Hidden&&!brain.CanSeePlayer());
            var imported=new[]{ExternalAudio.Shared("enemy-baby-mutter",0),ExternalAudio.Shared("enemy-baby-mutter",1)};
            CheckValue("two owned Korean voice variants",baby.OwnedMutters.Length==2&&imported.All(clip=>clip)&&
                baby.OwnedMutters.All(clip=>!imported.Contains(clip))&&!baby.MutterSource.loop&&baby.MutterSource.spatialBlend==1&&
                !baby.MutterSource.ignoreListenerPause&&!baby.MutterSource.ignoreListenerVolume);
            capture=RouteAudioCapture.Attach(Path.Combine(output,"listener-audio"));tap=session.player.eyes.gameObject.AddComponent<CorridorAudioPhaseProbe>();
            yield return Await(()=>baby.Muttering,"actual movement starts searching voice");
            CheckValue("authored slow travel",agent.velocity.magnitude>.12f&&agent.speed<=CorridorBabyBehaviour.CalmSpeed+.001f);
            yield return Measure("moving Korean mutter",true);
            CheckValue("active utterance before pause",baby.Muttering&&baby.MutterSource.timeSamples>0);
            session.Shell.Pause();int cursor=baby.MutterSource.timeSamples;var at=brain.transform.position;
            yield return Measure("paused Korean mutter",false);
            CheckValue("pause freezes active voice and body",baby.MutterSource.clip&&baby.MutterSource.timeSamples==cursor&&brain.transform.position==at);
            session.Shell.Resume();int utterance=baby.MutterUtterances;
            yield return Await(()=>baby.MutterUtterances>utterance&&baby.Muttering,"natural voice repeats during wandering");
            yield return Measure("repeated Korean mutter",true);
            baby.MutterSource.mute=true;yield return Measure("source muted Korean mutter",false);baby.MutterSource.mute=false;
            utterance=baby.MutterUtterances;
            yield return Await(()=>baby.MutterUtterances>utterance&&baby.Muttering&&baby.MutterSource.timeSamples>0,"fresh active utterance before master mute");
            CheckValue("master mute interrupts actual owned voice",baby.MutterSource.isPlaying&&baby.MutterSource.timeSamples>0);
            session.Shell.AdjustSettings(-1,0);yield return Measure("master muted Korean mutter",false);
            session.Shell.AdjustSettings(.8f,0);utterance=baby.MutterUtterances;
            yield return Await(()=>baby.MutterUtterances>utterance&&baby.Muttering,"master restore keeps searching voice");
            yield return Measure("master restored Korean mutter",true);
            baby.enabled=false;yield return Measure("disabled Korean mutter",false);CheckValue("disable stops owned voice",!baby.Muttering);
            var owned=baby.OwnedMutters.ToArray();var emitter=baby.MutterSource.gameObject;Destroy(baby);yield return null;yield return null;
            CheckValue("owned voice cleanup preserves imported resources",owned.All(clip=>!clip)&&!emitter&&imported.All(clip=>clip));
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;var audio=capture?capture.Complete():null;
            if(failure==null&&(audio==null||!audio.nonSilent||audio.truncated||audio.nonfiniteSamples>0||!string.IsNullOrEmpty(audio.writerError)))failure="Natural voice PCM missing";
            var report=new Report{status=failure==null&&errors.Count==0?"PASS":"FAIL",failure=failure??"",checks=checks.Select(x=>new Check{name=x.Key,passed=x.Value}).ToArray(),errors=errors.ToArray(),phases=phases.ToArray(),audio=audio};
            RestoreFixture();
            File.WriteAllText(Path.Combine(output,"baby-mutter.json"),JsonUtility.ToJson(report,true));Debug.Log("HAPPYTOY_BABY_MUTTER_"+report.status);Application.Quit(report.status=="PASS"?0:2);
        }
        void RestoreFixture()
        {
            foreach(var pair in mutes)if(pair.Key)pair.Key.mute=pair.Value;
            mutes.Clear();if(session&&session.player)session.player.HidingRandomSample=null;
        }
        void OnDestroy()=>RestoreFixture();
    }
}
