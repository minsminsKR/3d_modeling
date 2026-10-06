using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Floors are anchored when an actor is released, never to its moving stair position.
    public static class EnemyNavigation
    {
        public const float FloorTolerance = 1.6f;
        public static bool SameFloor(Vector3 point, float floorY) => Mathf.Abs(point.y - floorY) <= FloorTolerance;
        public static bool Ready(NavMeshAgent agent) => agent && agent.enabled && agent.isOnNavMesh;

        public static bool TryRoute(NavMeshAgent agent, Vector3 point, float floorY, NavMeshPath path,
            float maximumLength = float.PositiveInfinity)
        {
            if (!Ready(agent) || path == null || !SameFloor(agent.transform.position, floorY) || !SameFloor(point, floorY) ||
                !NavMesh.SamplePosition(point, out var hit, 1.5f, agent.areaMask) || !SameFloor(hit.position, floorY) ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            float length = 0;
            var previous = agent.transform.position;
            foreach (var corner in corners)
            {
                if (!SameFloor(corner, floorY)) return false;
                length += Vector3.Distance(previous, corner);
                if (length > maximumLength) return false;
                previous = corner;
            }
            return true;
        }

        public static bool ClearSight(Vector3 origin, Vector3 target, PlayerMotor player = null)
        {
            // The player's Ignore Raycast layer is intentional. Disabled player colliders in
            // controlled probes must not make an otherwise unobstructed line invisible.
            return !Physics.Linecast(origin, target, out var hit, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) || (player && hit.collider.GetComponentInParent<PlayerMotor>() == player);
        }

        static readonly RaycastHit[] soundHits = new RaycastHit[32];

        // Range already models distance decay. Physical cover reduces the radius
        // additionally, without granting a listener visual knowledge through cover.
        public static float SoundTransmission(Vector3 origin, Vector3 target, Transform listener, Transform emitter)
        {
            var delta = target - origin;
            if (delta.sqrMagnitude < .0001f) return 1;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, soundHits, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == soundHits.Length) return 0; // Omitted cover cannot certify hearing.
            float transmission = 1;
            for (int i = 0; i < count; i++)
            {
                var collider = soundHits[i].collider;
                if (!collider || listener && collider.transform.IsChildOf(listener) ||
                    emitter && collider.transform.IsChildOf(emitter)) continue;
                var door = collider.GetComponentInParent<Interactable>();
                transmission = Mathf.Min(transmission,
                    door && door.kind == Interactable.Kind.Door ? .55f : .38f);
            }
            return transmission;
        }

        public static void Stop(NavMeshAgent agent, bool clearPath = false)
        {
            if (!Ready(agent)) return;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (clearPath) agent.ResetPath();
        }
    }
}
