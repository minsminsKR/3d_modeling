using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // V1 mannequin: flashlight-powered, floor-bound pursuit interrupted by visible gaze.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WeepingAngelEncounter : MonoBehaviour
    {
        public Transform visual;
        public Light displayLight;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Observed { get; private set; }
        public bool Moving { get; private set; }
        public bool Resolved { get; private set; }
        public int PathRequests { get; private set; }
        public int AttacksStarted { get; private set; }
        public bool AttackActive => attack.Active;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        static readonly Vector3[] sightPoints = {
            new Vector3(0, 1.65f, 0), new Vector3(-.25f, 1.1f, 0),
            new Vector3(.25f, 1.1f, 0), new Vector3(0, .4f, 0)
        };
        NavMeshAgent agent;
        NavMeshPath path;
        AudioSource sound;
        AudioClip creak;
        float intro, repath, soundTimer, floorY, unobservedFor;
        Quaternion startTurn, endTurn;
        readonly EnemyAttackClock attack = new EnemyAttackClock();

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); floorY = transform.position.y;
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1;
            sound.minDistance = 2; sound.maxDistance = 15; sound.volume = .3f; sound.dopplerLevel = 0;
            const int rate = 24000; var data = new float[rate];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(Mathf.PI * t) * (.28f * Mathf.Sin(720 * t + 18 * Mathf.Sin(31 * t)) + .12f * Mathf.Sin(1190 * t));
            }
            creak = AudioClip.Create("Mannequin joint creak", rate, 1, rate, false); creak.SetData(data, 0);
        }
        void Stop()
        {
            Moving = false; EnemyNavigation.Stop(agent, true);
        }
        bool Clear(Camera camera, Vector3 point)
        {
            return camera && EnemyNavigation.ClearSight(camera.transform.position, point);
        }
        public bool VisibleTo(Camera camera)
        {
            if (!camera) return false;
            // A small screen-edge margin and body samples avoid movement while a shoulder
            // is still visible. Reuse the samples rather than allocating an array per frame.
            foreach (var local in sightPoints)
            {
                var point = transform.TransformPoint(local); var viewport = camera.WorldToViewportPoint(point);
                if (viewport.z > 0 && viewport.x >= -.025f && viewport.x <= 1.025f &&
                    viewport.y >= -.025f && viewport.y <= 1.025f && Clear(camera, point)) return true;
            }
            return false;
        }
        void Resolve()
        {
            Resolved = true; Observed = false; attack.Reset(); Stop(); if (sound) sound.Stop();
            if (displayLight) displayLight.enabled = false;
            if (visual) visual.gameObject.SetActive(false);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session) return;
            if (session.Finished || session.StoryStep >= 4) { Resolve(); return; }
            if (Resolved) return;
            if (!session.InputAllowed || !EnemyNavigation.Ready(agent)) { Stop(); repath = 0; return; }
            var player = session.player;
            if (!player || !player.eyes || !visual) { Stop(); return; }
            var delta = player.transform.position - transform.position;
            bool sameFloor = EnemyNavigation.SameFloor(player.transform.position, floorY);
            Observed = sameFloor && VisibleTo(player.eyes);
            if (!Triggered)
            {
                Stop();
                if (session.StoryStep < 1 || player.Hidden || !sameFloor || delta.magnitude > 8 || !Observed) return;
                Triggered = true; startTurn = visual.localRotation;
                var toward = delta; toward.y = 0;
                endTurn = toward.sqrMagnitude > .001f ? Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(toward) : startTurn;
                session.Notify("등을 돌린 마네킹이 돌아봅니다. 눈을 떼지 마세요. 손전등을 끄면 멈춥니다.");
                sound.PlayOneShot(creak);
            }
            if (!Released)
            {
                Stop(); intro += Time.deltaTime;
                visual.localRotation = Quaternion.Slerp(startTurn, endTurn, Mathf.SmoothStep(0, 1, Mathf.Clamp01((intro - .65f) / .85f)));
                if (intro >= 2.2f) Released = true;
                return;
            }
            if (!sameFloor || delta.magnitude > 30 || player.Hidden || !player.flashlight || !player.flashlight.enabled || Observed)
            {
                Stop(); repath = 0; unobservedFor = 0; attack.Reset(); return;
            }
            // A single occluded frame at a door edge cannot trigger a movement or strike.
            unobservedFor += Time.deltaTime;
            if (unobservedFor < .2f) { Stop(); repath = 0; return; }
            bool contact = delta.magnitude < 1.05f && Clear(player.eyes, transform.position + Vector3.up * 1.2f);
            if (attack.Active)
            {
                Stop();
                if (attack.Tick(Time.deltaTime) && contact)
                    session.TryDefeat("Weeping mannequin", "마네킹의 관절 소리가 가까워지면 돌아보거나 손전등을 끄세요. 시선이 닿으면 공격도 멈춥니다.");
                if (!attack.Active) repath = 0;
                return;
            }
            if (contact)
            {
                attack.Begin(.65f, .9f); AttacksStarted++; Stop();
                sound.PlayOneShot(creak, 1.3f);
                session.WarnThreat("마네킹의 관절 소리가 가까이 납니다 · 돌아보거나 손전등을 끄세요.", 1.6f);
                return;
            }
            repath -= Time.deltaTime;
            if (repath <= 0)
            {
                repath = .4f; PathRequests++;
                if (!EnemyNavigation.TryRoute(agent, player.transform.position, floorY, path)) { Stop(); return; }
                agent.SetPath(path); agent.isStopped = false;
            }
            Moving = !agent.isStopped && agent.hasPath;
            if (Moving)
            {
                var facing = agent.steeringTarget - transform.position; facing.y = 0;
                if (facing.sqrMagnitude > .01f) visual.rotation = Quaternion.LookRotation(facing);
                soundTimer -= Time.deltaTime;
                if (soundTimer <= 0) { soundTimer = .8f; sound.PlayOneShot(creak); }
            }
        }
        void OnDisable() { attack.Reset(); unobservedFor = 0; Stop(); if (sound) sound.Stop(); }
        void OnDestroy() { if (creak) Destroy(creak); }
    }
}
