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
            public AudioSource source;public AudioLowPassFilter filter;public AudioClip ownedClip;public float gain;public bool occluded;
        }
        readonly List<Voice> voices=new List<Voice>();
        public IReadOnlyList<Voice> Voices=>voices;
        public int MixUpdates {get;private set;}
        public float StoryGain {get;private set;}=1;
        float nextTrace;
        Vector3[] runPositions;
        public void ConfigureRunPositions(Vector3[] positions)
        {
            if (positions == null || positions.Length != 3) throw new System.ArgumentException("Expected three spatial ambience positions");
            runPositions = (Vector3[])positions.Clone(); nextTrace = 0;
            for (int i = 0; i < voices.Count; i++) if (voices[i].source) voices[i].source.transform.position = runPositions[i];
        }
        void Start()
        {
            Add("washroom-drip",new Vector3(-5.7f,1.1f,-5.1f),ExternalAudio.Required("ambience-ground"),.28f);
            Add("infirmary-wiring",new Vector3(5.7f,2.6f,5.5f),ExternalAudio.Required("ambience-upper"),.24f);
            // Water drips belong to FloorAtmosphere's one physical leak. This is a
            // distinct air/pipe room bed, so the same recording never doubles in B1.
            Add("classroom-draft",new Vector3(-6.4f,1.8f,7.8f),ExternalAudio.Required("ambience-basement-bed"),.3f);
        }
        void Add(string name,Vector3 position,AudioClip clip,float gain)
        {
            if (runPositions != null) position = runPositions[voices.Count];
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=position;
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.clip=clip;
            source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.2f;source.maxDistance=12;
            source.dopplerLevel=0;source.priority=180;source.volume=0;source.ignoreListenerPause=false;
            var filter=go.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=6000;
            voices.Add(new Voice{source=source,filter=filter,ownedClip=clip,gain=gain});source.Play();
        }

        void Update()
        {
            var session=GameSession.Current;if(!session||!session.InputAllowed)return;
            MixUpdates++;
            // Mix from the player's actual sensory history, never an unseen actor's
            // Chase flag. The short aftermath can persist after the threat moves away.
            var tension=session.player?session.player.GetComponent<PerceivedTension>():null;
            float perceivedGain=tension?tension.AmbienceGain:1;
            StoryGain=Mathf.MoveTowards(StoryGain,session.StoryStep>=4?.25f:perceivedGain,Time.deltaTime*.7f);
            bool trace=Time.time>=nextTrace;if(trace)nextTrace=Time.time+.2f;
            foreach(var voice in voices)
            {
                if(trace)voice.occluded=Physics.Linecast(session.player.eyes.transform.position,voice.source.transform.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                voice.source.volume=Mathf.MoveTowards(voice.source.volume,voice.gain*StoryGain*(voice.occluded?.28f:1),Time.deltaTime*.5f);
                voice.filter.cutoffFrequency=Mathf.MoveTowards(voice.filter.cutoffFrequency,voice.occluded?850:6000,Time.deltaTime*9000);
            }
        }
        void OnDisable(){foreach(var voice in voices)if(voice.source)voice.source.Stop();}
        void OnEnable(){foreach(var voice in voices)if(voice.source)voice.source.Play();}
        // Retain ownership independently of a child emitter's lifetime/order of teardown.
        void OnDestroy(){foreach(var voice in voices)if(voice.ownedClip)Destroy(voice.ownedClip);}
    }
}
