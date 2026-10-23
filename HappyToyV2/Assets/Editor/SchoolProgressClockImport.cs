using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Derives the prefab from preserved generated FBX/PBR; no scene YAML edits.
    public static class SchoolProgressClockImport
    {
        const string Folder="Assets/Resources/SchoolProgressClock/";
        [Serializable] sealed class Report{public Surface[] materials;public Level[] lods;public int textureMaxSize;}
        [Serializable] sealed class Surface
        {
            public string name,baseColor,metallicSmoothness,normal,occlusion;
            public float normalScale=1,occlusionStrength=1;public float[] baseColorFactor;
        }
        [Serializable] sealed class Level{public string file;public int triangles,targetTriangles;}
        static Texture2D Texture(string path,int size,bool linear,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Generated clock PBR map missing: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!linear;importer.maxTextureSize=size;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Happy Toy V2/Import school progress clock")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"clock/asset.json"));
            var materials=new Dictionary<string,Material>();
            foreach(var surface in report.materials)
            {
                string mapFolder=Folder+"clock/",path=mapFolder+surface.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                material.enableInstancing=true;var tint=surface.baseColorFactor;
                material.SetColor("_BaseColor",tint?.Length==4?new Color(tint[0],tint[1],tint[2],tint[3]):Color.white);
                material.SetTexture("_BaseMap",Texture(mapFolder+surface.baseColor,report.textureMaxSize,false));
                if(!string.IsNullOrEmpty(surface.metallicSmoothness))
                {
                    material.SetTexture("_MetallicGlossMap",Texture(mapFolder+surface.metallicSmoothness,report.textureMaxSize,true));
                    material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if(!string.IsNullOrEmpty(surface.normal))
                {material.SetTexture("_BumpMap",Texture(mapFolder+surface.normal,report.textureMaxSize,true,true));material.SetFloat("_BumpScale",surface.normalScale);material.EnableKeyword("_NORMALMAP");}
                if(!string.IsNullOrEmpty(surface.occlusion))
                {material.SetTexture("_OcclusionMap",Texture(mapFolder+surface.occlusion,report.textureMaxSize,true));material.SetFloat("_OcclusionStrength",surface.occlusionStrength);material.EnableKeyword("_OCCLUSIONMAP");}
                EditorUtility.SetDirty(material);materials.Add(surface.name,material);
            }
            var root=new GameObject("Generated aged school digital progress clock");
            try
            {
                var pivot=new GameObject("Source-derived clock LODs").transform;pivot.SetParent(root.transform,false);
                var lods=new List<Renderer[]>();Bounds bounds=default;
                foreach(var level in report.lods)
                {
                    string path=Folder+level.file;var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                    if(!importer)throw new InvalidOperationException("Generated clock LOD missing: "+path);
                    importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;
                    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    importer.materialLocation=ModelImporterMaterialLocation.InPrefab;importer.SaveAndReimport();
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),pivot);
                    foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var found)?found:materials.Count==1?materials.Values.Single():throw new InvalidOperationException("Unmapped clock PBR surface")).ToArray();
                    lods.Add(model.GetComponentsInChildren<Renderer>(true));if(lods.Count==1)bounds=GraphicsPropLibrary.LocalBounds(pivot);
                }
                pivot.localPosition=-bounds.center;
                var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.045f,lods[0]),new LOD(.001f,lods[1])});group.RecalculateBounds();
                foreach(var item in root.GetComponentsInChildren<Transform>(true))item.gameObject.layer=8;
                AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Folder+"clock-visual.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            string ledPath=Folder+"clock-led.mat";var led=AssetDatabase.LoadAssetAtPath<Material>(ledPath);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"SchoolClockLED.shader");
            if(!shader)throw new InvalidOperationException("School clock LED shader missing");
            if(!led){led=new Material(shader);AssetDatabase.CreateAsset(led,ledPath);}else led.shader=shader;
            led.SetColor("_BaseColor",new Color(1.4f,.095f,.025f,1));EditorUtility.SetDirty(led);
            AssetDatabase.SaveAssets();Ensure();Debug.Log("SCHOOL_PROGRESS_CLOCK_IMPORT_PASS");
        }
        public static void Ensure()
        {
            var prefab=Resources.Load<GameObject>("SchoolProgressClock/clock-visual");
            var led=Resources.Load<Material>("SchoolProgressClock/clock-led");
            if(!prefab||!led||!led.shader||!prefab.GetComponent<LODGroup>()||prefab.GetComponent<LODGroup>().GetLODs().Length!=2)
                throw new InvalidOperationException("Generated school progress clock/LED/LODs incomplete");
            if(prefab.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Clock must not obstruct the school route");
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if(!filter.sharedMesh||filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount)throw new InvalidOperationException("Clock source UVs incomplete");
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterials.Any(m=>!m||!m.GetTexture("_BaseMap")))throw new InvalidOperationException("Clock source PBR incomplete");
        }
    }
}
