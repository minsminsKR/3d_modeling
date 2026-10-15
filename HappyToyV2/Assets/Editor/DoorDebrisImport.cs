using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Editor APIs own imported PBR materials, flattened shard assets and prefab.
    public static class DoorDebrisImport
    {
        const string Folder="Assets/Resources/DoorDebris/";
        [Serializable] sealed class Report {public int parts;public string baseColor,metallicSmoothness,normal;}
        static Texture2D Texture(string name,bool linear,bool normal=false)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+name);
            if(!importer)throw new InvalidOperationException("Missing generated timber PBR map: "+name);
            importer.sRGBTexture=!linear;importer.maxTextureSize=2048;importer.mipmapEnabled=true;
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name);
        }
        public static void ApplyAndReview(){Apply();BaeksugwiHallwayAuthoring.Apply();}
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"asset.json"));
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"geometry.fbx");
            if(!importer)throw new InvalidOperationException("Retained webapp door debris must be finished first");
            importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            string path=Folder+"BrokenTimber.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",Texture(report.baseColor,false));material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",Texture(report.metallicSmoothness,true));material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Smoothness",.6f);
            if(!string.IsNullOrEmpty(report.normal)){material.SetTexture("_BumpMap",Texture(report.normal,true,true));material.EnableKeyword("_NORMALMAP");}
            EditorUtility.SetDirty(material);
            var root=new GameObject("ShatteredDoor");
            var original=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"geometry.fbx"));
            try
            {
                foreach(var filter in original.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=filter.name;
                    var points=mesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
                    var bounds=new Bounds(points[0],Vector3.zero);foreach(var v in points)bounds.Encapsulate(v);
                    mesh.vertices=points.Select(v=>v-bounds.center).ToArray();mesh.normals=mesh.normals.Select(n=>filter.transform.TransformDirection(n).normalized).ToArray();
                    mesh.RecalculateBounds();mesh.RecalculateTangents();
                    string meshPath=Folder+filter.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
                    var child=new GameObject(filter.name);child.layer=2;child.transform.SetParent(root.transform,false);child.transform.localPosition=bounds.center;
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                if(root.GetComponentsInChildren<MeshFilter>().Length!=report.parts)throw new InvalidOperationException("Generated timber chunk count changed on import");
                root.layer=2;PrefabUtility.SaveAsPrefabAsset(root,Folder+"ShatteredDoor.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(original);}
            AssetDatabase.SaveAssets();Ensure();Debug.Log("MODELED_DOOR_DEBRIS_IMPORT_PASS");
        }
        public static void Ensure()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ShatteredDoor.prefab");
            if(!prefab || prefab.GetComponentsInChildren<MeshFilter>().Length<8 || prefab.GetComponentsInChildren<Collider>().Length!=0)
                throw new InvalidOperationException("Missing reviewed modeled timber fragments");
            foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
                if(!renderer.sharedMaterial || !renderer.sharedMaterial.GetTexture("_BaseMap"))throw new InvalidOperationException("Timber fragment lacks original-derived PBR");
        }
    }
}
