using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in, silent release-player proof of the real menu button and loading frames.
    public sealed class MenuApproachProbe : MonoBehaviour
    {
        string output; bool school,finished; float started;
        readonly List<string> errors=new List<string>();
        readonly List<Frame> frames=new List<Frame>();
        [Serializable] sealed class Frame { public string image; public float seconds,distance,headAngle,floorGap; public int renders; }
        [Serializable] sealed class Report
        {
            public string status,failure,mode;
            public string scope="Real release menu pointer click, actual frame-sliced world creation and async navigation, unscaled title approach, blocked input, playable mode entry. Not full-route survival or audio certification.";
            public float loadingSeconds,maxLoadingFrameSeconds,approachDistance,wristSwing;
            public int loadingFrames,titleRenders; public bool inputBlocked,modeReady;
            public float longestPreparationStep; public string longestPreparationStepName;
            public Frame[] frames; public string[] errors;
        }
        readonly Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-menu-approach-output");
            if(index<0 || index+1>=args.Length)return;
            var probe=new GameObject("Explicit silent loading approach proof").AddComponent<MenuApproachProbe>();
            probe.output=Path.GetFullPath(args[index+1]);probe.school=args.Contains("-v2-menu-approach-school");
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Update(){if(!finished&&started>0&&Time.realtimeSinceStartup-started>180)Finish("Approach proof timeout");}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);
            var run=RunPreparation.Flatten(Run());
            while(!finished)
            {
                bool more=false;object next=null;string failure=null;
                try{more=run.MoveNext();if(more)next=run.Current;}catch(Exception error){failure=error.ToString();}
                if(failure!=null){Finish(failure);break;}
                if(!more){Finish(null);break;}yield return next;
            }
            (run as IDisposable)?.Dispose();
        }
        IEnumerator Run()
        {
            GameSession session=null;GameShellView view=null;CinematicTitleScene title=null;
            while(!session||!view||!title||view.Root?.Q<Button>(school?"begin-school":"begin")==null)
            {session=GameSession.Current;view=FindAnyObjectByType<GameShellView>();title=FindAnyObjectByType<CinematicTitleScene>();yield return null;}
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-saves"));
            var shell=session.Shell;shell.RestoreDefaultSettings();view.SetCaptureSize(1600,900);
            yield return null;yield return new WaitForSecondsRealtime(.3f);
            var creature=GameObject.Find("Actual Baeksugwi source clone");
            var nodes=creature.GetComponentsInChildren<Transform>();var head=nodes.Single(t=>t.name=="Head");
            var wrist=nodes.First(t=>t.name=="Arm1L_Wrist");var firstWrist=wrist.localRotation;var firstHead=head.localRotation;
            var pivot=creature.transform.parent;var firstPosition=pivot.position;
            Save(view,"title.png");
            var button=view.Root.Q<Button>(school?"begin-school":"begin");
            var point=button.worldBound.center;var target=view.Root.panel.Pick(point);
            Require(target!=null&&(target==button||button.Contains(target)),"Actual menu button obscured");
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=point,button=0,clickCount=1}))target.SendEvent(down);
            yield return null;
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,mousePosition=point,button=0,clickCount=1}))target.SendEvent(up);
            Require(shell.IsStartingRun&&shell.IsReloading,"Actual menu did not enter approach loading");
            float began=Time.realtimeSinceStartup,last=began,nextCapture=began+.2f;int rendered=title.RenderedFrames;
            report.inputBlocked=true;report.mode=school?"school":"corridor";
            while(shell.IsReloading)
            {
                Require(!session.InputAllowed&&Time.timeScale==0,"Input unlocked before world was ready");
                yield return null;float now=Time.realtimeSinceStartup;
                report.maxLoadingFrameSeconds=Mathf.Max(report.maxLoadingFrameSeconds,now-last);
                report.loadingFrames++;report.approachDistance=Mathf.Max(report.approachDistance,Vector3.Distance(firstPosition,pivot.position));
                report.wristSwing=Mathf.Max(report.wristSwing,Quaternion.Angle(firstWrist,wrist.localRotation));
                if(now>=nextCapture && shell.IsReloading && frames.Count<6)
                {
                    var frame=new Frame{image="approach-"+frames.Count.ToString("D2")+".png",seconds=now-began,distance=report.approachDistance,
                        headAngle=Quaternion.Angle(firstHead,head.localRotation),floorGap=title.SurfaceFloorGap,renders=title.RenderedFrames-rendered};
                    frames.Add(frame);Save(view,frame.image);nextCapture=Time.realtimeSinceStartup+.7f;
                }
                last=Time.realtimeSinceStartup;
            }
            report.loadingSeconds=Time.realtimeSinceStartup-began;report.titleRenders=title.RenderedFrames-rendered;
            report.longestPreparationStep=shell.LongestPreparationStep;report.longestPreparationStepName=shell.LongestPreparationStepName;
            report.modeReady=school?session.ChapterMode:session.CorridorMode;
            Require(string.IsNullOrEmpty(shell.ReloadError)&&report.modeReady&&session.InputAllowed,"Load failed to enter playable mode: "+shell.ReloadError);
            Require(report.approachDistance>.1f&&report.wristSwing>3&&report.titleRenders>5,"Approach or original gait did not animate");
            yield return null;yield return null;Save(view,"playing.png");
        }
        static void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        void Save(GameShellView view,string name)
        {
            var target=view.CaptureTarget;var previous=RenderTexture.active;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;Destroy(texture);}
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;report.status=failure==null&&errors.Count==0?"PASS":"FAIL";report.failure=failure;report.frames=frames.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"menu-approach.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="PASS"?0:1);
        }
    }
}
