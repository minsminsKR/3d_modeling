using System;
using UnityEngine;

namespace HappyToy.V2
{
    // The crying remains a real spatial source on the Baby. This quiet subjective
    // soundscape belongs to the witness: only seeing the actor starts the pulse.
    public sealed class SchoolBabyAtmosphere : MonoBehaviour
    {
        GameSession session; Transform subject; AudioClip airClip, pulseClip;
        bool started, held, witnessed;
        float gain;
        public bool Witnessed=>witnessed;
        public void RestoreWitnessed(bool value){witnessed=value;gain=value?1:0;}
        public AudioSource AirSource {get;private set;}
        public AudioSource PulseSource {get;private set;}
        public static SchoolBabyAtmosphere Build(GameSession owner,Transform baby)
        {
            var atmosphere=new GameObject("School Baby witness tension").AddComponent<SchoolBabyAtmosphere>();
            atmosphere.transform.SetParent(baby,false);atmosphere.session=owner;atmosphere.subject=baby;
            try
            {
                atmosphere.airClip=ExternalAudio.Required("tension-air");
                atmosphere.pulseClip=ExternalAudio.Required("tension-heart");
                atmosphere.AirSource=atmosphere.Make(atmosphere.airClip,180);
                atmosphere.PulseSource=atmosphere.Make(atmosphere.pulseClip,170);
                return atmosphere;
            }
            catch{Destroy(atmosphere.gameObject);throw;}
        }
        AudioSource Make(AudioClip clip,int priority)
        {
            var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;
            source.playOnAwake=false;source.spatialBlend=0;source.volume=0;source.priority=priority;
            source.dopplerLevel=0;source.ignoreListenerPause=false;source.ignoreListenerVolume=false;
            return source;
        }
        bool CanSeeBaby()
        {
            if(!session||!session.player||!session.player.eyes||!subject)return false;
            var camera=session.player.eyes;var target=subject.position+Vector3.up*.5f;
            var viewport=camera.WorldToViewportPoint(target);
            if(viewport.z<=0||viewport.x<.08f||viewport.x>.92f||viewport.y<.08f||viewport.y>.92f)return false;
            return !Physics.Linecast(camera.transform.position,target,1<<8,QueryTriggerInteraction.Ignore);
        }
        public void Tick(double elapsed,bool pursuing,bool advancing)
        {
            if(!advancing)
            {
                if(started&&!held){AirSource.Pause();PulseSource.Pause();held=true;}
                return;
            }
            if(!started){AirSource.Play();PulseSource.Play();started=true;}
            else if(held){AirSource.UnPause();PulseSource.UnPause();held=false;}
            if(!witnessed&&CanSeeBaby())witnessed=true;
            float urgency=Mathf.Clamp01((float)(elapsed/SchoolAnomalyEventClock.BabyWaitSeconds));
            // The low air gradually thickens during the minute. The first clear
            // look at Baby brings in the human heartbeat; it stays after turning away.
            float comfort=session.Shell&&session.Shell.ReducedMotion ? .45f : 1;
            gain=Mathf.MoveTowards(gain,witnessed?1:0,Time.deltaTime*1.25f);
            AirSource.volume=(.012f+.023f*urgency)*comfort;
            PulseSource.volume=gain*(.045f+.07f*urgency)*comfort;
            PulseSource.pitch=Mathf.Lerp(.90f,1.15f,urgency);
        }
        public void Stop()
        {
            if(AirSource)AirSource.Stop();if(PulseSource)PulseSource.Stop();started=held=false;
        }
        void OnDisable()=>Stop();
        void OnDestroy(){Stop();if(airClip)Destroy(airClip);if(pulseClip)Destroy(pulseClip);}
    }
}
