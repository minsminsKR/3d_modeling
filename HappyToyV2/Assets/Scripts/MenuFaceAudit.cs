using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in release UI evidence only; never installed during ordinary play.
    public sealed class MenuFaceAudit : MonoBehaviour
    {
        string output; GameSession session; GameShell shell; GameShellView view;
        Keyboard keys; InputSettings.BackgroundBehavior background;
        float deadline; bool finished, preferencesSaved;
        readonly List<string> errors = new List<string>();
        readonly List<Frame> frames = new List<Frame>();
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly HashSet<string> existing = new HashSet<string>();
        readonly Vector2Int[] sizes = { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1280,960), new Vector2Int(2560,1080) };
        [Serializable] sealed class Frame
        {
            public string image, page; public int width, height, buttons;
            public bool largeText, highContrast, faceVisible, buttonsFit, backgroundNonPickable;
            public Rect logicalViewport, backgroundBounds; public float uiOpaqueFraction;
        }
        [Serializable] sealed class Report
        {
            public string status, texture;
            public string scope = "Release UI Toolkit snapshots of the actual borrowed menu bitmap: normal title, four aspect ratios with large text/high contrast on title and pause, settings focus/submit, real Escape pause and focused Resume button, and original authored scene plus unobscured playing HUD. Controlled Begin() is used only to reach pause/HUD. No gameplay, path, survival, audio or performance certification.";
            public bool developmentBuild, settingsFocus, escapePause, resumeButton, textureAlive, playingFaceHidden;
            public int referenceChangedPixels; public Frame[] frames; public string[] errors;
        }
        readonly Report report = new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-v2-menu-face-output");
            if (at >= 0 && at + 1 < args.Length) new GameObject("Opt-in native menu face audit").AddComponent<MenuFaceAudit>().output = args[at + 1];
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        IEnumerator Paint() { for (int frame = 0; frame < 4; frame++) yield return null; }
        bool FaceVisible => (bool)typeof(GameShellView).GetProperty("MenuFaceVisible").GetValue(view);
        Texture2D FaceTexture => (Texture2D)typeof(GameShellView).GetProperty("MenuFaceTexture").GetValue(view);
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true; deadline = Time.realtimeSinceStartup + 120;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool next; object value;
                try { var current = stack.Peek(); next = current.MoveNext(); value = next ? current.Current : null; }
                catch (Exception error) { errors.Add(error.ToString()); break; }
                if (!next) { stack.Pop(); continue; }
                if (value is IEnumerator child) { stack.Push(child); continue; }
                yield return value;
            }
            Finish();
        }
        void Update() { if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline) { errors.Add("Menu audit timed out"); Finish(); } }
        void SavePreferences()
        {
            foreach (string key in new[] { "v2.volume", "v2.sensitivity", "v2.fov" })
            { if (PlayerPrefs.HasKey(key)) existing.Add(key); floats[key] = PlayerPrefs.GetFloat(key); }
            foreach (string key in new[] { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" })
            { if (PlayerPrefs.HasKey(key)) existing.Add(key); ints[key] = PlayerPrefs.GetInt(key); }
            preferencesSaved = true;
        }
        void Finish()
        {
            if (finished) return; finished = true;
            try
            {
                if (preferencesSaved)
                {
                    // Flush the audit's dirty shell settings before restoring
                    // caller preferences; quit must not save them over again.
                    if (shell) typeof(GameShell).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                    foreach (var pair in floats) { if (existing.Contains(pair.Key)) PlayerPrefs.SetFloat(pair.Key,pair.Value); else PlayerPrefs.DeleteKey(pair.Key); }
                    foreach (var pair in ints) { if (existing.Contains(pair.Key)) PlayerPrefs.SetInt(pair.Key,pair.Value); else PlayerPrefs.DeleteKey(pair.Key); }
                    PlayerPrefs.Save(); preferencesSaved = false;
                }
                if (keys != null && keys.added) { InputSystem.RemoveDevice(keys); InputSystem.settings.backgroundBehavior = background; }
            }
            catch (Exception error) { errors.Add("Cleanup: " + error.Message); }
            report.developmentBuild = Debug.isDebugBuild; report.frames = frames.ToArray(); report.errors = errors.ToArray();
            report.status = errors.Count == 0 && !report.developmentBuild && frames.Count == 11 && report.settingsFocus &&
                report.escapePause && report.resumeButton && report.textureAlive && report.playingFaceHidden ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(output,"menu-face-report.json"), JsonUtility.ToJson(report,true));
            Application.Quit(report.status == "PASS" ? 0 : 2);
        }
        IEnumerator Button(string id, bool keyboard = false)
        {
            var button = view.Root.Q<Button>(id); Require(button != null && button.enabledInHierarchy, "Missing enabled button: " + id);
            var at = button.worldBound.center; var target = view.Root.panel.Pick(at);
            Require(target == button || target != null && button.Contains(target), "Menu image obscures pointer target: " + id);
            if (keyboard)
            {
                button.Focus(); yield return null;
                Require(view.Root.panel.focusController.focusedElement == button, "Keyboard focus did not reach " + id);
                using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
            }
            else
            {
                using (var down = PointerDownEvent.GetPooled(new Event {type=EventType.MouseDown,mousePosition=at,button=0,clickCount=1})) target.SendEvent(down);
                yield return null;
                using (var up = PointerUpEvent.GetPooled(new Event {type=EventType.MouseUp,mousePosition=at,button=0,clickCount=1})) target.SendEvent(up);
            }
            yield return Paint();
        }
        IEnumerator Run()
        {
            yield return Paint(); session = GameSession.Current; Require(session, "No production session"); shell = session.Shell;
            view = session.GetComponent<GameShellView>(); Require(view && view.Root != null, "No initialized UI Toolkit view");
            Require(shell.Screen == GameShell.Page.Title && !session.InputAllowed, "Opt-in audit did not retain Title");
            SavePreferences(); session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            var texture = FaceTexture; Require(texture && texture == Resources.Load<Texture2D>("Menu/cyclopse-menace-v1"), "Menu bitmap resource is missing");
            report.texture = texture.name;
            yield return Button("settings"); Require(shell.Screen == GameShell.Page.Settings, "Pointer settings button did not navigate");
            if (shell.HighContrast) shell.ToggleHighContrast(); if (shell.LargeText) shell.ToggleLargeText(); yield return Paint();
            yield return Button("back"); yield return Capture("title-normal",1600,900,true);
            yield return Button("settings"); yield return Button("high-contrast",true); yield return Button("large-text",true);
            report.settingsFocus = shell.Screen == GameShell.Page.Settings && shell.HighContrast && shell.LargeText;
            Require(report.settingsFocus, "Focused settings submit did not change real shell preferences");
            yield return Capture("settings-large-high-contrast",1600,900); yield return Button("back");
            foreach (var size in sizes) yield return Capture("title-large-high-contrast",size.x,size.y);
            shell.Begin(); yield return Paint(); Require(shell.Screen == GameShell.Page.Playing && session.InputAllowed, "Authored play fixture did not start");
            background = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keys = InputSystem.AddDevice<Keyboard>(); InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Escape));
            yield return new WaitForSecondsRealtime(.08f); InputSystem.QueueStateEvent(keys,new KeyboardState()); yield return Paint();
            report.escapePause = shell.Screen == GameShell.Page.Pause && !session.InputAllowed && Time.timeScale == 0;
            Require(report.escapePause, "Real Escape input did not pause");
            foreach (var size in sizes) yield return Capture("pause-large-high-contrast",size.x,size.y);
            yield return Button("resume",true); report.resumeButton = shell.Screen == GameShell.Page.Playing && session.InputAllowed;
            Require(report.resumeButton, "Focused resume button did not return to Playing");
            yield return Capture("playing-world-and-hud",1600,900);
            report.playingFaceHidden = !FaceVisible; report.textureAlive = texture && FaceTexture == texture;
            Require(report.textureAlive && report.playingFaceHidden, "Playing hid/destroyed the borrowed texture incorrectly");
        }
        static bool Fits(Rect outer, Rect inner) => !float.IsNaN(inner.x) && !float.IsNaN(inner.y) && !float.IsInfinity(inner.width) &&
            !float.IsInfinity(inner.height) && inner.width > 0 && inner.height > 0 && inner.xMin >= outer.xMin-1 && inner.yMin >= outer.yMin-1 &&
            inner.xMax <= outer.xMax+1 && inner.yMax <= outer.yMax+1;
        static Texture2D Read(RenderTexture target)
        {
            var old = RenderTexture.active; var frame = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try { RenderTexture.active=target; frame.ReadPixels(new Rect(0,0,target.width,target.height),0,0); frame.Apply(); return frame; }
            catch { Destroy(frame); throw; } finally { RenderTexture.active=old; }
        }
        IEnumerator Capture(string name, int width, int height, bool reference = false)
        {
            view.SetCaptureSize(width,height); yield return Paint();
            var bounds=view.Root.worldBound; bool playing=shell.Screen==GameShell.Page.Playing;
            var backdrop=view.Root.Q<VisualElement>("menu-face-background"); Require(backdrop!=null,"Missing actual menu face element");
            var item=new Frame {image=name+"-"+width+"x"+height+".png",page=shell.Screen.ToString(),width=width,height=height,
                largeText=shell.LargeText,highContrast=shell.HighContrast,faceVisible=FaceVisible,logicalViewport=bounds,buttonsFit=true,
                backgroundNonPickable=backdrop.pickingMode==PickingMode.Ignore};
            Require(Fits(bounds,bounds),"Non-finite UI viewport");
            view.Root.Query<Button>().ForEach(button=>{item.buttons++;item.buttonsFit &= Fits(bounds,button.worldBound);});
            Require(item.buttonsFit,"Menu button clipped at "+width+"x"+height);
            if(!playing)
            {
                item.backgroundBounds=backdrop.worldBound;
                Require(FaceVisible && item.backgroundNonPickable && Fits(bounds,item.backgroundBounds) &&
                    Mathf.Abs(item.backgroundBounds.width-bounds.width)<1 && Mathf.Abs(item.backgroundBounds.height-bounds.height)<1,"Menu background does not fill the viewport behind controls");
            }
            else Require(!FaceVisible,"Menu face covers the playing HUD");
            var ui=Read(view.CaptureTarget); var colours=ui.GetPixels();
            item.uiOpaqueFraction=colours.Count(pixel=>pixel.a>.1f)/(float)colours.Length;
            if(playing) Require(item.uiOpaqueFraction<.2f,"Playing UI still contains an opaque full-screen menu layer");
            try
            {
                if(reference)
                {
                    var original=backdrop.style.backgroundImage;
                    try
                    {
                        backdrop.style.backgroundImage=StyleKeyword.None;yield return Paint();
                        var without=Read(view.CaptureTarget);
                        try
                        {
                            var pixels=without.GetPixels(); report.referenceChangedPixels=colours.Zip(pixels,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)).Count(delta=>delta>.045f);
                            File.WriteAllBytes(Path.Combine(output,"title-normal-1600x900-resource-off.png"),without.EncodeToPNG());
                        }
                        finally {Destroy(without);}
                    }
                    finally {backdrop.style.backgroundImage=original;}
                    yield return Paint();
                }
                if(playing) CompositeWorld(colours,width,height);
                var png=new Texture2D(width,height,TextureFormat.RGB24,false);
                try {png.SetPixels(colours);png.Apply();File.WriteAllBytes(Path.Combine(output,item.image),png.EncodeToPNG());}
                finally {Destroy(png);}
                frames.Add(item);
            }
            finally {Destroy(ui);}
        }
        void CompositeWorld(Color[] ui, int width, int height)
        {
            var camera=session.player.eyes; float aspect=camera.aspect;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();
            var old=RenderTexture.active;Texture2D world=null;
            try
            {
                camera.aspect=width/(float)height;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;world=new Texture2D(width,height,TextureFormat.RGBAHalf,false,true);world.ReadPixels(new Rect(0,0,width,height),0,0);world.Apply();
                var pixels=world.GetPixels();for(int i=0;i<ui.Length;i++)ui[i]=Color.Lerp(pixels[i].gamma,new Color(ui[i].r,ui[i].g,ui[i].b,1),ui[i].a);
            }
            finally {camera.aspect=aspect;RenderTexture.active=old;if(world)Destroy(world);target.Release();Destroy(target);}
        }
    }
}
