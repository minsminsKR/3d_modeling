using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator BaeksugwiLoadingKeepsApproachingWhileActualCorridorIsPrepared()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;
            var title=One("CinematicTitleScene");var creature=GameObject.Find("Actual Baeksugwi source clone");
            var wrist=creature.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Arm1L_Wrist");
            var firstWrist=wrist.localRotation;var pivot=creature.transform.parent;
            var start=pivot.position;int renders=Get<int>(title,"RenderedFrames");
            Call(shell,"StartCorridorFromMenu");Assert.That(Get<bool>(shell,"IsStartingRun"),Is.True);
            Call(shell,"StartCorridorFromMenu");Call(shell,"StartChapterFromMenu");
            float distance=0,gait=0;int frames=0;float deadline=Time.realtimeSinceStartup+60;
            while(Get<bool>(shell,"IsReloading") && Time.realtimeSinceStartup<deadline)
            {
                Assert.That(Get<bool>(session,"InputAllowed"),Is.False);Assert.That(Time.timeScale,Is.Zero);
                yield return null;frames++;
                distance=Mathf.Max(distance,Vector3.Distance(start,pivot.position));
                gait=Mathf.Max(gait,Quaternion.Angle(firstWrist,wrist.localRotation));
            }
            Assert.That(Get<string>(shell,"ReloadError"),Is.Empty);
            Assert.That(Get<bool>(session,"CorridorMode"),Is.True);Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            Assert.That(Get<bool>(shell,"IsStartingRun"),Is.False);
            Assert.That(distance,Is.GreaterThan(.1f));Assert.That(gait,Is.GreaterThan(3));
            Assert.That(Get<int>(title,"RenderedFrames")-renders,Is.GreaterThan(5));Assert.That(frames,Is.GreaterThan(10));
            Assert.That(Components("CorridorRun").Length,Is.EqualTo(1));Assert.That(Components("MemoryChapter").Length,Is.Zero);
            Assert.That(Get<bool>(Get<Component>(session,"Corridor"),"Ready"),Is.True);
            Call(shell,"Pause");Assert.That(Get<bool>(session,"InputAllowed"),Is.False);
            Call(shell,"Resume");Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator BaeksugwiLoadingAlsoPreparesPlayableSchoolFromActualMenuButton()
        {
            yield return RecoveryUiReady();yield return RecoveryClick("begin-school");
            Assert.That(Get<string>(shell,"ReloadError"),Is.Empty);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            Assert.That(Get<bool>(shell,"IsStartingRun"),Is.False);
            Assert.That(Components("MemoryChapter").Length,Is.EqualTo(1));Assert.That(Components("CorridorRun").Length,Is.Zero);
        }

        [Test]
        public void BaeksugwiLargeHeadMotionKeepsTheWholeSkinInsideItsFloorAndHeightEnvelope()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace").SampleAnimation(instance,0);
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var evaluate=RequireType("EnemyStudioRig").GetMethod("PosedBounds");
                clip.SampleAnimation(instance,0);var neutral=(Bounds)evaluate.Invoke(null,new object[]{skins});
                for(int i=0;i<=180;i++)
                {
                    clip.SampleAnimation(instance,clip.length*i/180);
                    var bounds=(Bounds)evaluate.Invoke(null,new object[]{skins});
                    Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(neutral.min.y-.015f),"Head skin crosses the floor at "+i);
                    Assert.That(bounds.max.y,Is.LessThanOrEqualTo(neutral.max.y+.05f),"Head skin crosses the body height envelope at "+i);
                }
            }
            finally{Object.DestroyImmediate(instance);}
        }

        [Test]
        public void BaeksugwiHeadRollIsLargeIrregularAndDoesNotRepeatTheOldShortCycle()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                Assert.That(clip.length,Is.GreaterThan(30));
                Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace").SampleAnimation(instance,0);
                var head=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
                clip.SampleAnimation(instance,0);var neutral=head.rotation;
                float low=0,high=0;var differences=new System.Collections.Generic.List<float>();
                for(int i=0;i<160;i++)
                {
                    float time=clip.length*i/160;
                    clip.SampleAnimation(instance,time);
                    var pose=head.rotation;var delta=pose*Quaternion.Inverse(neutral);
                    float roll=2*Mathf.Atan2(delta.z,delta.w)*Mathf.Rad2Deg;
                    roll=Mathf.DeltaAngle(0,roll);low=Mathf.Min(low,roll);high=Mathf.Max(high,roll);
                    clip.SampleAnimation(instance,Mathf.Repeat(time+2.4f,clip.length));
                    differences.Add(Quaternion.Angle(pose,head.rotation));
                }
                Assert.That(high,Is.GreaterThan(40));Assert.That(low,Is.LessThan(-40));
                Assert.That(differences.Average(),Is.GreaterThan(20),"Must not repeat the old 2.4-second wave");
            }
            finally{Object.DestroyImmediate(instance);}
        }

        [Test]
        public void BaeksugwiHeadActionMovesOnlyHeadAndLoopsWithoutMovingAnyArm()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace").SampleAnimation(instance,0);
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");Assert.That(clip && clip.legacy,Is.True);
                var nodes=instance.GetComponentsInChildren<Transform>(true);
                var head=nodes.Single(t=>t.name=="Head");var baseline=head.localRotation;
                var others=nodes.Where(t=>t!=head).ToArray();var rotations=others.Select(t=>t.localRotation).ToArray();
                var positions=nodes.Select(t=>t.localPosition).ToArray();var scales=nodes.Select(t=>t.localScale).ToArray();
                clip.SampleAnimation(instance,0);var first=head.localRotation;float swing=0;
                Assert.That(Quaternion.Angle(baseline,first),Is.LessThan(.2f),"Neutral head must fit the existing rig");
                for(int sample=0;sample<=32;sample++)
                {
                    clip.SampleAnimation(instance,clip.length*sample/32f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));
                    for(int i=0;i<others.Length;i++)Assert.That(Quaternion.Angle(rotations[i],others[i].localRotation),Is.LessThan(.01f),others[i].name+" body rotation changed");
                    for(int i=0;i<nodes.Length;i++)
                    {
                        Assert.That(Vector3.Distance(positions[i],nodes[i].localPosition),Is.LessThan(.00001f),nodes[i].name+" joint translation changed");
                        Assert.That(Vector3.Distance(scales[i],nodes[i].localScale),Is.LessThan(.00001f));
                    }
                }
                Assert.That(swing,Is.GreaterThan(18));Assert.That(Quaternion.Angle(first,head.localRotation),Is.LessThan(.1f));
            }
            finally{Object.DestroyImmediate(instance);}
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiHeadWritheUsesChaseStateFreezesOnPauseAndStopsWhenCalm()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");((Behaviour)actor).enabled=false;
            PlacePlayer(new Vector3(200,5,200),false);
            var state=Call(actor,"CaptureChapterProgress");Set(state,"state","Chase");Set(state,"memory",8f);
            Call(actor,"RestoreCorridorProgress",state);
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));var head=Get<Transform>(art,"HeadSocket");
            Assert.That(Get<AnimationClip>(art,"HeadMotionClip"),Is.SameAs(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe")));
            var model=Get<Transform>(art,"BodyModel");var arms=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_Wrist")).ToArray();
            yield return null;var first=head.localRotation;var planted=arms.Select(t=>t.localRotation).ToArray();float swing=0;
            for(int i=0;i<10;i++) {yield return Delay(.12f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));}
            Assert.That(swing,Is.GreaterThan(12));Assert.That(Get<bool>(art,"HeadWrithing"),Is.True);
            for(int i=0;i<arms.Length;i++)Assert.That(Quaternion.Angle(planted[i],arms[i].localRotation),Is.LessThan(.01f),"Stopped chase must move only the head");
            Call(shell,"Pause");var paused=head.localRotation;float clock=Get<float>(art,"HeadMotionTime");yield return Delay(.2f);
            Assert.That(Quaternion.Angle(paused,head.localRotation),Is.LessThan(.01f));Assert.That(Get<float>(art,"HeadMotionTime"),Is.EqualTo(clock));
            Call(shell,"Resume");state=Call(actor,"CaptureChapterProgress");Set(state,"state","Wander");Set(state,"memory",0f);Call(actor,"RestoreCorridorProgress",state);
            yield return null;yield return null;Assert.That(Get<float>(art,"HeadMotionTime"),Is.Zero);Assert.That(Get<bool>(art,"HeadWrithing"),Is.False);
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiHeadWritheAlsoMovesTheSchoolFloatingMaskBeforeTransformation()
        {
            yield return RecoveryUiReady();yield return RecoveryClick("begin-school");
            var actor=Get<Component>(Get<Component>(session,"Chapter"),"Mask");((Behaviour)actor).enabled=false;
            var state=Call(actor,"CaptureChapterProgress");Set(state,"active",true);Set(state,"introComplete",true);
            Set(state,"transformed",false);Set(state,"state","Chase");Set(state,"memory",3f);Call(actor,"RestoreChapterProgress",state);
            var mask=Get<Transform>(actor,"mask");var body=Get<Transform>(actor,"body");var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(body.gameObject.activeSelf,Is.False);yield return null;var first=mask.localRotation;float swing=0;
            for(int i=0;i<12;i++){yield return Delay(.1f);swing=Mathf.Max(swing,Quaternion.Angle(first,mask.localRotation));}
            Assert.That(swing,Is.GreaterThan(15));Assert.That(Get<bool>(art,"HeadWrithing"),Is.True);
            Call(shell,"Pause");var paused=mask.localRotation;yield return Delay(.2f);Assert.That(Quaternion.Angle(paused,mask.localRotation),Is.LessThan(.01f));
            Call(shell,"Resume");state=Call(actor,"CaptureChapterProgress");Set(state,"state","Wander");Set(state,"memory",0f);Call(actor,"RestoreChapterProgress",state);
            yield return null;var calm=mask.localRotation;yield return Delay(.2f);
            Assert.That(Get<bool>(art,"HeadWrithing"),Is.False);Assert.That(Quaternion.Angle(calm,mask.localRotation),Is.LessThan(.01f));
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiTitleHeadMovesAtZeroTimeScaleWithAllBodySupportsPlanted()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;yield return null;
            var title=One("CinematicTitleScene");Assert.That(Time.timeScale,Is.Zero);
            Assert.That(Get<AnimationClip>(title,"HeadMotionClip"),Is.SameAs(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe")));
            var creature=GameObject.Find("Actual Baeksugwi source clone");Assert.That(creature,Is.Not.Null);
            var nodes=creature.GetComponentsInChildren<Transform>(true);var head=nodes.Single(t=>t.name=="Head");
            var others=nodes.Where(t=>t!=head).ToArray();var rotations=others.Select(t=>t.localRotation).ToArray();var positions=others.Select(t=>t.localPosition).ToArray();
            var first=head.localRotation;float swing=0;
            for(int phase=0;phase<16;phase++)
            {
                yield return Delay(.15f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));
                for(int i=0;i<others.Length;i++)
                {
                    Assert.That(Quaternion.Angle(rotations[i],others[i].localRotation),Is.LessThan(.01f),"Title body rotated: "+others[i].name);
                    Assert.That(Vector3.Distance(positions[i],others[i].localPosition),Is.LessThan(.00001f),"Title joint moved: "+others[i].name);
                }
            }
            Assert.That(swing,Is.GreaterThan(15));Assert.That(Get<int>(title,"PlantedLimbCount"),Is.EqualTo(12));
            Assert.That(Get<float>(title,"MaximumSupportFloorGap"),Is.LessThan(.006f));
            Call(title,"SetVisible",false);float clock=Get<float>(title,"HeadMotionTime");yield return Delay(.15f);
            Assert.That(Get<float>(title,"HeadMotionTime"),Is.EqualTo(clock));
            Call(title,"SetVisible",true);Call(shell,"ToggleReducedMotion");yield return Delay(1.1f);
            first=head.localRotation;clock=Get<float>(title,"HeadMotionTime");yield return Delay(1.1f);
            Assert.That(Get<float>(title,"HeadMotionTime"),Is.EqualTo(clock));Assert.That(Quaternion.Angle(first,head.localRotation),Is.LessThan(.01f));
        }
    }
}
