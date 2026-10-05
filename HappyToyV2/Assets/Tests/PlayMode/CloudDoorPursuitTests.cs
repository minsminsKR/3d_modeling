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
        [UnityTest, Timeout(60000)]
        public IEnumerator InvestigationPhysicallyApproachesDoorAndOpensAfterDelay()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing navigation");
            Assert.That(NavMesh.SamplePosition(new Vector3(-4.5f, 0, 0), out var hit, .25f, NavMesh.AllAreas), Is.True);
            var start = hit.position;
            ((Behaviour)player).enabled = false; PlacePlayer(start + Vector3.up * 5, false);
            var enemy = MovingStalker(start, Vector3.right);
            Assert.That((bool)Call(enemy, "HearNoise", start + Vector3.right * 5, 8f), Is.True);
            var root = new GameObject("CloudQA pursuit door"); root.transform.position = start + Vector3.right * 2;
            var leaf = Cube("CloudQA moving door leaf", root.transform.position + Vector3.up, new Vector3(.15f, 2, 2));
            leaf.transform.SetParent(root.transform, true);
            var door = root.AddComponent(RequireType("Interactable"));
            Set(door, "kind", "Door"); Set(door, "movingLeaf", leaf.transform);
            Set(door, "openOffset", Vector3.forward * 2.5f);
            Physics.SyncTransforms();
            yield return Delay(.4f);
            Assert.That(Get<bool>(door, "IsOpen"), Is.False, "Door opened remotely without approach and push delay");
            yield return Wait(() => Get<bool>(door, "IsOpen"), 5, "Investigation never pushed physical door");
            Assert.That(Vector3.Distance(start, enemy.transform.position), Is.GreaterThan(.3f), "No physical approach");
            Object.Destroy(root);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator PursuerDoorOpeningRespectsPauseAndNeverOpensCabinets()
        {
            IsolateThreats(); Begin();
            var door = Components("Interactable").First(x => Get<object>(x, "kind").ToString() == "Door" && Get<Transform>(x, "movingLeaf"));
            Assert.That(Get<bool>(door, "IsOpen"), Is.False);
            Call(shell, "Pause");
            Assert.That((bool)Call(door, "OpenForPursuer"), Is.False);
            Assert.That(Get<bool>(door, "IsOpen"), Is.False);
            Call(shell, "Resume");
            Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
            Assert.That(Get<bool>(door, "IsOpen"), Is.True);
            var obstacle = Get<NavMeshObstacle>(door, "obstacle");
            if (obstacle) Assert.That(obstacle.enabled, Is.False);
            Assert.That((bool)Call(door, "OpenForPursuer"), Is.False, "An open door must not repeat its opening cue");
            var cabinet = Components("Interactable").First(x => Get<object>(x, "kind").ToString() == "HidingPlace");
            Assert.That((bool)Call(cabinet, "OpenForPursuer"), Is.False);
            yield return null;
        }
    }
}
