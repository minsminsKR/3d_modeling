using UnityEngine;

namespace HappyToy.V2
{
    // Installed only on the corridor's Baby. The school nursery's reveal and AI
    // stay separate. One owned spatial recording follows this actor while crying.
    [DisallowMultipleComponent]
    public sealed class CorridorBabyBehaviour : MonoBehaviour
    {
        public const float CalmSpeed = .65f, LoudFootstepRadius = 10f;
        public readonly CorridorBabyMemory Memory = new CorridorBabyMemory();
        public CorridorBabyMemory.Phase Phase => Memory.Current;
        public AudioSource CrySource { get; private set; }
        public AudioClip OwnedCry { get; private set; }
        public EnemyAcoustics CryAcoustics { get; private set; }
        public bool WaitingForSound => Memory.Waiting;
        public bool HasChased => Memory.HasChased;
        StalkerBrain brain;
        GameObject emitter;
        bool started;
        float nextHeardReport;
        public void Configure(StalkerBrain owner)
        {
            if (brain == owner) return;
            brain = owner; Memory.Restore(null);
            OwnedCry = ExternalAudio.Required("enemy-baby-cry");
            emitter = new GameObject("Baby owned recorded crying voice");
            emitter.transform.SetParent(transform, false); emitter.transform.localPosition = Vector3.up * .85f;
            CrySource = emitter.AddComponent<AudioSource>(); CrySource.playOnAwake = false; CrySource.loop = true;
            CrySource.clip = OwnedCry; CrySource.minDistance = 1.5f; CrySource.maxDistance = 20;
            CrySource.priority = 80;
            CryAcoustics = EnemyAcoustics.Bind(CrySource, transform, .48f, 0);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!brain || !brain.isActiveAndEnabled || !session || !session.CorridorMode ||
                brain.gameObject.scene != session.gameObject.scene || brain.player != session.player ||
                !session.player || !session.player.eyes || session.Finished || session.EncountersResolved)
            { StopVoice(); return; }
            // Explicit listener pause freezes native playback. Do not stop/restart
            // the cry at menu entry or undo a deliberately muted/stopped source.
            if (!session.InputAllowed || !CrySource) return;
            if (!started) { CrySource.Play(); started = true; }
            float distance = Vector3.Distance(session.player.eyes.transform.position, CrySource.transform.position);
            float heardGain = CrySource.volume * Mathf.InverseLerp(CrySource.maxDistance, CrySource.minDistance, distance) * Mathf.Clamp01(AudioListener.volume);
            if (Time.time >= nextHeardReport && CrySource.isActiveAndEnabled && CrySource.isPlaying && !CrySource.mute &&
                !AudioListener.pause && heardGain > .015f && CryAcoustics && CryAcoustics.IsAudible(CrySource))
            {
                nextHeardReport = Time.time + .25f;
                LightExplorationRun.ReportAudibleMovement(session, CrySource);
            }
        }
        public void StopVoice() { if (CrySource) CrySource.Stop(); started = false; nextHeardReport = 0; }
        void OnDisable() { StopVoice(); }
        void OnDestroy()
        {
            StopVoice(); if (CrySource) CrySource.clip = null;
            if (emitter) Destroy(emitter); if (OwnedCry) Destroy(OwnedCry);
        }
    }
}
