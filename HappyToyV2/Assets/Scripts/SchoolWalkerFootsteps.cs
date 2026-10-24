using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Recorded human shoe contacts, driven by the same travelled-metres phase
    // as the authored two-step walk. No enemy sensing or synthetic attack cue.
    [DisallowMultipleComponent]
    public sealed class SchoolWalkerFootsteps : MonoBehaviour
    {
        public const float NearDistance=3,AudibleDistance=38;
        AudioClip[] contacts;
        AudioSource source;
        float halfStride,previousDistance;
        int traversal;
        bool synchronized;
        public int StepsPlayed {get;private set;}
        public AudioSource Source=>source;
        public AudioClip LastClip {get;private set;}
        public void Prepare(float metresPerCycle)
        {
            if(contacts!=null||!StealthRules.Finite(metresPerCycle)||metresPerCycle<=.1f)
                throw new ArgumentException("One school walker footstep owner needs its authored stride");
            halfStride=metresPerCycle*.5f;
            contacts=new AudioClip[ExternalAudio.VariantCount("step-stone")];
            if(contacts.Length==0)throw new InvalidOperationException("Recorded school tile contacts are missing");
            for(int i=0;i<contacts.Length;i++)contacts[i]=ExternalAudio.Required("step-stone",i);
            var emitter=new GameObject("School pedestrian recorded tile footsteps");
            emitter.transform.SetParent(transform,false);emitter.transform.localPosition=Vector3.up*.12f;
            source=emitter.AddComponent<AudioSource>();source.minDistance=NearDistance;source.maxDistance=AudibleDistance;
            source.priority=65;source.loop=false;source.pitch=1;
            EnemyAcoustics.Bind(source,transform,.85f,0);
        }
        public void Synchronize(SchoolWalkerRoute.State state)
        {
            previousDistance=state.Distance;traversal=state.Traversal;synchronized=true;
            if(source)source.Stop();
        }
        public void Apply(SchoolWalkerRoute.State state,bool advancing)
        {
            if(!source||!isActiveAndEnabled)return;
            float moved=state.Distance-previousDistance;
            if(!synchronized||state.Traversal!=traversal||moved<0||moved>SchoolWalkerRoute.WalkSpeed*.101f)
            {Synchronize(state);return;}
            if(!advancing||moved<=.000001f)source.Stop();
            else if(Mathf.FloorToInt((state.Distance+.00001f)/halfStride)>
                Mathf.FloorToInt((previousDistance+.00001f)/halfStride))
            {
                LastClip=contacts[StepsPlayed%contacts.Length];source.PlayOneShot(LastClip);StepsPlayed++;
            }
            previousDistance=state.Distance;
        }
        void OnDisable(){if(source)source.Stop();synchronized=false;}
        void OnDestroy()
        {
            if(source){source.Stop();Destroy(source.gameObject);}
            if(contacts!=null)foreach(var contact in contacts)if(contact)Destroy(contact);
        }
    }
}
