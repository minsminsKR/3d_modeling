using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    public static class LadyGameImport
    {
        const string Folder="Assets/Resources/Lady";
        public static void Ensure()
        {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/LadyRuntime.prefab")||
               !AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/chase.anim"))Apply();
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            string path=Folder+"/Lady.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(!importer)throw new InvalidOperationException("Export Lady's generated model and editable rig first");
            string previousSettings=EditorJsonUtility.ToJson(importer);
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.resampleCurves=true;
            importer.isReadable=true;importer.optimizeGameObjects=false;importer.preserveHierarchy=true;
            importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.meshCompression=ModelImporterMeshCompression.Off;importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=8;importer.minBoneWeight=.0001f;
            if(EditorJsonUtility.ToJson(importer)!=previousSettings)importer.SaveAndReimport();
            var material=Surface(Folder);var farMaterial=Surface(Folder+"/Far");
            var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            if(imported.Length!=1)throw new InvalidOperationException("Expected one authored Lady chase action");
            var action=new AnimationClip{name="chase",legacy=true,frameRate=60,wrapMode=WrapMode.Loop};
            var originalBindings=AnimationUtility.GetCurveBindings(imported[0]);
            // Imported clips can keep constant quaternion channels internally.
            // A whole static rotation group is export noise, not a head/limb beat.
            var staticRotations=originalBindings.Where(b=>b.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal))
                .GroupBy(b=>b.path).Where(group=>group.All(b=>
                {
                    var keys=AnimationUtility.GetEditorCurve(imported[0],b).keys;
                    return keys.Length==0||keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.00001f);
                })).Select(group=>group.Key).ToArray();
            foreach(var original in originalBindings)
            {
                var curve=AnimationUtility.GetEditorCurve(imported[0],original);var keys=curve.keys;
                if(keys.Length==0)continue;
                var binding=original;binding.path="Model"+(binding.path.Length>0?"/"+binding.path:"");
                if(original.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal)&&staticRotations.Contains(original.path))continue;
                // FBX adds ~1e-7 scale roundoff to otherwise fixed bones. Keep
                // those scales in the prefab baseline; retain rotation/tremor
                // curves at the original strict tolerance.
                float tolerance=binding.propertyName.StartsWith("m_LocalScale.",StringComparison.Ordinal)? .00001f : .0000001f;
                if(keys.All(k=>Mathf.Abs(k.value-keys[0].value)<tolerance))continue;
                AnimationUtility.SetEditorCurve(action,binding,curve);
            }
            var prior=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/chase.anim");
            if(prior){EditorUtility.CopySerialized(action,prior);UnityEngine.Object.DestroyImmediate(action);action=prior;}
            else AssetDatabase.CreateAsset(action,Folder+"/chase.anim");
            var root=new GameObject("lady");
            try
            {
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);model.name="Model";
                // Evaluate the complete native FBX pose, including constant
                // channels absent from GetCurveBindings (notably toe axes).
                imported[0].SampleAnimation(model,0);
                foreach(var old in model.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(old);
                foreach(var old in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(old);
                foreach(var old in model.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(old);
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length!=3||skins.Any(s=>!s.sharedMesh||s.bones.Length<20))throw new InvalidOperationException("Lady needs three real skinned LODs and her complete rig");
                var ordered=skins.OrderByDescending(s=>s.sharedMesh.vertexCount).ToArray();
                foreach(var skin in skins)
                {skin.enabled=true;skin.gameObject.SetActive(true);skin.sharedMaterials=Enumerable.Repeat(skin==ordered[2]?farMaterial:material,skin.sharedMesh.subMeshCount).ToArray();skin.updateWhenOffscreen=false;}
                if(ordered[0].sharedMesh.vertexCount>200000||ordered[0].sharedMesh.triangles.Length/3>300000)
                    throw new InvalidOperationException("Lady game LOD exceeds its finite mesh budget");
                foreach(var binding in AnimationUtility.GetCurveBindings(action))if(!root.transform.Find(binding.path))
                    throw new InvalidOperationException("Lady action lost a real joint path: "+binding.path);
                var envelopes=skins.ToDictionary(s=>s,s=>new Bounds());var mesh=new Mesh();
                try
                {
                    for(int frame=0;frame<65;frame++)
                    {
                        action.SampleAnimation(root,action.length*frame/64f);
                        foreach(var skin in skins){EnemyStudioRig.BakeLocalPose(skin,mesh);var bounds=envelopes[skin];if(frame==0)bounds=mesh.bounds;else bounds.Encapsulate(mesh.bounds);envelopes[skin]=bounds;}
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
                foreach(var skin in skins){var bounds=envelopes[skin];bounds.Expand(.12f/Mathf.Max(.001f,skin.transform.lossyScale.x));skin.localBounds=bounds;}
                action.SampleAnimation(root,0);
                var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.36f,new Renderer[]{ordered[0]}),new LOD(.13f,new Renderer[]{ordered[1]}),new LOD(.015f,new Renderer[]{ordered[2]})});lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/LadyRuntime.prefab");
                Debug.Log("LADY_GAME_IMPORT_PASS vertices="+ordered[0].sharedMesh.vertexCount+" duration="+action.length);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
        }
        static Material Surface(string folder)
        {
            Texture(folder,"albedo.png",true);Texture(folder,"metallic-smoothness.png",false);Texture(folder,"normal.png",false,true);
            var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Skin.mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Lady generated sobok and hair"};AssetDatabase.CreateAsset(material,folder+"/Skin.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/albedo.png"));material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/metallic-smoothness.png"));
            material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/normal.png"));material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Cull",(float)CullMode.Off);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);
            material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");EditorUtility.SetDirty(material);
            return material;
        }
        static void Texture(string folder,string name,bool color,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(folder+"/"+name) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing generated Lady PBR: "+name);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=color;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
