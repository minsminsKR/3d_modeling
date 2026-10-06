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
        static float S(float frequency, float time) => Mathf.Sin(2 * Mathf.PI * frequency * time);
        static float Hit(float time, float start, float decay)
        { float t = time - start; return t < 0 ? 0 : (1 - Mathf.Exp(-t * 1100)) * Mathf.Exp(-t * decay); }
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
            string recordedCue = kind == Cue.FrameStrain ? "frame-strain" :
                kind == Cue.FrameImpact ? "frame-impact" :
                kind == Cue.ToyMechanism || kind == Cue.LanternTicks ? "door-seat" :
                kind == Cue.UncatScrape ? "cabinet-rustle" :
                kind == Cue.NurseryContact ? "step-wet" :
                kind == Cue.MannequinTension ? "frame-strain" :
                kind == Cue.MannequinSettle ? "step-wood" : null;
            // Different authored reveals must not alias the same recorded take.
            // The frame and mannequin share a material family, but use real
            // independent takes. A toy mechanism has its own four-contact phrase,
            // assembled from the recorded latch rather than relabeling one tick.
            int variant = kind == Cue.MannequinTension ? 1 : 0;
            var recorded = ExternalAudio.Owned(recordedCue, variant);
            if (recorded)
            {
                if (kind != Cue.ToyMechanism) return recorded;
                try { return RecordedToyMechanism(recorded); }
                finally { Object.Destroy(recorded); }
            }
            const int rate = 24000;
            float duration = kind == Cue.NurseryWhimper ? 2.7f : kind == Cue.WraithGrowth ? 4.8f :
                kind == Cue.CyclopseBreath ? 1.05f : kind == Cue.ToyMechanism ? 1.15f :
                kind == Cue.FrameStrain ? 1f : kind == Cue.FrameImpact ? .48f : kind == Cue.UncatScrape ? .8f :
                kind == Cue.NurseryContact ? .34f : kind == Cue.LanternTicks ? .65f :
                kind == Cue.LanternRise ? .85f : kind == Cue.MannequinTension || kind == Cue.HwacatJaw ? .65f : .42f;
            var data = new float[Mathf.RoundToInt(rate * duration)];
            var random = new System.Random(7201 + (int)kind * 197); float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate, u = t / duration;
                float n = (float)(random.NextDouble() * 2 - 1); low = Mathf.Lerp(low, n, .08f);
                float v;
                switch (kind)
                {
                    case Cue.FrameStrain:
                        v = (.18f * S(143 + 21 * Mathf.Sin(t * 12), t) + low * .7f) *
                            (Mathf.Exp(-Mathf.Pow((t - .24f) * 12, 2)) + .7f * Mathf.Exp(-Mathf.Pow((t - .72f) * 10, 2))); break;
                    case Cue.FrameImpact:
                        v = .34f * S(86, t) * Hit(t, 0, 13) + n * .28f * Hit(t, 0, 38) + .12f * S(347, t) * Hit(t, .017f, 10); break;
                    case Cue.ToyMechanism:
                        v = (.17f * S(760, t) + .08f * S(1173, t) + .06f * n) *
                            (Hit(t, 0, 45) + Hit(t, .23f, 38) + Hit(t, .51f, 48) + Hit(t, .84f, 32)) +
                            .065f * S(287 + 44 * u * u, t) * Mathf.Sin(Mathf.PI * u); break;
                    case Cue.UncatScrape:
                        v = (.24f * (n - low) * (.2f + .8f * Mathf.Abs(S(23, t))) + .15f * low + .055f * S(131, t)) * Mathf.Sin(Mathf.PI * u); break;
                    case Cue.NurseryWhimper:
                        float breath = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2) * (.6f + .4f * Mathf.Sin(t * 6));
                        v = breath * (.11f * S(208 + 22 * Mathf.Sin(t * 3), t) + .055f * S(521, t) + .035f * S(851, t) + low * .22f); break;
                    case Cue.NurseryContact:
                        v = .32f * low * Hit(t, 0, 12) + .18f * n * Hit(t, .018f, 45) + .075f * S(63 + 90 * u, t) * Hit(t, 0, 15); break;
                    case Cue.LanternTicks:
                        v = (.16f * S(1431, t) + .095f * S(2067, t) + .07f * n) * (Hit(t, .02f, 28) + .75f * Hit(t, .38f, 22)); break;
                    case Cue.LanternRise:
                        v = Mathf.Sin(Mathf.PI * u) * (.1f * S(341 + 105 * u, t) + .06f * S(699 + 38 * u, t) + low * .25f); break;
                    case Cue.MannequinTension:
                        v = Mathf.Sin(Mathf.PI * u) * (.13f * S(116 + 8 * Mathf.Sin(t * 19), t) + .075f * S(237, t) + low * .4f); break;
                    case Cue.MannequinSettle:
                        v = (.17f * S(181, t) + .15f * low) * Hit(t, 0, 15) + .07f * n * Hit(t, .11f, 65); break;
                    case Cue.CyclopseBreath:
                        v = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.4f) * (.45f * low + .08f * S(54, t) + .025f * (n - low)); break;
                    case Cue.HwacatJaw:
                        v = (.21f * low + .14f * S(72, t)) * (Hit(t, .015f, 17) + .7f * Hit(t, .18f, 13)) +
                            .11f * (n - low) * Hit(t, .11f, 55) + .045f * S(293, t) * Hit(t, .03f, 8); break;
                    default: // WraithGrowth: stretched metal and airy body, not a pitched version of another cue.
                        v = Mathf.Sin(Mathf.PI * u) * (.085f * S(91 + 112 * u * u, t) + .055f * S(317 + 37 * u, t) +
                            low * .23f * (.7f + .3f * S(3.7f, t))); break;
                }
                float edge = Mathf.Clamp01(t / .009f) * Mathf.Clamp01((duration - t) / .035f);
                data[i] = Mathf.Clamp(v * edge, -.58f, .58f);
            }
            var clip = AudioClip.Create("First appearance " + kind, data.Length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }
    }
}
