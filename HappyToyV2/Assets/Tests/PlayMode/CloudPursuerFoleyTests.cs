using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator PursuerRecordedShoeTakesCycleOnWoodAndFollowActualTileAndWaterFloors()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            ((Behaviour)player).enabled = false;
            var actor = Get<Component>(Get<Component>(session, "Chapter"), "Cyclopse");
            ((Behaviour)actor).enabled = false;
            foreach (var startup in actor.GetComponents(RequireType("NavMeshStartup")))
            { ((MonoBehaviour)startup).StopAllCoroutines(); ((Behaviour)startup).enabled = false; }
            var agent = actor.GetComponent<NavMeshAgent>();
            agent.enabled = false; actor.gameObject.SetActive(true);
            var steps = actor.GetComponent(RequireType("StalkerFootsteps"));
            // The old tile target (-4.5,-6) was inside the authored toilet
            // cubicle. SetDestination projected it onto the front edge at
            // z=-4.14, leaving less than Cyclopse's .95m stride. Use the actual
            // open ceramic aisle, as in the keyboard floor-contact fixture.
            var starts = new[] {new Vector3(-7.3f,0,0),new Vector3(-6.25f,0,-3.2f),new Vector3(13.8f,-5,-25)};
            var ends = new[] {new Vector3(2,0,0),new Vector3(-3.65f,0,-3.2f),new Vector3(13.8f,-5,-28.4f)};
            var surfaces = new[] {"wood","stone","wet"};
            for (int surface = 0; surface < 3; surface++)
            {
                agent.enabled = false;
                Assert.That(NavMesh.SamplePosition(starts[surface],out var begin,1,NavMesh.AllAreas),Is.True);
                Assert.That(Mathf.Abs(begin.position.y-starts[surface].y),Is.LessThan(.2f));
                actor.transform.position = begin.position; agent.enabled = true;
                Assert.That(agent.Warp(begin.position),Is.True);
                Assert.That(NavMesh.SamplePosition(ends[surface],out var destination,.15f,agent.areaMask),Is.True,
                    "Floor fixture target is outside real navigation: "+surfaces[surface]);
                Assert.That(Vector3.Distance(destination.position,ends[surface]),Is.LessThan(.15f),
                    "Floor fixture silently projected its target onto a collider boundary: "+surfaces[surface]);
                var route=new NavMeshPath();
                Assert.That(agent.CalculatePath(destination.position,route),Is.True);
                Assert.That(route.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(route.corners.Length,Is.GreaterThanOrEqualTo(2));
                Assert.That(Vector3.Distance(route.corners[route.corners.Length-1],destination.position),Is.LessThan(.15f),
                    "Complete NavMesh path does not end at the requested floor waypoint");
                // The authored agent is serialized at rest; its enabled brain
                // normally applies patrolSpeed. This controlled floor fixture
                // uses that same production speed while disabling only decisions.
                agent.speed = Get<float>(actor,"patrolSpeed");
                // This endpoint is a floor-sampling waypoint, not an attack
                // target. Walk the short tile/water strip fully; the authored
                // melee stopping distance otherwise stops before one stride.
                agent.stoppingDistance = .05f;
                Assert.That(agent.speed,Is.GreaterThan(0)); agent.isStopped = false;
                yield return null; yield return null;
                int initial = Get<int>(steps,"StepsPlayed"), last = initial;
                var fingerprints = new HashSet<string>();
                Assert.That(agent.SetDestination(destination.position),Is.True);
                yield return null;
                yield return Wait(()=>agent.hasPath&&!agent.pathPending,3,"Controlled pursuer failed to acquire its real floor path");
                float until = Time.realtimeSinceStartup + 12;
                // remainingDistance measures the baked path endpoint, which
                // may differ from the requested coordinate. End this physical
                // contact probe only after reaching the validated waypoint.
                while (Time.realtimeSinceStartup < until && (agent.pathPending || Vector3.Distance(actor.transform.position,destination.position)>.13f))
                {
                    yield return null;
                    int played = Get<int>(steps,"StepsPlayed");
                    if (played == last) continue;
                    last = played;
                    Assert.That(Get<string>(steps,"LastSurface"),Is.EqualTo(surfaces[surface]));
                    var clip = Get<AudioClip>(steps,"MovementClip");
                    string cue = surface==0 ? "enemy-cyclopse-movement" : "step-"+surfaces[surface];
                    Assert.That(CloudExternalAudioTests.MatchesFamily(clip,cue,surface==1?5:3),Is.True);
                    fingerprints.Add(CloudExternalAudioTests.Fingerprint(clip));
                }
                Assert.That(Vector3.Distance(actor.transform.position,destination.position),Is.LessThanOrEqualTo(.15f),
                    "Pursuer did not reach real "+surfaces[surface]+" target="+destination.position+
                    " position="+actor.transform.position+" remaining="+agent.remainingDistance+" path="+agent.pathStatus);
                Assert.That(Get<int>(steps,"StepsPlayed")-initial,Is.GreaterThanOrEqualTo(2),
                    "Actual floor "+surfaces[surface]+" position="+actor.transform.position+" speed="+agent.speed+" path="+agent.pathStatus+" moving="+agent.velocity);
                Assert.That(fingerprints.Count,Is.GreaterThanOrEqualTo(surface==0?3:2),"Moving pursuer repeats the same recorded sole contact");
                agent.ResetPath(); agent.isStopped = true;
                int stationary = Get<int>(steps,"StepsPlayed"); yield return Delay(.22f);
                Assert.That(Get<int>(steps,"StepsPlayed"),Is.EqualTo(stationary));
            }
            Debug.Log("HAPPYTOY_PURSUER_FOLEY_PASS controlled real NavMesh displacement, three recorded shoe takes, real authored ceramic/water contact, no stationary phantom steps; full native pursuit is separate");
        }
    }
}
