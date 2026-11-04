using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Two existing CC0 door recordings follow the opt-in gate's three-second
    // opening. The motion clock is authoritative for both delay and restore.
    public sealed class TempleGateReviewAudio : MonoBehaviour
    {
        const float SecondAt = 1.65f, FirstPitch = .85f, SecondPitch = .70f;
        TempleGateReviewMotion motion;
        AudioSource first, second;
        bool armed, ignorePause;
        public bool SecondTriggered { get; private set; }

        public void Prepare(TempleGateReviewMotion gate, Vector3 gatePoint)
        {
            if (motion || !gate) throw new ArgumentException("Fresh gate audio review is required");
            var firstClip = ExternalAudio.Shared("door-open", 1);
            var secondClip = ExternalAudio.Shared("door-open", 0);
            if (!firstClip || !secondClip) throw new InvalidOperationException("Both recorded door cues are required");
            motion = gate;
            first = CreateSource("Review gate left hinge", gatePoint + new Vector3(-1.2f, 1.5f, 0), firstClip, FirstPitch);
            second = CreateSource("Review gate right hinge", gatePoint + new Vector3(1.2f, 1.5f, 0), secondClip, SecondPitch);
        }

        AudioSource CreateSource(string name, Vector3 position, AudioClip clip, float pitch)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.position = position;
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.clip = clip; source.pitch = pitch; source.volume = .65f; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 16; source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
            return source;
        }

        public void StartAt(float progress, bool duringMaskedTransition)
        {
            if (!motion || armed || float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0 || progress > 1)
                throw new InvalidOperationException("Fresh gate audio and valid progress are required");
            armed = true; ignorePause = duringMaskedTransition;
            first.ignoreListenerPause = second.ignoreListenerPause = ignorePause;
            float elapsed = progress * TempleGateReviewMotion.Duration;
            PlayAt(first, elapsed * FirstPitch);
            SecondTriggered = elapsed >= SecondAt;
            if (SecondTriggered) PlayAt(second, (elapsed - SecondAt) * SecondPitch);
        }

        static void PlayAt(AudioSource source, float offset)
        {
            if (offset >= source.clip.length) return;
            source.time = Mathf.Max(0, offset);
            source.Play();
        }

        void Update()
        {
            if (!armed || SecondTriggered || !motion || !motion.Started || !ignorePause && AudioListener.pause) return;
            float elapsed = motion.Progress * TempleGateReviewMotion.Duration;
            if (elapsed < SecondAt) return;
            SecondTriggered = true;
            PlayAt(second, (elapsed - SecondAt) * SecondPitch);
        }

        void OnDestroy()
        {
            if (first) Destroy(first.gameObject);
            if (second) Destroy(second.gameObject);
        }
    }
}
