using UnityEngine;

namespace HappyToy.V2
{
    // Pause the world bus while the two existing player locomotion voices and
    // the one transition impact bypass it. No clip, mixer asset or volume reset.
    [DefaultExecutionOrder(700),DisallowMultipleComponent]
    public sealed class LadyFinaleAudio : MonoBehaviour
    {
        static LadyFinaleAudio current;
        GameSession session;AudioSource steps,breath,impact;AudioClip clip;
        bool stepBypass,breathBypass;
        public static bool Active=>current&&current.session&&current.session==GameSession.Current&&!current.session.Finished;
        public AudioSource ImpactSource=>impact;
        public void Begin(GameSession owner,bool playImpact)
        {
            session=owner;current=this;
            steps=owner.player.Feedback.FootstepSource;breath=owner.player.Feedback.BreathingSource;
            stepBypass=steps&&steps.ignoreListenerPause;breathBypass=breath&&breath.ignoreListenerPause;
            impact=gameObject.AddComponent<AudioSource>();impact.playOnAwake=false;impact.spatialBlend=0;
            impact.ignoreListenerVolume=false;impact.ignoreListenerPause=true;impact.volume=.75f;impact.pitch=.78f;
            clip=ExternalAudio.Required("frame-impact",0);impact.clip=clip;
            Apply();if(playImpact)impact.Play();
        }
        void Apply()
        {
            if(!session)return;
            if(!steps){steps=session.player.Feedback.FootstepSource;stepBypass=steps&&steps.ignoreListenerPause;}
            if(!breath){breath=session.player.Feedback.BreathingSource;breathBypass=breath&&breath.ignoreListenerPause;}
            bool playing=session.InputAllowed;
            AudioListener.pause=true;
            if(steps)steps.ignoreListenerPause=playing;
            if(breath)breath.ignoreListenerPause=playing;
            if(impact)impact.ignoreListenerPause=playing;
        }
        void LateUpdate()=>Apply();
        void OnDestroy()
        {
            if(steps)steps.ignoreListenerPause=stepBypass;
            if(breath)breath.ignoreListenerPause=breathBypass;
            if(clip)Destroy(clip);
            if(impact){impact.Stop();Destroy(impact);}
            if(current!=this)return;
            current=null;
            if(!GameSession.Current||GameSession.Current==session)
                AudioListener.pause=session&&!session.InputAllowed;
        }
    }
}
