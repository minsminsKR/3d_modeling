using System;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Explicitly ticked by the encounter after its real movement gates. This
    // component never runs an autonomous Update or moves a root transform.
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerDoorTraversal : MonoBehaviour
    {
        public enum Result { Clear, Waiting, Blocked }
        Behaviour owner;
        NavMeshAgent agent;
        NavMeshPath approachPath;
        readonly RaycastHit[] hits = new RaycastHit[32];
        Interactable door, deferredDoor;
        bool passing;
        float entrySide, push, closeWait, blockedWait, retryUntil;
        Vector3 deferredTarget;
        Vector3 lastTarget, lastHitPoint; float lastFloorY, lastHitDistance;
        int lastHitCount; Collider lastHitCollider; string lastDecision="not ticked";
        public string Diagnostics
        {
            get
            {
                bool ready=EnemyNavigation.Ready(agent);
                return "owner="+(owner?owner.GetType().Name:"unbound")+" enabled="+(owner&&owner.isActiveAndEnabled)+
                    " decision="+lastDecision+" position="+transform.position+" intended="+lastTarget+" floor="+lastFloorY+
                    " ready="+ready+" speed="+(agent?agent.speed:0)+" stoppingDistance="+(agent?agent.stoppingDistance:0)+
                    (ready?" path="+agent.pathStatus+" stopped="+agent.isStopped+" steering="+agent.steeringTarget+" remaining="+agent.remainingDistance:"")+
                    " hits="+lastHitCount+" firstHit="+(lastHitCollider?lastHitCollider.name:"none")+" hit="+lastHitPoint+" hitDistance="+lastHitDistance+
                    " door="+(door?door.name:"none")+" entry="+entrySide+" passing="+passing+" push="+push+" blockWait="+blockedWait+" closeWait="+closeWait;
            }
        }

        [Serializable] public sealed class Progress
        {
            public string door = "", deferredDoor = "";
            public bool passing;
            public float entrySide, push, closeWait, blockedWait, retryRemaining;
            public Vector3 deferredTarget;
            public void Validate()
            {
                if (!CorridorCheckpoint.Number(entrySide,-1,1) ||
                    !CorridorCheckpoint.Number(push,0,1.21f) || !CorridorCheckpoint.Number(closeWait,0,2.01f) ||
                    !CorridorCheckpoint.Number(blockedWait,0,6.01f) || !CorridorCheckpoint.Number(retryRemaining,0,2.01f) ||
                    !CorridorCheckpoint.Vector(deferredTarget) ||
                    (door != null && door.Length > 40) || (deferredDoor != null && deferredDoor.Length > 40) ||
                    passing && (Mathf.Abs(entrySide) != 1 || string.IsNullOrEmpty(door)))
                    throw new ArgumentException("Invalid physical door passage");
            }
        }

        public void Bind(Behaviour actor)
        {
            if (!actor || actor.gameObject != gameObject) throw new ArgumentException("Door actor must share the NavMesh root");
            if (owner && owner != actor) throw new InvalidOperationException("Door traversal already belongs to another actor");
            owner = actor; agent = GetComponent<NavMeshAgent>();
            if (approachPath == null) approachPath = new NavMeshPath();
        }
        public void Suspend()
        {
            // Pause/gaze/intro/attack owns locomotion. Keep the outstanding
            // request, without advancing timers or issuing another door cue.
            lastDecision="owner suspended locomotion";
            if (door && owner && owner.isActiveAndEnabled) door.HoldDoorPassage(owner);
        }
        void ClearPassage()
        {
            if (door && owner) door.ReleaseDoorPassage(owner);
            door = null; passing = false; entrySide = push = closeWait = blockedWait = 0;
        }
        public void Cancel()
        { ClearPassage(); deferredDoor = null; retryUntil = 0; deferredTarget = Vector3.zero; }
        void OnDisable() { Cancel(); }
        static float Horizontal(Vector3 value) { value.y = 0; return value.magnitude; }

        public static bool TryDoorApproach(NavMeshAgent walkingAgent,Interactable actualDoor,float fromSide,float floorY,NavMeshPath route)
        {
            if(!EnemyNavigation.Ready(walkingAgent)||!actualDoor||Mathf.Abs(fromSide)!=1)return false;
            // A hit near the leaf edge is not a safe handle approach: the real
            // obstacle's avoidance can deflect that capsule into the fixed jamb.
            // Walk to the actual opening centre on the observed entry side using
            // the same native route and authored stopping distance. No obstacle,
            // wall, actor speed or position is modified here.
            var at=actualDoor.transform.position+actualDoor.DoorNormal*fromSide*
                (.75f+walkingAgent.radius-walkingAgent.stoppingDistance);
            at.y=walkingAgent.transform.position.y;
            if(!EnemyNavigation.TryRoute(walkingAgent,at,floorY,route,12))return false;
            var corners=route.corners;
            return corners.Length>0&&Vector3.Dot(corners[corners.Length-1]-actualDoor.transform.position,actualDoor.DoorNormal)*fromSide>.1f;
        }

        public Result Tick(Vector3 intendedTarget, float floorY)
        {
            lastTarget=intendedTarget; lastFloorY=floorY; lastHitCollider=null; lastHitCount=0;
            lastHitPoint=Vector3.zero; lastHitDistance=0; lastDecision="scanning physical route";
            var session = GameSession.Current;
            if (!owner || !owner.isActiveAndEnabled || !session || !session.InputAllowed || session.EncountersResolved ||
                !EnemyNavigation.Ready(agent) || !EnemyNavigation.SameFloor(transform.position,floorY) ||
                !EnemyNavigation.SameFloor(intendedTarget,floorY))
            { EnemyNavigation.Stop(agent,true); Suspend(); return Result.Waiting; }
            float seconds = Time.deltaTime;
            if (!StealthRules.Finite(seconds) || seconds < 0)
            { EnemyNavigation.Stop(agent,true); return Result.Waiting; }
            if (door && (!EnemyNavigation.SameFloor(door.transform.position,floorY) ||
                Horizontal(transform.position-door.transform.position)>4.5f)) ClearPassage();
            if (deferredDoor && Horizontal(intendedTarget-deferredTarget)>.5f)
            { deferredDoor=null; retryUntil=0; }
            if (door)
            {
                door.HoldDoorPassage(owner);
                float side=Vector3.Dot(transform.position-door.transform.position,door.DoorNormal);
                if (passing && side*entrySide < -(agent.radius+.5f))
                {
                    if (door.HasOtherDoorPassage(owner) || !door.IsOpen || door.CloseForPursuer(owner)) ClearPassage();
                    else if (closeWait<2 && door.DoorOperable)
                    {
                        closeWait=Mathf.Min(2,closeWait+seconds); EnemyNavigation.Stop(agent,true);
                        lastDecision="occupied close-behind wait";
                        Face(door.transform.position); return Result.Waiting;
                    }
                    else ClearPassage();
                }
            }
            Vector3 target=intendedTarget;
            if (agent.hasPath && !door) target=agent.steeringTarget;
            Vector3 direction=target-transform.position; direction.y=0;
            if (direction.sqrMagnitude<.001f) return Result.Clear;
            float radius=agent.radius+.06f;
            Vector3 bottom=transform.position+Vector3.up*(radius+.12f);
            Vector3 top=transform.position+Vector3.up*Mathf.Max(radius+.12f,agent.height-radius);
            int count=Physics.CapsuleCastNonAlloc(bottom,top,radius,direction.normalized,hits,
                Mathf.Min(direction.magnitude,6),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            lastHitCount=count;
            if (count==hits.Length) { lastDecision="physical hit buffer full"; EnemyNavigation.Stop(agent,true); return Result.Waiting; }
            int first=-1; float distance=float.PositiveInfinity;
            for (int i=0;i<count;i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].collider.GetComponentInParent<StalkerBrain>() ||
                    hits[i].collider.GetComponentInParent<LanternMaskEncounter>() || hits[i].collider.GetComponentInParent<WeepingAngelEncounter>()) continue;
                if (hits[i].distance<distance) { first=i; distance=hits[i].distance; }
            }
            if (first<0) { lastDecision="route physically clear"; return Result.Clear; }
            var hit=hits[first]; lastHitCollider=hit.collider; lastHitPoint=hit.point; lastHitDistance=hit.distance;
            var nextDoor=hit.collider.GetComponentInParent<Interactable>();
            // Walls/furniture remain the native geometry; no door beyond the
            // first obstruction can be learned or operated remotely.
            if (!nextDoor || nextDoor.kind!=Interactable.Kind.Door || !nextDoor.movingLeaf ||
                !EnemyNavigation.SameFloor(nextDoor.transform.position,floorY))
            { lastDecision="first physical obstruction is not an operable door"; if (door && !passing) ClearPassage(); return Result.Clear; }
            if (nextDoor==deferredDoor && Time.time<retryUntil)
            { lastDecision="blocked route retry cooldown"; EnemyNavigation.Stop(agent,true); return Result.Blocked; }
            if (door!=nextDoor)
            {
                ClearPassage(); door=nextDoor;
                entrySide=Vector3.Dot(transform.position-door.transform.position,door.DoorNormal)>=0?1:-1;
            }
            door.HoldDoorPassage(owner);
            if (Horizontal(transform.position-hit.point)>Mathf.Max(1.25f,agent.radius+.85f))
            {
                push=0;
                if (!TryDoorApproach(agent,door,entrySide,floorY,approachPath))
                { lastDecision="door approach has no complete native route"; EnemyNavigation.Stop(agent,true); return Result.Waiting; }
                lastDecision="physically approaching door with authored stopping distance";
                agent.SetPath(approachPath); agent.isStopped=false;
                return Result.Waiting;
            }
            EnemyNavigation.Stop(agent,true); Face(hit.point);
            blockedWait=Mathf.Min(6,blockedWait+seconds);
            if (blockedWait>=6)
            {
                deferredDoor=door; deferredTarget=intendedTarget; retryUntil=Time.time+2;
                ClearPassage(); lastDecision="six-second physical blockage"; return Result.Blocked;
            }
            if (door.IsOpen) { lastDecision="waiting for real leaf capsule clearance"; passing=true; return Result.Waiting; }
            if (!door.DoorOperable) { lastDecision="physical door disabled"; return Result.Waiting; }
            lastDecision="physical closed-door push";
            push=Mathf.Min(1.2f,push+seconds);
            if (push>=1.2f && door.OpenForPursuer()) { lastDecision="opening requested once"; passing=true; push=0; }
            return Result.Waiting;
        }
        void Face(Vector3 point)
        {
            var facing=point-transform.position; facing.y=0;
            if (facing.sqrMagnitude>.001f) transform.rotation=Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(facing),180*Time.deltaTime);
        }
        public Progress CaptureProgress() => new Progress {
            door=door?door.stableId:"",passing=passing,entrySide=entrySide,push=push,closeWait=closeWait,blockedWait=blockedWait,
            deferredDoor=deferredDoor?deferredDoor.stableId:"",deferredTarget=deferredTarget,retryRemaining=Mathf.Max(0,retryUntil-Time.time) };
        public static bool ReferencesValid(Progress saved,Interactable[] doors)
        {
            if (saved==null) return true;
            bool Exists(string id)
            {
                if (string.IsNullOrEmpty(id)) return true;
                foreach (var item in doors) if (item && item.stableId==id) return true;
                return false;
            }
            return Exists(saved.door) && Exists(saved.deferredDoor);
        }
        public void RestoreProgress(Progress saved)
        {
            if (saved==null) { Cancel(); return; }
            saved.Validate();
            var doors=FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if (!ReferencesValid(saved,doors)) throw new ArgumentException("Unknown saved physical door");
            Interactable Find(string id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                foreach (var item in doors) if (item && item.stableId==id && item.gameObject.scene==gameObject.scene) return item;
                throw new ArgumentException("Saved physical door belongs to another scene");
            }
            var restoredDoor=Find(saved.door); var restoredDeferred=Find(saved.deferredDoor);
            Cancel(); door=restoredDoor; deferredDoor=restoredDeferred; passing=saved.passing;
            entrySide=saved.entrySide; push=saved.push; closeWait=saved.closeWait; blockedWait=saved.blockedWait;
            deferredTarget=saved.deferredTarget; retryUntil=Time.time+saved.retryRemaining;
        }
    }
}
