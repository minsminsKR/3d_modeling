using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class CreatureGamePolishTests
    {
        static readonly string[] Keys={"Baby","Uncat","Cyclopse"};
        [Serializable] sealed class Fingerprint {public string key,sha256;}
        [Serializable] sealed class Fingerprints {public Fingerprint[] rows;}

        [Test]
        public void NearSkinsKeepTheirExactPrePolishExpressionAndEyeVertexCorrespondence()
        {
            var expected=JsonUtility.FromJson<Fingerprints>("{\"rows\":"+File.ReadAllText("SourceArt/CreatureStudio/near-mesh-fingerprints.json")+"}").rows;
            // Use the same pinned serializer as the MCP baseline without
            // changing references for the entire existing test assembly.
            var serializer=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json",true)
                .GetMethod("SerializeObject",new[]{typeof(object)});
            foreach(var key in Keys)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EnemyStudio/"+key+"/"+key+"Runtime.prefab");
                var mesh=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name==key+"_LOD0").sharedMesh;
                var bytes=Encoding.UTF8.GetBytes((string)serializer.Invoke(null,new object[]{new {
                    vertices=mesh.vertices.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),uv=mesh.uv.Select(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.triangles,
                    weights=mesh.boneWeights.Select(w=>new{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3,w.weight0,w.weight1,w.weight2,w.weight3}).ToArray()}}));
                using(var hash=SHA256.Create())
                    Assert.That(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),
                        Is.EqualTo(expected.Single(row=>row.key==key).sha256),key+" near expression/eye skin changed");
            }
        }

        static void CheckAction(GameObject prefab,AnimationClip source,AnimationClip clip)
        {
            Assert.That(AnimationUtility.GetCurveBindings(clip).Length,Is.LessThan(AnimationUtility.GetCurveBindings(source).Length));
            var raw=UnityEngine.Object.Instantiate(prefab);var clean=UnityEngine.Object.Instantiate(prefab);
            try
            {
                raw.SetActive(false);clean.SetActive(false);
                var before=raw.GetComponentsInChildren<Transform>(true);var after=clean.GetComponentsInChildren<Transform>(true);
                for(int frame=0;frame<=Mathf.CeilToInt(source.length*source.frameRate);frame++)
                {
                    float time=Mathf.Min(frame/source.frameRate,source.length);
                    source.SampleAnimation(raw.transform.Find("Rigged asset").gameObject,time);clip.SampleAnimation(clean,time);
                    for(int bone=0;bone<before.Length;bone++)
                    {
                        Assert.That(Quaternion.Angle(before[bone].localRotation,after[bone].localRotation),Is.LessThan(.08f),prefab.name+"/"+clip.name+" "+before[bone].name);
                        Assert.That(Vector3.Distance(before[bone].position,after[bone].position),Is.LessThan(.0005f),prefab.name+"/"+clip.name+" support changed");
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(raw);UnityEngine.Object.DestroyImmediate(clean);}
        }

        [Test]
        public void StudioActionsKeepTheCompleteNativeFbxPoseAcrossEveryFrame()
        {
            foreach(var key in Keys)
            {
                var folder="Assets/Resources/EnemyStudio/"+key+"/";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+key+"Runtime.prefab");
                var source=AssetDatabase.LoadAllAssetsAtPath(folder+key+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                foreach(var alias in key=="Baby"?new[]{"patrol","chase","cry"}:new[]{"patrol","chase"})
                    CheckAction(prefab,source.Single(c=>c.name.EndsWith("|"+alias)),AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+alias+".anim"));
            }
        }

        [Test]
        public void DistantCopiesRetainBonesAtlasesAndMeaningfullyReduceTriangleBudgets()
        {
            foreach(var key in Keys)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EnemyStudio/"+key+"/"+key+"Runtime.prefab");
                var group=prefab.GetComponent<LODGroup>();
                Assert.That(prefab.GetComponentsInChildren<LODGroup>(true).Length,Is.EqualTo(key=="Uncat"?0:1),"Renderers must have exactly one LOD owner");
                if(key=="Uncat"){Assert.That(group,Is.Null,"Safe reduction did not justify a second skin");continue;}
                Assert.That(group,Is.Not.Null,key);var lods=group.GetLODs();Assert.That(lods.Length,Is.EqualTo(2));
                int primary=((SkinnedMeshRenderer)lods[0].renderers.Single()).sharedMesh.triangles.Length/3;
                Assert.That(lods[1].screenRelativeTransitionHeight,Is.EqualTo(.015f).Within(.0001f));
                foreach(var lod in lods)
                {
                    var skin=(SkinnedMeshRenderer)lod.renderers.Single();var mesh=skin.sharedMesh;
                    Assert.That(skin.bones.Length,Is.EqualTo(22));Assert.That(skin.bones.All(b=>b),Is.True);
                    if(lod.renderers[0]!=lods[0].renderers[0])Assert.That(mesh.triangles.Length/3,Is.LessThanOrEqualTo(primary*.85f));
                    Assert.That(mesh.triangles.Distinct().Count(),Is.EqualTo(mesh.vertexCount));Assert.That(skin.updateWhenOffscreen,Is.False);
                    foreach(var material in skin.sharedMaterials)
                        Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),Is.EqualTo("Assets/Resources/EnemyStudio/"+key+"/albedo.png"));
                }
            }
        }

        [Test]
        public void BaeksugwiRunKeepsEveryOriginalBonePoseAfterStaticScaleCleanup()
        {
            var folder="Assets/Resources/Baeksugwi/";
            CheckAction(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Baeksugwi.prefab"),
                AssetDatabase.LoadAllAssetsAtPath(folder+"Baeksugwi.fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__")),
                AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"Run_InPlace.anim"));
        }
    }
}
