using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Controlled validated checkpoint input, not a production event selector.
        object UseSchoolAnomalyFixture(Component world,string kind)
        {
            int traversal=Math.Max(1,Get<int>(world,"Traversals"));Call(world,"RestoreTraversal",traversal);
            var director=Get<Component>(world,"Anomalies");var data=Call(director,"Capture");var deck=Get<object>(data,"deck");
            Set(deck,"Kind",Enum.Parse(RequireType("SchoolAnomalyKind"),kind));Set(deck,"NormalStreak",kind=="Normal"?1:0);Set(data,"deck",deck);
            Call(director,"Restore",data,traversal);return data;
        }
        IEnumerator CrossSchoolJudgmentSeam(Component world,int direction)
        {
            int count=Get<int>(world,"Traversals");
            PlacePlayer(LoopOrigin+new Vector3(direction>0?6.6f:-6.6f,.03f,direction>0?46.1f:-7.7f));
            player.transform.rotation=Quaternion.Euler(0,direction>0?0:180,0);yield return null;
            Keys(Key.W);yield return Wait(()=>Get<int>(world,"Traversals")==count+1,3,"The real judgment seam did not transfer");Keys();yield return null;
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolAnomalyFirstPassIsNormalAndActualSeamsJudgeBothDirections()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var director=Get<Component>(world,"Anomalies");Assert.That(Get<object>(director,"Kind").ToString(),Is.EqualTo("Normal"));
            Assert.That(Get<Component>(director,"Baby"),Is.Null);Assert.That(Get<bool>(Get<Component>(world,"Walker"),"Dancing"),Is.False);
            yield return CrossSchoolJudgmentSeam(world,1);Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1));
            foreach(var choice in new[]{("Normal",1,4),("Normal",-1,0),("Baby",1,0),("Blackout",-1,4),("DancingWalker",-1,4)})
            {
                Call(shell,"Pause");UseSchoolAnomalyFixture(world,choice.Item1);Call(world,"RestoreClock",3);Call(shell,"Resume");
                yield return CrossSchoolJudgmentSeam(world,choice.Item2);
                Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(choice.Item3));
                foreach(var display in Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay")))
                    Assert.That(Get<int>(display,"Hours"),Is.EqualTo(choice.Item3));
            }
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerSolidCapsuleAndFlickerTrembleAdvanceAndPause()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var walker=Get<Component>(world,"Walker");var collider=Get<CapsuleCollider>(walker,"BodyCollider");
            Assert.That(collider.enabled&&!collider.isTrigger,Is.True);Assert.That(collider.radius,Is.GreaterThan(.4f));
            Call(shell,"Pause");var state=Call(walker,"CaptureState");Set(state,"Z",12f);Call(walker,"RestoreState",state);
            PlacePlayer(LoopOrigin+new Vector3(.65f,.03f,10.5f));player.transform.rotation=Quaternion.identity;Call(shell,"Resume");
            Keys(Key.W);yield return Delay(1.8f);Keys();
            Assert.That(player.transform.position.z-LoopOrigin.z,Is.GreaterThan(10.6f));
            Assert.That(walker.transform.position.z-player.transform.position.z,Is.GreaterThan(.6f),"The actual CharacterController passed through the pedestrian");
            Assert.That(Mathf.Abs(walker.transform.position.x-.65f),Is.LessThan(.01f));
            Call(shell,"Pause");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,8));UseSchoolAnomalyFixture(world,"DancingWalker");
            Call(walker,"RestoreTraversal",Get<int>(world,"Traversals"));var motion=Get<Component>(walker,"Motion");
            var human=Get<Transform>(walker,"Visual");var creature=Get<Transform>(walker,"Creature");
            Assert.That(human.gameObject.activeSelf,Is.False);Assert.That(creature.gameObject.activeSelf,Is.True);
            Assert.That(walker.transform.localPosition.x,Is.EqualTo(0).Within(.001f),"The flicker creature must approach along the centre of the hall");
            var creatureSkin=creature.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
            var creatureMaterial=creatureSkin.sharedMaterial;
            Assert.That(creatureMaterial.GetTexture("_BaseMap"),Is.Not.Null,"The generated colour map must reach the active creature: "+creatureMaterial.name+" / "+creatureMaterial.shader.name+" / "+creatureSkin.name);
            Assert.That(creatureMaterial.IsKeywordEnabled("_EMISSION"),Is.True,"The actual colour map must remain visible under red lighting");
            Assert.That(creatureMaterial.GetTexture("_EmissionMap"),Is.EqualTo(creatureMaterial.GetTexture("_BaseMap")));
            var neutral=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("SchoolFlickerCreature/creature-visual"));
            try
            {
                var skin=neutral.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
                var baked=new Mesh();try{skin.BakeMesh(baked);var points=baked.vertices.Select(v=>neutral.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                    var actual=new Bounds(points[0],Vector3.zero);foreach(var point in points)actual.Encapsulate(point);
                    Assert.That(actual.size.x,Is.EqualTo(3.9f).Within(.05f),"The creature must fill most of the 4.4m corridor");
                    Assert.That(actual.min.y,Is.EqualTo(0).Within(.04f),"The actual creature feet must be grounded");
                    Assert.That(actual.size.y,Is.LessThan(3.2f),"The creature must fit below the ceiling in its neutral pose");
                }finally{UnityEngine.Object.Destroy(baked);}
            }finally{UnityEngine.Object.Destroy(neutral);}
            var bones=creature.GetComponentsInChildren<Transform>();var before=bones.Select(b=>b.localRotation).ToArray();
            float distance=Get<float>(walker,"Distance");var heldState=Call(walker,"CaptureState");double heldTime=Get<double>(heldState,"Elapsed");
            Call(shell,"Resume");yield return Wait(()=>Get<double>(Call(walker,"CaptureState"),"Elapsed")>=heldTime+.3,10,"The flicker clock did not advance");
            Assert.That(Get<bool>(walker,"Dancing")&&Get<bool>(motion,"Dancing"),Is.True);
            Assert.That(Get<float>(walker,"Distance")-distance,Is.LessThan(.01f),"The red hold must keep the walker in place");
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(before[i],b.localRotation)).Sum(),Is.LessThan(.01f),"The lit pose must remain completely still");
            yield return Delay(1.7f);
            Assert.That(Get<float>(walker,"Distance")-distance,Is.GreaterThan(.1f),"The flashes must advance the walker");
            Assert.That(walker.transform.localPosition.x,Is.EqualTo(0).Within(.001f),"The approaching creature must stay on the centre line");
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(before[i],b.localRotation)).Sum(),Is.GreaterThan(20),"The flashes must change the rig pose");
            Call(shell,"Pause");var paused=bones.Select(b=>(b.localPosition,b.localRotation)).ToArray();float stopped=Get<float>(walker,"Distance");yield return Delay(.2f);
            CollectionAssert.AreEqual(paused,bones.Select(b=>(b.localPosition,b.localRotation)).ToArray());
            Assert.That(Get<float>(walker,"Distance"),Is.EqualTo(stopped),"Pause must hold the flicker advance");
            ExportSchoolAnomalyFrame(world,"dancing-walker",new Vector3(-1.2f,.03f,25),new Vector3(.65f,1.3f,31));
            var snapState=Call(walker,"CaptureState");Set(snapState,"Elapsed",1.60d);Set(snapState,"IdleElapsed",0d);Call(walker,"RestoreState",snapState);
            var curtain=Get<Component>(walker,"FlickerCurtain");Assert.That(Get<bool>(curtain,"Black"),Is.True,"The screen must be black while the flicker lights are off");
            var document=UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(d=>d.name=="School flicker independent screen curtain");
            Assert.That(document,Is.Not.Null);Assert.That(document.panelSettings.sortingOrder,Is.GreaterThan(500));
            Assert.That(document.rootVisualElement.style.backgroundColor.value,Is.EqualTo(Color.black));
            Assert.That(document.rootVisualElement.style.display.value,Is.EqualTo(DisplayStyle.Flex));
            bool invertedAtStart=Get<bool>(walker,"CreatureInverted");
            var flashPose=bones.Select(b=>b.localRotation).ToArray();
            var fixtures=Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolFluorescent"));
            Assert.That(fixtures.All(f=>Get<Light>(f,"Source").intensity==0),Is.True,"The pose must snap at light-off");
            Set(snapState,"Elapsed",1.65d);Call(walker,"RestoreState",snapState);
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(flashPose[i],b.localRotation)).Sum(),Is.LessThan(.01f),"The entire dark interval must hold one pose");
            Assert.That(Get<bool>(walker,"CreatureInverted"),Is.EqualTo(invertedAtStart),"Orientation must remain fixed throughout each flash");
            Set(snapState,"Elapsed",1.70d);Call(walker,"RestoreState",snapState);Assert.That(Get<bool>(curtain,"Black"),Is.False,"The curtain must clear as soon as lights return");
            Set(snapState,"Elapsed",1.73d);Call(walker,"RestoreState",snapState);Assert.That(Get<bool>(curtain,"Black"),Is.True);
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(flashPose[i],b.localRotation)).Sum(),Is.GreaterThan(100),"The next light-off must snap to a different contortion");
            bool sawInverted=false,sawUpright=false;
            foreach(double t in new[]{1.60,1.73,1.84,1.97,2.10,2.23,2.35,2.48,3.90,4.02,4.14,4.27,4.42})
            {Set(snapState,"Elapsed",t);Call(walker,"RestoreState",snapState);bool upsideDown=Get<bool>(walker,"CreatureInverted");sawInverted|=upsideDown;sawUpright|=!upsideDown;}
            Assert.That(sawInverted&&sawUpright,Is.True,"The saved event seed must produce both orientations");
            Call(walker,"SetDancing",false);
            Assert.That(human.gameObject.activeSelf,Is.True);Assert.That(creature.gameObject.activeSelf,Is.False);Assert.That(Get<bool>(curtain,"Black"),Is.False);
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBabyAppearsAtTheStudySignCriesPursuesAfterTimeoutAndRetreatRemovesIt()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,15));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");var babyTorch=Get<Light>(player,"flashlight");bool torchEnabled=babyTorch.enabled;
            yield return Delay(.15f);Assert.That(Get<Component>(director,"Baby"),Is.Null);
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16.1f));yield return Wait(()=>Get<Component>(director,"Baby"),3,"Passing the actual study sign did not summon Baby");
            var baby=Get<Component>(director,"Baby");var source=Get<AudioSource>(baby,"CrySource");var visual=Get<Transform>(baby,"Visual");
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.loop,Is.True);Assert.That(source.maxDistance,Is.GreaterThanOrEqualTo(40));
            Assert.That(visual.GetComponent(RequireType("EnemyStudioRig")),Is.Not.Null);
            Assert.That(visual.localScale.x,Is.EqualTo(Resources.Load<GameObject>("EnemyStudio/Baby/BabyRuntime").transform.localScale.x*2f/3f).Within(.001f));
            Assert.That(baby.transform.position.z-LoopOrigin.z,Is.EqualTo(9.4f).Within(.5f),"Baby must wait behind the study sign at the shop");
            Assert.That(Get<float>(baby,"Opacity"),Is.EqualTo(1),"Baby must be visible as soon as the lights go out");
            var allFixtures=Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolFluorescent"));
            Assert.That(allFixtures.All(f=>Get<Light>(f,"Source").intensity==0),Is.True,"All school fixtures must switch off together");
            Assert.That(babyTorch.enabled,Is.EqualTo(torchEnabled),"The player flashlight must survive the Baby blackout");
            var atmosphere=Get<Component>(baby,"Atmosphere");var camera=Get<Camera>(player,"eyes");
            camera.transform.rotation=Quaternion.LookRotation(baby.transform.position+Vector3.up*.5f-camera.transform.position);
            Call(atmosphere,"Tick",1d,false,true);
            Assert.That(Get<bool>(atmosphere,"Witnessed"),Is.True,"Looking directly at the crying Baby must introduce the player heartbeat");
            float firstPulse=Get<AudioSource>(atmosphere,"PulseSource").volume;
            Call(atmosphere,"Tick",59d,false,true);
            Assert.That(Get<AudioSource>(atmosphere,"PulseSource").volume,Is.GreaterThan(firstPulse),"The pulse must build towards the deadline");
            foreach(string materialName in new[]{"BabyFade","BabyFadeEmissive"})
            {
                var retained=Resources.Load<Material>("SchoolAnomalies/"+materialName);
                Assert.That(retained&&retained.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")&&retained.IsKeywordEnabled("_NORMALMAP")&&retained.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),Is.True);
                Assert.That(retained.GetFloat("_Surface"),Is.EqualTo(1));
            }
            Assert.That(baby.GetComponent(RequireType("StalkerBrain")),Is.Null,"The retired corridor AI was accidentally re-enabled");
            var agent=Get<NavMeshAgent>(baby,"Agent");Assert.That(agent.isStopped,Is.True);
            yield return Delay(2.3f);Assert.That(Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),Is.False);
            ExternalMuteExcept(source);int rate=AudioSettings.outputSampleRate;bool started=false;float capture=Time.captureDeltaTime;
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=StartOwnedAudioRenderer();Assert.That(started,Is.True);
                yield return ExternalCapture("school-baby-distant-cry",source,source.clip,"enemy-baby-cry",.5f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.GreaterThan(.00001),"The distant crying did not reach the real quiet mixer");
            }
            finally{if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=capture;}
            var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);yield return null;
            var frozenClock=Call(Get<object>(director,"EventClock"),"Capture");var frozenPosition=baby.transform.position;
            yield return Delay(.25f);Assert.That(Call(Get<object>(director,"EventClock"),"Capture"),Is.EqualTo(frozenClock));
            Assert.That(baby.transform.position,Is.EqualTo(frozenPosition));Assert.That(source.isPlaying,Is.False);
            Assert.That(Get<AudioSource>(atmosphere,"AirSource").isPlaying||Get<AudioSource>(atmosphere,"PulseSource").isPlaying,Is.False,
                "Both subjective tension sounds must pause with the Baby");
            Call(rituals,"EndEffect");yield return null;
            Call(shell,"Pause");ExportSchoolAnomalyFrame(world,"baby-crying",new Vector3(-1.2f,.03f,16),new Vector3(-.95f,.6f,9.4f));
            var saved=Call(director,"Capture");var clock=Get<object>(saved,"clock");Set(clock,"Elapsed",59.8);Set(saved,"clock",clock);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));baby=Get<Component>(director,"Baby");agent=Get<NavMeshAgent>(baby,"Agent");
            Assert.That(allFixtures.All(f=>Get<Light>(f,"Source").intensity==0),Is.True,"Restoring an active Baby must restore its blackout");
            var held=Call(Get<object>(director,"EventClock"),"Capture");yield return Delay(.3f);Assert.That(Call(Get<object>(director,"EventClock"),"Capture"),Is.EqualTo(held));
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),1,"The 60-second pursuit deadline did not fire");
            var at=baby.transform.position;yield return Delay(.4f);Assert.That(Vector3.Distance(at,baby.transform.position),Is.GreaterThan(.2f));
            Call(world,"RestoreClock",2);yield return CrossSchoolJudgmentSeam(world,-1);
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(3));yield return null;
            Assert.That(allFixtures.All(f=>Get<Light>(f,"Source").intensity>0),Is.True,"Retreat must restore the extinguished fixtures");
            Assert.That(baby&&Get<bool>(baby,"Departing"),Is.True,"Retreat must start a visible departure, not delete Baby immediately");
            yield return Wait(()=>!baby,3,"The retired Baby did not finish fading out");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            yield return Wait(()=>Get<Component>(director,"Baby"),3,"The next Baby event did not appear");yield return Delay(2.3f);
            PlacePlayer(LoopOrigin+new Vector3(-.95f,.03f,12.5f));yield return Delay(.2f);
            Assert.That(Get<bool>(Get<object>(director,"EventClock"),"Pursuing"),Is.False,"Close approach must not shorten the full minute");
            baby=Get<Component>(director,"Baby");PlacePlayer(baby.transform.position+Vector3.forward*.45f);
            yield return Wait(()=>Get<bool>(session,"Finished"),2,"Actual Baby contact did not cause defeat");
            Assert.That(Get<string>(session,"DefeatSource"),Does.Contain("베이비"));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBlackoutExtinguishesFromFarEndRestoresOnRetreatAndKillsAfterFiveDarkSeconds()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Blackout");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");var clock=Get<object>(director,"EventClock");
            yield return Wait(()=>Get<bool>(clock,"Triggered"),2,"Study-sign crossing did not start the blackout");
            Call(shell,"Pause");var saved=Call(director,"Capture");var timing=Get<object>(saved,"clock");Set(timing,"Elapsed",2.5);Set(saved,"clock",timing);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));
            var aisle=Get<Transform>(world,"Centre").GetComponentsInChildren(RequireType("SchoolFluorescent"))
                .Where(f=>Mathf.Abs(f.transform.localPosition.x)<.2f).OrderByDescending(f=>f.transform.localPosition.z).ToArray();
            Assert.That(Get<Light>(aisle[0],"Source").intensity,Is.Zero);Assert.That(Get<Light>(aisle[1],"Source").intensity,Is.Zero);
            Assert.That(Get<Light>(aisle[2],"Source").intensity,Is.GreaterThan(0),"The near lights went out before the far lights");
            ExportSchoolAnomalyFrame(world,"blackout-progress",new Vector3(.72f,.03f,16.2f),new Vector3(0,1.5f,36));
            Call(shell,"Resume");yield return CrossSchoolJudgmentSeam(world,-1);Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1));
            Assert.That(aisle.All(f=>Get<Light>(f,"Source").intensity>0),Is.True,"Retreat left the next passage's lamps switched off");
            Call(shell,"Pause");saved=UseSchoolAnomalyFixture(world,"Blackout");timing=Get<object>(saved,"clock");Set(timing,"Triggered",true);Set(timing,"Elapsed",13.5);Set(saved,"clock",timing);
            Call(director,"Restore",saved,Get<int>(world,"Traversals"));Assert.That(Get<int>(Get<Component>(director,"Blackout"),"Extinguished"),Is.EqualTo(9));
            Assert.That(Get<float>(Get<Component>(director,"Blackout"),"Darkness"),Is.EqualTo(1));
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));yield return Delay(.3f);Assert.That(Get<bool>(session,"Finished"),Is.False);
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            PlacePlayer(LoopOrigin+new Vector3(.72f,.03f,6.3f),false);
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(2.2f,1.3f,8.4f)-camera.transform.position);
            torch.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
            torch.enabled=false;var unlit=SchoolHorrorFrame(camera,640,360);
            torch.enabled=true;var lit=SchoolHorrorFrame(camera,640,360);
            float off=SchoolDisplayLuma(unlit,new Rect(0,0,1,1)),on=SchoolDisplayLuma(lit,new Rect(0,0,1,1));
            var darkPixels=unlit.GetPixels32();var litPixels=lit.GetPixels32();int brighter=0;
            for(int i=0;i<darkPixels.Length;i++)if(litPixels[i].r+litPixels[i].g+litPixels[i].b>darkPixels[i].r+darkPixels[i].g+darkPixels[i].b+20)brighter++;
            Debug.Log("HAPPYTOY_SCHOOL_BLACKOUT_TORCH off="+off+" on="+on+" brighterPixels="+brighter);
            string review=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(review))
            {
                review=Path.GetFullPath(review);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(review.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(review);
                File.WriteAllBytes(Path.Combine(review,"blackout-torch-off.png"),unlit.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(review,"blackout-torch-on.png"),lit.EncodeToPNG());
            }
            Assert.That(on,Is.GreaterThan(off+.005f),"A screen-wide blackout filter is still suppressing the real flashlight");
            Assert.That(brighter,Is.GreaterThan(2000),"The torch did not reveal enough actual school surfaces after all nine lamps went out");
            Object.Destroy(unlit);Object.Destroy(lit);
            Call(shell,"Resume");yield return Wait(()=>Get<bool>(session,"Finished"),2,"Five seconds in full darkness did not cause defeat");
            Assert.That(Get<bool>(session,"Escaped"),Is.False);
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBabyRetreatCarriesTheGhostThroughTheSeamAndFadesBodyAndVoiceDuringSafeDeparture()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");var saved=UseSchoolAnomalyFixture(world,"Baby");var timing=Get<object>(saved,"clock");
            Set(timing,"Triggered",true);Set(timing,"Pursuing",true);Set(timing,"Elapsed",10d);Set(saved,"clock",timing);
            var body=Get<object>(saved,"baby");Set(body,"present",true);Set(body,"position",new Vector3(-6.6f,.02f,-5f));Set(body,"yaw",180f);Set(saved,"baby",body);
            var director=Get<Component>(world,"Anomalies");Call(director,"Restore",saved,Get<int>(world,"Traversals"));
            var baby=Get<Component>(director,"Baby");var voice=Get<AudioSource>(baby,"CrySource");var ownedVoice=voice.clip;
            var shared=Resources.Load<GameObject>("EnemyStudio/Baby/BabyRuntime");var sourceMaterials=shared.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            var sourceColors=sourceMaterials.Select(m=>m.GetColor("_BaseColor")).ToArray();
            Call(shell,"Resume");yield return CrossSchoolJudgmentSeam(world,-1);Call(shell,"Pause");
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1));Assert.That(Get<Component>(director,"Baby"),Is.Null);
            Assert.That(baby&&Get<bool>(baby,"Departing"),Is.True);Assert.That(Get<NavMeshAgent>(baby,"Agent").enabled,Is.False);
            Assert.That(baby.transform.position.z-LoopOrigin.z,Is.LessThan(-8),"The retiring ghost stayed on the wrong side of the rigid transfer");
            float first=Get<float>(baby,"Opacity");Assert.That(first,Is.GreaterThan(.8f));
            var at=baby.transform.position;yield return Delay(.2f);Assert.That(Get<float>(baby,"Opacity"),Is.EqualTo(first));
            var checkpoint=Call(session,"CaptureChapterCheckpoint");Call(checkpoint,"Validate");
            var next=Get<object>(Get<object>(checkpoint,"loop"),"anomalies");Assert.That(Get<bool>(Get<object>(next,"baby"),"present"),Is.False,
                "A visual departure must not resurrect the old attacking event in the next passage's save");
            Call(shell,"Resume");yield return Delay(.65f);
            float middle=Get<float>(baby,"Opacity");Assert.That(middle,Is.InRange(.05f,first-.1f));
            Assert.That(Vector3.Distance(at,baby.transform.position),Is.LessThan(.001f),"The safely retiring Baby kept pursuing");
            Assert.That(voice.volume,Is.LessThanOrEqualTo(.72f*middle+.001f),"The crying voice ignored the actor's fade envelope");
            foreach(var material in Get<Transform>(baby,"Visual").GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                Assert.That(material.GetColor("_BaseColor").a,Is.EqualTo(middle).Within(.001f));
            Assert.That(Get<bool>(session,"Finished"),Is.False);
            Call(shell,"Pause");var local=baby.transform.position-LoopOrigin;
            ExportSchoolAnomalyFrame(world,"baby-departure-mid",new Vector3(-6.1f,.03f,-7.3f),local+Vector3.up*.8f);
            Call(shell,"Resume");var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);yield return null;
            float held=Get<float>(baby,"Opacity");yield return Delay(.2f);Assert.That(Get<float>(baby,"Opacity"),Is.EqualTo(held));
            Call(rituals,"EndEffect");yield return Wait(()=>!baby,3,"The visual and voice departure never completed");yield return null;
            Assert.That(!voice&&!ownedVoice,Is.True,"The retiring Baby leaked its owned cry source or recording");
            CollectionAssert.AreEqual(sourceColors,sourceMaterials.Select(m=>m.GetColor("_BaseColor")).ToArray());
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolAnomalyCheckpointKeepsTheDrawTimersBabyPoseAndLegacyNormalMigration()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16));Call(shell,"Resume");
            var director=Get<Component>(world,"Anomalies");yield return Wait(()=>Get<Component>(director,"Baby"),3,"Baby did not appear");yield return Delay(.3f);
            Call(shell,"Pause");var checkpoint=Call(session,"CaptureChapterCheckpoint");var saved=Get<object>(Get<object>(checkpoint,"loop"),"anomalies");
            string json=(string)Call(checkpoint,"ToJson");var copy=Call(RequireType("ChapterCheckpoint"),"FromJson",json);Call(copy,"Validate");
            var expectedBaby=Get<Component>(director,"Baby").transform.position;
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",copy);Call(session,"ApplyChapterCheckpoint",copy);world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");director=Get<Component>(world,"Anomalies");
            var restored=Call(director,"Capture");Assert.That(Get<object>(restored,"deck"),Is.EqualTo(Get<object>(saved,"deck")));
            Assert.That(Get<object>(restored,"clock"),Is.EqualTo(Get<object>(saved,"clock")));
            Assert.That(Vector3.Distance(Get<Component>(director,"Baby").transform.position,expectedBaby),Is.LessThan(.02f));
            var invalid=Call(copy,"Copy");var bad=Get<object>(Get<object>(invalid,"loop"),"anomalies");var body=Get<object>(bad,"baby");
            Assert.That(Physics.Raycast(LoopOrigin+new Vector3(0,.9f,20),Vector3.right,out var wall,3,1<<8,QueryTriggerInteraction.Ignore),Is.True,
                "The corruption fixture requires an actual solid aisle wall, not the open exit elbow");
            var insideWall=wall.point-LoopOrigin-wall.normal*.05f;insideWall.y=0;
            Set(body,"position",insideWall);Set(bad,"baby",body);
            var unchanged=player.transform.position;Assert.Catch<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",invalid));
            Assert.That(player.transform.position,Is.EqualTo(unchanged));
            Assert.That(Get<object>(Call(director,"Capture"),"deck"),Is.EqualTo(Get<object>(saved,"deck")));
            var legacy=Call(copy,"Copy");Set(Get<object>(legacy,"loop"),"anomalies",null);
            legacy=Call(RequireType("ChapterCheckpoint"),"FromJson",(string)Call(legacy,"ToJson"));Assert.That(Get<object>(Get<object>(legacy,"loop"),"anomalies"),Is.Null);
            previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",legacy);Call(session,"ApplyChapterCheckpoint",legacy);world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Assert.That(Get<object>(Get<Component>(world,"Anomalies"),"Kind").ToString(),Is.EqualTo("Normal"));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBabyBlackoutShopTimeoutContactAndPauseAreHeadlessSafe()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var director=Get<Component>(world,"Anomalies");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Baby");
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,15));Call(shell,"Resume");yield return Delay(.1f);
            Assert.That(Get<Component>(director,"Baby"),Is.Null,"Baby appeared before the study sign");
            PlacePlayer(LoopOrigin+new Vector3(-1.2f,.03f,16.1f));
            yield return Wait(()=>Get<Component>(director,"Baby"),3,"The study sign did not trigger Baby");
            var baby=Get<Component>(director,"Baby");var clock=Get<object>(director,"EventClock");
            var visual=Get<Transform>(baby,"Visual");var source=Resources.Load<GameObject>("EnemyStudio/Baby/BabyRuntime");
            Assert.That(visual.localScale.x,Is.EqualTo(source.transform.localScale.x*2f/3f).Within(.001f));
            Assert.That(baby.transform.position.z-LoopOrigin.z,Is.EqualTo(9.4f).Within(.5f));
            Assert.That(Get<float>(baby,"Opacity"),Is.EqualTo(1));
            var fixtures=Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolFluorescent"));
            Assert.That(fixtures.Length,Is.GreaterThan(27));
            Assert.That(fixtures.All(f=>Get<Light>(f,"Source").intensity==0),Is.True,"Room, connector and corridor lamps must all go out");
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(baby.transform.position+Vector3.up*.5f-camera.transform.position);
            Physics.SyncTransforms();var atmosphere=Get<Component>(baby,"Atmosphere");Call(atmosphere,"Tick",1d,false,true);
            Assert.That(Get<bool>(atmosphere,"Witnessed"),Is.True,"Looking at Baby must introduce the witness heartbeat");
            float pulse=Get<AudioSource>(atmosphere,"PulseSource").volume;Call(atmosphere,"Tick",59d,false,true);
            Assert.That(Get<AudioSource>(atmosphere,"PulseSource").volume,Is.GreaterThan(pulse));
            Call(shell,"Pause");var held=Call(clock,"Capture");var position=baby.transform.position;yield return Delay(.2f);
            Assert.That(Call(clock,"Capture"),Is.EqualTo(held));Assert.That(baby.transform.position,Is.EqualTo(position));
            PlacePlayer(LoopOrigin+new Vector3(-.95f,.03f,12.5f));Call(shell,"Resume");yield return Delay(.2f);
            Assert.That(Get<bool>(clock,"Pursuing"),Is.False,"Close approach must not shorten the full minute");
            Call(shell,"Pause");var saved=Call(director,"Capture");var timing=Get<object>(saved,"clock");
            Assert.That(Get<bool>(Get<object>(saved,"baby"),"witnessed"),Is.True,"Capture must preserve that Baby was witnessed");
            var checkpoint=Call(session,"CaptureChapterCheckpoint");string json=(string)Call(checkpoint,"ToJson");
            var decoded=Call(RequireType("ChapterCheckpoint"),"FromJson",json);
            Assert.That(Get<bool>(Get<object>(Get<object>(Get<object>(decoded,"loop"),"anomalies"),"baby"),"witnessed"),Is.True,
                "The actual JSON codec must preserve a witnessed Baby");
            string oldJson=Regex.Replace(json,"\\\"witnessed\\\"\\s*:\\s*(?:true|false)\\s*,","");
            Assert.That(oldJson,Does.Not.Contain("\"witnessed\""),"Legacy JSON fixture must omit the optional field");
            var legacy=Call(RequireType("ChapterCheckpoint"),"FromJson",oldJson);
            Assert.That(Get<bool>(Get<object>(Get<object>(Get<object>(legacy,"loop"),"anomalies"),"baby"),"witnessed"),Is.False,
                "Old v3 saves must default to an unwitnessed Baby");
            Set(timing,"Elapsed",59.8d);Set(saved,"clock",timing);Call(director,"Restore",saved,Get<int>(world,"Traversals"));
            Assert.That(fixtures.All(f=>Get<Light>(f,"Source").intensity==0),Is.True,"A saved active event must restore darkness");
            baby=Get<Component>(director,"Baby");atmosphere=Get<Component>(baby,"Atmosphere");
            Assert.That(Get<bool>(atmosphere,"Witnessed"),Is.True,"Restoring must retain the heartbeat after looking away");
            camera.transform.rotation=Quaternion.Euler(0,0,0);Call(shell,"Resume");
            Call(atmosphere,"Tick",59.8d,false,true);
            Assert.That(Get<AudioSource>(atmosphere,"PulseSource").volume,Is.GreaterThan(0),"The witnessed heartbeat must continue without another look");
            yield return Wait(()=>Get<bool>(clock,"Pursuing"),2,"The 60-second deadline did not start the chase");
            PlacePlayer(baby.transform.position+Vector3.forward*.45f);
            yield return Wait(()=>Get<bool>(session,"Finished"),2,"Touching Baby must immediately defeat the player");
            Assert.That(Get<string>(session,"DefeatSource"),Does.Contain("베이비"));
        }
        void ExportSchoolAnomalyFrame(Component world,string name,Vector3 feet,Vector3 target)
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR");if(string.IsNullOrWhiteSpace(output))return;
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(output);
            var camera=Get<Camera>(player,"eyes");var before=player.transform.position;var rotation=camera.transform.rotation;
            PlacePlayer(LoopOrigin+feet,false);camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+target-camera.transform.position);
            var frame=SchoolHorrorFrame(camera,1280,720);File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());Object.Destroy(frame);
            PlacePlayer(before,false);camera.transform.rotation=rotation;
        }
    }
}
