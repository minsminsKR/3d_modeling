using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Controlled validated checkpoint input, not a production event selector.
        object UseSchoolAnomalyFixture(Component world,string kind)
        {
            int traversal=Math.Max(1,Get<int>(world,"Traversals"));Call(world,"RestoreTraversal",traversal);
            var director=Get<Component>(world,"Anomalies");var data=Call(director,"Capture");var deck=Get<object>(data,"deck");
            Set(deck,"Kind",Enum.Parse(RequireType("SchoolAnomalyKind"),kind));Set(deck,"NormalStreak",kind=="Normal"?1:0);Set(data,"deck",deck);
            Call(director,"Restore",data,traversal);return data;
        }
        IEnumerator CrossSchoolJudgmentSeam(Component world,int direction)
        {
            int count=Get<int>(world,"Traversals");
            PlacePlayer(LoopOrigin+new Vector3(direction>0?6.6f:-6.6f,.03f,direction>0?46.1f:-7.7f));
            player.transform.rotation=Quaternion.Euler(0,direction>0?0:180,0);yield return null;
            Keys(Key.W);yield return Wait(()=>Get<int>(world,"Traversals")==count+1,3,"The real judgment seam did not transfer");Keys();yield return null;
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolAnomalyFirstPassIsNormalAndActualSeamsJudgeBothDirections()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var director=Get<Component>(world,"Anomalies");Assert.That(Get<object>(director,"Kind").ToString(),Is.EqualTo("Normal"));
            Assert.That(Get<Component>(director,"Baby"),Is.Null);Assert.That(Get<bool>(Get<Component>(world,"Walker"),"Dancing"),Is.False);
            yield return CrossSchoolJudgmentSeam(world,1);Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1));
            foreach(var choice in new[]{("Normal",1,4),("Normal",-1,0),("Baby",1,0),("Blackout",-1,4),("DancingWalker",-1,4)})
            {
                Call(shell,"Pause");UseSchoolAnomalyFixture(world,choice.Item1);Call(world,"RestoreClock",3);Call(shell,"Resume");
                yield return CrossSchoolJudgmentSeam(world,choice.Item2);
                Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(choice.Item3));
                foreach(var display in Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay")))
                    Assert.That(Get<int>(display,"Hours"),Is.EqualTo(choice.Item3));
            }
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerSolidCapsuleStopsActualPlayerInputAndDancerMovesFourTimesFaster()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var walker=Get<Component>(world,"Walker");var collider=Get<CapsuleCollider>(walker,"BodyCollider");
            Assert.That(collider.enabled&&!collider.isTrigger,Is.True);Assert.That(collider.radius,Is.GreaterThan(.4f));
            Call(shell,"Pause");var state=Call(walker,"CaptureState");Set(state,"Z",12f);Call(walker,"RestoreState",state);
            PlacePlayer(LoopOrigin+new Vector3(.65f,.03f,10.5f));player.transform.rotation=Quaternion.identity;Call(shell,"Resume");
            Keys(Key.W);yield return Delay(1.8f);Keys();
            Assert.That(player.transform.position.z-LoopOrigin.z,Is.GreaterThan(10.6f));
            Assert.That(walker.transform.position.z-player.transform.position.z,Is.GreaterThan(.6f),"The actual CharacterController passed through the pedestrian");
            Assert.That(Mathf.Abs(walker.transform.position.x-.65f),Is.LessThan(.01f));
            Call(shell,"Pause");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,8));UseSchoolAnomalyFixture(world,"DancingWalker");
            Call(walker,"RestoreTraversal",Get<int>(world,"Traversals"));var motion=Get<Component>(walker,"Motion");
            var bones=Get<Transform>(walker,"Visual").GetComponentsInChildren<Transform>();var before=bones.Select(b=>b.localRotation).ToArray();
            float distance=Get<float>(walker,"Distance");Call(shell,"Resume");yield return Delay(.55f);
            Assert.That(Get<bool>(walker,"Dancing")&&Get<bool>(motion,"Dancing"),Is.True);
            Assert.That(Get<float>(walker,"Distance")-distance,Is.GreaterThan(1.1f));
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(before[i],b.localRotation)).Sum(),Is.GreaterThan(80));
            Call(shell,"Pause");var paused=bones.Select(b=>(b.localPosition,b.localRotation)).ToArray();yield return Delay(.2f);
            CollectionAssert.AreEqual(paused,bones.Select(b=>(b.localPosition,b.localRotation)).ToArray());
            ExportSchoolAnomalyFrame(world,"dancing-walker",new Vector3(-1.2f,.03f,25),new Vector3(.65f,1.3f,31));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBabyAppearsAtTheStudySignCriesPursuesAfterTimeoutAndRetreatRemovesIt()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,15));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");yield return Delay(.15f);Assert.That(Get<Component>(director,"Baby"),Is.Null);
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16.1f));yield return Wait(()=>Get<Component>(director,"Baby"),3,"Passing the actual study sign did not summon Baby");
            var baby=Get<Component>(director,"Baby");var source=Get<AudioSource>(baby,"CrySource");var visual=Get<Transform>(baby,"Visual");
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.loop,Is.True);Assert.That(source.maxDistance,Is.GreaterThanOrEqualTo(40));
            Assert.That(visual.GetComponent(RequireType("EnemyStudioRig")),Is.Not.Null);
            foreach(string materialName in new[]{"BabyFade","BabyFadeEmissive"})
            {
                var retained=Resources.Load<Material>("SchoolAnomalies/"+materialName);
                Assert.That(retained&&retained.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")&&retained.IsKeywordEnabled("_NORMALMAP")&&retained.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),Is.True);
                Assert.That(retained.GetFloat("_Surface"),Is.EqualTo(1));
            }
            Assert.That(baby.GetComponent(RequireType("StalkerBrain")),Is.Null,"The retired corridor AI was accidentally re-enabled");
            var agent=Get<NavMeshAgent>(baby,"Agent");Assert.That(agent.isStopped,Is.True);
            yield return Delay(2.3f);Assert.That(Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),Is.False);
            ExternalMuteExcept(source);int rate=AudioSettings.outputSampleRate;bool started=false;float capture=Time.captureDeltaTime;
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                yield return ExternalCapture("school-baby-distant-cry",source,source.clip,"enemy-baby-cry",.5f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.GreaterThan(.00001),"The distant crying did not reach the real quiet mixer");
            }
            finally{if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
            var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);yield return null;
            var frozenClock=Call(Get<object>(director,"EventClock"),"Capture");var frozenPosition=baby.transform.position;
            yield return Delay(.25f);Assert.That(Call(Get<object>(director,"EventClock"),"Capture"),Is.EqualTo(frozenClock));
            Assert.That(baby.transform.position,Is.EqualTo(frozenPosition));Assert.That(source.isPlaying,Is.False);
            Call(rituals,"EndEffect");yield return null;
            Call(shell,"Pause");ExportSchoolAnomalyFrame(world,"baby-crying",new Vector3(-1.2f,.03f,23),new Vector3(0,1,35.4f));
            var saved=Call(director,"Capture");var clock=Get<object>(saved,"clock");Set(clock,"Elapsed",59.8);Set(saved,"clock",clock);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));baby=Get<Component>(director,"Baby");agent=Get<NavMeshAgent>(baby,"Agent");
            var held=Call(Get<object>(director,"EventClock"),"Capture");yield return Delay(.3f);Assert.That(Call(Get<object>(director,"EventClock"),"Capture"),Is.EqualTo(held));
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),1,"The 60-second pursuit deadline did not fire");
            var at=baby.transform.position;yield return Delay(.4f);Assert.That(Vector3.Distance(at,baby.transform.position),Is.GreaterThan(.2f));
            Call(world,"RestoreClock",2);yield return CrossSchoolJudgmentSeam(world,-1);
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(3));yield return null;
            Assert.That(!baby,Is.True,"Baby survived a successful anomaly retreat");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            yield return Wait(()=>Get<Component>(director,"Baby"),3,"The next Baby event did not appear");yield return Delay(2.3f);
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,30.5f));
            yield return Wait(()=>Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),1,"Actual close approach did not start pursuit");
            Assert.That(Get<double>(Get<object>(director,"EventClock"),"Elapsed"),Is.LessThan(60));
            baby=Get<Component>(director,"Baby");PlacePlayer(baby.transform.position+Vector3.forward*.55f);
            yield return Wait(()=>Get<bool>(session,"Finished"),2,"Actual Baby contact did not cause defeat");
            Assert.That(Get<string>(session,"DefeatSource"),Does.Contain("베이비"));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBlackoutExtinguishesFromFarEndRestoresOnRetreatAndKillsAfterFiveDarkSeconds()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Blackout");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");var clock=Get<object>(director,"EventClock");
            yield return Wait(()=>Get<bool>(clock,"Triggered"),2,"Study-sign crossing did not start the blackout");
            Call(shell,"Pause");var saved=Call(director,"Capture");var timing=Get<object>(saved,"clock");Set(timing,"Elapsed",2.5);Set(saved,"clock",timing);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));
            var aisle=Get<Transform>(world,"Centre").GetComponentsInChildren(RequireType("SchoolFluorescent"))
                .Where(f=>Mathf.Abs(f.transform.localPosition.x)<.2f).OrderByDescending(f=>f.transform.localPosition.z).ToArray();
            Assert.That(Get<Light>(aisle[0],"Source").intensity,Is.Zero);Assert.That(Get<Light>(aisle[1],"Source").intensity,Is.Zero);
            Assert.That(Get<Light>(aisle[2],"Source").intensity,Is.GreaterThan(0),"The near lights went out before the far lights");
            ExportSchoolAnomalyFrame(world,"blackout-progress",new Vector3(.72f,.03f,16.2f),new Vector3(0,1.5f,36));
            Call(shell,"Resume");yield return CrossSchoolJudgmentSeam(world,-1);Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1));
            Assert.That(aisle.All(f=>Get<Light>(f,"Source").intensity>0),Is.True,"Retreat left the next passage's lamps switched off");
            Call(shell,"Pause");saved=UseSchoolAnomalyFixture(world,"Blackout");timing=Get<object>(saved,"clock");Set(timing,"Triggered",true);Set(timing,"Elapsed",13.5);Set(saved,"clock",timing);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));Assert.That(Get<int>(Get<Component>(director,"Blackout"),"Extinguished"),Is.EqualTo(9));
            Assert.That(Get<float>(Get<Component>(director,"Blackout"),"Darkness"),Is.EqualTo(1));
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));yield return Delay(.3f);Assert.That(Get<bool>(session,"Finished"),Is.False);
            var camera=Get<Camera>(player,"eyes");var frame=SchoolHorrorFrame(camera,320,180);
            Assert.That(SchoolDisplayLuma(frame,new Rect(.1f,.1f,.8f,.8f)),Is.LessThan(.01f),"The real production camera did not fall into darkness");Object.Destroy(frame);
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(session,"Finished"),2,"Five seconds in full darkness did not cause defeat");
            Assert.That(Get<bool>(session,"Escaped"),Is.False);
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolAnomalyCheckpointKeepsTheDrawTimersBabyPoseAndLegacyNormalMigration()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");yield return Wait(()=>Get<Component>(director,"Baby"),3,"Baby did not appear");yield return Delay(.3f);
            Call(shell,"Pause");var checkpoint=Call(session,"CaptureChapterCheckpoint");var saved=Get<object>(Get<object>(checkpoint,"loop"),"anomalies");
            string json=(string)Call(checkpoint,"ToJson");var copy=Call(RequireType("ChapterCheckpoint"),"FromJson",json);Call(copy,"Validate");
            var expectedBaby=Get<Component>(director,"Baby").transform.position;
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",copy);Call(session,"ApplyChapterCheckpoint",copy);world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");director=Get<Component>(world,"Anomalies");
            var restored=Call(director,"Capture");Assert.That(Get<object>(restored,"deck"),Is.EqualTo(Get<object>(saved,"deck")));
            Assert.That(Get<object>(restored,"clock"),Is.EqualTo(Get<object>(saved,"clock")));
            Assert.That(Vector3.Distance(Get<Component>(director,"Baby").transform.position,expectedBaby),Is.LessThan(.02f));
            var invalid=Call(copy,"Copy");var bad=Get<object>(Get<object>(invalid,"loop"),"anomalies");var body=Get<object>(bad,"baby");
            Assert.That(Physics.Raycast(LoopOrigin+new Vector3(0,.9f,20),Vector3.right,out var wall,3,1<<8,QueryTriggerInteraction.Ignore),Is.True,
                "The corruption fixture requires an actual solid aisle wall, not the open exit elbow");
            var insideWall=wall.point-LoopOrigin-wall.normal*.05f;insideWall.y=0;
            Set(body,"position",insideWall);Set(bad,"baby",body);
            var unchanged=player.transform.position;Assert.Catch<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",invalid));
            Assert.That(player.transform.position,Is.EqualTo(unchanged));
            Assert.That(Get<object>(Call(director,"Capture"),"deck"),Is.EqualTo(Get<object>(saved,"deck")));
            var legacy=Call(copy,"Copy");Set(Get<object>(legacy,"loop"),"anomalies",null);
            legacy=Call(RequireType("ChapterCheckpoint"),"FromJson",(string)Call(legacy,"ToJson"));Assert.That(Get<object>(Get<object>(legacy,"loop"),"anomalies"),Is.Null);
            previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",legacy);Call(session,"ApplyChapterCheckpoint",legacy);world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Assert.That(Get<object>(Get<Component>(world,"Anomalies"),"Kind").ToString(),Is.EqualTo("Normal"));
        }
        void ExportSchoolAnomalyFrame(Component world,string name,Vector3 feet,Vector3 target)
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR");if(string.IsNullOrWhiteSpace(output))return;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(output);
            var camera=Get<Camera>(player,"eyes");var before=player.transform.position;var rotation=camera.transform.rotation;
            PlacePlayer(LoopOrigin+feet,false);camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+target-camera.transform.position);
            var frame=SchoolHorrorFrame(camera,1280,720);File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());Object.Destroy(frame);
            PlacePlayer(before,false);camera.transform.rotation=rotation;
        }
    }
}
