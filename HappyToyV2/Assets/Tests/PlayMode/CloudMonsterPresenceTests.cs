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
        public IEnumerator ChapterCyclopseFallsGetsUpBeforeLookingAndKeepsItsRealSkinGrounded()
        {
            Call(shell,"BeginChapter");yield return null;
            var chapter=Get<Component>(session,"Chapter");var shots=Get<Component>(chapter,"FirstAppearances");
            var actor=Get<Component>(chapter,"Cyclopse");var motion=actor.GetComponent(RequireType("V1MonsterMotion"));
            var intro=Get<Component>(shots,"CyclopseIntro");var feet=player.transform.position;
            var phases=new System.Collections.Generic.List<string>();
            float maximumPitch=0,maximumGroundGap=0,fallenHead=float.PositiveInfinity,recoveredHead=0;
            bool sampledProne=false,sampledRise=false;Vector3 fallenRoot=Vector3.zero;
            var recorder=new GameObject("CloudQA school real post-LateUpdate pose recorder").AddComponent<CloudReferenceLatePoseRecorder>();
            recorder.Sample=()=>
            {
                if(!actor.gameObject.activeInHierarchy)return;
                string phase=Get<string>(intro,"Phase");
                if(phases.Count==0||phases[phases.Count-1]!=phase)phases.Add(phase);
                if(phase=="stumble"||phase=="fallen"||phase=="getUp")
                {
                    Assert.That(((Behaviour)actor).enabled,Is.False);Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);
                    maximumPitch=Mathf.Max(maximumPitch,Get<float>(motion,"IntroBodyPitch"));
                    maximumGroundGap=Mathf.Max(maximumGroundGap,Mathf.Abs(Get<float>(motion,"GroundGap")));
                    Assert.That(actor.GetComponent<NavMeshAgent>().isStopped,Is.True);
                    if(phase=="fallen")
                    {
                        sampledProne=true;fallenRoot=actor.transform.position;
                        fallenHead=Mathf.Min(fallenHead,Get<Vector3>(motion,"IntroHeadPosition").y);
                        Assert.That(Get<bool>(motion,"IntroPoseActive"),Is.True);
                        Assert.That(Get<float>(motion,"IntroBodyPitch"),Is.GreaterThan(80));
                        var towardEye=Get<Camera>(player,"eyes").transform.position-actor.transform.position;towardEye.y=0;
                        Assert.That(Vector3.Dot(Get<Vector3>(motion,"IntroHeadPosition")-actor.transform.position,towardEye.normalized),
                            Is.GreaterThan(.35f),"The prone head must fall into the visible main hall rather than behind its corner");
                        Assert.That(CandleDangerPlayerSees(actor),Is.True,"The camera lost the actual fallen Cyclopse");
                        Call(LightRun,"RefreshDanger");
                        Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThanOrEqualTo(.45f),
                            "Falling must not interrupt the warning for a visibly present Cyclopse");
                    }
                    if(phase=="getUp")sampledRise=true;
                }
                if(phase=="roar")recoveredHead=Mathf.Max(recoveredHead,Get<Vector3>(motion,"IntroHeadPosition").y);
            };
            try
            {
                Call(Get<Component[]>(chapter,"Memories")[0],"Use",player);
                yield return Wait(()=>Get<string>(intro,"Phase")=="fallen"&&sampledProne,12,"Walking school Cyclopse never visibly fell on its actual skeleton");
                Assert.That(recorder.Error,Is.Null);Assert.That(Get<bool>(motion,"IntroPoseRigAvailable"),Is.True);
                Assert.That(Get<bool>(intro,"FallCompleted"),Is.True);
                float posePitch=Get<float>(motion,"IntroBodyPitch"),poseYaw=Get<float>(motion,"IntroBodyYaw"),clock=Get<float>(intro,"StumbleElapsed");
                var head=Get<Vector3>(motion,"IntroHeadPosition");Call(shell,"Pause");yield return Delay(.2f);
                Assert.That(Get<string>(intro,"Phase"),Is.EqualTo("fallen"));Assert.That(actor.transform.position,Is.EqualTo(fallenRoot));
                Assert.That(Get<float>(intro,"StumbleElapsed"),Is.EqualTo(clock));Assert.That(Get<float>(motion,"IntroBodyPitch"),Is.EqualTo(posePitch));
                Assert.That(Get<float>(motion,"IntroBodyYaw"),Is.EqualTo(poseYaw));
                Assert.That(Vector3.Distance(head,Get<Vector3>(motion,"IntroHeadPosition")),Is.LessThan(.001f));
                Call(shell,"Resume");yield return Wait(()=>Get<string>(intro,"Phase")=="roar",4,"Cyclopse did not stand up before facing the player");
                Assert.That(Get<bool>(intro,"GetUpCompleted"),Is.True);Assert.That(Get<bool>(motion,"IntroPoseActive"),Is.False);
                Assert.That(Get<float>(motion,"IntroBodyYaw"),Is.Zero);
                yield return null;
                Assert.That(recorder.Error,Is.Null);Assert.That(sampledRise,Is.True);
                Assert.That(maximumPitch,Is.GreaterThan(80));Assert.That(maximumGroundGap,Is.LessThan(.035f));
                Assert.That(recoveredHead-fallenHead,Is.GreaterThan(.75f),"Actual posed head did not rise from the floor to its standing height");
                yield return ChapterAwaitAppearance();Assert.That(recorder.Error,Is.Null);
                var requested=new[]{"emerge","stumble","fallen","getUp","roar","turnAway","pass","done"};
                int previous=-1;foreach(var phase in requested)
                {int index=phases.IndexOf(phase);Assert.That(index,Is.GreaterThan(previous),"School appearance order: "+string.Join(",",phases));previous=index;}
                Assert.That(Get<bool>(intro,"Completed"),Is.True);Assert.That(Get<bool>(intro,"PassCompleted"),Is.True);
                Assert.That(Get<int>(actor,"AttacksStarted"),Is.Zero);Assert.That(((Behaviour)actor).enabled,Is.False);
                Assert.That(Get<bool>(shots,"CyclopseGraceActive"),Is.True);Assert.That(Vector3.Distance(feet,player.transform.position),Is.LessThan(.05f));
            }
            finally {Object.Destroy(recorder.gameObject);}
        }

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
            yield return Wait(()=>Get<string>(intro,"Phase")=="roar",14,"School Cyclopse never finished falling/getting up before its glance");
            Assert.That(Get<bool>(intro,"StumbleStarted")&&Get<bool>(intro,"FallCompleted")&&Get<bool>(intro,"GetUpCompleted"),Is.True);
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
            PlacePlayer(new Vector3(13.8f,.03f,.1f));
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
