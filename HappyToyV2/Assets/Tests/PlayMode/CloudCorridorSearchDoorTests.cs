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
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorSearchAtAnInoperableDoorStillExpiresWithoutCrossing()
        {
            Call(session,"CreateCorridor",73); Begin();
            var door=Components("Interactable").First(x=>x.name=="Corridor sliding door");
            var actors=Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor")).ToArray();
            var brain=actors.First(x=>x.gameObject.activeSelf);
            foreach(var actor in actors) if(actor!=brain) actor.gameObject.SetActive(false);
            ((Behaviour)player).enabled=false; PlacePlayer(new Vector3(200,5,200),false);
            var forward=door.transform.forward; var start=door.transform.position-forward*1.55f; start.y=.03f;
            var agent=brain.GetComponent<NavMeshAgent>();
            Assert.That(NavMesh.SamplePosition(start,out var hit,.4f,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(hit.position),Is.True); brain.transform.rotation=Quaternion.LookRotation(forward);
            var evidence=door.transform.position+forward*2; evidence.y=.03f;
            Assert.That((bool)Call(brain,"HearNoise",evidence,.01f),Is.True);
            ((Behaviour)door).enabled=false; Set(brain,"state","Chase");
            var leaf=Get<Transform>(door,"movingLeaf"); var closedPosition=leaf.localPosition;
            yield return null; yield return null;
            Assert.That(Get<object>(brain,"state").ToString(),Is.EqualTo("Search"));
            float deadline=Time.realtimeSinceStartup+23;
            while(Get<object>(brain,"state").ToString()=="Search" && Time.realtimeSinceStartup<deadline)
            {
                Assert.That(leaf.localPosition,Is.EqualTo(closedPosition),"Disabled leaf moved");
                Assert.That(Vector3.Dot(brain.transform.position-door.transform.position,forward),Is.LessThan(-.18f));
                yield return null;
            }
            Assert.That(Get<object>(brain,"state").ToString(),Is.EqualTo("Patrol"),"An inoperable door held search forever");
            Assert.That(leaf.localPosition,Is.EqualTo(closedPosition));
        }
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorSearchingMonsterCannotPassAClosedPhysicalDoor()
        {
            Call(session,"CreateCorridor",73); Begin();
            var door=Components("Interactable").First(x=>x.name=="Corridor sliding door");
            var actors=Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor")).ToArray();
            var brain=actors.First(x=>x.gameObject.activeSelf);
            foreach(var actor in actors) if(actor!=brain) actor.gameObject.SetActive(false);
            ((Behaviour)player).enabled=false; PlacePlayer(new Vector3(200,5,200),false);
            var forward=door.transform.forward; var start=door.transform.position-forward*1.55f; start.y=.03f;
            var agent=brain.GetComponent<NavMeshAgent>();
            Assert.That(NavMesh.SamplePosition(start,out var hit,.4f,NavMesh.AllAreas),Is.True);
            Assert.That(agent.Warp(hit.position),Is.True); brain.transform.rotation=Quaternion.LookRotation(forward);
            var evidence=door.transform.position+forward*2; evidence.y=.03f;
            // Real reachable sound evidence, then expiry of an established chase.
            // No private search destination injection or current hidden-player tracking.
            Assert.That((bool)Call(brain,"HearNoise",evidence,.01f),Is.True);
            Set(brain,"state","Chase"); yield return null; yield return null;
            Assert.That(Get<object>(brain,"state").ToString(),Is.EqualTo("Search"));
            Debug.Log("SEARCH_DOOR start="+brain.transform.position+" evidence="+Get<Vector3>(brain,"SearchOrigin")+" door="+door.transform.position+" speed="+Get<float>(brain,"patrolSpeed"));
            float deadline=Time.realtimeSinceStartup+8; bool sawClosedPause=false;
            while(Vector3.Dot(brain.transform.position-door.transform.position,forward)<.6f && Time.realtimeSinceStartup<deadline)
            {
                if(!Get<bool>(door,"IsOpen"))
                {
                    Assert.That(Vector3.Dot(brain.transform.position-door.transform.position,forward),Is.LessThan(-.18f),
                        "Search crossed a real closed leaf without opening it");
                    if(!sawClosedPause && Vector3.Distance(brain.transform.position,door.transform.position)<1.4f)
                    {
                        sawClosedPause=true; Call(shell,"Pause"); var position=brain.transform.position;
                        yield return Delay(.3f); Assert.That(brain.transform.position,Is.EqualTo(position));
                        Assert.That(Get<bool>(door,"IsOpen"),Is.False); Call(shell,"Resume");
                    }
                }
                yield return null;
            }
            Assert.That(sawClosedPause,Is.True,"No approach: position="+brain.transform.position+" state="+Get<object>(brain,"state")+" path="+agent.pathStatus+" steering="+agent.steeringTarget);
            Assert.That(Get<bool>(door,"IsOpen"),Is.True,"Closed door remained shut: "+brain.transform.position);
            Assert.That(Vector3.Dot(brain.transform.position-door.transform.position,forward),Is.GreaterThan(.6f),"Search never passed the opened door");
            Assert.That(Vector3.Distance(Get<Vector3>(brain,"SearchOrigin"),evidence),Is.LessThan(.2f));
        }
    }
}

