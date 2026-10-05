using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class StalkerBrain : MonoBehaviour
    {
        public Transform[] patrol;
        public float patrolSpeed = 1.45f, chaseSpeed = 3.5f;
        public PlayerMotor player;
        public CorridorThreatRole corridorRole;
        public CorridorThreatRules Senses => CorridorThreatRules.For(corridorRole);
        public enum State { Patrol, Investigate, Chase, Search }
        public State state;
        NavMeshAgent agent;
        NavMeshPath path;
        StalkerFootsteps footsteps;
        Vector3 lastKnown, hidingApproach, attackHidingApproach, attackFacing;
        Vector3 searchOrigin, searchTarget;
        Quaternion searchFacing;
        float searchDwell, searchTransit, searchDoorWait;
        int searchCandidate;
        bool searchArrived, searchStarted;
        float memory, repath, floorY;
        int waypoint;
        Interactable blockingDoor;
        float doorPush;
        bool patrolDwelling;
        float patrolDwellUntil;
        bool witnessedHiding, attackingHiding, recognitionCueIssued;
        PlayerMotor noisePlayer;
        readonly StealthRules.Awareness awareness = new StealthRules.Awareness();
        readonly EnemyAttackClock attack = new EnemyAttackClock();
        public bool SawHiding => witnessedHiding;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        public bool AttackActive => attack.Active;
        public int AttacksStarted { get; private set; }
        public int NoisesAccepted { get; private set; }
        public int FootstepNoisesAccepted { get; private set; }
        public float Awareness => awareness.Value;
        public float HomeFloorY => floorY;
        public Vector3 LastKnownPosition => lastKnown;
        public Vector3 SearchOrigin => searchOrigin;
        public int SearchPointsVisited { get; private set; }

        public bool HearNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.EncountersResolved ||
                !StealthRules.Finite(duration) || duration <= 0 ||
                state == State.Chase || AttackActive || awareness.Acquired ||
                !StealthRules.Finite(point.x) || !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                Vector3.Distance(point, transform.position) > 28 ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, 38)) return false;
            var corners = path.corners;
            lastKnown = corners.Length > 0 ? corners[corners.Length - 1] : point;
            memory = duration; state = State.Investigate; repath = 0; NoisesAccepted++;
            return true;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath();
            footsteps = GetComponent<StalkerFootsteps>();
            if (!footsteps) footsteps = gameObject.AddComponent<StalkerFootsteps>();
            floorY = transform.position.y;
        }
        void OnEnable()
        {
            patrolDwelling = false;
            floorY = transform.position.y; repath = 0; recognitionCueIssued = false; awareness.Reset(); BindFootsteps(player);
        }
        void OnDisable()
        {
            blockingDoor = null; doorPush = 0;
            BindFootsteps(null); awareness.Reset(); attack.Reset(); witnessedHiding = false;
            EnemyNavigation.Stop(agent, true);
        }
        void BindFootsteps(PlayerMotor next)
        {
            if (ReferenceEquals(noisePlayer, next)) return;
            // Use reference identity when unsubscribing: Unity's destroyed-object null
            // comparison must not leave a managed event listener behind.
            if (!ReferenceEquals(noisePlayer, null)) noisePlayer.FootstepNoiseEmitted -= HearFootstep;
            noisePlayer = next;
            if (noisePlayer) noisePlayer.FootstepNoiseEmitted += HearFootstep;
        }
        void HearFootstep(Vector3 point, float radius)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.EncountersResolved ||
                !player || noisePlayer != player || player.Hidden || state == State.Chase || attack.Active ||
                !StealthRules.Finite(radius) || radius <= 0 || !StealthRules.Finite(point.x) ||
                !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z)) return;
            // The listener hears a larger real footstep radius, with the same
            // physical route/floor restrictions as the original actor.
            radius *= Senses.HearingScale;
            if (!StealthRules.Finite(radius) || Vector3.Distance(point, transform.position) > radius ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, radius)) return;
            var corners = path.corners;
            lastKnown = corners.Length > 0 ? corners[corners.Length - 1] : point;
            state = State.Investigate; memory = 3; repath = 0; FootstepNoisesAccepted++;
        }

        public bool CanSeePlayer()
        {
            if (!player || player.Hidden || !EnemyNavigation.SameFloor(player.transform.position, floorY)) return false;
            var eye = transform.position + Vector3.up * 1.7f;
            // A visible torso counts too; a camera point can sit outside the capsule's curved head.
            for (int i = 0; i < 2; i++)
            {
                var target = i == 0 && player.eyes ? player.eyes.transform.position :
                    player.transform.position + Vector3.up * player.SightTargetHeight;
                var delta = target - eye;
                if (delta.magnitude < Senses.SightRange && (state == State.Chase || Vector3.Angle(transform.forward, delta) < Senses.SightCone) &&
                    EnemyNavigation.ClearSight(eye, target, player)) return true;
            }
            return false;
        }

        public void ObserveHiding(Vector3 entrance)
        {
            // Called before the player's collider disappears, not inferred from old chase memory.
            witnessedHiding = (state == State.Chase || awareness.Acquired) && CanSeePlayer() &&
                EnemyNavigation.SameFloor(entrance, floorY);
            if (!witnessedHiding) return;
            hidingApproach = entrance; lastKnown = entrance; memory = 8; state = State.Chase;
        }

        bool AtWitnessedHidingPlace()
        {
            var delta = transform.position - hidingApproach; delta.y = 0;
            return player.Hidden && witnessedHiding && state == State.Chase && delta.magnitude < .85f &&
                EnemyNavigation.SameFloor(hidingApproach, floorY) &&
                EnemyNavigation.ClearSight(transform.position + Vector3.up * 1.1f, hidingApproach + Vector3.up * 1.1f);
        }

        void BeginAttack(bool hiding, GameSession session)
        {
            if (!attack.Begin(.75f, .9f)) return;
            attackingHiding = hiding; attackHidingApproach = hidingApproach;
            attackFacing = (hiding ? hidingApproach : player.transform.position) - transform.position;
            attackFacing.y = 0;
            if (attackFacing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(attackFacing);
            EnemyNavigation.Stop(agent); AttacksStarted++;
            if (footsteps) footsteps.PlayAttackCue(hiding);
            if (hiding) player.ReportHidingDoorAttack(1.5f);
            session.WarnThreat(hiding ? "은신처를 들켰습니다 · 문 앞에서 공격을 준비합니다. 지금 빠져나오세요." :
                "가까운 적이 공격을 준비합니다 · 즉시 거리를 벌리세요.", 1.5f);
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0; return Vector3.Distance(a, b); }

        bool TryPassDoor()
        {
            // Only inspect the first physical obstruction toward observed evidence.
            // A wall must never reveal or open a door behind it.
            if (state != State.Chase && state != State.Investigate && state != State.Patrol && state != State.Search)
            { blockingDoor = null; doorPush = 0; return false; }
            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPoint = state == State.Search ? searchTarget : lastKnown;
            if (agent.hasPath) targetPoint = agent.steeringTarget;
            else if (state == State.Patrol)
            {
                if (patrol == null || patrol.Length == 0 || !patrol[waypoint % patrol.Length]) return false;
                targetPoint = patrol[waypoint % patrol.Length].position;
            }
            Vector3 target = targetPoint + Vector3.up;
            if (!Physics.Linecast(origin, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { blockingDoor = null; doorPush = 0; return false; }
            var door = hit.collider.GetComponentInParent<Interactable>();
            if (!door || door.kind != Interactable.Kind.Door || !door.movingLeaf ||
                !EnemyNavigation.SameFloor(door.transform.position, floorY))
            { blockingDoor = null; doorPush = 0; return false; }
            if (blockingDoor != door) { blockingDoor = door; doorPush = 0; }
            // The collider still matters while a requested opening is sliding.
            // Native agents do not collide with it, so keep them stopped until clear.
            if (door.IsOpen) { EnemyNavigation.Stop(agent); return true; }
            if (HorizontalDistance(transform.position, hit.point) > 1.25f)
            {
                doorPush = 0;
                Vector3 approach = hit.point + (origin - hit.point).normalized * .85f;
                approach.y = transform.position.y;
                if (!EnemyNavigation.TryRoute(agent, approach, floorY, path, 12)) return false;
                agent.SetPath(path); agent.isStopped = false;
                return true;
            }
            EnemyNavigation.Stop(agent, true);
            Vector3 facing = hit.point - origin; facing.y = 0;
            if (facing.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 180 * Time.deltaTime);
            doorPush += Time.deltaTime;
            // Closing a door buys time, rather than permanently cancelling pursuit.
            if (doorPush >= 1.2f)
            { door.OpenForPursuer(); blockingDoor = null; doorPush = 0; repath = 0; }
            return true;
        }

        void BeginSearch()
        {
            state = State.Search; memory = Senses.SearchSeconds; witnessedHiding = false; awareness.Reset();
            // This anchor is observed evidence, never the current unseen player.
            searchOrigin = searchTarget = lastKnown;
            searchCandidate = 0; searchArrived = searchStarted = false; searchDwell = searchDoorWait = 0; searchTransit = 4;
            SearchPointsVisited = 0; repath = 0;
            searchFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }

        void SearchArea()
        {
            if (!searchArrived && HorizontalDistance(transform.position, searchTarget) < .65f)
            {
                searchArrived = searchStarted = true; searchDwell = 0; SearchPointsVisited++;
                searchFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
            if (searchArrived)
            {
                EnemyNavigation.Stop(agent, true);
                searchDwell += Time.deltaTime;
                // A visible, bounded look-around gives cover a purpose: a player
                // behind the searching enemy can move, but can also be rediscovered.
                float sweep = Mathf.Lerp(-80, 100, Mathf.Clamp01(searchDwell / 1.4f));
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    searchFacing * Quaternion.Euler(0, sweep, 0), 160 * Time.deltaTime);
                if (searchDwell < 1.4f) return;
                bool found = false;
                while (searchCandidate < 8)
                {
                    int index = searchCandidate++;
                    float angle = (index % 2 == 0 ? 1 : -1) * (55 + index / 2 * 45);
                    Vector3 candidate = searchOrigin + searchFacing * Quaternion.Euler(0, angle, 0) *
                        Vector3.forward * (index < 4 ? 2.2f : 3.2f);
                    if (!NavMesh.SamplePosition(candidate, out var hit, .6f, agent.areaMask) ||
                        !EnemyNavigation.SameFloor(hit.position, floorY) ||
                        HorizontalDistance(hit.position, searchOrigin) > 3.8f ||
                        HorizontalDistance(hit.position, transform.position) < 1 ||
                        !EnemyNavigation.TryRoute(agent, hit.position, floorY, path, 8)) continue;
                    searchTarget = hit.position; searchArrived = false; repath = .25f;
                    agent.SetPath(path); agent.isStopped = false; found = true; break;
                }
                // No valid local branch is a normal dead end, not permission to
                // route through walls or another floor. Keep looking until timeout.
                if (!found) searchDwell = 0;
                return;
            }
            repath -= Time.deltaTime;
            if (repath > 0) return;
            repath = .25f;
            if (EnemyNavigation.TryRoute(agent, searchTarget, floorY, path, 8))
            { agent.SetPath(path); agent.isStopped = false; }
            else { EnemyNavigation.Stop(agent, true); searchArrived = searchStarted = true; searchDwell = 0; }
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!player && session) player = session.player;
            BindFootsteps(player);
            if (!session || !player) { EnemyNavigation.Stop(agent); return; }
            if (session.EncountersResolved)
            { awareness.Reset(); attack.Reset(); witnessedHiding = false; EnemyNavigation.Stop(agent, true); return; }
            if (!EnemyNavigation.Ready(agent)) { attack.Reset(); repath = 0; return; }
            if (!session.InputAllowed) { EnemyNavigation.Stop(agent); repath = 0; return; }

            if (state != State.Chase) recognitionCueIssued = false;
            bool visible = CanSeePlayer();
            var offset = player.transform.position - transform.position;
            if (attack.Active)
            {
                EnemyNavigation.Stop(agent);
                if (attack.Tick(Time.deltaTime))
                {
                    var horizontal = offset; horizontal.y = 0;
                    // Commit the strike to its warning direction; circling behind the reach is a dodge.
                    bool inArc = horizontal.sqrMagnitude < .01f || Vector3.Angle(attackFacing, horizontal) <= 75;
                    bool hit = attackingHiding ? AtWitnessedHidingPlace() &&
                        Vector3.Distance(attackHidingApproach, hidingApproach) < .1f : visible && offset.magnitude < 1.55f && inArc;
                    if (hit)
                    {
                        string hint = attackingHiding ? "숨는 모습을 본 적은 문 앞까지 따라옵니다. 시야를 끊은 뒤 은신처에 들어가세요." :
                            "공격음과 예고가 시작되면 거리를 벌리세요. 공격이 빗나간 직후 지나갈 수 있습니다.";
                        string counterplay = CorridorThreatRules.Counterplay(corridorRole);
                        if (!string.IsNullOrEmpty(counterplay)) hint = (attackingHiding ?
                            "숨는 모습을 보이면 은신처까지 쫓아옵니다." : "공격 예고가 들리면 즉시 거리를 벌리세요.") + "\n\n" + counterplay;
                        session.TryDefeat(name, hint);
                    }
                }
                if (!attack.Active) repath = 0;
                return;
            }
            bool acquiring = false;
            if (state != State.Chase)
            {
                bool lightOn = player.flashlight && player.flashlight.isActiveAndEnabled;
                acquiring = visible && offset.magnitude < StealthRules.SightRange(player.Crouching, lightOn, false, Senses.SightRange);
                awareness.Tick(acquiring,
                    StealthRules.AcquisitionSeconds(player.Crouching, lightOn, player.Running, offset.magnitude), Time.deltaTime);
            }
            if (visible && (state == State.Chase || acquiring && awareness.Acquired))
            {
                // Scripted encounters can enable an actor already in Chase.
                // Its first actual sight, not that scripted flag alone, earns the cue.
                if (!recognitionCueIssued) { DetectionFeedback.Signal(session, transform); recognitionCueIssued = true; }
                state = State.Chase; memory = Senses.ChaseMemory; lastKnown = player.transform.position; witnessedHiding = false;
            }
            else if (state == State.Chase)
            {
                memory -= Time.deltaTime;
                // Once the last confirmed location is reached, actually inspect the
                // area instead of standing at one stale destination for nine seconds.
                if (memory <= 0 || !witnessedHiding && HorizontalDistance(transform.position, lastKnown) < .65f)
                    BeginSearch();
            }
            else
            {
                if (state == State.Search)
                {
                    // Give the arrival scan its own budget, with bounded travel to
                    // stale evidence if a door has made that point unreachable.
                    if (!searchStarted)
                    {
                        searchTransit -= Time.deltaTime;
                        if (searchTransit <= 0)
                        {
                            // Travel timed out: inspect the current area without
                            // falsely counting the unreachable evidence as visited.
                            searchArrived = searchStarted = true; searchDwell = 0;
                            searchFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                        }
                    }
                    else memory -= Time.deltaTime;
                }
                else if (state == State.Investigate) memory -= Time.deltaTime;
                if ((state == State.Search || state == State.Investigate) && memory <= 0)
                { state = State.Patrol; repath = 0; }
            }
            if (AtWitnessedHidingPlace()) { BeginAttack(true, session); return; }
            if (visible && state == State.Chase && offset.magnitude < 1.5f) { BeginAttack(false, session); return; }

            if (acquiring && state != State.Chase)
            {
                // A spatial tell only while actually seeing the player. No global warning
                // reveals an enemy through walls, and occlusion stops target tracking.
                EnemyNavigation.Stop(agent); repath = 0;
                var facing = offset; facing.y = 0;
                if (facing.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 180 * Time.deltaTime);
                return;
            }

            agent.speed = state == State.Chase ? chaseSpeed : patrolSpeed;
            if (state != State.Patrol) patrolDwelling = false;
            if (TryPassDoor())
            {
                // A known operable leaf needs time to push/slide. Preserve the
                // travel budget during that physical wait, with a finite allowance
                // so a broken/disabled leaf cannot hold a search indefinitely.
                if (state == State.Search && !searchStarted && agent.isStopped && searchDoorWait < 4)
                {
                    float wait = Mathf.Min(Time.deltaTime, 4 - searchDoorWait);
                    searchTransit += wait; searchDoorWait += wait;
                }
                return;
            }
            if (state == State.Search) { SearchArea(); return; }
            if (state == State.Patrol && Senses.PatrolDwell > 0 && patrol != null && patrol.Length > 0)
            {
                waypoint %= patrol.Length;
                if (patrol[waypoint] && HorizontalDistance(transform.position, patrol[waypoint].position) < .6f)
                {
                    if (!patrolDwelling) { patrolDwelling = true; patrolDwellUntil = Time.time + Senses.PatrolDwell; }
                    if (Time.time < patrolDwellUntil)
                    {
                        EnemyNavigation.Stop(agent, true); repath = 0;
                        transform.Rotate(0, 65 * Time.deltaTime, 0);
                        return;
                    }
                    waypoint = (waypoint + 1) % patrol.Length; patrolDwelling = false;
                }
            }
            repath -= Time.deltaTime;
            if (repath > 0) return;
            repath = .25f;
            if (state != State.Patrol)
            {
                if (EnemyNavigation.TryRoute(agent, lastKnown, floorY, path))
                { agent.SetPath(path); agent.isStopped = false; }
                else EnemyNavigation.Stop(agent, true);
                return;
            }
            if (patrol == null || patrol.Length == 0) { EnemyNavigation.Stop(agent, true); return; }
            waypoint %= patrol.Length;
            if (patrol[waypoint] && Vector3.Distance(transform.position, patrol[waypoint].position) < .6f)
                waypoint = (waypoint + 1) % patrol.Length;
            // Ignore invalid/other-floor markers instead of following a complete path through stairs.
            for (int i = 0; i < patrol.Length; i++)
            {
                waypoint %= patrol.Length;
                if (patrol[waypoint] && EnemyNavigation.TryRoute(agent, patrol[waypoint].position, floorY, path))
                { agent.SetPath(path); agent.isStopped = false; return; }
                waypoint = (waypoint + 1) % patrol.Length;
            }
            EnemyNavigation.Stop(agent, true);
        }
    }
}
