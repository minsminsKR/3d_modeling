using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Adapted from V1 CyclopseIntroEvent and SoundManager.playMonsterRoar.
    // Keep the advance -> roar -> AI handoff; omit the old large-map camera teleport.
    public sealed class V1CyclopseIntro:MonoBehaviour
    {
        public bool Completed { get; private set; }
        public bool RoarPlayed { get; private set; }
        public string Phase { get; private set; }="idle";
        AudioClip roar;AudioSource voice;
        public IEnumerator Play(StalkerBrain brain)
        {
            if(Phase!="idle")yield break;
            var agent=brain.GetComponent<NavMeshAgent>();brain.enabled=false;brain.state=StalkerBrain.State.Patrol;
            Phase="emerge";agent.isStopped=false;agent.speed=1.05f;
            var destination=brain.transform.position+Vector3.right*(brain.transform.position.x>0?-1.35f:1.35f);
            agent.SetDestination(destination);
            yield return new WaitForSeconds(1.3f);
            if(GameSession.Current.Finished||GameSession.Current.StoryStep>=4){brain.enabled=true;yield break;}
            Phase="roar";agent.isStopped=true;
            var facing=GameSession.Current.player.transform.position-brain.transform.position;facing.y=0;
            if(facing.sqrMagnitude>.01f)brain.transform.rotation=Quaternion.LookRotation(facing);
            voice=brain.gameObject.AddComponent<AudioSource>();voice.spatialBlend=1;voice.minDistance=2;voice.maxDistance=18;voice.volume=.6f;
            // V1's two descending sawtooth voices, bandpass at 245 Hz (Q .85).
            const int rate=24000;var samples=new float[(int)(rate*.78f)];
            float omega=2*Mathf.PI*245/rate,alpha=Mathf.Sin(omega)/(2*.85f),a0=1+alpha;
            float b0=alpha/a0,b2=-alpha/a0,a1=-2*Mathf.Cos(omega)/a0,a2=(1-alpha)/a0;
            double phaseA=0,phaseB=0;float x1=0,x2=0,y1=0,y2=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;phaseA+=136*Mathf.Pow(48f/136,Mathf.Min(t/.72f,1))/rate;phaseB+=143*Mathf.Pow(48f/143,Mathf.Min(t/.72f,1))/rate;
                float x=(.23f*(2*(float)(phaseA%1)-1)+.15f*(2*(float)(phaseB%1)-1))*Mathf.Exp(-10*t);
                float y=b0*x+b2*x2-a1*y1-a2*y2;samples[i]=y;x2=x1;x1=x;y2=y1;y1=y;
            }
            roar=AudioClip.Create("V1 Cyclopse descending roar",samples.Length,1,rate,false);roar.SetData(samples,0);voice.PlayOneShot(roar);RoarPlayed=true;
            yield return new WaitForSeconds(1.3f);
            agent.isStopped=false;brain.enabled=true;Phase="done";Completed=true;
        }
        void OnDestroy(){if(roar)Destroy(roar);if(voice)Destroy(voice);}
    }
}
