using UnityEngine;

namespace HappyToy.V2
{
    // Player-local subjective pressure. The existing perception owner admits
    // genuine recognition and witnessed pursuit; this voice never inspects AI
    // positions or creates knowledge, warnings or footsteps of its own.
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class DetectionPursuitAudio : MonoBehaviour
    {
        public const float ImpactMaximumGain = .34f, SoftImpactMaximumGain = .12f;
        public const float PursuitMaximumGain = .22f, SoftPursuitMaximumGain = .075f;
        public const float FadeInSeconds = .25f, FadeOutSeconds = 1.3f;
        public AudioSource ImpactSource { get; private set; }
        public AudioSource PursuitSource { get; private set; }
        public AudioClip[] OwnedImpactClips { get; private set; }
        public AudioClip OwnedPursuitClip { get; private set; }
        public float LoopGain => PursuitSource ? PursuitSource.volume : 0;
        public float Envelope => envelope;
        public int ImpactsPlayed { get; private set; }
        public int LoopsStarted { get; private set; }
        DetectionFeedback detection;
        PlayerMotor player;
        GameObject emitter;
        int lastRecognition;
        float envelope;
        GameSession CurrentSession
        {
            get
            {
                var session = GameSession.Current;
                return isActiveAndEnabled && detection && detection.isActiveAndEnabled && player && session &&
                    session.player == player && session.gameObject.scene == gameObject.scene && !session.Finished &&
                    session.StoryStep < 4 ? session : null;
            }
        }
        void Awake()
        {
            detection = GetComponent<DetectionFeedback>(); player = GetComponent<PlayerMotor>();
            if (!detection || !player) throw new System.InvalidOperationException("Pursuit audio requires the real player's perception owner");
            int count = ExternalAudio.VariantCount("detection-impact");
            if (count != 2) throw new System.InvalidOperationException("Two authored recognition impact variants are required");
            OwnedImpactClips = new AudioClip[count];
            for (int i = 0; i < count; i++) OwnedImpactClips[i] = ExternalAudio.Required("detection-impact", i);
            OwnedPursuitClip = ExternalAudio.Required("pursuit-loop");
            emitter = new GameObject("Owned witnessed recognition and pursuit music"); emitter.SetActive(false);
            emitter.transform.SetParent(transform, false);
            ImpactSource = Source(false, 22); PursuitSource = Source(true, 70); PursuitSource.clip = OwnedPursuitClip;
            emitter.SetActive(true); lastRecognition = detection.CuesPlayed;
        }
        AudioSource Source(bool loop, int priority)
        {
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.spatialBlend = 0; source.panStereo = 0; source.dopplerLevel = 0; source.priority = priority;
            source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            return source;
        }
        public void OnRecognition()
        {
            var session = CurrentSession;
            if (!session || !session.InputAllowed || AudioListener.pause || detection.Remaining <= 0 ||
                detection.CuesPlayed <= lastRecognition) return;
            lastRecognition = detection.CuesPlayed;
            ImpactSource.volume = detection.Softened ? SoftImpactMaximumGain : ImpactMaximumGain;
            ImpactSource.pitch = 1; ImpactSource.panStereo = 0;
            ImpactSource.PlayOneShot(OwnedImpactClips[ImpactsPlayed % OwnedImpactClips.Length]); ImpactsPlayed++;
        }
        void Update()
        {
            var session = CurrentSession;
            if (!session) { ResetPresentation(); return; }
            ApplyGains();
            // Listener pause owns the natural clip cursors; menu changes may
            // reduce comfort gains without advancing the envelope or restarting.
            if (!session.InputAllowed || AudioListener.pause) return;
            float target = detection.WitnessedPressure > 0 ? Mathf.Clamp01(detection.ChaseStrength) : 0;
            float rate = target > envelope ? 1 / FadeInSeconds : 1 / FadeOutSeconds;
            envelope = Mathf.MoveTowards(envelope, target, Time.deltaTime * rate);
            ApplyGains();
            if (envelope > .0001f)
            {
                if (!PursuitSource.isPlaying) { PursuitSource.Play(); LoopsStarted++; }
            }
            else if (PursuitSource.isPlaying) PursuitSource.Stop();
        }
        void ApplyGains()
        {
            bool soft = detection && detection.Softened;
            if (ImpactSource) ImpactSource.volume = soft ? SoftImpactMaximumGain : ImpactMaximumGain;
            if (PursuitSource) PursuitSource.volume = envelope * (soft ? SoftPursuitMaximumGain : PursuitMaximumGain);
        }
        public void ResetPresentation()
        {
            envelope = 0;
            if (ImpactSource) ImpactSource.Stop();
            if (PursuitSource) { PursuitSource.Stop(); PursuitSource.volume = 0; }
            if (detection) lastRecognition = detection.CuesPlayed;
        }
        void OnDisable() => ResetPresentation();
        void OnDestroy()
        {
            ResetPresentation();
            if (PursuitSource) PursuitSource.clip = null;
            if (emitter) Destroy(emitter);
            if (OwnedImpactClips != null) foreach (var clip in OwnedImpactClips) if (clip) Destroy(clip);
            if (OwnedPursuitClip) Destroy(OwnedPursuitClip);
        }
    }
}
