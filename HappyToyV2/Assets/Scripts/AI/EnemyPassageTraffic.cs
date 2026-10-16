using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    /// <summary>Temporary right-of-way manoeuvres on genuine corridor routes.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(NavMeshAgent)),DefaultExecutionOrder(100)]
    public sealed class EnemyPassageTraffic : MonoBehaviour
    {
        static readonly List<EnemyPassageTraffic> actors=new List<EnemyPassageTraffic>();
        static readonly float[] retreats={1.25f,2.2f,.65f}, sides={.85f,-.85f,1.3f,-1.3f};
        const float SidingRetrySeconds=.25f, YieldRepathSeconds=.2f;
        static int nextIdentity;int identity;float normalStoppingDistance;
        NavMeshAgent agent;Behaviour owner;
        int originalPriority,priority;
        bool configured,intent,fromBehind;
        Vector3 wanted,yieldPoint,yieldHeading;
        EnemyPassageTraffic peer;
        NavMeshPath path;
        Vector3[] corners=new Vector3[32];
        EnemyPassageTraffic retryPeer;
        Vector3 retryPosition,retryPeerPosition,retryTarget;
        float sidingRetry,yieldRepath;
        float releaseDeadline,blockedTime;Vector3 previous;
        public bool Yielding { get; private set; }
        public int YieldsStarted { get; private set; }
        public int RoutesResumed { get; private set; }
        public int SidingSearches { get; private set; }
        public int YieldPathPlans { get; private set; }
        public Vector3 IntendedPoint => wanted;
        public int RightOfWayPriority => priority;
        public void Configure(Behaviour navigationOwner)
        {
            owner=navigationOwner;agent=GetComponent<NavMeshAgent>();path=new NavMeshPath();
            if(!configured)originalPriority=agent.avoidancePriority;
            priority=owner is LanternMaskEncounter?10:EnemySoundProfile.Identify(transform)==EnemySoundKind.Cyclopse?20:
                EnemySoundProfile.Identify(transform)==EnemySoundKind.Uncat?30:40;
            configured=true;agent.avoidancePriority=priority;
        }
        public void ObserveRoute(Vector3 point){wanted=point;intent=true;}
        public void Cancel(){if(Yielding && agent)agent.stoppingDistance=normalStoppingDistance;Yielding=false;peer=retryPeer=null;intent=false;sidingRetry=yieldRepath=0;if(agent && configured)agent.avoidancePriority=priority;}
        internal void DelayTimeStopDeadline(float seconds) { if (Yielding) releaseDeadline += seconds; }
        void OnEnable(){previous=transform.position;blockedTime=0;if(identity==0)identity=++nextIdentity;if(!actors.Contains(this))actors.Add(this);if(configured && agent)agent.avoidancePriority=priority;}
        void OnDisable(){actors.Remove(this);Cancel();if(agent && configured)agent.avoidancePriority=originalPriority;}
        void OnDestroy()=>actors.Remove(this);
        bool Live => configured && owner && owner.isActiveAndEnabled && EnemyNavigation.Ready(agent) && intent;
        Vector3 Heading { get {var d=wanted-transform.position;d.y=0;return d.sqrMagnitude>.01f?d.normalized:Vector3.zero;} }
        void LateUpdate()
        {
            if (EnemyTimeStop.Active) return;
            var deltaMoved=transform.position-previous;deltaMoved.y=0;previous=transform.position;
            var session=GameSession.Current;
            if(!Live || !session || !session.InputAllowed || session.Finished){Cancel();return;}
            sidingRetry=Mathf.Max(0,sidingRetry-Time.deltaTime);
            if(Yielding)
            {
                var delta=peer?peer.transform.position-transform.position:Vector3.zero;delta.y=0;
                if(!peer || !peer.isActiveAndEnabled || !peer.Live || Mathf.Abs(peer.transform.position.y-transform.position.y)>.7f ||
                    delta.magnitude>4.5f || MonsterPassingRules.Passed(fromBehind,Vector3.Dot(delta,yieldHeading)) || Time.time>releaseDeadline)
                {
                    Yielding=false;peer=null;agent.avoidancePriority=priority;agent.stoppingDistance=normalStoppingDistance;
                    if(agent.CalculatePath(wanted,path) && path.status==NavMeshPathStatus.PathComplete){agent.SetPath(path);agent.isStopped=false;RoutesResumed++;}
                    return;
                }
                // AI retains and refreshes its true target; it never saves this
                // local manoeuvre as a noise/player/checkpoint target.
                yieldRepath-=Time.deltaTime;
                bool arrived=(transform.position-yieldPoint).sqrMagnitude<.04f;
                if(arrived){agent.isStopped=true;return;}
                if(yieldRepath<=0)
                {
                    yieldRepath=YieldRepathSeconds;YieldPathPlans++;
                    if(agent.CalculatePath(yieldPoint,path) && path.status==NavMeshPathStatus.PathComplete)
                    {agent.SetPath(path);agent.isStopped=false;}
                }
                // A normal AI refresh may replace this short local route. Reuse
                // the accepted corridor until its next timed navigation query.
                else if(path.status==NavMeshPathStatus.PathComplete &&
                    (!agent.hasPath || (agent.destination-yieldPoint).sqrMagnitude>.0001f))
                {agent.SetPath(path);agent.isStopped=false;}
                return;
            }
            if(agent.isStopped || !agent.hasPath || Heading.sqrMagnitude<.5f){blockedTime=0;return;}
            blockedTime=Vector3.Dot(deltaMoved,Heading)>Time.deltaTime*.12f?0:blockedTime+Time.deltaTime;
            if(blockedTime<.65f)return;
            EnemyPassageTraffic blocker=null;float nearest=8f;
            foreach(var other in actors)
            {
                if(other==this || !other || !other.Live || other.Yielding || Mathf.Abs(other.transform.position.y-transform.position.y)>.7f ||
                    MonsterPassingRules.HasRightOfWay(priority,identity,other.priority,other.identity))continue;
                var delta=other.transform.position-transform.position;delta.y=0;float distance=delta.magnitude;
                if(distance>nearest || distance>Mathf.Min(8,2+(agent.speed+other.agent.speed)*.7f) || distance<.01f)continue;
                var relative=other.Heading*other.agent.speed-Heading*agent.speed;
                float time=relative.sqrMagnitude>.01f?Mathf.Clamp(-Vector3.Dot(delta,relative)/relative.sqrMagnitude,0,1.6f):0;
                if((delta+relative*time).magnitude>agent.radius+other.agent.radius+.45f)continue;
                nearest=distance;blocker=other;
            }
            if(!blocker){retryPeer=null;sidingRetry=0;return;}
            // A different blocker or a material route/position change warrants
            // an immediate retry; tiny crowd jitter must not defeat the budget.
            if(blocker!=retryPeer || (transform.position-retryPosition).sqrMagnitude>.25f ||
                (blocker.transform.position-retryPeerPosition).sqrMagnitude>.25f || (wanted-retryTarget).sqrMagnitude>.25f)
                sidingRetry=0;
            if(sidingRetry>0)return;
            retryPeer=blocker;retryPosition=transform.position;retryPeerPosition=blocker.transform.position;retryTarget=wanted;
            sidingRetry=SidingRetrySeconds;
            if(!TrySiding(blocker,out var point))return;
            peer=blocker;yieldPoint=point;yieldHeading=Heading;
            fromBehind=Vector3.Dot(peer.transform.position-transform.position,yieldHeading)<0;
            Yielding=true;blockedTime=0;YieldsStarted++;releaseDeadline=Time.time+4;
            agent.avoidancePriority=80;normalStoppingDistance=agent.stoppingDistance;agent.stoppingDistance=.08f;
            // TrySiding already calculated this complete, length-checked path.
            agent.SetPath(path);agent.isStopped=false;yieldRepath=YieldRepathSeconds;YieldPathPlans++;
        }
        bool TrySiding(EnemyPassageTraffic other,out Vector3 point)
        {
            SidingSearches++;
            var forward=Heading;var right=Vector3.Cross(Vector3.up,forward);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            foreach(float retreat in retreats)foreach(float side in sides)
            {
                var candidate=transform.position-forward*retreat+right*side;
                if(!NavMesh.SamplePosition(candidate,out var hit,.18f,filter) || Mathf.Abs(hit.position.y-transform.position.y)>.2f ||
                    Vector3.Distance(hit.position,other.transform.position)<agent.radius+other.agent.radius+.35f ||
                    !agent.CalculatePath(hit.position,path) || path.status!=NavMeshPathStatus.PathComplete)continue;
                int count=path.GetCornersNonAlloc(corners);
                while(count==corners.Length){corners=new Vector3[corners.Length*2];count=path.GetCornersNonAlloc(corners);}
                float length=0;var previous=transform.position;for(int i=0;i<count;i++){length+=Vector3.Distance(previous,corners[i]);previous=corners[i];}
                if(length>3.5f)continue;
                point=hit.position;return true;
            }
            point=default;return false;
        }
    }
}
