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
        public IEnumerator ChapterCyclopseGlancesThenPassesWithPauseAndSafeControlReturn()
        {
            Call(shell,"BeginChapter");yield return null;
            var chapter=Get<Component>(session,"Chapter");var shots=Get<Component>(chapter,"FirstAppearances");
            var actor=Get<Component>(chapter,"Cyclopse");var navigation=actor.GetComponent<NavMeshAgent>();
            var camera=Get<Camera>(player,"eyes");float fov=camera.fieldOfView;
            Vector3 playerAt=player.transform.position;var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);
            var intro=Get<Component>(shots,"CyclopseIntro");
            yield return Wait(()=>Get<string>(intro,"Phase")=="roar",10,"School Cyclopse never reached its glance");
            Assert.That(Get<bool>(intro,"CrossCorridorOnly"),Is.True);
            Assert.That(((Behaviour)actor).enabled,Is.False);
            Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);
            Assert.That(Vector3.Distance(actor.transform.position,playerAt),Is.GreaterThan(5));
            Assert.That(camera.fieldOfView,Is.LessThan(fov-15));
            Call(shell,"Pause");var pausedActor=actor.transform.position;
            float pausedClock=Get<float>(shots,"ShotElapsed");yield return Delay(.18f);
            Assert.That(actor.transform.position,Is.EqualTo(pausedActor));
            Assert.That(Get<float>(shots,"ShotElapsed"),Is.EqualTo(pausedClock));
            Call(shell,"Resume");
            yield return Wait(()=>Get<string>(intro,"Phase")=="pass",4,"Cyclopse did not turn sideways and pass");
            Assert.That(Get<bool>(intro,"LookAtPlayerCompleted"),Is.True);
            Assert.That(Get<bool>(intro,"SideTurnCompleted"),Is.True);
            Assert.That(Mathf.Abs(actor.transform.forward.z),Is.GreaterThan(.98f));
            Assert.That(camera.fieldOfView,Is.LessThan(fov-15));
            while(Get<bool>(shots,"CameraOwned"))
            {
                Assert.That(actor.transform.position.x,Is.EqualTo(13.8f).Within(.22f));
                Assert.That(((Behaviour)actor).enabled,Is.False);
                Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);
                yield return null;
            }
            Assert.That(Get<bool>(intro,"Completed"),Is.True);
            Assert.That(Get<bool>(intro,"PassCompleted"),Is.True);
            Assert.That(Get<int>(intro,"RouteLegsCompleted"),Is.EqualTo(2));
            Assert.That(Vector3.Distance(playerAt,player.transform.position),Is.LessThan(.05f));
            Assert.That(camera.fieldOfView,Is.EqualTo(fov).Within(.01f));
            Assert.That(Get<bool>(shots,"CyclopseGraceActive"),Is.True);
            Assert.That(((Behaviour)actor).enabled,Is.False);
            Assert.That(Get<bool>(player,"Paused"),Is.False);
            Assert.That(navigation.isStopped,Is.True);
            Call(shell,"Pause");yield return Delay(.2f);
            Assert.That(Get<bool>(shots,"CyclopseGraceActive"),Is.True);
            Assert.That(((Behaviour)actor).enabled,Is.False);Call(shell,"Resume");
            yield return Wait(()=>!Get<bool>(shots,"CyclopseGraceActive"),6,"Protected return never released normal patrol");
            Assert.That(((Behaviour)actor).enabled,Is.True);
            // A later mannequin camera shot must restore the already released actor.
            Call(memories[1],"Use",player);yield return ChapterAwaitAppearance();
            Assert.That(((Behaviour)actor).enabled,Is.True,"Second shot permanently disabled Cyclopse");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EnlargedMonstersKeepNavigationScaleAndGroundedAuthoredPose()
        {
            Call(session,"CreateCorridor",73);
            var actors=Components("StalkerBrain").Where(item=>item.name.EndsWith("— corridor")).ToArray();
            foreach(var actor in actors)((Behaviour)actor).enabled=false;
            Begin();yield return null;
            var scratch=new Mesh();
            try
            {
                foreach(var actor in actors)
                {
                    var nav=actor.GetComponent<NavMeshAgent>();float navHeight=nav.height,navRadius=nav.radius;
                    nav.enabled=false;actor.gameObject.SetActive(true);nav.enabled=true;
                    Assert.That(nav.Warp(actor.transform.position),Is.True);nav.isStopped=true;
                    var motion=actor.GetComponent(RequireType("V1MonsterMotion"));
                    var model=Get<Transform>(motion,"model");var animation=Get<Animation>(motion,"animationPlayer");
                    animation.Play("patrol");animation["patrol"].time=0;animation["patrol"].speed=0;animation.Sample();
                    yield return null;
                    // Probe actual baked vertices independently of the sizing helper.
                    var vertices=model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(skin=>
                    {
                        skin.BakeMesh(scratch,true);
                        return scratch.vertices.Select(point=>skin.transform.TransformPoint(point)).ToArray();
                    }).ToArray();
                    float height=vertices.Max(point=>point.y)-vertices.Min(point=>point.y);
                    float target=actor.name.Contains("Cyclopse")?2.38f:actor.name.Contains("Uncat")?2.20f:
                        actor.name.Contains("Hwacat")?2.22f:1.48f;
                    Assert.That(height,Is.GreaterThanOrEqualTo(target-.025f),actor.name+" remains undersized");
                    Assert.That(height,Is.LessThan(2.44f),actor.name+" no longer clears corridor lintels");
                    Assert.That(Mathf.Abs(Get<float>(motion,"GroundGap")),Is.LessThan(.04f));
                    Assert.That(actor.transform.lossyScale,Is.EqualTo(Vector3.one));
                    Assert.That(nav.height,Is.EqualTo(navHeight));Assert.That(nav.radius,Is.EqualTo(navRadius));
                    Assert.That(model.GetComponentsInChildren<Collider>(true),Is.Empty);
                    TestContext.Out.WriteLine("HAPPYTOY_MONSTER_PRESENCE "+actor.name+" renderedPatrolHeight="+height+
                        "; navHeight="+nav.height+"; rootScale="+actor.transform.lossyScale);
                    actor.gameObject.SetActive(false);
                }
            }
            finally {Object.Destroy(scratch);}
        }
    }
}
