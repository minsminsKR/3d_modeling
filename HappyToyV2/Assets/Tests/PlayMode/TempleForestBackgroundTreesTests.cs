using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleOuterForestUsesSharedLodsAndAccessibleTrunkColliders()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var background = world.GetComponent(RequireType("TempleForestBackgroundTrees"));
            Assert.That(Get<int>(background, "TreeCount"), Is.EqualTo(6));
            Assert.That(Get<int>(background, "PendingColliders"), Is.Zero);
            var prefab = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            var sourceLods = prefab.GetComponent<LODGroup>().GetLODs();
            for (int i = 0; i < 6; i++)
            {
                var tree = world.transform.Find("Outer forest tree " + i);
                Assert.That(tree, Is.Not.Null);
                // Each trunk is inside the old forest boundaries and away from
                // the central route, so a physical collider is required.
                Assert.That(Mathf.Abs(tree.localPosition.x), Is.InRange(10f, 20f));
                Assert.That(tree.localPosition.z, Is.InRange(-25f, 26f));
                var trunk = tree.GetComponent<CapsuleCollider>();
                Assert.That(trunk && trunk.enabled, Is.True);
                Assert.That(tree.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
                var lods = tree.GetComponent<LODGroup>().GetLODs();
                Assert.That(lods.Length, Is.EqualTo(2));
                Assert.That(lods[0].screenRelativeTransitionHeight, Is.EqualTo(.46f).Within(.0001f));
                for (int level = 0; level < 2; level++)
                {
                    var actual = lods[level].renderers[0]; var source = sourceLods[level].renderers[0];
                    Assert.That(actual.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(source.GetComponent<MeshFilter>().sharedMesh));
                    Assert.That(actual.sharedMaterial, Is.SameAs(source.sharedMaterial));
                    Assert.That(actual.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                }
            }
            Assert.Throws<System.InvalidOperationException>(() => Call(background, "Prepare", player));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleOuterForestDoesNotTrapOlderSaveAndBlocksAfterClearance()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var checkpoint = Call(world, "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            var saved = new Vector3(500 - 12.8f, .04f, 500 - 7.5f);
            Set(Get<object>(checkpoint, "player"), "position", saved);
            Assert.DoesNotThrow(() => Call(session, "CreatePrologueForCheckpoint", checkpoint));
            world = Get<Component>(session, "Prologue");
            var background = world.GetComponent(RequireType("TempleForestBackgroundTrees"));
            var tree = world.transform.Find("Outer forest tree 0");
            var trunk = tree.GetComponent<CapsuleCollider>();
            Assert.That(Vector3.Distance(player.transform.position, saved), Is.LessThan(.01f));
            Assert.That(trunk.enabled, Is.False, "New tree enclosed the restored player");
            Assert.That(Get<int>(background, "PendingColliders"), Is.GreaterThan(0));
            PlacePlayer(saved + Vector3.right * 2); Physics.SyncTransforms();
            yield return Wait(() => trunk.enabled, 3, "Tree collider did not enable after the player cleared it");
            var controller = player.GetComponent<CharacterController>();
            Physics.SyncTransforms();
            var collision = controller.Move(Vector3.left * 3);
            Assert.That((collision & CollisionFlags.Sides) != 0, Is.True, "The accessible trunk did not block the player");
        }
    }
}
