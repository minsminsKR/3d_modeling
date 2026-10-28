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
            // The source's metallic map makes the dark skin appear pale under the
            // flashlight. Keep its authored albedo and a restrained dielectric
            // response. The emergency light belongs to the environment; the
            // creature must never generate a textured glow of its own.
            material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",.05f);material.SetFloat("_Smoothness",.35f);
            material.SetTexture("_EmissionMap",null);
            material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;EditorUtility.SetDirty(material);
            var root=new GameObject("Flickering school creature");
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/creature.fbx");
                if(!source)throw new InvalidOperationException("Creature FBX cannot be loaded");
                var model=Object.Instantiate(source,root.transform);model.name="Model";
                foreach(var group in model.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(group);
                foreach(var animator in model.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(animator);
                foreach(var animation in model.GetComponentsInChildren<Animation>(true))Object.DestroyImmediate(animation);
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).ToArray();
                if(skins.Length!=3||skins.Any(s=>!s.sharedMesh||!s.rootBone||s.bones.Length<30))throw new InvalidOperationException("Creature needs three skinned LODs on its actual rig");
                foreach(var skin in skins){skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMesh.subMeshCount).ToArray();skin.updateWhenOffscreen=false;skin.shadowCastingMode=ShadowCastingMode.On;skin.skinnedMotionVectors=false;skin.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;}
                // Measure the deformed vertices, not renderer culling bounds or
                // the safety padding. This captures the imported armature scale.
                Bounds Measure()
                {
                    var mesh=new Mesh();
                    try{skins[0].BakeMesh(mesh);var vertices=mesh.vertices;if(vertices.Length==0)throw new InvalidOperationException("Creature has no baked vertices");
                        Vector3 Point(Vector3 p)=>root.transform.InverseTransformPoint(skins[0].transform.TransformPoint(p));
                        var result=new Bounds(Point(vertices[0]),Vector3.zero);foreach(var vertex in vertices)result.Encapsulate(Point(vertex));return result;
                    }finally{Object.DestroyImmediate(mesh);}
                }
                for(int i=0;i<20;i++){var measured=Measure();if(measured.size.x<.1f||measured.size.y<.1f)throw new InvalidOperationException("Invalid creature dimensions");
                    var scale=model.transform.localScale;scale.x*=Mathf.Sqrt(3.9f/measured.size.x);scale.y*=Mathf.Sqrt(3.0f/measured.size.y);scale.z=scale.x;model.transform.localScale=scale;}
                var bounds=Measure();model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                bounds=Measure();if(Mathf.Abs(bounds.size.x-3.9f)>.04f||Mathf.Abs(bounds.size.y-3.0f)>.04f||Mathf.Abs(bounds.min.y)>.03f)
                    throw new InvalidOperationException("Creature geometry does not fit the school corridor: "+bounds);
                foreach(var skin in skins){var envelope=skin.localBounds;envelope.Expand(.25f);skin.localBounds=envelope;}
                Debug.Log("SCHOOL_FLICKER_CREATURE_GEOMETRY "+bounds);
                var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.35f,new Renderer[]{skins[0]}),new LOD(.12f,new Renderer[]{skins[1]}),new LOD(.015f,new Renderer[]{skins[2]})});lod.RecalculateBounds();
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                string oldPath=Folder+"/creature.prefab",prefabPath=Folder+"/creature-visual.prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(oldPath)&&!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                {var error=AssetDatabase.MoveAsset(oldPath,prefabPath);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);}
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);AssetDatabase.SaveAssets();
                Debug.Log("SCHOOL_FLICKER_CREATURE_IMPORT_PASS "+string.Join(",",skins.Select(s=>s.sharedMesh.triangles.Length/3)));
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void Ensure()
        {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/creature-visual.prefab"))Apply();
            var data=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/pose-calibration.json");
            if(!data)throw new InvalidOperationException("Run FlickerCreatureDiagnostics.Inspect to create pose calibration");
            var c=JsonUtility.FromJson<SchoolFlickerCreatureMotion.Calibration>(data.text);
            string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create()){var text=System.IO.File.ReadAllText(path).Replace("\r\n","\n");return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
            if(c==null||c.version!=1||c.prefabSha256!=Hash(Folder+"/creature-visual.prefab")||c.motionSha256!=Hash("Assets/Scripts/SchoolFlickerCreatureMotion.cs"))
                throw new InvalidOperationException("Creature pose calibration is stale; run FlickerCreatureDiagnostics.Inspect");
        }
    }
}
