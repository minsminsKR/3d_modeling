using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Optional timing of the same automated route. Not a GPU profiler or hardware-independent benchmark.
    public sealed class RoutePerformance : MonoBehaviour
    {
        readonly List<float> calm=new List<float>(12000),threat=new List<float>(12000);
        string output;
        RenderTexture target;Texture2D readback;float nextSample;
        int sampleIndex;
        readonly List<SlowSample> slowSamples=new List<SlowSample>();
        [Serializable] class SlowSample {public int index,storyStep;public float milliseconds,realtime;public Vector3 playerPosition;public bool firstRender;}
        [Serializable] class Stats {public int frames,over33ms,over50ms;public float p50ms,p95ms,p99ms,maxMs;}
        [Serializable] class Report
        {
            public string device,cpu,graphicsApi,unity,measurement;
            public int width,height,targetFrameRate,vSync;
            public long managedBytes;
            public bool developmentBuild,escaped;
            public Stats beforeThreat,duringThreat;
            public SlowSample[] slowSamples;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-route-performance");
            if(index<0||index+1>=args.Length)return;
            new GameObject("Optional route timing").AddComponent<RoutePerformance>().output=args[index+1];
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!session||!session.player||session.player.MovementUpdates<30||!session.InputAllowed)return;
            if(Time.realtimeSinceStartup<nextSample)return;
            nextSample=Time.realtimeSinceStartup+.1f;
            if(!target){target=new RenderTexture(1280,720,24);target.Create();readback=new Texture2D(1,1,TextureFormat.RGB24,false);}
            var previous=RenderTexture.active;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            RenderPipeline.SubmitRenderRequest(session.player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            // A one-pixel synchronous readback waits for GPU completion, including the rendered scene.
            RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1,1),0,0);readback.Apply();RenderTexture.active=previous;
            timer.Stop();float ms=(float)timer.Elapsed.TotalMilliseconds;
            if(ms>16.667f||sampleIndex==0)slowSamples.Add(new SlowSample{index=sampleIndex,storyStep=session.StoryStep,
                milliseconds=ms,realtime=Time.realtimeSinceStartup,playerPosition=session.player.transform.position,firstRender=sampleIndex==0});
            sampleIndex++;
            (session.StoryStep>=2&&session.StoryStep<4?threat:calm).Add(ms);
        }
        static Stats Summarize(List<float> values)
        {
            values.Sort();var result=new Stats{frames=values.Count};if(values.Count==0)return result;
            float Percent(float q)=>values[Mathf.Clamp(Mathf.CeilToInt(q*values.Count)-1,0,values.Count-1)];
            result.p50ms=Percent(.5f);result.p95ms=Percent(.95f);result.p99ms=Percent(.99f);result.maxMs=values[values.Count-1];
            foreach(float value in values){if(value>33.333f)result.over33ms++;if(value>50)result.over50ms++;}return result;
        }
        void OnApplicationQuit()
        {
            if(string.IsNullOrEmpty(output))return;
            Directory.CreateDirectory(output);
            var report=new Report{device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                unity=Application.unityVersion,width=Screen.width,height=Screen.height,targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,
                managedBytes=GC.GetTotalMemory(false),developmentBuild=Debug.isDebugBuild,escaped=GameSession.Current&&GameSession.Current.Escaped,
                measurement="1280x720 offscreen render plus synchronous one-pixel GPU readback, sampled at most 10Hz; not presented-frame FPS; beforeThreat also includes post-restoration frames",
                beforeThreat=Summarize(calm),duringThreat=Summarize(threat),slowSamples=slowSamples.ToArray()};
            File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
            if(target){target.Release();Destroy(target);}if(readback)Destroy(readback);
        }
    }
}
