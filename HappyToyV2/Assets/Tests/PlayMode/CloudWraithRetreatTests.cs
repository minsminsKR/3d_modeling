using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator MaskRunnerLostPursuitRetreatsPhysicallyPausesSavesAndStillRecognizesSight()
        {
            yield return PrepareCorridorMaskRunner();
            var run = Get<Component>(session, "Corridor");
            var mask = Get<Component>(run, "Mask");
            var original = CopyAdvancedProgress(Call(mask, "CaptureChapterProgress"));
            var lastSeen = mask.transform.position;
            Set(original, "state", "Chase"); Set(original, "memory", .03f); Set(original, "target", lastSeen);
            PlacePlayer(new Vector3(200, 5, 200), false);
            Call(mask, "RestoreCorridorProgress", original);
            yield return Wait(() => Get<bool>(mask, "RunnerRetreatActive"), 3, "Lost pursuit never started a corridor respite");
            var destination = Get<Vector3>(mask, "RunnerRetreatTarget");
            Assert.That(Vector3.Distance(destination, lastSeen), Is.GreaterThanOrEqualTo(18));
            var at = mask.transform.position;
            yield return Wait(() => Vector3.Distance(at, mask.transform.position) > .5f, 5, "Retreat did not use real navigation");
            Assert.That(Get<Vector3>(mask, "RunnerRetreatTarget"), Is.EqualTo(destination), "Retreat polled the hidden player's position");
            Assert.That(mask.GetComponent<NavMeshAgent>().speed, Is.EqualTo(6.6f).Within(.001f));
            Call(shell, "Pause");
            float remaining = Get<float>(mask, "RunnerRetreatRemaining"); at = mask.transform.position;
            yield return Delay(.2f);
            Assert.That(Get<float>(mask, "RunnerRetreatRemaining"), Is.EqualTo(remaining));
            Assert.That(Vector3.Distance(mask.transform.position, at), Is.LessThan(.001f));
            PlacePlayer((Vector3)Call(run, "CellPosition", 0) + Vector3.up * .03f, false);
            var saved = Call(session, "CaptureCheckpoint");
            yield return RestoreCheckpointInFreshScene(saved);
            run = Get<Component>(session, "Corridor"); mask = Get<Component>(run, "Mask");
            Assert.That(Get<float>(mask, "RunnerRetreatRemaining"), Is.EqualTo(remaining).Within(.001f));
            Assert.That(Get<Vector3>(mask, "RunnerRetreatTarget"), Is.EqualTo(destination));
            var legacy = CopyAdvancedProgress(Call(mask, "CaptureChapterProgress"));
            Set(legacy, "runnerRetreatRemaining", 0f); Set(legacy, "runnerRetreatTarget", Vector3.zero);
            Call(mask, "RestoreCorridorProgress", legacy);
            Assert.That(Get<bool>(mask, "RunnerRetreatActive"), Is.False, "Legacy optional defaults invented a respite");
            var resume = CopyAdvancedProgress(Get<object>(saved, "mask"));
            // A supported fixture anchor gives a clear view; retreat must never
            // grant immunity when the player deliberately approaches the runner.
            Set(resume, "position", Get<Vector3>(original, "position"));
            Set(resume, "rotation", Get<Quaternion>(original, "rotation"));
            Call(mask, "RestoreCorridorProgress", resume);
            foreach (var actor in CheckpointThreats()) ((Behaviour)actor).enabled = false;
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(200, 5, 200), false); Begin();
            var ritual = Get<Component>(player, "Rituals"); Call(ritual, "Restore", 0, 0, 1);
            Assert.That((bool)Call(ritual, "UseTalisman"), Is.True);
            remaining = Get<float>(mask, "RunnerRetreatRemaining"); at = mask.transform.position;
            yield return Delay(.2f);
            Assert.That(Get<float>(mask, "RunnerRetreatRemaining"), Is.EqualTo(remaining), "Time stop consumed the respite");
            Assert.That(Vector3.Distance(mask.transform.position, at), Is.LessThan(.001f));
            Call(ritual, "EndEffect");
            CorridorMaskCameraAndBodySight(mask, 3);
            yield return Wait(() => Get<object>(mask, "State").ToString() == "Chase", 2, "Retreat suppressed actual clear sight");
            Assert.That(Get<bool>(mask, "RunnerRetreatActive"), Is.False);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator MaskRunnerSuccessfulCabinetEscapeLeavesForADistantArea()
        {
            yield return PrepareCorridorMaskRunner();
            var mask = Get<Component>(Get<Component>(session, "Corridor"), "Mask");
            var cabinet = CheckpointCabinetFixture(true);
            var entrance = Get<Transform>(cabinet, "outside").position;
            PlacePlayer(entrance, false);
            var outward = entrance - Get<Transform>(cabinet, "inside").position; outward.y = 0; outward.Normalize();
            Assert.That(NavMesh.SamplePosition(entrance + outward * 3, out var support, .5f, NavMesh.AllAreas), Is.True);
            var state = CopyAdvancedProgress(Call(mask, "CaptureChapterProgress"));
            Set(state, "position", support.position); Set(state, "rotation", Quaternion.LookRotation(entrance - support.position));
            Set(state, "target", entrance); Set(state, "state", "Chase"); Set(state, "memory", 8f); Set(state, "doorPassage", null);
            Call(mask, "RestoreCorridorProgress", state);
            Assert.That((bool)Call(mask, "CanWitnessHiding", player, entrance), Is.True);
            Set(player, "HidingRandomSample", (Func<float>)(() => 0));
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            yield return Wait(() => Get<bool>(mask, "RunnerRetreatActive"), 15, "Successful cabinet escape never sent the runner away");
            Assert.That(Vector3.Distance(Get<Vector3>(mask, "RunnerRetreatTarget"), entrance), Is.GreaterThanOrEqualTo(18));
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Get<object>(mask, "State").ToString(), Is.EqualTo("Wander"));
        }
    }
}
