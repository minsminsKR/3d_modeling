using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit silent native proof of head-only animation; absent in ordinary play.
    public sealed class BaeksugwiHeadProbe:MonoBehaviour
    {
        string output;bool finished;float started;
        readonly List<string> errors=new List<string>();readonly List<Frame> frames=new List<Frame>();
        [Serializable]sealed class Frame{public string image,stage;public float time,headAngle,rollDegrees,bodyRotationError,bodyPositionError,soleY,roofY;}
        [Serializable]sealed class Report
        {
            public string status,failure;
            public string scope="Actual title UI at timeScale zero, followed by controlled seed-73 corridor Chase using the real actor/prefab/camera. AI stopped for pose capture. Body joints compared while the head moves; pause and calm transition checked. Not a full survival, audio or hardware performance certification.";
            public float titleHeadSwing,chaseHeadSwing,titleMinRoll,titleMaxRoll,chaseMinRoll,chaseMaxRoll,clipSeconds;public bool pauseFrozen,calmHeadStopped,titleBodyFixed,chaseBodyFixed;
            public Frame[] frames;public string[] errors;
        }
        readonly Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v2-baeksugwi-head-output");
            if(at>=0&&at+1<args.Length)new GameObject("Explicit Baeksugwi head diagnostic").AddComponent<BaeksugwiHeadProbe>().output=Path.GetFullPath(args[at+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Update(){if(!finished&&started>0&&Time.realtimeSinceStartup-started>240)Finish("Native head diagnostic timeout");}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(!finished)
            {
                bool moved=false;object next=null;string failure=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}catch(Exception e){failure=e.ToString();}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved){stack.Pop();if(stack.Count==0){Finish(null);yield break;}continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static float Roll(Transform root,Transform head,Quaternion neutral)
        {
            var delta=Quaternion.Inverse(root.rotation)*head.rotation*Quaternion.Inverse(neutral)*root.rotation;
            return Vector3.SignedAngle(Vector3.up,Vector3.ProjectOnPlane(delta*Vector3.up,Vector3.forward),Vector3.forward);
        }
        sealed class BodyPose
        {
            readonly Transform[] joints;readonly Quaternion[] rotations;readonly Vector3[] positions;
            public BodyPose(GameObject creature){joints=creature.GetComponentsInChildren<Transform>(true).Where(t=>t.name!="Head").ToArray();rotations=joints.Select(t=>t.localRotation).ToArray();positions=joints.Select(t=>t.localPosition).ToArray();}
            public void Check(Frame frame)
            {
                for(int i=0;i<joints.Length;i++){frame.bodyRotationError=Mathf.Max(frame.bodyRotationError,Quaternion.Angle(rotations[i],joints[i].localRotation));frame.bodyPositionError=Mathf.Max(frame.bodyPositionError,Vector3.Distance(positions[i],joints[i].localPosition));}
                Require(frame.bodyRotationError<.01f&&frame.bodyPositionError<.00001f,"Non-head joint moved in "+frame.stage);
            }
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);yield return null;yield return null;
            var session=GameSession.Current;Require(session&&session.Shell&&session.player,"Actual session missing");
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));session.Shell.RestoreDefaultSettings();
            var view=session.GetComponent<GameShellView>();view.SetCaptureSize(1280,720);
            for(int i=0;i<5;i++)yield return null;
            var title=FindAnyObjectByType<CinematicTitleScene>();var creature=GameObject.Find("Actual Baeksugwi source clone");
            Require(title&&creature&&Time.timeScale==0,"Actual title at paused game clock missing");
            var head=creature.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");var first=head.localRotation;var body=new BodyPose(creature);
            title.HeadMotionClip.SampleAnimation(creature,0);var neutral=head.rotation;
            report.clipSeconds=title.HeadMotionClip.length;
            for(int i=0;i<96;i++)
            {
                yield return new WaitForSecondsRealtime(report.clipSeconds/96);yield return new WaitForEndOfFrame();
                var frame=new Frame{stage="title",image="title-"+i.ToString("D2")+".png",time=title.HeadMotionTime,headAngle=Quaternion.Angle(first,head.localRotation),rollDegrees=Roll(creature.transform,head,neutral)};
                report.titleMinRoll=Mathf.Min(report.titleMinRoll,frame.rollDegrees);report.titleMaxRoll=Mathf.Max(report.titleMaxRoll,frame.rollDegrees);
                body.Check(frame);report.titleHeadSwing=Mathf.Max(report.titleHeadSwing,frame.headAngle);frames.Add(frame);
                var screenshot=ReadUi(view.CaptureTarget);File.WriteAllBytes(Path.Combine(output,frame.image),screenshot.EncodeToPNG());Destroy(screenshot);
            }
            Require(report.titleHeadSwing>15,"Title head stayed still");report.titleBodyFixed=true;
            Require(report.titleMinRoll<-35&&report.titleMaxRoll>35,"Title needs pronounced roll in both directions");
            session.CreateCorridor(73);session.Shell.Begin();session.ToggleCheatSafeMode();
            foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None)){actor.enabled=false;actor.gameObject.SetActive(false);}
            for(int i=0;i<3;i++)Require(session.Corridor.Collect("memory-"+i),"Actual mask release failed");
            yield return null;yield return null;
            var mask=session.Corridor.Mask;mask.enabled=false;session.player.enabled=false;session.player.GetComponent<CharacterController>().enabled=false;
            var state=mask.CaptureChapterProgress();state.state=LanternMaskEncounter.Phase.Chase;state.memory=8;state.doorPassage=null;state.investigation=null;
            mask.RestoreCorridorProgress(state);var agent=mask.GetComponent<NavMeshAgent>();agent.isStopped=true;
            foreach(var door in session.Corridor.GetComponentsInChildren<Interactable>(true))if(door.kind==Interactable.Kind.Door)door.OpenForPursuer();
            yield return new WaitForSeconds(.8f);
            var visual=mask.GetComponent<MaskHorrorVisual>();Require(visual&&visual.HeadMotionClip==title.HeadMotionClip,"Title and Chase must share the same head action");
            var camera=session.player.eyes;session.player.flashlight.enabled=true;
            bool found=false;
            foreach(var direction in new[]{mask.transform.forward,mask.transform.right,-mask.transform.right,-mask.transform.forward})
            {
                if(!NavMesh.SamplePosition(mask.transform.position+direction*3.2f,out var site,.5f,NavMesh.AllAreas))continue;
                session.player.transform.position=site.position+Vector3.up*.03f;visual.AttachmentBounds(out var bounds,out _);camera.transform.LookAt(bounds.center);
                Physics.SyncTransforms();if(Physics.Linecast(camera.transform.position,visual.HeadSocket.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                var facing=camera.transform.position-mask.transform.position;facing.y=0;mask.transform.rotation=Quaternion.LookRotation(facing);found=true;break;
            }
            Require(found,"No supported unobstructed real corridor camera");yield return null;yield return null;
            head=visual.HeadSocket;first=head.localRotation;body=new BodyPose(visual.BodyModel.gameObject);var mesh=new Mesh();
            visual.HeadMotionClip.SampleAnimation(visual.BodyModel.gameObject,0);neutral=head.rotation;
            try
            {
                for(int i=0;i<96;i++)
                {
                    yield return new WaitForSeconds(report.clipSeconds/96);
                    var frame=new Frame{stage="chase",image="chase-"+i.ToString("D2")+".png",time=visual.HeadMotionTime,headAngle=Quaternion.Angle(first,head.localRotation),rollDegrees=Roll(visual.BodyModel,head,neutral),soleY=float.MaxValue,roofY=float.MinValue};body.Check(frame);
                    report.chaseMinRoll=Mathf.Min(report.chaseMinRoll,frame.rollDegrees);report.chaseMaxRoll=Mathf.Max(report.chaseMaxRoll,frame.rollDegrees);
                    foreach(var skin in visual.BodyModel.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(mesh,false);var inherited=skin.transform.parent.lossyScale;
                        foreach(var point in mesh.vertices){var world=skin.transform.TransformPoint(new Vector3(point.x/inherited.x,point.y/inherited.y,point.z/inherited.z));frame.soleY=Mathf.Min(frame.soleY,world.y);frame.roofY=Mathf.Max(frame.roofY,world.y);}
                    }
                    Require(frame.soleY>mask.transform.position.y-.015f&&frame.roofY<mask.transform.position.y+2.44f,"Head action crosses actual corridor floor/roof: sole="+frame.soleY+" roof="+frame.roofY+" actor="+mask.transform.position.y);
                    report.chaseHeadSwing=Mathf.Max(report.chaseHeadSwing,frame.headAngle);frames.Add(frame);Capture(camera,frame.image);
                }
            }
            finally{Destroy(mesh);}
            Require(report.chaseHeadSwing>15,"Chase head stayed still");report.chaseBodyFixed=true;
            Require(report.chaseMinRoll<-35&&report.chaseMaxRoll>35,"Chase needs pronounced roll in both directions");
            session.Shell.Pause();float paused=visual.HeadMotionTime;var rotation=head.localRotation;yield return new WaitForSecondsRealtime(.25f);
            report.pauseFrozen=visual.HeadMotionTime==paused&&Quaternion.Angle(rotation,head.localRotation)<.01f;Require(report.pauseFrozen,"Chase head moved during pause");
            session.Shell.Resume();state=mask.CaptureChapterProgress();state.state=LanternMaskEncounter.Phase.Wander;state.memory=0;state.doorPassage=null;state.investigation=null;mask.RestoreCorridorProgress(state);
            yield return null;yield return null;report.calmHeadStopped=visual.HeadMotionTime==0&&!visual.HeadWrithing;Require(report.calmHeadStopped,"Calm head still writhing");
        }
        static Texture2D ReadUi(RenderTexture target)
        {
            var previous=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();return texture;}
            catch{Destroy(texture);throw;}finally{RenderTexture.active=previous;}
        }
        void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(1280,720,24);target.Create();var previous=RenderTexture.active;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(output,filename),texture.EncodeToPNG());RenderTexture.active=previous;target.Release();Destroy(target);Destroy(texture);
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;report.failure=failure;report.status=failure==null&&errors.Count==0?"PASS":"FAIL";report.frames=frames.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"baeksugwi-head.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="PASS"?0:1);
        }
    }
}
