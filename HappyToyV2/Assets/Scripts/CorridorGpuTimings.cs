using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Opt-in player timing alongside an actual native input route. The optional
    // offscreen flag explicitly submits the same eye camera; no pixel readback,
    // FPS override, observer teleport or gameplay change is made here.
    public sealed class CorridorGpuTimings : MonoBehaviour
    {
        [Serializable] sealed class Sample
        {
            public int frame,width,height,memories;public bool witnessed;
            public double gpuMs,cpuMs,mainThreadMs,renderThreadMs,presentWaitMs;
            public ulong timestamp;public float realtime;
        }
        [Serializable] sealed class Stats
        {
            public int count;public double median,p95,p99,max;
            public Stats(IEnumerable<double> values)
            {
                var sorted=values.Where(x=>x>0&&!double.IsNaN(x)&&!double.IsInfinity(x)).OrderBy(x=>x).ToArray();
                count=sorted.Length;if(count==0)return;
                median=sorted[(count-1)/2];p95=sorted[Mathf.CeilToInt((count-1)*.95f)];
                p99=sorted[Mathf.CeilToInt((count-1)*.99f)];max=sorted[count-1];
            }
        }
        [Serializable] sealed class Report
        {
            public string device,graphics,unity;
            public string scope="FrameTimingManager CPU/GPU timestamps during active corridor gameplay. offscreenTarget=true explicitly submits the normal production eye camera to a 1920x1080 HDR target each Update when hidden-window automatic rendering is suspended. Automatic renders, if present, add workload. No pixel readback or CPU render stopwatch is used. This conservative GPU workload is not hardware-presented FPS. CPU includes waits/audit overhead. Zero GPU time is unavailable; RTX 3060 is not this measured device.";
            public bool featureEnabled,developmentBuild,gpuAvailable,offscreenTarget;public int skippedDuplicates,unavailableSamples;
            public Stats gpu,cpu,witnessedGpu,lateGpu;public Sample[] samples;
        }
        string output;ulong lastTimestamp;int duplicate,unavailable;bool written,offscreen;
        Camera targetCamera;RenderTexture originalTarget,renderTarget;
        ProfilerRecorder gpuRecorder,mainRecorder;readonly FrameTiming[] timings=new FrameTiming[1];
        readonly List<Sample> samples=new List<Sample>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v4-gpu-timings");
            if(i<0||i+1>=args.Length)return;
            var go=new GameObject("Opt-in actual corridor GPU timings");DontDestroyOnLoad(go);
            var capture=go.AddComponent<CorridorGpuTimings>();capture.output=Path.GetFullPath(args[i+1]);
            capture.offscreen=Array.IndexOf(args,"-v4-offscreen-gpu")>=0;
        }
        void OnEnable()
        {
            // Unity enables frame timings in release players only while this recorder lives.
            gpuRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GPU Frame Time");
            mainRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"CPU Main Thread Frame Time");
        }
        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            var session=GameSession.Current;
            if(!session||!session.CorridorMode||!session.InputAllowed||!session.Corridor.Ready)return;
            if(offscreen&&targetCamera!=session.player.eyes)
            {
                ReleaseTarget();targetCamera=session.player.eyes;originalTarget=targetCamera.targetTexture;
                renderTarget=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
                renderTarget.antiAliasing=targetCamera.allowMSAA?((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount:1;
                renderTarget.name="Opt-in production eye HDR GPU workload";renderTarget.Create();targetCamera.targetTexture=renderTarget;
            }
            if(renderTarget&&targetCamera)
                RenderPipeline.SubmitRenderRequest(targetCamera,new UniversalRenderPipeline.SingleCameraRequest {destination=renderTarget});
            if(FrameTimingManager.GetLatestTimings(1,timings)==0){unavailable++;return;}
            var t=timings[0];if(t.frameStartTimestamp==lastTimestamp){duplicate++;return;}lastTimestamp=t.frameStartTimestamp;
            if(t.gpuFrameTime<=0){unavailable++;return;}
            var threat=session.player.GetComponent<DetectionFeedback>();
            samples.Add(new Sample {frame=Time.frameCount,width=renderTarget?renderTarget.width:Screen.width,height=renderTarget?renderTarget.height:Screen.height,
                memories=session.Corridor.Recovered,witnessed=threat&&threat.WitnessedPressure>0,
                gpuMs=t.gpuFrameTime,cpuMs=t.cpuFrameTime,mainThreadMs=t.cpuMainThreadFrameTime,
                renderThreadMs=t.cpuRenderThreadFrameTime,presentWaitMs=t.cpuMainThreadPresentWaitTime,
                timestamp=t.frameStartTimestamp,realtime=Time.realtimeSinceStartup});
        }
        void Write()
        {
            if(written||string.IsNullOrEmpty(output))return;written=true;
            var report=new Report {device=SystemInfo.graphicsDeviceName,graphics=SystemInfo.graphicsDeviceType.ToString(),unity=Application.unityVersion,
                featureEnabled=FrameTimingManager.IsFeatureEnabled(),developmentBuild=Debug.isDebugBuild,gpuAvailable=samples.Count>0,offscreenTarget=offscreen,
                skippedDuplicates=duplicate,unavailableSamples=unavailable,gpu=new Stats(samples.Select(x=>x.gpuMs)),
                cpu=new Stats(samples.Select(x=>x.cpuMs)),witnessedGpu=new Stats(samples.Where(x=>x.witnessed).Select(x=>x.gpuMs)),
                lateGpu=new Stats(samples.Where(x=>x.memories>=3).Select(x=>x.gpuMs)),samples=samples.ToArray()};
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
        }
        void OnApplicationQuit(){Write();}
        void ReleaseTarget()
        {
            if(targetCamera&&targetCamera.targetTexture==renderTarget)targetCamera.targetTexture=originalTarget;
            if(renderTarget){renderTarget.Release();Destroy(renderTarget);}renderTarget=null;targetCamera=null;originalTarget=null;
        }
        void OnDisable(){Write();ReleaseTarget();gpuRecorder.Dispose();mainRecorder.Dispose();}
    }
}
