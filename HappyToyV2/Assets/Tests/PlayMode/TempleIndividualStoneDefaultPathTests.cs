using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // The PlayMode asmdef cannot reference the game assembly directly.
        // Match the existing tests' RuntimeAccess bridge and keep the game
        // assembly dependency one-way.
        static object DefaultStoneSettings(int requested, int minimum, int attempts, float height, bool allowUnderfill)
        {
            var settingsType = RequireType("TempleIndividualStonePath").GetNestedType("Settings");
            Assert.That(settingsType, Is.Not.Null);
            return Activator.CreateInstance(settingsType, new object[] { requested, minimum, attempts, height, allowUnderfill, 43093u });
        }

        // Draft for the case where presentation installs the component by
        // default. Fallback is an integration failure in this particular test;
        // production itself must keep the old path if construction fails.
        [UnityTest, Timeout(300000)]
        public IEnumerator DefaultIndividualStonePathPreservesGameplayAndRestoresOnRelease()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var path=world.transform.Find("Stone approach"); var ground=world.transform.Find("Forest ground");
            var relief=world.transform.Find("Authored forest stone approach"); var bed=world.transform.Find("Recessed forest stone bed");
            Assert.That(path && ground && relief && bed, Is.True);
            var reference=path.GetComponent<Renderer>(); var initialBounds=reference.bounds;
            Assert.That(reference.enabled && reference.forceRenderingOff, Is.True);
            var component=world.GetComponent(RequireType("TempleIndividualStonePath")) as Behaviour;
            Assert.That(component && Get<bool>(component, "IsPrepared"), Is.True, "The default path candidate failed or was not installed");
            var originalResult=Get<object>(component, "LastResult");
            Assert.That(Get<bool>(originalResult,"Installed"), Is.True, Get<string>(originalResult,"Reason"));
            Assert.That(Get<int>(originalResult,"Requested"), Is.EqualTo(180));
            Assert.That(Get<int>(originalResult,"Actual"), Is.InRange(150,180), "Do not silently accept a smaller layout");
            Assert.That(Get<bool>(originalResult,"Underfilled"), Is.EqualTo(Get<int>(originalResult,"Actual") < Get<int>(originalResult,"Requested")));
            TestContext.Out.WriteLine("Default individual path: requested=" + Get<int>(originalResult,"Requested") + ", actual=" +
                Get<int>(originalResult,"Actual") + ", attempts=" + Get<int>(originalResult,"Attempts") + ", underfilled=" + Get<bool>(originalResult,"Underfilled"));
            var originals=relief.GetComponentsInChildren<Renderer>(true).Concat(bed.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(originals.Length, Is.GreaterThan(1));
            Assert.That(originals.All(r => r.forceRenderingOff), Is.True, "Both old decorative layers must be hidden");
            var colliders=world.GetComponentsInChildren<Collider>(true);
            var colliderEnabled=colliders.Select(c => c.enabled).ToArray();
            var colliderBounds=colliders.Select(c => c.bounds).ToArray();
            bool wasStone=(bool)Call(world, "IsStoneApproach", world.transform.TransformPoint(new Vector3(0,0,-5)));
            bool wasOutside=(bool)Call(world, "IsStoneApproach", world.transform.TransformPoint(new Vector3(3,0,-5)));
            Assert.That(wasStone, Is.True); Assert.That(wasOutside, Is.False);
            var models=new[] {
                Resources.Load<GameObject>("TempleForest/temple-stepping-stone-reference-20261107/visual"),
                Resources.Load<GameObject>("TempleForest/temple-stepping-stone-long-20261108/visual") };
            Assert.That(models.All(p => p), Is.True);
            var meshes=models.SelectMany(p => p.GetComponent<LODGroup>().GetLODs()).SelectMany(l => l.renderers)
                .Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var materials=models.SelectMany(p => p.GetComponent<LODGroup>().GetLODs()).SelectMany(l => l.renderers)
                .Select(r => r.sharedMaterial).ToArray();
            var installed=world.transform.Find("Individual forest stone approach");
            Assert.That(installed && installed.gameObject.activeSelf, Is.True);
            Assert.That(installed.childCount, Is.EqualTo(Get<int>(originalResult,"Actual")));
            Assert.That(installed.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(installed.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var groups=installed.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(Get<int>(originalResult,"Actual")));
            Assert.That(groups.All(g => g.lodCount == 3), Is.True);
            Assert.That(installed.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == path.gameObject.layer), Is.True);
            var renderers=groups.SelectMany(g => g.GetLODs()).SelectMany(l => l.renderers).ToArray();
            Assert.That(renderers.All(r => meshes.Contains(r.GetComponent<MeshFilter>().sharedMesh)), Is.True);
            Assert.That(renderers.All(r => materials.Contains(r.sharedMaterial)), Is.True);
            var walkBounds=reference.bounds; float groundTop=ground.GetComponent<Renderer>().bounds.max.y;
            Assert.That(renderers.All(r => r.bounds.min.x >= walkBounds.min.x-.001f && r.bounds.max.x <= walkBounds.max.x+.001f &&
                r.bounds.min.z >= walkBounds.min.z-.001f && r.bounds.max.z <= walkBounds.max.z+.001f &&
                r.bounds.max.y <= groundTop+.0225f), Is.True, "All actual LOD bounds must stay within the reference and reviewed top");
            var settings=DefaultStoneSettings(180,150,40000,.022f,true);
            try
            {
                Assert.Throws<InvalidOperationException>(() => Call(component, "Prepare", settings, models));
                Call(component, "Release");
                Assert.That(Get<bool>(component, "IsPrepared"), Is.False);
                Assert.That(originals.All(r => !r.forceRenderingOff), Is.True, "Original default renderer state was not restored");
                Assert.That(!installed.gameObject.activeSelf, Is.True, "Retired candidate remains visible before deferred destruction");
                Assert.That(meshes.All(m => m) && materials.All(m => m), Is.True);
                Assert.That(reference.enabled && reference.forceRenderingOff, Is.True);
                Assert.That(Vector3.Distance(reference.bounds.center, initialBounds.center), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(reference.bounds.size, initialBounds.size), Is.LessThan(.0001f));
                Assert.That((bool)Call(world,"IsStoneApproach",world.transform.TransformPoint(new Vector3(0,0,-5))), Is.EqualTo(wasStone));
                Assert.That((bool)Call(world,"IsStoneApproach",world.transform.TransformPoint(new Vector3(3,0,-5))), Is.EqualTo(wasOutside));
                Physics.SyncTransforms();
                CollectionAssert.AreEquivalent(colliders, world.GetComponentsInChildren<Collider>(true));
                for (int i=0; i<colliders.Length; i++)
                {
                    Assert.That(colliders[i].enabled, Is.EqualTo(colliderEnabled[i]));
                    Assert.That(Vector3.Distance(colliders[i].bounds.center, colliderBounds[i].center), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(colliders[i].bounds.size, colliderBounds[i].size), Is.LessThan(.0001f));
                }
                // Force-off state that existed before installation must survive
                // errors, disable, destruction and re-entry, not be overwritten.
                originals[0].forceRenderingOff=true;
                var prior=originals.Select(r => r.forceRenderingOff).ToArray();
                var missing=Call(component, "Prepare", settings, new[] { models[0], (GameObject)null });
                Assert.That(Get<bool>(missing,"Installed"), Is.False); Assert.That(Get<bool>(component, "IsPrepared"), Is.False);
                CollectionAssert.AreEqual(prior, originals.Select(r => r.forceRenderingOff).ToArray());
                var shortage=Call(component, "Prepare", DefaultStoneSettings(1000,1000,1000,.022f,false),models);
                Assert.That(Get<bool>(shortage,"Installed"), Is.False); Assert.That(Get<bool>(shortage,"Underfilled"), Is.True);
                Assert.That(Get<int>(shortage,"Actual"), Is.LessThan(Get<int>(shortage,"Requested")));
                CollectionAssert.AreEqual(prior, originals.Select(r => r.forceRenderingOff).ToArray());
                var accepted=Call(component, "Prepare", DefaultStoneSettings(1000,1,1000,.022f,true),models);
                Assert.That(Get<bool>(accepted,"Installed"), Is.True, Get<string>(accepted,"Reason"));
                Assert.That(Get<bool>(accepted,"Underfilled"), Is.True); Assert.That(Get<int>(accepted,"Actual"), Is.EqualTo(Get<int>(shortage,"Actual")));
                Call(component, "Release"); CollectionAssert.AreEqual(prior, originals.Select(r => r.forceRenderingOff).ToArray());
                var retry=Call(component, "Prepare", settings,models);
                Assert.That(Get<bool>(retry,"Installed"), Is.True, Get<string>(retry,"Reason"));
                Assert.That(Get<int>(retry,"Actual"), Is.InRange(150,180));
                Assert.That(world.transform.Find("Individual forest stone approach").gameObject.activeSelf, Is.True,
                    "Same-frame re-entry must not find the retired candidate");
                component.enabled=false;
                Assert.That(Get<bool>(component, "IsPrepared"), Is.False);
                CollectionAssert.AreEqual(prior, originals.Select(r => r.forceRenderingOff).ToArray());
                component.enabled=true;
                var again=Call(component, "Prepare", settings,models);
                Assert.That(Get<bool>(again,"Installed"), Is.True, Get<string>(again,"Reason"));
                Object.Destroy(component); yield return null; yield return null;
                Assert.That(world.transform.Find("Individual forest stone approach"), Is.Null);
                CollectionAssert.AreEqual(prior, originals.Select(r => r.forceRenderingOff).ToArray());
                Assert.That(meshes.All(m => m) && materials.All(m => m), Is.True);
            }
            finally
            {
                if (component) Call(component, "Release");
                if (originals.Length>0 && originals[0]) originals[0].forceRenderingOff=false;
            }
        }
    }
}
