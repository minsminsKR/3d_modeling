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
        [UnityTest,Timeout(180000)]
        public IEnumerator UncatRecordedBellCarriesAcrossDistantPassagesWithDirectionAndWallTransmission()
        {
            yield return RevisionPrepare();Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            var actor=ReadabilityActor("Uncat");actor.gameObject.SetActive(true);
            // Actual walking/rhythm is covered by UncatActualWalkingBells... . This
            // controlled emitter fixture measures its production spatial mix only.
            ((Behaviour)actor).enabled=false;actor.GetComponent<NavMeshAgent>().enabled=false;
            var bell=actor.GetComponent(RequireType("UncatBellVoice"));((Behaviour)bell).enabled=false;
            var source=Get<AudioSource>(bell,"BellSource");var clip=RecordedPrivate<AudioClip>(bell,"bell");
            var acoustics=source.GetComponent(RequireType("EnemyAcoustics"));
            Assert.That(CloudExternalAudioTests.MatchesFamily(clip,"enemy-uncat-bell",1),Is.True);
            Assert.That(source.maxDistance,Is.EqualTo(48));Assert.That(source.minDistance,Is.EqualTo(3));
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.rolloffMode,Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.ignoreListenerPause||source.ignoreListenerVolume,Is.False);
            // Above the generated layout gives known clear rays at every distance;
            // the inserted solid wall then exercises the real transmission filter.
            Vector3 origin=new Vector3(0,40,0);actor.transform.position=origin;
            var camera=Get<Camera>(player,"eyes");ExternalMuteExcept(source);
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;GameObject wall=null;
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            double Channel(CloudExternalAudioTests.Segment segment,int channel)=>System.Math.Sqrt(
                Enumerable.Range(0,segment.samples/2).Average(i=>{double value=montage[segment.offset+i*2+channel];return value*value;}));
            IEnumerator Record(string label,Vector3 offset)
            {
                PlacePlayer(origin+offset,false);camera.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
                source.Stop();yield return Delay(.4f);yield return ExternalFlush(rate,.15f);source.PlayOneShot(clip);
                yield return ExternalCapture(label,source,clip,"enemy-uncat-bell",.85f,rate,montage,segments);
            }
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                yield return Record("bell-near-3m",Vector3.back*3);double near=segments.Last().rms;
                Assert.That(near,Is.GreaterThan(.001));
                yield return Record("bell-far-24m",Vector3.back*24);double mid=segments.Last().rms;
                Assert.That(mid,Is.GreaterThan(.0005),"Bell is inaudible beyond the former 18m cutoff");
                yield return Record("bell-far-36m",Vector3.back*36);double far=segments.Last().rms;
                Assert.That(far,Is.GreaterThan(.00025),"The distant approaching bell never reaches the listener");
                Assert.That(near,Is.GreaterThan(mid*1.5));Assert.That(mid,Is.GreaterThan(far*1.5),"Approach lost its distance gradient");
                yield return Record("bell-right-24m",Vector3.left*24);
                Assert.That(Channel(segments.Last(),1),Is.GreaterThan(Channel(segments.Last(),0)*1.25),"Distant bell lost its right bearing");
                yield return Record("bell-left-24m",Vector3.right*24);
                Assert.That(Channel(segments.Last(),0),Is.GreaterThan(Channel(segments.Last(),1)*1.25),"Distant bell lost its left bearing");
                wall=Cube("CloudQA distant bell physical wall",origin+new Vector3(0,1.3f,-12),new Vector3(5,4,.2f));
                yield return Record("bell-through-wall-24m",Vector3.back*24);
                Assert.That(Get<bool>(acoustics,"Occluded"),Is.True);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"A wall completely erases the approaching bell");
                Assert.That(segments.Last().rms,Is.LessThan(mid*.65),"Wall transmission lost its muffling");
                Object.Destroy(wall);wall=null;
                yield return Record("bell-outside-50m",Vector3.back*50);
                Assert.That(source.volume,Is.Zero);Assert.That(segments.Last().peak,Is.LessThan(.00001));
                PlacePlayer(origin+Vector3.back*24,false);yield return Delay(.4f);
                Call(shell,"AdjustSettings",-1f,0f);yield return Record("bell-master-zero",Vector3.back*24);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));Call(shell,"RestoreDefaultSettings");
                source.Stop();source.PlayOneShot(clip);Call(shell,"Pause");yield return ExternalFlush(rate,.2f);
                yield return ExternalCapture("bell-paused",source,clip,"enemy-uncat-bell",.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));
                CloudExperienceTests.Artifact("uncat-bell-distance-mix.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report {scope="Quiet isolated production recorded bell, positioned 3/24/36/50m, stereo bearing, physical wall, master mute and pause. Companion real locomotion test covers natural emission. Not a device listening verdict.",segments=segments.ToArray()},true)));
                Debug.Log("HAPPYTOY_UNCAT_DISTANCE_BELL_PASS near="+near+" mid24="+mid+" far36="+far);
            }
            finally{if(wall)Object.Destroy(wall);AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
        }
    }
}
