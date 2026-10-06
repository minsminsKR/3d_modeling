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
        static object FeedbackCall(object target, string method, params object[] args)
        {
            var member = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Missing controlled probe " + method);
            return member.Invoke(target, args);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator FeedbackSightStartsChaseInOneUpdateAndCoverNeverDoes()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing navigation");
            var origin = MainCorridorPoint();
            ((Behaviour)player).enabled = false;
            PlacePlayer(origin + Vector3.right * 4, false);
            var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var wall = Cube("CloudQA feedback sight wall", origin + Vector3.right * 2 + Vector3.up * 1.5f,
                new Vector3(.2f, 3, 2)); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.False);
            FeedbackCall(enemy, "Update");
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Patrol"));
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True);
            FeedbackCall(enemy, "Update");
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Chase"));
            Assert.That(Get<Vector3>(enemy, "LastKnownPosition"), Is.EqualTo(player.transform.position));
            Assert.That(enemy.GetComponent<NavMeshAgent>().hasPath, Is.True, "Chase waited for old repath timer");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator FeedbackFootstepsInvestigateSnapshotWithWallAndDoorAttenuation()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing navigation");
            var origin = MainCorridorPoint();
            ((Behaviour)player).enabled = false; PlacePlayer(origin + Vector3.right * 4, false);
            var hearingPath = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(origin, player.transform.position, NavMesh.AllAreas, hearingPath), Is.True);
            Assert.That(hearingPath.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Fixture requires real reachable floor");
            var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var wall = Cube("CloudQA feedback hearing wall", origin + Vector3.right * 2 + Vector3.up * 1.5f,
                new Vector3(.2f, 3, 2)); Physics.SyncTransforms();
            FeedbackCall(enemy, "HearFootstep", player.transform.position, 10f);
            Assert.That(Get<int>(enemy, "FootstepNoisesAccepted"), Is.Zero, "Wall did not reduce the audible radius");
            var door = wall.AddComponent(RequireType("Interactable")); Set(door, "kind", "Door");
            FeedbackCall(enemy, "HearFootstep", player.transform.position, 10f);
            Assert.That(Get<int>(enemy, "FootstepNoisesAccepted"), Is.EqualTo(1), "Nearby sprint behind a door was inaudible");
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Investigate"));
            var evidence = Get<Vector3>(enemy, "LastKnownPosition");
            PlacePlayer(origin + Vector3.right * 6, false);
            FeedbackCall(enemy, "Update");
            Assert.That(Get<Vector3>(enemy, "LastKnownPosition"), Is.EqualTo(evidence), "Unseen motion updated a sound snapshot");
            Assert.That(Get<float>(enemy, "Awareness"), Is.Zero, "Sound acquired visual recognition");
            Call(shell, "Pause"); FeedbackCall(enemy, "HearFootstep", origin, 10f);
            Assert.That(Get<int>(enemy, "FootstepNoisesAccepted"), Is.EqualTo(1));
            Call(shell, "Resume");
            wall.SetActive(false); PlacePlayer(origin + Vector3.right * 4, false);
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.right); Physics.SyncTransforms();
            FeedbackCall(enemy, "Update");
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Chase"));
        }
    }
}
