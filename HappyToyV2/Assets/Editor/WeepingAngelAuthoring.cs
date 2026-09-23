using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2.Editor
{
    public static class WeepingAngelAuthoring
    {
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)throw new InvalidOperationException("Open stopped Annex scene");
            if(UnityEngine.Object.FindFirstObjectByType<WeepingAngelEncounter>())throw new InvalidOperationException("Mannequin already exists");
            var root=new GameObject("V1 silent mannequin — eastern loop");root.transform.position=new Vector3(39.4f,0,8);
            var pivot=new GameObject("Turning mannequin").transform;pivot.SetParent(root.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/V1/Mannequin/Mannequin.fbx"));model.transform.SetParent(pivot,false);
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new InvalidOperationException("No mannequin meshes");
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.localScale*=1.85f/bounds.size.y;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.position+=root.transform.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/V1/Mannequin/Image_0.png"));mat.SetFloat("_Smoothness",.22f);
            AssetDatabase.CreateAsset(mat,"Assets/Annex/MannequinSkin.mat");
            foreach(var r in renderers){var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=mat;r.sharedMaterials=mats;}
            var agent=root.AddComponent<NavMeshAgent>();agent.height=1.85f;agent.radius=.38f;agent.speed=1.3f;agent.acceleration=20;agent.updateRotation=false;agent.stoppingDistance=.7f;
            var encounter=root.AddComponent<WeepingAngelEncounter>();encounter.visual=pivot;
            var lamp=new GameObject("Mannequin display lamp").AddComponent<Light>();lamp.transform.position=new Vector3(39.4f,2.6f,7.3f);lamp.transform.SetParent(root.transform,true);
            lamp.type=LightType.Point;lamp.color=new Color(.73f,.79f,1);lamp.range=5;lamp.intensity=1.6f;encounter.displayLight=lamp;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
    }
}
