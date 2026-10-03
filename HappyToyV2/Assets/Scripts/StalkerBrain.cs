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
        float memory, repath, floorY;
        int waypoint;
        bool witnessedHiding, attackingHiding;
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

        public bool HearNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.Finished || session.StoryStep >= 4 ||
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
            floorY = transform.position.y; repath = 0; awareness.Reset(); BindFootsteps(player);
        }
        void OnDisable()
        {
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
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.Finished || session.StoryStep >= 4 ||
                !player || noisePlayer != player || player.Hidden || state == State.Chase || attack.Active ||
                !StealthRules.Finite(radius) || radius <= 0 || !StealthRules.Finite(point.x) ||
                !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                Vector3.Distance(point, transform.position) > radius ||
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
                if (delta.magnitude < 14 && (state == State.Chase || Vector3.Angle(transform.forward, delta) < 65) &&
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
            if (footsteps) footsteps.PlayAttackCue();
            session.WarnThreat(hiding ? "은신처를 들켰습니다 · 문 앞에서 공격을 준비합니다. 지금 빠져나오세요." :
                "가까운 적이 공격을 준비합니다 · 즉시 거리를 벌리세요.", 1.5f);
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!player && session) player = session.player;
            BindFootsteps(player);
            if (!session || !player) { EnemyNavigation.Stop(agent); return; }
            if (session.Finished || session.StoryStep >= 4)
            { awareness.Reset(); attack.Reset(); witnessedHiding = false; EnemyNavigation.Stop(agent, true); return; }
            if (!EnemyNavigation.Ready(agent)) { attack.Reset(); repath = 0; return; }
            if (!session.InputAllowed) { EnemyNavigation.Stop(agent); repath = 0; return; }

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
                    if (hit) session.TryDefeat(name, attackingHiding ?
                        "숨는 모습을 본 적은 문 앞까지 따라옵니다. 시야를 끊은 뒤 은신처에 들어가세요." :
                        "공격음과 예고가 시작되면 거리를 벌리세요. 공격이 빗나간 직후 지나갈 수 있습니다.");
                }
                if (!attack.Active) repath = 0;
                return;
            }
            bool acquiring = false;
            if (state != State.Chase)
            {
                bool lightOn = player.flashlight && player.flashlight.isActiveAndEnabled;
                acquiring = visible && offset.magnitude < StealthRules.SightRange(player.Crouching, lightOn, false, 14);
                awareness.Tick(acquiring,
                    StealthRules.AcquisitionSeconds(player.Crouching, lightOn, player.Running, offset.magnitude), Time.deltaTime);
            }
            if (visible && (state == State.Chase || acquiring && awareness.Acquired))
            { state = State.Chase; memory = 5; lastKnown = player.transform.position; witnessedHiding = false; }
            else if (state == State.Chase)
            {
                memory -= Time.deltaTime;
                if (memory <= 0) { state = State.Search; memory = 4; witnessedHiding = false; awareness.Reset(); }
            }
            else
            {
                if (state == State.Search || state == State.Investigate)
                { memory -= Time.deltaTime; if (memory <= 0) { state = State.Patrol; repath = 0; } }
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
