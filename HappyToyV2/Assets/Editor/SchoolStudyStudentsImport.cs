using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HappyToy.V2.Editor
{
    // Runtime owns behaviour. This importer only builds source-derived art prefabs.
    public static class SchoolStudyStudentsImport
    {
        const string Root="Assets/Resources/SchoolStudyStudents";
        [Serializable] sealed class ImportStamp{public string inputs;}
        public static void Ensure()
        {
            for(int i=1;i<=3;i++)
            {
                string folder=$"{Root}/student0{i}";var stamp=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/import-stamp.json");
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/student-visual.prefab")||!stamp||
                    JsonUtility.FromJson<ImportStamp>(stamp.text)?.inputs!=InputHashes(folder)){Apply();return;}
            }
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            for(int i=1;i<=3;i++)Import($"{Root}/student0{i}");
            AssetDatabase.SaveAssets();
        }
        static void Import(string folder)
        {
            string modelPath=folder+"/student.fbx";
            var model=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(!model)throw new InvalidOperationException("Finished school student FBX is missing: "+modelPath);
            string before=EditorJsonUtility.ToJson(model);
            model.animationType=ModelImporterAnimationType.Generic;model.importAnimation=false;
            model.optimizeGameObjects=false;model.preserveHierarchy=true;model.isReadable=true;
            model.importCameras=false;model.importLights=false;model.importBlendShapes=false;model.addCollider=false;
            model.materialImportMode=ModelImporterMaterialImportMode.None;model.importNormals=ModelImporterNormals.Import;
            model.importTangents=ModelImporterTangents.CalculateMikk;model.skinWeights=ModelImporterSkinWeights.Custom;
            model.maxBonesPerVertex=4;model.minBoneWeight=.00001f;
            if(before!=EditorJsonUtility.ToJson(model))model.SaveAndReimport();
            var albedo=Texture(folder+"/student-baseColor.png",true,false);
            var metallic=Texture(folder+"/student-metallicSmoothness.png",false,false);
            var normal=Texture(folder+"/student-normal.png",false,true);
            string matPath=folder+"/student.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!material)
            {
                var template=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SchoolWalker/SW_LOD0.mat");
                if(!template)throw new InvalidOperationException("Shared school PBR template is missing");
                material=new Material(template){name="Seated ghost student PBR"};AssetDatabase.CreateAsset(material,matPath);
            }
            material.SetTexture("_BaseMap",albedo);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",metallic);material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",.35f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale",1);material.SetFloat("_Cull",(float)CullMode.Back);material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if(!imported)throw new InvalidOperationException("Imported student missing: "+modelPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(imported);
            try
            {
                foreach(var animator in instance.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(animator);
                foreach(var existingLod in instance.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(existingLod);
                if(instance.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Student visual unexpectedly has a collider");
                var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).ToArray();
                int[] budgets={50000,12000,10000};
                if(skins.Length!=3||skins.Where((s,index)=>!s.sharedMesh||s.sharedMesh.triangles.Length/3>budgets[index]||
                    !s.bones.Any(b=>b&&b.name=="HeadPivot")).Any())throw new InvalidOperationException("Student LOD mesh or head rig is invalid: "+modelPath);
                foreach(var skin in skins)
                {
                    skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMesh.subMeshCount).ToArray();
                    // Blender FBX can retain 100x local mesh transforms; expand
                    // by a world-space margin instead of sixty metres.
                    float scale=Mathf.Max(.001f,Mathf.Max(Mathf.Abs(skin.transform.lossyScale.x),Mathf.Max(Mathf.Abs(skin.transform.lossyScale.y),Mathf.Abs(skin.transform.lossyScale.z))));
                    var bounds=skin.localBounds;bounds.Expand(.6f/scale);skin.localBounds=bounds;
                    skin.shadowCastingMode=ShadowCastingMode.On;skin.receiveShadows=true;
                }
                var lod=instance.AddComponent<LODGroup>();
                lod.SetLODs(new[]{new LOD(.35f,new Renderer[]{skins[0]}),new LOD(.12f,new Renderer[]{skins[1]}),new LOD(.015f,new Renderer[]{skins[2]})});
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(instance,folder+"/student-visual.prefab");
            }
            finally{Object.DestroyImmediate(instance);}
            File.WriteAllText(folder+"/import-stamp.json",JsonUtility.ToJson(new ImportStamp{inputs=InputHashes(folder)},true)+"\n");
            AssetDatabase.ImportAsset(folder+"/import-stamp.json",ImportAssetOptions.ForceUpdate);
        }
        static string InputHashes(string folder)
        {
            return string.Join(":",new[]{"student.fbx","student-baseColor.png","student-metallicSmoothness.png","student-normal.png"}.Select(name=>
            {
                using(var stream=File.OpenRead(folder+"/"+name))using(var hash=SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }));
        }
        static Texture2D Texture(string path,bool srgb,bool normal)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Finished student texture is missing: "+path);
            string before=EditorJsonUtility.ToJson(importer);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=srgb;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=4;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
