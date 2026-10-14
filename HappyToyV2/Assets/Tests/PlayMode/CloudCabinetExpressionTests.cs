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
            var face = actor.GetComponent(RequireType("CyclopseChaseFace"));
            yield return Wait(() => Get<bool>(actor, "AttackActive"), 8, key + " never physically reached the cabinet",
                () => "position=" + actor.transform.position + " entrance=" + entrance + " stage=" + Get<string>(actor, "CabinetStage"));
            var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
            float reach = motion ? (float)Call(motion, "BodySurfaceDistance", entrance + Vector3.up * 1.1f) : float.MaxValue;
            Assert.That(CabinetFlatDistance(actor.transform.position, entrance) < .85f || reach < .55f, Is.True);
            if (face) Assert.That(Get<float>(face, "Weight"), Is.GreaterThan(.25f), "Cyclopse did not restore its threatening face");
            yield return CabinetMustStayAliveFor(.45f);
            yield return Wait(() => Get<bool>(session, "Finished"), 3, key + " failed to resolve its witnessed physical strike");
            Assert.That(Get<bool>(session, "Escaped"), Is.False);
        }
        [UnityTest, Timeout(60000)] public IEnumerator CyclopseCabinetDetectionReraisesGrinAndRushesToKill() { yield return CabinetActorRush("Cyclopse"); }
        [UnityTest, Timeout(60000)] public IEnumerator UncatCabinetDetectionRushesToKill() { yield return CabinetActorRush("Uncat"); }
        [UnityTest, Timeout(60000)] public IEnumerator BabyCabinetDetectionRushesToKill() { yield return CabinetActorRush("Baby"); }
        [UnityTest, Timeout(60000)] public IEnumerator HwacatCabinetDetectionRushesToKill() { yield return CabinetActorRush("Hwacat"); }
        [UnityTest, Timeout(60000)] public IEnumerator MaskCabinetDetectionRushesToKill() { yield return CabinetActorRush("Mask"); }
        [UnityTest, Timeout(60000)] public IEnumerator MannequinCabinetDetectionRushesToKill() { yield return CabinetActorRush("Mannequin"); }

        [UnityTest, Timeout(60000)]
        public IEnumerator CyclopseFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin()
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled = false;
            var cabinet = SchoolCabinet(); PlacePlayer(Get<Transform>(cabinet, "outside").position);
            var actor = CabinetActor("Cyclopse", cabinet); var face = actor.GetComponent(RequireType("CyclopseChaseFace"));
            Assert.That(face, Is.Not.Null); Assert.That(Get<bool>(face, "Prepared"), Is.True);
            Set(actor, "chaseSpeed", 0f);
            yield return Wait(() => Get<float>(face, "Weight") > .95f, 2, "Genuine visual pursuit did not morph the face");
            // Remove genuine visual contact, retaining an ordinary last-seen pursuit for its finite memory.
            PlacePlayer(new Vector3(200, 5, 200), false);
            yield return Wait(() => Get<float>(face, "Weight") > .95f, 2, "Real chase did not morph the face");
            Call(shell, "Pause"); float frozen = Get<float>(face, "Weight"); yield return Delay(.2f);
            Assert.That(Get<float>(face, "Weight"), Is.EqualTo(frozen)); Call(shell, "Resume");
            Set(actor, "state", "Patrol");
            yield return Wait(() => Get<float>(face, "Weight") == 0, 2, "Lost pursuit did not restore original face");
            Set(actor, "state", "Chase"); yield return Delay(.2f); actor.gameObject.SetActive(false);
            Assert.That(Get<float>(face, "Weight"), Is.Zero);
        }
    }
}
