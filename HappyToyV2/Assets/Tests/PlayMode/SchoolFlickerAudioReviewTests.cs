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
        // Opt-in actual Unity mixer capture. The existing diagnostic guard
        // must mute this process's device session before positive gain is used.
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolFlickerActualMixerReview()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_FLICKER_AUDIO_REVIEW_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False);Directory.CreateDirectory(output);
            var silence=RequireType("DiagnosticAudioSilence");
            var muted=silence.GetProperty("DeviceSessionMuted");Assert.That(muted,Is.Not.Null);
            yield return (IEnumerator)Call(silence,"WaitForSafeAudio",output,true);
            Assert.That((bool)muted.GetValue(null),Is.True,"Device playback must be muted before capture");
            yield return BeginObservationSchool();Call(shell,"Pause");UseSchoolAnomalyFixture(Get<Component>(Get<Component>(session,"Chapter"),"Loop"),"DancingWalker");
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var walker=Get<Component>(world,"Walker");
            var state=Call(walker,"CaptureState");Set(state,"X",0f);Set(state,"Z",33f);Set(state,"Yaw",180f);Set(state,"Elapsed",0d);Set(state,"IdleElapsed",0d);Call(walker,"RestoreState",state);
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,24));Call(shell,"Resume");
            int rate=AudioSettings.outputSampleRate;Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
            float previous=Time.captureDeltaTime;bool started=false;var samples=new List<float>();
            try
            {
                Time.captureDeltaTime=1f/60f;yield return null;started=StartOwnedAudioRenderer();
                Assert.That(started,Is.True,"Unity audio renderer could not start");
                yield return CaptureAudio(samples,rate*2*2,12);
                var atmosphere=Get<Component>(walker,"FlickerAtmosphere");
                Assert.That(Get<int>(atmosphere,"StepsPlayed"),Is.GreaterThan(0),"Capture did not include a real creature movement cue");
                File.WriteAllBytes(Path.Combine(output,"flicker-game-mix.wav"),CloudExperienceTests.Wave(samples,rate,2));
                double peak=samples.Max(value=>Math.Abs(value));double rms=Math.Sqrt(samples.Average(value=>value*(double)value));
                File.WriteAllText(Path.Combine(output,"measurement.json"),JsonUtility.ToJson(new FlickerMixMeasurement{sampleRate=rate,samples=samples.Count,peak=peak,rms=rms,deviceSessionMuted=(bool)muted.GetValue(null)},true));
                Assert.That(rms,Is.GreaterThan(.00002),"The actual event mix is silent");
                Assert.That(peak,Is.LessThan(1),"The actual event mix clips");
            }
            finally{if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=previous;}
        }
        [Serializable] sealed class FlickerMixMeasurement {public int sampleRate,samples;public double peak,rms;public bool deviceSessionMuted;}
    }
}
