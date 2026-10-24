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
        void AssertStudioActor(Component actor, string key)
        {
            var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
            Assert.That(motion, Is.Not.Null);
            Assert.That(Get<bool>(motion, "StudioVisual"), Is.True, key + " must use the new generated model");
            Assert.That(Get<bool>(motion, "AttackRigAvailable"), Is.True, key + " must preserve semantic attack joints");
            var model = Get<Transform>(motion, "model");
            var rig = model.GetComponent(RequireType("EnemyStudioRig"));
            Assert.That(Get<string>(rig, "key"), Is.EqualTo(key));
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, "Visual replacement must not replace game collision");
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.GreaterThan(0));
            foreach (var skin in skins)
            {
                Assert.That(skin.sharedMesh.vertexCount, Is.InRange(1,80000));
                Assert.That(skin.sharedMesh.isReadable, Is.True);
                Assert.That(skin.bones.All(bone => bone), Is.True);
                foreach (var material in skin.sharedMaterials)
                    Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Resources.Load<Texture2D>("EnemyStudio/"+key+"/albedo")));
            }
            var animation = Get<Animation>(motion, "animationPlayer");
            foreach (string alias in key=="Baby" ? new[]{"patrol","chase","cry"} : new[]{"patrol","chase"})
            {
                var clip = Resources.Load<AnimationClip>("EnemyStudio/"+key+"/"+alias);
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.legacy && clip.length>.1f, Is.True);
                Assert.That(animation.GetClip(alias), Is.SameAs(clip));
            }
            Assert.That(Get<bool>(motion, key=="Cyclopse" ? "IntroPoseRigAvailable" : key=="Uncat" ? "LimbContactRigAvailable" : "RefinedVisual"), Is.True);
        }

        Component StudioSubject(string key, Vector3 at)
        {
            if (key != "Baby") return Components("StalkerBrain").Single(actor => actor.name.EndsWith("— corridor") && actor.name.Contains(key));
            // Preserve the authored school-event asset and motion coverage
            // without returning Baby to the normal corridor roster.
            return (Component)Call(RequireType("CorridorBabyProbe"),"CreateAuthoredDiagnosticFixture",session,at);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CreatureStudioCorridorKeepsTwoActorsAndPreservesAuthoredSchoolBabyClipsAndMaterials()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            Call(session,"CreateCorridor",73);Begin();Call(session,"ToggleCheatSafeMode");
            // Three memories precede the fourth-memory Lady finale. The
            // preserved Baby is mounted only by an explicit asset fixture.
            for(int i=0;i<3;i++) Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.True);
            yield return Delay(.4f);
            var corridorActors=Components("StalkerBrain").Where(actor=>actor.name.EndsWith("— corridor")).ToArray();
            Assert.That(corridorActors.Length,Is.EqualTo(2));
            Assert.That(corridorActors.Any(actor=>actor.name.Contains("Baby")),Is.False);
            var anchor=corridorActors.Single(actor=>actor.name.Contains("Cyclopse")).transform.position;
            foreach(string key in new[]{"Cyclopse","Uncat","Baby"})
            {
                var actor=StudioSubject(key,anchor);
                actor.gameObject.SetActive(true);
                yield return null;
                AssertStudioActor(actor,key);
            }
            Assert.That(Get<bool>(session,"Finished"),Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CreatureStudioPursuitHasOffBeatHeadMotionAndRestoresCalmMotionAfterPause()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            Call(session,"CreateCorridor",73);Begin();Call(session,"ToggleCheatSafeMode");
            // Exercise pursuit before Lady owns the permanent finale hold.
            for(int i=0;i<3;i++) Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.True);
            foreach(var built in Components("StalkerBrain")) ((Behaviour)built).enabled=false;
            yield return Delay(.2f);
            var anchor=Components("StalkerBrain").Single(x=>x.name.EndsWith("— corridor") && x.name.Contains("Cyclopse")).transform.position;
            Assert.That(NavMesh.SamplePosition(anchor,out var site,1,NavMesh.AllAreas),Is.True);
            foreach(string key in new[]{"Cyclopse","Uncat","Baby"})
            {
                var actor=StudioSubject(key,site.position);
                var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=false;
                actor.transform.position=site.position;actor.gameObject.SetActive(true);((Behaviour)actor).enabled=false;
                agent.enabled=true;
                Assert.That(agent.Warp(site.position),Is.True,key+" controlled fixture must use the actual corridor NavMesh");agent.isStopped=true;
                var motion=actor.GetComponent(RequireType("V1MonsterMotion"));
                var model=Get<Transform>(motion,"model");var animation=Get<Animation>(motion,"animationPlayer");
                Set(actor,"state","Chase");yield return null;yield return null;
                Assert.That(Get<string>(motion,"CurrentClip"),Is.EqualTo("chase"),key+" must switch when AI enters pursuit");
                ((Behaviour)motion).enabled=false;animation.Stop();
                var head=model.GetComponentsInChildren<Transform>().Single(node=>node.name=="mixamorig:Head");
                var rotations=new Quaternion[2];float calm=0,pursuit=0;
                for(int action=0;action<2;action++)
                {
                    var clip=animation.GetClip(action==0?"patrol":"chase");
                    clip.SampleAnimation(animation.gameObject,0);rotations[action]=head.localRotation;
                    float largest=0;
                    for(int phase=1;phase<64;phase++)
                    {
                        clip.SampleAnimation(animation.gameObject,clip.length*phase/64f);
                        largest=Mathf.Max(largest,Quaternion.Angle(rotations[action],head.localRotation));
                        Assert.That(head.localScale.x,Is.EqualTo(1).Within(.001f),key+" neck must not stretch");
                    }
                    clip.SampleAnimation(animation.gameObject,clip.length);
                    Assert.That(Quaternion.Angle(rotations[action],head.localRotation),Is.LessThan(.2f),key+" seamless head loop");
                    if(action==0)calm=largest;else pursuit=largest;
                }
                Assert.That(pursuit,Is.GreaterThan(18),key+" visible head snaps");
                Assert.That(pursuit,Is.GreaterThan(calm*3),key+" pursuit must differ from calm walk");
                ((Behaviour)motion).enabled=true;animation.Play("chase");
                yield return null;Call(shell,"Pause");
                var paused=head.localRotation;float pausedTime=animation["chase"].time;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(Quaternion.Angle(paused,head.localRotation),Is.LessThan(.05f),key+" pause must freeze head");
                Assert.That(animation["chase"].time,Is.EqualTo(pausedTime).Within(.01f));
                Call(shell,"Resume");Set(actor,"state","Patrol");yield return null;yield return null;
                Assert.That(Get<string>(motion,"CurrentClip"),Is.EqualTo(key=="Baby" ? "cry" : "patrol"),key+" restores its own stationary calm action");
                actor.gameObject.SetActive(false);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CreatureStudioSchoolKeepsDistinctHwacatAndUpdatesCyclopseNurseryWithValidSave()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            yield return RecoveryUiReady();yield return BeginLegacySchoolFixture();
            Call(session,"ToggleCheatSafeMode");
            for(int i=0;i<5;i++) Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.True);
            yield return Delay(.2f);
            var chapter=Get<Component>(session,"Chapter");
            AssertStudioActor(Get<Component>(chapter,"Cyclopse"),"Cyclopse");
            var portrait=Get<Component>(chapter,"Portrait");
            var angry=Get<Component>(portrait,"angry");
            var motion=angry.GetComponent(RequireType("V1MonsterMotion"));
            Assert.That(angry.name,Does.Contain("Hwacat"));
            Assert.That(Get<bool>(motion,"RefinedVisual"),Is.True);
            Assert.That(Get<bool>(motion,"StudioVisual"),Is.False,"School Hwacat was replaced by the Uncat Studio cat");
            Assert.That(Get<bool>(motion,"AttackRigAvailable"),Is.True);
            var model=Get<Transform>(motion,"model");
            Assert.That(model.GetComponent(RequireType("EnemyStudioRig")),Is.Null);
            var source=Resources.Load<GameObject>("EnemyRefinement/Hwacat_angry-v2");
            var expected=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin=>skin.sharedMesh).ToArray();
            Assert.That(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(skin=>expected.Contains(skin.sharedMesh)),Is.True,
                "School transformation must mount the Hwacat demonic surface");
            var animation=Get<Animation>(motion,"animationPlayer");
            Assert.That(animation.GetClip("chase"),Is.Not.Null);
            Assert.That(animation.GetClip("chase"),Is.Not.SameAs(Resources.Load<AnimationClip>("EnemyStudio/Uncat/chase")));
            var eyes=model.GetComponentInChildren(RequireType("MonsterRedEyes"));
            Assert.That(Get<string>(eyes,"ProfileKey"),Is.EqualTo("Hwacat_angry"));
            Assert.That(Components("StalkerBrain").Where(actor=>actor.name.Contains("Uncat")).All(actor=>!actor.gameObject.activeInHierarchy),Is.True,
                "Chapter 1 Uncat must remain dormant in the school");
            var normal=Get<GameObject>(portrait,"normal");
            Assert.That(normal.GetComponentInChildren<Animation>(true).GetClip("chase"),Is.Not.Null,
                "School Hwacat must retain its own dance clip");
            AssertStudioActor(Get<Component>(Get<Component>(chapter,"Nursery"),"monster"),"Baby");
            Call(shell,"Pause");Assert.DoesNotThrow(()=>Call(session,"CaptureChapterCheckpoint"));
        }
    }
}
