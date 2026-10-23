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
        public IEnumerator SchoolRenewedFurnitureKeepsGeneratedPbrAndFacesTheTeachingWall()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var centre=Get<Transform>(world,"Centre");var music=Get<Transform>(world,"MusicRoom");
            foreach(string key in new[]{"school-desk","school-chair","archive-shelf","metal-cabinet","timber-cabinet"})
            {
                var source=Resources.Load<GameObject>("SchoolRenewal/"+key+"/"+key+"-visual");
                Assert.That(source,Is.Not.Null,"Generated furniture prefab missing: "+key);
                Assert.That(source.GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(2));
                Assert.That(source.GetComponentsInChildren<Collider>(true),Is.Empty);
                var meshes=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
                Assert.That(meshes.All(m=>m&&m.isReadable&&m.uv.Length==m.vertexCount),Is.True);
                Assert.That(centre.GetComponentsInChildren<MeshFilter>(true).Any(f=>meshes.Contains(f.sharedMesh)),Is.True,
                    "New furniture was supplied but not actually assembled in the school: "+key);
                foreach(var material in source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                    Assert.That(material&&material.GetTexture("_BaseMap")&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap"),Is.True,
                        "Generated source PBR/detail was replaced by a plain surface: "+key);
            }
            var chairs=centre.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Unoccupied study chair"&&
                centre.InverseTransformPoint(t.position).z>14&&centre.InverseTransformPoint(t.position).z<23).ToArray();
            Assert.That(chairs.Length,Is.EqualTo(9));
            var north=centre.TransformDirection(Vector3.forward);
            foreach(var chair in chairs)
            {
                var model=chair.Find("New school model — school-chair");Assert.That(model,Is.Not.Null);
                Assert.That(Vector3.Dot(model.TransformDirection(Vector3.back),north),Is.GreaterThan(.85f),
                    "New student chair is facing away from the teaching wall");
            }
            var desks=centre.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Retained Korean classroom desk").ToArray();
            Assert.That(desks.Length,Is.EqualTo(9));
            foreach(var desk in desks)
            {
                var model=desk.Find("New school model — school-desk");Assert.That(model,Is.Not.Null);
                Assert.That(Vector3.Dot(model.TransformDirection(Vector3.back),north),Is.LessThan(-.9f),
                    "The desk book opening no longer faces its student chair");
                Assert.That(desk.GetComponentsInChildren<Collider>(true),Is.Empty);
            }
            var musicChairs=music.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Music room classroom-chair").ToArray();
            Assert.That(musicChairs.Length,Is.EqualTo(12));
            foreach(var chair in musicChairs)
                Assert.That(Vector3.Dot(chair.TransformDirection(Vector3.back),north),Is.GreaterThan(.99f),
                    "Music room seating no longer faces the piano end");
            // Physical guards remain at the original feet, independent of the
            // centred imported model pivot. This catches doubled half-height.
            var guards=centre.GetComponentsInChildren<BoxCollider>(true).Where(c=>
                c.name.StartsWith("Retained school furnishing",StringComparison.Ordinal)||
                c.name.StartsWith("School salvaged storage",StringComparison.Ordinal)).ToArray();
            Assert.That(guards.Length,Is.GreaterThan(8));
            foreach(var guard in guards)
            {
                Assert.That(guard.bounds.min.y,Is.EqualTo(LoopOrigin.y).Within(.012f));
                foreach(var renderer in guard.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.bounds.min.y,Is.GreaterThanOrEqualTo(guard.bounds.min.y-.03f));
                    Assert.That(renderer.bounds.max.y,Is.LessThanOrEqualTo(guard.bounds.max.y+.03f));
                }
            }
        }
    }
}
