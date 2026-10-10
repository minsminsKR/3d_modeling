using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class PlayerMotor
    {
        [Serializable] public sealed class Progress
        {
            public Vector3 position;
            public float yaw, pitch, stamina, slow, noiseRadius, noiseRemaining, itemCooldown;
            public bool crouched, exhausted, light;
            public int stock;
            // Legacy checkpoints omit these fields and correctly start with no prior entry.
            public int hidingEntry, hidingRolls;
            public CabinetHidingOutcome hidingOutcome;
            public void Validate()
            { ValidatePosition(false); }
            public void ValidateChapter()
            { ValidatePosition(true); }
            void ValidatePosition(bool chapter)
            {
                CabinetHidingRules.Validate(hidingEntry, hidingRolls, hidingOutcome);
                if(!(chapter ? ChapterCheckpoint.Point(position) : CorridorCheckpoint.Point(position)) || !CorridorCheckpoint.Number(yaw,0,360) || !CorridorCheckpoint.Number(pitch,-78,78) ||
                    !CorridorCheckpoint.Number(stamina,0,1) || !CorridorCheckpoint.Number(slow,0,3600) ||
                    !CorridorCheckpoint.Number(noiseRadius,0,28) || !CorridorCheckpoint.Number(noiseRemaining,0,.81f) ||
                    !CorridorCheckpoint.Number(itemCooldown,0,.51f) || stock < 0 || stock > FirecrackerInventory.Capacity)
                    throw new ArgumentException("Invalid player checkpoint");
            }
        }
        public Progress CaptureProgress()
        {
            if(Hidden) throw new InvalidOperationException("Leave hiding before suspending");
            return new Progress { position=transform.position,yaw=transform.eulerAngles.y,pitch=pitch,stamina=Stamina,
                slow=SlowRemaining,noiseRadius=FootstepNoiseRadius,noiseRemaining=FootstepNoiseRemaining,
                itemCooldown=Firecrackers.Cooldown,stock=Firecrackers.Count,crouched=Crouching,exhausted=SprintExhausted,
                light=flashlight&&flashlight.enabled,hidingEntry=HidingEntryId,hidingRolls=HidingRolls,hidingOutcome=HidingOutcome };
        }
        public void RestoreProgress(Progress data)
        { data.Validate(); RestoreValidatedProgress(data); }
        public void RestoreChapterProgress(Progress data)
        { data.ValidateChapter(); RestoreValidatedProgress(data); }
        void RestoreValidatedProgress(Progress data)
        {
            controller.enabled=false;
            Hidden=false; hidingPlace=warnedHidingPlace=null; hidingThreatUntil=0;
            hidingDecision.Restore(data.hidingEntry,data.hidingRolls,data.hidingOutcome);
            transform.SetPositionAndRotation(data.position,Quaternion.Euler(0,data.yaw,0)); pitch=data.pitch;
            Crouching=data.crouched; StandingBlocked=false;
            controller.height=Crouching?crouchedHeight:standingHeight;
            controller.center=standingCenter-Vector3.up*(standingHeight-controller.height)*.5f;
            Stamina=data.stamina; SprintExhausted=data.exhausted; SlowRemaining=data.slow;
            FootstepNoiseRadius=data.noiseRadius; FootstepNoiseRemaining=data.noiseRemaining;
            Running=sprintRequested=false; ActualSpeed=0; moveDirection=Vector3.zero; fallSpeed=-2;
            if(eyes) eyes.transform.localRotation=Quaternion.Euler(pitch,0,0);
            if(flashlight) flashlight.enabled=data.light;
            Firecrackers.RestoreStock(data.stock,data.itemCooldown); controller.enabled=true;
        }
        public bool CanRestoreProgress(Progress data)
        { data.Validate(); return CanRestoreValidatedProgress(data,(1<<8)|(1<<9)); }
        public bool CanRestoreChapterProgress(Progress data)
        { data.ValidateChapter(); return CanRestoreValidatedProgress(data,Physics.DefaultRaycastLayers); }
        bool CanRestoreValidatedProgress(Progress data,int supportLayers)
        {
            if(!NavMesh.SamplePosition(data.position,out _,.65f,NavMesh.AllAreas) ||
                !Physics.Raycast(data.position+Vector3.up*.15f,Vector3.down,.4f,supportLayers,QueryTriggerInteraction.Ignore)) return false;
            float height=data.crouched?crouchedHeight:standingHeight;
            var center=standingCenter-Vector3.up*(standingHeight-height)*.5f+data.position;
            float half=Mathf.Max(0,height*.5f-controller.radius);
            return !Physics.OverlapCapsule(center-Vector3.up*half,center+Vector3.up*half,controller.radius-.02f,~0,QueryTriggerInteraction.Ignore)
                .Any(x=>!x.transform.IsChildOf(transform)&&!x.GetComponentInParent<StalkerBrain>());
        }
    }
    public sealed partial class StalkerBrain
    {
        [Serializable] public sealed class Progress
        {
            public bool active, witnessed, cueIssued, searchArrived, searchStarted, patrolDwelling, passingDoor;
            public State state;
            public Vector3 position,lastKnown,hidingApproach,searchOrigin,searchTarget;
            public float yaw,floor,memory,awareness,searchDwell,searchTransit,searchDoorWait,searchYaw,patrolRemaining,doorPush,doorEntrySide,doorCloseWait,doorBlockedWait;
            public int waypoint,searchCandidate,visited,attacks,noises,footstepNoises;
            public string door;
            public NoiseInvestigationClock.Progress investigation;
            public int babyVersion;
            public CorridorBabyMemory.Progress baby;
            public float investigationYaw;
            public void Validate()
            { ValidatePosition(false); }
            public void ValidateChapter()
            { ValidatePosition(true); }
            void ValidatePosition(bool chapter)
            {
                Func<Vector3,bool> point = chapter ? ChapterCheckpoint.Point : CorridorCheckpoint.Point;
                if(!point(position) || !Enum.IsDefined(typeof(State),state) ||
                    !CorridorCheckpoint.Number(yaw,0,360) || !CorridorCheckpoint.Number(floor,chapter?-6:-.15f,chapter?8:.85f) ||
                    !CorridorCheckpoint.Vector(lastKnown) || !CorridorCheckpoint.Vector(hidingApproach) ||
                    !CorridorCheckpoint.Vector(searchOrigin) || !CorridorCheckpoint.Vector(searchTarget) ||
                    active && state!=State.Patrol && !point(lastKnown) ||
                    active && state==State.Search && (!point(searchOrigin)||!point(searchTarget)) ||
                    !CorridorCheckpoint.Number(memory,0,3600) || !CorridorCheckpoint.Number(awareness,0,1) ||
                    !CorridorCheckpoint.Number(searchDwell,0,4) || !CorridorCheckpoint.Number(searchTransit,0,chapter?40.1f:8.1f) ||
                    !CorridorCheckpoint.Number(searchDoorWait,0,4.01f) || !CorridorCheckpoint.Number(searchYaw,0,360) ||
                    !CorridorCheckpoint.Number(patrolRemaining,0,1.81f) || !CorridorCheckpoint.Number(doorPush,0,1.21f) ||
                    !CorridorCheckpoint.Number(doorEntrySide,-1,1) || !CorridorCheckpoint.Number(doorCloseWait,0,2.1f) ||
                    !CorridorCheckpoint.Number(doorBlockedWait,0,6.11f) ||
                    passingDoor && (Mathf.Abs(doorEntrySide) != 1 || string.IsNullOrEmpty(door)) ||
                    waypoint<0 || waypoint>(chapter?1000:3) || searchCandidate<0 || searchCandidate>8 || visited<0 || visited>9 ||
                    attacks<0 || noises<0 || footstepNoises<0 || door==null || door.Length>40)
                    throw new ArgumentException("Invalid threat checkpoint");
                investigation?.Validate();
                if (babyVersion < 0 || babyVersion > 1 || babyVersion == 1 && baby == null ||
                    babyVersion == 0 && baby != null && !baby.LegacyEmpty)
                    throw new ArgumentException("Invalid Baby checkpoint marker");
                if (babyVersion == 1)
                {
                    baby.Validate();
                    if (state != (baby.phase == CorridorBabyMemory.Phase.Chasing ? State.Chase :
                        baby.phase == CorridorBabyMemory.Phase.InvestigatingCry ? State.Investigate : State.Patrol))
                        throw new ArgumentException("Baby phase does not match saved pursuit");
                }
                if(!CorridorCheckpoint.Number(investigationYaw,0,360) ||
                    investigation!=null && investigation.active && state==State.Investigate &&
                    Vector3.Distance(investigation.point,lastKnown)>.002f) throw new ArgumentException("Invalid investigation evidence snapshot");
            }
        }
        public Progress CaptureProgress()
        {
            if(AttackActive) throw new InvalidOperationException("Resolve the attack before suspending");
            return new Progress { active=gameObject.activeSelf,state=state,position=transform.position,yaw=transform.eulerAngles.y,
                floor=floorY,lastKnown=lastKnown,hidingApproach=hidingApproach,searchOrigin=searchOrigin,searchTarget=searchTarget,
                memory=Mathf.Max(0,memory),awareness=awareness.Value,witnessed=witnessedHiding,cueIssued=recognitionCueIssued,
                searchArrived=searchArrived,searchStarted=searchStarted,searchDwell=searchDwell,searchTransit=Mathf.Max(0,searchTransit),
                searchDoorWait=searchDoorWait,searchYaw=searchFacing.eulerAngles.y,searchCandidate=searchCandidate,
                visited=SearchPointsVisited,waypoint=patrol!=null&&patrol.Length>0?waypoint%patrol.Length:0,patrolDwelling=patrolDwelling,
                patrolRemaining=patrolDwelling?Mathf.Max(0,patrolDwellUntil-Time.time):0,
                door=blockingDoor?blockingDoor.stableId:"",doorPush=doorPush,passingDoor=passingDoor,doorEntrySide=doorEntrySide,doorCloseWait=doorCloseWait,doorBlockedWait=doorBlockedWait,
                attacks=AttacksStarted,noises=NoisesAccepted,footstepNoises=FootstepNoisesAccepted,
                investigation=investigation.Capture(),investigationYaw=investigationFacing.eulerAngles.y,
                babyVersion=CorridorBaby ? 1 : 0, baby=CorridorBaby ? CorridorBaby.Memory.Capture() : null };
        }
        public void RestoreProgress(Progress data,Interactable[] doors)
        { data.Validate(); RestoreValidatedProgress(data,doors); }
        public void RestoreChapterProgress(Progress data,Interactable[] doors)
        { data.ValidateChapter(); RestoreValidatedProgress(data,doors); }
        void RestoreValidatedProgress(Progress data,Interactable[] doors)
        {
            ClearDoorPassage();
            agent=GetComponent<NavMeshAgent>(); agent.enabled=false;
            transform.SetPositionAndRotation(data.position,Quaternion.Euler(0,data.yaw,0));
            gameObject.SetActive(data.active);
            if(data.active)
            {
                agent.enabled=true;
                if(!agent.Warp(data.position)) throw new InvalidOperationException("Cannot restore threat to navigation");
                EnemyNavigation.Stop(agent,true);
            }
            floorY=data.floor; state=data.state; lastKnown=data.lastKnown; hidingApproach=data.hidingApproach;
            memory=data.memory; awareness.Restore(data.awareness); witnessedHiding=data.witnessed; recognitionCueIssued=data.cueIssued;
            attack.Reset(); attackingHiding=false; witnessedHidingEntry=attackHidingEntry=0; repath=0;
            searchOrigin=data.searchOrigin; searchTarget=data.searchTarget; searchFacing=Quaternion.Euler(0,data.searchYaw,0);
            searchArrived=data.searchArrived; searchStarted=data.searchStarted; searchDwell=data.searchDwell;
            searchTransit=data.searchTransit; searchDoorWait=data.searchDoorWait; searchCandidate=data.searchCandidate;
            SearchPointsVisited=data.visited; waypoint=data.waypoint; patrolDwelling=data.patrolDwelling;
            patrolDwellUntil=Time.time+data.patrolRemaining;
            blockingDoor=string.IsNullOrEmpty(data.door)?null:doors.Single(x=>x.stableId==data.door); doorPush=data.doorPush;
            passingDoor=data.passingDoor; doorEntrySide=data.doorEntrySide; doorCloseWait=data.doorCloseWait;
            doorBlockedWait=data.doorBlockedWait;
            // Old saves did not carry passage stages. Their pending closed-door
            // push safely reconstructs its entry side on first approach.
            if (blockingDoor && doorEntrySide == 0)
                doorEntrySide=Vector3.Dot(transform.position-blockingDoor.transform.position,blockingDoor.DoorNormal)>=0?1:-1;
            AttacksStarted=data.attacks; NoisesAccepted=data.noises; FootstepNoisesAccepted=data.footstepNoises;
            investigation.Restore(data.investigation); investigationFacing=Quaternion.Euler(0,data.investigationYaw,0);
            if (CorridorBaby)
            {
                var savedBaby = data.babyVersion == 1 ? data.baby : null;
                CorridorBaby.Memory.Restore(savedBaby);
                // Legacy saved pursuit is still real pursuit. Older patrol saves
                // start from the new waiting state because no chase history exists.
                if (savedBaby == null && state == State.Chase) CorridorBaby.Memory.SeePlayer();
                else if (savedBaby == null && state == State.Investigate) CorridorBaby.Memory.HearSmall();
                else if (savedBaby == null && state == State.Search)
                { CorridorBaby.Memory.SeePlayer(); CorridorBaby.Memory.LosePlayer(); state = State.Patrol; }
            }
        }
    }
}
