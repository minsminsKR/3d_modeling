using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    // Observe the genuine travel candidate after Update, before the rendered
    // contact observer consumes it in LateUpdate. No contact flag is injected.
    [DefaultExecutionOrder(-10000)]
    public sealed class CloudUncatCandidateProbe : MonoBehaviour
    {
        public Component steps;
        public Action cancel;
        public bool observed;
        public int count;
        void LateUpdate()
        {
            if(cancel==null || !Get<bool>(steps,"PendingContact")) return;
            count=Get<int>(steps,"StepsPlayed");observed=true;
            var action=cancel;cancel=null;action();
        }
    }
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(90000)]
        public IEnumerator UncatMovementCuesWaitForActualWeightedLimbPlantAndDiscardPausedStoppedWarpedDisabledDebt()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;
            PlacePlayer(new Vector3(200,5,200),false);
            foreach(var door in Components("Interactable").Where(item=>Get<object>(item,"kind").ToString()=="Door"))Call(door,"OpenForPursuer");
            yield return Delay(1.5f);
            var actor=ReadabilityActor("Uncat");actor.gameObject.SetActive(true);((Behaviour)actor).enabled=false;
            var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=true;
            var motion=actor.GetComponent(RequireType("V1MonsterMotion"));
            var steps=actor.GetComponent(RequireType("StalkerFootsteps"));
            var run=Get<Component>(session,"Corridor");var start=(Vector3)Call(run,"CellPosition",0);
            int bits=Get<int[]>(Get<object>(run,"Layout"),"Connections")[0];int direction=Enumerable.Range(0,4).First(d=>(bits&(1<<d))!=0);
            var forward=new Vector3(new[]{0,1,0,-1}[direction],0,new[]{1,0,-1,0}[direction]);var end=start+forward*6;
            var probe=actor.gameObject.AddComponent<CloudUncatCandidateProbe>();probe.steps=steps;
            try
            {
                Assert.That(agent.Warp(start),Is.True);actor.transform.rotation=Quaternion.LookRotation(forward);
                Set(actor,"state","Chase");agent.speed=Get<float>(actor,"chaseSpeed");agent.stoppingDistance=.05f;
                agent.isStopped=false;Assert.That(agent.SetDestination(end),Is.True);
                int before=Get<int>(steps,"StepsPlayed");
                yield return Wait(()=>Get<int>(steps,"StepsPlayed")>=before+3,5,"Actual quadruped movement never produced three contacts");
                Assert.That(Get<float>(motion,"LimbContactGap"),Is.LessThanOrEqualTo(.10f));
                for(int kind=0;kind<5;kind++)
                {
                    agent.isStopped=true;agent.ResetPath();Assert.That(agent.Warp(start),Is.True);yield return null;yield return null;
                    actor.transform.rotation=Quaternion.LookRotation(forward);((Behaviour)motion).enabled=true;((Behaviour)steps).enabled=true;
                    int cancellation=kind;probe.observed=false;
                    probe.cancel=()=>
                    {
                        if(cancellation==0)Call(shell,"Pause");
                        else if(cancellation==1){agent.isStopped=true;agent.ResetPath();}
                        else if(cancellation==2){Assert.That(agent.Warp(end),Is.True);agent.isStopped=true;agent.ResetPath();}
                        else if(cancellation==3)((Behaviour)steps).enabled=false;
                        else ((Behaviour)motion).enabled=false;
                    };
                    agent.isStopped=false;Assert.That(agent.SetDestination(end),Is.True);
                    yield return Wait(()=>probe.observed,5,"Real travel never queued a candidate before the contact observer");
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(probe.count),"Cancellation emitted a stale contact: "+kind);
                    Assert.That(Get<bool>(steps,"PendingContact"),Is.False);
                    agent.isStopped=true;agent.ResetPath();if(kind==0)Call(shell,"Resume");
                    ((Behaviour)steps).enabled=true;((Behaviour)motion).enabled=true;yield return null;yield return null;
                    Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(probe.count));
                }
            }
            finally{probe.cancel=null;UnityEngine.Object.Destroy(probe);}
        }
    }
}
