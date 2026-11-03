using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Explicit Windows-player review only. No OS input, audible output, shared
    // save files, display focus or cursor changes are permitted in this route.
    public sealed class NativeMenuReviewProbe : MonoBehaviour
    {
        const int Width=960,Height=540;
        string output;Gamepad pad;InputSettings.BackgroundBehavior oldBackground;
        readonly List<Sample> samples=new List<Sample>();
        [Serializable] sealed class Sample {public string file;public float seconds,oldOpacity,newOpacity,traceWidth,echoOpacity;}
        [Serializable] sealed class Report
        {
            public string scope="Windows player, real UI and school camera rendered offscreen; synthetic gamepad, separate profile, no audio. Not a native display/FPS audit.";
            public string unity,graphics,device,error;public bool batchMode,cursorSuppressed,noAudio,reducedMotion,settingsCaptured,pauseCaptured;
            public Sample[] samples;
        }
        Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-v2-native-menu-review");
            if(at<0)return;
            if(at+1>=args.Length||!Application.isBatchMode||Application.platform!=RuntimePlatform.WindowsPlayer||
                !DiagnosticDesktopPolicy.SuppressCursorWrites||!args.Contains("-noaudio")||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            {Debug.LogError("Native menu review requires a graphics-enabled Windows batch player, noaudio and desktop isolation.");Application.Quit(2);return;}
            var path=Path.GetFullPath(args[at+1]);
            if(Directory.Exists(path)){Debug.LogError("Native menu review requires a fresh output directory.");Application.Quit(2);return;}
            Directory.CreateDirectory(path);
            var probe=new GameObject("Explicit isolated native menu review").AddComponent<NativeMenuReviewProbe>();probe.output=path;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            pad=InputSystem.AddDevice<Gamepad>("Isolated menu audit gamepad");
            report.unity=Application.unityVersion;report.graphics=SystemInfo.graphicsDeviceType.ToString();report.device=SystemInfo.graphicsDeviceName;
            report.batchMode=Application.isBatchMode;report.cursorSuppressed=DiagnosticDesktopPolicy.SuppressCursorWrites;report.noAudio=Environment.GetCommandLineArgs().Contains("-noaudio");
            var stack=new Stack<IEnumerator>();stack.Push(Run());string failure=null;double deadline=Time.realtimeSinceStartupAsDouble+120;
            while(stack.Count>0&&failure==null)
            {
                if(Time.realtimeSinceStartupAsDouble>deadline){failure="Native menu review timed out";break;}
                bool moved=false;object next=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception error){failure=error.ToString();}
                if(failure!=null)break;
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}
                yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            report.error=failure;report.samples=samples.ToArray();
            File.WriteAllText(Path.Combine(output,"native-menu-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(failure==null?0:1);
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        IEnumerator Run()
        {
            yield return null;yield return null;var session=GameSession.Current;
            Require(session&&session.Shell&&session.player,"Production player session missing");
            var shell=session.Shell;var view=session.GetComponent<GameShellView>();Require(view&&view.Root!=null,"Production menu unavailable");
            Require(shell.Screen==GameShell.Page.Title,"Review must start on the title");
            report.reducedMotion=shell.ReducedMotion;view.SetCaptureSize(Width,Height);shell.Settings();yield return new WaitForSecondsRealtime(.4f);
            Require(shell.Screen==GameShell.Page.Settings&&view.Root.Q<Button>("volume-up")!=null,"Lobby settings unavailable");
            var volumeUp=view.Root.Q<Button>("volume-up");volumeUp.Focus();yield return new WaitForSecondsRealtime(.3f);
            Require(view.Root.panel.focusController.focusedElement==volumeUp,"Lobby settings focus did not move");
            var settingMarker=volumeUp.Q<VisualElement>("volume-up-selection");
            Require(settingMarker!=null&&settingMarker.resolvedStyle.opacity>.95f,"Settings focus effect did not settle");
            SaveUi(view,"settings-lobby.png");report.settingsCaptured=true;
            shell.Back();yield return null;
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));session.CreateSchoolLoop();shell.Begin();yield return null;yield return null;
            Require(session.ChapterMode&&session.Chapter.Loop&&shell.Screen==GameShell.Page.Playing,"Production school did not start");
            shell.Pause();yield return new WaitForSecondsRealtime(.4f);
            Require(shell.Screen==GameShell.Page.Pause&&Time.timeScale==0,"Pause did not hold simulation");
            var root=view.Root;var resume=root.Q<Button>("resume");var journal=root.Q<Button>("journal");
            Require(resume!=null&&journal!=null&&root.panel!=null,"Pause controls missing");
            Require(root.panel.focusController.focusedElement==resume,"Initial focus is not resume");
            var background=CameraFrame(session.player.eyes);var pixels=background.GetPixels32();Destroy(background);
            SaveComposite(view,pixels,"pause-before.png");report.pauseCaptured=true;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));
            double deadline=Time.realtimeSinceStartupAsDouble+5,began=-1;bool released=false;
            float[] marks={0,.045f,.09f,.14f,.20f,.30f};var captures=new List<Color32[]>();
            while(samples.Count<marks.Length&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                yield return null;
                if(began<0&&root.panel.focusController.focusedElement==journal)began=Time.realtimeSinceStartupAsDouble;
                if(began<0)continue;
                float elapsed=(float)(Time.realtimeSinceStartupAsDouble-began);
                if(!released&&elapsed>=.10f){InputSystem.QueueStateEvent(pad,new GamepadState());released=true;}
                if(elapsed<marks[samples.Count])continue;
                var ui=Read(view.CaptureTarget);captures.Add(ui.GetPixels32());Destroy(ui);
                samples.Add(new Sample{file=$"pause-selection-{samples.Count:00}.png",seconds=elapsed,
                    oldOpacity=resume.Q<VisualElement>("resume-selection").resolvedStyle.opacity,
                    newOpacity=journal.Q<VisualElement>("journal-selection").resolvedStyle.opacity,
                    traceWidth=journal.Q<VisualElement>("journal-trace").resolvedStyle.width,
                    echoOpacity=journal.Q<Label>("journal-echo").resolvedStyle.opacity});
            }
            InputSystem.QueueStateEvent(pad,new GamepadState());
            Require(samples.Count==marks.Length&&root.panel.focusController.focusedElement==journal,"Synthetic gamepad selection was not captured");
            Require(samples.Last().oldOpacity<.05f&&samples.Last().newOpacity>.95f,"Selection did not settle");
            for(int i=0;i<samples.Count;i++)SaveCompositePixels(pixels,captures[i],samples[i].file);
        }
        static Texture2D Read(RenderTexture target)
        {
            Require(target&&target.IsCreated(),"Offscreen render target unavailable");var previous=RenderTexture.active;RenderTexture.active=target;
            try{var frame=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);frame.ReadPixels(new Rect(0,0,target.width,target.height),0,0);frame.Apply();return frame;}
            finally{RenderTexture.active=previous;}
        }
        static Texture2D CameraFrame(Camera camera)
        {
            Require(camera,"Player camera unavailable");var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=camera.targetTexture;float aspect=camera.aspect;
            try{Require(target.Create(),"Camera render target unavailable");camera.targetTexture=target;camera.aspect=Width/(float)Height;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});return Read(target);}
            finally{camera.targetTexture=previous;camera.aspect=aspect;target.Release();Destroy(target);}
        }
        void SaveUi(GameShellView view,string file){var frame=Read(view.CaptureTarget);try{File.WriteAllBytes(Path.Combine(output,file),frame.EncodeToPNG());}finally{Destroy(frame);}}
        void SaveComposite(GameShellView view,Color32[] world,string file){var ui=Read(view.CaptureTarget);try{SaveCompositePixels(world,ui.GetPixels32(),file);}finally{Destroy(ui);}}
        void SaveCompositePixels(Color32[] world,Color32[] ui,string file)
        {
            Require(world.Length==ui.Length&&world.Length==Width*Height,"Composite dimensions disagree");var pixels=new Color32[world.Length];
            for(int i=0;i<pixels.Length;i++){float rest=1-ui[i].a/255f;pixels[i]=new Color32(
                (byte)Mathf.Clamp(ui[i].r+world[i].r*rest,0,255),(byte)Mathf.Clamp(ui[i].g+world[i].g*rest,0,255),
                (byte)Mathf.Clamp(ui[i].b+world[i].b*rest,0,255),255);}
            var frame=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{frame.SetPixels32(pixels);frame.Apply();File.WriteAllBytes(Path.Combine(output,file),frame.EncodeToPNG());}finally{Destroy(frame);}
        }
        void OnDestroy(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=oldBackground;}
    }
}
