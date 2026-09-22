using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2.Editor
{
    public static class V1CharacterBuilder
    {
        public static bool UseCyclopseCandidate { get; set; }
        public static bool UseCyclopseNormalsCandidate { get; set; }
        public static bool UseCyclopseShapeCandidate { get; set; }
        public static bool UseCyclopseShapeSkinCandidate { get; set; }
        static string ModelPath(string key,string file)=>"Assets/Art/"+(key=="Cyclopse"?(UseCyclopseShapeSkinCandidate?"CandidateShapeSkin/":UseCyclopseShapeCandidate?"CandidateShape/":UseCyclopseNormalsCandidate?"CandidateNormals/":UseCyclopseCandidate?"Candidate/":"Refined/"):"V1/")+key+"/"+file+".fbx";
        static AnimationClip ImportClip(string key,string file,string name,bool lockY)
        {
            var path=ModelPath(key,file);var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.importNormals=ModelImporterNormals.Calculate;importer.normalSmoothingAngle=75;
            if(key=="Cyclopse"&&UseCyclopseNormalsCandidate)importer.importNormals=ModelImporterNormals.Import;
            if(key=="Cyclopse"){importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=32;importer.minBoneWeight=.000001f;}
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var clip=UnityEngine.Object.Instantiate(source);clip.name=name;clip.legacy=true;clip.wrapMode=WrapMode.Loop;
            // Port CharacterLoader.prepareLoopingAnimations: remove root travel, retain limb animation.
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var leaf=binding.path.Split('/').Last().ToLowerInvariant();
                if(!(leaf.Contains("hips")||leaf.Contains("pelvis")||leaf.Contains("root")||leaf.Contains("armature")))continue;
                if(!binding.propertyName.StartsWith("m_LocalPosition."))continue;
                if(binding.propertyName.EndsWith(".y")&&!lockY)continue;
                var curve=AnimationUtility.GetEditorCurve(clip,binding);if(curve.keys.Length==0)continue;
                float value=curve.keys[0].value;AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,clip.length,value));
            }
            AssetDatabase.CreateAsset(clip,AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/"+key+"_"+name+".anim"));return clip;
        }
        public static GameObject Visual(Transform parent,string key,string baseFile,string walkFile,string runFile,float height)
        {
            var walking=ImportClip(key,walkFile,"patrol",false);var running=ImportClip(key,runFile,"chase",true);
            if(baseFile!=walkFile&&baseFile!=runFile)ImportClip(key,baseFile,"idle",false);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(key,baseFile));
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(parent,false);
            var anim=model.GetComponent<Animation>();if(!anim)anim=model.AddComponent<Animation>();
            anim.playAutomatically=false;anim.cullingType=AnimationCullingType.AlwaysAnimate;
            anim.AddClip(walking,"patrol");anim.AddClip(running,"chase");anim.clip=walking;
            var bindClip=AssetDatabase.LoadAllAssetsAtPath(ModelPath(key,baseFile)).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            bindClip.SampleAnimation(model,0);
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>();if(renderers.Length==0)throw new Exception(key+" has no skinned mesh");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.localScale*=height/bounds.size.y;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=parent.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="V1 "+key+" textured"};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/V1/"+key+"/model_textured.jpg"));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",.18f);material.SetFloat("_Metallic",0);
            if(key=="Cyclopse")
            {
                // V1's fur atlas should retain its diffuse color in shade, not read as gray plastic.
                material.SetFloat("_SpecularHighlights",0);material.SetFloat("_EnvironmentReflections",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/"+key+"_skin.mat"));
            foreach(var renderer in renderers){renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();renderer.updateWhenOffscreen=true;}
            return model;
        }
        public static StalkerBrain Create(PlayerMotor player,string key="Cyclopse",string walk="Walking",string run="Run",float height=2.15f)
        {
            var root=new GameObject("V1 "+key);root.transform.position=new Vector3(7.5f,0,0);
            var model=Visual(root.transform,key,walk,walk,run,height);
            var agent=root.AddComponent<NavMeshAgent>();agent.height=height;agent.radius=.34f;agent.acceleration=12;
            var brain=root.AddComponent<StalkerBrain>();brain.player=player;
            var motion=root.AddComponent<V1MonsterMotion>();motion.animationPlayer=model.GetComponent<Animation>();motion.model=model.transform;
            return brain;
        }
    }
}
