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
        [UnityTest, Timeout(60000)]
        public IEnumerator FeedbackFloorAndCeilingUsePhysicalUvsAndImportedNormalsInRealCamera()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return null; yield return null;
            var run = Get<Component>(session, "Corridor");
            var filters = run.GetComponentsInChildren<MeshFilter>(true);
            foreach (string surface in new[] { "Floor", "Ceiling" })
            {
                var pieces = filters.Where(x => x.name == surface).ToArray();
                Assert.That(pieces.Length, Is.EqualTo(81));
                var first = pieces[0]; var material = first.GetComponent<Renderer>().sharedMaterial;
                string key=surface=="Floor"?"wood-floor":"concrete-rough";
                Assert.That(material.GetTag("GraphicsSurface",false),Is.EqualTo(key));
                foreach(var channel in new[]{("_BaseMap","albedo"),("_BumpMap","normal"),("_OcclusionMap","ao"),("_MetallicGlossMap","metallic-smoothness")})
                    Assert.That(material.GetTexture(channel.Item1),Is.SameAs(Resources.Load<Texture2D>("GraphicsPbr/"+key+"/"+channel.Item2)),"Actual physical channel missing: "+channel.Item2);
                foreach(string keyword in new[]{"_NORMALMAP","_OCCLUSIONMAP","_METALLICSPECGLOSSMAP"})Assert.That(material.IsKeywordEnabled(keyword),Is.True);
                Assert.That(material.mainTexture.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(material.mainTextureScale,Is.EqualTo(Vector2.one));
                Assert.That(first.sharedMesh.tangents.Length,Is.EqualTo(first.sharedMesh.vertexCount));
                float span=float.Parse(material.GetTag("GraphicsTileMetres",false),System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(span,Is.EqualTo(surface=="Floor"?1.99999964f:2f).Within(.000001f));
                foreach (var filter in pieces)
                {
                    var mesh = filter.sharedMesh;
                    for (int i=0;i<mesh.vertexCount;i++)
                    {
                        if (Mathf.Abs(mesh.normals[i].y) < .5f) continue;
                        var position = filter.transform.TransformPoint(mesh.vertices[i]);
                        Assert.That(Vector2.Distance(mesh.uv[i], new Vector2(position.x,position.z)/span), Is.LessThan(.0001f));
                    }
                }
            }
            var altars=run.GetComponentsInChildren<Transform>().Where(t=>t.name=="Graphics joined seal altar").ToArray();
            Assert.That(altars.Length,Is.EqualTo(5));
            var altarAsset=Resources.Load<GameObject>("GraphicsUpgrade/Props/seal-altar");
            Assert.That(altarAsset,Is.Not.Null);
            var importedAltarMeshes=altarAsset.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh).ToArray();
            foreach(var altar in altars)
            {
                Assert.That(altar.GetComponentsInChildren<Collider>().Length,Is.Zero,"Art prefab altered physical paths");
                var meshes=altar.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Sum(m=>m.sharedMesh.vertexCount),Is.GreaterThan(1000),"Detailed joinery missing");
                Assert.That(meshes.All(filter=>importedAltarMeshes.Contains(filter.sharedMesh)),Is.True,"Altar copied/mutated imported geometry instead of sharing canonical assets");
                var renderer=altar.GetComponentInChildren<Renderer>();
                Assert.That(renderer.bounds.size.x,Is.LessThan(.85f));
                Assert.That(renderer.bounds.size.y,Is.LessThan(.72f));
                Assert.That(renderer.bounds.size.z,Is.LessThan(.65f));
                var physicalFloor=run.GetComponentsInChildren<BoxCollider>().Single(collider=>collider.name=="Floor" &&
                    Mathf.Abs(collider.bounds.center.x-renderer.bounds.center.x)<2.9f && Mathf.Abs(collider.bounds.center.z-renderer.bounds.center.z)<2.9f);
                Assert.That(Mathf.Abs(renderer.bounds.min.y-physicalFloor.bounds.max.y),Is.LessThanOrEqualTo(.002f),
                    "Detailed altar floats above or penetrates its actual physical floor");
            }
            ((Behaviour)player).enabled = false;
            var camera=Get<Camera>(player,"eyes"); Get<Light>(player,"flashlight").enabled=true;
            var feet=(Vector3)Call(run,"CellPosition",0); PlacePlayer(feet,false);
            foreach (var aim in new[] {new Vector3(0,-.6f,1),new Vector3(0,.65f,1),Vector3.forward})
            {
                camera.transform.rotation=Quaternion.LookRotation(aim); yield return Delay(.2f);
                var image=SchoolCameraFrame(camera,out _,out _,out _);
                try {CloudExperienceTests.Artifact("feedback-surface-"+(aim.y<0?"floor":aim.y>0?"ceiling":"hall")+".png",image.EncodeToPNG());}
                finally {Object.Destroy(image);}
            }
            Assert.That(Get<bool>(Get<Component>(run,"Presentation"),"PhysicalStateIntact"),Is.True);
        }
    }
}
