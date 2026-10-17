using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.CloudTests
{
    // Explicit, silent authoring export. Every browser step is an actual URP
    // render, including recaptured reflections; the viewer applies no filter.
    internal static class EntranceLightingReviewExport
    {
        [Serializable] sealed class Frame { public float gain; public string file, sha256; }
        [Serializable] sealed class View { public string id, label; public Vector3 position; public Quaternion rotation; public Frame[] frames; }
        [Serializable] sealed class Manifest
        {
            public int version=1,width=1280,height=720;
            public string unity,gameCommit,createdUtc,parameter="Entrance local light multiplier";
            public float defaultGain=1;
            public float[] levels,baseLightIntensities;
            public string[] lightNames;
            public View[] views;
        }

        public static IEnumerator Write(string output,Camera camera,IReadOnlyList<Light> lights,
            Transform chamber,ReflectionProbe[] probes)
        {
            string root=Path.GetFullPath(output),builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            if(!root.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Lighting review exports belong under this project's Builds directory");
            if(Directory.Exists(Path.Combine(root,"frames")))throw new InvalidOperationException("Use a fresh lighting review output");
            Directory.CreateDirectory(Path.Combine(root,"frames"));
            var intensities=lights.Select(light=>light.intensity).ToArray();
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            var manifest=new Manifest {unity=Application.unityVersion,gameCommit=Environment.GetEnvironmentVariable("HAPPYTOY_REVIEW_COMMIT")??"",
                createdUtc=DateTime.UtcNow.ToString("o"),levels=Enumerable.Range(0,41).Select(index=>index*.05f).ToArray(),
                baseLightIntensities=intensities,lightNames=lights.Select(light=>light.name).ToArray(),views=new[]{
                    new View {id="start",label="시작 정면",position=position,rotation=rotation,frames=new Frame[41]},
                    new View {id="gate",label="대문 전체",position=chamber.TransformPoint(new Vector3(0,1.8f,0)),
                        rotation=Quaternion.LookRotation(chamber.TransformPoint(new Vector3(0,4,-12))-chamber.TransformPoint(new Vector3(0,1.8f,0))),frames=new Frame[41]},
                    new View {id="vault",label="천장·암벽",position=position,rotation=Quaternion.Euler(-22,0,0),frames=new Frame[41]}}};
            try
            {
                for(int index=0;index<manifest.levels.Length;index++)
                {
                    float gain=manifest.levels[index];
                    for(int light=0;light<lights.Count;light++)lights[light].intensity=intensities[light]*gain;
                    yield return RefreshReflections(probes);
                    foreach(var view in manifest.views)
                    {
                        camera.transform.SetPositionAndRotation(view.position,view.rotation);
                        string file="frames/"+view.id+"-"+index.ToString("D2")+".jpg";
                        byte[] bytes=Render(camera,manifest.width,manifest.height);
                        File.WriteAllBytes(Path.Combine(root,file),bytes);
                        string digest;using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                        view.frames[index]=new Frame {gain=gain,file=file,sha256=digest};
                    }
                    File.WriteAllText(Path.Combine(root,"progress.json"),"{\"completed\":"+(index+1)+",\"total\":41}");
                    yield return null;
                }
                File.WriteAllText(Path.Combine(root,"manifest.json"),JsonUtility.ToJson(manifest,true));
                Debug.Log("HAPPYTOY_ENTRANCE_LIGHTING_REVIEW_EXPORTED frames=123 output="+root);
            }
            finally
            {
                for(int index=0;index<lights.Count;index++)if(lights[index])lights[index].intensity=intensities[index];
                camera.transform.SetPositionAndRotation(position,rotation);
            }
            yield return RefreshReflections(probes);
        }
        static IEnumerator RefreshReflections(ReflectionProbe[] probes)
        {
            foreach(var probe in probes.Where(probe=>probe))
            {
                int render=probe.RenderProbe();float until=Time.realtimeSinceStartup+15;
                while(!probe.IsFinishedRendering(render))
                {
                    if(Time.realtimeSinceStartup>until)throw new TimeoutException("Lighting review reflection capture stalled");
                    yield return null;
                }
            }
        }
        static byte[] Render(Camera camera,int width,int height)
        {
            var target=new RenderTexture(width,height,24);target.Create();var previous=RenderTexture.active;
            var oldTarget=camera.targetTexture;float aspect=camera.aspect;Texture2D image=null;
            try
            {
                camera.aspect=width/(float)height;camera.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                RenderTexture.active=target;image=new Texture2D(width,height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image.EncodeToJPG(94);
            }
            finally
            {
                camera.targetTexture=oldTarget;camera.aspect=aspect;RenderTexture.active=previous;
                if(image)UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);
            }
        }
    }
}
