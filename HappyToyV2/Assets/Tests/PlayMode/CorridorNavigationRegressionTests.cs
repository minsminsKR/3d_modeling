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
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator UncatWallBlockedPatrolRecoversUsingActualBodyDisplacementInsteadOfNativeVelocity()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var run=Get<Component>(session,"Corridor");
            foreach(var door in CheckpointItems("Door"))Call(door,"OpenForPursuer");yield return Delay(2);
            var walls=UnityEngine.Object.FindObjectsByType<BoxCollider>()
                .Where(w=>w.enabled&&!w.isTrigger&&w.bounds.size.y>2&&w.name.IndexOf("wall",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            foreach(string key in new[]{"Uncat"})
            {
                var actor=ReadabilityActor(key);actor.gameObject.SetActive(true);((Behaviour)actor).enabled=false;yield return null;
                var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=true;
                var query=actor.transform.Find("Rendered body clearance query").GetComponent<BoxCollider>();
                var path=new NavMeshPath();bool found=false;Vector3 blocked=default,safe=default;Quaternion facing=Quaternion.identity;
                for(int cell=1;cell<81&&!found;cell++)
                foreach(var offset in new[]{new Vector3(2.5f,0,2.5f),new Vector3(-2.5f,0,2.5f),new Vector3(2.5f,0,-2.5f),new Vector3(-2.5f,0,-2.5f)})
                {
                    if(!NavMesh.SamplePosition((Vector3)Call(run,"CellPosition",cell)+offset,out var bad,.25f,NavMesh.AllAreas)||Mathf.Abs(bad.position.y-.03f)>.15f)continue;
                    var nearby=walls.Where(w=>w.bounds.SqrDistance(bad.position)<9).ToArray();
                    bool Fits(Vector3 p,Quaternion q)=>!nearby.Any(w=>Physics.ComputePenetration(query,p,q,w,w.transform.position,w.transform.rotation,out _,out float d)&&d>.02f);
                    // Prove that turning, or stopping just inside the public .6m
                    // arrival radius, cannot fit this body into the real corner.
                    bool accessible=false;
                    for(int yaw=0;yaw<16&&!accessible;yaw++)for(int step=0;step<9&&!accessible;step++)
                    {
                        var delta=step==8?Vector3.zero:new Vector3(Mathf.Cos(step*Mathf.PI/4),0,Mathf.Sin(step*Mathf.PI/4))*.6f;
                        accessible=Fits(bad.position+delta,Quaternion.Euler(0,yaw*22.5f,0));
                    }
                    if(accessible)continue;
                    var at=bad.position-offset.normalized*2.2f;
                    var rotation=Quaternion.LookRotation(offset);
                    if(!NavMesh.SamplePosition(at,out var clear,.25f,NavMesh.AllAreas)||Mathf.Abs(clear.position.y-.03f)>.15f||!Fits(clear.position,rotation)||
                        !NavMesh.CalculatePath(clear.position,bad.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    blocked=bad.position;safe=clear.position;facing=rotation;found=true;
                    if(found)break;
                }
                Assert.That(found,Is.True,key+" needs a genuine NavMesh-valid destination blocked by its rendered body at a real wall");
                var goal=new GameObject("CloudQA NavMesh-valid wall patrol marker").transform;
                var exit=new GameObject("CloudQA safe patrol marker").transform;goal.position=blocked;
                Assert.That(NavMesh.SamplePosition(safe+(safe-blocked).normalized,out var exitHit,.3f,NavMesh.AllAreas),Is.True);exit.position=exitHit.position;
                try
                {
                    Set(actor,"patrol",new[]{goal,exit});Set(actor,"state","Patrol");
                    Assert.That(agent.Warp(safe),Is.True);actor.transform.rotation=facing;((Behaviour)actor).enabled=true;
                    var waypoint=actor.GetType().GetField("waypoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);waypoint.SetValue(actor,0);
                    yield return Wait(()=>Get<int>(actor,"PatrolRouteRecoveries")>0,12,key+" kept walking against a real wall",()=>PatrolNavigationDetails(actor));
                    Assert.That(Vector3.Distance(actor.transform.position,blocked),Is.GreaterThan(.6f),"Fixture reached the raw marker instead of exercising physical blockage");
                    yield return Wait(()=>Vector3.Distance(actor.transform.position,exit.position)<.6f,8,key+" did not resume the next real route",()=>PatrolNavigationDetails(actor));
                    Assert.That(Get<int>(actor.GetComponent(RequireType("V1MonsterMotion")),"BodyClearanceCorrections"),Is.GreaterThan(0));
                    Assert.That(agent.isOnNavMesh,Is.True);TestContext.WriteLine(key+" recovered from blocked="+blocked+" to safe="+safe);
                }
                finally{actor.gameObject.SetActive(false);UnityEngine.Object.Destroy(goal.gameObject);UnityEngine.Object.Destroy(exit.gameObject);}
            }
        }

        string PatrolNavigationDetails(Component actor)
        {
            var agent=actor.GetComponent<NavMeshAgent>();
            object Field(object owner,string name)=>owner.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).GetValue(owner);
            var passage=Field(actor,"doorPassage");
            var door=Field(passage,"Door") as Component;
            return "at="+actor.transform.position+" velocity="+agent.velocity+" steering="+agent.steeringTarget+
                " destination="+agent.destination+" remaining="+agent.remainingDistance+" stopped="+agent.isStopped+
                " waypoint="+Field(actor,"waypoint")+" guide="+Get<Vector3>(actor.GetComponent(RequireType("CorridorRouteGuide")),"IntermediateTarget")+
                " corrections="+Get<int>(actor.GetComponent(RequireType("V1MonsterMotion")),"BodyClearanceCorrections")+
                " door="+door+" doorAt="+(door?door.transform.position:Vector3.zero)+" normal="+(door?Get<Vector3>(door,"DoorNormal"):Vector3.zero)+
                " passing="+Field(passage,"Passing")+" nativeCorners="+string.Join(";",agent.path.corners.Select(p=>p.ToString()))+
                " query="+actor.transform.Find("Rendered body clearance query").GetComponent<BoxCollider>().size;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ClosedDoorApproachDoesNotCycleAtTheWallBesideItsOpening()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var actor=ReadabilityActor("Cyclopse");var markers=Get<Transform[]>(actor,"patrol");
            Set(actor,"patrol",new[]{markers[3],markers[3]});actor.gameObject.SetActive(true);((Behaviour)actor).enabled=true;
            var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=true;
            var start=new Vector3(217.63f,.03f,241.83f);
            Assert.That(NavMesh.SamplePosition(start,out var hit,.2f,NavMesh.AllAreas)&&agent.Warp(hit.position),Is.True);
            actor.transform.rotation=Quaternion.LookRotation(Vector3.right);
            float next=0,deadline=Time.realtimeSinceStartup+18;bool escaped=false;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(Time.time>=next){TestContext.WriteLine(PatrolNavigationDetails(actor));next=Time.time+2;}
                if(Vector3.Distance(actor.transform.position,start)>4){escaped=true;break;}
                yield return null;
            }
            Assert.That(escaped,Is.True,"Closed approach did not escape its initial corner: "+PatrolNavigationDetails(actor));
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator LargeCorridorPatrolsClearEveryDirectedCornerWithoutWallWalking()
        {
            yield return RevisionPrepare();
            ((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var run=Get<Component>(session,"Corridor");var bits=Get<int[]>(Get<object>(run,"Layout"),"Connections");
            var marker=new GameObject("CloudQA corner patrol target").transform;
            Vector3 Direction(int d)=>new Vector3(new[]{0,1,0,-1}[d],0,new[]{1,0,-1,0}[d]);
            float oldScale=Time.timeScale;
            try
            {
                // Accelerated simulation only; retain actual agent/body size and speed.
                Time.timeScale=3;
                foreach(string key in new[]{"Uncat","Cyclopse"})
                foreach(int cell in Enumerable.Range(1,80).Where(i=>new[]{3,6,9,12}.Contains(bits[i])))
                foreach(int entry in Enumerable.Range(0,4).Where(d=>(bits[cell]&(1<<d))!=0))
                {
                    int exit=Enumerable.Range(0,4).First(d=>d!=entry&&(bits[cell]&(1<<d))!=0);
                    var centre=(Vector3)Call(run,"CellPosition",cell);
                    var start=centre+Direction(entry)*3.9f;marker.position=centre+Direction(exit)*3.9f;
                    foreach(var door in CheckpointItems("Door"))Call(door,"OpenForPursuer");
                    yield return Delay(.8f);
                    var actor=ReadabilityActor(key);actor.gameObject.SetActive(true);((Behaviour)actor).enabled=true;
                    Set(actor,"patrol",new[]{marker,marker});Set(actor,"state","Patrol");
                    var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=true;
                    Assert.That(NavMesh.SamplePosition(start,out var sample,.7f,NavMesh.AllAreas)&&agent.Warp(sample.position),Is.True);
                    actor.transform.rotation=Quaternion.LookRotation(-Direction(entry));
                    yield return Wait(()=>Vector3.Distance(actor.transform.position,marker.position)<.8f,18,
                        key+" failed directed corner cell="+cell+" entry="+entry+" exit="+exit,
                        ()=>PatrolNavigationDetails(actor));
                    TestContext.WriteLine(key+" cleared cell="+cell+" entry="+entry+" exit="+exit);
                    actor.gameObject.SetActive(false);
                }
            }
            finally{Time.timeScale=oldScale;UnityEngine.Object.Destroy(marker.gameObject);}
        }
    }
}
