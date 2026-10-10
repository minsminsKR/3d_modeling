using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class LanternMaskEncounter
    {
        public const float RunnerPatrolSpeed = 6.6f, RunnerChaseSpeed = 7.2f, NearWhistleDistance = 7f;
        public bool CorridorRunner { get; private set; }
        public int DoorsShattered { get; private set; }
        Interactable[] runnerDoors;
        NavMeshPath runnerDoorRoute, runnerApproach;
        public void ConfigureCorridorRunner()
        {
            CorridorRunner = true;
            if (!agent) agent = GetComponent<NavMeshAgent>();
            agent.acceleration = Mathf.Max(agent.acceleration, 24);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 540);
            agent.stoppingDistance = .15f;
            runnerDoorRoute = new NavMeshPath(); runnerApproach = new NavMeshPath();
            runnerDoors = FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!GetComponent<CorridorMaskAudio>()) gameObject.AddComponent<CorridorMaskAudio>();
            if (gameObject.activeInHierarchy) PrepareCorridorRunner();
        }
        void PrepareCorridorRunner()
        {
            // The corridor release is the already-grown spirit. School authoring
            // retains its lantern reveal, curse and five-second transformation.
            IntroStarted = IntroCompleted = true; IntroElapsed = 2.2f; riseCueIssued = true;
            Transformed = true; transformTime = 5;
            var oldFeet = GetComponent<StalkerFootsteps>(); if (oldFeet) oldFeet.enabled = false;
        }
        StalkerDoorTraversal.Result TickRunnerDoor(Vector3 destination)
        {
            if (runnerDoors == null) runnerDoors = FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (runnerDoorRoute == null) runnerDoorRoute = new NavMeshPath();
            if (runnerApproach == null) runnerApproach = new NavMeshPath();
            Interactable candidate = null; float nearest = 2.4f; bool routed = false;
            foreach (var door in runnerDoors)
            {
                if (!door || !door.DoorOperable || door.gameObject.scene != gameObject.scene ||
                    !EnemyNavigation.SameActorFloor(agent, door.transform.position, floorY)) continue;
                var delta = door.transform.position - transform.position; delta.y = 0;
                if (delta.magnitude >= nearest) continue;
                if (!routed)
                {
                    if (!EnemyNavigation.TryRoute(agent, destination, floorY, runnerDoorRoute)) return StalkerDoorTraversal.Result.Clear;
                    routed = true;
                }
                if (!StalkerDoorTraversal.RouteCrossesOpening(agent, destination, door, runnerDoorRoute)) continue;
                var origin = transform.position + Vector3.up;
                var point = door.transform.position + Vector3.up;
                if (Physics.Linecast(origin, point, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Interactable>() != door) continue;
                nearest = delta.magnitude; candidate = door;
            }
            if (!candidate) return StalkerDoorTraversal.Result.Clear;
            if (candidate.BreakForMask(this))
            {
                DoorsShattered++; if (doorTraversal) doorTraversal.Cancel(); repath = 0;
                return StalkerDoorTraversal.Result.Clear;
            }
            // Keep running at the real opening while its intact obstacle still
            // exists. No transform warp, remote door operation or speed-zero wait.
            float side = Vector3.Dot(transform.position - candidate.transform.position, candidate.DoorNormal) >= 0 ? 1 : -1;
            if (!StalkerDoorTraversal.TryDoorApproach(agent, candidate, side, floorY, runnerApproach))
                return StalkerDoorTraversal.Result.Blocked;
            agent.SetPath(runnerApproach); agent.isStopped = false;
            return StalkerDoorTraversal.Result.Waiting;
        }
    }
}
