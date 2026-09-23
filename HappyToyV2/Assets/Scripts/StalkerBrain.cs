using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerBrain : MonoBehaviour
    {
        public Transform[] patrol;
        public float patrolSpeed = 1.45f, chaseSpeed = 3.5f;
        public PlayerMotor player;
        public enum State { Patrol, Investigate, Chase, Search }
        public State state;
        NavMeshAgent agent;
        Vector3 lastKnown;
        float memory, repath;
        int waypoint;
        bool witnessedHiding;
        public bool SawHiding => witnessedHiding;
        Vector3 hidingApproach;
        float attackTimer, recovery;
        public float AttackWindup => attackTimer>0 ? 1-attackTimer/.75f : 0;
        public float AttackRecovery => Mathf.Clamp01(recovery/.9f);
        public bool AttackActive => attackTimer>0||recovery>0;
        public int AttacksStarted { get; private set; }
        public int NoisesAccepted { get; private set; }
        public bool HearNoise(Vector3 point,float duration)
        {
            if(!isActiveAndEnabled||!agent||!agent.enabled||!agent.isOnNavMesh||duration<=0||
                state==State.Chase||AttackActive||CanSeePlayer()||
                Mathf.Abs(point.y-transform.position.y)>1.6f||Vector3.Distance(point,transform.position)>28)return false;
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(point,out var hit,2,NavMesh.AllAreas)||
                Mathf.Abs(hit.position.y-transform.position.y)>1.6f||
                !agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            foreach(var corner in path.corners)if(Mathf.Abs(corner.y-transform.position.y)>1.6f)return false;
            lastKnown=hit.position;memory=duration;state=State.Investigate;repath=0;NoisesAccepted++;
            return true;
        }
        void Awake() { agent = GetComponent<NavMeshAgent>(); if(!GetComponent<StalkerFootsteps>())gameObject.AddComponent<StalkerFootsteps>(); }
        public bool CanSeePlayer()
        {
            if(!player||player.Hidden)return false;
            var eye=transform.position+Vector3.up*1.7f;
            // A visible torso counts too; a camera point can sit outside the capsule's curved head.
            for(int i=0;i<2;i++)
            {
                var target=i==0?player.eyes.transform.position:player.transform.position+Vector3.up*1.1f;
                var delta=target-eye;
                if(delta.magnitude<14&&(state==State.Chase||Vector3.Angle(transform.forward,delta)<65)&&
                    Physics.Raycast(eye,delta.normalized,out var hit,delta.magnitude+.1f,~0,QueryTriggerInteraction.Ignore)&&
                    hit.collider.GetComponentInParent<PlayerMotor>()==player)return true;
            }
            return false;
        }
        public void ObserveHiding(Vector3 entrance)
        {
            // Called before the player's collider disappears, not inferred from old chase memory.
            witnessedHiding=CanSeePlayer();
            if(!witnessedHiding)return;
            hidingApproach=entrance;lastKnown=entrance;memory=8;state=State.Chase;
        }
        void Update()
        {
            if (!player || !agent.isOnNavMesh || GameSession.Current.Finished) return;
            var eye = transform.position + Vector3.up * 1.7f;
            var offset = player.eyes.transform.position - eye;
            bool visible = CanSeePlayer();
            if(attackTimer>0)
            {
                attackTimer-=Time.deltaTime;
                if(attackTimer<=0)
                {
                    if(visible&&offset.magnitude<1.55f)GameSession.Current.Finish(false);
                    recovery=.9f;
                }
                return;
            }
            if(recovery>0)
            {
                recovery-=Time.deltaTime;
                if(recovery<=0)agent.isStopped=false;
                return;
            }
            if (visible)
            { state = State.Chase; memory = 5; lastKnown = player.transform.position; witnessedHiding = false; }
            else if (state == State.Chase)
            {
                memory -= Time.deltaTime;
                if (memory <= 0) { state = State.Search; memory = 4; witnessedHiding = false; }
            }
            else if (player.Running && offset.magnitude < 8)
            { state = State.Investigate; lastKnown = player.transform.position; memory = 3; }
            else if (state == State.Search || state == State.Investigate)
            { memory -= Time.deltaTime; if (memory <= 0) state = State.Patrol; }
            var approachOffset=transform.position-hidingApproach;approachOffset.y=0;
            if (player.Hidden && witnessedHiding && state == State.Chase && approachOffset.magnitude < .8f)
            { GameSession.Current.Finish(false); return; }
            if(visible&&offset.magnitude<1.5f)
            {
                attackTimer=.75f;agent.isStopped=true;AttacksStarted++;
                GetComponent<StalkerFootsteps>().PlayAttackCue();return;
            }
            agent.speed = state == State.Chase ? chaseSpeed : patrolSpeed;
            repath -= Time.deltaTime;
            if (repath > 0) return;
            repath = .25f;
            if (state != State.Patrol) agent.SetDestination(lastKnown);
            else if (patrol.Length > 0)
            {
                if (!agent.pathPending && agent.remainingDistance < .6f) waypoint = (waypoint+1)%patrol.Length;
                agent.SetDestination(patrol[waypoint].position);
            }
        }
    }
}
