using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Compact-room adaptation of V1 SoundManager's wiring drone and wet_drip.
    // Spatial sources belong to real rooms; there are no arbitrary pan-only fake enemy cues.
    public sealed class RoomAmbience:MonoBehaviour
    {
        public sealed class Voice
        {
            public AudioSource source;public AudioLowPassFilter filter;public float gain;public bool occluded;
        }
        readonly List<Voice> voices=new List<Voice>();
        public IReadOnlyList<Voice> Voices=>voices;
        public int MixUpdates {get;private set;}
        public float StoryGain {get;private set;}=1;
        float nextTrace;
        StalkerBrain[] enemies;
        void Start()
        {
            Add("washroom-drip",new Vector3(-5.7f,1.1f,-5.1f),MakeClip("V1 wet drip adaptation",6,0),.42f);
            Add("infirmary-wiring",new Vector3(5.7f,2.6f,5.5f),MakeClip("V1 wiring adaptation",4,1),.32f);
            Add("classroom-draft",new Vector3(-6.4f,1.8f,7.8f),MakeClip("Classroom paper air",6,2),.28f);
            enemies=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        }
        void Add(string name,Vector3 position,AudioClip clip,float gain)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=position;
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.clip=clip;
            source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.2f;source.maxDistance=12;
            source.dopplerLevel=0;source.priority=180;source.volume=0;source.ignoreListenerPause=false;
            var filter=go.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=6000;
            voices.Add(new Voice{source=source,filter=filter,gain=gain});source.Play();
        }
        static AudioClip MakeClip(string name,int seconds,int kind)
        {
            const int rate=24000;var data=new float[rate*seconds];var random=new System.Random(821+kind);float smooth=0;
            var dropTimes=new[]{.8f,2.9f,5.1f};
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,v=0;
                if(kind==0)
                {
                    foreach(float start in dropTimes)
                    {
                        float q=t-start;if(q<0||q>.28f)continue;
                        // V1 wet_drip falls from about 240 Hz to 64 Hz; integrate the exponential sweep.
                        float k=Mathf.Log(64f/240)/.2f,phase=240*(Mathf.Exp(k*q)-1)/k;
                        v+=.28f*Mathf.Sin(2*Mathf.PI*phase)*Mathf.Exp(-25*q)*Mathf.Clamp01(q/.004f);
                    }
                }
                else if(kind==1)
                    v=.065f*(Mathf.Sin(2*Mathf.PI*41.5f*t)+Mathf.Sin(2*Mathf.PI*43*t))+.025f*Mathf.Sin(2*Mathf.PI*50*t)+.012f*Mathf.Sin(2*Mathf.PI*100*t);
                else
                {
                    smooth=Mathf.Lerp(smooth,(float)random.NextDouble()*2-1,.09f);
                    v=smooth*.14f*(.35f+.65f*Mathf.Pow(Mathf.Sin(Mathf.PI*t/seconds),2));
                }
                float seam=Mathf.Clamp01(Mathf.Min(t,seconds-t)/.025f);data[i]=v*seam;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void Update()
        {
            var session=GameSession.Current;if(!session||!session.InputAllowed)return;
            MixUpdates++;bool chasing=false;
            foreach(var enemy in enemies)if(enemy&&enemy.isActiveAndEnabled&&enemy.state==StalkerBrain.State.Chase)chasing=true;
            StoryGain=Mathf.MoveTowards(StoryGain,session.StoryStep>=4?.25f:chasing?.5f:1,Time.deltaTime*.7f);
            bool trace=Time.time>=nextTrace;if(trace)nextTrace=Time.time+.2f;
            foreach(var voice in voices)
            {
                if(trace)voice.occluded=Physics.Linecast(session.player.eyes.transform.position,voice.source.transform.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                voice.source.volume=Mathf.MoveTowards(voice.source.volume,voice.gain*StoryGain*(voice.occluded?.28f:1),Time.deltaTime*.5f);
                voice.filter.cutoffFrequency=Mathf.MoveTowards(voice.filter.cutoffFrequency,voice.occluded?850:6000,Time.deltaTime*9000);
            }
        }
        void OnDestroy(){foreach(var voice in voices)if(voice.source&&voice.source.clip)Destroy(voice.source.clip);}
    }
}
