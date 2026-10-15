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
        Component CabinetActor(string key, Component cabinet)
        {
            var entrance = Get<Transform>(cabinet, "outside").position;
            var inside = Get<Transform>(cabinet, "inside").position;
            var away = entrance - inside; away.y = 0; away.Normalize();
            Assert.That(NavMesh.SamplePosition(entrance + away * 3, out var anchor, .6f, NavMesh.AllAreas), Is.True);
            var actor = key == "Mask" ? One("LanternMaskEncounter") : key == "Mannequin" ? One("WeepingAngelEncounter") :
                Components("StalkerBrain").First(item => item.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            actor.gameObject.SetActive(false); var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = false;
            actor.transform.SetPositionAndRotation(anchor.position, Quaternion.LookRotation(entrance - anchor.position));
            agent.enabled = true; ((Behaviour)actor).enabled = true; actor.gameObject.SetActive(true);
            Assert.That(agent.Warp(anchor.position), Is.True); agent.updateRotation = true;
            if (key == "Mask") { Set(actor, "activationStep", 0); Call(actor, "ConfigureCorridorRunner"); }
            else if (key == "Mannequin") Call(actor, "SkipIntroForCheat");
            else { Set(actor, "player", player); Set(actor, "state", "Chase"); }
            Physics.SyncTransforms(); return actor;
        }
        IEnumerator CabinetActorPursuing(string key, Component actor)
        {
            if (key == "Mask") yield return Wait(() => Get<object>(actor, "State").ToString() == "Chase", 5, "Mask never visually acquired the actual exposed player");
            else if (key == "Mannequin") yield return Wait(() => Get<bool>(actor, "Moving"), 5, "Released mannequin never began physical pursuit");
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator AllSixMonstersInspectSuccessfulCabinetEntryAndPhysicallyLeaveWithoutDamage()
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var entrance = Get<Transform>(cabinet, "outside").position;
            foreach (string key in new[] { "Cyclopse", "Uncat", "Baby", "Hwacat", "Mask", "Mannequin" })
            {
                PlacePlayer(entrance); var actor = CabinetActor(key, cabinet);
                yield return CabinetActorPursuing(key, actor);
                int attacks = Get<int>(actor, "AttacksStarted");
                Set(player, "HidingRandomSample", (Func<float>)(() => .2f)); Call(cabinet, "Use", player);
                Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Survived"), key);
                yield return Wait(() => Get<string>(actor, "CabinetStage") == "Inspect", 10, key + " never inspected the entrance");
                var facing = actor.transform.rotation; yield return Delay(.35f);
                Assert.That(Quaternion.Angle(facing, actor.transform.rotation), Is.GreaterThan(5), key + " never visibly looked around");
                Call(shell, "Pause"); var position = actor.transform.position; var rotation = actor.transform.rotation;
                yield return Delay(.15f); Assert.That(actor.transform.position, Is.EqualTo(position)); Assert.That(actor.transform.rotation, Is.EqualTo(rotation));
                Call(shell, "Resume");
                yield return Wait(() => Get<string>(actor, "CabinetStage") == "Complete", 12, key + " did not finish retreating");
                Assert.That(CabinetFlatDistance(actor.transform.position, entrance), Is.GreaterThan(2.5f), key);
                Assert.That(Get<int>(actor, "AttacksStarted"), Is.EqualTo(attacks), key);
                Assert.That(Get<bool>(session, "Finished"), Is.False, key);
                actor.gameObject.SetActive(false); Physics.SyncTransforms(); Call(cabinet, "Use", player);
                Assert.That(Get<bool>(player, "Hidden"), Is.False, key);
            }
        }
        IEnumerator CabinetActorRush(string key)
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); var entrance = Get<Transform>(cabinet, "outside").position;
            PlacePlayer(entrance); var actor = CabinetActor(key, cabinet); yield return CabinetActorPursuing(key, actor);
            Set(player, "HidingRandomSample", (Func<float>)(() => .95f)); Call(cabinet, "Use", player);
            Assert.That(Get<object>(player, "HidingOutcome").ToString(), Is.EqualTo("Defeated"));
            yield return Wait(() => Get<string>(actor, "CabinetStage") == "Inspect", 10, key + " skipped the initial calm scan");
            Assert.That(Get<bool>(actor, "CabinetAggressive"), Is.False);
            yield return Wait(() => Get<string>(actor, "CabinetStage") == "Rush", 5, key + " never rushed after detection");
            Assert.That(Get<bool>(actor, "CabinetAggressive"), Is.True);
            var face = actor.GetComponent(RequireType("EnemyChaseFace"));
            yield return Wait(() => Get<bool>(actor, "AttackActive"), 8, key + " never physically reached the cabinet",
                () => "position=" + actor.transform.position + " entrance=" + entrance + " stage=" + Get<string>(actor, "CabinetStage"));
            var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
            float reach = motion ? (float)Call(motion, "BodySurfaceDistance", entrance + Vector3.up * 1.1f) : float.MaxValue;
            Assert.That(CabinetFlatDistance(actor.transform.position, entrance) < .85f || reach < .55f, Is.True);
            if (face) Assert.That(Get<float>(face, "Weight"), Is.GreaterThan(.25f), key + " did not restore its threatening face");
            if (key == "Cyclopse" || key == "Uncat")
            {
                Assert.That(Get<float>(face, "CabinetWeight"), Is.GreaterThan(.1f), "Detected rush never opened the authored cabinet jaw");
                var mouth = actor.GetComponentsInChildren<MeshFilter>().Single(m => m.name == "Owned cabinet teeth gums and mouth cavity");
                Assert.That(mouth.transform.parent.name, Is.EqualTo("mixamorig:Head"));
                Assert.That(mouth.sharedMesh.subMeshCount, Is.EqualTo(5));
                Assert.That(mouth.sharedMesh.vertexCount, Is.GreaterThan(2000), "Expected actual tooth/gum/cavity geometry");
                if(key=="Uncat")Assert.That(actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Owned chase mouth interior").gameObject.activeSelf,Is.False,"Legacy mouth cap must not cover the dropped cabinet jaw");
            }
            yield return CabinetMustStayAliveFor(.45f);
            yield return Wait(() => Get<bool>(session, "Finished"), 3, key + " failed to resolve its witnessed physical strike");
            Assert.That(Get<bool>(session, "Escaped"), Is.False);
            if (key == "Cyclopse" || key == "Uncat")
            {
                Assert.That(Get<float>(face, "CabinetWeight"), Is.GreaterThan(.9f), "Defeat did not retain the fully gaping face");
                float held=Get<float>(face,"CabinetWeight");yield return Delay(.15f);
                Assert.That(Get<float>(face,"CabinetWeight"),Is.EqualTo(held));
            }
        }
        [UnityTest, Timeout(60000)] public IEnumerator CyclopseCabinetDetectionReraisesGrinAndRushesToKill() { yield return CabinetActorRush("Cyclopse"); }
        [UnityTest, Timeout(60000)] public IEnumerator UncatCabinetDetectionRushesToKill() { yield return CabinetActorRush("Uncat"); }
        [UnityTest, Timeout(60000)] public IEnumerator BabyCabinetDetectionRushesToKill() { yield return CabinetActorRush("Baby"); }
        [UnityTest, Timeout(60000)] public IEnumerator HwacatCabinetDetectionRushesToKill() { yield return CabinetActorRush("Hwacat"); }
        [UnityTest, Timeout(60000)] public IEnumerator MaskCabinetDetectionRushesToKill() { yield return CabinetActorRush("Mask"); }
        [UnityTest, Timeout(60000)] public IEnumerator MannequinCabinetDetectionRushesToKill() { yield return CabinetActorRush("Mannequin"); }

        [UnityTest, Timeout(60000)]
        public IEnumerator CyclopseFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin()
        { yield return FaceFollowsChasePauseCalmAndDisable("Cyclopse"); }
        [UnityTest, Timeout(60000)]
        public IEnumerator UncatFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin()
        { yield return FaceFollowsChasePauseCalmAndDisable("Uncat"); }
        [UnityTest, Timeout(60000)]
        public IEnumerator BabyFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin()
        { yield return FaceFollowsChasePauseCalmAndDisable("Baby"); }
        IEnumerator FaceFollowsChasePauseCalmAndDisable(string key)
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); PlacePlayer(Get<Transform>(cabinet, "outside").position);
            var originalPrefab = Resources.Load<GameObject>("EnemyStudio/" + key + "/" + key + "Runtime");
            var sharedMesh = originalPrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            var originalVertices = sharedMesh.vertices; var originalUv = sharedMesh.uv;
            var actor = CabinetActor(key, cabinet); var face = actor.GetComponent(RequireType("EnemyChaseFace"));
            Assert.That(face, Is.Not.Null); Assert.That(Get<bool>(face, "Prepared"), Is.True);
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin.sharedMesh, Is.Not.SameAs(sharedMesh));
            CollectionAssert.AreEqual(originalUv, skin.sharedMesh.uv, key + " replaced the body UV atlas");
            Set(actor, "chaseSpeed", 0f);
            yield return Wait(() => Get<float>(face, "Weight") > .95f, 2, "Genuine visual pursuit did not morph the face");
            Assert.That(skin.sharedMaterials.All(m => m.shader.name == "HappyToy/CyclopseChaseFace"), Is.True);
            Assert.That(skin.sharedMaterials.All(m => m.GetTexture("_ChaseFace")), Is.True);
            AssertExpressionEyes(actor,true);
            Assert.That(Get<float>(face,"CabinetWeight"),Is.Zero,"Ordinary pursuit must retain its existing grin");
            var eye=actor.GetComponentInChildren(RequireType("MonsterRedEyes")) as Component;
            Call(eye,"SetEmissionEnabled",false);AssertExpressionEyes(actor,false);
            yield return Delay(.1f);AssertExpressionEyes(actor,false);
            Call(eye,"SetEmissionEnabled",true);AssertExpressionEyes(actor,true);
            // Remove genuine visual contact, retaining an ordinary last-seen pursuit for its finite memory.
            PlacePlayer(new Vector3(200, 5, 200), false);
            yield return Wait(() => Get<float>(face, "Weight") > .95f, 2, "Real chase did not morph the face");
            Call(shell, "Pause"); float frozen = Get<float>(face, "Weight"); yield return Delay(.2f);
            Assert.That(Get<float>(face, "Weight"), Is.EqualTo(frozen));AssertExpressionEyes(actor,true); Call(shell, "Resume");
            Set(actor, "state", "Patrol");
            yield return Wait(() => Get<float>(face, "Weight") == 0, 2, "Lost pursuit did not restore original face");
            Assert.That(skin.sharedMaterials.All(m => m.shader.name != "HappyToy/CyclopseChaseFace"), Is.True, "Calm did not restore original lit materials");
            Set(actor, "state", "Chase"); yield return Delay(.2f); actor.gameObject.SetActive(false);
            Assert.That(Get<float>(face, "Weight"), Is.Zero);
            Assert.That(Get<float>(face,"CabinetWeight"),Is.Zero);
            CollectionAssert.AreEqual(originalVertices, sharedMesh.vertices, key + " edited the shared source vertices");
            CollectionAssert.AreEqual(originalUv, sharedMesh.uv, key + " edited the original UV atlas");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CyclopseCabinetGapePausesAndRestoresAfterEscape() { yield return CabinetGapeRestoresAfterEscape("Cyclopse"); }
        [UnityTest, Timeout(60000)]
        public IEnumerator UncatCabinetGapePausesAndRestoresAfterEscape() { yield return CabinetGapeRestoresAfterEscape("Uncat"); }
        IEnumerator CabinetGapeRestoresAfterEscape(string key)
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled=false;
            var cabinet=SchoolCabinet();PlacePlayer(Get<Transform>(cabinet,"outside").position);
            var actor=CabinetActor(key,cabinet);
            Set(player,"HidingRandomSample",(Func<float>)(()=>.95f));Call(cabinet,"Use",player);
            var face=actor.GetComponent(RequireType("EnemyChaseFace"));
            yield return Wait(()=>Get<float>(face,"CabinetWeight")>.2f,12,key+" never began its cabinet gape");
            Call(shell,"Pause");float held=Get<float>(face,"CabinetWeight");yield return Delay(.2f);
            Assert.That(Get<float>(face,"CabinetWeight"),Is.EqualTo(held));Call(shell,"Resume");
            Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.False);
            PlacePlayer(new Vector3(200,5,200),false);
            yield return Wait(()=>Get<float>(face,"CabinetWeight")==0,2,"Escaping did not close the cabinet-only mouth");
            Assert.That(Get<bool>(session,"Finished"),Is.False);
            Assert.That(actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Owned cabinet ")).All(t=>!t.gameObject.activeSelf),Is.True);
        }
    }
}
