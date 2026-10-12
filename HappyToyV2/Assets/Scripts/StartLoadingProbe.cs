using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in silent proof using the real title button, UI render target, player
    // camera and actual entrance desk. No ordinary-game installation or output.
    public sealed class StartLoadingProbe:MonoBehaviour
    {
        string output;float started;bool finished;
        readonly List<string> errors=new List<string>();readonly List<Frame> frames=new List<Frame>();
        [Serializable]sealed class Frame{public string image,page;public float seconds,menu,black,approach;public bool preparing,input;}
        [Serializable]sealed class Report
        {
            public string status,failure;
            public string scope="Actual title pointer start, incremental corridor preparation and unscaled approach, black transition and gated corridor reveal. Screens compose the actual UI target over the actual player camera when Playing. Controlled actual entrance-drawer fixture keeps the player capsule at touching distance and uses its public interaction. PlayMode additionally verifies real E input. Not a complete survival run or audio certification.";
            public int preparingFrames,movingPreparingFrames;public float maximumApproach,minimumMenu=1,maximumBlack,maximumArmSwing,loadingSeconds,drawerTravel,playerDrift;
            public bool revealGated,drawerOpened,drawerClosed,playerPairRestored;
            public Frame[] frames;public string[] errors;
        }
        readonly Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v2-start-loading-output");
            if(at>=0&&at+1<args.Length)new GameObject("Explicit silent start-loading proof").AddComponent<StartLoadingProbe>().output=Path.GetFullPath(args[at+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void Update(){if(!finished&&started>0&&Time.realtimeSinceStartup-started>120)Finish("Start-loading proof timeout");}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(!finished)
            {
                bool more=false;object next=null;string failure=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception error){failure=error.ToString();}
                if(failure!=null){Finish(failure);yield break;}
                if(!more){stack.Pop();if(stack.Count==0){Finish(null);yield break;}continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);yield return null;yield return null;
            var session=GameSession.Current;var shell=session.Shell;var view=session.GetComponent<GameShellView>();
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));shell.RestoreDefaultSettings();view.SetCaptureSize(960,540);
            for(int i=0;i<5;i++)yield return null;
            var title=FindAnyObjectByType<CinematicTitleScene>();Require(title&&shell.Screen==GameShell.Page.Title,"Actual title missing");
            var arm=GameObject.Find("Actual Baeksugwi source clone").GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Arm1L_Wrist");var armRest=arm.localRotation;
            yield return new WaitForEndOfFrame();Capture(view,session,"title-before.png",0);
            var button=view.Root.Q<Button>("begin");var position=button.worldBound.center;var target=view.Root.panel.Pick(position);
            Require(button.enabledInHierarchy && (target==button||button.Contains(target)),"Actual start pointer target blocked");
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=position,button=0,clickCount=1}))target.SendEvent(down);
            yield return new WaitForSecondsRealtime(.04f);
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,mousePosition=position,button=0,clickCount=1}))target.SendEvent(up);
            Require(shell.CinematicLoading,"Real start did not enter the cinematic");
            float begin=Time.realtimeSinceStartup,nextCapture=0,previousDistance=0;int image=0;
            while(shell.CinematicLoading)
            {
                Require(!session.InputAllowed&&shell.IsReloading,"Player input enabled before cinematic reveal completed");
                report.minimumMenu=Mathf.Min(report.minimumMenu,shell.TransitionMenuOpacity);report.maximumBlack=Mathf.Max(report.maximumBlack,shell.TransitionBlack);
                report.maximumApproach=Mathf.Max(report.maximumApproach,title.ApproachDistance);
                report.maximumArmSwing=Mathf.Max(report.maximumArmSwing,Quaternion.Angle(armRest,arm.localRotation));
                if(shell.CorridorPreparing){report.preparingFrames++;if(title.ApproachDistance>previousDistance+.0001f)report.movingPreparingFrames++;}
                previousDistance=title.ApproachDistance;
                if(shell.Screen==GameShell.Page.Playing && shell.TransitionBlack<.9f)report.revealGated=true;
                if(Time.realtimeSinceStartup>=nextCapture)
                {yield return new WaitForEndOfFrame();Capture(view,session,"transition-"+(image++).ToString("D3")+".png",Time.realtimeSinceStartup-begin);nextCapture=Time.realtimeSinceStartup+.16f;}
                yield return null;
            }
            report.loadingSeconds=Time.realtimeSinceStartup-begin;
            Require(session.CorridorMode&&session.InputAllowed,"Corridor never became playable");
            Require(report.movingPreparingFrames>3&&report.maximumApproach>1,"Title approach froze during preparation");
            Require(report.maximumArmSwing>10,"Loading creature did not use its locomotion");
            Require(report.minimumMenu<.01f&&report.maximumBlack>.99f&&report.revealGated,"Missing menu fade, full black or gated reveal");
            yield return new WaitForEndOfFrame();Capture(view,session,"corridor-ready.png",Time.realtimeSinceStartup-begin);
            foreach(var brain in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None))brain.gameObject.SetActive(false);
            var drawer=session.Corridor.Drawers.Single(x=>x.StableId=="drawer-atmosphere-0");
            var furniture=drawer.GetComponentInParent<CorridorFurniturePlacement>();
            var front=drawer.GetComponentsInChildren<Collider>().Single(x=>x.name=="Drawer physical front and handle target");
            var player=session.player;var controller=player.GetComponent<CharacterController>();var outward=furniture.transform.TransformDirection(Vector3.back).normalized;
            var close=front.bounds.center+outward*(controller.radius+.022f+.035f);close.y=.03f;
            controller.enabled=false;player.transform.position=close;controller.enabled=true;player.enabled=false;
            player.eyes.transform.LookAt(front.bounds.center);Physics.SyncTransforms();
            yield return new WaitForEndOfFrame();Capture(view,session,"drawer-before.png",Time.realtimeSinceStartup-begin);
            drawer.Use(player);float deadline=Time.realtimeSinceStartup+3;
            while(!drawer.AtRequestedPose){Require(Time.realtimeSinceStartup<deadline,"Close player blocked opening");yield return null;}
            report.drawerOpened=drawer.IsOpen;report.drawerTravel=drawer.Travel;report.playerDrift=Vector3.Distance(close,player.transform.position);
            Require(report.drawerOpened&&report.drawerTravel>.29f&&report.playerDrift<.06f,"Drawer did not open at touching distance");
            yield return new WaitForEndOfFrame();Capture(view,session,"drawer-open.png",Time.realtimeSinceStartup-begin);
            yield return null;drawer.Use(player);deadline=Time.realtimeSinceStartup+3;
            while(!drawer.AtRequestedPose){Require(Time.realtimeSinceStartup<deadline,"Close player blocked closing");yield return null;}
            report.drawerClosed=!drawer.IsOpen;Require(report.drawerClosed,"Drawer did not close");
            controller.enabled=false;player.transform.position=close+outward*1.2f;controller.enabled=true;yield return null;yield return null;
            report.playerPairRestored=!Physics.GetIgnoreCollision(front,controller);Require(report.playerPairRestored,"Drawer/player collision pair not restored");
        }
        void Capture(GameShellView view,GameSession session,string name,float elapsed)
        {
            var prior=RenderTexture.active;var ui=new Texture2D(960,540,TextureFormat.RGBA32,false,true);
            RenderTexture.active=view.CaptureTarget;ui.ReadPixels(new Rect(0,0,960,540),0,0);ui.Apply();RenderTexture.active=prior;
            if(session.Shell.Screen==GameShell.Page.Playing)
            {
                var world=new RenderTexture(960,540,24);world.Create();
                RenderPipeline.SubmitRenderRequest(session.player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=world});
                var scene=new Texture2D(960,540,TextureFormat.RGBA32,false);RenderTexture.active=world;scene.ReadPixels(new Rect(0,0,960,540),0,0);scene.Apply();RenderTexture.active=prior;
                var overlay=ui.GetPixels32();var background=scene.GetPixels32();
                // UI Toolkit renders premultiplied colour into its transparent target.
                for(int i=0;i<overlay.Length;i++){int remaining=255-overlay[i].a;overlay[i]=new Color32((byte)Mathf.Min(255,overlay[i].r+background[i].r*remaining/255),(byte)Mathf.Min(255,overlay[i].g+background[i].g*remaining/255),(byte)Mathf.Min(255,overlay[i].b+background[i].b*remaining/255),255);}
                ui.SetPixels32(overlay);ui.Apply();world.Release();Destroy(world);Destroy(scene);
            }
            File.WriteAllBytes(Path.Combine(output,name),ui.EncodeToPNG());Destroy(ui);
            var shell=session.Shell;var title=FindAnyObjectByType<CinematicTitleScene>();
            frames.Add(new Frame{image=name,page=shell.Screen.ToString(),seconds=elapsed,menu=shell.TransitionMenuOpacity,black=shell.TransitionBlack,approach=title.ApproachDistance,preparing=shell.CorridorPreparing,input=session.InputAllowed});
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;report.status=failure==null&&errors.Count==0?"PASS":"FAIL";report.failure=failure;report.frames=frames.ToArray();report.errors=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"start-loading.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="PASS"?0:1);
        }
    }
}
