using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Only patrol progress: goals/state changes stay with the brain. Do not
    // mistake an intentional handle wait, crowd yield or arrival for a wall.
    internal sealed class StalkerPatrolProgress
    {
        readonly PatrolProgressClock clock=new PatrolProgressClock();
        Vector3 anchor,goal;bool observing;
        public int Recoveries=>clock.Recoveries;
        public void Reset(){clock.Reset();observing=false;}
        public bool Tick(NavMeshAgent agent,Vector3? target,float seconds)
        {
            var traffic=agent.GetComponent<EnemyPassageTraffic>();
            bool walking=target.HasValue&&EnemyNavigation.Ready(agent)&&!agent.isStopped&&agent.hasPath&&
                !agent.pathPending&&agent.speed>.01f&&!(traffic&&traffic.Yielding)&&
                Vector3.Distance(agent.transform.position,target.Value)>.6f;
            if(!walking){Reset();return false;}
            var position=agent.transform.position;
            if(!observing||(goal-target.Value).sqrMagnitude>.01f)
            {Reset();observing=true;anchor=position;goal=target.Value;}
            var delta=position-anchor;delta.y=0;
            bool recover=clock.Tick(seconds,delta.magnitude,true);
            if(recover||delta.sqrMagnitude>=PatrolProgressClock.MinimumDisplacement*PatrolProgressClock.MinimumDisplacement)anchor=position;
            return recover;
        }
    }
}
