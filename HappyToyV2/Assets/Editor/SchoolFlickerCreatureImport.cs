using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HappyToy.V2.Editor
{
    public static class SchoolFlickerCreatureImport
    {
        const string Folder="Assets/Resources/SchoolFlickerCreature";
        [MenuItem("HappyToy/Import school flicker creature")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var importer=AssetImporter.GetAtPath(Folder+"/creature.fbx") as ModelImporter;
            if(!importer)throw new InvalidOperationException("Creature source FBX is missing");
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;
            importer.optimizeGameObjects=false;importer.preserveHierarchy=true;importer.isReadable=true;
            importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            TextureImporter Texture(string file,bool srgb)
            {
                var t=AssetImporter.GetAtPath(Folder+"/"+file) as TextureImporter;
                if(!t)throw new InvalidOperationException("Creature PBR map missing: "+file);
                t.sRGBTexture=srgb;t.maxTextureSize=4096;t.alphaSource=TextureImporterAlphaSource.FromInput;t.SaveAndReimport();return t;
            }
            Texture("creature-base.png",true);Texture("creature-metallic-smoothness.png",false);
            string materialPath=Folder+"/creature.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/creature-base.png"));
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/creature-metallic-smoothness.png"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(material);
            var root=new GameObject("Flickering school creature");
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/creature.fbx");
                if(!source)throw new InvalidOperationException("Creature FBX cannot be loaded");
                var model=Object.Instantiate(source,root.transform);model.name="Model";
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).ToArray();
                if(skins.Length!=3||skins.Any(s=>!s.sharedMesh||!s.rootBone||s.bones.Length<30))throw new InvalidOperationException("Creature needs three skinned LODs on its actual rig");
                foreach(var skin in skins){skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMesh.subMeshCount).ToArray();skin.updateWhenOffscreen=false;skin.shadowCastingMode=ShadowCastingMode.On;var envelope=skin.localBounds;envelope.Expand(.25f);skin.localBounds=envelope;}
                var bounds=skins[0].bounds;
                if(bounds.size.y<.1f)throw new InvalidOperationException("Invalid creature bounds");
                float scale=2.5f/bounds.size.y;model.transform.localScale=Vector3.one*scale;
                model.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
                var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.35f,new Renderer[]{skins[0]}),new LOD(.12f,new Renderer[]{skins[1]}),new LOD(.015f,new Renderer[]{skins[2]})});lod.RecalculateBounds();
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/creature.prefab");AssetDatabase.SaveAssets();
                Debug.Log("SCHOOL_FLICKER_CREATURE_IMPORT_PASS "+string.Join(",",skins.Select(s=>s.sharedMesh.triangles.Length/3)));
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void Ensure(){if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/creature.prefab"))Apply();}
    }
}
