using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator LadyRecordedPulseAcceleratesWithDistanceAndFlatlineSurvivesConfirmedDeathResult()
        {
            yield return LadyPrepare();Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            LadyCollect(0,5);var pulse=Get<Component>(LadyFinale,"PulseAudio");
            var heart=Get<AudioSource>(pulse,"HeartbeatSource");var flat=Get<AudioSource>(pulse,"FlatlineSource");
                Assert.That(flat.clip.length,Is.EqualTo(3).Within(.001f));
            Assert.That(Get<bool>(LadyFinale,"Hunting"),Is.False);
            yield return Delay(.35f);
            Assert.That(Get<int>(pulse,"Beats"),Is.GreaterThan(0));Assert.That(heart.volume,Is.InRange(.001f,.04f));
            yield return Wait(()=>Get<Component>(LadyFinale,"Actor"),3,"Lady never spawned for pulse distance test");
            var actor=Get<Component>(LadyFinale,"Actor");((Behaviour)actor).enabled=false;
            var agent=actor.GetComponent<NavMeshAgent>();agent.isStopped=true;
            var far=Enumerable.Range(0,81).Select(i=>(Vector3)Call(LadyRun,"CellPosition",i)).OrderByDescending(p=>Vector3.Distance(p,player.transform.position)).First();
            Assert.That(NavMesh.SamplePosition(far,out var farHit,1,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(farHit.position),Is.True);Assert.That(Vector3.Distance(actor.transform.position,player.transform.position),Is.GreaterThan(32));
            ExternalMuteExcept(heart,flat);
            var segments=new List<CloudExternalAudioTests.Segment>();var montage=new List<float>();
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);AudioListener.volume=1;
                yield return ExternalFlush(rate,2);
                int before=Get<int>(pulse,"Beats");
                yield return ExternalCapture("lady-distant-heart",heart,heart.clip,"lady-heartbeat",5,rate,montage,segments);
                int farBeats=Get<int>(pulse,"Beats")-before;
                double farRms=segments.Last().rms;Assert.That(farRms,Is.GreaterThan(.00002));
                Assert.That(NavMesh.SamplePosition(player.transform.position,out var nearHit,1,NavMesh.AllAreas),Is.True);
                Assert.That(agent.Warp(nearHit.position),Is.True);
                yield return ExternalFlush(rate,2);
                before=Get<int>(pulse,"Beats");
                yield return ExternalCapture("lady-near-heart",heart,heart.clip,"lady-heartbeat",5,rate,montage,segments);
                int nearBeats=Get<int>(pulse,"Beats")-before;
                Assert.That(nearBeats,Is.GreaterThanOrEqualTo(farBeats*2));Assert.That(Get<float>(pulse,"Bpm"),Is.GreaterThan(155));
                Assert.That(segments.Last().rms,Is.GreaterThan(farRms*2));Assert.That(heart.pitch,Is.EqualTo(1));
                Call(shell,"Pause");yield return ExternalFlush(rate,.3f);before=Get<int>(pulse,"Beats");
                yield return ExternalCapture("lady-heart-pause",heart,heart.clip,"lady-heartbeat",.35f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));Assert.That(Get<int>(pulse,"Beats"),Is.EqualTo(before));
                Call(shell,"Resume");
                var ritual=Get<Component>(player,"Rituals");Call(ritual,"Restore",1,0,1);Assert.That((bool)Call(ritual,"UseTalisman"),Is.True);
                before=Get<int>(pulse,"Beats");
                yield return ExternalCapture("lady-heart-during-talisman",heart,heart.clip,"lady-heartbeat",1,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002));Assert.That(Get<int>(pulse,"Beats"),Is.GreaterThan(before));Call(ritual,"EndEffect");
                AudioListener.volume=0;yield return ExternalFlush(rate,.2f);
                yield return ExternalCapture("lady-heart-master-muted",heart,heart.clip,"lady-heartbeat",.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));AudioListener.volume=1;
                ((Behaviour)actor).enabled=true;agent.isStopped=false;agent.Warp(nearHit.position);
                yield return ExternalFlush(rate,.4f);Assert.That(Get<bool>(pulse,"Captured"),Is.False,"Safe mode triggered a false terminal tone");
                Call(session,"ToggleCheatSafeMode");
                yield return Wait(()=>Get<bool>(session,"Finished"),4,"Actual Lady contact never committed defeat");
                Assert.That(Get<string>(session,"DefeatSource"),Is.EqualTo("lady"));Assert.That(Get<bool>(pulse,"Captured"),Is.True);
                Assert.That(heart.isPlaying,Is.False);Assert.That(flat.ignoreListenerPause,Is.True);
                before=Get<int>(pulse,"Beats");
                yield return ExternalCapture("lady-terminal-tone-on-result",flat,flat.clip,"lady-flatline",2.6f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.001));Assert.That(Get<int>(pulse,"Beats"),Is.EqualTo(before));
                Assert.That((bool)Call(pulse,"MarkCaught"),Is.False,"Repeated contact restarted the terminal tone");
                yield return ExternalFlush(rate,.7f);Assert.That(flat.isPlaying,Is.False,"Terminal tone exceeded its three-second recorded sustain");
                CloudExperienceTests.Artifact("lady-heartbeat-actual-mixer.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CloudExternalAudioTests.Report{
                    scope="Quiet pre-device production voices: fixed-pitch heartbeat at far/near positions, pause, continuing player pulse during talisman, master mute, real lethal contact and terminal tone on Result. Far/near beat counts: "+farBeats+"/"+nearBeats,
                    segments=segments.ToArray()},true)));
                var owned=flat.clip;var shared=CloudExternalAudioTests.Shared("lady-flatline");
                Assert.That(owned,Is.Not.SameAs(shared));Object.Destroy(pulse);yield return null;yield return null;
                Assert.That(owned==null,Is.True);Assert.That(shared!=null,Is.True,"Removing the owner destroyed the imported shared recording");
            }
            finally{AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
        }
    }
}
