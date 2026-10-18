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
        Component LadyRun=>Get<Component>(session,"Corridor");
        Component LadyFinale=>Get<Component>(LadyRun,"Lady");
        void LadyCollect(int first,int last)
        {for(int i=first;i<last;i++)Call(session,"CheatCollectNextMemory");}
        IEnumerator LadyPrepare()
        {yield return RevisionPrepare();Call(session,"ToggleCheatSafeMode");}

        [UnityTest,Timeout(120000)]
        public IEnumerator LadyFinalMemoryBlacksOutCandlesFreezesOldActorsAndRestoresWithoutReplay()
        {
            yield return LadyPrepare();LadyCollect(0,4);IsolateThreats();
            var candles=Components("WaymarkCandle");Assert.That(candles.Length,Is.GreaterThan(10));
            Call(candles[0],"Restore",true);Assert.That(Get<bool>(candles[0],"HasBeenLit"),Is.True);
            var old=ReadabilityActor("Cyclopse");old.gameObject.SetActive(true);((Behaviour)old).enabled=true;
            var face=old.GetComponent(RequireType("EnemyChaseFace"));
            yield return Wait(()=>Get<bool>(face,"Prepared"),3,"The old creature's owned face did not prepare");
            var mask=Get<Component>(LadyRun,"Mask");mask.gameObject.SetActive(true);((Behaviour)mask).enabled=true;
            var body=Get<Transform>(mask,"body");
            yield return Wait(()=>body.gameObject.activeInHierarchy,3,"The actual grown wraith never became visible");
            // Controlled real mesh pose, using the same production operation
            // as a cabinet capture. Separate regressions exercise sight-driven
            // morphing; this fixture tests holding an already open mouth.
            RequireType("EnemyChaseFace").GetMethod("HoldCabinetDeathFace",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(face,null);
            float expression=Get<float>(face,"Weight");Assert.That(expression,Is.GreaterThan(.95f));
            var at=old.transform.position;LadyCollect(4,5);
            Assert.That(Get<bool>(LadyFinale,"Active"),Is.True);Assert.That(Get<bool>(LadyFinale,"Hunting"),Is.False);
            Assert.That((bool)Call(LadyFinale,"Begin"),Is.False);
            Assert.That(Get<int>(LadyFinale,"FrozenActorCount"),Is.GreaterThan(0));
            Assert.That(candles.All(c=>Get<bool>(c,"FinaleBlackout")&&!Get<bool>(c,"Lit")&&!Get<Light>(c,"LocalLight").enabled),Is.True);
            Assert.That(Get<bool>(candles[0],"HasBeenLit"),Is.True);
            Call(shell,"Pause");float remaining=Get<float>(LadyFinale,"Remaining");
            yield return Delay(.15f);Assert.That(Get<float>(LadyFinale,"Remaining"),Is.EqualTo(remaining));
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(LadyFinale,"Hunting"),3,"Lady did not emerge after silence");
            Assert.That(Vector3.Distance(old.transform.position,at),Is.LessThan(.0001f));Assert.That(((Behaviour)old).enabled,Is.False);
            Assert.That(Get<float>(face,"Weight"),Is.EqualTo(expression),"The frozen monster's actual face continued morphing");
            Assert.That(body.gameObject.activeInHierarchy,Is.True,"Freezing the wraith hid its existing visible body");
            var actor=Get<Component>(LadyFinale,"Actor");Assert.That(actor.GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);
            Assert.That(Vector3.Distance(actor.transform.position,player.transform.position),Is.GreaterThan(8));
            yield return Delay(.12f);Call(shell,"Pause");var saved=Call(session,"CaptureCheckpoint");
            var pose=Get<object>(saved,"lady");var point=Get<Vector3>(pose,"position");float clock=Get<float>(pose,"poseClock");
            yield return RestoreCheckpointInFreshScene(saved);
            actor=Get<Component>(LadyFinale,"Actor");
            Assert.That(Vector3.Distance(actor.transform.position,point),Is.LessThan(.01f));
            Assert.That(Get<float>(Get<Component>(actor,"Motion"),"Clock"),Is.EqualTo(clock).Within(.001f));
            var impact=Get<AudioSource>(Get<Component>(LadyFinale,"AudioScope"),"ImpactSource");
            Assert.That(impact.isPlaying,Is.False,"Restore replayed the transition impact");
            Assert.That(Components("WaymarkCandle").All(c=>!Get<bool>(c,"Lit")),Is.True);
            var legacy=CheckpointCopy(saved);Set(legacy,"ladyVersion",0);Set(legacy,"lady",null);Call(legacy,"Validate");
            var invalid=CheckpointCopy(saved);Set(Get<object>(invalid,"lady"),"position",new Vector3(0,0,0));
            Assert.Throws<System.ArgumentException>(()=>Call(LadyFinale,"ValidateRestore",Get<object>(invalid,"lady")));
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator LadyAlwaysPursuesThroughDoorsIgnoresTrapAndTalismanHoldsActualHeadAndAgent()
        {
            yield return LadyPrepare();LadyCollect(0,5);
            yield return Wait(()=>Get<Component>(LadyFinale,"Actor"),3,"Lady model did not spawn");
            var actor=Get<Component>(LadyFinale,"Actor");var agent=actor.GetComponent<NavMeshAgent>();
            foreach(var door in CheckpointItems("Door"))Call(door,"OpenForPursuer");
            ((Behaviour)player).enabled=false;
            var start=actor.transform.position;
            actor.transform.rotation=Quaternion.LookRotation(actor.transform.position-player.transform.position);
            yield return Delay(.7f);
            Assert.That(Vector3.Distance(actor.transform.position,start),Is.GreaterThan(.2f),"Pursuit incorrectly requires seeing the player");
            Assert.That(Get<Vector3>(actor,"PursuitTarget"),Is.EqualTo(player.transform.position));
            Assert.That((bool)Call(RequireType("EnemyTrapRestraint"),"Eligible",agent),Is.False);
            var trap=(Component)Call(RequireType("PlacedMonsterTrap"),"Create",actor.transform.parent,actor.transform.position,Quaternion.identity,false);
            yield return Delay(.1f);Assert.That(Get<bool>(trap,"Armed"),Is.True);Assert.That(Get<bool>(actor,"TrapImmune"),Is.True);
            var motion=Get<Component>(actor,"Motion");var head=Get<Transform>(motion,"Head");
            var ritual=Get<Component>(player,"Rituals");Call(ritual,"Restore",1,0,1);
            Assert.That((bool)Call(ritual,"UseTalisman"),Is.True);
            var at=actor.transform.position;var rotation=head.localRotation;float clock=Get<float>(motion,"Clock");
            yield return Delay(.25f);
            Assert.That(Vector3.Distance(actor.transform.position,at),Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(head.localRotation,rotation),Is.LessThan(.005f));
            Assert.That(Get<float>(motion,"Clock"),Is.EqualTo(clock));
            Call(ritual,"EndEffect");yield return Delay(.1f);Assert.That(Get<float>(motion,"Clock"),Is.GreaterThan(clock));
            Call(shell,"Pause");clock=Get<float>(motion,"Clock");at=actor.transform.position;
            yield return Delay(.15f);Assert.That(Get<float>(motion,"Clock"),Is.EqualTo(clock));
            Assert.That(Vector3.Distance(actor.transform.position,at),Is.LessThan(.0001f));
            Call(shell,"Resume");yield return null;
            var joints=actor.GetComponentsInChildren<Transform>();
            var hand=joints.Single(t=>t.name=="LeftHand");var knee=joints.Single(t=>t.name=="RightLeg");
            Call(motion,"RestoreClock",0f);var initial=head.localRotation;var h0=hand.localRotation;var k0=knee.localRotation;
            float maxHead=0,maxHand=0,maxKnee=0;
            for(int i=1;i<60;i++)
            {Call(motion,"RestoreClock",i/60f);maxHead=Mathf.Max(maxHead,Quaternion.Angle(initial,head.localRotation));
             maxHand=Mathf.Max(maxHand,Quaternion.Angle(h0,hand.localRotation));maxKnee=Mathf.Max(maxKnee,Quaternion.Angle(k0,knee.localRotation));}
            Assert.That(maxHead,Is.GreaterThan(35));Assert.That(maxHand,Is.GreaterThan(30));Assert.That(maxKnee,Is.GreaterThan(15));
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.That(skins.Length,Is.EqualTo(3));
            Assert.That(skins.All(s=>s.bones.Length>=20&&!s.sharedMaterial.IsKeywordEnabled("_EMISSION")),Is.True);
            Assert.That(actor.GetComponentsInChildren<LODGroup>().Length,Is.EqualTo(1),"Imported and runtime LOD groups must not compete");
            var largest=skins.OrderByDescending(s=>s.sharedMesh.vertexCount).First();
            var bake=new Mesh();
            try
            {
                for(int i=0;i<12;i++)
                {
                    Call(motion,"RestoreClock",i*.19f);Call(RequireType("EnemyStudioRig"),"BakeLocalPose",largest,bake);
                    var points=bake.vertices.Select(largest.transform.TransformPoint).ToArray();
                    var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
                    Assert.That(bounds.size.y,Is.InRange(1.2f,3.5f),"Unit conversion broke the actual posed skin");
                    Assert.That(bounds.size.x,Is.LessThan(4));Assert.That(bounds.size.z,Is.LessThan(4));
                }
            }
            finally{Object.Destroy(bake);}
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator LadyOpensPhysicalDoorAndFindsPlayerAfterUnwitnessedCabinetEntry()
        {
            yield return LadyPrepare();LadyCollect(0,5);
            yield return Wait(()=>Get<Component>(LadyFinale,"Actor"),5,"Lady did not spawn");
            var actor=Get<Component>(LadyFinale,"Actor");var agent=actor.GetComponent<NavMeshAgent>();
            ((Behaviour)player).enabled=false;
            var door=CheckpointItems("Door").First(d=>Get<bool>(d,"DoorOperable")&&!Get<bool>(d,"DoorLocked")&&!Get<bool>(d,"IsOpen"));
            var forward=door.transform.forward;var start=door.transform.position-forward*1.55f;start.y=.03f;
            Assert.That(NavMesh.SamplePosition(start,out var anchor,.4f,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(anchor.position),Is.True);actor.transform.rotation=Quaternion.LookRotation(forward);
            var target=door.transform.position+forward*3;target.y=.03f;PlacePlayer(target,false);Physics.SyncTransforms();
            yield return Wait(()=>Get<bool>(door,"IsOpen"),6,"Lady never used the actual closed door");
            yield return Wait(()=>Vector3.Dot(actor.transform.position-door.transform.position,forward)>.7f,6,"Lady did not cross the physical doorway");
            var cabinet=CheckpointCabinetFixture(true);var outside=Get<Transform>(cabinet,"outside").position;
            var direction=outside-Get<Transform>(cabinet,"inside").position;direction.y=0;direction.Normalize();
            PlacePlayer(outside);Set(player,"HidingRandomSample",(System.Func<float>)(()=>.01f));
            // Enter while Lady is elsewhere, then approach through the same
            // unconditional path used in play. No witness flag is supplied.
            Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Assert.That(NavMesh.SamplePosition(outside+direction*2.4f,out anchor,.5f,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(anchor.position),Is.True);
            Call(session,"ToggleCheatSafeMode");Assert.That(Get<bool>(session,"CheatSafeMode"),Is.False);
            yield return Wait(()=>Get<bool>(session,"CabinetDeathActive"),8,"Hiding incorrectly defeated unconditional Lady pursuit");
            Assert.That(Get<string>(session,"DefeatSource"),Is.EqualTo("lady"));
            Call(shell,"Pause");
            yield return Delay(.1f);Assert.That(Get<bool>(session,"Finished"),Is.False);
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(session,"Finished"),4,"Lady's confirmed cabinet contact never completed defeat");
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator LadyFinaleActualMixerKeepsOnlyPlayerLocomotionAndOneImpactWithPause()
        {
            yield return LadyPrepare();Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            LadyCollect(0,5);var scope=Get<Component>(LadyFinale,"AudioScope");
            var impact=Get<AudioSource>(scope,"ImpactSource");
            var feedback=Get<Component>(player,"Feedback");var steps=Get<AudioSource>(feedback,"FootstepSource");var breath=Get<AudioSource>(feedback,"BreathingSource");
            Assert.That(AudioListener.pause&&steps.ignoreListenerPause&&breath.ignoreListenerPause,Is.True);
            var ordinary=player.gameObject.AddComponent<AudioSource>();ordinary.clip=impact.clip;ordinary.spatialBlend=0;ordinary.volume=1;
            ExternalMuteExcept(steps,breath,impact,ordinary);
            var segments=new List<CloudExternalAudioTests.Segment>();var montage=new List<float>();
            int rate=AudioSettings.outputSampleRate;float capture=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                impact.Play();yield return ExternalCapture("lady-one-impact",impact,impact.clip,"frame-impact",.6f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002));impact.Stop();
                ordinary.Play();yield return ExternalFlush(rate,.3f);
                yield return ExternalCapture("lady-world-silenced",ordinary,ordinary.clip,"frame-impact",.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));
                steps.clip=CloudExternalAudioTests.Shared("step-wood");steps.volume=.8f;steps.Play();
                yield return ExternalCapture("lady-player-steps",steps,steps.clip,"player-step",.3f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002));steps.Stop();
                breath.volume=.5f;breath.Play();
                yield return ExternalCapture("lady-player-breath",breath,breath.clip,"player-breath",.4f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002));
                Call(shell,"Pause");yield return null;yield return ExternalFlush(rate,.3f);
                yield return ExternalCapture("lady-pause",breath,breath.clip,"player-breath",.2f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));
                CloudExperienceTests.Artifact("lady-actual-mixer.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CloudExternalAudioTests.Report{scope="Quiet pre-device Unity mixer: final impact, excluded world audio, existing player steps/breath, pause.",segments=segments.ToArray()},true)));
            }
            finally{Object.Destroy(ordinary);AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
        }
    }
}
