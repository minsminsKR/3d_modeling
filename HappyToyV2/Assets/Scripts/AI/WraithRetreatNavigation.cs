using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Owns the corridor runner's post-pursuit destination and respite. It never
    // reads the live player, changes sensing, or moves/warps the actor itself.
    internal sealed class WraithRetreatNavigation
    {
        public const float MinimumSeparation = 18;
        readonly WraithRetreatClock clock = new WraithRetreatClock();
        NavMeshPath route;
        public bool Active => clock.Active;
        public float Remaining => clock.Remaining;
        public Vector3 Destination { get; private set; }

        public bool Begin(NavMeshAgent agent, Vector3 lastSeen, Transform[] patrol, float floor)
        {
            Reset();
            if (!EnemyNavigation.Ready(agent) || patrol == null) return false;
            if (route == null) route = new NavMeshPath();
            float best = float.NegativeInfinity;
            foreach (var marker in patrol)
            {
                if (!marker || Vector3.Distance(marker.position, lastSeen) < MinimumSeparation ||
                    Vector3.Distance(marker.position, agent.transform.position) < 6 ||
                    !EnemyNavigation.TryRoute(agent, marker.position, floor, route)) continue;
                var corners = route.corners;
                if (corners.Length == 0) continue;
                var destination = corners[corners.Length - 1];
                if (Vector3.Distance(destination, lastSeen) < MinimumSeparation) continue;
                float clearance = float.PositiveInfinity, length = 0;
                var previous = agent.transform.position;
                foreach (var corner in corners)
                {
                    var segment = corner - previous;
                    float t = segment.sqrMagnitude > .0001f ?
                        Mathf.Clamp01(Vector3.Dot(lastSeen - previous, segment) / segment.sqrMagnitude) : 0;
                    clearance = Mathf.Min(clearance, Vector3.Distance(lastSeen, previous + segment * t));
                    length += segment.magnitude; previous = corner;
                }
                // Prefer a route away from the observed area, then a distant
                // endpoint. A looping route back past that area is less desirable.
                float score = clearance * 4 + Vector3.Distance(destination, lastSeen) - length * .15f;
                if (score <= best) continue;
                best = score; Destination = destination;
            }
            if (float.IsNegativeInfinity(best)) return false;
            clock.Begin(); return true;
        }
        public void Tick(float seconds)
        {
            clock.Tick(seconds);
            if (!Active) Destination = Vector3.zero;
        }
        public void Restore(float remaining, Vector3 destination)
        { clock.Restore(remaining); Destination = Active ? destination : Vector3.zero; }
        public void Reset() { clock.Reset(); Destination = Vector3.zero; }
    }
}
