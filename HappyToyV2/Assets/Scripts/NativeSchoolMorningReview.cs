using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Explicit isolated Windows-player inspection; never installed during play.
    public sealed class NativeSchoolMorningReview : MonoBehaviour
    {
        string output,phase="startup";GameSession session;RouteAudioCapture capture;
        readonly List<string> images=new List<string>();
        readonly List<Step> steps=new List<Step>();
        [Serializable] sealed class Step {public float seconds,z;public int warning,marks;}
        [Serializable] sealed class Report
        {
            public string scope="Windows player, production school and StandardRequest camera; controlled checkpoint and player positions, no OS input, process-only audio mute, natural pre-device listener DSP. No display, subjective listening or FPS certification.";
            public string error,phase,unity,device;public bool batch,desktopIsolated,muted,posterFarNormal,posterNearGhost,clearStarted,clearFinished;
            public float morningAtCapture;public string[] images;public Step[] steps;public RouteAudioCapture.Report audio;
        }
        Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-school-morning-review-output");
            if(index<0)return;
            if(index+1>=args.Length||!Application.isBatchMode||Application.platform!=RuntimePlatform.WindowsPlayer||
                !DiagnosticDesktopPolicy.SuppressCursorWrites||args.Contains("-noaudio")||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            {Debug.LogError("School morning review requires an isolated graphics Windows batch player with audio.");Application.Quit(2);return;}
            string path=Path.GetFullPath(args[index+1]);
            if(Directory.Exists(path)){Debug.LogError("School morning review requires a fresh output directory.");Application.Quit(2);return;}
            Directory.CreateDirectory(path);
            var probe=new GameObject("Explicit isolated school morning review").AddComponent<NativeSchoolMorningReview>();probe.output=path;
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        IEnumerator Start()
        {
            Application.runInBackground=true;report.unity=Application.unityVersion;report.device=SystemInfo.graphicsDeviceName;
            report.batch=Application.isBatchMode;report.desktopIsolated=DiagnosticDesktopPolicy.SuppressCursorWrites;
            var stack=new Stack<IEnumerator>();stack.Push(Run());string error=null;double deadline=Time.realtimeSinceStartupAsDouble+150;
            while(stack.Count>0&&error==null)
            {
                if(Time.realtimeSinceStartupAsDouble>deadline){error="Timed out at "+phase;break;}
                bool moved=false;object next=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}catch(Exception ex){error=ex.ToString();}
                if(error!=null)break;
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}
                yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            if(capture)report.audio=capture.Complete();
            report.error=error;report.phase=phase;report.images=images.ToArray();report.steps=steps.ToArray();
            DiagnosticAudioSilence.StopAndRestore();
            File.WriteAllText(Path.Combine(output,"review.json"),JsonUtility.ToJson(report,true));
            Application.Quit(error==null?0:2);
        }
        IEnumerator Run()
        {
            phase="quiet-guard";yield return DiagnosticAudioSilence.WaitForSafeAudio(output,true);
            Require(DiagnosticAudioSilence.DeviceSessionMuted,"Audio device session was not safely muted");
            report.muted=true;yield return null;yield return null;
            session=GameSession.Current;Require(session&&session.player&&session.Shell&&session.Shell.Screen==GameShell.Page.Title,"Fresh title required");
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            session.CreateSchoolLoop();session.Shell.Begin();yield return null;yield return null;
            var world=session.Chapter.Loop;Require(world,"School loop unavailable");
            session.player.enabled=false;session.player.flashlight.enabled=false;
            world.RestoreTraversal(1);world.Walker.gameObject.SetActive(false);
            phase="poster";Fixture(world,SchoolAnomalyKind.AwardPoster);
            Pose(new Vector3(0,.03f,7.7f),new Vector3(2.04f,1.65f,13.6f));yield return null;
            report.posterFarNormal=!world.AwardPoster.Revealed;SaveCamera("poster-far.png");
            Pose(new Vector3(.5f,.03f,13.6f),new Vector3(2.04f,1.65f,13.6f));yield return null;yield return null;
            report.posterNearGhost=world.AwardPoster.Revealed;SaveCamera("poster-near.png");
            Require(report.posterFarNormal&&report.posterNearGhost,"Poster did not change at the actual camera/player positions");
            phase="cleaner";Fixture(world,SchoolAnomalyKind.Cleaning);
            Pose(new Vector3(-.8f,.03f,8),new Vector3(0,.1f,17.5f));
            AudioListener.volume=.8f;capture=RouteAudioCapture.Attach(Path.Combine(output,"audio"));
            var cleaning=world.Anomalies.Cleaning;var source=cleaning.GetComponentInChildren<AudioSource>();
            Require(source&&capture,"Footstep source or listener tap missing");
            float begin=Time.realtimeSinceStartup;int lastWarning=-1,lastMarks=-1;bool pre=false,post=false;
            while(Time.realtimeSinceStartup-begin<7.5f)
            {
                yield return null;
                if(!DiagnosticAudioSilence.DeviceSessionMuted)throw new InvalidOperationException("Device mute lost during capture");
                if(cleaning.ForewarningSteps!=lastWarning||cleaning.StepsEmitted!=lastMarks)
                {lastWarning=cleaning.ForewarningSteps;lastMarks=cleaning.StepsEmitted;steps.Add(new Step{seconds=cleaning.Elapsed,z=source.transform.localPosition.z,warning=lastWarning,marks=lastMarks});}
                if(!pre&&cleaning.Elapsed>2){SaveCamera("cleaner-before-marks.png");pre=true;}
                if(!post&&cleaning.Elapsed>5.3f){SaveCamera("cleaner-marks.png");post=true;}
            }
            report.audio=capture.Complete();capture=null;
            Require(report.audio.callbacks>0&&report.audio.nonSilent&&report.audio.capturedSamples>0,"Listener capture is silent or incomplete");
            Require(cleaning.ForewarningSteps>0&&cleaning.StepsEmitted>0&&pre&&post,"Cleaner sequence incomplete");
            phase="morning";world.RestoreTraversal(world.Traversals);world.RestoreClock(8);
            Pose(new Vector3(.7f,.03f,14.5f),new Vector3(-6.2f,1.5f,15.3f));
            var lighting=world.Root.GetComponent<SchoolHorrorLighting>();yield return null;
            report.clearStarted=world.MorningStarted;SaveCamera("morning-start.png");
            yield return new WaitForSeconds(4);SaveCamera("morning-middle.png");
            yield return new WaitForSeconds(4.5f);report.morningAtCapture=lighting.Morning;SaveCamera("morning-full.png");
            Pose(new Vector3(.8f,.03f,18.8f),new Vector3(-9.2f,1.8f,18.8f));SaveCamera("morning-window.png");
            Pose(new Vector3(.5f,.03f,8),new Vector3(0,1.5f,35));SaveCamera("morning-corridor.png");
            float deadline=Time.realtimeSinceStartup+12;
            while(!session.Finished&&Time.realtimeSinceStartup<deadline)yield return null;
            report.clearFinished=session.Finished&&session.Escaped;
            Require(report.clearStarted&&report.morningAtCapture>.99f&&report.clearFinished,"Morning clear did not finish");
            phase="complete";
        }
        void Fixture(SchoolLoopWorld world,SchoolAnomalyKind kind)
        {
            world.RestoreTraversal(world.Traversals);var state=world.Anomalies.Capture();var deck=state.deck;
            deck.Kind=kind;deck.NormalStreak=0;state.deck=deck;world.Anomalies.Restore(state,world.Traversals);
        }
        void Pose(Vector3 feet,Vector3 look)
        {
            var heading=look-feet;heading.y=0;session.player.RelocateWorld(SchoolLoopWorld.Origin+feet,Quaternion.LookRotation(heading));
            var camera=session.player.eyes;camera.transform.rotation=Quaternion.LookRotation(SchoolLoopWorld.Origin+look-camera.transform.position);
            Physics.SyncTransforms();session.GetComponent<LocalShadowBudget>()?.RefreshNow();
        }
        void SaveCamera(string file)
        {
            var camera=session.player.eyes;var target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var old=camera.targetTexture;var previous=RenderTexture.active;float aspect=camera.aspect;Texture2D frame=null;
            try
            {
                Require(target.Create(),"Camera target unavailable");camera.targetTexture=target;camera.aspect=960f/540;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;
                frame=new Texture2D(960,540,TextureFormat.RGB24,false,false);frame.ReadPixels(new Rect(0,0,960,540),0,0);frame.Apply();
                File.WriteAllBytes(Path.Combine(output,file),frame.EncodeToPNG());images.Add(file);
            }
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=previous;if(frame)Destroy(frame);target.Release();Destroy(target);}
        }
    }
}
