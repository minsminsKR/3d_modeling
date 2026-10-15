using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // AI owners gate their own timers/attacks. This collaborator owns the hold,
    // navigation/animation snapshot and restoration, without disabling those owners.
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class EnemyTrapRestraint : MonoBehaviour
    {
        readonly TrapHoldClock clock = new TrapHoldClock();
        readonly List<KeyValuePair<AnimationState,float>> clips = new List<KeyValuePair<AnimationState,float>>();
        readonly List<KeyValuePair<Animator,float>> animators = new List<KeyValuePair<Animator,float>>();
        NavMeshAgent agent;
        Behaviour owner;
        Vector3 anchor;
        Quaternion facing;
        bool stopped, rotation;
        public bool Held => clock.Held;
        public float Remaining => clock.Remaining;
        public int Sequence { get; private set; }
        public static bool IsHeld(Component actor) => actor && actor.TryGetComponent<EnemyTrapRestraint>(out var hold) && hold.Held;
        public static bool Eligible(NavMeshAgent candidate)
        {
            if (!candidate || !candidate.isActiveAndEnabled || !candidate.isOnNavMesh || IsHeld(candidate)) return false;
            if (candidate.TryGetComponent<StalkerBrain>(out var brain))
                return brain.isActiveAndEnabled && !(candidate.GetComponent<V1MonsterMotion>()?.IntroPoseActive ?? false);
            if (candidate.TryGetComponent<LanternMaskEncounter>(out var mask))
                return mask.isActiveAndEnabled && mask.IntroCompleted && mask.State != LanternMaskEncounter.Phase.Dormant &&
                    mask.State != LanternMaskEncounter.Phase.Resolved && mask.State != LanternMaskEncounter.Phase.Transforming;
            return candidate.TryGetComponent<WeepingAngelEncounter>(out var angel) && angel.isActiveAndEnabled && angel.Released && !angel.Resolved;
        }
        public static EnemyTrapRestraint TryCatch(NavMeshAgent candidate)
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || session.EncountersResolved || EnemyTimeStop.Active || !Eligible(candidate)) return null;
            var hold = candidate.GetComponent<EnemyTrapRestraint>();
            if (!hold) hold = candidate.gameObject.AddComponent<EnemyTrapRestraint>();
            hold.agent = candidate;
            hold.owner = (Behaviour)candidate.GetComponent<StalkerBrain>() ?? (Behaviour)candidate.GetComponent<LanternMaskEncounter>() ?? candidate.GetComponent<WeepingAngelEncounter>();
            hold.anchor = candidate.transform.position; hold.facing = candidate.transform.rotation;
            hold.stopped = candidate.isStopped; hold.rotation = candidate.updateRotation;
            if (!hold.clock.Begin()) return null;
            hold.Sequence++;
            foreach (var animation in candidate.GetComponentsInChildren<Animation>(true))
                foreach (AnimationState state in animation) { hold.clips.Add(new KeyValuePair<AnimationState,float>(state,state.speed)); state.speed = 0; }
            foreach (var animator in candidate.GetComponentsInChildren<Animator>(true))
            { hold.animators.Add(new KeyValuePair<Animator,float>(animator,animator.speed)); animator.speed = 0; }
            hold.StopBody(); return hold;
        }
        void StopBody()
        {
            if (EnemyNavigation.Ready(agent)) { agent.isStopped = true; agent.updateRotation = false; agent.velocity = Vector3.zero; agent.nextPosition = anchor; }
            transform.SetPositionAndRotation(anchor, facing);
        }
        void Update()
        {
            if (!Held) return;
            var session = GameSession.Current;
            if (!owner || !owner.isActiveAndEnabled || !session || session.Finished || session.EncountersResolved)
            { Release(); return; }
            StopBody();
            if (session.InputAllowed && !EnemyTimeStop.Active && clock.Tick(Time.deltaTime)) Release();
        }
        void LateUpdate() { if (Held) StopBody(); }
        void Release()
        {
            clock.Reset();
            foreach (var entry in clips) if (entry.Key != null) entry.Key.speed = entry.Value;
            foreach (var entry in animators) if (entry.Key) entry.Key.speed = entry.Value;
            clips.Clear(); animators.Clear();
            if (agent)
            {
                agent.updateRotation = rotation;
                if (EnemyNavigation.Ready(agent) && owner && owner.isActiveAndEnabled) agent.isStopped = stopped;
            }
        }
        void OnDisable() { if (Held || clips.Count > 0 || animators.Count > 0) Release(); }
    }
}
