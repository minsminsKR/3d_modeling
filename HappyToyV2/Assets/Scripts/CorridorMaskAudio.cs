using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    /// <summary>The corridor runner owns recorded animal paw contacts and its near human whistle.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LanternMaskEncounter), typeof(NavMeshAgent))]
    public sealed class CorridorMaskAudio : MonoBehaviour
    {
        public const float ContactStride = 1.35f;
        public int ContactsPlayed { get; private set; }
        public int WhistlesPlayed { get; private set; }
        public int DoorSmashesPlayed { get; private set; }
        public int TeleportsSuppressed { get; private set; }
        public AudioSource ContactSource => contacts;
        public AudioSource WhistleSource => whistle;
        public EnemyAcoustics ContactAcoustics => contactAcoustics;
        public EnemyAcoustics WhistleAcoustics => whistleAcoustics;
        AudioClip[] contactClips, whistleClips;
        AudioSource contacts, whistle;
        EnemyAcoustics contactAcoustics, whistleAcoustics;
        LanternMaskEncounter mask;
        NavMeshAgent agent;
        Vector3 previous;
        float travelled, nextWhistle;
        int whistleVariant;

        void Awake()
        {
            mask = GetComponent<LanternMaskEncounter>(); agent = GetComponent<NavMeshAgent>();
            contactClips = LoadBank("mask-beast-step"); whistleClips = LoadBank("mask-near-whistle");
            contacts = MakeEmitter("Corridor mask recorded beast paws", .1f, 3.6f, 31f, 28, .92f, out contactAcoustics);
            whistle = MakeEmitter("Corridor mask near whistle", 1.95f, 1.8f,
                LanternMaskEncounter.NearWhistleDistance + .25f, 22, .64f, out whistleAcoustics);
        }
        AudioClip[] LoadBank(string cue)
        {
            var clips = new AudioClip[ExternalAudio.VariantCount(cue)];
            if (clips.Length == 0) throw new System.InvalidOperationException("Missing required mask cue: " + cue);
            for (int index = 0; index < clips.Length; index++) clips[index] = ExternalAudio.Required(cue, index);
            return clips;
        }
        AudioSource MakeEmitter(string name, float height, float minimum, float maximum, int priority,
            float gain, out EnemyAcoustics acoustics)
        {
            var emitter = new GameObject(name); emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = Vector3.up * height;
            var voice = emitter.AddComponent<AudioSource>(); voice.minDistance = minimum; voice.maxDistance = maximum;
            voice.priority = priority; acoustics = EnemyAcoustics.Bind(voice, transform, gain, 0);
            return voice;
        }
        bool Live()
        {
            var session = GameSession.Current;
            return isActiveAndEnabled && mask && mask.isActiveAndEnabled && mask.CorridorRunner &&
                mask.State != LanternMaskEncounter.Phase.Dormant && mask.State != LanternMaskEncounter.Phase.Resolved &&
                session && session.InputAllowed && !session.Finished && session.StoryStep < 4 && !AudioListener.pause && !EnemyTimeStop.Active;
        }
        void OnEnable() { previous = transform.position; travelled = 0; nextWhistle = Time.time + .25f; }
        internal void DelayTimeStopDeadline(float seconds) { nextWhistle += seconds; }
        void Update()
        {
            if (EnemyTimeStop.Active) return;
            Vector3 delta = transform.position - previous; previous = transform.position;
            float vertical = Mathf.Abs(delta.y); delta.y = 0;
            if (!Live())
            {
                travelled = 0;
                var session = GameSession.Current;
                // Listener pause preserves the current natural tails. Finished,
                // disabled or dormant actors cannot leave an emitter playing.
                if (!session || session.Finished || session.StoryStep >= 4 || !mask || !mask.isActiveAndEnabled || !mask.CorridorRunner ||
                    mask.State == LanternMaskEncounter.Phase.Dormant || mask.State == LanternMaskEncounter.Phase.Resolved)
                    StopVoices();
                return;
            }
            if (!EnemyNavigation.Ready(agent)) { travelled = 0; return; }
            float movement = delta.magnitude;
            float maximumTravel = Mathf.Max(.5f, agent.velocity.magnitude * Time.deltaTime * 2.5f + .1f);
            if (vertical > .65f || movement > maximumTravel)
            { travelled = 0; TeleportsSuppressed++; return; }
            bool running = !agent.isStopped && agent.hasPath && agent.velocity.sqrMagnitude > .25f && movement >= .001f;
            if (running)
            {
                travelled += movement;
                if (travelled >= ContactStride)
                {
                    travelled %= ContactStride;
                    contacts.pitch = ContactsPlayed % 2 == 0 ? .985f : 1.015f;
                    contacts.PlayOneShot(contactClips[ContactsPlayed % contactClips.Length]); ContactsPlayed++;
                    PerceivedTension.ReportSound(GameSession.Current, contacts, contactAcoustics, false);
                }
            }
            else travelled = 0;
            var player = GameSession.Current.player;
            bool near = player && player.eyes && EnemyNavigation.SameFloor(transform.position, player.transform.position.y) &&
                Vector3.Distance(player.eyes.transform.position, whistle.transform.position) <= LanternMaskEncounter.NearWhistleDistance;
            if (near && Time.time >= nextWhistle && !whistle.isPlaying)
            {
                whistle.pitch = 1; var clip = whistleClips[whistleVariant++ % whistleClips.Length];
                whistle.PlayOneShot(clip); WhistlesPlayed++; nextWhistle = Time.time + clip.length + .6f;
                PerceivedTension.ReportSound(GameSession.Current, whistle, whistleAcoustics, false);
            }
            // A close warning has an exact physical range: its uncompleted tail
            // must not follow the player into a safe distant room.
            if (!near && whistle.isPlaying) whistle.Stop();
        }
        public void PlayDoorSmash(Transform door)
        {
            if(door && Live() && DoorDestructionAudio.PlayAt(door.GetComponent<Interactable>()))DoorSmashesPlayed++;
        }
        void StopVoices()
        { if (contacts) contacts.Stop(); if (whistle) whistle.Stop(); }
        void OnDisable() { travelled = 0; StopVoices(); }
        void OnDestroy()
        {
            StopVoices();
            if (contactClips != null) foreach (var clip in contactClips) if (clip) Destroy(clip);
            if (whistleClips != null) foreach (var clip in whistleClips) if (clip) Destroy(clip);
            if (contacts) Destroy(contacts.gameObject); if (whistle) Destroy(whistle.gameObject);
        }
    }
}
