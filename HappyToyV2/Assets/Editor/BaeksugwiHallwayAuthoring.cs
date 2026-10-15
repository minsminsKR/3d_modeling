using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    public static class BaeksugwiHallwayAuthoring
    {
        const string Output="Builds/BaeksugwiHallway/review";
        public static void Ensure()
        {
            var profile=AssetDatabase.LoadAssetAtPath<BaeksugwiHallwayPresentation>("Assets/Resources/Baeksugwi/HallwayPresentation.asset");
            if(!profile || profile.scale.x<=1 || profile.scale.y<=1 || profile.targetHeight>=2.44f || profile.targetWidth>=2.58f)
                throw new InvalidOperationException("Missing reviewed hallway visual profile");
        }
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var source=Resources.Load<GameObject>("Baeksugwi/Baeksugwi");
            var model=UnityEngine.Object.Instantiate(source);
            try
            {
                var run=Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
                var head=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                var mesh=new Mesh();var envelope=new Bounds();bool first=true;
                try
                {
                    for(int gait=0;gait<4;gait++)for(int pose=0;pose<8;pose++)
                    {
                        run.SampleAnimation(model,gait*run.length/4);
                        head.SampleAnimation(model,pose*head.length/8);
                        var b=Bounds(model,mesh);
                        if(first){envelope=b;first=false;}else envelope.Encapsulate(b);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
                const string path="Assets/Resources/Baeksugwi/HallwayPresentation.asset";
                var profile=AssetDatabase.LoadAssetAtPath<BaeksugwiHallwayPresentation>(path);
                if(!profile){profile=ScriptableObject.CreateInstance<BaeksugwiHallwayPresentation>();AssetDatabase.CreateAsset(profile,path);}
                profile.sourceEnvelope=envelope;
                profile.scale=new Vector3(profile.targetWidth/envelope.size.x,profile.targetHeight/envelope.size.y,1.05f);
                profile.offset=new Vector3(-envelope.center.x*profile.scale.x,-envelope.min.y*profile.scale.y,0);
                EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
                File.WriteAllText("SourceArt/BaeksugwiStudio/hallway-presentation.json",JsonUtility.ToJson(profile,true));
                run.SampleAnimation(model,0);head.SampleAnimation(model,0);
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.23f,.25f);
                var key=new GameObject("Hallway key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.8f;key.transform.rotation=Quaternion.Euler(38,-145,0);
                var fill=new GameObject("Cold fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.color=new Color(.65f,.75f,1);fill.transform.rotation=Quaternion.Euler(15,35,0);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review floor";floor.transform.position=new Vector3(0,-.1f,-1);floor.transform.localScale=new Vector3(2.8f,.2f,10);
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.2f,.17f,.14f);floor.GetComponent<Renderer>().sharedMaterial=material;
                for(int side=-1;side<=1;side+=2)
                {
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(side*1.5f,1.5f,-1);wall.transform.localScale=new Vector3(.2f,3,10);wall.GetComponent<Renderer>().sharedMaterial=material;
                }
                var camera=new GameObject("Hallway review camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.fieldOfView=64;
                camera.transform.position=new Vector3(0,1.45f,5.1f);camera.transform.LookAt(new Vector3(0,1.1f,-.65f));
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.018f,.023f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var target=new RenderTexture(1000,800,24);target.Create();var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
                void Capture(string name)
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();RenderTexture.active=previous;
                    File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
                }
                try
                {
                    Capture("before");
                    var body=new GameObject("Visual-only fit").transform;
                    model.transform.SetParent(body,false);BaeksugwiHallwayPresentation.Fit(body,model.transform,body);
                    Capture("after");
                    var frame=new GameObject("Actual corridor door dimensions");frame.transform.position=Vector3.forward*.85f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.transform.SetParent(frame.transform,false);
                        post.transform.localPosition=new Vector3(side*1.36f,1.2f,0);post.transform.localScale=new Vector3(.14f,2.4f,.14f);post.GetComponent<Renderer>().sharedMaterial=material;
                    }
                    var lintel=GameObject.CreatePrimitive(PrimitiveType.Cube);lintel.transform.SetParent(frame.transform,false);
                    lintel.transform.localPosition=new Vector3(0,2.72f,0);lintel.transform.localScale=new Vector3(2.8f,.56f,.24f);lintel.GetComponent<Renderer>().sharedMaterial=material;
                    Capture("fitted-doorway");
                    var leaf=GameObject.CreatePrimitive(PrimitiveType.Cube);leaf.transform.SetParent(frame.transform,false);
                    leaf.transform.localPosition=new Vector3(0,1.18f,0);leaf.transform.localScale=new Vector3(2.6f,2.36f,.12f);
                    var panel=leaf.GetComponent<Renderer>();panel.sharedMaterial=Resources.Load<Material>("GraphicsPbr/wood-aged/material");
                    UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
                    var debris=DoorShatterDebris.Create(frame.transform,new[]{panel},"review-door");panel.enabled=false;
                    debris.Launch(Vector3.forward);
                    var pose=typeof(DoorShatterDebris).GetMethod("Pose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    pose.Invoke(debris,new object[]{.35f});Capture("impact-pieces");
                    debris.Seal();Capture("settled-doorway");
                    camera.transform.position=new Vector3(.6f,2.1f,3);camera.transform.LookAt(new Vector3(0,.08f,.85f));Capture("rubble-close");
                    File.WriteAllText(Output+"/rubble.json","{\"pieces\":"+debris.PieceCount+",\"settledRenderers\":"+debris.GetComponentsInChildren<Renderer>().Length+",\"colliders\":"+debris.GetComponentsInChildren<Collider>().Length+"}");
                    Debug.Log("BAEKSUGWI_HALLWAY_FIT_PASS source="+envelope+" scale="+profile.scale);
                }
                finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);}
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
        }
        public static Bounds Bounds(GameObject model,Mesh mesh)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.BakeMesh(mesh,false);var inherited=skin.transform.parent?skin.transform.parent.lossyScale:Vector3.one;
                foreach(var p in mesh.vertices)
                {
                    var point=skin.transform.TransformPoint(new Vector3(p.x/inherited.x,p.y/inherited.y,p.z/inherited.z));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
