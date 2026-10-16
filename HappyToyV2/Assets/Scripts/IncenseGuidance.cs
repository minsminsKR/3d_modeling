using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // One stable, reachable memory; the renderer owns its continuous plume.
    public sealed class IncenseGuidance : MonoBehaviour
    {
        public const float Duration = 30;
        public float Remaining { get; private set; } = Duration;
        public int TargetCount => targets.Count;
        public IReadOnlyList<Vector3> TargetPoints => targets;
        readonly List<Vector3> targets = new List<Vector3>(1);
        readonly Vector3[] corners = new Vector3[256];
        Interactable[] items;
        Interactable memory;
        NavMeshPath route;
        PlayerMotor player;
        IncenseSmokeTrail trail;
        Vector3 direction;
        float refresh;

        void Awake() { route = new NavMeshPath(); }
        public void Prepare(PlayerMotor owner, float remaining = Duration)
        {
            player = owner; Remaining = remaining;
            items = player.Rituals.WorldRoot ? player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true) : new Interactable[0];
            trail = gameObject.AddComponent<IncenseSmokeTrail>();
            trail.Prepare(player.eyes);
            RefreshTarget();
        }
        static bool AvailableMemory(Interactable item) => item && item.InteractionAvailable &&
            (item.kind == Interactable.Kind.CorridorMemory || item.kind == Interactable.Kind.ChapterMemory);

        bool RouteTo(Vector3 target, bool haveFloor, NavMeshHit from, out float length, out Vector3 heading)
        {
            heading = target - player.transform.position; heading.y = 0;
            length = heading.magnitude;
            if (!haveFloor || !NavMesh.SamplePosition(target, out var to, 2, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, route) ||
                route.status != NavMeshPathStatus.PathComplete) return false;
            int count = route.GetCornersNonAlloc(corners);
            if (count == 0) return false;
            length = 0;
            bool foundHeading = false;
            for (int i = 1; i < count; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
                var toward = corners[i] - from.position; toward.y = 0;
                if (!foundHeading && toward.sqrMagnitude > .0225f)
                { heading = toward; foundHeading = true; }
            }
            return true;
        }
        void RefreshTarget()
        {
            targets.Clear();
            if (!player || !player.Rituals.WorldRoot) return;
            bool haveFloor = NavMesh.SamplePosition(player.transform.position, out var from, 1.5f, NavMesh.AllAreas);
            if (!AvailableMemory(memory))
            {
                memory = null;
                float best = float.PositiveInfinity;
                bool foundReachable = false;
                foreach (var item in items)
                {
                    if (!AvailableMemory(item)) continue;
                    bool reachable = RouteTo(item.transform.position, haveFloor, from, out float length, out _);
                    if (reachable && !foundReachable) { best = float.PositiveInfinity; foundReachable = true; }
                    if (!reachable && foundReachable) continue;
                    if (length < best) { best = length; memory = item; }
                }
            }
            if (memory) targets.Add(memory.transform.position);
            else
            {
                var session = GameSession.Current;
                if (session && session.CorridorMode && session.Corridor.AltarChamber && session.Corridor.AltarChamber.Offering)
                    targets.Add(session.Corridor.AltarChamber.Offering.transform.position);
                else foreach (var item in items)
                    if (item && item.kind == Interactable.Kind.Exit) { targets.Add(item.transform.position); break; }
            }
            if (targets.Count == 0) return;
            RouteTo(targets[0], haveFloor, from, out _, out direction);
            if (direction.sqrMagnitude < .01f) direction = player.eyes ? player.eyes.transform.forward : player.transform.forward;
            direction.Normalize();
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished) { Destroy(gameObject); return; }
            if (!player || !player.eyes || !session.InputAllowed) return;
            Remaining = Mathf.Max(0, Remaining - Time.deltaTime);
            if (Remaining <= 0) { Destroy(gameObject); return; }
            refresh -= Time.deltaTime;
            if (refresh <= 0) { RefreshTarget(); refresh = .35f; }
            Vector3 tip = new Vector3(.13f, -.18f, .48f);
            if (player.Hands && player.Hands.TryGetIncenseTip(out var actualTip)) tip = actualTip;
            trail.Advance(player.eyes.transform.TransformPoint(tip), direction, Time.deltaTime, Remaining);
        }
    }
}
