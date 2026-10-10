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
        IEnumerator CandleSchoolFirstSightBeforeRelease(string kind)
        {
            Call(shell,"BeginChapter");yield return null;yield return null;
            IntroSetup(kind);
            var chapter=Get<Component>(session,"Chapter");
            // Restore already-seen camera shots, then drive the actual memory
            // collection and source actor's first-sight trigger in this fixture.
            Call(Get<Component>(chapter,"FirstAppearances"),"RestoreProgress",2);
            var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);Call(memories[1],"Use",player);
            bool mask=kind=="LanternMaskEncounter";
            if(mask)Call(memories[2],"Use",player);
            var other=Get<Component>(chapter,mask?"Mannequin":"Mask");other.gameObject.SetActive(false);
            var actor=Get<Component>(chapter,mask?"Mask":"Mannequin");
            IntroPlace(actor.transform.position,actor.transform.position+Vector3.up*1.4f,1.7f);
            CandleDangerArmAll();
            yield return Wait(()=>Get<bool>(actor,mask?"IntroStarted":"Triggered"),2,
                "Actual chapter first sight did not start its harmless introduction");
            Assert.That(Get<bool>(actor,mask?"IntroCompleted":"Released"),Is.False);
            Assert.That(CandleDangerPlayerSees(actor),Is.True);
            Call(LightRun,"RefreshDanger");
            Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThanOrEqualTo(.45f));
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False,"Harmless visible introduction caused contact blackout");
            Assert.That(CandleDangerMarks().All(mark=>Get<bool>(mark,"Lit")),Is.True);
            Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);
        }
        [UnityTest, Timeout(40000)]
        public IEnumerator CandleSchoolMaskStartsWarningDuringHarmlessFirstSight()
        {yield return CandleSchoolFirstSightBeforeRelease("LanternMaskEncounter");}
        [UnityTest, Timeout(40000)]
        public IEnumerator CandleSchoolMannequinStartsWarningDuringHarmlessFirstTurn()
        {yield return CandleSchoolFirstSightBeforeRelease("WeepingAngelEncounter");}

        [UnityTest, Timeout(60000)]
        public IEnumerator ChapterCyclopseGlancesThenPassesWithPauseAndSafeControlReturn()
        {
            Call(shell,"BeginChapter");yield return null;
            var chapter=Get<Component>(session,"Chapter");var shots=Get<Component>(chapter,"FirstAppearances");
            var actor=Get<Component>(chapter,"Cyclopse");var navigation=actor.GetComponent<NavMeshAgent>();
            var camera=Get<Camera>(player,"eyes");float fov=camera.fieldOfView;
            float expectedZoomFov=Mathf.Min(fov,Get<bool>(shell,"ReducedMotion")?48:24);
            Vector3 playerAt=player.transform.position;var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);
            var intro=Get<Component>(shots,"CyclopseIntro");
            yield return Wait(()=>Get<string>(intro,"Phase")=="roar",10,"School Cyclopse never reached its glance");
            Assert.That(Get<bool>(intro,"CrossCorridorOnly"),Is.True);
            Assert.That(((Behaviour)actor).enabled,Is.False);
            Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);
            Assert.That(Vector3.Distance(actor.transform.position,playerAt),Is.GreaterThan(5));
            Assert.That(camera.fieldOfView,Is.EqualTo(expectedZoomFov).Within(.2f));
            Assert.That(CandleDangerPlayerSees(actor),Is.True);
            Call(LightRun,"RefreshDanger");
            Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThanOrEqualTo(.45f),"First visible Cyclopse did not start the candle warning");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False);
            Call(shell,"Pause");var pausedActor=actor.transform.position;
            float pausedClock=Get<float>(shots,"ShotElapsed");yield return Delay(.18f);
            Assert.That(actor.transform.position,Is.EqualTo(pausedActor));
            Assert.That(Get<float>(shots,"ShotElapsed"),Is.EqualTo(pausedClock));
            Call(shell,"Resume");
            yield return Wait(()=>Get<string>(intro,"Phase")=="pass",4,"Cyclopse did not turn sideways and pass");
            Assert.That(Get<bool>(intro,"LookAtPlayerCompleted"),Is.True);
            Assert.That(Get<bool>(intro,"SideTurnCompleted"),Is.True);
            Assert.That(Mathf.Abs(actor.transform.forward.z),Is.GreaterThan(.98f));
            Assert.That(camera.fieldOfView,Is.EqualTo(expectedZoomFov).Within(.2f));
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
            Call(memories[1],"Use",player);
            var mannequin=Get<Component>(chapter,"Mannequin");
            Assert.That(Get<bool>(shots,"CameraOwned"),Is.False,"The ribbon must not cut to a different corridor");
            Assert.That(Get<bool>(shots,"MannequinArmed"),Is.True);
            PlacePlayer(mannequin.transform.position+Vector3.forward*5);
            player.transform.rotation=Quaternion.Euler(0,180,0);
            camera.transform.rotation=Quaternion.LookRotation(mannequin.transform.position+Vector3.up*1.65f-camera.transform.position);
            var mannequinEye=camera.transform.position;
            yield return Wait(()=>Get<bool>(shots,"CameraOwned")&&CandleDangerPlayerSees(mannequin),2,
                "Actual sight never zoomed toward the real mannequin");
            Assert.That(((Behaviour)mannequin).enabled,Is.True);
            Assert.That(Get<bool>(mannequin,"Triggered"),Is.True);
            Assert.That(Vector3.Distance(mannequinEye,camera.transform.position),Is.LessThan(.02f));
            Call(LightRun,"RefreshDanger");
            Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThanOrEqualTo(.45f),"First camera-visible mannequin did not start the warning");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False);
            yield return ChapterAwaitAppearance();
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
