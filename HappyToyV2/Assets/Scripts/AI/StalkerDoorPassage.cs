using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Owns the physical passage and its finite waits. It does not know an AI
    // state or choose search/patrol goals: a blocked route returns to the owner.
    internal sealed class StalkerDoorPassage
    {
        internal enum Outcome { Clear, Waiting, Blocked }
        internal readonly struct Decision
        {
            internal readonly Outcome Result;
            internal readonly bool Repath;
            internal readonly Vector3 HandlePoint;
            internal Decision(Outcome result, bool repath = false, Vector3 handlePoint = default)
            { Result = result; Repath = repath; HandlePoint = handlePoint; }
        }

        // The checkpoint adapter writes these stages only while restoring.
        internal Interactable Door;
        internal float Push, EntrySide, CloseWait, BlockedWait;
        internal bool Passing;
        readonly RaycastHit[] hits = new RaycastHit[32];

        internal void Clear(Behaviour owner)
        {
            if (Door) Door.ReleaseDoorPassage(owner);
            Door = null;
            Push = EntrySide = CloseWait = BlockedWait = 0;
            Passing = false;
        }

        internal void Hold(Behaviour owner)
        {
            if (Door) Door.HoldDoorPassage(owner);
        }

        internal Decision Tick(Behaviour owner, NavMeshAgent agent, NavMeshPath route,
            Vector3? intendedTarget, float homeFloor, float seconds, float bodyReach = 0)
        {
            var actor = owner.transform;
            bool repath = false;
            if (Door && (!EnemyNavigation.SameActorFloor(agent, Door.transform.position, homeFloor) ||
                HorizontalDistance(actor.position, Door.transform.position) > 4.5f)) Clear(owner);
            if (Door)
            {
                Hold(owner);
                float side = Vector3.Dot(actor.position - Door.transform.position, Door.DoorNormal);
                if (Passing && side * EntrySide < -Mathf.Max(agent.radius + .5f, bodyReach + .15f))
                {
                    // Close after every body has cleared; the last actor owns the handle.
                    if (Door.HasOtherDoorPassage(owner) || !Door.IsOpen || Door.CloseForPursuer(owner, bodyReach + .65f))
                    { Clear(owner); repath = true; }
                    else if (CloseWait < 2 && Door.DoorOperable)
                    {
                        CloseWait = Mathf.Min(2, CloseWait + seconds);
                        EnemyNavigation.Stop(agent, true);
                        Face(actor, Door.transform.position, seconds);
                        return new Decision(Outcome.Waiting);
                    }
                    else { Clear(owner); repath = true; }
                }
            }

            // Finish an existing close-behind stage before rejecting a missing
            // patrol marker, matching the physical passage's original priority.
            if (!intendedTarget.HasValue)
            { Clear(owner); return new Decision(Outcome.Clear, repath); }
            var destination = intendedTarget.Value;
            var point = Door && !Passing ? Door.transform.position : agent.hasPath ? agent.steeringTarget : destination;
            var direction = point - actor.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) { direction = destination - actor.position; direction.y = 0; }
            if (direction.sqrMagnitude < .001f) return new Decision(Outcome.Clear, repath);
            float radius = agent.radius + .06f;
            var bottom = actor.position + Vector3.up * (radius + .12f);
            var top = actor.position + Vector3.up * Mathf.Max(radius + .12f, agent.height - radius);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction.normalized, hits,
                Mathf.Min(direction.magnitude, 6), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            { EnemyNavigation.Stop(agent); return new Decision(Outcome.Waiting, repath); }
            int first = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(actor) ||
                    hits[i].collider.GetComponentInParent<StalkerBrain>() ||
                    hits[i].collider.GetComponentInParent<LanternMaskEncounter>() ||
                    hits[i].collider.GetComponentInParent<WeepingAngelEncounter>()) continue;
                if (hits[i].distance < distance) { first = i; distance = hits[i].distance; }
            }
            // A wall cannot reveal a remote door; examine the first native obstruction.
            var hit = first >= 0 ? hits[first] : default;
            var next = first >= 0 ? hit.collider.GetComponentInParent<Interactable>() : null;
            var handle = hit.point;
            if (!next || next.kind != Interactable.Kind.Door || !next.movingLeaf)
            {
                next = StalkerDoorTraversal.NearbyDoorOnRoute(agent, destination, homeFloor, route,
                    Mathf.Max(2.2f, bodyReach + .65f));
                if (next) handle = next.transform.position;
            }
            if (!next || next.kind != Interactable.Kind.Door || !next.movingLeaf ||
                !EnemyNavigation.SameActorFloor(agent, next.transform.position, homeFloor))
            {
                if (Door && !Passing) Clear(owner);
                return new Decision(Outcome.Clear, repath);
            }
            if (Door != next)
            {
                Clear(owner);
                Door = next;
                EntrySide = Vector3.Dot(actor.position - next.transform.position, next.DoorNormal) >= 0 ? 1 : -1;
            }
            Hold(owner);
            if (HorizontalDistance(actor.position, handle) > Mathf.Max(Mathf.Max(1.25f, agent.radius + .85f), bodyReach + .4f))
            {
                Push = 0;
                if (!StalkerDoorTraversal.TryDoorApproach(agent, next, EntrySide, homeFloor, route))
                { EnemyNavigation.Stop(agent, true); return new Decision(Outcome.Waiting, repath); }
                agent.SetPath(route);
                agent.isStopped = false;
                return new Decision(Outcome.Waiting, repath);
            }
            EnemyNavigation.Stop(agent, true);
            BlockedWait = Mathf.Min(6.1f, BlockedWait + seconds);
            if (BlockedWait >= 6) return new Decision(Outcome.Blocked, repath, handle);
            return ContinuePush(owner, seconds, handle, repath);
        }

        // A still-visible chase can reject abandonment and keep using the handle.
        internal Decision ContinuePush(Behaviour owner, float seconds, Vector3 handle, bool repath = false)
        {
            Face(owner.transform, handle, seconds);
            if (Door.IsOpen) { Passing = true; return new Decision(Outcome.Waiting, repath); }
            if (!Door.DoorOperable) return new Decision(Outcome.Waiting, repath);
            Push = Mathf.Min(1.2f, Push + seconds);
            if (Push >= 1.2f && Door.OpenForPursuer())
            { Passing = true; Push = 0; repath = true; }
            return new Decision(Outcome.Waiting, repath);
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0; return Vector3.Distance(a, b); }

        static void Face(Transform actor, Vector3 point, float seconds)
        {
            var facing = point - actor.position;
            facing.y = 0;
            if (facing.sqrMagnitude > .001f) actor.rotation = Quaternion.RotateTowards(actor.rotation,
                Quaternion.LookRotation(facing), 180 * seconds);
        }
    }
}
