using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class UncatContactRegressionEvidence
        {public string scope="Natural real NavMesh movement; cue-frame independently baked imported-weight foot/hand skin; real pause/stop/warp/disable cleanup";public RefinedFoleyFrame[] cues;public int airborneSamples,queuedChecks;}
        [UnityTest,Timeout(120000)]
        public IEnumerator UncatMovementCuesWaitForActualWeightedLimbPlantAndDiscardPausedStoppedWarpedDisabledDebt()
        {
            IsolateThreats();Call(shell,"BeginChapter");yield return null;yield return null;
            ((Behaviour)player).enabled=false;
            var motion=Components("V1MonsterMotion").Single(item=>item.name.Contains("Uncat"));
            var actor=motion.GetComponent(RequireType("StalkerBrain"));((Behaviour)actor).enabled=false;
            foreach(var startup in actor.GetComponents(RequireType("NavMeshStartup")))
            {((MonoBehaviour)startup).StopAllCoroutines();((Behaviour)startup).enabled=false;}
            var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=false;actor.gameObject.SetActive(true);((Behaviour)motion).enabled=true;
            var steps=actor.GetComponent(RequireType("StalkerFootsteps"));
            var animation=Get<Animation>(motion,"animationPlayer");var model=Get<Transform>(motion,"model");
            var bones=model.GetComponentsInChildren<Transform>();
            var probe=actor.gameObject.AddComponent<CloudRefinedEnemyFoleyProbe>();
            probe.motion=motion;probe.steps=steps;probe.animation=animation;probe.agent=agent;probe.clip="patrol";
            probe.skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            probe.leftToe=bones.Single(bone=>bone.name=="mixamorig:LeftToeBase");probe.rightToe=bones.Single(bone=>bone.name=="mixamorig:RightToeBase");
            probe.leftHand=bones.Single(bone=>bone.name=="mixamorig:LeftHand");probe.rightHand=bones.Single(bone=>bone.name=="mixamorig:RightHand");
            // The probe separately reads the imported weight stream and re-bakes
            // actual skin after production grounding; it never trusts the gate flag.
            probe.CacheContactVertices();
            RefinedFoleyFrame latest=null;var cues=new List<RefinedFoleyFrame>();
            int prior=Get<int>(steps,"StepsPlayed"),airborne=0,queuedChecks=0;
            float oldCapture=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=0;Assert.That(Time.timeScale,Is.EqualTo(1));
                probe.receive=frame=>
                {
                    latest=frame;
                    if(UncatIndependentLimbGap(frame)>.15f)airborne++;
                    if(frame.steps>prior)cues.Add(frame);
                    prior=frame.steps;
                };
                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);
                queuedChecks++;int beforeLanding=Get<int>(steps,"StepsPlayed");
                yield return Wait(()=>Get<int>(steps,"StepsPlayed")>beforeLanding,3,"Actual Uncat landing never consumed its real-travel candidate");
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(beforeLanding+1));
                yield return Delay(.1f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(beforeLanding+1),"Landing emitted excess airborne distance debt");

                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);queuedChecks++;
                int pauseCount=Get<int>(steps,"StepsPlayed");Call(shell,"Pause");yield return Delay(.15f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(pauseCount));Assert.That(Get<bool>(steps,"PendingContact"),Is.False);
                Call(shell,"Resume");agent.isStopped=true;yield return Delay(.15f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(pauseCount),"Resume played a stale paused contact while stationary");

                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);queuedChecks++;
                int stoppedCount=Get<int>(steps,"StepsPlayed");agent.isStopped=true;agent.ResetPath();yield return Delay(.2f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(stoppedCount));Assert.That(Get<bool>(steps,"PendingContact"),Is.False);
                Assert.That(Get<float>(steps,"TravelRemainder"),Is.Zero);
                // Resume at the same actual position, with no warp/clip restart.
                // A small fresh displacement cannot recreate old full-stride debt.
                var resumedAt=actor.transform.position;
                Assert.That(NavMesh.SamplePosition(new Vector3(2,0,0),out var resumeTarget,.2f,agent.areaMask),Is.True);
                agent.isStopped=false;Assert.That(agent.SetDestination(resumeTarget.position),Is.True);
                yield return Wait(()=>Vector3.Distance(actor.transform.position,resumedAt)>.08f,2,"Stopped Uncat never resumed physical navigation");
                Assert.That(Vector3.Distance(actor.transform.position,resumedAt),Is.LessThan(.4f));
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(stoppedCount));
                Assert.That(Get<bool>(steps,"PendingContact"),Is.False,"Tiny resumed travel requeued discarded airborne stride debt");
                Assert.That(Get<float>(steps,"TravelRemainder"),Is.LessThan(Get<float>(steps,"StepDistance")));

                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);queuedChecks++;
                int warpCount=Get<int>(steps,"StepsPlayed"),suppressed=Get<int>(steps,"TeleportsSuppressed");
                Assert.That(NavMesh.SamplePosition(new Vector3(2,0,0),out var warp,.2f,agent.areaMask),Is.True);
                Assert.That(Vector3.Distance(actor.transform.position,warp.position),Is.GreaterThan(1));
                Assert.That(agent.Warp(warp.position),Is.True);yield return null;yield return null;
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(warpCount));Assert.That(Get<bool>(steps,"PendingContact"),Is.False);
                Assert.That(Get<int>(steps,"TeleportsSuppressed"),Is.GreaterThan(suppressed));

                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);queuedChecks++;
                int disabledCount=Get<int>(steps,"StepsPlayed");((Behaviour)steps).enabled=false;
                Assert.That(Get<bool>(steps,"PendingContact"),Is.False);yield return Delay(.15f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(disabledCount));
                agent.isStopped=true;((Behaviour)steps).enabled=true;yield return Delay(.15f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(disabledCount),"Re-enable played a stale disabled contact");

                yield return QueueActualUncat(actor,steps,animation,agent,()=>latest);queuedChecks++;
                int visualDisabledCount=Get<int>(steps,"StepsPlayed");((Behaviour)motion).enabled=false;
                Assert.That(Get<bool>(steps,"PendingContact"),Is.False);Assert.That(Get<float>(steps,"TravelRemainder"),Is.Zero);
                yield return Delay(.15f);Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(visualDisabledCount));
                agent.isStopped=true;((Behaviour)motion).enabled=true;yield return Delay(.15f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(visualDisabledCount),"Disabled visual contact observer left stale cue debt");

                Assert.That(airborne,Is.GreaterThan(10),"Fixture never exercised original airborne limb poses");
                Assert.That(cues.Count,Is.GreaterThanOrEqualTo(3));
                foreach(var cue in cues)
                {
                    Assert.That(cue.speed,Is.GreaterThan(.06f));
                    Assert.That(UncatIndependentLimbGap(cue),Is.LessThanOrEqualTo(.04501f),"Movement cue played above actual weighted foot/hand skin at phase "+cue.phase);
                }
                Debug.Log("HAPPYTOY_UNCAT_CONTACT_PASS actual weighted skin cue frame, natural NavMesh, one queued candidate/no landing burst, pause/stop/warp/disable debt cleanup");
            }
            finally
            {
                probe.receive=null;Time.captureDeltaTime=oldCapture;
                CloudExperienceTests.Artifact("uncat-limb-contact-regression.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new UncatContactRegressionEvidence {cues=cues.ToArray(),airborneSamples=airborne,queuedChecks=queuedChecks},true)));
                UnityEngine.Object.Destroy(probe);
            }
        }
        static float UncatIndependentLimbGap(RefinedFoleyFrame frame)
        {return Mathf.Min(frame.weightedLeftFootY,frame.weightedRightFootY,frame.weightedLeftHandY,frame.weightedRightHandY)-frame.floorY;}
        IEnumerator QueueActualUncat(Component actor,Component steps,Animation animation,NavMeshAgent agent,Func<RefinedFoleyFrame> latest)
        {
            agent.enabled=false;
            Assert.That(NavMesh.SamplePosition(new Vector3(-7.3f,0,0),out var start,1,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(2,0,0),out var end,.2f,NavMesh.AllAreas),Is.True);
            actor.transform.position=start.position;agent.enabled=true;Assert.That(agent.Warp(start.position),Is.True);
            agent.isStopped=true;yield return null;yield return null;
            Set(actor,"state","Patrol");animation.Play("patrol");animation["patrol"].time=0;animation.Sample();
            agent.speed=Get<float>(actor,"patrolSpeed");agent.stoppingDistance=.05f;agent.isStopped=false;
            Assert.That(agent.SetDestination(end.position),Is.True);
            yield return Wait(()=>Get<bool>(steps,"PendingContact")&&latest()!=null&&UncatIndependentLimbGap(latest())>.15f,
                5,"Real navigation never produced a deferred airborne Uncat contact");
        }
    }
}
