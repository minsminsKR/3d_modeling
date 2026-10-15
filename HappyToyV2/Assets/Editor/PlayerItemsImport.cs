using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class PlayerItemsImport
    {
        const string Root="Assets/Resources/PlayerItems/";
        public static void ApplyAndReview() { Apply(); PlayerHandsPreview.Render(); }
        [Serializable] sealed class Report { public string baseColor,metallicRoughness; public bool rigged; }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string key in new[]{"hand","incense","talisman","flashlight"})
            {
                string folder=Root+key+"/";
                var report=JsonUtility.FromJson<Report>(File.ReadAllText(folder+"asset.json"));
                var albedo=Texture(folder+report.baseColor,false); var rough=Texture(folder+report.metallicRoughness,true);
                Material Surface(string name,Shader shader)
                {
                    string path=folder+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!m) { m=new Material(shader); AssetDatabase.CreateAsset(m,path); } else m.shader=shader;
                    m.SetTexture("_BaseMap",albedo); m.SetTexture("_MetalRough",rough); m.SetColor("_BaseColor",Color.white);
                    m.SetFloat("_Viewmodel",name=="held"?1:0); EditorUtility.SetDirty(m); return m;
                }
                var shader=Resources.Load<Shader>("PlayerItemSurface");
                var surface=Surface("surface",shader); Surface("held",shader);
                Prefab(key,folder+"geometry.fbx",surface,report.rigged);
                if (key=="talisman")
                    for (int i=0;i<5;i++) Prefab("fragment_"+i,folder+"fragment_"+i+".fbx",surface,false);
            }
            AssetDatabase.SaveAssets();
            Ensure();
        }
        public static void Ensure()
        {
            foreach (string key in new[]{"hand","incense","talisman","flashlight","fragment_0","fragment_1","fragment_2","fragment_3","fragment_4"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+key+".prefab");
                if (!prefab || prefab.GetComponentsInChildren<Renderer>().Length==0 ||
                    prefab.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m || !m.GetTexture("_BaseMap"))))
                    throw new InvalidOperationException("Generated player item is incomplete: "+key);
                if (key=="hand" && (prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Length!=1 ||
                    prefab.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("index_"))!=3))
                    throw new InvalidOperationException("Editable player hand rig is missing");
            }
        }
        static Texture2D Texture(string path,bool linear)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing generated player item map: "+path);
            importer.sRGBTexture=!linear; importer.maxTextureSize=2048; importer.mipmapEnabled=true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Prefab(string key,string path,Material material,bool rigged)
        {
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing source-derived item geometry: "+path);
            importer.importAnimation=false; importer.animationType=rigged?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
            importer.isReadable=true; importer.addCollider=false; importer.optimizeGameObjects=false; importer.SaveAndReimport();
            var root=new GameObject(key);
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,Root+key+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
