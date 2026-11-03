using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator LobbySettingsUsesHorrorMenuAndPreservesControls()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SETTINGS_REVIEW_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            Assert.That(Application.isBatchMode&&Environment.GetCommandLineArgs().Contains("-v2-audit-no-desktop-interaction")&&
                Environment.GetCommandLineArgs().Contains("-noaudio"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            Call(shell,"RestoreDefaultSettings");yield return RecoveryUiReady();yield return RecoveryClick("settings");
            Assert.That(RecoveryPage,Is.EqualTo("Settings"));var view=One("GameShellView");
            yield return CaptureLobbySettings(view,output,"settings-normal-16x9.png",1280,720);
            yield return CaptureLobbySettings(view,output,"settings-normal-21x9.png",1680,720);
            float before=Get<float>(shell,"Volume");yield return RecoveryClick("volume-up");
            Assert.That(Get<float>(shell,"Volume"),Is.EqualTo(before+.05f).Within(.001f));
            yield return RecoveryClick("reduced-motion");Assert.That(Get<bool>(shell,"ReducedMotion"),Is.True);
            var root=Get<VisualElement>(view,"Root");var up=root.Q<Button>("volume-up");up.Focus();yield return null;
            Assert.That(up.Q<VisualElement>("volume-up-selection").resolvedStyle.opacity,Is.EqualTo(1).Within(.01f));
            Assert.That(up.Q<Label>("volume-up-echo").resolvedStyle.opacity,Is.Zero,"Reduced motion must suppress the red echo");
            Call(shell,"ToggleLargeText");Call(shell,"ToggleHighContrast");yield return null;yield return null;
            yield return CaptureLobbySettings(view,output,"settings-large-4x3.png",1024,768);
            yield return RecoveryClick("back");Assert.That(RecoveryPage,Is.EqualTo("Title"));
        }

        IEnumerator CaptureLobbySettings(Component view,string output,string filename,int width,int height)
        {
            Call(view,"SetCaptureSize",width,height);for(int i=0;i<5;i++)yield return null;yield return Delay(.25f);
            var root=Get<VisualElement>(view,"Root");var up=root.Q<Button>("volume-up");
            Assert.That(up,Is.Not.Null);Assert.That(root.Q<Button>("back"),Is.Not.Null);
            Assert.That(root.Q<Button>("large-text"),Is.Not.Null);Assert.That(up.Q<VisualElement>("volume-up-selection"),Is.Not.Null);
            Assert.That(up.resolvedStyle.unityFont,Is.SameAs(Resources.Load<Font>("Fonts/PauseMyeongjo")));
            Assert.That(CloudExperienceTests.Layout(root),Is.Empty,"Settings layout clipped at "+width+"x"+height);
            var frame=CloudExperienceTests.Read(Get<RenderTexture>(view,"CaptureTarget"));
            try{Assert.That(CloudExperienceTests.HasContent(frame),Is.True);File.WriteAllBytes(Path.Combine(output,filename),frame.EncodeToPNG());}
            finally{Object.Destroy(frame);}
        }

        // Opt-in, offscreen capture of the real paused school camera and UI.
        // It does not send OS input or certify native-player performance.
        [UnityTest,Timeout(180000)]
        public IEnumerator PauseMenuOverActualSchoolCamera()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_PAUSE_REVIEW_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            Assert.That(Application.isBatchMode&&Environment.GetCommandLineArgs().Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            Assert.That(Environment.GetCommandLineArgs().Contains("-noaudio"),Is.True,"Review must not play through the user's speakers");
            Call(shell,"RestoreDefaultSettings");yield return BeginObservationSchool();
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,12));
            var view=One("GameShellView");var camera=Get<Camera>(player,"eyes");
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(0,1.5f,30)-camera.transform.position);
            Call(shell,"Pause");yield return null;yield return null;
            Assert.That(Time.timeScale,Is.Zero,"The menu must pause simulation");
            yield return CapturePauseComposite(view,camera,output,"pause-normal-16x9.png",1280,720);
            yield return CapturePauseComposite(view,camera,output,"pause-normal-21x9.png",1680,720);
            Call(shell,"ToggleLargeText");Call(shell,"ToggleHighContrast");yield return null;yield return null;
            yield return CapturePauseComposite(view,camera,output,"pause-large-4x3.png",1024,768);
            var root=Get<VisualElement>(view,"Root");var journal=root.Q<Button>("journal");var resume=root.Q<Button>("resume");
            journal.Focus();yield return Delay(.3f);
            Assert.That(root.panel.focusController.focusedElement,Is.SameAs(journal));
            Assert.That(journal.Q<VisualElement>("journal-selection").resolvedStyle.opacity,Is.GreaterThan(.95f));
            Assert.That(resume.Q<VisualElement>("resume-selection").resolvedStyle.opacity,Is.LessThan(.05f));
            Assert.That(journal.Q<VisualElement>("journal-trace").resolvedStyle.width,Is.GreaterThan(43));
            yield return RecoveryClick("settings");Assert.That(RecoveryPage,Is.EqualTo("Settings"));
            yield return RecoveryClick("reduced-motion");Assert.That(Get<bool>(shell,"ReducedMotion"),Is.True);
            yield return RecoveryClick("back");Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            root=Get<VisualElement>(view,"Root");journal=root.Q<Button>("journal");resume=root.Q<Button>("resume");journal.Focus();yield return null;
            Assert.That(journal.Q<VisualElement>("journal-selection").resolvedStyle.opacity,Is.EqualTo(1).Within(.01f),"Reduced motion must select immediately");
            Assert.That(resume.Q<VisualElement>("resume-selection").resolvedStyle.opacity,Is.LessThan(.01f));
            yield return RecoveryClick("resume");Assert.That(RecoveryPage,Is.EqualTo("Playing"));
            Assert.That(Time.timeScale,Is.GreaterThan(0));
            yield return RecoveryPulse(UnityEngine.InputSystem.Key.Escape);Assert.That(RecoveryPage,Is.EqualTo("Pause"));
            yield return RecoveryPulse(UnityEngine.InputSystem.Key.Escape);Assert.That(RecoveryPage,Is.EqualTo("Playing"));
        }

        IEnumerator CapturePauseComposite(Component view,Camera camera,string output,string filename,int width,int height)
        {
            Call(view,"SetCaptureSize",width,height);
            for(int i=0;i<5;i++)yield return null;
            yield return Delay(.25f); // Capture the settled selection, not its first transition frame.
            var root=Get<VisualElement>(view,"Root");
            Assert.That(root.Q<VisualElement>("pause-scene-scrim"),Is.Not.Null);
            Assert.That(root.Q<Button>("resume"),Is.Not.Null);Assert.That(root.Q<Button>("settings"),Is.Not.Null);
            Assert.That(root.Q<Button>("suspend-chapter"),Is.Not.Null);
            Assert.That(root.Q<Label>("pause-objective"),Is.Null,"The pause screen must not restore the right-hand explanation");
            Assert.That(root.Q<Label>("pause-progress"),Is.Null,"Progress remains in the journal, not the pause screen");
            var font=Resources.Load<Font>("Fonts/PauseMyeongjo");Assert.That(font,Is.Not.Null,"Bundled pause font is missing");
            Assert.That(root.Q<Button>("resume").resolvedStyle.unityFont,Is.SameAs(font));
            foreach(char character in "일시정지계속하기조사기록환경설접근성학교저장후나가기처음부터다시작화면으로진행초기화·()")
                Assert.That(font.HasCharacter(character),Is.True,"Pause font lacks Korean glyph "+character);
            Assert.That(CloudExperienceTests.Layout(root),Is.Empty,"Pause layout clipped at "+width+"x"+height);
            var background=SchoolHorrorFrame(camera,width,height);var overlay=CloudExperienceTests.Read(Get<RenderTexture>(view,"CaptureTarget"));
            var composite=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                var world=background.GetPixels32();var ui=overlay.GetPixels32();
                for(int i=0;i<world.Length;i++)
                {
                    float a=ui[i].a/255f,remaining=1-a;
                    world[i]=new Color32((byte)Mathf.Clamp(ui[i].r+world[i].r*remaining,0,255),
                        (byte)Mathf.Clamp(ui[i].g+world[i].g*remaining,0,255),
                        (byte)Mathf.Clamp(ui[i].b+world[i].b*remaining,0,255),255);
                }
                composite.SetPixels32(world);composite.Apply();File.WriteAllBytes(Path.Combine(output,filename),composite.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output,"ui-"+filename),overlay.EncodeToPNG());
            }
            finally{Object.Destroy(background);Object.Destroy(overlay);Object.Destroy(composite);}
        }
    }
}
