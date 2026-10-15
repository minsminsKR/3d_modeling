using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class TrapAssetsImport
    {
        const string Folder="Assets/Resources/Traps/";
        [Serializable] sealed class Report { public float widthMetres; public Surface[] materials; }
        [Serializable] sealed class Surface { public string name,baseColor,metallicSmoothness,normal; public float roughness; }
        public static void ImportAudio()
        {
            AssetDatabase.Refresh();
            foreach(string cue in new[]{"trap-place","trap-trigger","trap-release"})for(int i=0;i<2;i++)
            {
                string path="Assets/Resources/Audio/External/"+cue+"-"+i+".wav";
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                if(!importer)throw new InvalidOperationException("Missing original-recording trap cue: "+path);
                importer.forceToMono=true;importer.loadInBackground=false;
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData=true;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            Debug.Log("TRAP_RECORDED_AUDIO_IMPORT_PASS");
        }
        static Texture2D Texture(string name,bool linear,bool normal=false)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+name);
            if(!importer)throw new InvalidOperationException("Missing original-derived trap PBR map: "+name);
            importer.sRGBTexture=!linear;importer.maxTextureSize=2048;importer.mipmapEnabled=true;
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name);
        }
        public static void ApplyAndReview() { Apply(); TrapAssetsPreview.Render(); }
        public static void Apply()
        {
            ImportAudio();
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(Folder+"asset.json"));
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"geometry.fbx");
            if(!importer)throw new InvalidOperationException("Webapp trap geometry and editable source must be finished first");
            importer.importAnimation=false;importer.isReadable=true;importer.addCollider=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            var materials=report.materials.ToDictionary(item=>item.name,item=>{
                string path=Folder+item.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                m.SetTexture("_BaseMap",Texture(item.baseColor,false));m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",1-item.roughness);
                if(!string.IsNullOrEmpty(item.metallicSmoothness))
                {m.SetTexture("_MetallicGlossMap",Texture(item.metallicSmoothness,true));m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}
                if(!string.IsNullOrEmpty(item.normal))
                {m.SetTexture("_BumpMap",Texture(item.normal,true,true));m.EnableKeyword("_NORMALMAP");}
                EditorUtility.SetDirty(m);return m;
            });
            var root=new GameObject("Trap");
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"geometry.fbx"),root.transform);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&materials.TryGetValue(m.name,out var found)?found:
                        materials.Count==1?materials.Values.Single():throw new InvalidOperationException("Trap PBR material slot mismatch")).ToArray();
                var bounds=MonsterPresentationScale.Bounds(model.transform);model.transform.localScale*=report.widthMetres/bounds.size.x;
                bounds=MonsterPresentationScale.Bounds(model.transform);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=2;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"Trap.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Ensure();Debug.Log("TRAP_ASSET_IMPORT_PASS");
        }
        public static void Ensure()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Trap.prefab");
            if(!prefab||!prefab.GetComponentsInChildren<Renderer>().Any()||
                prefab.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m||!m.GetTexture("_BaseMap"))))
                throw new InvalidOperationException("Generated webapp trap/PBR prefab is incomplete");
            foreach(string name in new[]{"JawFront","JawBack","PressurePlate"})
                if(prefab.GetComponentsInChildren<Transform>().Count(t=>t.name==name)!=1)throw new InvalidOperationException("Missing mechanical trap part: "+name);
            foreach(string cue in new[]{"trap-place","trap-trigger","trap-release"})for(int i=0;i<2;i++)
                if(!AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/External/"+cue+"-"+i+".wav"))throw new InvalidOperationException("Missing recorded trap cue");
        }
    }
}
