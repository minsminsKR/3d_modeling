using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class StoryDirector : MonoBehaviour
    {
        public StalkerBrain stalker;
        public Light[] corridorLights;
        public Transform emptyChair;
        AudioSource source;
        AudioClip bell;
        AudioClip chairScrape;
        public bool RestorationChairCompleted { get; private set; }
        public int RestorationChairCues { get; private set; }
        void Start()
        {
            stalker.gameObject.SetActive(false);
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=2;source.maxDistance=22;
            // Original synthesized cracked-school-bell cue; no borrowed soundtrack.
            const int rate=24000;var samples=new float[rate*2];
            for(int i=0;i<samples.Length;i++)
            {float t=i/(float)rate;samples[i]=.18f*Mathf.Exp(-3*t)*(Mathf.Sin(2*Mathf.PI*540*t)+.35f*Mathf.Sin(2*Mathf.PI*839*t));}
            bell=AudioClip.Create("Cracked dismissal bell",samples.Length,1,rate,false);bell.SetData(samples,0);
            var scrape=new float[(int)(rate*.72f)];var random=new System.Random(431);float previous=0;
            for(int i=0;i<scrape.Length;i++)
            {
                float t=i/(float)rate;float noise=(float)(random.NextDouble()*2-1);
                float rasp=noise-previous*.8f;previous=noise;
                float envelope=Mathf.Sin(Mathf.PI*i/(scrape.Length-1));
                scrape[i]=envelope*(rasp*.12f+Mathf.Sin(2*Mathf.PI*(190*t+35*t*t))*.055f);
            }
            chairScrape=AudioClip.Create("Empty chair wood scrape",scrape.Length,1,rate,false);chairScrape.SetData(scrape,0);
            GameSession.Current.StoryChanged+=OnStory;
        }
        void OnDestroy()
        {
            if(GameSession.Current)GameSession.Current.StoryChanged-=OnStory;
            if(bell)Destroy(bell);
            if(chairScrape)Destroy(chairScrape);
        }
        void OnStory(int step)
        {
            if(step!=4)source.PlayOneShot(bell,.7f);
            if(step==2)StartCoroutine(Wake());
            if(step==4)
            {
                if(emptyChair)StartCoroutine(RestoreChair());
                stalker.gameObject.SetActive(false);
                foreach(var light in corridorLights)if(light){light.enabled=true;light.color=new Color(.86f,.73f,.5f);light.intensity=2.8f;}
            }
        }
        IEnumerator RestoreChair()
        {
            if(RestorationChairCues>0)yield break;
            RestorationChairCues++;
            var start=emptyChair.localPosition;var rotation=emptyChair.localRotation;
            var end=start+Vector3.back*.16f;
            var sound=emptyChair.gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;
            sound.spatialBlend=1;sound.minDistance=2;sound.maxDistance=14;sound.rolloffMode=AudioRolloffMode.Linear;
            sound.PlayOneShot(chairScrape,.65f);
            for(float elapsed=0;elapsed<.72f;elapsed+=Time.deltaTime)
            {
                float blend=Mathf.SmoothStep(0,1,elapsed/.72f);
                emptyChair.localPosition=Vector3.Lerp(start,end,blend);
                emptyChair.localRotation=Quaternion.Slerp(rotation,rotation*Quaternion.Euler(0,22,0),blend);
                yield return null;
            }
            emptyChair.localPosition=end;emptyChair.localRotation=rotation*Quaternion.Euler(0,22,0);
            RestorationChairCompleted=true;Destroy(sound,1);
        }
        IEnumerator Wake()
        {
            for(int i=0;i<6;i++)
            {foreach(var light in corridorLights)if(light)light.enabled=i%2==1;yield return new WaitForSeconds(.18f);}
            // Telegraph the threat; never spawn it right on the player.
            yield return new WaitForSeconds(2);
            if(GameSession.Current.StoryStep>=4)yield break;
            var player=GameSession.Current.player.transform.position;
            var spawn=new Vector3(player.x<0?7.5f:-7.5f,0,0);
            if(NavMesh.SamplePosition(spawn,out var hit,1,NavMesh.AllAreas))stalker.transform.position=hit.position;
            stalker.gameObject.SetActive(true);
            var intro=GetComponent<V1CyclopseIntro>();if(!intro)intro=gameObject.AddComponent<V1CyclopseIntro>();
            yield return intro.Play(stalker);
        }
    }
}
