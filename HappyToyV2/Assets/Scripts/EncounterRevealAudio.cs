using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Small owned spatial voices for authored first appearances; no music director or enemy-state polling.</summary>
    [DisallowMultipleComponent]
    public sealed class EncounterRevealAudio : MonoBehaviour
    {
        public enum Cue { FrameStrain, FrameImpact, ToyMechanism, UncatScrape, NurseryWhimper, NurseryContact,
            LanternTicks, LanternRise, MannequinTension, MannequinSettle, CyclopseBreath, WraithGrowth, HwacatJaw }
        readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        GameObject emitter;
        AudioSource source;
        EnemyAcoustics acoustics;
        public int CuesPlayed { get; private set; }
        public Cue LastCue { get; private set; }
        public AudioSource Source => source;
        public AudioClip ActiveClip { get; private set; }
        public int OwnedClipCount => clips.Count;
        public static EncounterRevealAudio Ensure(Transform owner)
        {
            var result = owner.GetComponent<EncounterRevealAudio>();
            return result ? result : owner.gameObject.AddComponent<EncounterRevealAudio>();
        }
        void Awake()
        {
            emitter = new GameObject("Owned first-appearance voice"); emitter.SetActive(false);
            emitter.transform.SetParent(transform, false);
            source = emitter.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 1; source.minDistance = 1.5f; source.maxDistance = 18;
            source.volume = .42f; source.priority = 65;
            acoustics = EnemyAcoustics.Bind(source, emitter.transform, .42f);
            emitter.SetActive(true);
        }
        public void Play(Cue kind, Vector3 worldPosition, string caption = null, float gain = 1)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.StoryStep >= 4 ||
                gameObject.scene != session.gameObject.scene || !StealthRules.Finite(worldPosition.x) ||
                !StealthRules.Finite(worldPosition.y) || !StealthRules.Finite(worldPosition.z) ||
                !StealthRules.Finite(gain) || gain <= 0) return;
            if (!clips.TryGetValue(kind, out var clip)) { clip = CreateClip(kind); clips.Add(kind, clip); }
            // Each authored beat replaces its preceding voice. No accumulated
            // one-shots or moving a still-playing impact to a different location.
            source.Stop(); emitter.transform.position = worldPosition;
            acoustics = EnemyAcoustics.Bind(source, emitter.transform, .42f); source.pitch = 1;
            source.PlayOneShot(clip, Mathf.Clamp01(gain));
            LastCue = kind; ActiveClip = clip; CuesPlayed++;
            if (!string.IsNullOrEmpty(caption) && session.Shell && acoustics.IsAudible(source))
                session.Shell.ShowCaption(caption, Mathf.Clamp(clip.length + 1, 1.8f, 3.5f), 2);
        }
        public void Stop() { if (source) source.Stop(); ActiveClip = null; }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished || session.StoryStep >= 4) Stop();
        }
        void OnDisable() { Stop(); if (emitter) emitter.SetActive(false); }
        void OnEnable() { if (emitter) emitter.SetActive(true); }
        void OnDestroy()
        {
            Stop(); if (emitter) { emitter.SetActive(false); Destroy(emitter); }
            foreach (var clip in clips.Values) if (clip) Destroy(clip);
            clips.Clear();
        }
        static AudioClip RecordedToyMechanism(AudioClip contact)
        {
            // Preserve the original mechanism's 1.15 s / four-click grammar.
            // These are decoded recorded samples; source Resources remain owned
            // by ExternalAudio, and this returned phrase belongs to this voice.
            const float duration = 1.15f;
            var sourceData = new float[contact.samples * contact.channels];
            if (!contact.GetData(sourceData, 0))
                throw new System.InvalidOperationException("Recorded toy contact cannot be decoded");
            int frames = Mathf.CeilToInt(duration * contact.frequency);
            var phrase = new float[frames * contact.channels];
            float[] starts = { 0f, .23f, .51f, .84f };
            float[] gains = { .85f, .65f, .75f, .55f };
            for (int hit = 0; hit < starts.Length; hit++)
            {
                int offset = Mathf.RoundToInt(starts[hit] * contact.frequency) * contact.channels;
                int count = Mathf.Min(sourceData.Length, phrase.Length - offset);
                for (int sample = 0; sample < count; sample++)
                    phrase[offset + sample] += sourceData[sample] * gains[hit];
            }
            float peak = 0;
            foreach (float value in phrase)
            {
                if (!StealthRules.Finite(value))
                    throw new System.InvalidOperationException("Recorded toy contact contains invalid samples");
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float normalization = peak > .58f ? .58f / peak : 1;
            for (int frame = 0; frame < frames; frame++)
            {
                float time = frame / (float)contact.frequency;
                float edge = Mathf.Clamp01(time / .004f) * Mathf.Clamp01((duration - time) / .035f);
                for (int channel = 0; channel < contact.channels; channel++)
                    phrase[frame * contact.channels + channel] *= normalization * edge;
            }
            var result = AudioClip.Create("Recorded toy mechanism / " + contact.name, frames, contact.channels, contact.frequency, false);
            result.SetData(phrase, 0); return result;
        }
        public static AudioClip CreateClip(Cue kind)
        {
            string cue = kind == Cue.FrameStrain ? "frame-strain" :
                kind == Cue.FrameImpact ? "frame-impact" :
                kind == Cue.ToyMechanism || kind == Cue.LanternTicks ? "door-seat" :
                kind == Cue.UncatScrape ? "cabinet-rustle" :
                kind == Cue.NurseryContact ? "step-wet" :
                kind == Cue.NurseryWhimper ? "nursery-whimper" :
                kind == Cue.MannequinTension ? "frame-strain" :
                kind == Cue.MannequinSettle ? "step-wood" :
                kind == Cue.CyclopseBreath ? "cyclopse-roar" :
                kind == Cue.WraithGrowth ? "wraith-growth" :
                kind == Cue.HwacatJaw ? "hwacat-jaw" : "lantern-rise";
            var recorded = ExternalAudio.Required(cue, kind == Cue.MannequinTension ? 1 : 0);
            if (kind != Cue.ToyMechanism) return recorded;
            try { return RecordedToyMechanism(recorded); }
            finally { Object.Destroy(recorded); }
        }
    }
}
