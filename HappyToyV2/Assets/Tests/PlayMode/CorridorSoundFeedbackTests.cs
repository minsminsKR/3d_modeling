using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Test]
        public void EverySnapperTakeContainsThreeSeparatedRecordedTicks()
        {
            for(int variant=0;variant<3;variant++)
            {
                var clip=CloudExternalAudioTests.Shared("firecracker",variant);
                Assert.That(clip,Is.Not.Null);clip.LoadAudioData();
                var samples=new float[clip.samples*clip.channels];Assert.That(clip.GetData(samples,0),Is.True);
                double Energy(float from,float to)
                {
                    int begin=Mathf.RoundToInt(from*clip.frequency)*clip.channels;
                    int end=Mathf.Min(samples.Length,Mathf.RoundToInt(to*clip.frequency)*clip.channels);
                    return samples.Skip(begin).Take(end-begin).Average(value=>(double)value*value);
                }
                foreach(float offset in new[]{0,.075f,.150f})
                    Assert.That(Energy(offset,offset+.06f),Is.GreaterThan(.00001),"A recorded tick is missing from variant "+variant);
                Assert.That(Energy(.061f,.073f),Is.LessThan(.0000001),"First two ticks merged into one long noise");
                Assert.That(Energy(.136f,.148f),Is.LessThan(.0000001),"Last two ticks merged into one long noise");
                Assert.That(Energy(.215f,.27f),Is.LessThan(.0000001),"Unexpected fourth tick/tail");
            }
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator MaskRunningContactsUseAnimalRecordingsReachTheMixerAndPause()
        {
            yield return PrepareCorridorMaskRunner();Call(shell,"RestoreDefaultSettings");
            var mask=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            var voice=mask.GetComponent(RequireType("CorridorMaskAudio"));
            var source=Get<AudioSource>(voice,"ContactSource");
            var clips=RecordedPrivate<AudioClip[]>(voice,"contactClips");
            Assert.That(clips.All(clip=>CloudExternalAudioTests.MatchesFamily(clip,"mask-beast-step",3)),Is.True);
            Assert.That(clips.Any(clip=>CloudExternalAudioTests.MatchesFamily(clip,"mask-heavy-step",3)),Is.False,"Human shoes/metal survived in the game runner");
            PlacePlayer(mask.transform.position-mask.transform.forward*2,false);
            ((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            ExternalMuteExcept(source);
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;
                started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                int before=Get<int>(voice,"ContactsPlayed");
                yield return ExternalCapture("real-running-animal-contacts",source,clips[0],"mask-beast-step",1.4f,rate,montage,segments);
                Assert.That(Get<int>(voice,"ContactsPlayed"),Is.GreaterThan(before),"Real motion emitted no paw contacts");
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Recorded animal contacts never reached the actual spatial mixer");
                Call(shell,"Pause");yield return null;before=Get<int>(voice,"ContactsPlayed");
                yield return ExternalCapture("animal-contacts-paused",source,clips[0],"mask-beast-step",.25f,rate,montage,segments);
                Assert.That(Get<int>(voice,"ContactsPlayed"),Is.EqualTo(before));
                Assert.That(segments.Last().peak,Is.LessThan(.00001));
            }
            finally{AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
        }
    }
}
