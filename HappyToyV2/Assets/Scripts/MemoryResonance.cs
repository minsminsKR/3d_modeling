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
        float nextTrace;
        bool started, occluded;
        void Awake()
        {
            voice = GetComponent<AudioSource>(); filter = GetComponent<AudioLowPassFilter>();
            const int rate = 24000;
            var samples = new float[rate * 4];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                for (int note = 0; note < 3; note++)
                {
                    float t = time - .12f - note * .22f;
                    if (t < 0 || t > 1.2f) continue;
                    float pitch = note == 1 ? 932.33f : 622.25f;
                    samples[i] += .22f * Mathf.Clamp01(t / .008f) * Mathf.Exp(-7 * t) *
                        (Mathf.Sin(2 * Mathf.PI * pitch * t) + .22f * Mathf.Sin(2 * Mathf.PI * pitch * 2.01f * t));
                }
            }
            clip = AudioClip.Create("Original memory bell", samples.Length, 1, rate, false); clip.SetData(samples, 0);
            voice.clip = clip; voice.playOnAwake = false; voice.loop = true; voice.spatialBlend = 1;
            voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = .5f; voice.maxDistance = 9;
            voice.dopplerLevel = 0; voice.priority = 150; voice.volume = .16f;
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) { if (started) voice.Pause(); return; }
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
        void OnDestroy() { if (clip) Destroy(clip); }
    }
}
