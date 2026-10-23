using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Rebuilds game prefabs from preserved CLI/Blender outputs, not scene YAML.
    public static class SchoolRenewalImport
    {
        const string Folder="Assets/Resources/SchoolRenewal/";
        [Serializable] sealed class Report{public string key;public int textureMaxSize;public Surface[] materials;public Level[] lods;}
        [Serializable] sealed class Surface
        {
            public string name,baseColor,metallicSmoothness,normal,occlusion,emission;
            public float[] baseColorFactor,emissionColor;
            public float normalScale=1,occlusionStrength=1;
        }
        [Serializable] sealed class Level{public string file;public int triangles,targetTriangles;}
        static Texture2D Map(string path,int size,bool linear,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("School renewal map missing: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!linear;
            importer.maxTextureSize=size;importer.mipmapEnabled=true;importer.anisoLevel=8;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string folder,Surface s,int size)
        {
            string path=folder+s.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.enableInstancing=true;mat.SetOverrideTag("GraphicsSurface","school-renewal-"+s.name);mat.SetOverrideTag("GraphicsTileMetres","1");
            var tint=s.baseColorFactor;mat.SetColor("_BaseColor",tint?.Length==4?new Color(tint[0],tint[1],tint[2],tint[3]):Color.white);
            mat.SetTexture("_BaseMap",Map(folder+s.baseColor,size,false));
            mat.SetTexture("_MetallicGlossMap",Map(folder+s.metallicSmoothness,size,true));
            mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            if(!string.IsNullOrEmpty(s.normal))
            {mat.SetTexture("_BumpMap",Map(folder+s.normal,size,true,true));mat.SetFloat("_BumpScale",s.normalScale);mat.EnableKeyword("_NORMALMAP");}
            if(!string.IsNullOrEmpty(s.occlusion))
            {mat.SetTexture("_OcclusionMap",Map(folder+s.occlusion,size,true));mat.SetFloat("_OcclusionStrength",s.occlusionStrength);mat.EnableKeyword("_OCCLUSIONMAP");}
            if(!string.IsNullOrEmpty(s.emission))
            {
                mat.SetTexture("_EmissionMap",Map(folder+s.emission,size,false));mat.SetColor("_EmissionColor",Color.black);
                mat.EnableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);return mat;
        }
        [MenuItem("Happy Toy V2/Import renewed school model kit")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach(string key in SchoolRenewalModels.Keys)
                if(!File.Exists(Folder+key+"/asset.json"))throw new InvalidOperationException("Awaiting completed school model: "+key);
            foreach(string key in SchoolRenewalModels.Keys)Import(key);
            AssetDatabase.SaveAssets();Ensure();Debug.Log("SCHOOL_RENEWAL_IMPORT_PASS");
        }
        static void Import(string key)
        {
            string folder=Folder+key+"/";var report=JsonUtility.FromJson<Report>(File.ReadAllText(folder+"asset.json"));
            if(report.key!=key||report.lods.Length<2||report.materials.Length<1)throw new InvalidOperationException("New model contract incomplete: "+key);
            var materials=new Dictionary<string,Material>();
            foreach(var s in report.materials)materials.Add(s.name,Material(folder,s,report.textureMaxSize));
            var root=new GameObject("Generated derelict Korean school — "+key);
            try
            {
                var pivot=new GameObject("Preserved source derived game LODs").transform;pivot.SetParent(root.transform,false);
                var levels=new List<LOD>();Bounds near=default;
                for(int i=0;i<report.lods.Length;i++)
                {
                    var level=report.lods[i];string path=folder+level.file;var imp=AssetImporter.GetAtPath(path) as ModelImporter;
                    if(!imp)throw new InvalidOperationException("New model FBX missing: "+path);
                    imp.importAnimation=false;imp.isReadable=true;imp.addCollider=false;
                    imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.materialLocation=ModelImporterMaterialLocation.InPrefab;
                    imp.SaveAndReimport();var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),pivot);
                    var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
                    foreach(var renderer in renderers)
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var found)?found:
                            report.materials.Length==report.lods.Length?materials[report.materials[i].name]:materials.Count==1?materials.Values.Single():
                            throw new InvalidOperationException("Unmapped renewed school material: "+key+"/"+m.name)).ToArray();
                        renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                    }
                    if(i==0)near=GraphicsPropLibrary.LocalBounds(pivot);
                    levels.Add(new LOD(i==0?.045f:.001f,renderers));
                }
                pivot.localPosition=-near.center;var group=root.AddComponent<LODGroup>();group.SetLODs(levels.ToArray());group.RecalculateBounds();
                foreach(var item in root.GetComponentsInChildren<Transform>(true))item.gameObject.layer=8;
                PrefabUtility.SaveAsPrefabAsset(root,folder+key+"-visual.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void Ensure()
        {
            foreach(string key in SchoolRenewalModels.Keys)
            {
                var prefab=SchoolRenewalModels.Source(key);var lods=prefab.GetComponent<LODGroup>().GetLODs();
                if(lods.Length!=2||lods.Any(l=>l.renderers.Length==0))throw new InvalidOperationException("School new model LOD contract missing: "+key);
                if(prefab.GetComponentsInChildren<Collider>(true).Length>0)throw new InvalidOperationException("School model must use existing physical route: "+key);
                foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if(!f.sharedMesh||!f.sharedMesh.isReadable||f.sharedMesh.uv.Length!=f.sharedMesh.vertexCount)throw new InvalidOperationException("School model game UVs incomplete: "+key);
                foreach(var m in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                    if(!m||!m.GetTexture("_BaseMap")||!m.GetTexture("_BumpMap")||!m.GetTexture("_MetallicGlossMap"))
                        throw new InvalidOperationException("School model rebaked PBR/normal missing: "+key);
            }
        }
    }
}
