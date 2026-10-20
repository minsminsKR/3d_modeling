using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Explicit, silent asset review. Uses the actual owned runtime skin and
    // mouth, without opening a gameplay scene or starting a player.
    public static class UncatCabinetFacePreview
    {
        public static void Render()
        {
            UncatCabinetFaceImport.Ensure();
            const string output="Builds/UncatCabinetFace/review";
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("EnemyStudio/Uncat/UncatRuntime"));
            Resources.Load<AnimationClip>("EnemyStudio/Uncat/patrol").SampleAnimation(root,0);
            foreach(var behaviour in root.GetComponentsInChildren<Behaviour>())behaviour.enabled=false;
            var skin=EnemyStudioGameImport.PrimarySkin(root);
            var brain=root.AddComponent<StalkerBrain>();brain.enabled=false;
            var face=root.AddComponent<EnemyChaseFace>();face.Configure(skin,brain,"ChaseFaces/Uncat/Expression");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            object cabinet=typeof(EnemyChaseFace).GetField("cabinetFace",flags).GetValue(face);
            var tick=cabinet.GetType().GetMethod("Tick");var reset=cabinet.GetType().GetMethod("Reset");
            var apply=typeof(EnemyChaseFace).GetMethod("Apply",flags);
            void Pose(float chase,float gape)
            {
                reset.Invoke(cabinet,null);tick.Invoke(cabinet,new object[]{true,gape*.6f});apply.Invoke(face,new object[]{chase});
                // ExecuteMethod renders many poses in one Editor frame. Reset
                // the skin cache so each capture uses THIS pose's vertices.
                skin.enabled=false;skin.enabled=true;
            }
            var projection=UncatCabinetFaceImport.FaceProfile();var forward=projection.forward.normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;var faceRotation=Quaternion.LookRotation(forward);
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.19f,.22f);
            var key=new GameObject("Cabinet face key light").AddComponent<Light>();key.type=LightType.Directional;
            key.intensity=1.25f;key.color=new Color(1,.88f,.75f);key.transform.rotation=faceRotation*Quaternion.Euler(25,155,0);key.shadows=LightShadows.Soft;
            var fill=new GameObject("Cabinet face cold fill").AddComponent<Light>();fill.type=LightType.Directional;
            fill.intensity=.45f;fill.color=new Color(.53f,.65f,1);fill.transform.rotation=faceRotation*Quaternion.Euler(10,-130,0);
            var camera=new GameObject("Actual cabinet face review").AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=.59f;camera.aspect=1;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.015f,.022f);
            var centre=projection.centre-Vector3.up*.11f;camera.transform.position=centre+forward*2;camera.transform.LookAt(centre);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(768,768,24);target.Create();var image=new Texture2D(768,768,TextureFormat.RGB24,false);
            void Capture(string name)
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());
            }
            try
            {
                Pose(0,0);Capture("neutral");Pose(1,0);Capture("chase");
                Pose(1,1);Capture("gape");
                skin.enabled=false;Capture("gape-without-original");skin.enabled=true;
                var lip=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Owned cabinet resurfaced lips and cheeks");
                lip.enabled=false;Capture("gape-mouth-source");lip.enabled=true;
                camera.transform.position=centre+forward*2+right*.85f+Vector3.up*.04f;camera.transform.LookAt(centre);Capture("gape-angle");
                camera.transform.position=centre+forward*.9f+right*1.7f;camera.transform.LookAt(centre);Capture("gape-side-right");
                camera.transform.position=centre+forward*.9f-right*1.7f;camera.transform.LookAt(centre);Capture("gape-side-left");
                camera.transform.position=centre+forward*2;camera.transform.LookAt(centre);
                for(int frame=0;frame<60;frame++)
                {
                    float t=frame/15f;
                    float chase=Mathf.SmoothStep(0,1,t/.6f);
                    float gape=t<.7f?0:t<1.6f?Mathf.SmoothStep(0,1,(t-.7f)/.9f):t<2.8f?1:1-Mathf.SmoothStep(0,1,(t-2.8f)/1.1f);
                    Pose(chase,gape);Capture($"frame-{frame:000}");
                }
                var head=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Head");
                var trackedCentre=head.InverseTransformPoint(centre);
                foreach(string clipName in new[]{"patrol","chase"})
                {
                    var clip=Resources.Load<AnimationClip>("EnemyStudio/Uncat/"+clipName);
                    clip.SampleAnimation(root,clip.length*.35f);Pose(1,1);
                    var tracked=head.TransformPoint(trackedCentre);camera.transform.position=tracked+forward*2+right*.5f;camera.transform.LookAt(tracked);
                    Capture("gape-pose-"+clipName);
                }
                File.WriteAllText(output+"/review.json","{\"scope\":\"Actual runtime owned mesh, head-parented hinged jaw and 22 individually authored irregular teeth/gums/cavity, lip-landmark-aligned frame, lit front/oblique/both sides and patrol/chase head-pose asset review; no gameplay scene/player launch\",\"frames\":60}");
                Debug.Log("UNCAT_CABINET_FACE_PREVIEW_PASS "+output);
            }
            finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
