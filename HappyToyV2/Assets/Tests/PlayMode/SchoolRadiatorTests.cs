using System;
using System.Collections;
using System.IO;
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
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolRadiatorsUseTheGeneratedCastIronAssemblyAndKeepTheClearAisle()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var source=Resources.Load<GameObject>("SchoolRenewal/radiator/radiator-visual");
            Assert.That(source,Is.Not.Null,"The old slat covers have no generated replacement");
            var sourceLods=source.GetComponent<LODGroup>().GetLODs();Assert.That(sourceLods.Length,Is.EqualTo(2));
            var meshes=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            Assert.That(meshes.All(m=>m&&m.isReadable&&m.uv.Length==m.vertexCount),Is.True);
            Assert.That(meshes[0].vertexCount,Is.GreaterThan(100),"The new cast-iron assembly is still a primitive cover");
            var sourceMaterials=source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
            foreach(var material in sourceMaterials)
                Assert.That(material&&material.GetTexture("_BaseMap")&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap"),Is.True,
                    "Generated paint, cast-iron detail and rust PBR maps must survive import");
            Assert.That(source.GetComponentsInChildren<Collider>(true),Is.Empty);
            var sections=root.Cast<Transform>().Where(t=>t.name.StartsWith("School loop section",StringComparison.Ordinal)).ToArray();
            Assert.That(sections.Length,Is.EqualTo(3));
            foreach(var section in sections)
            {
                var radiators=section.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="New school model — radiator")
                    .OrderBy(t=>t.localPosition.z).ToArray();
                Assert.That(radiators.Length,Is.EqualTo(10),"Both continuation sections must use the same radiator layout");
                for(int index=0;index<radiators.Length;index++)
                {
                    var radiator=radiators[index];float z=4.8f+index*3.1f;
                    Assert.That(radiator.localPosition.x,Is.EqualTo(2.035f).Within(.001f));
                    Assert.That(radiator.localPosition.y,Is.EqualTo(.326f).Within(.001f));
                    Assert.That(radiator.localPosition.z,Is.EqualTo(z).Within(.001f));
                    Assert.That(Vector3.Dot(radiator.TransformDirection(Vector3.back),section.TransformDirection(Vector3.left)),Is.GreaterThan(.999f));
                    Assert.That(radiator.GetComponentsInChildren<Collider>(true),Is.Empty,"Replacement art must not alter navigation or player collision");
                    CollectionAssert.AreEquivalent(meshes,radiator.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));
                    var placedLods=radiator.GetComponentInChildren<LODGroup>().GetLODs();Assert.That(placedLods.Length,Is.EqualTo(2));
                    for(int level=0;level<2;level++)
                        CollectionAssert.AreEqual(sourceLods[level].renderers.SelectMany(r=>r.sharedMaterials),placedLods[level].renderers.SelectMany(r=>r.sharedMaterials),
                            "A repeated radiator cloned or replaced its source PBR material");
                    var bounds=RadiatorBoundsInSection(section,radiator);
                    Assert.That(bounds.min.x,Is.EqualTo(1.92f).Within(.012f),"Radiator intrudes into the previously clear aisle");
                    Assert.That(bounds.max.x,Is.EqualTo(2.15f).Within(.012f),"End connections no longer meet the existing wall");
                    Assert.That(bounds.min.y,Is.EqualTo(.006f).Within(.012f));
                    Assert.That(bounds.max.y,Is.EqualTo(.646f).Within(.012f));
                    Assert.That(bounds.min.z,Is.EqualTo(z-.49f).Within(.012f));
                    Assert.That(bounds.max.z,Is.EqualTo(z+.49f).Within(.012f));
                    Assert.That(radiator.GetComponentsInChildren<Light>(true),Is.Empty,"Replacing a radiator must not change school lighting");
                }
            }
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="School radiator wall cover"||t.name=="School radiator vent"),Is.False,
                "Old primitive covers still obscure the cast-iron sections");
        }

        static Bounds RadiatorBoundsInSection(Transform section,Transform radiator)
        {
            var bounds=new Bounds();bool first=true;
            foreach(var filter in radiator.GetComponentsInChildren<MeshFilter>(true))
            {
                var source=filter.sharedMesh.bounds;var matrix=section.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int corner=0;corner<8;corner++)
                {
                    var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    var point=matrix.MultiplyPoint3x4(source.center+Vector3.Scale(source.extents,sign));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            Assert.That(first,Is.False,"A radiator has no imported geometry");return bounds;
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolRadiatorNormalCameraShowsTheNewCastIronSections()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflection initialization did not finish");
            var radiator=centre.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="New school model — radiator"&&Mathf.Abs(t.localPosition.z-11)<.01f);
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;
            Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            PlacePlayer(LoopOrigin+new Vector3(.10f,.03f,9.9f),false);
            camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(2.02f,.40f,11)-camera.transform.position);
            Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
            Texture2D frame=null,absent=null;
            try
            {
                frame=SchoolHorrorFrame(camera,1440,900);
                Assert.That(frame.GetPixels32().Count(c=>c.r>180&&c.b>180&&c.g<50),Is.LessThan(40),"Radiator PBR shader is missing");
                string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_RADIATOR_REVIEW_DIR");
                if(!string.IsNullOrWhiteSpace(output))
                {
                    output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                    Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                    Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"cast-iron-radiator-normal-camera.png"),frame.EncodeToPNG());
                }
                radiator.gameObject.SetActive(false);absent=SchoolHorrorFrame(camera,1440,900);
                var before=frame.GetPixels32();var after=absent.GetPixels32();int changed=0;
                for(int i=0;i<before.Length;i++)
                    if(Mathf.Abs(before[i].r-after[i].r)+Mathf.Abs(before[i].g-after[i].g)+Mathf.Abs(before[i].b-after[i].b)>18)changed++;
                Assert.That(changed,Is.GreaterThan(1000),"The new modeled radiator is unreadable from the real corridor camera");
                Debug.Log("HAPPYTOY_SCHOOL_RADIATOR_CAMERA_PASS changed="+changed);
            }
            finally{radiator.gameObject.SetActive(true);if(frame)Object.Destroy(frame);if(absent)Object.Destroy(absent);}
        }
    }
}
