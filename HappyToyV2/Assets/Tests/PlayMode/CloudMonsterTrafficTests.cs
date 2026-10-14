using System.Collections;
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
        [UnityTest,Timeout(120000)]
        public IEnumerator CorridorTemporarilyOccupiedDoorMakesOnlyBlockedActorYieldAndResume()
        {
            yield return RevisionPrepare();IsolateThreats();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"))Call(door,"OpenForPursuer");
            yield return Delay(1.5f);
            var doorway=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && x.name=="Corridor sliding door");
            var forward=doorway.transform.forward;var centre=doorway.transform.position;
            var actors=new[]{ReadabilityActor("Cyclopse"),ReadabilityActor("Uncat")};
            var agents=actors.Select(x=>x.GetComponent<NavMeshAgent>()).ToArray();
            var traffic=actors.Select(x=>x.GetComponent(RequireType("EnemyPassageTraffic"))).ToArray();
            var starts=new[]{centre,centre-forward*4};var goals=new[]{centre-forward*4,centre+forward*4};
            for(int i=0;i<2;i++)
            {
                actors[i].gameObject.SetActive(true);((Behaviour)actors[i]).enabled=false;agents[i].enabled=true;
                Call(traffic[i],"Configure",agents[i]);agents[i].updateRotation=true;
                Assert.That(NavMesh.SamplePosition(starts[i],out var at,.4f,NavMesh.AllAreas)&&agents[i].Warp(at.position),Is.True);
                actors[i].transform.rotation=Quaternion.LookRotation(i==0?-forward:forward);
                var path=new NavMeshPath();Assert.That(agents[i].CalculatePath(goals[i],path),Is.True);agents[i].SetPath(path);agents[i].isStopped=false;
                Call(traffic[i],"ObserveRoute",goals[i]);
            }
            // A temporary large avoidance footprint deliberately occupies this
            // real doorway. No game asset, wall or bake is replaced by the fixture.
            float normalStopping=agents[1].stoppingDistance,normalRadius=agents[0].radius;
            agents[0].radius=1.2f;agents[0].isStopped=true;
            // Hold requested forward displacement briefly, modelling the
            // reported zero-progress crowd lock while a native route remains live.
            float blockUntil=Time.realtimeSinceStartup+1.3f;
            while(!Get<bool>(traffic[1],"Yielding") && Time.realtimeSinceStartup<blockUntil)
            {agents[1].velocity=Vector3.zero;yield return null;}
            Assert.That(Get<bool>(traffic[1],"Yielding"),Is.True,"Zero-progress actor never chose a siding; position="+actors[1].transform.position+
                " hasPath="+agents[1].hasPath+" stopped="+agents[1].isStopped+" intent="+Get<Vector3>(traffic[1],"IntendedPoint"));
            Assert.That(Get<int>(traffic[0],"YieldsStarted"),Is.Zero,"Both actors yielded at once");
            // Release the occupied lane; the manoeuvre must restore the true
            // endpoint itself, without an AI refresh or fixture reissuing it.
            agents[0].radius=normalRadius;agents[0].isStopped=false;
            yield return Wait(()=>Vector3.Distance(actors[1].transform.position,goals[1])<1,16,"Yielded actor never returned to its original endpoint");
            Assert.That(Get<int>(traffic[1],"RoutesResumed"),Is.GreaterThan(0),"No natural route recovery occurred");
            Assert.That(agents[1].stoppingDistance,Is.EqualTo(normalStopping).Within(.001f));
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator CorridorLargeMonstersPassHeadOnThroughAnActualOpenDoor()
        {
            yield return RevisionPrepare();IsolateThreats();
            ((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"))Call(door,"OpenForPursuer");
            yield return Delay(1.5f);
            var doorway=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && x.name=="Corridor sliding door");
            Vector3 forward=doorway.transform.forward;var centre=doorway.transform.position;
            var starts=new[]{centre-forward*3.8f,centre+forward*3.8f};
            var actors=new[]{ReadabilityActor("Cyclopse"),ReadabilityActor("Uncat")};
            var agents=actors.Select(x=>x.GetComponent<NavMeshAgent>()).ToArray();
            for(int i=0;i<2;i++)
            {
                actors[i].gameObject.SetActive(true);((Behaviour)actors[i]).enabled=true;agents[i].enabled=true;
                Assert.That(NavMesh.SamplePosition(starts[i],out var at,.4f,NavMesh.AllAreas)&&agents[i].Warp(at.position),Is.True);
                actors[i].transform.rotation=Quaternion.LookRotation(i==0?forward:-forward);
                Set(actors[i],"state","Patrol");Set(actors[i],"patrolSpeed",1.45f);
            }
            yield return null;
            for(int i=0;i<2;i++)Assert.That((bool)Call(actors[i],"HearNoise",starts[1-i],20f),Is.True);
            float deadline=Time.realtimeSinceStartup+20,minimumSeparation=100;
            while(Time.realtimeSinceStartup<deadline && actors.Where((actor,i)=>Vector3.Distance(actor.transform.position,starts[1-i])>1).Any())
            {
                minimumSeparation=Mathf.Min(minimumSeparation,Vector3.Distance(actors[0].transform.position,actors[1].transform.position));
                Assert.That(actors.All(x=>Mathf.Abs(x.transform.position.y-.03f)<.25f),Is.True,"Passing moved an actor off the corridor floor");
                yield return null;
            }
            for(int i=0;i<2;i++)Assert.That(Vector3.Distance(actors[i].transform.position,starts[1-i]),Is.LessThan(1),
                actors[i].name+" stayed wedged: position="+actors[i].transform.position+" target="+starts[1-i]+" native velocity="+agents[i].velocity+
                " guide="+Get<Vector3>(actors[i].GetComponent(RequireType("CorridorRouteGuide")),"IntermediateTarget"));
            Assert.That(minimumSeparation,Is.GreaterThan((agents[0].radius+agents[1].radius)*.75f),"Passing overlapped agent capsules");
        }
    }
}
