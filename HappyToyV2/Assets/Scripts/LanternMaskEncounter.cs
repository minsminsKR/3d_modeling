using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LanternMaskEncounter:MonoBehaviour
    {
        public enum Phase { Dormant, Wander, Investigate, Chase, Transforming, Resolved }
        public Phase State {get;private set;}=Phase.Dormant;
        public Transform mask,body,lantern;
        public Animation motion;
        public Light flameLight;
        public Transform[] patrol;
        public bool Transformed {get;private set;}
        public float TransformProgress=>Mathf.Clamp01(transformTime/5);
        public int CursesApplied {get;private set;}
        NavMeshAgent agent;NavMeshPath path;Vector3 target;float floorY,age,transformTime,memory,repath;int waypoint;
        void Awake(){agent=GetComponent<NavMeshAgent>();path=new NavMeshPath();floorY=transform.position.y;}
        void Start(){SetVisible(false);}
        void SetVisible(bool show){mask.gameObject.SetActive(show);lantern.gameObject.SetActive(show&&!Transformed);body.gameObject.SetActive(show&&TransformProgress>0);flameLight.enabled=show;}
        void Stop(){if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}}
        bool Route(Vector3 point)
        {
            if(Mathf.Abs(point.y-floorY)>1.6f||!NavMesh.SamplePosition(point,out var hit,2,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-floorY)>1.6f)return false;
            if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            foreach(var corner in path.corners)if(Mathf.Abs(corner.y-floorY)>1.6f)return false;
            return true;
        }
        bool Sees(PlayerMotor p)
        {
            return !p.Hidden&&Mathf.Abs(p.transform.position.y-floorY)<1.6f&&Vector3.Distance(p.transform.position,transform.position)<12&&
                !Physics.Linecast(transform.position+Vector3.up,p.eyes.transform.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public bool HearNoise(Vector3 point,float duration)
        {
            if(!isActiveAndEnabled||!agent.isOnNavMesh||State==Phase.Dormant||State==Phase.Resolved||State==Phase.Chase||State==Phase.Transforming||duration<=0||
                Vector3.Distance(point,transform.position)>28||Sees(GameSession.Current.player)||!Route(point))return false;
            State=Phase.Investigate;target=point;memory=duration;repath=0;return true;
        }
        void Update()
        {
            var s=GameSession.Current;if(!s)return;
            if(s.Finished||s.StoryStep>=4){State=Phase.Resolved;Stop();SetVisible(false);return;}
            if(!s.InputAllowed||!agent.isOnNavMesh){Stop();return;}
            if(State==Phase.Dormant){if(s.StoryStep<2)return;State=Phase.Wander;SetVisible(true);}
            age+=Time.deltaTime;var p=s.player;
            if(State==Phase.Transforming)
            {
                Stop();transformTime=Mathf.Min(5,transformTime+Time.deltaTime);
                if(transformTime>=5){Transformed=true;State=Phase.Chase;memory=8;target=p.transform.position;repath=0;}
                Visual();return;
            }
            bool sees=Sees(p);float distance=Vector3.Distance(p.transform.position,transform.position);
            if(sees){State=Phase.Chase;target=p.transform.position;memory=Transformed?8:3;}
            else if(State==Phase.Chase||State==Phase.Investigate)
            {
                memory-=Time.deltaTime;if(memory<=0){State=Phase.Wander;repath=0;}
            }
            if(sees&&distance<(Transformed?.75f:.85f))
            {
                if(Transformed){s.Finish(false);Stop();return;}
                p.ApplyCurse(10);CursesApplied++;State=Phase.Transforming;transformTime=0;Stop();
                s.Notify("가면의 저주 · 10초간 속도 50%. 몸이 자라기 전에 다른 복도로 피하세요.");Visual();return;
            }
            if(Mathf.Abs(p.transform.position.y-floorY)>1.6f){Stop();repath=0;Visual();return;}
            if(State==Phase.Wander&&patrol.Length>0)
            {
                if(Vector3.Distance(transform.position,patrol[waypoint].position)<.6f)waypoint=(waypoint+1)%patrol.Length;
                target=patrol[waypoint].position;
            }
            float stride=age%2.4f<.2f?.18f:age%2.4f<.75f?1.4f:.9f;
            agent.speed=State==Phase.Chase?(Transformed?3.4f*stride:2.7f):1.15f;
            repath-=Time.deltaTime;
            if(repath<=0)
            {
                repath=.35f;
                if(Route(target)){agent.SetPath(path);agent.isStopped=false;}else Stop();
            }
            Visual();
        }
        void Visual()
        {
            float progress=TransformProgress;
            mask.localPosition=new Vector3(0,1.25f+progress*.82f+.2f*(1-progress)*Mathf.Sin(age*2.3f),0);
            mask.localRotation=Quaternion.Euler(0,0,progress*(-16+Mathf.Sin(age*31)*2.5f));
            body.gameObject.SetActive(progress>0);body.localScale=new Vector3(.3f+.7f*progress,Mathf.Max(.001f,progress),.45f+.55f*progress);
            lantern.gameObject.SetActive(progress<1);lantern.localScale=Vector3.one*Mathf.Max(.001f,1-progress);
            flameLight.intensity=1.2f+Mathf.Sin(age*9)*.18f;
            if(motion&&progress>0)
            {
                bool moving=!agent.isStopped&&agent.velocity.sqrMagnitude>.01f;
                var clip=motion["run"];if(clip!=null){if(!motion.IsPlaying("run"))motion.Play("run");clip.speed=moving?agent.speed/3.4f:0;if(!moving){clip.time=0;motion.Sample();}}
            }
        }
        void LateUpdate()
        {
            if(TransformProgress<=0||State==Phase.Resolved)return;
            var skin=body.GetComponentInChildren<SkinnedMeshRenderer>();
            if(!skin)return;
            var bounds=skin.bounds;
            var attached=new Vector3(bounds.center.x,bounds.max.y+.15f,bounds.center.z)+transform.forward*.03f;
            mask.position=Vector3.Lerp(mask.position,attached,TransformProgress);
        }
        void OnDisable(){Stop();if(flameLight)flameLight.enabled=false;}
    }
}
