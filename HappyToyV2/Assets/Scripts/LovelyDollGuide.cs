using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LovelyDollGuide : MonoBehaviour
    {
        public Transform model;
        public Animation animationPlayer;
        public Light guideLight;
        public bool Awakened { get; private set; }
        public bool Following { get; private set; }
        public bool WaitingForPlayer { get; private set; }
        public bool RouteBlocked { get; private set; }
        public string TargetId { get; private set; }
        NavMeshAgent agent;
        Interactable[] records;
        AudioSource source; AudioClip music;
        float gaze,repath;
        string clip;
        void Start()
        {
            agent=GetComponent<NavMeshAgent>();agent.speed=2.2f;agent.stoppingDistance=.35f;
            records=FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            model.gameObject.SetActive(false);guideLight.enabled=false;
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=2;source.maxDistance=12;source.playOnAwake=false;
            const int rate=24000;var samples=new float[rate*3];var notes=new[]{660f,880f,784f,990f,880f};
            for(int k=0;k<notes.Length;k++)for(int i=0;i<rate;i++)
            {int index=k*rate/3+i;if(index>=samples.Length)break;float t=i/(float)rate;samples[index]+=.12f*Mathf.Exp(-6*t)*(Mathf.Sin(2*Mathf.PI*notes[k]*t)+.3f*Mathf.Sin(2*Mathf.PI*notes[k]*2.76f*t));}
            music=AudioClip.Create("Lovely Doll music box",samples.Length,1,rate,false);music.SetData(samples,0);
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!session||!agent.isOnNavMesh)return;
            if(!session.InputAllowed){agent.isStopped=true;return;}
            var player=session.player;var delta=player.transform.position-transform.position;
            if(!Awakened)
            {
                if(session.StoryStep<1||player.Hidden||Mathf.Abs(delta.y)>1.6f||delta.magnitude>4.2f)return;
                Awakened=true;model.gameObject.SetActive(true);guideLight.enabled=true;source.PlayOneShot(music);
                session.Notify("작은 인형이 손을 흔듭니다. 눈을 맞추면 다음 기록으로 안내합니다.");
            }
            if(!Following)
            {
                agent.isStopped=true;Play("wave",1);
                var eye=player.eyes.transform.position;var direction=transform.position+Vector3.up-eye;
                bool looking=direction.magnitude<6&&Vector3.Dot(player.eyes.transform.forward,direction.normalized)>.85f&&!Physics.Linecast(eye,transform.position+Vector3.up,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                gaze=looking?gaze+Time.deltaTime:0;
                if(gaze>.6f){Following=true;repath=0;session.Notify("인형이 길을 안내합니다. 멀어지면 기다려 줍니다.");}
                return;
            }
            WaitingForPlayer=player.Hidden||delta.magnitude>5.5f;
            repath-=Time.deltaTime;
            if(repath<=0){repath=.8f;ChooseRoute(session.CurrentObjectiveId);}
            agent.isStopped=WaitingForPlayer||RouteBlocked;
            bool moving=!agent.isStopped&&agent.velocity.sqrMagnitude>.02f;
            Play(moving?"patrol":"wave",moving?Mathf.Clamp(agent.velocity.magnitude/2.2f,.25f,1.3f):.35f);
        }
        void ChooseRoute(string id)
        {
            TargetId=id;
            var target=records.FirstOrDefault(i=>i&&(id=="exit"?i.kind==Interactable.Kind.Exit:i.stableId==id));
            RouteBlocked=true;
            if(!target){agent.ResetPath();return;}
            float best=float.MaxValue;NavMeshPath bestPath=null;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                var candidate=target.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.25f;
                if(!NavMesh.SamplePosition(candidate,out var hit,2,NavMesh.AllAreas))continue;
                if(Mathf.Abs(hit.position.y-target.transform.position.y)>1.8f)continue;
                var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;for(int n=1;n<path.corners.Length;n++)length+=Vector3.Distance(path.corners[n-1],path.corners[n]);
                if(length<best){best=length;bestPath=path;}
            }
            if(bestPath!=null){agent.SetPath(bestPath);RouteBlocked=false;}else agent.ResetPath();
        }
        void Play(string next,float speed)
        {if(!animationPlayer.GetClip(next))return;if(clip!=next){animationPlayer.CrossFade(next,.2f);clip=next;}animationPlayer[next].speed=speed;}
        void OnDestroy(){if(music)Destroy(music);}
    }
}
