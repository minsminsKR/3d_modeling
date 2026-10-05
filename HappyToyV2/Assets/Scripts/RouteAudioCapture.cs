using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace HappyToy.V2
{
    // Explicit opt-in listener DSP tap. The audio thread reads the real buffer
    // unchanged and uses managed counters/Stopwatch, never Unity APIs.
    public sealed class RouteAudioCapture : MonoBehaviour
    {
        readonly object gate=new object();
        const int MaximumSeconds=180;
        string output,writerError="",progressMode="legacy-story";short[] pcm,spare;int used,channels,rate,callbacks,nonfinite,nextFile=1;
        long observed,captured,overflowSamples,startedTicks,firstCallbackTicks,lastCallbackTicks;
        sealed class Chunk {public short[] data;public int samples,index,channels;}
        Chunk pending,writingChunk;Task<FileSegment> writing;
        readonly List<FileSegment> files=new List<FileSegment>();
        bool ready,truncated,written,initialized;
        int currentStage,currentFloor;bool paused,steadyPaused;long pauseChangedTicks;
        double dspStart,lastDsp;float gameStart,lastGame;
        readonly long[] counts=new long[6],clips=new long[6],floorCounts=new long[3],floorClips=new long[3];
        readonly double[] sums=new double[6],peaks=new double[6],floorSums=new double[3],floorPeaks=new double[3];
        long pausedSamples,steadyPausedSamples;double pausedSum,pausedPeak,steadyPausedSum,steadyPausedPeak;
        [Serializable] public sealed class Segment
        {public int storyStep;public string name;public long samples,clipped;public double peak,rms;}
        [Serializable] public sealed class PauseEvidence
        {public long samples,steadySamples;public double peak,rms,steadyPeak,steadyRms;public float transitionAllowanceSeconds=.1f;}
        [Serializable] public sealed class FileSegment
        {public string file,sha256;public int samples,channels;public double seconds;}
        [Serializable] public sealed class Report
        {
            public string capture="Unity listener DSP tap, pre-device; natural native callback clock; no device listening certification";
            public int sampleRate,channels,callbacks,nonfiniteSamples,maximumSeconds=MaximumSeconds;
            public long observedSamples,capturedSamples,overflowSamples;
            public double seconds,dspSeconds,callbackWallSeconds,firstCallbackAfterStartSeconds;
            public float gameSeconds;public bool truncated,nonSilent;
            public Segment[] stages,floors;public PauseEvidence pause;
            public string writerError,progressMode;public FileSegment[] files;
        }
        public string OutputDirectory=>output;
        public int CallbackCount {get{lock(gate)return callbacks;}}
        public bool HasCallbacks=>CallbackCount>0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-audio-output");
            if(i>=0 && i+1<args.Length)Attach(args[i+1]);
        }
        public static RouteAudioCapture Attach(string directory)
        {
            var listener=FindFirstObjectByType<AudioListener>();
            if(!listener || !listener.enabled)throw new InvalidOperationException("Natural audio capture requires an active listener");
            var capture=listener.GetComponent<RouteAudioCapture>();
            if(!capture){capture=listener.gameObject.AddComponent<RouteAudioCapture>();capture.output=Path.GetFullPath(directory);}
            capture.Begin();return capture;
        }
        void Start(){Begin();}
        void Begin()
        {
            if(initialized)return;
            Directory.CreateDirectory(output);rate=AudioSettings.outputSampleRate;
            if(rate<=0 || rate>192000)throw new InvalidOperationException("Unbounded/invalid listener sample rate");
            pcm=new short[checked(rate*MaximumSeconds*2)];spare=new short[pcm.Length];
            startedTicks=Stopwatch.GetTimestamp();dspStart=lastDsp=AudioSettings.dspTime;
            gameStart=lastGame=GameSession.Current?GameSession.Current.ElapsedPlayTime:0;
            paused=AudioListener.pause;pauseChangedTicks=startedTicks;
            initialized=true;lock(gate)ready=true;
        }
        void Update()
        {
            if(!initialized || written)return;
            DrainWriter(false);
            var session=GameSession.Current;
            int stage=session?Math.Min(5,session.ChapterMode?session.Chapter.Recovered:session.CorridorMode?session.Corridor.Recovered:session.StoryStep):0;
            float y=session&&session.player?session.player.transform.position.y:0;
            bool pause=AudioListener.pause;long now=Stopwatch.GetTimestamp();
            double dsp=AudioSettings.dspTime;float game=session?session.ElapsedPlayTime:0;
            lock(gate)
            {
                currentStage=stage;currentFloor=y>3?1:y< -2?2:0;
                progressMode=session&&session.ChapterMode?"school-memories":session&&session.CorridorMode?"corridor-memories":"legacy-story";
                if(pause!=paused){paused=pause;pauseChangedTicks=now;}
                steadyPaused=paused&&(now-pauseChangedTicks)/(double)Stopwatch.Frequency>=.1;
                lastDsp=dsp;lastGame=game;
            }
        }
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            long tick=Stopwatch.GetTimestamp();
            lock(gate)
            {
                if(!ready)return;
                callbacks++;observed+=data.Length;
                if(firstCallbackTicks==0)firstCallbackTicks=tick;lastCallbackTicks=tick;
                if(channels==0)channels=channelCount;
                if(channelCount<=0 || channels!=channelCount){truncated=true;return;}
                int stage=currentStage,floor=currentFloor;
                for(int i=0;i<data.Length;i++)
                {
                    double value=data[i];bool finite=!double.IsNaN(value)&&!double.IsInfinity(value);
                    if(!finite){nonfinite++;value=0;}
                    double amplitude=Math.Abs(value),square=value*value;
                    peaks[stage]=Math.Max(peaks[stage],amplitude);sums[stage]+=square;counts[stage]++;
                    floorPeaks[floor]=Math.Max(floorPeaks[floor],amplitude);floorSums[floor]+=square;floorCounts[floor]++;
                    if(!finite || amplitude>=1){clips[stage]++;floorClips[floor]++;}
                    if(paused)
                    {
                        pausedSamples++;pausedSum+=square;pausedPeak=Math.Max(pausedPeak,amplitude);
                        if(steadyPaused){steadyPausedSamples++;steadyPausedSum+=square;steadyPausedPeak=Math.Max(steadyPausedPeak,amplitude);}
                    }
                    int capacity=pcm.Length-pcm.Length%channels;
                    if(used==capacity)
                    {
                        // Two preallocated buffers bound memory. The main thread
                        // dispatches disk work; the audio callback never writes files.
                        if(spare!=null && pending==null)
                        {
                            pending=new Chunk {data=pcm,samples=used,index=nextFile++,channels=channels};
                            pcm=spare;spare=null;used=0;
                        }
                        else {overflowSamples++;truncated=true;continue;}
                    }
                    pcm[used++]=(short)(Math.Max(-1,Math.Min(1,value))*32767);captured++;
                }
            }
        }
        public Report Snapshot()
        {
            lock(gate)
            {
                var report=new Report {sampleRate=rate,channels=channels,callbacks=callbacks,nonfiniteSamples=nonfinite,
                    observedSamples=observed,capturedSamples=captured,overflowSamples=overflowSamples,seconds=channels>0?captured/(double)(rate*channels):0,
                    dspSeconds=lastDsp-dspStart,gameSeconds=lastGame-gameStart,
                    callbackWallSeconds=firstCallbackTicks>0?(lastCallbackTicks-firstCallbackTicks)/(double)Stopwatch.Frequency:0,
                    firstCallbackAfterStartSeconds=firstCallbackTicks>0?(firstCallbackTicks-startedTicks)/(double)Stopwatch.Frequency:-1,
                    truncated=truncated,writerError=writerError,progressMode=progressMode,files=files.ToArray(),stages=new Segment[6],floors=new Segment[3],
                    pause=new PauseEvidence {samples=pausedSamples,steadySamples=steadyPausedSamples,peak=pausedPeak,
                        rms=pausedSamples>0?Math.Sqrt(pausedSum/pausedSamples):0,steadyPeak=steadyPausedPeak,
                        steadyRms=steadyPausedSamples>0?Math.Sqrt(steadyPausedSum/steadyPausedSamples):0}};
                for(int i=0;i<6;i++)
                {report.stages[i]=new Segment{storyStep=i,name="progress-"+i,samples=counts[i],clipped=clips[i],peak=peaks[i],rms=counts[i]>0?Math.Sqrt(sums[i]/counts[i]):0};report.nonSilent|=peaks[i]>.00001;}
                for(int i=0;i<3;i++)report.floors[i]=new Segment{storyStep=i,name=new[]{"ground","upper","basement"}[i],samples=floorCounts[i],clipped=floorClips[i],peak=floorPeaks[i],rms=floorCounts[i]>0?Math.Sqrt(floorSums[i]/floorCounts[i]):0};
                return report;
            }
        }
        public Report Complete()
        {
            lock(gate)ready=false;
            if(written || !initialized)return Snapshot();
            DrainWriter(true);
            if(channels>0 && used>0)
            {
                try{files.Add(WriteChunk(new Chunk{data=pcm,samples=used,index=nextFile++,channels=channels}));}
                catch(Exception error){writerError=error.Message;truncated=true;}
            }
            var report=Snapshot();
            File.WriteAllText(Path.Combine(output,"audio.json"),JsonUtility.ToJson(report,true));written=true;
            return report;
        }
        void DrainWriter(bool finish)
        {
            if(writing!=null && (finish || writing.IsCompleted))
            {
                try{files.Add(writing.GetAwaiter().GetResult());}
                catch(Exception error){writerError=error.Message;truncated=true;}
                lock(gate)spare=writingChunk.data;writing=null;writingChunk=null;
            }
            Chunk next=null;lock(gate){if(writing==null && pending!=null){next=pending;pending=null;}}
            if(next==null)return;
            if(finish)
            {
                try{files.Add(WriteChunk(next));}
                catch(Exception error){writerError=error.Message;truncated=true;}
                lock(gate)spare=next.data;
            }
            else {writingChunk=next;writing=Task.Run(()=>WriteChunk(next));}
        }
        FileSegment WriteChunk(Chunk chunk)
        {
            string name=chunk.index==1?"route.wav":"route-part-"+chunk.index.ToString("D3")+".wav";
            string path=Path.Combine(output,name);
            using(var file=new BinaryWriter(File.Create(path)))
            {
                file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+chunk.samples*2);
                file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)chunk.channels);
                file.Write(rate);file.Write(rate*chunk.channels*2);file.Write((short)(chunk.channels*2));file.Write((short)16);
                file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(chunk.samples*2);
                // A small staging block keeps the writer's extra memory bounded.
                var bytes=new byte[32768];int at=0;
                while(at<chunk.samples)
                {
                    int count=Math.Min(bytes.Length/sizeof(short),chunk.samples-at);
                    Buffer.BlockCopy(chunk.data,at*sizeof(short),bytes,0,count*sizeof(short));file.Write(bytes,0,count*sizeof(short));at+=count;
                }
            }
            string digest;using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            return new FileSegment{file=name,sha256=digest,samples=chunk.samples,channels=chunk.channels,seconds=chunk.samples/(double)(rate*chunk.channels)};
        }
        void OnApplicationQuit(){Complete();}
    }
}
