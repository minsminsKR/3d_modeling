using UnityEngine;

namespace HappyToy.V2
{
    // A door owns its one real impact voice, including school and save lifetime.
    [DisallowMultipleComponent]
    public sealed class DoorDestructionAudio : MonoBehaviour
    {
        AudioSource source;
        public int CuesPlayed { get; private set; }
        public AudioSource Source => source;
        public static bool PlayAt(Interactable door)
        {
            var session=GameSession.Current;
            if(!door || !session || !session.InputAllowed || session.Finished)return false;
            var voice=door.GetComponent<DoorDestructionAudio>();
            if(!voice)voice=door.gameObject.AddComponent<DoorDestructionAudio>();
            if(!voice.source)
            {
                var emitter=new GameObject("Broken timber door impact");emitter.transform.SetParent(door.transform,false);
                emitter.transform.localPosition=Vector3.up*.95f;
                voice.source=emitter.AddComponent<AudioSource>();voice.source.playOnAwake=false;
                voice.source.minDistance=3;voice.source.maxDistance=27;voice.source.priority=20;
                EnemyAcoustics.Bind(voice.source,door.transform,.82f,0);
            }
            voice.source.clip=ExternalAudio.Shared("mask-door-smash",0);voice.source.pitch=1;
            if(!voice.source.clip)throw new System.InvalidOperationException("Missing real recorded timber break");
            voice.source.Play();voice.CuesPlayed++;
            PerceivedTension.ReportSound(session,voice.source,voice.source.GetComponent<EnemyAcoustics>(),true);
            if(session.player && Vector3.Distance(session.player.transform.position,door.transform.position)<18)
                session.Shell.ShowCaption("[나무 문이 부서지며 파편이 쏟아지는 소리]",1.5f);
            return true;
        }
        void Update(){var session=GameSession.Current;if(source && (!session || session.Finished))source.Stop();}
        void OnDisable(){if(source)source.Stop();}
    }
}
