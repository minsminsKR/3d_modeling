using System;
using UnityEngine;

namespace HappyToy.V2
{
    // The school ending owns this recording; the imported clip remains a shared resource.
    [DisallowMultipleComponent]
    public sealed class SchoolMorningBirdsong : MonoBehaviour
    {
        public const string ResourcePath="SchoolMorning/birds";
        public const float FullGain=.55f,FadeInSeconds=4f,FadeOutSeconds=1.5f;
        public AudioSource Source {get;private set;}
        bool started,held;

        public void Prepare()
        {
            var clip=Resources.Load<AudioClip>(ResourcePath);
            if(!clip)throw new InvalidOperationException("The recorded school morning birdsong is missing");
            Source=gameObject.AddComponent<AudioSource>();
            Source.playOnAwake=false;Source.loop=true;Source.clip=clip;Source.spatialBlend=0;
            Source.ignoreListenerPause=false;Source.ignoreListenerVolume=false;Source.volume=0;
        }
        public void Tick(float elapsed,bool advancing,float duration)
        {
            if(!Source)return;
            if(!advancing)
            {
                if(started&&!held){Source.Pause();held=true;}
                return;
            }
            if(!started){Source.Play();started=true;}
            else if(held){Source.UnPause();held=false;}
            float fadeIn=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/FadeInSeconds));
            float fadeOut=Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-elapsed)/FadeOutSeconds));
            Source.volume=FullGain*fadeIn*fadeOut;
        }
        public void Stop(){if(Source){Source.Stop();Source.volume=0;}held=false;}
        void OnDisable()=>Stop();
    }
}
