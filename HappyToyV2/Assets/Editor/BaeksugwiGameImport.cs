using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class BaeksugwiGameImport
    {
        const string Folder="Assets/Resources/Baeksugwi";
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/Baeksugwi_Source.fbx");
            if(!importer)throw new InvalidOperationException("Export the reviewed Blender rig first");
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.resampleCurves=true;
            importer.isReadable=true;importer.optimizeGameObjects=false;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            foreach(var name in new[]{"albedo","metallic-smoothness"})
            {
                var t=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+name+".png");
                t.sRGBTexture=name=="albedo";t.maxTextureSize=2048;t.mipmapEnabled=true;
                t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Skin.mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/Skin.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/albedo.png"));
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/metallic-smoothness.png"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
            EditorUtility.SetDirty(material);
            var imported=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Baeksugwi_Source.fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
            var clipPath=Folder+"/Run_InPlace.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,clipPath);}else clip.ClearCurves();
            clip.name="Run_InPlace";clip.legacy=true;clip.frameRate=60;clip.wrapMode=WrapMode.Loop;
            var fixedScales=StaticScaleCurveCleanup.InvariantBindings(new[]{imported});
            foreach(var binding in AnimationUtility.GetCurveBindings(imported))
            {
                if(fixedScales.Contains(StaticScaleCurveCleanup.Key(binding)))continue;
                var target=binding;target.path="Rigged asset"+(binding.path.Length==0?"":"/"+binding.path);
                AnimationUtility.SetEditorCurve(clip,target,AnimationUtility.GetEditorCurve(imported,binding));
            }
            EditorUtility.SetDirty(clip);
            var root=new GameObject("Baeksugwi");
            try
            {
                var asset=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Baeksugwi_Source.fbx"),root.transform);
                asset.name="Rigged asset";
                imported.SampleAnimation(asset,0);
                foreach(var a in asset.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(a);
                foreach(var r in asset.GetComponentsInChildren<Renderer>(true))
                {r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();if(r is SkinnedMeshRenderer s)s.updateWhenOffscreen=true;}
                var animation=root.AddComponent<Animation>();animation.playAutomatically=false;animation.AddClip(clip,"run");
                clip.SampleAnimation(root,0);
                var nodes=root.GetComponentsInChildren<Transform>(true);
                var head=nodes.Single(t=>t.name=="Head");var tail=nodes.Single(t=>t.name=="Tail");
                var forward=head.position-tail.position;forward.y=0;
                root.transform.rotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward);
                var bounds=PoseBounds(root);root.transform.localScale=Vector3.one*(MaskHorrorVisual.BodyWidth/bounds.size.x);
                bounds=PoseBounds(root);
                root.transform.position=new Vector3(-bounds.center.x,-bounds.min.y,.25f-head.position.z);
                var finalBounds=PoseBounds(root);
                if(Mathf.Abs(finalBounds.size.x-MaskHorrorVisual.BodyWidth)>.02f || Mathf.Abs(finalBounds.min.y)>.005f)
                    throw new InvalidOperationException("Imported skin scale/grounding mismatch: "+finalBounds);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Baeksugwi.prefab");
                Directory.CreateDirectory("Verification/baeksugwi-game");
                File.WriteAllText("Verification/baeksugwi-game/import.json",JsonUtility.ToJson(new Report{
                    status="PASS",bones=root.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length,
                    arms=nodes.Count(t=>t.name.EndsWith("_Wrist")),clipSeconds=clip.length,
                    curves=AnimationUtility.GetCurveBindings(clip).Length,width=PoseBounds(root).size.x,
                    headForward=Vector3.Dot((head.position-tail.position).normalized,Vector3.forward)},true));
                if(nodes.Count(t=>t.name.EndsWith("_Wrist"))!=18||clip.length<.7f)throw new InvalidOperationException("Studio skeleton/clip import mismatch");
                AssetDatabase.SaveAssets();Debug.Log("BAEKSUGWI_GAME_IMPORT_PASS");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static Bounds PoseBounds(GameObject root)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=new Mesh();skin.BakeMesh(mesh,false);
                // Unity's BakeMesh(false) omits the renderer's own scale but
                // retains ancestor scale. Undo that before TransformPoint so
                // the fitted prefab scale is applied exactly once.
                var inherited=skin.transform.parent?skin.transform.parent.lossyScale:Vector3.one;
                foreach(var p in mesh.vertices){var local=new Vector3(p.x/inherited.x,p.y/inherited.y,p.z/inherited.z);var world=skin.transform.TransformPoint(local);if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world);}
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            if(first)throw new InvalidOperationException("Missing skinned creature mesh");return bounds;
        }
        [Serializable]class Report{public string status;public int bones,arms,curves;public float clipSeconds,width,headForward;}
    }
}
