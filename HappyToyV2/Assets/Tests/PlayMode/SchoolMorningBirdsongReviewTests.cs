using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Explicit CPU-only mixer capture; the device session must already be muted.
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolMorningBirdsongQuietMixerReview()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_MORNING_BIRDS_REVIEW_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            var silence=RequireType("DiagnosticAudioSilence");var muted=silence.GetProperty("DeviceSessionMuted");
            yield return (IEnumerator)Call(silence,"WaitForSafeAudio",output,true);
            Assert.That((bool)muted.GetValue(null),Is.True,"Process audio must be muted before capture");
            yield return BeginObservationSchool();Call(shell,"Pause");
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");Call(world,"RestoreClock",8);
            float oldCapture=Time.captureDeltaTime;bool started=false;var samples=new List<float>();
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;
                started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                Call(shell,"Resume");yield return Wait(()=>Get<bool>(world,"MorningStarted"),3,"Morning did not start");
                var birds=Get<Component>(world,"MorningBirds");var source=Get<AudioSource>(birds,"Source");
                ExternalMuteExcept(source);AudioListener.volume=1;
                int rate=AudioSettings.outputSampleRate;Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
                yield return CaptureAudio(samples,rate*2*5,20);
                Assert.That((bool)muted.GetValue(null),Is.True,"Process audio mute was lost");
                double peak=samples.Max(value=>Math.Abs(value));
                double rms=Math.Sqrt(samples.Average(value=>value*(double)value));
                Assert.That(rms,Is.GreaterThan(.00002),"Recorded birds did not reach the Unity mixer");
                Assert.That(peak,Is.LessThan(1),"Birdsong clipped the Unity mixer");
                File.WriteAllBytes(Path.Combine(output,"school-morning-birds.wav"),CloudExperienceTests.Wave(samples,rate,2));
                File.WriteAllText(Path.Combine(output,"measurement.json"),JsonUtility.ToJson(new BirdMixMeasurement{
                    scope="Isolated school birds, Unity main-output capture, device session muted; no device or subjective listening certification.",
                    rate=rate,samples=samples.Count,peak=peak,rms=rms,muted=true},true));
            }
            finally{AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=oldCapture;}
        }
        [Serializable] sealed class BirdMixMeasurement {public string scope;public int rate,samples;public double peak,rms;public bool muted;}
    }
}
