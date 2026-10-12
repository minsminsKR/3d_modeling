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

        [UnityTest, Timeout(120000)]
        public IEnumerator CreatureStudioAllThreeReplaceActualCorridorActorsWithOwnClipsAndMaterials()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            Call(session,"CreateCorridor",73);Begin();Call(session,"ToggleCheatSafeMode");
            for(int i=0;i<5;i++) Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.True);
            yield return Delay(.4f);
            foreach(string key in new[]{"Cyclopse","Uncat","Baby"})
            {
                var actor=Components("StalkerBrain").Single(x=>x.name.EndsWith("— corridor") && x.name.Contains(key));
                actor.gameObject.SetActive(true);
                yield return null;
                AssertStudioActor(actor,key);
            }
            Assert.That(Get<bool>(session,"Finished"),Is.False);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CreatureStudioSchoolKeepsDistinctHwacatAndUpdatesCyclopseNurseryWithValidSave()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            yield return RecoveryUiReady();yield return RecoveryClick("begin-school");
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
