using System;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Owns only the recorded contacts for the unscaled title approach.</summary>
    internal sealed class TitleApproachFootsteps : IDisposable
    {
        readonly GameObject emitter;
        readonly AudioClip[] clips;
        public const float ContactInterval=.32f;
        const int ContactCount=13;
        const float LeadSeconds=.05f,PerformanceSeconds=4.25f;
        readonly AudioClip performance;
        bool active;int reported;
        public AudioSource Source {get;}
        public AudioClip PerformanceClip=>performance;
        public int StepsPlayed { get; private set; }
        public AudioClip LastClip { get; private set; }
        public TitleApproachFootsteps(Transform owner)
        {
            clips=new AudioClip[ExternalAudio.VariantCount(CorridorMaskAudio.ContactCue)];
            if(clips.Length==0)throw new InvalidOperationException("Missing recorded Baeksugwi contacts: "+CorridorMaskAudio.ContactCue);
            for(int i=0;i<clips.Length;i++)clips[i]=ExternalAudio.Required(CorridorMaskAudio.ContactCue,i);
            emitter=new GameObject("Owned title Baeksugwi approach footsteps");emitter.transform.SetParent(owner,false);
            // The title intentionally pauses gameplay. Only this cinematic voice
            // bypasses that pause; user master volume and diagnostic silence remain active.
            Source=emitter.AddComponent<AudioSource>();Source.playOnAwake=false;Source.loop=false;Source.spatialBlend=0;
            Source.ignoreListenerPause=true;Source.ignoreListenerVolume=false;Source.dopplerLevel=0;Source.priority=24;
            // Listener pause freezes Unity's public DSP scheduling clock. Build
            // one short PCM performance from the current in-game recordings;
            // native playback of an ignore-pause voice remains uninterrupted.
            int rate=clips[0].frequency;var data=new float[Mathf.CeilToInt(PerformanceSeconds*rate)];
            for(int contact=0;contact<ContactCount;contact++)
            {
                var clip=clips[contact%clips.Length];var take=new float[clip.samples*clip.channels];
                if(!clip.GetData(take,0))throw new InvalidOperationException("Preload title contact PCM before playback");
                double advance=(contact%2==0?.985:1.015)*clip.frequency/rate;
                int begin=Mathf.RoundToInt((LeadSeconds+contact*ContactInterval)*rate);
                for(int output=0;begin+output<data.Length;output++)
                {
                    double at=output*advance;int frame=(int)at;if(frame+1>=clip.samples)break;
                    float sample=0;
                    for(int channel=0;channel<clip.channels;channel++)sample+=Mathf.Lerp(take[frame*clip.channels+channel],take[(frame+1)*clip.channels+channel],(float)(at-frame));
                    data[begin+output]+=sample/clip.channels;
                }
            }
            performance=AudioClip.Create("Owned title approach / "+CorridorMaskAudio.ContactCue,data.Length,1,rate,false);
            performance.SetData(data,0);Source.clip=performance;
        }
        public void Tick(bool approaching,float distance,float maximum,float fade)
        {
            if(!approaching || fade<=.001f){Stop();return;}
            float gain=Mathf.Lerp(.40f,.92f,Mathf.Clamp01(distance/Mathf.Max(.01f,maximum)))*Mathf.Clamp01(fade);
            Source.volume=gain;
            if(!active)
            {
                active=true;reported=0;Source.Play();
            }
            ObservePlayed();
        }
        void ObservePlayed()
        {
            if(!active)return;
            double seconds=Source.isPlaying?Source.timeSamples/(double)performance.frequency:PerformanceSeconds;
            int heard=Mathf.Clamp((int)Math.Floor((seconds-LeadSeconds)/ContactInterval)+1,0,ContactCount);
            if(heard<=reported)return;
            StepsPlayed+=heard-reported;reported=heard;LastClip=clips[(heard-1)%clips.Length];
        }
        public void Stop(){ObservePlayed();if(Source)Source.Stop();active=false;}
        public void Dispose(){Stop();if(performance)UnityEngine.Object.Destroy(performance);foreach(var clip in clips)if(clip)UnityEngine.Object.Destroy(clip);if(emitter)UnityEngine.Object.Destroy(emitter);}
    }
}
