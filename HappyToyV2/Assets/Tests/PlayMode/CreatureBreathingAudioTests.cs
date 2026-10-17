using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator CyclopseRecordedBreathingMixFollowsChaseWallPauseMuteRangeAndOwnership()
        {
            yield return RevisionPrepare();Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            var run=Get<Component>(session,"Corridor");var centre=(Vector3)Call(run,"CellPosition",0);
            var actor=ReadabilityActor("Cyclopse");var voice=actor.GetComponent(RequireType("CyclopseBreathingVoice"));
            Assert.That(voice,Is.Not.Null);actor.gameObject.SetActive(true);((Behaviour)actor).enabled=true;
            actor.GetComponent<NavMeshAgent>().enabled=false;actor.transform.position=centre;Set(actor,"state","Patrol");
            PlacePlayer(centre+Vector3.back*1.8f,false);Physics.SyncTransforms();yield return null;
            var source=Get<AudioSource>(voice,"BreathSource");var clip=source.clip;
            var acoustics=source.GetComponent(RequireType("EnemyAcoustics"));
            Assert.That(source.loop,Is.True);Assert.That(source.transform.IsChildOf(actor.transform),Is.True);
            Assert.That(CloudExternalAudioTests.MatchesFamily(clip,"enemy-cyclopse-breath",1),Is.True);
            Assert.That(clip,Is.Not.SameAs(CloudExternalAudioTests.Shared("enemy-cyclopse-breath")));
            ExternalMuteExcept(source);
            var segments=new List<CloudExternalAudioTests.Segment>();var montage=new List<float>();
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;GameObject wall=null;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                yield return ExternalFlush(rate,.4f);source.timeSamples=clip.frequency*2;
                yield return ExternalCapture("cyclopse-near",source,clip,"enemy-cyclopse-breath",2f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"The actual spatial breathing voice is silent");
                float clear=source.volume;
                wall=Cube("CloudQA breathing occluder",centre+new Vector3(0,1.4f,-.9f),new Vector3(2,2.8f,.18f));
                Physics.SyncTransforms();yield return Wait(()=>Get<bool>(acoustics,"Occluded"),2,"Breathing bypasses a physical wall");
                yield return Delay(.4f);Assert.That(source.volume,Is.LessThan(clear*.65f));
                Object.Destroy(wall);wall=null;yield return null;
                yield return Wait(()=>!Get<bool>(acoustics,"Occluded"),2,"Breathing never clears its removed wall");
                Call(shell,"Pause");yield return null;int paused=source.timeSamples;
                yield return ExternalFlush(rate,.3f);yield return ExternalCapture("cyclopse-paused",source,clip,"enemy-cyclopse-breath",.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));Assert.That(source.timeSamples,Is.EqualTo(paused));
                Call(shell,"Resume");Set(actor,"state","Chase");yield return null;
                Assert.That(Get<bool>(voice,"FastBreathing"),Is.True);Assert.That(source.pitch,Is.GreaterThan(1));
                Call(shell,"AdjustSettings",-1f,0f);yield return ExternalFlush(rate,.3f);
                yield return ExternalCapture("cyclopse-master-zero",source,clip,"enemy-cyclopse-breath",.3f,rate,montage,segments);
                Assert.That(AudioListener.volume,Is.Zero);Assert.That(segments.Last().peak,Is.LessThan(.00001));
                Call(shell,"RestoreDefaultSettings");PlacePlayer(centre+Vector3.back*30,false);yield return null;yield return ExternalFlush(rate,.5f);
                yield return ExternalCapture("cyclopse-beyond-range",source,clip,"enemy-cyclopse-breath",.3f,rate,montage,segments);
                Assert.That(source.volume,Is.Zero);Assert.That(segments.Last().peak,Is.LessThan(.00001));
                PlacePlayer(centre+Vector3.back*1.8f,false);yield return null;
                var ritual=Get<Component>(player,"Rituals");Call(ritual,"Restore",1,0,1);
                Assert.That((bool)Call(ritual,"UseTalisman"),Is.True);yield return null;int held=source.timeSamples;
                yield return Delay(.2f);Assert.That(source.timeSamples,Is.EqualTo(held),"The held creature keeps breathing through time stop");
                Call(ritual,"EndEffect");yield return null;
                ((Behaviour)actor).enabled=false;yield return null;Assert.That(source.isPlaying,Is.False);
                Object.Destroy(voice);yield return null;yield return null;
                Assert.That(!clip&&!source,Is.True);Assert.That(CloudExternalAudioTests.Shared("enemy-cyclopse-breath"),Is.Not.Null);
                CloudExperienceTests.Artifact("cyclopse-breath-mix.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CloudExternalAudioTests.Report {
                    scope="Quiet actual production spatial voice and Unity pre-device mixer; controlled actor placement/state, physical wall, pause/master mute/range/time-stop and owned cleanup. No device listening or natural-play certification.",segments=segments.ToArray()},true)));
            }
            finally{if(wall)Object.Destroy(wall);AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
        }
    }
}
