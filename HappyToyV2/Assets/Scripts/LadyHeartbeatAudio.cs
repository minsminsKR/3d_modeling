using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns two recorded voices. A shorter lub/dub plays at a changing interval,
    // keeping its original pitch; the terminal tone is one confirmed-hit cue.
    [DefaultExecutionOrder(720),DisallowMultipleComponent]
    public sealed class LadyHeartbeatAudio : MonoBehaviour
    {
        Transform player;Func<Transform> pursuer;Func<bool> playing,presentation;
        readonly LadyHeartbeatClock clock=new LadyHeartbeatClock();
        AudioSource heartbeat,flatline;AudioClip beatClip,flatlineClip;
        public AudioSource HeartbeatSource=>heartbeat;
        public AudioSource FlatlineSource=>flatline;
        public float Bpm=>clock.Bpm;
        public int Beats=>clock.Beats;
        public bool Captured {get;private set;}
        public void Prepare(Transform listener,Func<Transform> lady,Func<bool> canPlay,Func<bool> canPresentDeath)
        {
            player=listener;pursuer=lady;playing=canPlay;presentation=canPresentDeath;
            beatClip=ExternalAudio.Required("lady-heartbeat");flatlineClip=ExternalAudio.Required("lady-flatline");
            heartbeat=Voice(beatClip,0);flatline=Voice(flatlineClip,.48f);
        }
        AudioSource Voice(AudioClip clip,float gain)
        {
            var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
            source.ignoreListenerVolume=false;source.ignoreListenerPause=false;source.loop=false;
            source.pitch=1;source.volume=gain;source.clip=clip;return source;
        }
        public bool MarkCaught()
        {
            if(Captured)return false;Captured=true;clock.Stop();heartbeat.Stop();heartbeat.volume=0;
            flatline.ignoreListenerPause=presentation();flatline.Play();return true;
        }
        void Update()
        {
            if(!heartbeat||!player)return;
            bool run=!Captured&&playing();heartbeat.ignoreListenerPause=run;
            flatline.ignoreListenerPause=Captured&&presentation();
            if(!run)return;
            var lady=pursuer();float distance=lady?Vector3.Distance(player.position,lady.position):32;
            bool beat=clock.Tick(Time.unscaledDeltaTime,distance,true);
            heartbeat.volume=clock.Gain;if(beat)heartbeat.Play();
        }
        void OnDestroy()
        {
            if(heartbeat){heartbeat.Stop();Destroy(heartbeat);}if(flatline){flatline.Stop();Destroy(flatline);}
            if(beatClip)Destroy(beatClip);if(flatlineClip)Destroy(flatlineClip);
        }
    }
}
