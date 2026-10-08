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
        float doorPush, doorEntrySide, doorCloseWait, doorBlockedWait;
        bool passingDoor;
        readonly RaycastHit[] doorHits = new RaycastHit[32];
        bool patrolDwelling;
        float patrolDwellUntil;
        bool witnessedHiding, attackingHiding, recognitionCueIssued;
        PlayerMotor noisePlayer;
        readonly StealthRules.Awareness awareness = new StealthRules.Awareness();
        readonly EnemyAttackClock attack = new EnemyAttackClock();
        readonly NoiseInvestigationClock investigation = new NoiseInvestigationClock();
        Quaternion investigationFacing;
        public bool InvestigationArrived => investigation.Arrived;
        public bool InvestigationActive => investigation.Active;
        public Vector3 InvestigationPoint => investigation.Point;
        public float InvestigationTravelRemaining => investigation.TravelRemaining;
        public float InvestigationDwellRemaining => investigation.DwellRemaining;
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
                !StealthRules.Finite(duration) || duration <= 0 || duration > 3600 ||
                state == State.Chase || AttackActive || awareness.Acquired ||
                !StealthRules.Finite(point.x) || !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                Vector3.Distance(point, transform.position) > 28 ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, 38)) return false;
            var corners = path.corners;
            lastKnown = corners.Length > 0 ? corners[corners.Length - 1] : point;
            BeginNoiseInvestigation(duration); NoisesAccepted++;
            return true;
        }

        void BeginNoiseInvestigation(float dwell)
        {
            NoiseInvestigationRoute.Begin(investigation,transform.position,lastKnown,path,patrolSpeed,dwell);
            memory=investigation.DwellRemaining; state=State.Investigate; repath=0;
        }
        void EnsureNoiseInvestigation()
        {
            if(investigation.Active)return;
            float dwell=Mathf.Clamp(memory,.01f,3600);
            if(EnemyNavigation.TryRoute(agent,lastKnown,floorY,path)) BeginNoiseInvestigation(dwell);
            else investigation.Begin(lastKnown,dwell,Vector3.Distance(transform.position,lastKnown),patrolSpeed,0);
        }
        void InspectNoisePoint()
        {
            EnemyNavigation.Stop(agent,true);
            float sweep=Mathf.Sin((investigation.DwellDuration-investigation.DwellRemaining)*1.7f)*65;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,
                investigationFacing*Quaternion.Euler(0,sweep,0),160*Time.deltaTime);
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
            patrolDwelling = false; investigation.Reset();
            floorY = transform.position.y; repath = 0; recognitionCueIssued = false; awareness.Reset(); BindFootsteps(player);
        }
        void OnDisable()
        {
            ClearDoorPassage(); investigation.Reset();
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
            radius *= EnemyNavigation.SoundTransmission(point + Vector3.up * .5f,
                transform.position + Vector3.up, transform, player.transform);
            if (!StealthRules.Finite(radius) || Vector3.Distance(point, transform.position) > radius ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, radius)) return;
            var corners = path.corners;
            lastKnown = corners.Length > 0 ? corners[corners.Length - 1] : point;
            BeginNoiseInvestigation(3); FootstepNoisesAccepted++;
        }

        public bool CanSeePlayer()
        {
            if (!player || player.Hidden || !EnemyNavigation.WithinFloorPolicy(agent, player.transform.position, floorY)) return false;
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
            witnessedHiding = isActiveAndEnabled && player &&
                (state == State.Chase || awareness.Acquired) && CanSeePlayer() &&
                EnemyNavigation.WithinFloorPolicy(agent, entrance, floorY);
            if (!witnessedHiding) return;
            hidingApproach = entrance; lastKnown = entrance;
            if (player.HidingOutcome == CabinetHidingOutcome.Survived)
            {
                // The successful entry's shared 75% result overrides witness-based capture.
                // Inspect only the witnessed entrance; no knowledge of the hidden body is added.
                BeginSearch(); witnessedHiding = true; return;
            }
            memory = 8; state = State.Chase;
        }

        bool AtWitnessedHidingPlace()
        {
            var delta = transform.position - hidingApproach; delta.y = 0;
            return player.Hidden && !player.HidingProtected && witnessedHiding && state == State.Chase && delta.magnitude < .85f &&
                EnemyNavigation.SameActorFloor(agent, hidingApproach, floorY) &&
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

        float SearchRouteLimit => EnemyNavigation.AllowsCrossFloor(agent) && !searchStarted ? 38 : 8;

        void ClearDoorPassage()
        {
            if (blockingDoor) blockingDoor.ReleaseDoorPassage(this);
            blockingDoor = null; doorPush = doorEntrySide = doorCloseWait = doorBlockedWait = 0; passingDoor = false;
        }

        bool AbandonBlockedDoorRoute()
        {
            if (state == State.Patrol)
            {
                if (patrol == null || patrol.Length < 2) return false;
                waypoint = (waypoint + 1) % patrol.Length; patrolDwelling = false;
                ClearDoorPassage(); repath = 0; return true;
            }
            if (state == State.Investigate)
            {
                investigation.Cancel(); memory=0; state=State.Patrol;
                ClearDoorPassage(); repath=0; return true;
            }
            if (state == State.Chase && CanSeePlayer()) return false;
            if (state != State.Search) BeginSearch();
            // Keep the evidence anchor, but inspect the reachable side of this
            // physical blockage instead of retrying an inoperable leaf forever.
            searchTarget = transform.position; searchArrived = searchStarted = true;
            searchDwell = 0; searchFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            ClearDoorPassage(); repath = 0; return true;
        }

        bool TryPassDoor()
        {
            if (state != State.Chase && state != State.Investigate && state != State.Patrol && state != State.Search)
            { ClearDoorPassage(); return false; }
            if (state == State.Search && searchArrived)
            {
                // Arrival can precede full body clearance through a cached door.
                // Suspend handle requests during the local scan, keeping the
                // passage for the next observed route to finish and close behind.
                if (blockingDoor) blockingDoor.HoldDoorPassage(this);
                return false;
            }
            // SearchArea owns the same bounded native route. Stale/unreachable
            // evidence must enter its fallback scan before it can choose a door
            // along a straight line towards a goal that navigation has rejected.
            if (state == State.Search && !searchArrived &&
                !EnemyNavigation.TryRoute(agent,searchTarget,floorY,path,SearchRouteLimit))
            {
                ClearDoorPassage(); EnemyNavigation.Stop(agent,true);
                searchArrived=searchStarted=true; searchDwell=0;
                searchFacing=Quaternion.Euler(0,transform.eulerAngles.y,0);
                return false;
            }
            if (blockingDoor && (!EnemyNavigation.SameActorFloor(agent, blockingDoor.transform.position, floorY) ||
                HorizontalDistance(transform.position, blockingDoor.transform.position) > 4.5f)) ClearDoorPassage();
            if (blockingDoor)
            {
                blockingDoor.HoldDoorPassage(this);
                float side = Vector3.Dot(transform.position - blockingDoor.transform.position, blockingDoor.DoorNormal);
                if (passingDoor && side * doorEntrySide < -(agent.radius + .5f))
                {
                    // The first actor leaves the handle to the last actor. The
                    // final actor closes from within reach only after every body
                    // has cleared the opening; a blocked handle has a finite wait.
                    if (blockingDoor.HasOtherDoorPassage(this)) { ClearDoorPassage(); repath = 0; }
                    else if (!blockingDoor.IsOpen || blockingDoor.CloseForPursuer(this))
                    { ClearDoorPassage(); repath = 0; }
                    else if (doorCloseWait < 2 && blockingDoor.DoorOperable)
                    {
                        doorCloseWait = Mathf.Min(2, doorCloseWait + Time.deltaTime); EnemyNavigation.Stop(agent, true);
                        Vector3 facing = blockingDoor.transform.position - transform.position; facing.y = 0;
                        if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation,
                            Quaternion.LookRotation(facing), 180 * Time.deltaTime);
                        return true;
                    }
                    else { ClearDoorPassage(); repath = 0; }
                }
            }
            Vector3 targetPoint = state == State.Search ? searchTarget : lastKnown;
            if (state == State.Patrol)
            {
                if (patrol == null || patrol.Length == 0 || !patrol[waypoint % patrol.Length])
                { ClearDoorPassage(); return false; }
                targetPoint = patrol[waypoint % patrol.Length].position;
            }
            Vector3 intendedTarget = targetPoint;
            targetPoint = blockingDoor && !passingDoor ? blockingDoor.transform.position : agent.hasPath ? agent.steeringTarget : intendedTarget;
            Vector3 direction = targetPoint - transform.position; direction.y = 0;
            if (direction.sqrMagnitude < .001f) { direction = intendedTarget - transform.position; direction.y = 0; }
            if (direction.sqrMagnitude < .001f) return false;
            float radius = agent.radius + .06f;
            Vector3 bottom = transform.position + Vector3.up * (radius + .12f);
            Vector3 top = transform.position + Vector3.up * Mathf.Max(radius + .12f, agent.height - radius);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction.normalized, doorHits,
                Mathf.Min(direction.magnitude, 6), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == doorHits.Length) { EnemyNavigation.Stop(agent); return true; }
            int first = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (doorHits[i].collider.transform.IsChildOf(transform) ||
                    doorHits[i].collider.GetComponentInParent<StalkerBrain>() ||
                    doorHits[i].collider.GetComponentInParent<LanternMaskEncounter>() ||
                    doorHits[i].collider.GetComponentInParent<WeepingAngelEncounter>()) continue;
                if (doorHits[i].distance < distance) { first = i; distance = doorHits[i].distance; }
            }
            // Inspect the first physical obstruction on the native route. A wall
            // cannot reveal a remote door, and a radius-wide opening must be clear.
            var hit = first >= 0 ? doorHits[first] : default(RaycastHit);
            var door = first >= 0 ? hit.collider.GetComponentInParent<Interactable>() : null;
            Vector3 handlePoint = hit.point;
            if (!door || door.kind != Interactable.Kind.Door || !door.movingLeaf)
            {
                door = StalkerDoorTraversal.NearbyDoorOnRoute(agent, intendedTarget, floorY, path);
                if (door) handlePoint = door.transform.position;
            }
            if (!door || door.kind != Interactable.Kind.Door || !door.movingLeaf ||
                !EnemyNavigation.SameActorFloor(agent, door.transform.position, floorY))
            {
                if (blockingDoor && !passingDoor) ClearDoorPassage();
                return false;
            }
            if (blockingDoor != door)
            {
                ClearDoorPassage(); blockingDoor = door;
                doorEntrySide = Vector3.Dot(transform.position - door.transform.position, door.DoorNormal) >= 0 ? 1 : -1;
            }
            door.HoldDoorPassage(this);
            if (HorizontalDistance(transform.position, handlePoint) > Mathf.Max(1.25f, agent.radius + .85f))
            {
                doorPush = 0;
                if (!StalkerDoorTraversal.TryDoorApproach(agent,door,doorEntrySide,floorY,path))
                { EnemyNavigation.Stop(agent, true); return true; }
                agent.SetPath(path); agent.isStopped = false;
                return true;
            }
            EnemyNavigation.Stop(agent, true);
            doorBlockedWait = Mathf.Min(6.1f, doorBlockedWait + Time.deltaTime);
            if (doorBlockedWait >= 6 && AbandonBlockedDoorRoute()) return state != State.Search;
            Vector3 pushFacing = handlePoint - transform.position; pushFacing.y = 0;
            if (pushFacing.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(pushFacing), 180 * Time.deltaTime);
            if (door.IsOpen) { passingDoor = true; return true; }
            if (!door.DoorOperable) return true;
            doorPush = Mathf.Min(1.2f, doorPush + Time.deltaTime);
            if (doorPush >= 1.2f && door.OpenForPursuer())
            { passingDoor = true; doorPush = 0; repath = 0; }
            return true;
        }

        void BeginSearch()
        {
            investigation.Cancel();
            state = State.Search; memory = Senses.SearchSeconds; witnessedHiding = false; awareness.Reset();
            // This anchor is observed evidence, never the current unseen player.
            searchOrigin = searchTarget = lastKnown;
            searchCandidate = 0; searchArrived = searchStarted = false; searchDwell = searchDoorWait = 0; searchTransit = 4;
            // Narrow halls require an approach around the actual closed leaf.
            // Budget the observed route at walking speed instead of abandoning
            // it after a fixed four seconds; door waits remain separately bounded.
            var session=GameSession.Current;
            bool schoolStairs = EnemyNavigation.AllowsCrossFloor(agent);
            if((schoolStairs || session && session.CorridorMode && session.Corridor.Layout.Version>=2) &&
                EnemyNavigation.TryRoute(agent,searchOrigin,floorY,path,SearchRouteLimit))
            {
                float length=0; var previous=transform.position;
                foreach(var corner in path.corners) { length+=Vector3.Distance(previous,corner); previous=corner; }
                searchTransit=Mathf.Clamp(length/Mathf.Max(.25f,patrolSpeed)+3,4,schoolStairs?40:8);
            }
            SearchPointsVisited = 0; repath = 0;
            searchFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }

        void SearchArea()
        {
            if (!searchArrived && Vector3.Distance(transform.position, searchTarget) < .65f)
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
                        !EnemyNavigation.SameFloor(hit.position, searchOrigin.y) ||
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
            if (EnemyNavigation.TryRoute(agent, searchTarget, floorY, path, SearchRouteLimit))
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
                        string hint = attackingHiding ? CabinetHidingRules.RiskExplanation :
                            "공격음과 예고가 시작되면 거리를 벌리세요. 공격이 빗나간 직후 지나갈 수 있습니다.";
                        string counterplay = CorridorThreatRules.Counterplay(corridorRole);
                        if (!string.IsNullOrEmpty(counterplay)) hint = (attackingHiding ?
                            CabinetHidingRules.RiskExplanation : "공격 예고가 들리면 즉시 거리를 벌리세요.") + "\n\n" + counterplay;
                        session.TryDefeat(name, hint);
                    }
                }
                if (!attack.Active) repath = 0;
                return;
            }
            // A confirmed physical sight starts pursuit in this update. Sound
            // evidence never contributes to visual recognition or tracks the player.
            if (visible)
            {
                // Scripted encounters can enable an actor already in Chase.
                // Its first actual sight, not that scripted flag alone, earns the cue.
                if (!recognitionCueIssued) { DetectionFeedback.Signal(session, transform); recognitionCueIssued = true; }
                if (state != State.Chase) repath = 0;
                awareness.Restore(1); investigation.Cancel();
                state = State.Chase; memory = Senses.ChaseMemory; lastKnown = player.transform.position; witnessedHiding = false;
            }
            else if (state == State.Chase)
            {
                memory -= Time.deltaTime;
                // Once the last confirmed location is reached, actually inspect the
                // area instead of standing at one stale destination for nine seconds.
                if (memory <= 0 || !witnessedHiding && Vector3.Distance(transform.position, lastKnown) < .65f)
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
                else if (state == State.Investigate)
                {
                    EnsureNoiseInvestigation(); bool hadArrived=investigation.Arrived;
                    var step=investigation.Tick(Time.deltaTime,Vector3.Distance(transform.position,investigation.Point),true);
                    memory=investigation.DwellRemaining;
                    if(!hadArrived && investigation.Arrived) investigationFacing=Quaternion.Euler(0,transform.eulerAngles.y,0);
                    if(step==NoiseInvestigationClock.Step.Expired)
                    { memory=0; state=State.Patrol; ClearDoorPassage(); repath=0; }
                }
                if ((state == State.Search || state == State.Investigate) && memory <= 0)
                { state = State.Patrol; repath = 0; }
            }
            if (AtWitnessedHidingPlace()) { BeginAttack(true, session); return; }
            if (visible && state == State.Chase && offset.magnitude < 1.5f) { BeginAttack(false, session); return; }

            if(state==State.Investigate && investigation.Arrived) { InspectNoisePoint(); return; }
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
                    searchTransit = Mathf.Min(EnemyNavigation.AllowsCrossFloor(agent) ? 40 : 8, searchTransit + wait);
                    searchDoorWait += wait;
                }
                return;
            }
            if (state == State.Search) { SearchArea(); return; }
            if (state == State.Patrol && Senses.PatrolDwell > 0 && patrol != null && patrol.Length > 0)
            {
                waypoint %= patrol.Length;
                if (patrol[waypoint] && Vector3.Distance(transform.position, patrol[waypoint].position) < .6f)
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
            // Only complete native routes may reach patrol markers, including
            // connected stair routes in the school chapter.
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
