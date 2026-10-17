using UnityEngine;

namespace HappyToy.V2
{
    // One owned recorded human panting loop at the creature's chest. The AI
    // supplies its state; this voice owns audio, never detection or movement.
    [DisallowMultipleComponent,RequireComponent(typeof(StalkerBrain))]
    public sealed class CyclopseBreathingVoice : MonoBehaviour
    {
        StalkerBrain brain;
        AudioSource voice;
        AudioClip breath;
        EnemyAcoustics acoustics;
        bool chasing;
        float previousTime;
        public AudioSource BreathSource=>voice;
        public int PhrasesStarted {get;private set;}
        public bool FastBreathing=>brain&&brain.state==StalkerBrain.State.Chase;

        void Awake()
        {
            brain=GetComponent<StalkerBrain>();breath=ExternalAudio.Required("enemy-cyclopse-breath");
            var holder=new GameObject("Cyclopse recorded laboured breathing");holder.transform.SetParent(transform,false);
            holder.transform.localPosition=Vector3.up*1.8f;
            voice=holder.AddComponent<AudioSource>();voice.minDistance=2;voice.maxDistance=18;voice.priority=58;
            voice.loop=true;voice.clip=breath;
            acoustics=EnemyAcoustics.Bind(voice,transform,.44f,0);
        }
        void Update()
        {
            // Native time-stop capture owns Pause/UnPause and its sample cursor.
            if(EnemyTimeStop.Active)return;
            var session=GameSession.Current;
            if(!session||session.Finished||session.EncountersResolved||session.StoryStep>=4||!brain||!brain.isActiveAndEnabled||brain.Trapped)
            {Stop();return;}
            if(!session.InputAllowed||AudioListener.pause)return;
            if(chasing!=FastBreathing)
            {
                chasing=FastBreathing;voice.pitch=chasing?1.12f:1;
                acoustics=EnemyAcoustics.Bind(voice,transform,chasing?.66f:.44f,0);
            }
            bool phrase=!voice.isPlaying||voice.time<previousTime;
            if(!voice.isPlaying)voice.Play();
            previousTime=voice.time;
            if(phrase)
            {
                PhrasesStarted++;
                PerceivedTension.ReportSound(session,voice,acoustics,false);
            }
        }
        void Stop(){if(voice)voice.Stop();previousTime=0;}
        void OnDisable()=>Stop();
        void OnDestroy(){Stop();if(breath)Destroy(breath);if(voice)Destroy(voice.gameObject);}
    }
}
