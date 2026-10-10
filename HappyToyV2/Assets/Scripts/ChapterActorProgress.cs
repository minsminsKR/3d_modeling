using System;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class WeepingAngelEncounter
    {
        [Serializable] public sealed class ChapterProgress
        {
            public bool active, released;
            public Vector3 position;
            public Quaternion rotation, visualRotation;
            public float unobserved, soundRemaining;
            public int paths, attacks;
            public StalkerDoorTraversal.Progress doorPassage;
            public void Validate()
            {
                if(!ChapterCheckpoint.Point(position) || !ChapterCheckpoint.Rotation(rotation) || !ChapterCheckpoint.Rotation(visualRotation) ||
                    !CorridorCheckpoint.Number(unobserved,0,1000000000) || !CorridorCheckpoint.Number(soundRemaining,0,.81f) ||
                    paths<0 || attacks<0 || released && !active) throw new ArgumentException("Invalid mannequin checkpoint");
                doorPassage?.Validate();
            }
        }
        public ChapterProgress CaptureChapterProgress() => new ChapterProgress {
            active=gameObject.activeSelf,released=Released,position=transform.position,rotation=transform.rotation,
            visualRotation=visual?visual.localRotation:Quaternion.identity,unobserved=unobservedFor,
            soundRemaining=Mathf.Max(0,soundTimer),paths=PathRequests,attacks=AttacksStarted,
            doorPassage=doorTraversal?doorTraversal.CaptureProgress():null };
        public void RestoreChapterProgress(ChapterProgress data)
        {
            data.Validate(); agent=GetComponent<NavMeshAgent>(); agent.enabled=false;
            transform.SetPositionAndRotation(data.position,data.rotation); gameObject.SetActive(data.active);
            if(data.active) { agent.enabled=true; if(!agent.Warp(data.position)) throw new InvalidOperationException("Mannequin restore has no floor"); }
            floorY=data.position.y; Triggered=Released=data.released; Resolved=false; Observed=Moving=false;
            intro=data.released?2.8f:0; partialCueIssued=settleCueIssued=data.released; attack.Reset(); repath=0;
            unobservedFor=data.unobserved; soundTimer=data.soundRemaining; PathRequests=data.paths; AttacksStarted=data.attacks;
            if(visual) { visual.gameObject.SetActive(true); visual.localRotation=data.visualRotation; }
            RestoreDisplayLight(); EnemyNavigation.Stop(agent,true);
            DoorTraversal.RestoreProgress(data.doorPassage);
        }
    }
    public sealed partial class LanternMaskEncounter
    {
        [Serializable] public sealed class ChapterProgress
        {
            public bool active, introComplete, transformed, cueIssued;
            public Phase state;
            public Vector3 position, target;
            public Quaternion rotation;
            public float age, memory, awareness;
            public int waypoint, curses, attacks, noises, doorsShattered;
            public StalkerDoorTraversal.Progress doorPassage;
            public NoiseInvestigationClock.Progress investigation;
            public float investigationYaw;
            // JsonUtility can turn a missing inline class into its empty fields.
            // Only this representation may accompany the legacy corridor marker.
            public bool LegacyEmpty => !active && !introComplete && !transformed && !cueIssued && state == Phase.Dormant &&
                position == Vector3.zero && target == Vector3.zero && rotation.Equals(default(Quaternion)) && age == 0 && memory == 0 &&
                awareness == 0 && waypoint == 0 && curses == 0 && attacks == 0 && noises == 0 && doorsShattered == 0 && investigationYaw == 0 &&
                (doorPassage == null || doorPassage.LegacyEmpty) &&
                (investigation == null || investigation.version >= 0 && investigation.version <= 1 && investigation.LegacyEmpty);
            public void Validate() => Validate(false);
            public void ValidateCorridor() => Validate(true);
            void Validate(bool corridor)
            {
                if(!(corridor ? CorridorCheckpoint.Point(position) : ChapterCheckpoint.Point(position)) || !CorridorCheckpoint.Vector(target) || !ChapterCheckpoint.Rotation(rotation) ||
                    !Enum.IsDefined(typeof(Phase),state) || state==Phase.Transforming || state==Phase.Resolved ||
                    !CorridorCheckpoint.Number(age,0,1000000000) || !CorridorCheckpoint.Number(memory,0,state==Phase.Investigate?3600:8.01f) ||
                    !CorridorCheckpoint.Number(awareness,0,1) || waypoint<0 || waypoint>(corridor?3:2) || curses<0 || attacks<0 || noises<0 ||
                    doorsShattered < 0 || doorsShattered > 162 || introComplete && !active || transformed && !introComplete)
                    throw new ArgumentException("Invalid mask checkpoint");
                doorPassage?.Validate(); investigation?.Validate();
                if(!CorridorCheckpoint.Number(investigationYaw,0,360) ||
                    investigation!=null && investigation.active && state==Phase.Investigate &&
                    Vector3.Distance(investigation.point,target)>.002f) throw new ArgumentException("Invalid mask investigation evidence");
            }
        }
        public ChapterProgress CaptureChapterProgress() => new ChapterProgress {
            active=gameObject.activeSelf,introComplete=IntroCompleted,transformed=Transformed,cueIssued=recognitionCueIssued,
            state=State,position=transform.position,target=target,rotation=transform.rotation,age=age,memory=Mathf.Max(0,memory),
            awareness=awareness.Value,waypoint=waypoint,curses=CursesApplied,attacks=AttacksStarted,noises=FootstepNoisesAccepted,doorsShattered=DoorsShattered,
            doorPassage=doorTraversal?doorTraversal.CaptureProgress():null,
            investigation=investigation.Capture(),investigationYaw=investigationFacing.eulerAngles.y };
        public void RestoreChapterProgress(ChapterProgress data)
        { data.Validate(); RestoreMaskProgress(data); }
        public void RestoreCorridorProgress(ChapterProgress data)
        { data.ValidateCorridor(); RestoreMaskProgress(data); }
        void RestoreMaskProgress(ChapterProgress data)
        {
            agent=GetComponent<NavMeshAgent>(); agent.enabled=false;
            transform.SetPositionAndRotation(data.position,data.rotation); gameObject.SetActive(data.active);
            if(data.active) { agent.enabled=true; if(!agent.Warp(data.position)) throw new InvalidOperationException("Mask restore has no floor"); }
            floorY=data.position.y; State=data.state; target=data.target; age=data.age; memory=data.memory; waypoint=data.waypoint;
            IntroStarted=IntroCompleted=data.introComplete; IntroElapsed=data.introComplete?2.2f:0; riseCueIssued=data.introComplete;
            Transformed=data.transformed; transformTime=data.transformed?5:0; recognitionCueIssued=data.cueIssued;
            awareness.Restore(data.awareness); CursesApplied=data.curses; AttacksStarted=data.attacks; FootstepNoisesAccepted=data.noises;
            DoorsShattered=data.doorsShattered;
            if (CorridorRunner && data.active) PrepareCorridorRunner();
            attack.Reset(); repath=0; EnemyNavigation.Stop(agent,true); SetVisible(data.active); if(data.active) Visual();
            DoorTraversal.RestoreProgress(data.doorPassage);
            investigation.Restore(data.investigation); investigationFacing=Quaternion.Euler(0,data.investigationYaw,0);
        }
    }
    public sealed partial class V1HwacatEvent
    {
        public void RestoreChapterCompletion(bool completed,bool witnessed)
        {
            if(!chapterDriven || witnessed && !completed) throw new ArgumentException("Invalid portrait restoration");
            StopAllCoroutines(); Cancelled=false; Triggered=Completed=completed; ChapterWitnessed=witnessed;
            chapterSightTime=witnessed?.5f:0; Phase=completed?"done":"idle"; PhaseElapsed=RevealElapsed=0;
            if(normal) normal.SetActive(false); RestorePainting();
            if(completed && capturedPainting && painting)
            {
                var start=painting.position; var rotation=painting.rotation;
                painting.position=new Vector3(start.x,spawn.y+.10f,start.z-.35f);
                painting.rotation=rotation*Quaternion.Euler(85,0,0);
            }
            if(angry) angry.enabled=true;
        }
    }
    public sealed partial class AnnexEncounter
    {
        public void RestoreChapterCompletion(bool released)
        {
            if(!chapterDriven) throw new InvalidOperationException("Nursery is not chapter driven");
            StopAllCoroutines(); Cancelled=false; Triggered=Released=released; Phase=released?"released":"idle";
            RevealElapsed=released?5:0; RestoreLight();
            if(monster) { monster.enabled=true; var motion=monster.GetComponent<V1MonsterMotion>(); if(motion) motion.enabled=true; }
        }
    }
}
