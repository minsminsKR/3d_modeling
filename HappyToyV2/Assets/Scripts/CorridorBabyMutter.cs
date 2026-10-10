using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class CorridorBabyBehaviour
    {
        public AudioSource MutterSource { get; private set; }
        public EnemyAcoustics MutterAcoustics { get; private set; }
        public int MutterUtterances { get; private set; }
        public bool Muttering => MutterSource && MutterSource.isPlaying;
        public AudioClip[] OwnedMutters { get; private set; }
        GameObject mutterEmitter;
        NavMeshAgent mutterAgent;
        float nextMutter;
        bool cryDucked;
        void ConfigureMutter()
        {
            OwnedMutters = new AudioClip[ExternalAudio.VariantCount("enemy-baby-mutter")];
            if (OwnedMutters.Length == 0) throw new System.InvalidOperationException("Missing Korean Baby mutter cues");
            for (int i = 0; i < OwnedMutters.Length; i++) OwnedMutters[i] = ExternalAudio.Required("enemy-baby-mutter", i);
            mutterAgent = brain.GetComponent<NavMeshAgent>();
            mutterEmitter = new GameObject("Baby owned Korean searching voice");
            mutterEmitter.transform.SetParent(transform, false); mutterEmitter.transform.localPosition = Vector3.up * .9f;
            MutterSource = mutterEmitter.AddComponent<AudioSource>();
            MutterSource.playOnAwake = false; MutterSource.loop = false;
            MutterSource.minDistance = 1.8f; MutterSource.maxDistance = 13; MutterSource.priority = 65;
            MutterAcoustics = EnemyAcoustics.Bind(MutterSource, transform, .62f, 0);
        }
        void DuckCry(bool duck)
        {
            if (cryDucked == duck) return;
            cryDucked = duck;
            if (CrySource) CryAcoustics = EnemyAcoustics.Bind(CrySource, transform, duck ? .17f : .48f, 0);
        }
        void UpdateMutter(GameSession session)
        {
            if (!MutterSource || !session.player || !session.player.eyes) return;
            bool searching = Phase == CorridorBabyMemory.Phase.InvestigatingCry || Phase == CorridorBabyMemory.Phase.WanderingCry;
            bool nearby = Vector3.Distance(session.player.eyes.transform.position, MutterSource.transform.position) < MutterSource.maxDistance;
            if (!searching || !nearby) { StopMutter(); return; }
            // Follow admitted production movement. This voice never gives the
            // brain player coordinates or starts an investigation by itself.
            if (!Muttering && Time.time >= nextMutter && EnemyNavigation.Ready(mutterAgent) &&
                mutterAgent.velocity.sqrMagnitude > .0144f)
            {
                MutterSource.clip = OwnedMutters[MutterUtterances % OwnedMutters.Length];
                MutterSource.Play(); MutterUtterances++;
                nextMutter = Time.time + MutterSource.clip.length + 2.1f;
            }
            DuckCry(Muttering);
            if (Muttering && !MutterSource.mute && MutterSource.volume > 0 && AudioListener.volume > 0 &&
                MutterAcoustics && MutterAcoustics.IsAudible(MutterSource))
                LightExplorationRun.ReportAudibleMovement(session, MutterSource);
        }
        void StopMutter()
        {
            if (MutterSource) MutterSource.Stop();
            nextMutter = 0; DuckCry(false);
        }
        void DisposeMutter()
        {
            if (MutterSource) MutterSource.clip = null;
            if (mutterEmitter) Destroy(mutterEmitter);
            if (OwnedMutters != null) foreach (var clip in OwnedMutters) if (clip) Destroy(clip);
        }
    }
}
