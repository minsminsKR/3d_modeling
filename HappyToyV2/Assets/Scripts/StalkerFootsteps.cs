using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerFootsteps : MonoBehaviour
    {
        public int StepsPlayed { get; private set; }
        public int AttackCuesPlayed { get; private set; }
        public int CabinetAttackCuesPlayed { get; private set; }
        AudioSource source;AudioClip clip, cabinetRattle;NavMeshAgent agent;Vector3 previous;float distance;
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();source=gameObject.AddComponent<AudioSource>();
            source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=2;source.maxDistance=16;source.dopplerLevel=0;source.volume=.7f;
            source.playOnAwake=false;source.ignoreListenerPause=false;source.ignoreListenerVolume=false;
            const int rate=24000;var samples=new float[4800];var noise=new System.Random(114);
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                samples[i]=.55f*Mathf.Exp(-25*t)*Mathf.Sin(2*Mathf.PI*(85-80*t)*t)
                    +(float)(noise.NextDouble()*2-1)*.16f*Mathf.Exp(-65*t);
            }
            clip=AudioClip.Create("Dragging wooden footstep",samples.Length,1,rate,false);clip.SetData(samples,0);
            // Original restrained wood/handle impacts, distinct from a walking step.
            // No visual shake or flash is added; master volume and listener pause apply.
            var door = new float[(int)(rate * .42f)]; var grain = new System.Random(731);
            for (int i = 0; i < door.Length; i++)
            {
                float t = i / (float)rate, value = 0;
                for (int strike = 0; strike < 2; strike++)
                {
                    float q = t - strike * .135f;
                    if (q < 0) continue;
                    float envelope = Mathf.Clamp01(q / .003f) * Mathf.Exp(-q * 27);
                    value += envelope * (.25f * Mathf.Sin(2 * Mathf.PI * (92 + strike * 24) * q) +
                        .11f * (float)(grain.NextDouble() * 2 - 1));
                }
                door[i] = Mathf.Clamp(value, -.38f, .38f) * Mathf.Clamp01((.42f - t) / .025f);
            }
            cabinetRattle = AudioClip.Create("Cabinet door attack rattle", door.Length, 1, rate, false);
            cabinetRattle.SetData(door, 0);
        }
        void OnEnable(){previous=transform.position;distance=0;}
        public void PlayAttackCue() => PlayAttackCue(false);
        public void PlayAttackCue(bool atCabinet)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !source || !session || !session.InputAllowed) return;
            source.pitch = atCabinet ? 1 : .45f;
            source.PlayOneShot(atCabinet ? cabinetRattle : clip, atCabinet ? .95f : 1.4f);
            AttackCuesPlayed++;
            if (atCabinet) CabinetAttackCuesPlayed++;
        }
        void Update()
        {
            var delta=transform.position-previous;delta.y=0;previous=transform.position;
            if(!agent.isOnNavMesh||GameSession.Current.Finished||delta.magnitude>1)return;
            distance+=delta.magnitude;
            if(distance<.85f)return;
            distance%=.85f;source.pitch=StepsPlayed%2==0?.85f:1.03f;
            source.PlayOneShot(clip);StepsPlayed++;
        }
        void OnDisable() { if (source) source.Stop(); }
        void OnDestroy(){if(clip)Destroy(clip);if(cabinetRattle)Destroy(cabinetRattle);}
    }
}
