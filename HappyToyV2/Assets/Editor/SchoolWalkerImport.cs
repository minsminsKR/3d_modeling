using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HappyToy.V2.Editor
{
    // Imports only the collected/source-finished flower man. Runtime movement,
    // collisions, seam transfers and clip blending belong to the walker module.
    public static class SchoolWalkerImport
    {
        const string Folder="Assets/Resources/SchoolWalker";
        const string Stamp=Folder+"/import-stamp.json";
        [Serializable] sealed class ImportStamp{public string sourceManifestHash;}
        public static void Ensure()
        {
            var stamp=AssetDatabase.LoadAssetAtPath<TextAsset>(Stamp);
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/walker-visual.prefab")||
                !AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/walk.anim")||
                !AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/idle.anim")||!stamp||
                JsonUtility.FromJson<ImportStamp>(stamp.text).sourceManifestHash!=ManifestHash())Apply();
        }
        static string ManifestHash()
        {
            using(var stream=File.OpenRead(Folder+"/asset.json"))using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            if(!File.Exists(Folder+"/asset.json"))throw new InvalidOperationException("Finish the collected SchoolWalker source first");
            Configure(Folder+"/walker.fbx");Configure(Folder+"/idle.fbx");
            var near=Surface("SW_LOD0",4096);var far=Surface("SW_LOD2",1024);
            var importedWalk=SourceClip(Folder+"/walker.fbx");var importedIdle=SourceClip(Folder+"/idle.fbx");
            var walk=SaveClip(importedWalk,"walk");var idle=SaveClip(importedIdle,"idle");
            var root=new GameObject("Generated school flower man");
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/walker.fbx");
                var model=Object.Instantiate(source,root.transform);model.name="Model";
                // Importer bind axes include static FBX channels. Start with
                // the complete native pose before playing copied bone curves.
                importedWalk.SampleAnimation(model,0);
                foreach(var component in model.GetComponentsInChildren<Animation>(true))Object.DestroyImmediate(component);
                foreach(var component in model.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(component);
                foreach(var component in model.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(component);
                if(model.GetComponentsInChildren<Collider>(true).Length!=0)
                    throw new InvalidOperationException("Generated walker art must not own navigation bodies");
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).ToArray();
                if(skins.Length!=3||skins.Any(s=>!s.sharedMesh||s.bones.Length<15||!s.rootBone))
                    throw new InvalidOperationException("School walker needs three real skins on its preserved AI rig");
                int[] budgets={50000,12000,4000};
                for(int index=0;index<skins.Length;index++)
                {
                    var skin=skins[index];
                    if(skin.sharedMesh.triangles.Length/3>budgets[index])
                        throw new InvalidOperationException("School walker exceeded its reviewed triangle budget");
                    // A far mesh can legitimately omit unused finger joints
                    // from its skin palette. All entries still reference this
                    // one actual hierarchy and its common root bone.
                    if(skin.rootBone!=skins[0].rootBone||skin.bones.Any(b=>!b||!b.IsChildOf(model.transform)))
                        throw new InvalidOperationException("School walker LODs lost their shared bone hierarchy");
                    skin.enabled=true;skin.gameObject.SetActive(true);skin.updateWhenOffscreen=false;
                    skin.sharedMaterials=Enumerable.Repeat(index==2?far:near,skin.sharedMesh.subMeshCount).ToArray();
                    skin.shadowCastingMode=ShadowCastingMode.On;skin.receiveShadows=true;
                    skin.lightProbeUsage=LightProbeUsage.BlendProbes;skin.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                }
                foreach(var clip in new[]{walk,idle})foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    if(!root.transform.Find(binding.path))throw new InvalidOperationException("School walker action lost a source bone: "+binding.path);
                var envelopes=new Bounds[3];var mesh=new Mesh();
                try
                {
                    bool first=true;
                    foreach(var clip in new[]{walk,idle})for(int frame=0;frame<=32;frame++)
                    {
                        clip.SampleAnimation(root,clip.length*frame/32f);
                        for(int index=0;index<skins.Length;index++)
                        {
                            EnemyStudioRig.BakeLocalPose(skins[index],mesh);
                            if(first)envelopes[index]=mesh.bounds;else envelopes[index].Encapsulate(mesh.bounds);
                        }
                        first=false;
                    }
                }
                finally{Object.DestroyImmediate(mesh);}
                for(int index=0;index<skins.Length;index++)
                {var bounds=envelopes[index];bounds.Expand(.06f);skins[index].localBounds=bounds;}
                idle.SampleAnimation(root,0);
                var lod=root.AddComponent<LODGroup>();
                lod.SetLODs(new[]{new LOD(.35f,new Renderer[]{skins[0]}),new LOD(.12f,new Renderer[]{skins[1]}),new LOD(.015f,new Renderer[]{skins[2]})});
                lod.fadeMode=LODFadeMode.None;lod.RecalculateBounds();
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/walker-visual.prefab");
                Debug.Log("SCHOOL_WALKER_IMPORT_PASS triangles="+string.Join(",",skins.Select(s=>s.sharedMesh.triangles.Length/3))+" walk="+walk.length+" idle="+idle.length);
            }
            finally{Object.DestroyImmediate(root);}
            File.WriteAllText(Stamp,JsonUtility.ToJson(new ImportStamp{sourceManifestHash=ManifestHash()},true)+"\n");
            AssetDatabase.ImportAsset(Stamp,ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
        }
        static void Configure(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(!importer)throw new InvalidOperationException("Missing source-finished SchoolWalker FBX: "+path);
            string before=EditorJsonUtility.ToJson(importer);
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.resampleCurves=true;
            importer.isReadable=true;importer.optimizeGameObjects=false;importer.preserveHierarchy=true;
            importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.meshCompression=ModelImporterMeshCompression.Off;importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;importer.addCollider=false;
            importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=4;importer.minBoneWeight=.00001f;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
        }
        static AnimationClip SourceClip(string path)
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            if(clips.Length!=1)throw new InvalidOperationException("Expected one exact authored clip in "+path+", found "+clips.Length);
            return clips[0];
        }
        static AnimationClip SaveClip(AnimationClip source,string name)
        {
            var clip=new AnimationClip{name=name,legacy=true,frameRate=source.frameRate,wrapMode=WrapMode.Loop};
            foreach(var original in AnimationUtility.GetCurveBindings(source))
            {
                var binding=original;binding.path="Model"+(original.path.Length>0?"/"+original.path:"");
                AnimationUtility.SetEditorCurve(clip,binding,AnimationUtility.GetEditorCurve(source,original));
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/"+name+".anim");
            if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);return existing;}
            AssetDatabase.CreateAsset(clip,Folder+"/"+name+".anim");return clip;
        }
        static Material Surface(string prefix,int size)
        {
            var albedo=Texture(prefix+"-baseColor.png",true,false,size);
            var metallic=Texture(prefix+"-metallicSmoothness.png",false,false,size);
            var normal=Texture(prefix+"-normal.png",false,true,size);
            string path=Folder+"/"+prefix+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="SchoolWalker generated PBR "+prefix};AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",albedo);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",metallic);material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale",1);material.SetFloat("_Surface",0);material.SetFloat("_Cull",(float)CullMode.Back);
            material.SetColor("_EmissionColor",Color.black);material.DisableKeyword("_EMISSION");material.enableInstancing=true;
            EditorUtility.SetDirty(material);return material;
        }
        static Texture2D Texture(string name,bool color,bool normal,int size)
        {
            string path=Folder+"/"+name;var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing SchoolWalker source-derived map: "+name);
            string before=EditorJsonUtility.ToJson(importer);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=color;importer.maxTextureSize=size;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=4;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
