using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class PauseMotionSample
        {public string file;public float seconds,oldOpacity,newOpacity,traceWidth,padding,echoOpacity;}
        [Serializable] sealed class PauseMotionReport
        {public string scope="Offscreen real school camera and UI; synthetic gamepad, no audio. Not a native player or FPS audit.";public PauseMotionSample[] samples;}

        [UnityTest,Timeout(180000)]
        public IEnumerator PauseSelectionMotionOverActualSchool()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_PAUSE_MOTION_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            Assert.That(Application.isBatchMode&&Environment.GetCommandLineArgs().Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(Environment.GetCommandLineArgs().Contains("-noaudio"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            Call(shell,"RestoreDefaultSettings");AddAuditPad();yield return BeginObservationSchool();
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,12));var view=One("GameShellView");
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(0,1.5f,30)-camera.transform.position);
            Call(view,"SetCaptureSize",960,540);Call(shell,"Pause");yield return Delay(.4f);
            var root=Get<VisualElement>(view,"Root");var resume=root.Q<Button>("resume");var journal=root.Q<Button>("journal");
            Assert.That(root.panel.focusController.focusedElement,Is.SameAs(resume));
            var background=SchoolHorrorFrame(camera,960,540);var captured=new List<Color32[]>();var samples=new List<PauseMotionSample>();
            try
            {
                InputSystem.QueueStateEvent(auditPad,new GamepadState().WithButton(GamepadButton.DpadDown));
                double deadline=Time.realtimeSinceStartupAsDouble+5,began=-1;bool released=false;
                float[] marks={0,.045f,.09f,.14f,.20f,.30f};
                while(samples.Count<marks.Length&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return null;
                    if(began<0&&root.panel.focusController.focusedElement==journal)began=Time.realtimeSinceStartupAsDouble;
                    if(began<0)continue;
                    float elapsed=(float)(Time.realtimeSinceStartupAsDouble-began);
                    if(!released&&elapsed>=.10f){InputSystem.QueueStateEvent(auditPad,new GamepadState());released=true;}
                    if(elapsed<marks[samples.Count])continue;
                    var ui=CloudExperienceTests.Read(Get<RenderTexture>(view,"CaptureTarget"));
                    try{captured.Add(ui.GetPixels32());}finally{Object.Destroy(ui);}
                    samples.Add(new PauseMotionSample{file=$"selection-{samples.Count:00}.png",seconds=elapsed,
                        oldOpacity=resume.Q<VisualElement>("resume-selection").resolvedStyle.opacity,
                        newOpacity=journal.Q<VisualElement>("journal-selection").resolvedStyle.opacity,
                        traceWidth=journal.Q<VisualElement>("journal-trace").resolvedStyle.width,
                        echoOpacity=journal.Q<Label>("journal-echo").resolvedStyle.opacity,
                        padding=journal.resolvedStyle.paddingLeft});
                }
                InputSystem.QueueStateEvent(auditPad,new GamepadState());
                Assert.That(samples.Count,Is.EqualTo(marks.Length),"Could not record the real selection transition");
                Assert.That(root.panel.focusController.focusedElement,Is.SameAs(journal),"Gamepad did not select the second action");
                Assert.That(samples.Last().newOpacity,Is.GreaterThan(.95f));Assert.That(samples.Last().oldOpacity,Is.LessThan(.05f));
                Assert.That(samples.Skip(1).All(s=>s.oldOpacity<.05f&&s.newOpacity>.95f),Is.True,
                    "Focus marker must not appear on two items during the transition");
                Assert.That(samples.Last().traceWidth,Is.GreaterThan(49),"The selected scratch never fully appeared");
                var world=background.GetPixels32();
                for(int j=0;j<samples.Count;j++)
                {
                    var ui=captured[j];var pixels=new Color32[world.Length];
                    for(int i=0;i<pixels.Length;i++)
                    {
                        float remaining=1-ui[i].a/255f;
                        pixels[i]=new Color32((byte)Mathf.Clamp(ui[i].r+world[i].r*remaining,0,255),
                            (byte)Mathf.Clamp(ui[i].g+world[i].g*remaining,0,255),
                            (byte)Mathf.Clamp(ui[i].b+world[i].b*remaining,0,255),255);
                    }
                    var composite=new Texture2D(960,540,TextureFormat.RGBA32,false);
                    try{composite.SetPixels32(pixels);composite.Apply();File.WriteAllBytes(Path.Combine(output,samples[j].file),composite.EncodeToPNG());}
                    finally{Object.Destroy(composite);}
                }
                File.WriteAllText(Path.Combine(output,"selection.json"),JsonUtility.ToJson(new PauseMotionReport{samples=samples.ToArray()},true));
            }
            finally{Object.Destroy(background);}
        }
    }
}
