using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HappyToy.V2.Editor
{
    public static class LovelyDollAuthoring
    {
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)throw new InvalidOperationException("Open stopped Annex scene");
            if(UnityEngine.Object.FindFirstObjectByType<LovelyDollGuide>())throw new InvalidOperationException("Guide already exists");
            var root=new GameObject("Lovely Doll — friendly guide");root.transform.position=new Vector3(10.6f,0,.6f);
            var model=V1CharacterBuilder.Visual(root.transform,"Lovely_doll","Walking","Walking","Fast Run",1.25f);
            const string dance="Assets/Art/V1/Lovely_doll/Chicken Dance.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(dance);importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.importNormals=ModelImporterNormals.Calculate;importer.SaveAndReimport();
            var wave=UnityEngine.Object.Instantiate(AssetDatabase.LoadAllAssetsAtPath(dance).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__")));
            wave.legacy=true;wave.wrapMode=WrapMode.Loop;AssetDatabase.CreateAsset(wave,"Assets/Annex/LovelyDollWave.anim");
            var animation=model.GetComponent<Animation>();animation.AddClip(wave,"wave");
            var agent=root.AddComponent<NavMeshAgent>();agent.radius=.25f;agent.height=1.25f;agent.acceleration=10;agent.angularSpeed=240;
            var guide=root.AddComponent<LovelyDollGuide>();guide.model=model.transform;guide.animationPlayer=animation;
            var light=new GameObject("Lovely Doll amber guide light").AddComponent<Light>();light.transform.SetParent(root.transform,false);light.transform.localPosition=Vector3.up;
            light.type=LightType.Point;light.color=new Color(1,.67f,.36f);light.range=4;light.intensity=1.8f;guide.guideLight=light;
            var grounding=root.AddComponent<V1RevealGrounding>();grounding.model=model.transform;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
    }
}
