using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Imports the two preserved generated music props and one existing shared
    // classroom chair. Never changes their original FBX/GLB or source materials.
    public static class SchoolLoopAssetsImport
    {
        const string Folder="Assets/Resources/SchoolLoop/";
        static readonly string[] Keys={"grand-piano","music-stand"};
        [Serializable] sealed class Report{public Surface[] materials;public Level[] lods;public float normalizedMetres;public int textureMaxSize;}
        [Serializable] sealed class Surface
        {
            public string name,baseColor,metallicSmoothness,normal,occlusion;
            public float normalScale,occlusionStrength;
            public float[] baseColorFactor;
        }
        [Serializable] sealed class Level{public string file;public int triangles,targetTriangles;}
        [MenuItem("Happy Toy V2/Import school loop music assets")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(string key in Keys)Import(key);
            Chair();Glass();AssetDatabase.SaveAssets();Ensure();
            Debug.Log("SCHOOL_LOOP_ASSETS_IMPORT_PASS");
        }
        static Texture2D Texture(string path,int size,bool linear,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Required school texture absent: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!linear;importer.maxTextureSize=size;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.isReadable=false;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string key,Surface surface,int size)
        {
            string folder=Folder+key+"/",path=folder+surface.name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.enableInstancing=true;
            material.SetColor("_BaseColor",surface.baseColorFactor?.Length==4?new Color(surface.baseColorFactor[0],surface.baseColorFactor[1],surface.baseColorFactor[2],surface.baseColorFactor[3]):Color.white);
            material.SetTexture("_BaseMap",Texture(folder+surface.baseColor,size,false));
            material.SetTexture("_MetallicGlossMap",Texture(folder+surface.metallicSmoothness,size,true));
            material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if(!string.IsNullOrEmpty(surface.normal))
            {material.SetTexture("_BumpMap",Texture(folder+surface.normal,size,true,true));material.SetFloat("_BumpScale",surface.normalScale);material.EnableKeyword("_NORMALMAP");}
            if(!string.IsNullOrEmpty(surface.occlusion))
            {material.SetTexture("_OcclusionMap",Texture(folder+surface.occlusion,size,true));material.SetFloat("_OcclusionStrength",surface.occlusionStrength);material.EnableKeyword("_OCCLUSIONMAP");}
            EditorUtility.SetDirty(material);return material;
        }
        static void Import(string key)
        {
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+key+"/asset.json"));
            foreach(var level in report.lods)
            {
                var importer=AssetImporter.GetAtPath(Folder+level.file) as ModelImporter;
                if(!importer)throw new InvalidOperationException("School music LOD missing: "+level.file);
                importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
                importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
            }
            var materials=report.materials.ToDictionary(s=>s.name,s=>Material(key,s,report.textureMaxSize));AssetDatabase.SaveAssets();
            var root=new GameObject("Generated school "+key);
            try
            {
                var pivot=new GameObject("Grounded source-derived music visual").transform;pivot.SetParent(root.transform,false);
                var lods=new List<Renderer[]>();Bounds bounds=default;
                foreach(var level in report.lods)
                {
                    var mesh=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+level.file),pivot);
                    foreach(var renderer in mesh.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var found)?found:materials.Count==1?materials.Values.Single():throw new InvalidOperationException("Unmapped school surface")).ToArray();
                    lods.Add(mesh.GetComponentsInChildren<Renderer>(true));if(lods.Count==1)bounds=GraphicsPropLibrary.LocalBounds(pivot);
                }
                float scale=report.normalizedMetres/bounds.size.y;pivot.localScale=Vector3.one*scale;
                pivot.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.16f,lods[0]),new LOD(.001f,lods[1])});group.RecalculateBounds();
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+key+"-visual.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static Material RetainedPbr(string name,string source,Color tint)
        {
            string path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Resources.Load<Material>("GraphicsPbr/"+source+"/material"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",tint);material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }
        static void Chair()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Finished/classroom-chair.fbx");
            if(!source)throw new InvalidOperationException("Existing editable classroom chair required");
            var wood=RetainedPbr("chair-wood","wood-aged",new Color(.78f,.62f,.39f));
            var metal=RetainedPbr("chair-metal","painted-metal",new Color(.43f,.48f,.41f));
            var root=new GameObject("Retained school classroom chair");
            try
            {
                var visual=UnityEngine.Object.Instantiate(source,root.transform).transform;
                var bounds=GraphicsPropLibrary.LocalBounds(root.transform);float scale=.82f/bounds.size.y;
                visual.localScale*=scale;visual.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.ToLowerInvariant().Contains("iron")||m.name.ToLowerInvariant().Contains("metal")?metal:wood).ToArray();
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Folder+"classroom-chair-visual.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void Glass()
        {
            string path=Folder+"window-glass.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_BlendModePreserveSpecular",0);
            material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetColor("_BaseColor",new Color(.55f,.58f,.53f,.035f));material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.SetFloat("_Smoothness",.24f);material.SetFloat("_Metallic",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
            material.SetShaderPassEnabled("ShadowCaster",false);EditorUtility.SetDirty(material);
        }
        public static void Ensure()
        {
            foreach(var key in Keys)
            {
                var prefab=Resources.Load<GameObject>("SchoolLoop/"+key+"-visual");
                if(!prefab||!prefab.GetComponent<LODGroup>()||prefab.GetComponent<LODGroup>().GetLODs().Length!=2||prefab.GetComponentsInChildren<Collider>(true).Length!=0)
                    throw new InvalidOperationException("School music prefab/LODs incomplete: "+key);
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if(!filter.sharedMesh||filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount)throw new InvalidOperationException("Music mesh UVs incomplete");
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    if(renderer.sharedMaterials.Any(m=>!m||!m.GetTexture("_BaseMap")||!m.GetTexture("_MetallicGlossMap")))throw new InvalidOperationException("Music PBR incomplete");
            }
            if(!Resources.Load<GameObject>("SchoolLoop/classroom-chair-visual")||!Resources.Load<Material>("SchoolLoop/window-glass"))
                throw new InvalidOperationException("School chair/glazing incomplete");
        }
    }
}
