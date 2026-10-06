using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class GraphicsPropAssetTests
    {
        [Test]
        public void AuthoredPropsImportAtMeasuredMetreScaleWithCleanUvsNormalsAndNoPhysics()
        {
            // Exact reviewed Blender authoring bounds. The verified FBX/Unity import converts
            // right-handed source coordinates to Unity by reflecting X, preserving Y/Z and units.
            var expected=new[] {
                ("candle-waymark",new Vector3(0.2240000069f,1.2783581913f,0.2240000069f),new Vector3(0.0000000000f,-0.4208208472f,0.0000000000f),8010),
                ("battery-supply",new Vector3(0.2240000069f,1.3340000063f,0.2240000069f),new Vector3(0.0000000000f,-0.4530000016f,0.0000000000f),8460),
                ("paper-lantern",new Vector3(0.3688062429f,0.5409647822f,0.3694754392f),new Vector3(-0.0011271387f,0.0256353021f,0.0008569136f),7988),
                ("seal-altar",new Vector3(0.8100000024f,0.6895000339f,0.6000000238f),new Vector3(0.0000000000f,0.3457500041f,0.0000000000f),8408),
                ("cabinet-shell",new Vector3(0.8989999890f,1.8754999638f,0.6777652204f),new Vector3(0.0000000000f,0.9377499819f,-0.0308826119f),11452),
                ("cabinet-timber",new Vector3(0.8989999890f,1.8754999638f,0.6777652204f),new Vector3(0.0000000000f,0.9377499819f,-0.0308826119f),13332),
                ("door-hardware",new Vector3(0.0610000007f,0.2109999955f,0.0291500888f),new Vector3(0.0000000000f,0.0000000000f,-0.0105750442f),1144),
                ("candle-flame",new Vector3(0.0292097824f,0.0640000030f,0.0252964124f),new Vector3(0.0012495196f,0.0320000015f,0.0010821158f),48)
            };
            foreach(var item in expected)
            {
                string path="Assets/Resources/GraphicsUpgrade/Props/"+item.Item1+".fbx";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null,path);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.importNormals,Is.EqualTo(ModelImporterNormals.Import));
                Assert.That(importer.importTangents,Is.EqualTo(ModelImporterTangents.CalculateMikk));
                Assert.That(importer.addCollider,Is.False);Assert.That(importer.importAnimation,Is.False);
                var clone=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Assert.That(clone.GetComponentsInChildren<Collider>(true),Is.Empty);
                    Assert.That(clone.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
                    var filters=clone.GetComponentsInChildren<MeshFilter>(true);Assert.That(filters.Length,Is.EqualTo(1),"Static prop should stay one immutable mesh");
                    var mesh=filters[0].sharedMesh;var vertices=mesh.vertices;var bounds=new Bounds(filters[0].transform.TransformPoint(vertices[0]),Vector3.zero);
                    foreach(var vertex in vertices) bounds.Encapsulate(filters[0].transform.TransformPoint(vertex));
                    Assert.That(Vector3.Distance(bounds.size,item.Item2),Is.LessThan(.002f),item.Item1+" changed source metre scale");
                    var importedCenter=new Vector3(-item.Item3.x,item.Item3.y,item.Item3.z);
                    Assert.That(Vector3.Distance(bounds.center,importedCenter),Is.LessThan(.002f),item.Item1+" changed verified source-to-Unity origin/basis");
                    Assert.That(mesh.triangles.Length/3,Is.EqualTo(item.Item4),item.Item1+" topology did not match reviewed source");
                    Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount));Assert.That(mesh.normals.Length,Is.EqualTo(mesh.vertexCount));
                    Assert.That(mesh.tangents.Length,Is.EqualTo(mesh.vertexCount));
                    foreach(var uv in mesh.uv) Assert.That(!float.IsNaN(uv.x) && !float.IsNaN(uv.y) && uv.x>=-.001f && uv.x<=1.001f && uv.y>=-.001f && uv.y<=1.001f,Is.True,"Unpacked/invalid UV");
                    foreach(var normal in mesh.normals) Assert.That(normal.sqrMagnitude,Is.InRange(.98f,1.02f));
                    foreach(var material in clone.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials))
                        Assert.That(material.name,Does.StartWith("GU_"),"Material slot contract changed");
                }
                finally {UnityEngine.Object.DestroyImmediate(clone);}
            }
            using(var sha=SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Annex/SchoolAnnex.unity"))).Replace("-","").ToLowerInvariant(),
                    Is.EqualTo("0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede"));
        }

        [Test]
        public void OriginalPropPbrMapsHaveRealNormalsMetallicSmoothnessAndTransparentPhotographicFlame()
        {
            foreach(string key in new[]{"wax-tallow","wax-pool","brass-tarnished","cloth-charred","painted-metal"})
            {
                string prefix="Assets/Resources/GraphicsPbr/"+key+"/";
                foreach(string kind in new[]{"albedo","normal","ao","metallic-smoothness"})
                {
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+kind+".png");Assert.That(texture,Is.Not.Null);
                    Assert.That(texture.width,Is.EqualTo(2048));Assert.That(texture.height,Is.EqualTo(2048));
                    var importer=(TextureImporter)AssetImporter.GetAtPath(prefix+kind+".png");
                    Assert.That(importer.textureType,Is.EqualTo(kind=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default));
                    Assert.That(importer.sRGBTexture,Is.EqualTo(kind=="albedo"));
                }
                var packed=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try
                {
                    Assert.That(packed.LoadImage(File.ReadAllBytes(prefix+"metallic-smoothness.png")),Is.True);
                    var pixels=packed.GetPixels32();Assert.That(pixels.All(p=>p.g==0 && p.b==0),Is.True);
                    if(key=="wax-tallow" || key=="wax-pool" || key=="cloth-charred") Assert.That(pixels.All(p=>p.r==0),Is.True,"Wax/charcoal cannot be metallic");
                    else Assert.That(pixels.Max(p=>(int)p.r)-pixels.Min(p=>(int)p.r),Is.GreaterThan(15));
                    Assert.That(pixels.Max(p=>(int)p.a)-pixels.Min(p=>(int)p.a),Is.GreaterThan(4),"Smoothness field missing");
                }
                finally {UnityEngine.Object.DestroyImmediate(packed);}
            }
            var flame=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(flame.LoadImage(File.ReadAllBytes("Assets/Resources/GraphicsUpgrade/Textures/candle-flame-v3.png")),Is.True);
                var pixels=flame.GetPixels32();Assert.That(pixels.Count(p=>p.a<4),Is.GreaterThan(pixels.Length/3));
                Assert.That(pixels.Count(p=>p.a>160),Is.GreaterThan(1000));
                Assert.That(Resources.Load<Shader>("GraphicsUpgrade/Shaders/CandleFlame"),Is.Not.Null);
            }
            finally {UnityEngine.Object.DestroyImmediate(flame);}
        }
    }
}
