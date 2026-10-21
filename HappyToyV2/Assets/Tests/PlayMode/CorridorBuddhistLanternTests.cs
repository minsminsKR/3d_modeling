using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator CorridorCeilingLanternsUseGeneratedSharedLodsAndFitBelowTheActualCeiling()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            var run=Get<Component>(session,"Corridor");var art=Get<Component>(run,"Presentation");
            var root=Get<Transform>(art,"AdditionRoot");
            var lamps=Get<IReadOnlyList<Light>>(art,"Lanterns");
            var holders=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Graphics paper lantern composition").ToArray();
            Assert.That(holders.Length,Is.EqualTo(lamps.Count));Assert.That(holders.Length,Is.GreaterThanOrEqualTo(21));
            var prefab=Resources.Load<GameObject>("CorridorEntrance/buddhist-lantern-visual");
            var sourceMeshes=prefab.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh).ToArray();
            var sourceMaterials=prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials).Distinct().ToArray();
            foreach(var holder in holders)
            {
                var group=holder.GetComponentInChildren<LODGroup>(true);Assert.That(group,Is.Not.Null);
                Assert.That(group.GetLODs().Length,Is.EqualTo(2));
                CollectionAssert.AreEquivalent(sourceMeshes,holder.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh).ToArray());
                Assert.That(holder.GetComponentsInChildren<Collider>(true),Is.Empty);
                var renderers=holder.GetComponentsInChildren<MeshRenderer>(true);var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach(var material in renderer.sharedMaterials)
                    {CollectionAssert.Contains(sourceMaterials,material);Assert.That(material.GetTexture("_EmissionMap"),Is.Not.Null);}
                }
                var source=lamps.OrderBy(light=>Vector3.Distance(light.transform.position,holder.position)).First();
                Assert.That(Vector3.Distance(bounds.center,source.transform.position),Is.LessThan(.02f));
                Assert.That(bounds.size.y,Is.EqualTo(.58f).Within(.015f));
                Assert.That(bounds.max.y,Is.LessThan(2.93f),"The roof/loop penetrates ceiling joists");
                Assert.That(bounds.min.y,Is.GreaterThan(2.25f),"The hanging model enters player headroom");
                Assert.That(source.GetComponent(RequireType("ThreatFixtureFlicker")),Is.Not.Null);
            }
            Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True);
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            Call(shell,"Pause");
            int memoryCell=Get<int[]>(Get<object>(run,"Layout"),"Relics")[0];
            foreach(int cell in new[]{0,memoryCell})
            {
                var centre=(Vector3)Call(run,"CellPosition",cell);
                var fixture=holders.OrderBy(t=>Vector3.Distance(t.position,centre)).First();
                PlacePlayer(centre+Vector3.back*1.8f,false);
                camera.transform.rotation=Quaternion.LookRotation(fixture.position-camera.transform.position);
                var frame=EntranceRitualFrame(camera,1280,720);
                try
                {
                    EntranceBrightnessEvidence("corridor-buddhist-ceiling-"+(cell==0?"start":"memory-room"),frame);
                    Assert.That(EntranceRendererContribution(camera,frame,fixture.GetComponentsInChildren<Renderer>(true)),Is.GreaterThan(1000),"New ceiling lantern is absent from the real room camera");
                }
                finally{Object.Destroy(frame);}
            }
        }
    }
}
