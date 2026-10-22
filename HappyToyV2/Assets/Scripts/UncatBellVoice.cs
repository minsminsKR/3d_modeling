using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // One recorded bell emitter; physical movement and pursuit choose its rhythm.
    [DisallowMultipleComponent, RequireComponent(typeof(StalkerBrain))]
    public sealed class UncatBellVoice : MonoBehaviour
    {
        public const float NearDistance=3f, AudibleDistance=48f;
        StalkerBrain brain;
        NavMeshAgent agent;
        AudioSource voice;
        AudioClip bell;
        EnemyAcoustics acoustics;
        readonly EnemyBellClock rhythm = new EnemyBellClock();
        Vector3 previous;
        bool loud;
        public int RingsPlayed { get; private set; }
        public AudioSource BellSource => voice;
        public bool FastRhythm => brain && brain.state == StalkerBrain.State.Chase;
        void Awake()
        {
            brain = GetComponent<StalkerBrain>(); agent = GetComponent<NavMeshAgent>();
            var existing = transform.Find("Uncat recorded bell voice");
            var emitter = existing ? existing.gameObject : new GameObject("Uncat recorded bell voice");
            emitter.transform.SetParent(transform, false); emitter.transform.localPosition = Vector3.up * .65f;
            voice = emitter.GetComponent<AudioSource>(); if (!voice) voice = emitter.AddComponent<AudioSource>();
            // A carried bell warns across several corridor sections. Keep a smooth
            // linear falloff, real panning and the existing wall/floor transmission.
            voice.minDistance = NearDistance; voice.maxDistance = AudibleDistance; voice.priority = 55;
            acoustics = EnemyAcoustics.Bind(voice, transform, rhythm.Gain(false), 0);
            bell = ExternalAudio.Required("enemy-uncat-bell");
            previous = transform.position;
        }
        void LateUpdate()
        {
            if (EnemyTimeStop.Active) return;
            var delta = transform.position - previous; previous = transform.position; delta.y = 0;
            var session = GameSession.Current;
            bool allowed = session && session.CorridorMode && session.InputAllowed && !session.Finished &&
                brain && brain.isActiveAndEnabled && brain.corridorRole == CorridorThreatRole.Listener &&
                EnemyNavigation.Ready(agent) && !agent.isStopped && !brain.AttackActive;
            // Warps, blocked navigation and pause never manufacture movement sound.
            bool moving = allowed && delta.sqrMagnitude > .000001f && delta.magnitude < Mathf.Max(1, Time.deltaTime * 12);
            if (!allowed) { rhythm.Reset(); if (voice) voice.Stop(); return; }
            if (loud != FastRhythm)
            { loud = FastRhythm; acoustics = EnemyAcoustics.Bind(voice, transform, rhythm.Gain(loud), 0); }
            if (!rhythm.Tick(Time.deltaTime, moving, FastRhythm)) return;
            voice.pitch = RingsPlayed % 2 == 0 ? .985f : 1.015f;
            voice.PlayOneShot(bell); RingsPlayed++;
            PerceivedTension.ReportSound(session, voice, acoustics, false);
        }
        void OnEnable() { previous = transform.position; rhythm.Reset(); }
        void OnDisable() { rhythm.Reset(); if (voice) voice.Stop(); }
        void OnDestroy() { if (bell) Destroy(bell); if (voice) Destroy(voice.gameObject); }
    }
}
