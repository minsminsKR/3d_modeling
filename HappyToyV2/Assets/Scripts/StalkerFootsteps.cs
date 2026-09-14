using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerFootsteps : MonoBehaviour
    {
        public int StepsPlayed { get; private set; }
        AudioSource source;AudioClip clip;NavMeshAgent agent;Vector3 previous;float distance;
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();source=gameObject.AddComponent<AudioSource>();
            source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=2;source.maxDistance=16;source.dopplerLevel=0;source.volume=.7f;
            const int rate=24000;var samples=new float[4800];var noise=new System.Random(114);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                samples[i]=.55f*Mathf.Exp(-25*t)*Mathf.Sin(2*Mathf.PI*(85-80*t)*t)
                    +(float)(noise.NextDouble()*2-1)*.16f*Mathf.Exp(-65*t);
            }
            clip=AudioClip.Create("Dragging wooden footstep",samples.Length,1,rate,false);clip.SetData(samples,0);
        }
        void OnEnable(){previous=transform.position;distance=0;}
        public void PlayAttackCue(){source.pitch=.45f;source.PlayOneShot(clip,1.4f);}
        void Update()
        {
            var delta=transform.position-previous;delta.y=0;previous=transform.position;
            if(!agent.isOnNavMesh||GameSession.Current.Finished||delta.magnitude>1)return;
            distance+=delta.magnitude;
            if(distance<.85f)return;
            distance%=.85f;source.pitch=StepsPlayed%2==0?.85f:1.03f;
            source.PlayOneShot(clip);StepsPlayed++;
        }
        void OnDestroy(){if(clip)Destroy(clip);}
    }
}
