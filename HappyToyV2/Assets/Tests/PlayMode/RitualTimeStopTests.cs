using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator TalismanFreezesAllSixActualAttackersAndPreservesTheirPoseAndNavigation()
        {
            IsolateThreats(); Begin(); ((Behaviour)player).enabled = false;
            var ritual = Get<Component>(player, "Rituals");
            var cabinet = SchoolCabinet();
            var entrance = Get<Transform>(cabinet, "outside").position;
            foreach (string key in new[] { "Cyclopse", "Uncat", "Baby", "Hwacat", "Mask", "Mannequin" })
            {
                PlacePlayer(entrance);
                var actor = CabinetActor(key, cabinet);
                Get<Camera>(player, "eyes").transform.rotation =
                    Quaternion.LookRotation(player.transform.position - actor.transform.position);
                yield return CabinetActorPursuing(key, actor);
                yield return Wait(() => Get<bool>(actor, "AttackActive"), 10,
                    key + " never reached its actual physical attack warning");

                Call(ritual, "Restore", 1, 4, 0);
                Assert.That((bool)Call(ritual, "CollectFragment"), Is.True);
                Assert.That(Get<int>(ritual, "Fragments"), Is.Zero);
                Assert.That(Get<int>(ritual, "Talismans"), Is.EqualTo(1));
                var agent = actor.GetComponent<NavMeshAgent>();
                bool rotation = agent.updateRotation;
                var at = actor.transform.position; var facing = actor.transform.rotation;
                var pose = actor.GetComponentsInChildren<Transform>(true)
                    .Select(part => (part, part.localPosition, part.localRotation, part.localScale)).ToArray();
                var animations = actor.GetComponentsInChildren<Animation>(true)
                    .Select(animation => (animation, animation.enabled)).ToArray();
                float windup = Get<float>(actor, "AttackWindup");
                int attacks = Get<int>(actor, "AttacksStarted");
                Assert.That((bool)Call(ritual, "UseTalisman"), Is.True, key);
                Assert.That(Get<int>(ritual, "Talismans"), Is.Zero);
                Assert.That((bool)Call(ritual, "UseTalisman"), Is.False, "An active burn must not consume another talisman");
                yield return Delay(.25f);
                Assert.That(Get<float>(actor, "AttackWindup"), Is.EqualTo(windup), key);
                Assert.That(Get<int>(actor, "AttacksStarted"), Is.EqualTo(attacks), key);
                Assert.That(Vector3.Distance(actor.transform.position, at), Is.LessThan(.0001f), key);
                Assert.That(Quaternion.Angle(actor.transform.rotation, facing), Is.LessThan(.005f), key);
                foreach (var part in pose)
                {
                    Assert.That(Vector3.Distance(part.part.localPosition, part.localPosition), Is.LessThan(.00001f), key + "/" + part.part.name);
                    Assert.That(Quaternion.Angle(part.part.localRotation, part.localRotation), Is.LessThan(.005f), key + "/" + part.part.name);
                    Assert.That(part.part.localScale, Is.EqualTo(part.localScale), key);
                }
                Assert.That(animations.All(entry => !entry.animation.enabled), Is.True, key);
                Assert.That(agent.isStopped && !agent.updateRotation, Is.True, key);
                if (key != "Mannequin")
                    Assert.That((bool)Call(actor, "HearNoise", entrance, 3f), Is.False, key + " accepted a sound during frozen time");

                Call(shell, "Pause");
                float remaining = Get<float>(ritual, "Remaining");
                yield return Delay(.15f);
                Assert.That(Get<float>(ritual, "Remaining"), Is.EqualTo(remaining));
                Call(shell, "Resume");
                // Contact, an explicit defeat request and a new cabinet entry are all harmless.
                PlacePlayer(at, false);
                Assert.That((bool)Call(session, "TryDefeat", key, "Frozen contact"), Is.False);
                Set(player, "HidingRandomSample", (Func<float>)(() => .95f));
                Call(cabinet, "Use", player);
                Assert.That(Get<bool>(player, "Hidden"), Is.True);
                Assert.That((bool)Call(actor, "CanWitnessHiding", player, entrance), Is.False);
                yield return Delay(.1f);
                Assert.That(Get<bool>(session, "Finished"), Is.False, key);
                Call(cabinet, "Use", player);
                PlacePlayer(new Vector3(200, 5, 200), false);
                Call(ritual, "EndEffect");
                Assert.That(agent.updateRotation, Is.EqualTo(rotation), key + " leaked its rotation lock");
                foreach (var entry in animations) Assert.That(entry.animation.enabled, Is.EqualTo(entry.enabled), key);
                Assert.That(Shader.GetGlobalFloat("_RitualMonochrome"), Is.Zero);
                actor.gameObject.SetActive(false);
                yield return null;
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TalismanBurnsForTenGameplaySecondsWhilePlayerMovesAndPausePreservesItsClock()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats();
            yield return null; yield return null;
            Cube("Ritual clock movement fixture", new Vector3(200, 4.8f, 200), new Vector3(8, .4f, 8));
            PlacePlayer(new Vector3(200, 5.05f, 200));
            Get<Camera>(player, "eyes").transform.localRotation = Quaternion.identity;
            var ritual = Get<Component>(player, "Rituals");
            Call(ritual, "Restore", 1, 4, 0);
            Assert.That((bool)Call(ritual, "CollectFragment"), Is.True);
            Assert.That((bool)Call(ritual, "UseTalisman"), Is.True);
            float started = Time.time;
            Assert.That(Time.timeScale, Is.EqualTo(1), "Only enemy clocks should stop");
            var volume = ritual.GetComponentsInChildren<Volume>(true).Single(v => v.priority == 1000);
            Assert.That(volume.sharedProfile.TryGet<ColorAdjustments>(out var colour), Is.True);
            Assert.That(colour.saturation.value, Is.EqualTo(-100));
            var from = player.transform.position;
            yield return KeysObserved(Key.W);
            yield return Delay(.3f);
            Keys(); yield return null;
            Assert.That(Vector3.Distance(player.transform.position, from), Is.GreaterThan(.35f),
                "The player was immobilized with the enemies");
            Call(shell, "Pause");
            float remaining = Get<float>(ritual, "Remaining");
            float progress = Get<float>(ritual, "BurnedFraction");
            yield return Delay(.3f);
            Assert.That(Get<float>(ritual, "Remaining"), Is.EqualTo(remaining));
            Assert.That(Get<float>(ritual, "BurnedFraction"), Is.EqualTo(progress));
            Assert.That((bool)Call(ritual, "UseTalisman"), Is.False);
            Call(shell, "Resume");
            yield return Wait(() => !Get<bool>(ritual, "Burning"), 12, "The talisman never finished burning");
            Assert.That(Time.time - started, Is.InRange(9.98f, 10.35f));
            Assert.That(Get<float>(ritual, "BurnedFraction"), Is.EqualTo(1));
            Assert.That(Shader.GetGlobalFloat("_RitualMonochrome"), Is.Zero);
            Assert.That(Get<int>(ritual.GetComponent(RequireType("EnemyTimeStop")), "FrozenActorCount"), Is.Zero);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TalismanAlsoFreezesTheSchoolHwacatDanceAndResumesItsNestedReveal()
        {
            BeginLegacyChapterFixture(); IsolateThreats();
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(200, 5, 200), false);
            var reveal = One("V1HwacatEvent");
            Call(reveal, "PrepareChapter");
            Call(reveal, "StartChapterReveal");
            yield return Wait(() => Get<string>(reveal, "Phase") == "dance", 10, "The authored Hwacat dance never began");
            var normal = Get<GameObject>(reveal, "normal");
            Assert.That(normal.activeInHierarchy, Is.True);
            var animation = normal.GetComponentInChildren<Animation>();
            Assert.That(animation && animation.enabled, Is.True);
            var ritual = Get<Component>(player, "Rituals");
            Call(ritual, "Restore", 1, 0, 1);
            float elapsed = Get<float>(reveal, "RevealElapsed");
            float clipTime = animation["chase"].time;
            var pose = normal.GetComponentsInChildren<Transform>(true)
                .Select(part => (part, part.localPosition, part.localRotation)).ToArray();
            Assert.That((bool)Call(ritual, "UseTalisman"), Is.True);
            yield return Delay(.35f);
            Assert.That(Get<string>(reveal, "Phase"), Is.EqualTo("dance"));
            Assert.That(Get<float>(reveal, "RevealElapsed"), Is.EqualTo(elapsed));
            Assert.That(animation["chase"].time, Is.EqualTo(clipTime));
            foreach (var part in pose)
            {
                Assert.That(part.part.localPosition, Is.EqualTo(part.localPosition));
                Assert.That(Quaternion.Angle(part.part.localRotation, part.localRotation), Is.LessThan(.005f));
            }
            Call(ritual, "EndEffect");
            Assert.That(animation.enabled, Is.True);
            yield return Wait(() => Get<bool>(reveal, "Completed"), 5, "The nested Hwacat reveal failed to resume");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
        }
    }
}

