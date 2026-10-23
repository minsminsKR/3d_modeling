using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolRenewedArchitectureUsesEveryGeneratedModelWithoutChangingItsPhysicalRoute()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var keys=new[]{"wall-panel","floor-slab","ceiling-panel","classroom-door","classroom-window","pipe-manifold","pipe-run",
                "shoe-bank","school-desk","school-chair","archive-shelf","metal-cabinet","timber-cabinet","fluorescent-fixture","concrete-rubble","loose-stones"};
            foreach(string key in keys)
            {
                var source=Resources.Load<GameObject>("SchoolRenewal/"+key+"/"+key+"-visual");Assert.That(source,Is.Not.Null,key);
                Assert.That(source.GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(2),key);
                var models=centre.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="New school model — "+key).ToArray();
                Assert.That(models.Length,Is.GreaterThan(0),"Missing actual school placement: "+key);
                Assert.That(models.All(t=>t.GetComponentsInChildren<Collider>(true).Length==0),Is.True,"Visual changed collision: "+key);
                // All repeated sections must contain identical visual modules;
                // otherwise the existing position-preserving seam reveals a swap.
                foreach(Transform section in root)
                    if(section.name.StartsWith("School loop section",StringComparison.Ordinal))
                        Assert.That(section.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="New school model — "+key),Is.EqualTo(models.Length),key);
            }
            foreach(var body in centre.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="School solid wall"||t.name=="School loop physical floor"))
            {
                Assert.That(body.GetComponent<Collider>(),Is.Not.Null);
                Assert.That(body.GetComponent<Renderer>().enabled,Is.False,"Visible primitive backing survived the new generated finish");
            }
            var floor=Get<Material>(Get<object>(world,"HorrorSurfaces"),"RenewedFloor");
            var floorSource=Resources.Load<GameObject>("SchoolRenewal/floor-slab/floor-slab-visual").GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
            Assert.That(floor.GetTexture("_BaseMap"),Is.SameAs(floorSource.GetTexture("_BaseMap")));
            Assert.That(floor.GetTexture("_BumpMap"),Is.SameAs(floorSource.GetTexture("_BumpMap")));
            Assert.That(floor.GetFloat("_AuthoredFloor"),Is.EqualTo(1));Assert.That(floor.GetFloat("_WorldFloor"),Is.EqualTo(1));
            Assert.That(centre.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled&&r.sharedMaterials.Contains(floor)),Is.GreaterThan(5),
                "Wet/reflection material was not used by the generated floor batches");
            var wallMaps=Resources.Load<GameObject>("SchoolRenewal/wall-panel/wall-panel-visual").GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
            Assert.That(centre.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled&&r.sharedMaterials.Any(m=>m&&m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")==wallMaps.GetTexture("_BaseMap"))),Is.GreaterThan(10));
            var wallSource=Resources.Load<GameObject>("SchoolRenewal/wall-panel/wall-panel-visual").GetComponent<LODGroup>().GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var fullWall=centre.GetComponentsInChildren<Transform>(true).First(t=>t.name=="New school model — wall-panel"&&Mathf.Abs(t.localPosition.y-1.625f)<.001f)
                .GetComponentInChildren<MeshFilter>(true).sharedMesh;
            Assert.That(fullWall.vertexCount,Is.EqualTo(wallSource.vertexCount),"Full wall slices expanded indexed vertices into triangle corners before spatial batching");
            CollectionAssert.AreEqual(wallSource.triangles,fullWall.triangles,"Full wall topology changed during presentation-only slicing");
            CollectionAssert.AreEqual(wallSource.uv,fullWall.uv,"Full wall UV seams changed during presentation-only slicing");
            Assert.That(centre.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Raised chipped and curling painted plaster"||t.name=="Connector raised curling wall coating"),Is.False,
                "Old polygon overlays hide the newly generated wall detail");
        }
    }
}
