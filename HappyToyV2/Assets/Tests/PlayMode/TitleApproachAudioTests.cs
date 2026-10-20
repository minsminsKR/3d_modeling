using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(60000)]
        public IEnumerator TitleRecordedPerformanceKeepsEveryContactWithoutFurtherMainThreadTicks()
        {
            var owner=new GameObject("CloudQA owned title performance");
            object performance=Activator.CreateInstance(RequireType("TitleApproachFootsteps"),new object[]{owner.transform});
            var voice=Get<AudioSource>(performance,"Source");var clip=Get<AudioClip>(performance,"PerformanceClip");
            var shared=CloudExternalAudioTests.Shared("mask-beast-step");
            bool started=false;float capture=Time.captureDeltaTime;
            try
            {
                Assert.That(clip.length,Is.EqualTo(4.25f).Within(.001f));
                Assert.That(voice.ignoreListenerPause,Is.True);Assert.That(voice.ignoreListenerVolume,Is.False);
                ExternalMuteExcept(voice);Time.captureDeltaTime=1f/60;yield return null;
                started=StartOwnedAudioRenderer();Assert.That(started,Is.True);AudioListener.volume=1;
                Assert.That(AudioListener.pause,Is.True,"Fixture must retain the production title listener pause");
                Call(performance,"Tick",true,0f,8f,1f);
                var samples=new List<float>();int rate=AudioSettings.outputSampleRate;
                // No more Tick calls: a stalled render/update loop cannot
                // interrupt native playback of the already prepared contacts.
                yield return CaptureAudio(samples,rate*6,8);
                var rms=new List<double>();
                for(int contact=0;contact<9;contact++)
                {
                    int first=Mathf.RoundToInt(contact*.32f*rate)*2;
                    int count=Mathf.RoundToInt(.30f*rate)*2;
                    double energy=0;for(int i=first;i<first+count;i++)energy+=samples[i]*(double)samples[i];
                    rms.Add(Math.Sqrt(energy/count));
                }
                Assert.That(rms.All(x=>x>.001),Is.True,"A recorded contact disappeared without a main-thread update: "+string.Join(",",rms));
                Assert.That(samples.Max(x=>Mathf.Abs(x)),Is.LessThan(.99f));
                CloudExperienceTests.Artifact("title-uninterrupted-contacts.json",System.Text.Encoding.UTF8.GetBytes(
                    "{\"scope\":\"Quiet pre-device native playback, one initial Tick then three seconds without another Tick; not a device listening certificate\",\"contacts\":9,\"rms\":["+
                    string.Join(",",rms.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"]}"));
                Call(performance,"Stop");yield return ExternalFlush(rate,.1f);
                var stopped=new List<float>();yield return CaptureAudio(stopped,rate/5*2,4);
                Assert.That(stopped.Max(x=>Mathf.Abs(x)),Is.LessThan(.00001f));
            }
            finally
            {
                AudioListener.volume=0;Call(performance,"Dispose");UnityEngine.Object.Destroy(owner);
                if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;
            }
            yield return null;yield return null;
            Assert.That(clip==null,Is.True);Assert.That(shared!=null,Is.True,"Title disposal destroyed the shared in-game recording");
        }
    }
}
