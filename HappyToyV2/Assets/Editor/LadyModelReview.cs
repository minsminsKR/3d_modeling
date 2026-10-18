using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Explicit, silent asset render. Never launches the game or edits its scene.
    public static class LadyModelReview
    {
        public static void ImportAndRender(){LadyGameImport.Apply();Render();}
        public static void Render()
        {
            const string output="Builds/LadyReview-20261018/unity";
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Lady/LadyRuntime"));
            var clip=Resources.Load<AnimationClip>("Lady/chase");
            UnityEngine.Object.DestroyImmediate(root.GetComponent<LODGroup>());
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.3f,.32f,.36f);
            var key=new GameObject("Lady review key").AddComponent<Light>();key.type=LightType.Directional;
            key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(40,145,0);
            var fill=new GameObject("Lady review fill").AddComponent<Light>();fill.type=LightType.Directional;
            fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(30,-35,0);
            var camera=new GameObject("Lady review camera").AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=1.4f;camera.aspect=2f/3;
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.035f,.038f,.045f);
            camera.transform.position=new Vector3(.5f,1.15f,4);
            camera.transform.LookAt(new Vector3(0,1.05f,0));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(480,720,24);target.Create();
            var image=new Texture2D(480,720,TextureFormat.RGB24,false);
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s=>s.sharedMesh.vertexCount).ToArray();
            clip.SampleAnimation(root,0);
            for(int i=0;i<skins.Length;i++){skins[i].enabled=i==0;skins[i].updateWhenOffscreen=true;}
            root.SetActive(false);root.SetActive(true);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var before=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,480,720),0,0);image.Apply();RenderTexture.active=before;
            File.WriteAllBytes(output+"/skin-live.png",image.EncodeToPNG());
            Debug.Log("LADY_SHARED_BOUNDS "+skins[0].sharedMesh.bounds+" bind="+skins[0].sharedMesh.bindposes[0]+" root="+skins[0].rootBone.name);
            var baked=new Mesh[skins.Length];var renderers=new MeshRenderer[skins.Length];
            for(int i=0;i<skins.Length;i++)
            {
                baked[i]=new Mesh();var holder=new GameObject("Actual Unity baked skin");holder.transform.SetParent(skins[i].transform,false);
                holder.AddComponent<MeshFilter>().sharedMesh=baked[i];renderers[i]=holder.AddComponent<MeshRenderer>();
                renderers[i].sharedMaterials=skins[i].sharedMaterials;renderers[i].enabled=i==0;skins[i].enabled=false;
            }
            try
            {
                int minimumVisible=int.MaxValue;var head=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
                var neutral=head.localRotation;float largestHeadAngle=0;
                for(int frame=0;frame<64;frame++)
                {
                    clip.SampleAnimation(root,.2f+frame*.05f);
                    largestHeadAngle=Mathf.Max(largestHeadAngle,Quaternion.Angle(neutral,head.localRotation));
                    // Explicit multi-frame editor sampling has no player skin
                    // update loop. Bake the actual Unity skin at each pose.
                    for(int i=0;i<skins.Length;i++){EnemyStudioRig.BakeLocalPose(skins[i],baked[i]);baked[i].RecalculateTangents();}
                    if(frame==0)
                    {
                        foreach(var skin in skins)Debug.Log("LADY_REVIEW_SKIN "+skin.name+" transform="+skin.transform.localToWorldMatrix+" bounds="+skin.localBounds);
                        Debug.Log("LADY_REVIEW_BAKED "+baked[0].vertexCount+" bounds="+baked[0].bounds);
                        foreach(var bone in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Head"||t.name=="Hips"||t.name=="Lady_Fit"))
                            Debug.Log("LADY_REVIEW_BONE "+bone.name+" world="+bone.position+" scale="+bone.lossyScale+" rotation="+bone.rotation);
                    }
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var previous=RenderTexture.active;RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,480,720),0,0);image.Apply();RenderTexture.active=previous;
                    var pixels=image.GetPixels32();var background=pixels[0];
                    int visible=pixels.Count(p=>Mathf.Abs(p.r-background.r)+Mathf.Abs(p.g-background.g)+Mathf.Abs(p.b-background.b)>24);
                    minimumVisible=Mathf.Min(minimumVisible,visible);
                    if(visible<3000)throw new InvalidOperationException("Lady render lost the actual skin at frame "+frame);
                    File.WriteAllBytes(output+"/"+frame.ToString("000")+".png",image.EncodeToPNG());
                }
                if(largestHeadAngle<35)throw new InvalidOperationException("Lady's real imported head action did not move");
                File.WriteAllText(output+"/review-report.json","{\"status\":\"PASS\",\"actualUnitySkinFrames\":64,\"minimumVisiblePixels\":"+minimumVisible+",\"largestHeadAngle\":"+largestHeadAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
                Debug.Log("LADY_ACTUAL_UNITY_RENDER_PASS 64 real skinned action frames");
            }
            finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);foreach(var mesh in baked)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
