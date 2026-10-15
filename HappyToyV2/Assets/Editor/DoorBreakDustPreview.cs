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
    // Explicit silent asset rendering; does not run gameplay or launch a player.
    public static class DoorBreakDustPreview
    {
        public static void Render()
        {
            const string output="Builds/DoorDust/review";Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Dust review stage");
            var shader=Resources.Load<Shader>("DoorBreakDust");
            if(!shader)throw new InvalidOperationException("Missing dust shader");
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.26f,.27f,.30f);
            var key=new GameObject("Dust warm key").AddComponent<Light>();key.transform.SetParent(root.transform);
            key.type=LightType.Directional;key.intensity=1.5f;key.color=new Color(1,.88f,.7f);key.transform.rotation=Quaternion.Euler(42,-30,0);
            var fill=new GameObject("Dust cool fill").AddComponent<Light>();fill.transform.SetParent(root.transform);
            fill.type=LightType.Point;fill.intensity=3;fill.range=8;fill.transform.position=new Vector3(0,1.8f,2.7f);
            var surface=new Material(Shader.Find("Universal Render Pipeline/Lit"));surface.color=new Color(.12f,.14f,.17f);
            GameObject Box(string name,Vector3 at,Vector3 size,Material material)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(root.transform,false);
                box.transform.localPosition=at;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=material;return box;
            }
            Box("Floor",new Vector3(0,-.1f,0),new Vector3(2.8f,.2f,7),surface);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Hall wall",new Vector3(side*1.5f,1.5f,0),new Vector3(.2f,3,7),surface);
                Box("Door post",new Vector3(side*1.36f,1.2f,0),new Vector3(.14f,2.4f,.14f),surface);
            }
            Box("Lintel",new Vector3(0,2.72f,0),new Vector3(2.8f,.56f,.24f),surface);
            var leaf=Box("Original door leaf",new Vector3(0,1.18f,0),new Vector3(2.58f,2.36f,.12f),Resources.Load<Material>("GraphicsPbr/wood-aged/material"));
            UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
            var door=new GameObject("Door floor origin");door.transform.SetParent(root.transform,false);
            var debris=DoorShatterDebris.Create(door.transform,new[]{leaf.GetComponent<Renderer>()},"dust-review");
            leaf.GetComponent<Renderer>().enabled=false;debris.Launch(Vector3.forward);
            var dust=debris.GetComponentInChildren<DoorBreakDust>();if(!dust)throw new InvalidOperationException("Launch did not emit dust");
            var advance=typeof(DoorBreakDust).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            var pose=typeof(DoorShatterDebris).GetMethod("Pose",BindingFlags.Instance|BindingFlags.NonPublic);
            var camera=new GameObject("Dust review camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
            camera.enabled=false;camera.nearClipPlane=.03f;camera.farClipPlane=20;camera.fieldOfView=60;
            camera.transform.position=new Vector3(.15f,1.4f,4.15f);camera.transform.LookAt(new Vector3(0,1.05f,0));
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.023f,.03f);
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.renderPostProcessing=false;
            var target=new RenderTexture(1000,760,24);target.Create();var image=new Texture2D(1000,760,TextureFormat.RGB24,false);
            int peak=0;
            try
            {
                for(int frame=0;frame<49;frame++)
                {
                    float age=(frame+1)/12f;
                    advance.Invoke(dust,new object[]{1/12f});
                    if(age<1.15f)pose.Invoke(debris,new object[]{age/1.15f});else debris.Seal();
                    peak=Mathf.Max(peak,dust.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount));
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1000,760),0,0);image.Apply();RenderTexture.active=previous;
                    var png=image.EncodeToPNG();File.WriteAllBytes(output+"/frame-"+frame.ToString("000")+".png",png);
                    if(frame==4)File.WriteAllBytes(output+"/impact-dust.png",png);
                    if(frame==16)File.WriteAllBytes(output+"/settling-dust.png",png);
                    if(frame==48)File.WriteAllBytes(output+"/cleared.png",png);
                }
                if(ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("Dust shader did not compile");
                if(peak<40 || dust.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)!=0)throw new InvalidOperationException("Dust did not emit and expire");
                File.WriteAllText(output+"/review.json","{\"frames\":49,\"peakParticles\":"+peak+",\"remainingParticles\":0,\"colliders\":"+dust.GetComponentsInChildren<Collider>().Length+"}");
                Debug.Log("DOOR_BREAK_DUST_REVIEW_PASS peak="+peak);
            }
            finally
            {
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(surface);
            }
        }
    }
}
