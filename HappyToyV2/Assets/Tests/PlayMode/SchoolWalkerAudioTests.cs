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
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerRecordedTileContactsReachTheSpatialMixerAndStopForObstructionPauseTalismanAndMute()
        {
            yield return BeginObservationSchool();Call(shell,"RestoreDefaultSettings");
            var walker=One("SchoolWalkerEncounter");var voice=Get<Component>(walker,"Footsteps");
            var source=Get<AudioSource>(voice,"Source");var clips=RecordedPrivate<AudioClip[]>(voice,"contacts");
            var imported=Resources.Load<AudioClip>("Audio/External/step-stone-0");
            Assert.That(clips.Length,Is.EqualTo(5));
            Assert.That(clips.All(clip=>clip&&clip!=imported&&CloudExternalAudioTests.MatchesFamily(clip,"step-stone",5)),Is.True);
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.rolloffMode,Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.minDistance,Is.EqualTo(3));Assert.That(source.maxDistance,Is.EqualTo(38));
            Assert.That(source.ignoreListenerPause||source.ignoreListenerVolume||source.loop,Is.False);
            ((Behaviour)player).enabled=false;
            Call(shell,"Pause");var state=Call(walker,"CaptureState");Set(state,"Z",15f);Set(state,"Distance",18.2f);
            Call(walker,"RestoreState",state);PlacePlayer(LoopOrigin+new Vector3(.65f,.03f,12),false);Call(shell,"Resume");
            ExternalMuteExcept(source);
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                int before=Get<int>(voice,"StepsPlayed");
                yield return ExternalCapture("school-pedestrian-walking",source,clips[0],"step-stone",1.4f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.GreaterThanOrEqualTo(before+2));
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Walking contacts did not reach the actual listener mixer");
                PlacePlayer(walker.transform.position+walker.transform.forward*.81f,false);
                yield return Delay(.4f);yield return ExternalFlush(rate);
                before=Get<int>(voice,"StepsPlayed");var at=walker.transform.position;
                yield return ExternalCapture("school-pedestrian-blocked",source,clips[0],"step-stone",.8f,rate,montage,segments);
                Assert.That(walker.transform.position,Is.EqualTo(at));Assert.That(Get<int>(voice,"StepsPlayed"),Is.EqualTo(before));
                Assert.That(segments.Last().peak,Is.LessThan(.00001));
                PlacePlayer(walker.transform.position+Vector3.left*1.5f,false);
                yield return ExternalCapture("school-pedestrian-lane-cleared",source,clips[0],"step-stone",1.4f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.GreaterThan(before));Assert.That(segments.Last().rms,Is.GreaterThan(.00002));
                Call(shell,"Pause");yield return null;before=Get<int>(voice,"StepsPlayed");yield return ExternalFlush(rate);
                yield return ExternalCapture("school-pedestrian-paused",source,clips[0],"step-stone",.25f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.EqualTo(before));Assert.That(segments.Last().peak,Is.LessThan(.00001));
                Call(shell,"Resume");var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
                Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);yield return null;before=Get<int>(voice,"StepsPlayed");
                yield return ExternalFlush(rate);
                yield return ExternalCapture("school-pedestrian-time-stop",source,clips[0],"step-stone",.25f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.EqualTo(before));Assert.That(segments.Last().peak,Is.LessThan(.00001));
                Call(rituals,"EndEffect");AudioListener.volume=0;yield return ExternalFlush(rate);
                yield return ExternalCapture("school-pedestrian-master-zero",source,clips[0],"step-stone",.8f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.GreaterThan(before));Assert.That(segments.Last().peak,Is.LessThan(.00001));
                AudioListener.volume=1;((Behaviour)voice).enabled=false;before=Get<int>(voice,"StepsPlayed");yield return ExternalFlush(rate);
                yield return ExternalCapture("school-pedestrian-voice-disabled",source,clips[0],"step-stone",.8f,rate,montage,segments);
                Assert.That(Get<int>(voice,"StepsPlayed"),Is.EqualTo(before));Assert.That(segments.Last().peak,Is.LessThan(.00001));
                ((Behaviour)voice).enabled=true;
                AudioListener.volume=1;PlacePlayer(LoopOrigin+new Vector3(0,5,90),false);yield return ExternalFlush(rate);
                yield return ExternalCapture("school-pedestrian-beyond-range",source,clips[0],"step-stone",.8f,rate,montage,segments);
                Assert.That(source.volume,Is.Zero);Assert.That(segments.Last().peak,Is.LessThan(.00001));
            }
            finally{AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
            var owned=clips.ToArray();var emitter=source.gameObject;Object.Destroy(voice);yield return null;yield return null;
            Assert.That(!voice&&!source&&!emitter,Is.True);Assert.That(owned.All(clip=>!clip),Is.True);
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(!walker&&!voice&&!source,Is.True);Assert.That(owned.All(clip=>!clip),Is.True);
            Assert.That(Resources.Load<AudioClip>("Audio/External/step-stone-0"),Is.SameAs(imported));
        }
    }
}
