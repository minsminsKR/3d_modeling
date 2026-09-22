using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Opt-in manual review. Render callback intervals are not GPU times or hardware presentation timestamps.
    public sealed class PlayReviewTiming:MonoBehaviour
    {
        static bool installed;string output;long previous;int lastFrame=-1,unfocused,menuFrames,width,height,resolutionChanges;
        bool eligibleBefore;GameSession lastSession;
        readonly List<float> calm=new List<float>(),threat=new List<float>();
        [Serializable]class Stats {public int samples;public float p50ms,p95ms,p99ms,maxMs;}
        [Serializable]class Report {public string measurement="Focused main-camera render callback intervals during manual gameplay; not GPU time or hardware-presented FPS",device,cpu;public int width,height,resolutionChanges,unfocusedCallbacks,menuCallbacks,targetFrameRate,vSync;public bool enoughSamples,developmentBuild;public Stats calm,threat;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-play-review");if(i<0||i+1>=args.Length||installed)return;
            installed=true;var go=new GameObject("Manual play review timing");DontDestroyOnLoad(go);go.AddComponent<PlayReviewTiming>().output=args[i+1];
        }
        void OnEnable(){RenderPipelineManager.endCameraRendering+=Rendered;}
        void OnDisable(){RenderPipelineManager.endCameraRendering-=Rendered;}
        void Rendered(ScriptableRenderContext context,Camera camera)
        {
            var session=GameSession.Current;
            if(!session||camera!=session.player.eyes||camera.targetTexture||Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(width!=Screen.width||height!=Screen.height){if(width>0)resolutionChanges++;width=Screen.width;height=Screen.height;eligibleBefore=false;}
            bool focused=Application.isFocused,playing=session.InputAllowed;
            if(!focused)unfocused++;if(!playing)menuFrames++;
            bool eligible=focused&&playing;
            if(eligible&&eligibleBefore&&lastSession==session)
            {
                float ms=(float)((now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                var samples=session.StoryStep>=2&&session.StoryStep<4?threat:calm;
                if(samples.Count<72000)samples.Add(ms);
            }
            previous=now;eligibleBefore=eligible;lastSession=session;
        }
        static Stats Summarize(List<float> values)
        {
            values.Sort();var stats=new Stats{samples=values.Count};if(values.Count==0)return stats;
            float At(float p)=>values[Mathf.Clamp(Mathf.CeilToInt(values.Count*p)-1,0,values.Count-1)];
            stats.p50ms=At(.5f);stats.p95ms=At(.95f);stats.p99ms=At(.99f);stats.maxMs=values[values.Count-1];return stats;
        }
        void OnApplicationQuit()
        {
            Directory.CreateDirectory(output);
            var report=new Report{device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,width=width,height=height,resolutionChanges=resolutionChanges,
                unfocusedCallbacks=unfocused,menuCallbacks=menuFrames,targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,
                enoughSamples=calm.Count>=300&&threat.Count>=300,developmentBuild=Debug.isDebugBuild,calm=Summarize(calm),threat=Summarize(threat)};
            File.WriteAllText(Path.Combine(output,"manual-timing.json"),JsonUtility.ToJson(report,true));
        }
    }
}
