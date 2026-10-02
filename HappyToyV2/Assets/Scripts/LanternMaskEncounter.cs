using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LanternMaskEncounter : MonoBehaviour
    {
        public enum Phase { Dormant, Wander, Investigate, Chase, Transforming, Resolved }
        public Phase State { get; private set; } = Phase.Dormant;
        public Transform mask, body, lantern;
        public Animation motion;
        public Light flameLight;
        public Transform[] patrol;
        public bool Transformed { get; private set; }
        public float TransformProgress => Mathf.Clamp01(transformTime / 5);
        public int CursesApplied { get; private set; }
        public int AttacksStarted { get; private set; }
        public bool AttackActive => attack.Active;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        NavMeshAgent agent;
        NavMeshPath path;
        Vector3 target;
        float floorY, age, transformTime, memory, repath;
        int waypoint;
        readonly EnemyAttackClock attack = new EnemyAttackClock();
        AudioSource sound;
        AudioClip warning;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); floorY = transform.position.y;
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1;
            sound.minDistance = 2; sound.maxDistance = 16; sound.dopplerLevel = 0; sound.volume = .5f;
            const int rate = 24000;
            var data = new float[(int)(rate * .55f)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * i / (data.Length - 1));
                data[i] = envelope * (.18f * Mathf.Sin(2 * Mathf.PI * (190 * t + 75 * t * t)) +
                    .08f * Mathf.Sin(2 * Mathf.PI * 570 * t));
            }
            warning = AudioClip.Create("Lantern warning rattle", data.Length, 1, rate, false); warning.SetData(data, 0);
        }
        void Start() { SetVisible(false); }
        void SetVisible(bool show)
        {
            if (mask) mask.gameObject.SetActive(show);
            if (lantern) lantern.gameObject.SetActive(show && !Transformed);
            if (body) body.gameObject.SetActive(show && TransformProgress > 0);
            if (flameLight) flameLight.enabled = show;
        }
        void Stop() { EnemyNavigation.Stop(agent); }
        bool Route(Vector3 point) => EnemyNavigation.TryRoute(agent, point, floorY, path);
        bool Sees(PlayerMotor player)
        {
            return player && !player.Hidden && EnemyNavigation.SameFloor(player.transform.position, floorY) &&
                Vector3.Distance(player.transform.position, transform.position) < 12 &&
                EnemyNavigation.ClearSight(transform.position + Vector3.up, player.transform.position + Vector3.up * 1.1f, player);
        }
        public bool HearNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || !EnemyNavigation.Ready(agent) ||
                State == Phase.Dormant || State == Phase.Resolved || State == Phase.Chase || State == Phase.Transforming ||
                attack.Active || duration <= 0 || Vector3.Distance(point, transform.position) > 28 || Sees(session.player) ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, 38)) return false;
            State = Phase.Investigate; target = point; memory = duration; repath = 0; return true;
        }
        void Resolve()
        {
            State = Phase.Resolved; attack.Reset(); EnemyNavigation.Stop(agent, true); SetVisible(false);
            if (sound) sound.Stop();
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session) return;
            if (session.Finished || session.StoryStep >= 4) { Resolve(); return; }
            if (!session.InputAllowed || !EnemyNavigation.Ready(agent)) { Stop(); repath = 0; return; }
            if (State == Phase.Resolved) return;
            if (State == Phase.Dormant)
            {
                if (session.StoryStep < 2) { Stop(); return; }
                State = Phase.Wander;
            }
            SetVisible(true);
            age += Time.deltaTime;
            var player = session.player;
            if (!player) { Stop(); return; }
            if (State == Phase.Transforming)
            {
                Stop(); transformTime = Mathf.Min(5, transformTime + Time.deltaTime);
                if (transformTime >= 5)
                {
                    Transformed = true; State = Phase.Chase; memory = 8; target = player.transform.position; repath = 0;
                    sound.pitch = .6f; sound.PlayOneShot(warning);
                    if (EnemyNavigation.SameFloor(player.transform.position, floorY) &&
                        Vector3.Distance(player.transform.position, transform.position) <= sound.maxDistance)
                        session.WarnThreat("가면의 몸이 완성됐습니다 · 녹색 가면을 피해 다른 복도로 이동하세요.", 3);
                }
                Visual(); return;
            }
            bool sees = Sees(player);
            float distance = Vector3.Distance(player.transform.position, transform.position);
            float strikeRange = Transformed ? .85f : .95f;
            if (attack.Active)
            {
                Stop();
                if (attack.Tick(Time.deltaTime) && sees && distance < strikeRange)
                {
                    if (Transformed)
                    {
                        session.TryDefeat("Lantern mask", "가면이 기울며 소리를 내면 즉시 거리를 벌리세요. 변신한 가면의 공격도 피할 수 있습니다.");
                        Visual(); return;
                    }
                    player.ApplyCurse(10); CursesApplied++; State = Phase.Transforming; transformTime = 0; attack.Reset();
                    session.Notify("가면의 저주 · 10초간 속도 50%. 몸이 자라기 전에 다른 복도로 피하세요.");
                    session.WarnThreat("저주에 걸렸습니다 · 가면이 자라는 5초 동안 출구로 이동하세요.", 4);
                }
                if (!attack.Active) repath = 0;
                Visual(); return;
            }
            if (sees) { State = Phase.Chase; target = player.transform.position; memory = Transformed ? 8 : 3; }
            else if (State == Phase.Chase || State == Phase.Investigate)
            { memory -= Time.deltaTime; if (memory <= 0) { State = Phase.Wander; repath = 0; } }
            if (sees && distance < (Transformed ? .75f : .85f))
            {
                attack.Begin(Transformed ? .75f : .6f, .9f); AttacksStarted++; Stop();
                sound.pitch = Transformed ? .7f : 1; sound.PlayOneShot(warning);
                session.WarnThreat(Transformed ? "가면이 공격을 준비합니다 · 즉시 거리를 벌리세요." :
                    "녹색 가면이 저주를 준비합니다 · 뒤로 물러나세요.", 1.6f);
                Visual(); return;
            }
            if (!EnemyNavigation.SameFloor(player.transform.position, floorY)) { Stop(); repath = 0; Visual(); return; }
            if (State == Phase.Wander && patrol != null && patrol.Length > 0)
            {
                waypoint %= patrol.Length;
                if (patrol[waypoint] && Vector3.Distance(transform.position, patrol[waypoint].position) < .6f)
                    waypoint = (waypoint + 1) % patrol.Length;
                if (patrol[waypoint]) target = patrol[waypoint].position;
            }
            float stride = age % 2.4f < .2f ? .18f : age % 2.4f < .75f ? 1.4f : .9f;
            agent.speed = State == Phase.Chase ? (Transformed ? 3.4f * stride : 2.7f) : 1.15f;
            repath -= Time.deltaTime;
            if (repath <= 0)
            {
                repath = .35f;
                if (Route(target)) { agent.SetPath(path); agent.isStopped = false; }
                else { EnemyNavigation.Stop(agent, true); if (patrol != null && patrol.Length > 0) waypoint = (waypoint + 1) % patrol.Length; }
            }
            Visual();
        }
        void Visual()
        {
            float progress = TransformProgress;
            var session = GameSession.Current;
            bool reducedMotion = session && session.Shell && session.Shell.ReducedMotion;
            if (mask)
            {
                mask.localPosition = new Vector3(0, 1.25f + progress * .82f + (reducedMotion ? 0 : .2f * (1 - progress) * Mathf.Sin(age * 2.3f)), 0);
                mask.localRotation = Quaternion.Euler(-18 * attack.Windup, 0, progress * (-16 + (reducedMotion ? 0 : Mathf.Sin(age * 31) * 2.5f)));
            }
            if (body)
            {
                body.gameObject.SetActive(progress > 0);
                body.localScale = new Vector3(.3f + .7f * progress, Mathf.Max(.001f, progress), .45f + .55f * progress);
            }
            if (lantern)
            { lantern.gameObject.SetActive(progress < 1); lantern.localScale = Vector3.one * Mathf.Max(.001f, 1 - progress); }
            if (flameLight) flameLight.intensity = 1.2f + (reducedMotion ? 0 : Mathf.Sin(age * 9) * .18f) + attack.Windup * .7f;
            if (motion && progress > 0)
            {
                bool moving = EnemyNavigation.Ready(agent) && !agent.isStopped && agent.velocity.sqrMagnitude > .01f;
                var clip = motion["run"];
                if (clip != null)
                {
                    if (!motion.IsPlaying("run")) motion.Play("run");
                    clip.speed = moving ? agent.speed / 3.4f : 0;
                    if (!moving) { clip.time = 0; motion.Sample(); }
                }
            }
        }
        void LateUpdate()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || TransformProgress <= 0 || State == Phase.Resolved || !body || !mask) return;
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!skin) return;
            var bounds = skin.bounds;
            var attached = new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z) + transform.forward * .03f;
            mask.position = Vector3.Lerp(mask.position, attached, TransformProgress);
        }
        void OnDisable() { attack.Reset(); EnemyNavigation.Stop(agent, true); if (sound) sound.Stop(); SetVisible(false); }
        void OnDestroy() { if (warning) Destroy(warning); }
    }
}
