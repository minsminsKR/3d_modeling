using System;
using System.IO;
using UnityEngine;

namespace HappyToy.V2
{
    // Opt-in listener DSP tap; leaves the audio buffer unchanged. Not a microphone/device recording.
    public sealed class RouteAudioCapture:MonoBehaviour
    {
        readonly object gate=new object();
        string output;short[] pcm;int used,channels,rate;volatile int stage;bool ready,truncated;
        readonly long[] counts=new long[5],clips=new long[5];
        readonly double[] sums=new double[5],peaks=new double[5];
        [Serializable]class Segment {public int storyStep;public long samples,clipped;public double peak,rms;}
        [Serializable]class Report {public string capture="Unity listener DSP tap, pre-device; no subjective listening certification";public int sampleRate,channels;public double seconds;public bool truncated,nonSilent;public Segment[] stages;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-audio-output");if(i<0||i+1>=args.Length)return;
            var listener=FindFirstObjectByType<AudioListener>();
            if(!listener){Debug.LogError("Audio capture requires an active listener");return;}
            listener.gameObject.AddComponent<RouteAudioCapture>().output=args[i+1];
        }
        void Start()
        {Directory.CreateDirectory(output);rate=AudioSettings.outputSampleRate;pcm=new short[rate*120*2];lock(gate)ready=true;}
        void Update(){if(GameSession.Current)stage=Math.Min(4,GameSession.Current.StoryStep);}
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(gate)
            {
                if(!ready)return;
                if(channels==0)channels=channelCount;
                if(channels!=channelCount){truncated=true;return;}
                int step=stage;
                int length=Math.Min(data.Length,pcm.Length-used);length-=length%channels;
                if(length<data.Length)truncated=true;
                for(int i=0;i<length;i++)
                {
                    double v=data[i];
                    if(double.IsNaN(v)||double.IsInfinity(v)){clips[step]++;v=0;}
                    double amplitude=Math.Abs(v);peaks[step]=Math.Max(peaks[step],amplitude);
                    sums[step]+=v*v;counts[step]++;if(amplitude>=1)clips[step]++;
                    pcm[used++]=(short)(Math.Max(-1,Math.Min(1,v))*32767);
                }
            }
        }
        void OnApplicationQuit()
        {
            lock(gate)ready=false;
            if(pcm==null)return;
            var report=new Report{sampleRate=rate,channels=channels,seconds=channels>0?used/(double)(rate*channels):0,truncated=truncated,stages=new Segment[5]};
            for(int i=0;i<5;i++)
            {report.stages[i]=new Segment{storyStep=i,samples=counts[i],clipped=clips[i],peak=peaks[i],rms=counts[i]>0?Math.Sqrt(sums[i]/counts[i]):0};report.nonSilent|=peaks[i]>.00001;}
            if(channels>0)
            {
                using(var file=new BinaryWriter(File.Create(Path.Combine(output,"route.wav"))))
                {
                    file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+used*2);
                    file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)channels);
                    file.Write(rate);file.Write(rate*channels*2);file.Write((short)(channels*2));file.Write((short)16);
                    file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(used*2);
                    for(int i=0;i<used;i++)file.Write(pcm[i]);
                }
            }
            File.WriteAllText(Path.Combine(output,"audio.json"),JsonUtility.ToJson(report,true));
        }
    }
}
