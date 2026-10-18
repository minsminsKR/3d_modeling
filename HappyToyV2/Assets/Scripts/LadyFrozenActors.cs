using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Finale owns a permanent hold of the old enemies, separate from talisman
    // lifetime. Pose snapshots keep already-running legacy blends motionless.
    internal sealed class LadyFrozenActors
    {
        sealed class State
        {
            internal Behaviour owner;internal bool enabled,stopped;
            internal NavMeshAgent agent;internal Vector3 point;internal Quaternion rotation;
            internal readonly List<(Transform part,Vector3 point,Quaternion rotation,Vector3 scale,bool active)> pose=new List<(Transform,Vector3,Quaternion,Vector3,bool)>();
            internal readonly List<(Renderer part,bool enabled)> renderers=new List<(Renderer,bool)>();
            internal readonly List<(Behaviour part,bool enabled)> animations=new List<(Behaviour,bool)>();
        }
        readonly List<State> states=new List<State>();
        internal int Count=>states.Count;
        internal void Capture(IEnumerable<Behaviour> owners)
        {
            foreach(var owner in owners)
            {
                if(!owner||!owner.gameObject.activeInHierarchy)continue;
                var agent=owner.GetComponent<NavMeshAgent>();
                var state=new State{owner=owner,enabled=owner.enabled,agent=agent,
                    stopped=EnemyNavigation.Ready(agent)&&agent.isStopped,point=owner.transform.position,rotation=owner.transform.rotation};
                foreach(var part in owner.GetComponentsInChildren<Transform>(true))
                    state.pose.Add((part,part.localPosition,part.localRotation,part.localScale,part.gameObject.activeSelf));
                foreach(var renderer in owner.GetComponentsInChildren<Renderer>(true))state.renderers.Add((renderer,renderer.enabled));
                foreach(var animation in owner.GetComponentsInChildren<Animation>(true))state.animations.Add((animation,animation.enabled));
                foreach(var animator in owner.GetComponentsInChildren<Animator>(true))state.animations.Add((animator,animator.enabled));
                foreach(var motion in owner.GetComponentsInChildren<V1MonsterMotion>(true))state.animations.Add((motion,motion.enabled));
                foreach(var motion in owner.GetComponentsInChildren<MaskHorrorVisual>(true))state.animations.Add((motion,motion.enabled));
                owner.enabled=false;states.Add(state);
            }
            Hold();
        }
        internal void Hold()
        {
            foreach(var state in states)
            {
                if(!state.owner)continue;
                state.owner.enabled=false;
                EnemyNavigation.Stop(state.agent);
                if(EnemyNavigation.Ready(state.agent))state.agent.nextPosition=state.point;
                state.owner.transform.SetPositionAndRotation(state.point,state.rotation);
                foreach(var entry in state.animations)if(entry.part)entry.part.enabled=false;
                foreach(var entry in state.pose)if(entry.part)
                {
                    if(entry.part.gameObject.activeSelf!=entry.active)entry.part.gameObject.SetActive(entry.active);
                    entry.part.SetLocalPositionAndRotation(entry.point,entry.rotation);entry.part.localScale=entry.scale;
                }
                // Some brains hide their model in OnDisable. A permanent hold
                // retains that exact visible body instead of making it vanish.
                foreach(var entry in state.renderers)if(entry.part)entry.part.enabled=entry.enabled;
            }
        }
        internal void Release()
        {
            foreach(var state in states)
            {
                if(!state.owner)continue;
                state.owner.enabled=state.enabled;
                foreach(var entry in state.animations)if(entry.part)entry.part.enabled=entry.enabled;
                if(EnemyNavigation.Ready(state.agent))state.agent.isStopped=state.stopped;
            }
            states.Clear();
        }
    }
}
