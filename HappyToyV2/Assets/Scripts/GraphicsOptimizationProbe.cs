using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit, silent native comparison. No recorder, readback or capture in normal play.
    public sealed class GraphicsOptimizationProbe : MonoBehaviour
    {
        string output; bool finished; float started;
        readonly List<string> errors=new List<string>();
        readonly List<Sample> samples=new List<Sample>();
        [Serializable] sealed class Sample
        {
            public string pose,ssaoSource;public int frames,msaa,visiblePixels,magentaPixels;
            public float medianMs,p95Ms,meanMs;public Vector3 eye,look;public bool reflectionsExcluded=true;
        }
        [Serializable] sealed class Report
        {
            public string status,failure,unity,device,api;
            public string measurement="Controlled paused 1280x720 linear HDR production camera; reflection probes and environment reflections excluded ONLY in this comparison. Render submission plus synchronous one-pixel GPU completion readback, 64 warmup + 128 measured renders per supported pose. Excludes PNG/disk writes. Not displayed FPS, isolated GPU timestamps or reflection validation.";
            public Sample[] samples;public string[] errors;
            public long factoryPlannerBytes,reusedPlannerBytes;public int plannerIterations=1000;
            public bool plannerAllocationCounterAvailable;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-graphics-optimization-output");
            if(i>=0&&i+1<args.Length)new GameObject("Explicit silent graphics optimization probe")
                .AddComponent<GraphicsOptimizationProbe>().output=Path.GetFullPath(args[i+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType kind)
        {if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message);}
        void Update(){if(!finished&&started>0&&Time.realtimeSinceStartup-started>180)Finish("Timed out");}
        IEnumerator Start()
        {
            Application.runInBackground=true;started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(!finished)
            {
                object next=null;bool moved=false;string failure=null;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception error){failure=error.ToString();}
                if(failure!=null){while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Finish(failure);yield break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();if(stack.Count==0){Finish(null);yield break;}continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output,false);yield return null;yield return null;
            var session=GameSession.Current;Require(session&&session.player,"No production session");
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));session.Shell.RestoreDefaultSettings();
            bool school=Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-optimization-school")>=0;
            {
                if(school)session.CreateChapter();else session.CreateCorridor(73);
                session.Shell.Begin();yield return null;yield return null;
                foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))actor.enabled=false;
                foreach(var actor in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(actor is LanternMaskEncounter||actor is WeepingAngelEncounter||actor is StoryDirector||actor is AnnexEncounter||actor is UncatAnnexEvent||actor is V1HwacatEvent)actor.enabled=false;
                foreach(var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))if(agent.isOnNavMesh)agent.isStopped=true;
                session.player.enabled=false;session.player.GetComponent<CharacterController>().enabled=false;session.player.flashlight.enabled=true;
                if(school)
                {
                    yield return Measure(session,"school-ground",new Vector3(-6,0,0),new Vector3(6,1.5f,0));
                    yield return Measure(session,"school-upper",new Vector3(26,5,26),new Vector3(20,6.5f,26));
                }
                else
                {
                    yield return Measure(session,"corridor-joinery",new Vector3(201,.08f,200),new Vector3(211,1.45f,201));
                    yield return Measure(session,"corridor-lamps",new Vector3(202,.08f,247.8f),new Vector3(202.8f,1.25f,242.2f));
                }
            }
        }
        IEnumerator Measure(GameSession session,string pose,Vector3 feet,Vector3 look)
        {
            session.Shell.Resume();
            Require(NavMesh.SamplePosition(feet,out var support,1.2f,NavMesh.AllAreas)&&Mathf.Abs(support.position.y-feet.y)<.7f,"Unsupported observer "+pose);
            var player=session.player;player.transform.position=support.position;
            var yaw=look-player.transform.position;yaw.y=0;player.transform.rotation=Quaternion.LookRotation(yaw);
            player.eyes.transform.rotation=Quaternion.LookRotation(look-player.eyes.transform.position);Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(3.2f);
            var lighting=session.GetComponent<GraphicsLightingPresentation>();
            // Pause also prevents production requests from replacing the explicit
            // warmup request's render id while its six faces are being completed.
            session.Shell.Pause();
            session.GetComponent<LocalShadowBudget>().RefreshNow();
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var renderers=(ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(pipeline);
            var ao=renderers[0].rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            var settings=typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ao);
            string source=settings.GetType().GetField("Source",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance).GetValue(settings).ToString();
            var camera=player.eyes;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){antiAliasing=pipeline.msaaSampleCount};target.Create();
            bool oldEnabled=camera.enabled;camera.enabled=false;
            var pixel=new Texture2D(1,1,TextureFormat.RGBAHalf,false,true);var previous=RenderTexture.active;
            var values=new List<float>(128);var timer=new System.Diagnostics.Stopwatch();
            // Reflection completion is unavailable in this local native fixture.
            // Exclude it explicitly from BOTH comparisons, never claim it passed.
            // Normal game lighting/refresh settings are not changed by this probe.
            var probes=FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            var probeStates=probes.Select(p=>(p.enabled,p.intensity)).ToArray();
            float reflectionIntensity=RenderSettings.reflectionIntensity;
            foreach(var probe in probes){probe.intensity=0;probe.enabled=false;}
            RenderSettings.reflectionIntensity=0;
            int captures=lighting.ReflectionCaptures;
            try
            {
                for(int i=0;i<192;i++)
                {
                    timer.Restart();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    if(target.antiAliasing>1)target.ResolveAntiAliasedSurface();
                    RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();RenderTexture.active=previous;timer.Stop();
                    if(i>=64)values.Add((float)timer.Elapsed.TotalMilliseconds);
                    yield return null;
                }
                Require(lighting.ReflectionCaptures==captures,"Reflection refresh contaminated measured render samples");
                RenderTexture.active=target;var frame=new Texture2D(1280,720,TextureFormat.RGBAHalf,false,true);
                try
                {
                    frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
                    var pixels=GraphicsPresentationAudit.EncodeToneMappedLinearToSrgb(frame.GetPixels());
                    int visible=pixels.Count(x=>Math.Max(x.r,Math.Max(x.g,x.b))>35),magenta=pixels.Count(x=>x.r>180&&x.b>180&&x.g<70);
                    Require(visible>3000&&magenta<100,"Blank or missing shader "+pose+" visible="+visible+" magenta="+magenta);
                    var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    png.SetPixels32(pixels);png.Apply();File.WriteAllBytes(Path.Combine(output,pose+".png"),png.EncodeToPNG());Destroy(png);
                    values.Sort();samples.Add(new Sample{pose=pose,ssaoSource=source,frames=values.Count,msaa=pipeline.msaaSampleCount,visiblePixels=visible,magentaPixels=magenta,
                        medianMs=values[63],p95Ms=values[121],meanMs=values.Average(),eye=camera.transform.position,look=look});
                }
                finally{Destroy(frame);}
            }
            finally
            {
                camera.enabled=oldEnabled;RenderSettings.reflectionIntensity=reflectionIntensity;
                for(int i=0;i<probes.Length;i++)if(probes[i]){probes[i].enabled=probeStates[i].enabled;probes[i].intensity=probeStates[i].intensity;}
                RenderTexture.active=previous;target.Release();Destroy(target);Destroy(pixel);
            }
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;
            var report=new Report{status=failure==null&&errors.Count==0?"PASS":"FAIL",failure=failure,unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,
                api=SystemInfo.graphicsDeviceType.ToString(),samples=samples.ToArray(),errors=errors.ToArray()};
            // Compare the compatible factory with the production reuse path after
            // warming capacity. This is planner allocation, not whole-frame GC.
            var candidates=new List<ShadowAtlasPlan.Candidate>();
            for(int i=0;i<24;i++)candidates.Add(new ShadowAtlasPlan.Candidate{id=i,point=i%3==0,distanceSquared=24-i});
            var plan=new ShadowAtlasPlan();plan.Rebuild(2048,1024,512,true,candidates);
            ShadowAtlasPlan.Build(2048,1024,512,true,candidates);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<report.plannerIterations;i++)ShadowAtlasPlan.Build(2048,1024,512,true,candidates);
            report.factoryPlannerBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<report.plannerIterations;i++)plan.Rebuild(2048,1024,512,true,candidates);
            report.reusedPlannerBytes=GC.GetAllocatedBytesForCurrentThread()-before;
            report.plannerAllocationCounterAvailable=report.factoryPlannerBytes>0;
            File.WriteAllText(Path.Combine(output,"graphics-optimization.json"),JsonUtility.ToJson(report,true));Application.Quit(report.status=="PASS"?0:1);
        }
    }
}
