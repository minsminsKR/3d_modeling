using UnityEngine;

namespace HappyToy.V2
{
    // Player-local memory of witnessed recognition and actually audible cues.
    // Continued fear is admitted only by DetectionFeedback's witnessed pursuers.
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class PerceivedTension : MonoBehaviour
    {
        public const float RecognitionStrength = .85f, ContactCeiling = .35f, AttackCeiling = .95f;
        public const float RisePerSecond = 3.5f, DecayPerSecond = .16f, DuckSeconds = .24f;
        public const float PulseMaximumVolume = .095f, AirMaximumVolume = .035f;
        public const float InterferenceMaximumVolume = .075f;
        struct PendingCue
        {
            public AudioSource source;
            public EnemyAcoustics acoustics;
            public bool attack;
            public float scale;
            public int frame;
        }
        readonly PendingCue[] pending = new PendingCue[16];
        int pendingCount;
        PlayerMotor player;
        GameObject emitter;
        float recognitionCooldown, contactCooldown, attackCooldown, duckEnvelope = 1;
        bool bedRunning, interferenceRunning;
        public float Stress { get; private set; }
        public float TargetStress { get; private set; }
        public float AmbienceGain => CurrentSession ? Mathf.Lerp(1, .55f, Stress) : 1;
        public int RecognitionEvents { get; private set; }
        public int ContactEvents { get; private set; }
        public int AttackEvents { get; private set; }
        public int PerceivedEvents => RecognitionEvents + ContactEvents + AttackEvents;
        public float LastPerceivedStrength { get; private set; }
        public float LastStimulusAge { get; private set; }
        public float HoldRemaining { get; private set; }
        public float DuckRemaining { get; private set; }
        public float BedGain { get; private set; }
        public float InterferenceGain { get; private set; }
        public AudioSource PulseSource { get; private set; }
        public AudioSource AirSource { get; private set; }
        public AudioSource InterferenceSource { get; private set; }
        public AudioClip PulseClip { get; private set; }
        public AudioClip AirClip { get; private set; }
        public AudioClip InterferenceClip { get; private set; }
        GameSession CurrentSession
        {
            get
            {
                var session = GameSession.Current;
                return isActiveAndEnabled && player && session && session.player == player &&
                    session.gameObject.scene == gameObject.scene && !session.Finished && session.StoryStep < 4 ? session : null;
            }
        }
        static PerceivedTension Owner(GameSession session)
        {
            if (!session || session != GameSession.Current || !session.InputAllowed || session.StoryStep >= 4 ||
                !session.player || session.player.gameObject.scene != session.gameObject.scene) return null;
            var owner = session.player.GetComponent<PerceivedTension>();
            if (!owner) owner = session.player.gameObject.AddComponent<PerceivedTension>();
            return owner.isActiveAndEnabled ? owner : null;
        }
        void Awake()
        {
            player = GetComponent<PlayerMotor>();
            emitter = new GameObject("Perceived tension soundscape"); emitter.SetActive(false);
            emitter.transform.SetParent(transform, false);
            PulseClip = MakePulse(); AirClip = MakeAir(); InterferenceClip = MakeInterference();
            PulseSource = Source(PulseClip, 170); AirSource = Source(AirClip, 180);
            InterferenceSource = Source(InterferenceClip, 190);
            emitter.SetActive(true);
        }
        AudioSource Source(AudioClip clip, int priority)
        {
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.clip = clip;
            source.spatialBlend = 0; source.volume = 0; source.priority = priority; source.dopplerLevel = 0;
            source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            return source;
        }
        // Called only after DetectionFeedback's existing true-sight validation.
        public static void ReportRecognition(GameSession session)
        {
            var owner = Owner(session);
            if (!owner || owner.recognitionCooldown > 0) return;
            owner.recognitionCooldown = .8f; owner.RecognitionEvents++;
            owner.Accept(RecognitionStrength, .8f);
            owner.Duck();
        }
        // Call immediately after the production PlayOneShot; never from Update's AI state.
        // Admission is delayed one frame so acoustics and the source's native playback
        // state have settled. Nothing rejected below changes memory, duck or cooldowns.
        public static void ReportSound(GameSession session, AudioSource source, EnemyAcoustics acoustics,
            bool attack, float volumeScale = 1)
        {
            if (!Eligible(session, source, acoustics, volumeScale)) return;
            var owner = Owner(session);
            if (!owner || owner.pendingCount >= owner.pending.Length) return;
            owner.pending[owner.pendingCount++] = new PendingCue {
                source = source, acoustics = acoustics, attack = attack,
                scale = volumeScale, frame = Time.frameCount
            };
        }
        static bool Eligible(GameSession session, AudioSource source, EnemyAcoustics acoustics, float scale)
        {
            return session && session == GameSession.Current && session.InputAllowed && session.StoryStep < 4 &&
                session.player && session.player.eyes && source && source.isActiveAndEnabled && !source.mute &&
                source.gameObject.scene == session.gameObject.scene && acoustics && acoustics.isActiveAndEnabled &&
                acoustics.gameObject == source.gameObject && !AudioListener.pause && AudioListener.volume > 0 &&
                StealthRules.Finite(scale) && scale > 0;
        }
        void Hear(PendingCue cue, GameSession session)
        {
            if (!Eligible(session, cue.source, cue.acoustics, cue.scale) || !cue.source.isPlaying ||
                !cue.acoustics.IsAudible(cue.source)) return;
            float distance = Vector3.Distance(session.player.eyes.transform.position, cue.source.transform.position);
            float attenuation = Mathf.InverseLerp(cue.source.maxDistance, cue.source.minDistance, distance);
            float heardGain = cue.source.volume * attenuation * Mathf.Clamp01(AudioListener.volume) * cue.scale;
            if (!StealthRules.Finite(heardGain) || heardGain <= .015f) return;
            float strength;
            if (cue.attack)
            {
                if (attackCooldown > 0) return;
                strength = Mathf.Lerp(.12f, AttackCeiling, Mathf.Clamp01(heardGain / .7f));
                attackCooldown = .35f; AttackEvents++;
            }
            else
            {
                if (contactCooldown > 0) return;
                strength = Mathf.Min(ContactCeiling, heardGain * .6f);
                contactCooldown = .22f; ContactEvents++;
            }
            Accept(strength, cue.attack ? .55f : .25f); Duck();
        }
        void Accept(float strength, float hold)
        {
            // A quieter later contact cannot keep an earlier recognition peak alive.
            if (strength >= TargetStress) HoldRemaining = Mathf.Max(HoldRemaining, hold);
            TargetStress = Mathf.Max(TargetStress, strength);
            LastPerceivedStrength = strength; LastStimulusAge = 0;
        }
        void Duck() { DuckRemaining = DuckSeconds; duckEnvelope = .18f; }
        void Update()
        {
            var session = CurrentSession;
            if (!session) { Clear(); return; }
            // Comfort gain changes apply in menus without advancing the sound memory.
            if (!session.InputAllowed) { DiscardPending(); ApplyMix(session, false); return; }
            float dt = Time.deltaTime;
            LastStimulusAge += dt;
            recognitionCooldown = Mathf.Max(0, recognitionCooldown - dt);
            contactCooldown = Mathf.Max(0, contactCooldown - dt);
            attackCooldown = Mathf.Max(0, attackCooldown - dt);
            HoldRemaining = Mathf.Max(0, HoldRemaining - dt);
            if (HoldRemaining <= 0) TargetStress = Mathf.MoveTowards(TargetStress, 0, DecayPerSecond * dt);
            var detection = player.GetComponent<DetectionFeedback>();
            if (detection && detection.ChaseStrength > .15f)
                TargetStress = Mathf.Max(TargetStress, .82f * detection.ChaseStrength);
            Stress = Mathf.MoveTowards(Stress, TargetStress, (TargetStress > Stress ? RisePerSecond : DecayPerSecond) * dt);
            DuckRemaining = Mathf.Max(0, DuckRemaining - dt);
            if (DuckRemaining <= 0) duckEnvelope = Mathf.MoveTowards(duckEnvelope, 1, dt * 3);
            ApplyMix(session, true);
        }
        void LateUpdate()
        {
            var session = CurrentSession;
            if (!session || !session.InputAllowed) { DiscardPending(); return; }
            int retained = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                var cue = pending[i];
                if (cue.frame >= Time.frameCount) { pending[retained++] = cue; continue; }
                Hear(cue, session);
            }
            for (int i = retained; i < pendingCount; i++) pending[i] = default;
            pendingCount = retained;
            ApplyMix(session, true);
        }
        void ApplyMix(GameSession session, bool mayStart)
        {
            bool softened = session.Shell && session.Shell.ReducedMotion;
            float comfort = softened ? .45f : 1;
            BedGain = Stress * duckEnvelope * comfort;
            if (PulseSource)
            {
                PulseSource.volume = PulseMaximumVolume * BedGain;
                // Stress already rises/settles continuously. Only this subjective
                // pulse tightens; the real enemy contact cues and air stay unretuned.
                PulseSource.pitch = softened ? 1 : Mathf.Lerp(.9f, 1.2f, Stress);
            }
            if (AirSource) AirSource.volume = AirMaximumVolume * BedGain;
            // Audible feet can raise anticipation, but never invent visual/static
            // discovery. This texture belongs only to an actually seen pursuit.
            var detection = player.GetComponent<DetectionFeedback>();
            InterferenceGain = detection ? detection.WitnessedPressure * duckEnvelope * comfort : 0;
            if (InterferenceSource) InterferenceSource.volume = InterferenceMaximumVolume * InterferenceGain;
            if (InterferenceGain <= .0001f)
            {
                if (InterferenceSource) InterferenceSource.Stop();
                interferenceRunning = false;
            }
            else if (mayStart && !interferenceRunning)
            {
                interferenceRunning = true;
                if (InterferenceSource && InterferenceSource.isActiveAndEnabled && !InterferenceSource.mute) InterferenceSource.Play();
            }
            if (Stress <= .0001f)
            {
                if (PulseSource) PulseSource.Stop();
                if (AirSource) AirSource.Stop();
                bedRunning = false; return;
            }
            // Start once per tension episode. Do not undo explicit source Stop/mute
            // from an isolation fixture, or restart the loop after listener pause.
            if (!mayStart || bedRunning) return;
            bedRunning = true;
            if (PulseSource && PulseSource.isActiveAndEnabled && !PulseSource.mute) PulseSource.Play();
            if (AirSource && AirSource.isActiveAndEnabled && !AirSource.mute) AirSource.Play();
        }
        void Clear()
        {
            Stress = TargetStress = HoldRemaining = DuckRemaining = BedGain = InterferenceGain = 0;
            LastPerceivedStrength = LastStimulusAge = 0;
            recognitionCooldown = contactCooldown = attackCooldown = 0;
            duckEnvelope = 1; bedRunning = interferenceRunning = false;
            DiscardPending();
            if (PulseSource) { PulseSource.Stop(); PulseSource.volume = 0; }
            if (AirSource) { AirSource.Stop(); AirSource.volume = 0; }
            if (InterferenceSource) { InterferenceSource.Stop(); InterferenceSource.volume = 0; }
        }
        void DiscardPending()
        {
            for (int i = 0; i < pendingCount; i++) pending[i] = default;
            pendingCount = 0;
        }
        public static float HeartbeatEnvelope(float clock)
        {
            float t = Mathf.Repeat(clock, .72f), envelope = 0;
            for (int beat = 0; beat < 2; beat++)
            {
                float q = t - (beat == 0 ? .035f : .20f);
                if (q >= 0) envelope += (beat == 0 ? 1 : .55f) * Mathf.Clamp01(q / .012f) * Mathf.Exp(-22 * q);
            }
            return Mathf.Clamp01(envelope);
        }
        static AudioClip MakePulse()
        {
            const int rate = 24000; const float seconds = .72f;
            var samples = new float[Mathf.RoundToInt(rate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, value = 0;
                for (int beat = 0; beat < 2; beat++)
                {
                    float q = t - (beat == 0 ? .035f : .20f);
                    if (q < 0) continue;
                    value += (beat == 0 ? 1 : .55f) * Mathf.Clamp01(q / .012f) * Mathf.Exp(-18 * q) *
                        (.29f * Mathf.Sin(2 * Mathf.PI * (56 * q - 10 * q * q)) +
                         .075f * Mathf.Sin(2 * Mathf.PI * 89 * q) + .028f * Mathf.Sin(2 * Mathf.PI * 126 * q));
                }
                samples[i] = .34f * (float)System.Math.Tanh(value / .34f) * Mathf.Clamp01(Mathf.Min(t, seconds - t) / .025f);
            }
            var clip = AudioClip.Create("Original close double heartbeat", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        static AudioClip MakeAir()
        {
            const int rate = 24000; const float seconds = 2.88f;
            var samples = new float[Mathf.RoundToInt(rate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float seam = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(t, seconds - t) / .12f));
                // A restrained beating low chord adds body without static masking
                // real footsteps. Whole-cycle partials and a fade make a clean seam.
                float body = .13f * Mathf.Sin(2 * Mathf.PI * (161 / seconds) * t) +
                    .055f * Mathf.Sin(2 * Mathf.PI * (163 / seconds) * t) +
                    .024f * Mathf.Sin(2 * Mathf.PI * (242 / seconds) * t);
                samples[i] = body * (.8f + .2f * Mathf.Sin(2 * Mathf.PI * t / seconds)) * seam;
            }
            var clip = AudioClip.Create("Original low pursuit resonance", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        static AudioClip MakeInterference()
        {
            const int rate = 48000; const float seconds = 3.6f;
            var samples = new float[Mathf.RoundToInt(rate * seconds)];
            var random = new System.Random(27571);
            float fast = 0, slow = 0;
            float highCut = 1 - Mathf.Exp(-2 * Mathf.PI * 1700 / rate);
            float lowCut = 1 - Mathf.Exp(-2 * Mathf.PI * 210 / rate);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float white = (float)random.NextDouble() * 2 - 1;
                fast += (white - fast) * highCut; slow += (fast - slow) * lowCut;
                float breath = .60f + .24f * Mathf.Sin(2 * Mathf.PI * 2 * t / seconds) +
                    .10f * Mathf.Sin(2 * Mathf.PI * 5 * t / seconds + .7f);
                float seam = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(t, seconds - t) / .09f));
                // Narrow dark hiss and resonant grains rather than loud broadband
                // white noise. Physical steps stay louder and retain their timing.
                float texture = .20f * (float)System.Math.Tanh((fast - slow) * 2.8f) * breath;
                samples[i] = texture * seam;
            }
            var clip = AudioClip.Create("Original witnessed pursuit bandpass interference", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            Clear();
            if (emitter) { emitter.SetActive(false); Destroy(emitter); }
            if (PulseClip) Destroy(PulseClip);
            if (AirClip) Destroy(AirClip);
            if (InterferenceClip) Destroy(InterferenceClip);
        }
    }
}
