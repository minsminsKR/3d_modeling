using System;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // No sight/hearing memory or investigation goal: Lady always routes toward
    // the actual player. Trap eligibility intentionally excludes this AI type.
    [DisallowMultipleComponent,RequireComponent(typeof(NavMeshAgent))]
    public sealed class LadyPursuit : MonoBehaviour
    {
        NavMeshAgent agent;PlayerMotor target;Func<bool> playing;Action<bool> catchPlayer;
        NavMeshPath route;readonly StalkerDoorPassage passage=new StalkerDoorPassage();
        float repath,contact;
        public NavMeshAgent Agent=>agent;
        public Vector3 PursuitTarget {get;private set;}
        public bool TrapImmune=>true;
        public LadyMotion Motion {get;private set;}
        public void Prepare(PlayerMotor player,Func<bool> canAdvance,Action<bool> caught)
        {
            agent=GetComponent<NavMeshAgent>();route=new NavMeshPath();target=player;playing=canAdvance;catchPlayer=caught;
            var source=Resources.Load<GameObject>("Lady/LadyRuntime");
            if(!source)throw new InvalidOperationException("Generate/import Lady's actual model before starting the finale");
            var model=Instantiate(source,transform,false);model.name="lady visual";
            Motion=gameObject.AddComponent<LadyMotion>();
            Motion.Prepare(model,canAdvance,()=>GameSession.Current&&GameSession.Current.Shell.ReducedMotion);
        }
        void Update()
        {
            if(!target||playing==null||!playing()||EnemyTimeStop.Active)
            {EnemyNavigation.Stop(agent);passage.Hold(this);return;}
            var hiding=target.ActiveHidingPlace;
            PursuitTarget=hiding&&hiding.outside?hiding.outside.position:target.transform.position;
            repath-=Time.deltaTime;
            if(repath<=0)
            {
                repath=.16f;
                if(EnemyNavigation.TryRoute(agent,PursuitTarget,transform.position.y,route))
                    EnemyNavigation.FollowPath(agent,route);
                else EnemyNavigation.Stop(agent);
            }
            var door=passage.Tick(this,agent,route,PursuitTarget,transform.position.y,Time.deltaTime);
            if(door.Result!=StalkerDoorPassage.Outcome.Clear){contact=0;return;}
            var horizontal=PursuitTarget-transform.position;horizontal.y=0;
            bool close=horizontal.sqrMagnitude<(hiding?1.05f:.72f)*(hiding?1.05f:.72f);
            bool clear=EnemyNavigation.ClearSight(transform.position+Vector3.up*1.1f,
                (hiding?PursuitTarget:target.transform.position)+Vector3.up*1.1f,target);
            contact=close&&clear?contact+Time.deltaTime:0;
            if(contact>=.18f){contact=0;catchPlayer(hiding);}
        }
        void OnDisable(){passage?.Clear(this);EnemyNavigation.Stop(agent);}
    }
}
