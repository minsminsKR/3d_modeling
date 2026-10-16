using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Explicit native-route instrumentation. Observes the real screen camera;
    // never submits a render, reads pixels, records audio or changes gameplay.
    public sealed class CorridorPerformanceSampling : MonoBehaviour
    {
        const int WindowCapacity=8192, TotalCapacity=262144, SnapshotCapacity=1280, MaximumWindows=128;
        readonly double[] windowFrames=new double[WindowCapacity],windowCpu=new double[WindowCapacity],windowGpu=new double[WindowCapacity];
        readonly double[] allFrames=new double[TotalCapacity],rolling=new double[2048],sort=new double[WindowCapacity];
        readonly double[] normalFrames=new double[TotalCapacity],normalWindowFrames=new double[WindowCapacity];
        readonly FrameTiming[] timing=new FrameTiming[1];
        readonly List<Window> windows=new List<Window>(MaximumWindows);
        readonly List<Snapshot> snapshots=new List<Snapshot>(SnapshotCapacity);
        readonly List<Counter> counters=new List<Counter>();
        GameSession session;Camera camera;string output;int seed;
        NativeAuditMovement movement;
        bool collecting,completed,hooked;
        int frameCount,cpuCount,gpuCount,totalFrames,callbacks,overflow,lastFrame=-1,rollingHead,rollingCount;
        int unavailableTimings,duplicateTimings,validCpuTimings,validGpuTimings;
        int normalCount,normalWindowCount,normalFramesSinceExclusion=8;
        double normalEligibleSince=-1;
        double lastExcludedWall=double.NegativeInfinity,minimumNormalRollingFps=double.PositiveInfinity,windowMinimumNormalRollingFps=double.PositiveInfinity;
        double began,previous=-1,windowStart,nextSnapshot,nextLog,rollingSeconds,minimumRollingFps=double.PositiveInfinity,windowMinimumRollingFps=double.PositiveInfinity;
        ulong lastTimestamp;FrameTiming latestTiming;
        Objects before;

        [Serializable] public sealed class Distribution
        {
            public int samples,over16_67ms,over20ms,over33_33ms,over50ms;
            public double p50ms,p95ms,p99ms,maxMs,meanMs,below60FramePercent;
        }
        [Serializable] public sealed class Window
        {
            public double fromSeconds,toSeconds,minimumRollingOneSecondFps;
            public int records,targetFrameRate,vSync;public Vector3 position;
            public Distribution renderedIntervals,cpuFrame,gpuFrame;
            public Distribution normalSpeedRenderedIntervals;
            // Legacy exclusion-count alias retained for existing report readers.
            public int acceleratedOrTransitionIntervals,excludedFromNormalSpeedIntervals;
            public double minimumNormalSpeedRollingOneSecondFps;
        }
        [Serializable] public sealed class CounterValue {public string name,unit;public bool available;public long value=-1;}
        [Serializable] public sealed class CounterInfo {public string name,unit,unavailableReason;public bool available;}
        sealed class Counter
        {
            public CounterInfo info;public ProfilerRecorder recorder;public bool sampled;
            public CounterValue Read()
            {
                bool valid=recorder.Valid&&recorder.IsRunning&&recorder.Count>0;
                if(valid)sampled=true;
                return new CounterValue{name=info.name,unit=info.unit,available=valid,value=valid?recorder.LastValue:-1};
            }
        }
        [Serializable] public sealed class Snapshot
        {
            public double seconds,inventoryWorkMilliseconds;
            public int records,targetFrameRate,vSync,generation0Collections,generation1Collections,generation2Collections;
            public Vector3 position;
            public long managedBytes,monoUsedBytes,totalAllocatedBytes,totalReservedBytes;
            public bool cpuTimingAvailable,gpuTimingAvailable;
            public ulong frameStartTimestamp;
            public double cpuFrameMs,cpuMainThreadMs,cpuRenderThreadMs,presentWaitMs,gpuFrameMs;
            public CounterValue[] counters;
            public bool movementBoostActive,itemWheelOpen,enemyTimeStopActive;
            public float movementMultiplier,walkSpeed,runSpeed,timeScale;
        }
        [Serializable] public sealed class Objects
        {
            public int loadedObjects,gameObjects,sceneGameObjects,renderers,materials,meshes,textures;
            public double workMilliseconds;
        }
        [Serializable] public sealed class Report
        {
            public string status,failure,unity,device,cpu,graphicsApi;
            public string measurement="Actual main-camera endCameraRendering wall intervals during active route; one callback per engine frame. No additional rendering, readback, PNG, PCM or per-frame file writes. renderedOverall retains acceleration, slow wheel and global enemy-stop gameplay; only normalSpeedRenderedOverall and clean normal-speed rolling windows describe unaltered gameplay pace. Diagnostic acceleration, item-wheel slow time, global enemy time stop, pause and their transitions are excluded from that subset. Transitions exclude at least 8 real frames and .25s; rolling history must lie wholly after that stabilization. Delayed CPU/GPU samples are diagnostic context, with heuristic transition exclusion; their gameplay-clock attribution is unverified. Not hardware presentation timestamps. FrameTimingManager values are delayed cached CPU/GPU measurements; zero/unsupported values are unavailable independently.";
            public string inventoryScope="Loaded-object enumeration occurs only before collection and after collection stops; includes imported assets and inactive objects. Per-second memory/counter snapshots, 30-second logs and the unchanged route's milestone logs/SaveEvidence JSON writes have observer cost included in measured intervals. No forced GC. Counter values unavailable on this build are -1, never zero evidence.";
            public bool routeCompleted,measurementValid,developmentBuild,frameTimingEnabled;
            public bool headless,nullGraphicsDevice;
            public bool normalSpeedMeasurementValid;
            public bool cpuGpuNormalSpeedAttributionVerified=false;
            public int seed,width,height,qualityLevel,targetFrameRate,vSync,actualCameraCallbacks,renderedIntervals,overflowedSamples,unavailableTimings,duplicateTimings,validCpuTimings,validGpuTimings;
            public double durationSeconds,minimumRollingOneSecondFps;
            public ulong cpuTimerFrequency;
            public Distribution renderedOverall;
            public Distribution normalSpeedRenderedOverall;
            public double minimumNormalSpeedRollingOneSecondFps;
            public int excludedFromNormalSpeedIntervals;
            public NativeAuditMovement.Evidence movement;
            public Window[] windows;public Snapshot[] snapshots;public CounterInfo[] counters;
            public Objects before,after;
        }

        public static CorridorPerformanceSampling Attach(GameSession owner,string directory,int routeSeed)
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-corridor-performance")<0)
                throw new InvalidOperationException("Performance sampling requires the explicit native performance flag");
            var sampler=new GameObject("Explicit corridor performance sampling").AddComponent<CorridorPerformanceSampling>();
            sampler.transform.SetParent(owner.transform,false);
            sampler.session=owner;sampler.camera=owner.player.eyes;sampler.output=directory;sampler.seed=routeSeed;
            sampler.ConfigureCounters();sampler.before=Inventory();
            return sampler;
        }
        void ConfigureCounters()
        {
            var available=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(available);
            foreach(string name in new[]{"GPU Frame Time","CPU Main Thread Frame Time","GC Allocated In Frame","GC.Alloc","Total Used Memory","System Used Memory","Object Count","Game Object Count","Material Count","Mesh Count","Texture Count"})
            {
                var counter=new Counter{info=new CounterInfo{name=name,unit="unavailable",unavailableReason="Counter not exposed by this build"}};
                foreach(var handle in available)
                {
                    var description=ProfilerRecorderHandle.GetDescription(handle);
                    if(description.Name!=name)continue;
                    counter.info.unit=description.UnitType.ToString();
                    try
                    {
                        counter.recorder=ProfilerRecorder.StartNew(description.Category,name,1);
                        counter.info.available=counter.recorder.Valid&&counter.recorder.IsRunning;
                        counter.info.unavailableReason=counter.info.available?"":"Recorder could not start";
                    }
                    catch(Exception error){counter.info.unavailableReason=error.GetType().Name;}
                    break;
                }
                counters.Add(counter);
            }
        }
        public void Begin()
        {
            if(collecting||completed)throw new InvalidOperationException("Sampling can start only once");
            began=Now();windowStart=began;nextSnapshot=began+1;nextLog=began+30;
            collecting=true;RenderPipelineManager.endCameraRendering+=Rendered;hooked=true;
        }
        public void ObserveMovement(NativeAuditMovement source){movement=source;}
        static double Now()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        static bool Valid(double value)=>value>0&&!double.IsNaN(value)&&!double.IsInfinity(value);
        bool NormalGameplayPace => session && session.InputAllowed &&
            !(movement && movement.Active) && Mathf.Approximately(Time.timeScale,1f) &&
            !(session.Shell && session.Shell.ItemWheelOpen) && !EnemyTimeStop.Active;
        void ExcludePace(double now)
        {lastExcludedWall=now;normalFramesSinceExclusion=0;normalEligibleSince=-1;}
        void SuspendPace(double now)
        {ExcludePace(now);previous=-1;rollingHead=rollingCount=0;rollingSeconds=0;}
        bool ObservePace(double now,bool normalGameplay)
        {
            if(!normalGameplay){ExcludePace(now);return false;}
            normalFramesSinceExclusion++;
            bool stable=normalFramesSinceExclusion>=8 && now-lastExcludedWall>=.25;
            if(stable && normalEligibleSince<0)normalEligibleSince=now;
            return stable;
        }
        void Update()
        {
            if(!collecting)return;
            double now=Now();
            if(!session||!session.InputAllowed){SuspendPace(now);return;}
            if(!NormalGameplayPace)ExcludePace(now);
            if(now>=nextSnapshot)
            {
                nextSnapshot=now+1;
                if(snapshots.Count<SnapshotCapacity)snapshots.Add(Memory(now));else overflow++;
            }
            if(now>=nextLog)
            {
                nextLog=now+30;
                Debug.Log("HAPPYTOY_CORRIDOR_PERFORMANCE_LIVE seconds="+(now-began).ToString("F1")+" callbacks="+callbacks+
                    " records="+session.RecordsRecovered+" rollingMinimumFps="+Minimum(minimumRollingFps).ToString("F2")+
                    " managedBytes="+GC.GetTotalMemory(false)+" windows="+windows.Count);
            }
        }
        void Rendered(ScriptableRenderContext context,Camera renderedCamera)
        {
            if(!collecting||renderedCamera!=camera||renderedCamera.targetTexture||Time.frameCount==lastFrame)return;
            double now=Now();
            if(!session||!session.InputAllowed){SuspendPace(now);return;}
            lastFrame=Time.frameCount;callbacks++;
            // The raw route includes slow-time and time-stop gameplay. Only
            // the unaltered subset can represent normal-pace frame intervals.
            bool normalSpeed=ObservePace(now,NormalGameplayPace);
            if(previous>=0)
            {
                double seconds=now-previous;
                if(Valid(seconds))
                {
                    Add(windowFrames,ref frameCount,seconds*1000);
                    Add(allFrames,ref totalFrames,seconds*1000);
                    if(normalSpeed){Add(normalFrames,ref normalCount,seconds*1000);Add(normalWindowFrames,ref normalWindowCount,seconds*1000);}
                    Rolling(seconds,normalSpeed,now);
                }
            }
            previous=now;
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,timing)==0)unavailableTimings++;
            else if(timing[0].frameStartTimestamp==lastTimestamp)duplicateTimings++;
            else
            {
                latestTiming=timing[0];lastTimestamp=latestTiming.frameStartTimestamp;
                // CPU remains useful when the graphics API cannot supply GPU time.
                if(Valid(latestTiming.cpuFrameTime)){if(normalSpeed)Add(windowCpu,ref cpuCount,latestTiming.cpuFrameTime);validCpuTimings++;}
                if(Valid(latestTiming.gpuFrameTime)){if(normalSpeed)Add(windowGpu,ref gpuCount,latestTiming.gpuFrameTime);validGpuTimings++;}
            }
            if(now-windowStart>=10)CloseWindow(now);
        }
        void Add(double[] buffer,ref int count,double value)
        {if(count<buffer.Length)buffer[count++]=value;else overflow++;}
        void Rolling(double seconds,bool normalSpeed,double now)
        {
            if(rollingCount==rolling.Length){rollingSeconds-=rolling[rollingHead];rollingHead=(rollingHead+1)%rolling.Length;rollingCount--;overflow++;}
            rolling[(rollingHead+rollingCount)%rolling.Length]=seconds;rollingCount++;rollingSeconds+=seconds;
            while(rollingCount>1&&rollingSeconds-rolling[rollingHead]>=1)
            {rollingSeconds-=rolling[rollingHead];rollingHead=(rollingHead+1)%rolling.Length;rollingCount--;}
            if(rollingSeconds>=1)
            {
                double fps=rollingCount/rollingSeconds;
                minimumRollingFps=Math.Min(minimumRollingFps,fps);windowMinimumRollingFps=Math.Min(windowMinimumRollingFps,fps);
                if(normalSpeed && now-lastExcludedWall>=1.25 && normalEligibleSince>=0 && now-rollingSeconds>=normalEligibleSince)
                {minimumNormalRollingFps=Math.Min(minimumNormalRollingFps,fps);windowMinimumNormalRollingFps=Math.Min(windowMinimumNormalRollingFps,fps);}
            }
        }
        static double Minimum(double value)=>double.IsInfinity(value)?-1:value;
        void CloseWindow(double now)
        {
            if(frameCount>0)
            {
                if(windows.Count<MaximumWindows)windows.Add(new Window{fromSeconds=windowStart-began,toSeconds=now-began,
                    minimumRollingOneSecondFps=Minimum(windowMinimumRollingFps),records=session?session.RecordsRecovered:0,position=session?session.player.transform.position:Vector3.zero,
                    targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,
                    normalSpeedRenderedIntervals=Summary(normalWindowFrames,normalWindowCount,sort),acceleratedOrTransitionIntervals=frameCount-normalWindowCount,excludedFromNormalSpeedIntervals=frameCount-normalWindowCount,
                    minimumNormalSpeedRollingOneSecondFps=Minimum(windowMinimumNormalRollingFps),
                    renderedIntervals=Summary(windowFrames,frameCount,sort),cpuFrame=Summary(windowCpu,cpuCount,sort),gpuFrame=Summary(windowGpu,gpuCount,sort)});
                else overflow++;
            }
            frameCount=cpuCount=gpuCount=normalWindowCount=0;windowStart=now;windowMinimumRollingFps=windowMinimumNormalRollingFps=double.PositiveInfinity;
        }
        static Distribution Summary(double[] source,int count,double[] scratch)
        {
            var result=new Distribution{samples=count};if(count==0)return result;
            // Each distribution sorts an existing scratch array; the source is retained.
            Array.Copy(source,scratch,count);Array.Sort(scratch,0,count);
            double sum=0;
            for(int i=0;i<count;i++)
            {
                double value=scratch[i];sum+=value;
                if(value>1000d/60)result.over16_67ms++;if(value>20)result.over20ms++;
                if(value>1000d/30)result.over33_33ms++;if(value>50)result.over50ms++;
            }
            result.p50ms=scratch[Math.Max(0,(int)Math.Ceiling(count*.5)-1)];
            result.p95ms=scratch[Math.Max(0,(int)Math.Ceiling(count*.95)-1)];
            result.p99ms=scratch[Math.Max(0,(int)Math.Ceiling(count*.99)-1)];
            result.maxMs=scratch[count-1];result.meanMs=sum/count;result.below60FramePercent=100d*result.over16_67ms/count;
            return result;
        }
        Snapshot Memory(double now)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var values=new CounterValue[counters.Count];for(int i=0;i<counters.Count;i++)values[i]=counters[i].Read();
            var result=new Snapshot{seconds=now-began,records=session.RecordsRecovered,position=session.player.transform.position,
                targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,
                generation0Collections=GC.CollectionCount(0),generation1Collections=GC.CollectionCount(1),generation2Collections=GC.CollectionCount(2),
                managedBytes=GC.GetTotalMemory(false),monoUsedBytes=Profiler.GetMonoUsedSizeLong(),
                totalAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),totalReservedBytes=Profiler.GetTotalReservedMemoryLong(),
                frameStartTimestamp=latestTiming.frameStartTimestamp,cpuTimingAvailable=Valid(latestTiming.cpuFrameTime),gpuTimingAvailable=Valid(latestTiming.gpuFrameTime),
                cpuFrameMs=Valid(latestTiming.cpuFrameTime)?latestTiming.cpuFrameTime:-1,
                cpuMainThreadMs=Valid(latestTiming.cpuFrameTime)?latestTiming.cpuMainThreadFrameTime:-1,
                cpuRenderThreadMs=Valid(latestTiming.cpuFrameTime)?latestTiming.cpuRenderThreadFrameTime:-1,
                presentWaitMs=Valid(latestTiming.cpuFrameTime)?latestTiming.cpuMainThreadPresentWaitTime:-1,
                gpuFrameMs=Valid(latestTiming.gpuFrameTime)?latestTiming.gpuFrameTime:-1,counters=values};
            result.movementBoostActive=movement&&movement.Active;result.movementMultiplier=movement?movement.AppliedMultiplier:1;
            result.timeScale=Time.timeScale;result.itemWheelOpen=session.Shell && session.Shell.ItemWheelOpen;result.enemyTimeStopActive=EnemyTimeStop.Active;
            result.walkSpeed=session.player.walkSpeed;result.runSpeed=session.player.runSpeed;
            result.inventoryWorkMilliseconds=timer.Elapsed.TotalMilliseconds;return result;
        }
        static Objects Inventory()
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();var result=new Objects();
            foreach(var item in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                result.loadedObjects++;
                if(item is GameObject gameObject){result.gameObjects++;if(gameObject.scene.IsValid())result.sceneGameObjects++;}
                if(item is Renderer)result.renderers++;if(item is Material)result.materials++;
                if(item is Mesh)result.meshes++;if(item is Texture)result.textures++;
            }
            result.workMilliseconds=timer.Elapsed.TotalMilliseconds;return result;
        }
        public void Complete(bool routePassed,string routeFailure)
        {
            if(completed)return;completed=true;collecting=false;Unhook();double now=Now();CloseWindow(now);
            bool headless=Application.isBatchMode,nullGraphics=SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null;
            bool valid=callbacks>=300&&overflow==0&&!headless&&!nullGraphics;
            var information=new CounterInfo[counters.Count];
            for(int i=0;i<counters.Count;i++)
            {
                information[i]=counters[i].info;
                if(!counters[i].sampled){information[i].available=false;if(string.IsNullOrEmpty(information[i].unavailableReason))information[i].unavailableReason="Recorder returned no completed samples";}
            }
            var report=new Report{status=!valid?"MEASUREMENT_FAILED":routePassed?"PASS":"ROUTE_FAILED",
                failure=!valid?(headless||nullGraphics?"Headless/null-graphics execution cannot validate foreground gameplay frame rate":
                    callbacks<300?"Fewer than 300 actual main-camera callbacks; hidden/nonrendering window is not a measurement":"Bounded sampling capacity exceeded"):routeFailure,
                unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                routeCompleted=routePassed,measurementValid=valid,developmentBuild=Debug.isDebugBuild,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),
                headless=headless,nullGraphicsDevice=nullGraphics,
                cpuTimerFrequency=FrameTimingManager.GetCpuTimerFrequency(),seed=seed,width=Screen.width,height=Screen.height,qualityLevel=QualitySettings.GetQualityLevel(),
                targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,actualCameraCallbacks=callbacks,renderedIntervals=totalFrames,overflowedSamples=overflow,
                unavailableTimings=unavailableTimings,duplicateTimings=duplicateTimings,validCpuTimings=validCpuTimings,validGpuTimings=validGpuTimings,
                durationSeconds=now-began,minimumRollingOneSecondFps=Minimum(minimumRollingFps),renderedOverall=Summary(allFrames,totalFrames,allFrames),
                excludedFromNormalSpeedIntervals=totalFrames-normalCount,normalSpeedMeasurementValid=valid&&normalCount>=300,normalSpeedRenderedOverall=Summary(normalFrames,normalCount,normalFrames),
                minimumNormalSpeedRollingOneSecondFps=Minimum(minimumNormalRollingFps),movement=movement?movement.Data:null,
                windows=windows.ToArray(),snapshots=snapshots.ToArray(),counters=information,before=before,after=Inventory()};
            DisposeCounters();Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"corridor-performance.json"),JsonUtility.ToJson(report,true));
            Debug.Log("HAPPYTOY_CORRIDOR_PERFORMANCE_"+report.status+" callbacks="+callbacks+" p95ms="+report.renderedOverall.p95ms.ToString("F2"));
        }
        void Unhook(){if(hooked){RenderPipelineManager.endCameraRendering-=Rendered;hooked=false;}}
        void DisposeCounters(){foreach(var counter in counters)if(counter.recorder.Valid)counter.recorder.Dispose();}
        void OnDisable(){Unhook();DisposeCounters();}
    }
}
