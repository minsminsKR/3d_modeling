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

        public static void Stop(NavMeshAgent agent, bool clearPath = false)
        {
            if (!Ready(agent)) return;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (clearPath) agent.ResetPath();
        }
    }
}
