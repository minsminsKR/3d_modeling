using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Finale coordinator: progression clock, one environment transition, and
    // actor creation. Navigation/attacks, pose and audio have separate owners.
    [DefaultExecutionOrder(650),DisallowMultipleComponent]
    public sealed class LadyEncounter : MonoBehaviour
    {
        [Serializable]public sealed class Progress
        {
            public int phase;public float remaining,poseClock;
            public Vector3 position;public Quaternion rotation=Quaternion.identity;
            public bool LegacyEmpty=>phase==0&&remaining==0&&poseClock==0&&position==Vector3.zero&&(rotation.Equals(default(Quaternion))||rotation.Equals(Quaternion.identity));
            public void Validate()
            {
                var clock=new LadyFinaleClock();clock.Restore(phase,remaining);
                if(!CorridorCheckpoint.Number(poseClock,0,1000000000)||!CorridorCheckpoint.Vector(position)||!ChapterCheckpoint.Rotation(rotation))
                    throw new ArgumentException("Invalid Lady checkpoint");
            }
        }
        CorridorRun run;GameSession session;Transform world;Behaviour[] existing;
        readonly LadyFinaleClock clock=new LadyFinaleClock();
        readonly LadyFrozenActors frozen=new LadyFrozenActors();
        Vector3 spawn;LadyFinaleAudio audioScope;
        public bool Active=>clock.Active;
        public bool Hunting=>clock.Hunting;
        public float Remaining=>clock.Remaining;
        public int FrozenActorCount=>frozen.Count;
        public LadyPursuit Actor {get;private set;}
        public LadyFinaleAudio AudioScope=>audioScope;
        public void Prepare(CorridorRun owner,Transform parent,IEnumerable<Behaviour> oldActors)
        {run=owner;world=parent;session=owner.GetComponent<GameSession>();existing=oldActors.ToArray();}
        public bool Begin()
        {
            if(!clock.Begin(run.Recovered,CorridorRun.Required))return false;
            spawn=ChooseSpawn();ApplyWorld(true);return true;
        }
        void ApplyWorld(bool impact)
        {
            foreach(var candle in run.Lighting.Candles)candle.SetFinaleBlackout(true);
            frozen.Capture(existing);
            audioScope=gameObject.AddComponent<LadyFinaleAudio>();audioScope.Begin(session,impact);
        }
        Vector3 ChooseSpawn()
        {
            var path=new NavMeshPath();var hiding=session.player.ActiveHidingPlace;
            var target=hiding&&hiding.outside?hiding.outside.position:session.player.transform.position;
            bool found=false;float best=float.NegativeInfinity;Vector3 selected=default;
            for(int cell=0;cell<CorridorLayout.Count;cell++)
            {
                if(!NavMesh.SamplePosition(run.CellPosition(cell),out var hit,1,NavMesh.AllAreas)||
                    !NavMesh.CalculatePath(hit.position,target,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;var previous=hit.position;
                foreach(var corner in path.corners){length+=Vector3.Distance(previous,corner);previous=corner;}
                if(length<18||length>72)continue;
                bool covered=!EnemyNavigation.ClearSight(hit.position+Vector3.up*1.5f,target+Vector3.up*1.5f,session.player);
                float score=-(Mathf.Abs(length-30))+(covered?8:0)+Mathf.Repeat(cell*.137f+run.Seed*.031f,1);
                if(score<=best)continue;
                found=true;best=score;selected=hit.position;
            }
            if(!found)throw new InvalidOperationException("No distant reachable Lady spawn in this corridor");
            return selected;
        }
        void Spawn(Vector3 point,Quaternion rotation,float poseClock)
        {
            if(Actor)return;
            var root=new GameObject("lady — final memory pursuer");root.transform.SetParent(world,true);
            root.transform.SetPositionAndRotation(point,rotation);
            var agent=root.AddComponent<NavMeshAgent>();agent.radius=.30f;agent.height=2.05f;
            agent.speed=3.9f;agent.acceleration=18;agent.angularSpeed=720;agent.stoppingDistance=.38f;
            agent.obstacleAvoidanceType=ObstacleAvoidanceType.NoObstacleAvoidance;
            if(!agent.Warp(point)||!agent.isOnNavMesh)throw new InvalidOperationException("Lady spawn missed the real NavMesh");
            Actor=root.AddComponent<LadyPursuit>();
            Actor.Prepare(session.player,()=>session&&session.InputAllowed&&!session.Finished,hidden=>
            {
                const string hint="lady는 숨거나 덫으로 멈출 수 없습니다. 부적으로 시간을 벌어 제단으로 가세요.";
                if(hidden)session.TryCabinetDefeat(Actor,"lady",hint);
                else session.TryDefeat("lady",hint);
            });
            Actor.Motion.RestoreClock(poseClock);
        }
        void Update()
        {
            if(!session||session.Finished)return;
            if(clock.Tick(Time.deltaTime,session.InputAllowed,EnemyTimeStop.Active))
                Spawn(spawn,Quaternion.identity,0);
        }
        void LateUpdate(){if(Active)frozen.Hold();}
        public Progress Capture()=>new Progress{phase=clock.Phase,remaining=clock.Remaining,
            position=Actor?Actor.transform.position:spawn,rotation=Actor?Actor.transform.rotation:Quaternion.identity,
            poseClock=Actor?Actor.Motion.Clock:0};
        public void ValidateRestore(Progress data)
        {
            if(data==null)return;data.Validate();
            var route=new NavMeshPath();
            if(data.phase>0&&(!NavMesh.SamplePosition(data.position,out var hit,.25f,NavMesh.AllAreas)||
                Vector3.Distance(hit.position,data.position)>.25f||
                !NavMesh.CalculatePath(hit.position,run.CellPosition(0),NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete))
                throw new ArgumentException("Invalid saved Lady navigation point");
        }
        public void Restore(Progress data)
        {
            if(run.Recovered<CorridorRun.Required)return;
            if(data==null)
            {
                // Old completed-memory saves do not replay the transition impact.
                clock.Restore(2,0);spawn=ChooseSpawn();ApplyWorld(false);Spawn(spawn,Quaternion.identity,0);return;
            }
            ValidateRestore(data);clock.Restore(data.phase,data.remaining);spawn=data.position;
            if(!Active)return;
            ApplyWorld(false);if(Hunting)Spawn(data.position,data.rotation,data.poseClock);
        }
        void OnDestroy(){frozen.Release();}
    }
}
