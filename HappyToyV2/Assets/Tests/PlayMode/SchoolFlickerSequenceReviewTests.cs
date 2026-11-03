using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class FlickerSequenceFrame
        {public string file;public float elapsed,creatureZ,distance;public bool dark;public int steps,lunges;}
        [Serializable] sealed class FlickerSequenceReport
        {public string scope="Actual production camera and Unity mixer; process device output muted. No subjective listening or native FPS certification.";public int rate,samples,clipped;public double peak,rms;public bool deviceSessionMuted;public FlickerSequenceFrame[] frames;}

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolFlickerSequenceActualCameraAndMixer()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_FLICKER_SEQUENCE_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            Assert.That(Application.isBatchMode&&Environment.GetCommandLineArgs().Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var silence=RequireType("DiagnosticAudioSilence");var muted=silence.GetProperty("DeviceSessionMuted");
            yield return (IEnumerator)Call(silence,"WaitForSafeAudio",output,true);
            Assert.That((bool)muted.GetValue(null),Is.True,"Process audio must be muted before capture");
            yield return BeginObservationSchool();Call(shell,"Pause");
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");UseSchoolAnomalyFixture(world,"DancingWalker");
            var walker=Get<Component>(world,"Walker");var state=Call(walker,"CaptureState");
            Set(state,"X",0f);Set(state,"Z",33f);Set(state,"Yaw",180f);Set(state,"Elapsed",0d);Set(state,"IdleElapsed",0d);Call(walker,"RestoreState",state);
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,21));var camera=Get<Camera>(player,"eyes");
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(0,1.4f,33)-camera.transform.position);
            Call(shell,"Resume");
            int rate=AudioSettings.outputSampleRate;Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
            float previous=Time.captureDeltaTime;bool started=false;var samples=new List<float>();var frames=new List<FlickerSequenceFrame>();
            try
            {
                Time.captureDeltaTime=1f/15f;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                float deadline=Time.realtimeSinceStartup+50,lastLight=-10,lastDark=-10;int frame=0,target=rate*2*4;
                while(samples.Count<target&&Time.realtimeSinceStartup<deadline&&!Get<bool>(session,"Finished"))
                {
                    yield return null;int count=AudioRenderer.GetSampleCountForCaptureFrame();
                    if(count==0){using(var empty=new NativeArray<float>(0,Allocator.Temp))AudioRenderer.Render(empty);count=AudioRenderer.GetSampleCountForCaptureFrame();}
                    Assert.That(count,Is.InRange(0,rate*2));
                    if(count>0)using(var buffer=new NativeArray<float>(count*2,Allocator.Temp))
                    {Assert.That(AudioRenderer.Render(buffer),Is.True);for(int i=0;i<buffer.Length&&samples.Count<target;i++)samples.Add(buffer[i]);}
                    var current=Call(walker,"CaptureState");float elapsed=(float)Get<double>(current,"Elapsed");bool dark=Get<bool>(walker,"FlickerDark");
                    if((!dark&&elapsed-lastLight>=.22f)||(dark&&elapsed-lastDark>=1f))
                    {
                        if(dark)lastDark=elapsed;else lastLight=elapsed;
                        var image=SchoolHorrorFrame(camera,640,360);string file=$"frame-{frame:000}.png";
                        try{File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());}finally{Object.Destroy(image);}
                        var atmosphere=Get<Component>(walker,"FlickerAtmosphere");
                        frames.Add(new FlickerSequenceFrame{file=file,elapsed=elapsed,creatureZ=walker.transform.localPosition.z,
                            distance=Vector3.Distance(walker.transform.position,player.transform.position),dark=dark,steps=Get<int>(atmosphere,"StepsPlayed"),lunges=Get<int>(atmosphere,"LungesPlayed")});
                    }
                    frame++;
                }
                Assert.That(samples.Count,Is.EqualTo(target),"Actual pursuit mixer capture was incomplete");
                Assert.That(frames.Count,Is.GreaterThanOrEqualTo(5));Assert.That(frames.Last().creatureZ,Is.LessThan(frames[0].creatureZ-1),"Creature did not approach during capture");
                Assert.That(frames.Count(f=>!f.dark),Is.GreaterThanOrEqualTo(5),"Lit flicker phases were not captured");
                Assert.That(frames.Count(f=>f.dark),Is.GreaterThanOrEqualTo(2),"Dark flicker phases were not captured");
                Assert.That(frames.Last().steps,Is.GreaterThan(0),"Real creature movement cues were absent");
                Assert.That(frames.Last().lunges,Is.GreaterThan(0),"Real creature lunge cues were absent");
                double peak=samples.Max(value=>Math.Abs(value));double rms=Math.Sqrt(samples.Average(value=>value*(double)value));int clipped=samples.Count(value=>Math.Abs(value)>=1);
                File.WriteAllBytes(Path.Combine(output,"pursuit-mix.wav"),CloudExperienceTests.Wave(samples,rate,2));
                File.WriteAllText(Path.Combine(output,"sequence.json"),JsonUtility.ToJson(new FlickerSequenceReport{rate=rate,samples=samples.Count,peak=peak,rms=rms,clipped=clipped,deviceSessionMuted=(bool)muted.GetValue(null),frames=frames.ToArray()},true));
                Assert.That(rms,Is.GreaterThan(.00002));Assert.That(clipped,Is.Zero,"Real pursuit mix clips");
            }
            finally{if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=previous;}
        }
    }
}
