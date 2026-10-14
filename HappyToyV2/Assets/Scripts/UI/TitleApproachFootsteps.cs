using System;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Owns only the recorded contacts for the unscaled title approach.</summary>
    internal sealed class TitleApproachFootsteps : IDisposable
    {
        readonly GameObject emitter;
        readonly AudioClip[] clips;
        bool active;
        float previousDistance, travel;
        public AudioSource Source { get; }
        public int StepsPlayed { get; private set; }
        public AudioClip LastClip { get; private set; }
        public TitleApproachFootsteps(Transform owner)
        {
            clips=new AudioClip[ExternalAudio.VariantCount("mask-heavy-step")];
            if(clips.Length==0)throw new InvalidOperationException("Missing recorded Baeksugwi heavy contacts");
            for(int i=0;i<clips.Length;i++)clips[i]=ExternalAudio.Required("mask-heavy-step",i);
            emitter=new GameObject("Owned title Baeksugwi approach footsteps");emitter.transform.SetParent(owner,false);
            Source=emitter.AddComponent<AudioSource>();Source.playOnAwake=false;Source.loop=false;Source.spatialBlend=0;
            // The title intentionally pauses gameplay. Only this cinematic voice
            // bypasses that pause; user master volume and diagnostic silence remain active.
            Source.ignoreListenerPause=true;Source.ignoreListenerVolume=false;Source.dopplerLevel=0;Source.priority=24;
        }
        public void Tick(bool approaching,float distance,float maximum,float fade)
        {
            if(!approaching || fade<=.001f){Stop();return;}
            if(!active){active=true;previousDistance=distance;travel=CorridorMaskAudio.ContactStride;}
            travel+=Mathf.Max(0,distance-previousDistance);previousDistance=distance;
            Source.volume=Mathf.Lerp(.40f,.92f,Mathf.Clamp01(distance/Mathf.Max(.01f,maximum)))*Mathf.Clamp01(fade);
            if(travel<CorridorMaskAudio.ContactStride)return;
            // A loading hitch never replays a burst of missed contacts.
            travel%=CorridorMaskAudio.ContactStride;
            LastClip=clips[StepsPlayed%clips.Length];Source.pitch=StepsPlayed%2==0?.985f:1.015f;
            Source.PlayOneShot(LastClip);StepsPlayed++;
        }
        public void Stop(){if(Source && (active || Source.isPlaying))Source.Stop();active=false;travel=0;previousDistance=0;}
        public void Dispose(){Stop();foreach(var clip in clips)if(clip)UnityEngine.Object.Destroy(clip);if(emitter)UnityEngine.Object.Destroy(emitter);}
    }
}
