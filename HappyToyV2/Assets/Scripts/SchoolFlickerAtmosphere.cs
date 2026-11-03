using System;
using UnityEngine;

namespace HappyToy.V2
{
    // The creature owns its spatial breathing and impacts; the listener owns
    // only a low pressure bed and heartbeat that builds as it approaches.
    public sealed class SchoolFlickerAtmosphere : MonoBehaviour
    {
        GameSession session;Transform subject;EnemyAcoustics acoustics;GameObject spatialOwner;
        AudioClip air,heart,breath,lunge;AudioClip[] steps,strains;
        bool active,started,held;long lastFlash;int traversal;float previousDistance;
        public AudioSource AirSource {get;private set;}
        public AudioSource HeartSource {get;private set;}
        public AudioSource BreathSource {get;private set;}
        public AudioSource StepSource {get;private set;}
        public AudioSource StrainSource {get;private set;}
        public AudioSource LungeSource {get;private set;}
        public bool Active=>active;
        public bool Held=>held;
        public int StepsPlayed {get;private set;}
        public int StrainsPlayed {get;private set;}
        public int LungesPlayed {get;private set;}

        public static SchoolFlickerAtmosphere Build(GameSession owner,Transform actor)
        {
            if(!owner||!actor)throw new ArgumentException("Creature atmosphere requires an owner and actor");
            var item=new GameObject("Flicker creature dread audio").AddComponent<SchoolFlickerAtmosphere>();
            item.transform.SetParent(actor,false);item.session=owner;item.subject=actor;
            try
            {
                item.air=ExternalAudio.Required("tension-air");item.heart=ExternalAudio.Required("tension-heart");
                item.breath=ExternalAudio.Required("enemy-cyclopse-breath");
                item.lunge=ExternalAudio.Required("mask-beast-step",0);
                item.steps=new[]{ExternalAudio.Required("mask-heavy-step",0),ExternalAudio.Required("mask-heavy-step",1)};
                item.strains=new[]{ExternalAudio.Required("frame-strain",0),ExternalAudio.Required("frame-strain",1)};
                item.AirSource=item.Make(item.gameObject,item.air,true,false);
                item.HeartSource=item.Make(item.gameObject,item.heart,true,false);
                var emitter=new GameObject("Creature spatial breath and movement");emitter.transform.SetParent(item.transform,false);
                item.spatialOwner=emitter;
                emitter.transform.localPosition=Vector3.up*.8f;
                item.BreathSource=item.Make(emitter,item.breath,true,true);
                item.StepSource=item.Make(emitter,null,false,true);
                item.StrainSource=item.Make(emitter,null,false,true);
                item.LungeSource=item.Make(emitter,null,false,true);
                item.acoustics=EnemyAcoustics.Bind(item.BreathSource,actor,.42f,0);
                EnemyAcoustics.Bind(item.StepSource,actor,.78f,0);
                EnemyAcoustics.Bind(item.StrainSource,actor,.48f,0);
                EnemyAcoustics.Bind(item.LungeSource,actor,.58f,0);
                item.BreathSource.pitch=.76f;emitter.SetActive(false);return item;
            }
            catch{Destroy(item.gameObject);throw;}
        }
        AudioSource Make(GameObject owner,AudioClip clip,bool loop,bool spatial)
        {
            var source=owner.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;
            source.spatialBlend=spatial?1:0;source.minDistance=4;source.maxDistance=42;source.dopplerLevel=0;
            source.priority=65;source.ignoreListenerPause=false;source.ignoreListenerVolume=false;source.volume=0;
            return source;
        }
        public void SetActive(bool value,SchoolWalkerRoute.State state)
        {
            if(active==value)return;active=value;Synchronize(state);
            if(!active)StopAll();
            if(spatialOwner)spatialOwner.SetActive(active);
        }
        public void Synchronize(SchoolWalkerRoute.State state)
        {
            lastFlash=SchoolWalkerFlicker.FlashIndex(state.Elapsed);previousDistance=state.Distance;traversal=state.Traversal;
        }
        public void Tick(SchoolWalkerRoute.State state,bool advancing)
        {
            if(!active)return;
            if(!advancing)
            {
                if(started&&!held){foreach(var source in Sources())source.Pause();held=true;}
                return;
            }
            if(!started){AirSource.Play();HeartSource.Play();BreathSource.Play();started=true;held=false;}
            else if(held){foreach(var source in Sources())source.UnPause();held=false;}
            float distance=session&&session.player?Vector3.Distance(session.player.transform.position,subject.position):42;
            float near=SchoolFlickerDread.Nearness(distance);
            float comfort=session&&session.Shell&&session.Shell.ReducedMotion?.7f:1;
            AirSource.volume=(.035f+.065f*near)*comfort;
            HeartSource.volume=.16f*near*comfort;HeartSource.pitch=Mathf.Lerp(.82f,1.18f,near);
            if(acoustics)acoustics.SetBaseGain(BreathSource,(.35f+.18f*near)*comfort);
            long flash=SchoolWalkerFlicker.FlashIndex(state.Elapsed);
            float travelled=state.Distance-previousDistance;
            if(state.Traversal==traversal&&flash>lastFlash&&travelled>.08f)
            {
                bool lunged=SchoolFlickerDread.Stride(flash)>=1f;
                StepSource.pitch=lunged?.68f:.83f;StepSource.PlayOneShot(steps[StepsPlayed%steps.Length],lunged?.7f:1f);StepsPlayed++;
                if(lunged){LungeSource.pitch=.6f;LungeSource.PlayOneShot(lunge,.7f);LungesPlayed++;}
                if(flash%3==0){StrainSource.pitch=flash%2==0?.68f:.84f;StrainSource.PlayOneShot(strains[StrainsPlayed%strains.Length]);StrainsPlayed++;}
            }
            lastFlash=flash;previousDistance=state.Distance;traversal=state.Traversal;
        }
        AudioSource[] Sources()=>new[]{AirSource,HeartSource,BreathSource,StepSource,StrainSource,LungeSource};
        void StopAll(){foreach(var source in Sources())if(source)source.Stop();started=held=false;}
        void OnDisable()=>StopAll();
        void OnDestroy()
        {
            StopAll();if(air)Destroy(air);if(heart)Destroy(heart);if(breath)Destroy(breath);if(lunge)Destroy(lunge);
            if(steps!=null)foreach(var clip in steps)if(clip)Destroy(clip);
            if(strains!=null)foreach(var clip in strains)if(clip)Destroy(clip);
        }
    }
}
