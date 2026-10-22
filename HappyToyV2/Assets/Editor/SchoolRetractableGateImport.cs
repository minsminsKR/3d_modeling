using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class SchoolRetractableGateImport
    {
        const string Folder="Assets/Resources/SchoolRetractableGate/";
        const string Key="retractable-gate";
        [Serializable] sealed class Report{public Surface[] materials;public Level[] lods;public float normalizedMetres;public int textureMaxSize;}
        [Serializable] sealed class Surface
        {
            public string name,baseColor,metallicSmoothness,normal,occlusion;
            public float normalScale=1,occlusionStrength=1;public float[] baseColorFactor;
        }
        [Serializable] sealed class Level{public string file;public int triangles,targetTriangles;}
        static Texture2D Texture(string path,int size,bool linear,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Required generated gate texture missing: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!linear;importer.maxTextureSize=size;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Happy Toy V2/Import school retractable gate")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+Key+"/asset.json"));
            var materials=new Dictionary<string,Material>();
            foreach(var surface in report.materials)
            {
                string basePath=Folder+Key+"/",path=basePath+surface.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                material.enableInstancing=true;
                var tint=surface.baseColorFactor;material.SetColor("_BaseColor",tint?.Length==4?new Color(tint[0],tint[1],tint[2],tint[3]):Color.white);
                material.SetTexture("_BaseMap",Texture(basePath+surface.baseColor,report.textureMaxSize,false));
                material.SetTexture("_MetallicGlossMap",Texture(basePath+surface.metallicSmoothness,report.textureMaxSize,true));
                material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if(!string.IsNullOrEmpty(surface.normal))
                {material.SetTexture("_BumpMap",Texture(basePath+surface.normal,report.textureMaxSize,true,true));material.SetFloat("_BumpScale",surface.normalScale);material.EnableKeyword("_NORMALMAP");}
                if(!string.IsNullOrEmpty(surface.occlusion))
                {material.SetTexture("_OcclusionMap",Texture(basePath+surface.occlusion,report.textureMaxSize,true));material.SetFloat("_OcclusionStrength",surface.occlusionStrength);material.EnableKeyword("_OCCLUSIONMAP");}
                EditorUtility.SetDirty(material);materials.Add(surface.name,material);
            }
            var root=new GameObject("Generated folded school scissor gate with one hanging padlock");
            try
            {
                var pivot=new GameObject("Source-derived gate LODs").transform;pivot.SetParent(root.transform,false);
                var lods=new List<Renderer[]>();Bounds bounds=default;
                foreach(var level in report.lods)
                {
                    string path=Folder+level.file;var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                    if(!importer)throw new InvalidOperationException("Generated gate LOD missing: "+path);
                    importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;
                    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                    importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),pivot,false);
                    foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var found)?found:materials.Count==1?materials.Values.Single():throw new InvalidOperationException("Unmapped generated gate material")).ToArray();
                    lods.Add(model.GetComponentsInChildren<Renderer>(true));if(lods.Count==1)bounds=GraphicsPropLibrary.LocalBounds(pivot);
                }
                float scale=report.normalizedMetres/bounds.size.y;
                pivot.localScale=Vector3.one*scale;pivot.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.13f,lods[0]),new LOD(.001f,lods[1])});group.RecalculateBounds();
                // Authored storage-eye marker on the generated free end post; no duplicate lock mesh or interaction.
                var eye=new GameObject("Single padlock storage eye").transform;eye.SetParent(root.transform,false);
                // This generated original imports with its front at -Z and free post at -X.
                eye.localPosition=new Vector3(-.50f,1.18f,-.10f);
                foreach(var item in root.GetComponentsInChildren<Transform>(true))item.gameObject.layer=8;
                AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Folder+Key+"-visual.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Ensure();Debug.Log("SCHOOL_RETRACTABLE_GATE_IMPORT_PASS");
        }
        public static void Ensure()
        {
            var prefab=Resources.Load<GameObject>("SchoolRetractableGate/"+Key+"-visual");
            if(!prefab||!prefab.GetComponent<LODGroup>()||prefab.GetComponent<LODGroup>().GetLODs().Length!=2||
                prefab.GetComponentsInChildren<Collider>(true).Length!=0||!prefab.transform.Find("Single padlock storage eye"))
                throw new InvalidOperationException("Generated school gate prefab/LODs/padlock anchor incomplete");
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterials.Any(m=>!m||!m.GetTexture("_BaseMap")||!m.GetTexture("_MetallicGlossMap")))
                    throw new InvalidOperationException("Generated gate PBR incomplete");
        }
    }
}
