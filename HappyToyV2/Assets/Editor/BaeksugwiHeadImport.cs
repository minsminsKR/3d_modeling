using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class BaeksugwiHeadImport
    {
        const string Folder="Assets/Resources/Baeksugwi";
        public static void Ensure()
        {
            if(!AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Head_Writhe.anim"))Apply();
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var importer=AssetImporter.GetAtPath(Folder+"/Head_Writhe.fbx") as ModelImporter;
            if(!importer)throw new InvalidOperationException("Author the reviewed Baeksugwi head action first");
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.resampleCurves=true;
            importer.optimizeGameObjects=false;importer.preserveHierarchy=true;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Head_Writhe.fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
            var bindings=AnimationUtility.GetCurveBindings(source).Where(b=>b.path.Split('/').Last()=="Head" &&
                (b.propertyName.StartsWith("m_LocalRotation.")||b.propertyName.StartsWith("localEulerAnglesRaw."))).ToArray();
            if(bindings.Length<3||source.length<2)throw new InvalidOperationException("Missing bounded head quaternion action");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Baeksugwi.prefab");
            foreach(var binding in bindings)
                if(!prefab.transform.Find("Rigged asset/"+binding.path))throw new InvalidOperationException("Head clip binding differs from the real prefab: "+binding.path);
            var path=Folder+"/Head_Writhe.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();
            clip.name="Head_Writhe";clip.legacy=true;clip.wrapMode=WrapMode.Loop;clip.frameRate=60;
            foreach(var binding in bindings)
            {
                var next=binding;next.path="Rigged asset/"+binding.path;
                AnimationUtility.SetEditorCurve(clip,next,AnimationUtility.GetEditorCurve(source,binding));
            }
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Verification/baeksugwi-head-20261012");
            File.WriteAllText("Verification/baeksugwi-head-20261012/import.json",JsonUtility.ToJson(new Report{status="PASS",rotationOnly=true,curves=bindings.Length,seconds=clip.length},true));
            Debug.Log("BAEKSUGWI_HEAD_IMPORT_PASS rotation only "+bindings.Length);
        }
        [Serializable]sealed class Report{public string status;public bool rotationOnly;public int curves;public float seconds;}
    }
}
