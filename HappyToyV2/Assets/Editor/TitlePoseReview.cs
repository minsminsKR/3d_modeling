using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HappyToy.V2.Editor
{
    public static class TitlePoseReview
    {
        public static void Inspect()
        {
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(CinematicTitleScene.CreatureResource));
            var cameraObject=new GameObject("Owned title face inspection camera");
            RenderTexture target=null; Texture2D image=null; Material material=null;
            try
            {
                foreach(var b in root.GetComponentsInChildren<Behaviour>())b.enabled=false;
                Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace").SampleAnimation(root,0);
                var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var before=EnemyStudioRig.PosedBounds(skins);
                root.transform.rotation=Quaternion.Euler(0,180,0)*root.transform.rotation;
                var bounds=EnemyStudioRig.PosedBounds(skins);
                var head=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
                var points=skins.SelectMany(EnemyStudioRig.PosedVertices).ToArray();
                // The front-facing mask occupies the foremost central face region.
                float minZ=points.Min(v=>v.z);float width=bounds.size.x;
                var front=points.Where(v=>Mathf.Abs(v.x-head.position.x)<width*.16f && v.z<minZ+width*.20f).ToArray();
                var face=new Bounds(front[0],Vector3.zero);foreach(var point in front)face.Encapsulate(point);
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetTexture("_BaseMap",Resources.Load<Texture2D>("Baeksugwi/albedo"));
                foreach(var skin in skins){skin.sharedMaterials=skin.sharedMaterials.Select(_=>material).ToArray();skin.gameObject.layer=29;}
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
                camera.orthographicSize=face.size.y*.65f;camera.aspect=1;camera.cullingMask=1<<29;
                camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.position=face.center+new Vector3(0,0,-5);camera.transform.LookAt(face.center);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                target=new RenderTexture(1024,1024,24);target.Create();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                var old=RenderTexture.active;RenderTexture.active=target;
                image=new Texture2D(1024,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();RenderTexture.active=old;
                Directory.CreateDirectory("Builds/TitleGrounding-Work");File.WriteAllBytes("Builds/TitleGrounding-Work/face-probe.png",image.EncodeToPNG());
                File.WriteAllText("Builds/TitleGrounding-Work/pose-probe.json",JsonUtility.ToJson(new Probe{before=before,bounds=bounds,face=face,head=head.position,viewCenter=face.center,orthographicSize=camera.orthographicSize,headMatrix=head.worldToLocalMatrix},true));
                // Surface raycasts are software triangle intersections, without colliders or saved scene edits.
                var rays=new[]{new Vector2(.409f,.565f),new Vector2(.539f,.565f)};
                foreach(var uv in rays)
                {
                    var ray=camera.ViewportPointToRay(uv);float nearest=float.PositiveInfinity;Vector3 hit=default;
                    foreach(var skin in skins)
                    {
                        var verts=EnemyStudioRig.PosedVertices(skin);var triangles=skin.sharedMesh.triangles;
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            var a=verts[triangles[i]];var e1=verts[triangles[i+1]]-a;var e2=verts[triangles[i+2]]-a;
                            var p=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,p);if(Mathf.Abs(determinant)<1e-8f)continue;
                            var t=ray.origin-a;float u=Vector3.Dot(t,p)/determinant;if(u<0||u>1)continue;
                            var q=Vector3.Cross(t,e1);float v=Vector3.Dot(ray.direction,q)/determinant;if(v<0||u+v>1)continue;
                            float d=Vector3.Dot(e2,q)/determinant;if(d>0&&d<nearest){nearest=d;hit=ray.GetPoint(d);}
                        }
                    }
                    Debug.Log("TITLE_SURFACE "+uv+" world="+hit.ToString("F6")+" headLocal="+head.InverseTransformPoint(hit).ToString("F6"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);
                if(material)UnityEngine.Object.DestroyImmediate(material);if(image)UnityEngine.Object.DestroyImmediate(image);
                if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
        }
        [Serializable]class Probe{public Bounds before,bounds,face;public Vector3 head,viewCenter;public float orthographicSize;public Matrix4x4 headMatrix;}
    }
}
