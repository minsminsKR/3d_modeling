using UnityEngine;

namespace HappyToy.V2
{
    // A quiet sound emitted by the collectible itself. Walls muffle it; it gives
    // no map marker or information about actors the player has not perceived.
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public sealed class MemoryResonance : MonoBehaviour
    {
        AudioSource voice;
        AudioLowPassFilter filter;
        AudioClip clip;
        Interactable memory;
        float nextTrace;
        bool started, occluded;
        public AudioSource Source => voice;
        public bool GuidingCurrentMemory { get; private set; }
        void Awake()
        {
            voice = GetComponent<AudioSource>(); filter = GetComponent<AudioLowPassFilter>();
            memory = GetComponent<Interactable>();
            clip = ExternalAudio.Required("memory-bell");
            voice.clip = clip; voice.playOnAwake = false; voice.loop = true; voice.spatialBlend = 1;
            voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = .5f; voice.maxDistance = 9;
            voice.dopplerLevel = 0; voice.priority = 150; voice.volume = .16f;
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) { if (started) voice.Pause(); return; }
            // The objective already reveals the next memory. Ring only that clue,
            // rather than inviting a new player to a future, uncollectable item.
            GuidingCurrentMemory = !session.ChapterMode || memory && memory.stableId == session.CurrentObjectiveId;
            if (!GuidingCurrentMemory)
            {
                voice.Stop(); started = false; voice.volume = 0; return;
            }
            if (!started) { voice.Play(); started = true; } else voice.UnPause();
            if (Time.time >= nextTrace)
            {
                nextTrace = Time.time + .2f;
                occluded = Physics.Linecast(session.player.eyes.transform.position, transform.position, out var hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform);
            }
            voice.volume = Mathf.MoveTowards(voice.volume, occluded ? .04f : .16f, Time.deltaTime * .3f);
            filter.cutoffFrequency = Mathf.MoveTowards(filter.cutoffFrequency, occluded ? 1100 : 6500, Time.deltaTime * 15000);
        }
        void OnDisable() { if (voice) voice.Stop(); started = false; GuidingCurrentMemory = false; }
        void OnDestroy() { if (clip) Destroy(clip); }
    }
}
