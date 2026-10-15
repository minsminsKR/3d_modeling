using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Physical evidence only. The coordinator decides what heard/seen evidence
    // means for its state, and keeps session, cheat and Baby rules at that boundary.
    internal sealed class StalkerPerception
    {
        internal bool CanSee(Transform actor, NavMeshAgent navigation, PlayerMotor target,
            float homeFloor, CorridorThreatRules senses, bool chasing)
        {
            if (!target || target.Hidden ||
                !EnemyNavigation.WithinFloorPolicy(navigation, target.transform.position, homeFloor)) return false;
            var eye = actor.position + Vector3.up * 1.7f;
            // A visible torso counts too; the camera can sit outside the capsule's head.
            for (int i = 0; i < 2; i++)
            {
                var point = i == 0 && target.eyes ? target.eyes.transform.position :
                    target.transform.position + Vector3.up * target.SightTargetHeight;
                var delta = point - eye;
                if (delta.magnitude < senses.SightRange &&
                    (chasing || Vector3.Angle(actor.forward, delta) < senses.SightCone) &&
                    EnemyNavigation.ClearSight(eye, point, target)) return true;
            }
            return false;
        }

        internal bool TryHearFootstep(Transform actor, NavMeshAgent navigation, PlayerMotor source,
            Vector3 point, float radius, float hearingScale, float homeFloor, NavMeshPath route,
            out Vector3 evidence)
        {
            radius *= hearingScale;
            radius *= EnemyNavigation.SoundTransmission(point + Vector3.up * .5f,
                actor.position + Vector3.up, actor, source.transform);
            return TryResolveNoiseRoute(actor, navigation, point, radius, radius, homeFloor, route, out evidence);
        }

        internal bool TryResolveSnapperRoute(Transform actor, NavMeshAgent navigation, Vector3 point,
            float audibleRadius, float homeFloor, NavMeshPath route, out Vector3 evidence)
        {
            evidence = point;
            if (!CorridorCheckpoint.Vector(point) || Vector3.Distance(point, actor.position) > audibleRadius) return false;
            Vector3 ground = point;
            if (!EnemyNavigation.AllowsCrossFloor(navigation)) ground.y = homeFloor;
            else
            {
                // A report on a wall/ceiling is investigated from the supporting
                // walkable floor below it, not an unreachable point in the air.
                var toward = actor.position - point; toward.y = 0;
                if (Physics.Raycast(point + toward.normalized * .08f + Vector3.up * .12f, Vector3.down,
                    out var hit, 6, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.normal.y > .6f)
                    ground = hit.point;
            }
            return TryResolveNoiseRoute(actor, navigation, ground, audibleRadius, 38, homeFloor, route, out evidence);
        }

        internal bool TryResolveNoiseRoute(Transform actor, NavMeshAgent navigation, Vector3 point,
            float audibleRadius, float routeLimit, float homeFloor, NavMeshPath route, out Vector3 evidence)
        {
            evidence = point;
            if (!StealthRules.Finite(point.x) || !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                !StealthRules.Finite(audibleRadius) || Vector3.Distance(point, actor.position) > audibleRadius ||
                !EnemyNavigation.TryRoute(navigation, point, homeFloor, route, routeLimit)) return false;
            var corners = route.corners;
            evidence = corners.Length > 0 ? corners[corners.Length - 1] : point;
            return true;
        }
    }
}
