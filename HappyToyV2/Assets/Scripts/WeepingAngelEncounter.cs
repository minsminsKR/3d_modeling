using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // V1 mannequin: flashlight-powered, floor-bound pursuit interrupted by visible gaze.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WeepingAngelEncounter : MonoBehaviour
    {
        public Transform visual;
        public Light displayLight;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Observed { get; private set; }
        public bool Moving { get; private set; }
        public bool Resolved { get; private set; }
        public int PathRequests { get; private set; }
        NavMeshAgent agent;
        NavMeshPath path;
        AudioSource sound;
        AudioClip creak;
        float intro, repath, soundTimer, floorY;
        Quaternion startTurn, endTurn;

        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();path=new NavMeshPath();floorY=transform.position.y;
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1;
            sound.minDistance=2;sound.maxDistance=15;sound.volume=.3f;
            const int rate=24000;var data=new float[rate];
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;
                data[i]=Mathf.Sin(Mathf.PI*t)*(.28f*Mathf.Sin(720*t+18*Mathf.Sin(31*t))+.12f*Mathf.Sin(1190*t));
            }
            creak=AudioClip.Create("Mannequin joint creak",rate,1,rate,false);creak.SetData(data,0);
        }
        void Stop()
        {
            Moving=false;
            if(agent&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}
        }
        bool Clear(Camera camera,Vector3 point)
        {
            return !Physics.Linecast(camera.transform.position,point,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public bool VisibleTo(Camera camera)
        {
            // Multiple body points prevent movement while a visible head/shoulder remains at a doorway.
            foreach(var local in new[]{new Vector3(0,1.65f,0),new Vector3(-.25f,1.1f,0),new Vector3(.25f,1.1f,0),new Vector3(0,.4f,0)})
            {
                var point=transform.TransformPoint(local);var viewport=camera.WorldToViewportPoint(point);
                if(viewport.z>0&&viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1&&Clear(camera,point))return true;
            }
            return false;
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!session)return;
            if(session.StoryStep>=4)
            {
                Resolved=true;Stop();sound.Stop();if(displayLight)displayLight.enabled=false;
                if(visual)visual.gameObject.SetActive(false);return;
            }
            if(!session.InputAllowed||!agent.isOnNavMesh){Stop();return;}
            var player=session.player;var delta=player.transform.position-transform.position;
            bool sameFloor=Mathf.Abs(player.transform.position.y-floorY)<1.6f;
            Observed=sameFloor&&VisibleTo(player.eyes);
            if(!Triggered)
            {
                Stop();
                if(session.StoryStep<1||player.Hidden||!sameFloor||delta.magnitude>8||!Observed)return;
                Triggered=true;startTurn=visual.localRotation;
                var toward=delta;toward.y=0;
                endTurn=Quaternion.Inverse(transform.rotation)*Quaternion.LookRotation(toward);
                session.Notify("등을 돌린 마네킹이 돌아봅니다. 눈을 떼지 마세요. 손전등을 끄면 멈춥니다.");
                sound.PlayOneShot(creak);
            }
            if(!Released)
            {
                Stop();intro+=Time.deltaTime;
                visual.localRotation=Quaternion.Slerp(startTurn,endTurn,Mathf.SmoothStep(0,1,Mathf.Clamp01((intro-.65f)/.85f)));
                if(intro>=2.2f)Released=true;
                return;
            }
            if(!sameFloor||delta.magnitude>30||player.Hidden||!player.flashlight.enabled||Observed)
            {Stop();repath=0;return;}
            repath-=Time.deltaTime;
            if(repath<=0)
            {
                repath=.4f;PathRequests++;
                bool valid=agent.CalculatePath(player.transform.position,path)&&path.status==NavMeshPathStatus.PathComplete;
                if(valid)foreach(var corner in path.corners)if(Mathf.Abs(corner.y-floorY)>1.6f){valid=false;break;}
                if(!valid){Stop();return;}
                agent.SetPath(path);agent.isStopped=false;
            }
            Moving=!agent.isStopped&&agent.hasPath;
            if(Moving)
            {
                var facing=agent.steeringTarget-transform.position;facing.y=0;
                if(facing.sqrMagnitude>.01f)visual.rotation=Quaternion.LookRotation(facing);
                soundTimer-=Time.deltaTime;
                if(soundTimer<=0){soundTimer=.8f;sound.PlayOneShot(creak);}
            }
            // No invisible kill across walls, between floors, while hidden or during the introduction.
            if(delta.magnitude<1.05f&&Clear(player.eyes,transform.position+Vector3.up*1.2f))session.Finish(false);
        }
        void OnDisable(){Stop();if(sound)sound.Stop();}
        void OnDestroy(){if(creak)Destroy(creak);}
    }
}
