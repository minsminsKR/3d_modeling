using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerBrain : MonoBehaviour
    {
        public Transform[] patrol;
        public float patrolSpeed = 1.45f, chaseSpeed = 3.5f;
        public PlayerMotor player;
        public enum State { Patrol, Investigate, Chase, Search }
        public State state;
        NavMeshAgent agent;
        NavMeshPath path;
        StalkerFootsteps footsteps;
        Vector3 lastKnown, hidingApproach, attackHidingApproach, attackFacing;
        float memory, repath, hearingCheck, seenFor, floorY;
        int waypoint;
        bool witnessedHiding, attackingHiding;
        readonly EnemyAttackClock attack = new EnemyAttackClock();
        public bool SawHiding => witnessedHiding;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        public bool AttackActive => attack.Active;
        public int AttacksStarted { get; private set; }
        public int NoisesAccepted { get; private set; }
        public float HomeFloorY => floorY;

        public bool HearNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || duration <= 0 ||
                state == State.Chase || AttackActive || CanSeePlayer() ||
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
        void OnEnable() { floorY = transform.position.y; repath = hearingCheck = seenFor = 0; }
        void OnDisable() { attack.Reset(); witnessedHiding = false; EnemyNavigation.Stop(agent, true); }

        public bool CanSeePlayer()
        {
            if (!player || player.Hidden || !EnemyNavigation.SameFloor(player.transform.position, floorY)) return false;
            var eye = transform.position + Vector3.up * 1.7f;
            // A visible torso counts too; a camera point can sit outside the capsule's curved head.
            for (int i = 0; i < 2; i++)
            {
                var target = i == 0 && player.eyes ? player.eyes.transform.position : player.transform.position + Vector3.up * 1.1f;
                var delta = target - eye;
                if (delta.magnitude < 14 && (state == State.Chase || Vector3.Angle(transform.forward, delta) < 65) &&
                    EnemyNavigation.ClearSight(eye, target, player)) return true;
            }
            return false;
        }

        public void ObserveHiding(Vector3 entrance)
        {
            // Called before the player's collider disappears, not inferred from old chase memory.
            witnessedHiding = CanSeePlayer() && EnemyNavigation.SameFloor(entrance, floorY);
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
            if (footsteps) footsteps.PlayAttackCue();
            session.WarnThreat(hiding ? "은신처를 들켰습니다 · 문 앞에서 공격을 준비합니다. 지금 빠져나오세요." :
                "가까운 적이 공격을 준비합니다 · 즉시 거리를 벌리세요.", 1.5f);
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!session || !player) { EnemyNavigation.Stop(agent); return; }
            if (session.Finished || session.StoryStep >= 4)
            { attack.Reset(); witnessedHiding = false; EnemyNavigation.Stop(agent, true); return; }
            if (!EnemyNavigation.Ready(agent)) { attack.Reset(); repath = 0; return; }
            if (!session.InputAllowed) { EnemyNavigation.Stop(agent); repath = 0; return; }

            bool visible = CanSeePlayer();
            var offset = player.transform.position - transform.position;
            seenFor = visible ? seenFor + Time.deltaTime : 0;
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
                    if (hit) session.TryDefeat(name, attackingHiding ?
                        "숨는 모습을 본 적은 문 앞까지 따라옵니다. 시야를 끊은 뒤 은신처에 들어가세요." :
                        "공격음과 예고가 시작되면 거리를 벌리세요. 공격이 빗나간 직후 지나갈 수 있습니다.");
                }
                if (!attack.Active) repath = 0;
                return;
            }
            if (visible && (state == State.Chase || seenFor >= .18f))
            { state = State.Chase; memory = 5; lastKnown = player.transform.position; witnessedHiding = false; }
            else if (state == State.Chase)
            {
                memory -= Time.deltaTime;
                if (memory <= 0) { state = State.Search; memory = 4; witnessedHiding = false; }
            }
            else
            {
                hearingCheck -= Time.deltaTime;
                if (player.Running && !player.Hidden && offset.magnitude < 8 && hearingCheck <= 0)
                {
                    hearingCheck = .4f;
                    if (EnemyNavigation.TryRoute(agent, player.transform.position, floorY, path, 12))
                    { state = State.Investigate; lastKnown = player.transform.position; memory = 3; repath = 0; }
                }
                if (state == State.Search || state == State.Investigate)
                { memory -= Time.deltaTime; if (memory <= 0) { state = State.Patrol; repath = 0; } }
            }
            if (AtWitnessedHidingPlace()) { BeginAttack(true, session); return; }
            if (visible && state == State.Chase && offset.magnitude < 1.5f) { BeginAttack(false, session); return; }

            agent.speed = state == State.Chase ? chaseSpeed : patrolSpeed;
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
