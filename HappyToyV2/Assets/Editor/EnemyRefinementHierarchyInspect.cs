using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Read-only imported-asset evidence. No scene is opened/saved or source modified.
    public static class EnemyRefinementHierarchyInspect
    {
        [Serializable] sealed class Node
        { public string path, name, parent; public Vector3 localPosition,localScale; public Quaternion localRotation; public bool bone; }
        [Serializable] sealed class Skin
        { public string path,mesh,rootBone; public int vertices; public string[] bones; }
        [Serializable] sealed class Binding
        { public string path,property; public float initialValue; }
        [Serializable] sealed class Clip
        { public string path,name; public float seconds; public Binding[] bindings; }
        [Serializable] sealed class Asset
        {
            public string path,guid,root,animationType,normals,skinWeights;
            public float globalScale,minWeight; public int maxWeights;
            public bool preserveHierarchy,bakeAxisConversion; public Node[] nodes; public Skin[] skins; public Clip[] clips;
        }
        [Serializable] sealed class Pair
        { public string key; public Asset original,candidate; public Clip[] authoredClips; }
        [Serializable] sealed class Report
        { public string unityVersion; public Pair[] pairs; }
        static string Relative(Transform root,Transform node)
        {
            if(!node)return null; if(node==root)return "";
            string path=node.name;
            while(node.parent!=root) { node=node.parent;if(!node)return "<outside>"+path;path=node.name+"/"+path; }
            return path;
        }
        static Clip DescribeClip(string path,AnimationClip clip)
        {
            return new Clip {path=path,name=clip.name,seconds=clip.length,bindings=AnimationUtility.GetCurveBindings(clip)
                .Select(binding=>new Binding {path=binding.path,property=binding.propertyName,
                    initialValue=AnimationUtility.GetEditorCurve(clip,binding)?.keys.FirstOrDefault().value??0}).ToArray()};
        }
        static Asset Describe(string path)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(!model)throw new InvalidOperationException("Missing inspect model "+path);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var bones=skins.SelectMany(skin=>skin.bones).Where(bone=>bone).ToArray();
            return new Asset {path=path,guid=AssetDatabase.AssetPathToGUID(path),root=model.name,globalScale=importer.globalScale,
                preserveHierarchy=importer.preserveHierarchy,bakeAxisConversion=importer.bakeAxisConversion,
                animationType=importer.animationType.ToString(),normals=importer.importNormals.ToString(),
                skinWeights=importer.skinWeights.ToString(),maxWeights=importer.maxBonesPerVertex,minWeight=importer.minBoneWeight,
                nodes=model.GetComponentsInChildren<Transform>(true).Select(node=>new Node {path=Relative(model.transform,node),name=node.name,
                    parent=node==model.transform?null:Relative(model.transform,node.parent),localPosition=node.localPosition,
                    localRotation=node.localRotation,localScale=node.localScale,bone=bones.Contains(node)}).ToArray(),
                skins=skins.Select(skin=>new Skin {path=Relative(model.transform,skin.transform),mesh=skin.sharedMesh?skin.sharedMesh.name:"",
                    vertices=skin.sharedMesh?skin.sharedMesh.vertexCount:0,rootBone=Relative(model.transform,skin.rootBone),
                    bones=skin.bones.Select(bone=>Relative(model.transform,bone)).ToArray()}).ToArray(),
                clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(clip=>!clip.name.StartsWith("__preview__"))
                    .Select(clip=>DescribeClip(path,clip)).ToArray()};
        }
        public static void Run()
        {
            var keys=new[]{"Cyclopse","Uncat","Hwacat_angry","Baby"};
            var paths=new[]{"Assets/Art/CandidateShapeSkin/Cyclopse/Walking.fbx","Assets/Art/V1/Uncat/Walking.fbx",
                "Assets/Art/V1/Hwacat_angry/Zombie Run.fbx","Assets/Art/V1/Baby/Zombie Crawl.fbx"};
            var clipPaths=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Generated"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var report=new Report {unityVersion=Application.unityVersion,pairs=keys.Select((key,index)=>new Pair {key=key,
                original=Describe(paths[index]),candidate=Describe("Assets/Resources/EnemyRefinement/"+key+"-v2.fbx"),
                authoredClips=clipPaths.Select(path=>new {path,clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path)})
                    .Where(item=>item.clip&&item.clip.name.StartsWith(key+"_",StringComparison.Ordinal))
                    .Select(item=>DescribeClip(item.path,item.clip)).ToArray()}).ToArray()};
            string output="E:/AI/3d_modeling/game/verification/development-feedback/enemy-art/hierarchy-repair/unity-imported-hierarchy.json";
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-enemy-refinement-inspect-output");
            if(arg>=0&&arg+1<args.Length)output=args[arg+1];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(report,true)); Debug.Log("ENEMY_IMPORTED_HIERARCHY_INSPECTED "+output);
        }
    }
}
