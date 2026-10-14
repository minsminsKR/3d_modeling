using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Shared physical approach/scan/retreat or witnessed rush. The owning monster
    // supplies entry evidence and resolves the returned warning/impact events.
    internal sealed class EnemyCabinetEncounter
    {
        readonly CabinetEncounterClock clock = new CabinetEncounterClock();
        NavMeshPath route;
        Vector3 entrance, inspect, retreat;
        Quaternion facing;
        StalkerDoorTraversal doors;
        V1MonsterMotion motion;
        float repath;
        public bool Active => clock.Active;
        public bool Aggressive => clock.Aggressive;
        public bool Striking => clock.State == CabinetEncounterClock.Phase.Strike;
        public float Windup => clock.Windup;
        public float Recovery => clock.Recovery;
        public string Stage => clock.State.ToString();
        public void Begin(Behaviour owner, NavMeshAgent agent, Vector3 observedEntrance, int entry, CabinetHidingOutcome outcome,bool forcedCapture=false)
        {
            Reset(); if (route == null) route = new NavMeshPath(); entrance = observedEntrance;
            var away = owner.transform.position - entrance; away.y = 0;
            if (away.sqrMagnitude < .01f) away = -owner.transform.forward;
            away.Normalize();
            inspect = entrance + away * 1.65f; retreat = entrance + away * 4;
            if (Vector3.Distance(owner.transform.position, entrance) < 1.65f) inspect = owner.transform.position;
            if (NavMesh.SamplePosition(inspect, out var hit, .7f, agent.areaMask)) inspect = hit.position;
            else inspect = owner.transform.position;
            if (NavMesh.SamplePosition(retreat, out hit, 1, agent.areaMask)) retreat = hit.position;
            else retreat = owner.transform.position;
            facing = Quaternion.LookRotation(-away);
            doors = owner.GetComponent<StalkerDoorTraversal>();
            if (!doors) doors = owner.gameObject.AddComponent<StalkerDoorTraversal>();
            doors.Bind(owner); clock.Begin(entry, outcome,forcedCapture);
            motion = owner.GetComponent<V1MonsterMotion>();
        }
        public void Reset()
        { clock.Reset(); repath = 0; if (doors) doors.Cancel(); }
        public CabinetEncounterClock.Step Tick(NavMeshAgent agent, float homeFloor, PlayerMotor player, float calmSpeed, float rushSpeed)
        {
            bool sameEntry = player && player.Hidden && player.HidingEntryId == clock.Entry;
            var previous = clock.State;
            var toward = entrance - agent.transform.position; toward.y = 0;
            if ((previous == CabinetEncounterClock.Phase.Rush || Striking) && toward.sqrMagnitude > .001f)
                agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation, Quaternion.LookRotation(toward), 540 * Time.deltaTime);
            var target = previous == CabinetEncounterClock.Phase.Approach ? inspect :
                previous == CabinetEncounterClock.Phase.Retreat ? retreat : entrance;
            float flatDistance = Flat(agent.transform.position - entrance);
            bool clear = EnemyNavigation.SameActorFloor(agent, entrance, homeFloor) &&
                EnemyNavigation.ClearSight(agent.transform.position + Vector3.up * 1.1f, entrance + Vector3.up * 1.1f);
            // Large authored heads/torso must not penetrate the cabinet just to
            // put the navigation pivot at a small humanoid's reach threshold.
            float surfaceReach = Striking ? .65f : .55f;
            bool inReach = flatDistance < .85f || motion && motion.BodyHorizontalReach > 0 && flatDistance < 2.2f &&
                motion.BodySurfaceDistance(entrance + Vector3.up * 1.1f) < surfaceReach;
            bool impact = sameEntry && !player.HidingProtected && clear && inReach;
            // A long cat can already have its head at the cabinet while body
            // clearance keeps its pivot behind the nominal inspection marker.
            // Inspection may start there; attack reach remains independently strict.
            bool inspectingReach=previous==CabinetEncounterClock.Phase.Approach && clear && motion &&
                motion.BodyHorizontalReach>0 && flatDistance<4 && motion.BodySurfaceDistance(entrance+Vector3.up*1.1f)<.9f;
            bool arrived = previous == CabinetEncounterClock.Phase.Rush ? impact :
                inspectingReach || Vector3.Distance(agent.transform.position, target) < .5f;
            var step = clock.Tick(Time.deltaTime, sameEntry, player && player.HidingProtected, arrived, impact);
            if (!Active)
            { if (doors) doors.Cancel(); EnemyNavigation.Stop(agent, true); return step; }
            if (clock.State != previous) repath = 0;
            if (clock.State == CabinetEncounterClock.Phase.Inspect || clock.State == CabinetEncounterClock.Phase.Recovery || Striking)
            {
                EnemyNavigation.Stop(agent, true); doors.Suspend();
                if (!Striking)
                {
                    float sweep = Mathf.Sin(clock.Elapsed * 3.3f) * 62;
                    agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation, facing * Quaternion.Euler(0, sweep, 0), 180 * Time.deltaTime);
                }
                return step;
            }
            target = clock.State == CabinetEncounterClock.Phase.Approach ? inspect :
                clock.State == CabinetEncounterClock.Phase.Retreat ? retreat : entrance;
            agent.speed = Aggressive ? Mathf.Max(5.5f, rushSpeed * 1.4f) : Mathf.Max(.7f, calmSpeed);
            if (doors.Tick(target, homeFloor) != StalkerDoorTraversal.Result.Clear) { repath = 0; return step; }
            repath -= Time.deltaTime;
            if (repath <= 0)
            {
                repath = .2f;
                if (EnemyNavigation.TryRoute(agent, target, homeFloor, route, 50)) EnemyNavigation.FollowPath(agent, route);
                else EnemyNavigation.Stop(agent, true);
            }
            return step;
        }
        static float Flat(Vector3 value) { value.y = 0; return value.magnitude; }
    }
}
