using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // One filter per physical emitter, with independently owned source gains.
    // Geometry changes transmission, never enemy knowledge or player HUD state.
    [DisallowMultipleComponent]
    public sealed class EnemyAcoustics : MonoBehaviour
    {
        sealed class Voice { public AudioSource source; public float gain; }
        readonly List<Voice> voices = new List<Voice>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        Transform actor;
        AudioLowPassFilter filter;
        float nextTrace, height = .8f;
        bool initialized;
        public bool Occluded { get; private set; }
        public bool FloorOccluded { get; private set; }
        public bool InAudibleRange { get; private set; }
        public float Gain { get; private set; } = 1;
        public float CutoffFrequency => filter ? filter.cutoffFrequency : 9000;
        public int TraceCount { get; private set; }
        public int BoundSourceCount => voices.Count;

        public static EnemyAcoustics Bind(AudioSource source, Transform actorRoot, float baseGain, float sourceHeight = -1)
        {
            var acoustics = source.GetComponent<EnemyAcoustics>();
            if (!acoustics) acoustics = source.gameObject.AddComponent<EnemyAcoustics>();
            acoustics.actor = actorRoot ? actorRoot : source.transform;
            var agent = acoustics.actor.GetComponent<NavMeshAgent>();
            acoustics.height = agent ? Mathf.Clamp(agent.height * .5f, .4f, 1.3f) : .8f;
            if (sourceHeight >= 0 && StealthRules.Finite(sourceHeight)) acoustics.height = Mathf.Clamp(sourceHeight, 0, 2);
            if (!acoustics.filter)
            {
                acoustics.filter = source.GetComponent<AudioLowPassFilter>();
                if (!acoustics.filter) acoustics.filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                acoustics.filter.lowpassResonanceQ = 1;
            }
            acoustics.voices.RemoveAll(item => !item.source);
            Voice voice = acoustics.voices.Find(item => item.source == source);
            if (voice == null) { voice = new Voice { source = source }; acoustics.voices.Add(voice); }
            voice.gain = Mathf.Clamp01(baseGain);
            source.playOnAwake = false; source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
            source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            source.volume = voice.gain;
            acoustics.Refresh(true); return acoustics;
        }
        void Trace(PlayerMotor player)
        {
            TraceCount++; Occluded = FloorOccluded = false;
            if (!actor || !player || !player.eyes) return;
            // The owner excludes self geometry; the real voice supplies the ray origin.
            // Sliding doors and displaced reveal emitters must not trace from an old root.
            Vector3 origin = transform.position + Vector3.up * height;
            Vector3 delta = player.eyes.transform.position - origin;
            float length = delta.magnitude;
            if (length > .02f)
            {
                int count = Physics.RaycastNonAlloc(origin, delta / length, hits, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                // An unordered full buffer may have omitted a wall behind self hits.
                Occluded = count == hits.Length;
                for (int i = 0; i < count && !Occluded; i++)
                {
                    var collider = hits[i].collider;
                    if (!collider || collider.isTrigger || collider.transform.IsChildOf(actor) ||
                        collider.transform.IsChildOf(player.transform) || collider.GetComponentInParent<PlayerMotor>() == player) continue;
                    Occluded = true;
                }
            }
            FloorOccluded = Occluded && Mathf.Abs(actor.position.y - player.transform.position.y) > EnemyNavigation.FloorTolerance;
        }
        void Refresh(bool immediate)
        {
            var session = GameSession.Current;
            var player = session ? session.player : null;
            if (!filter || !player || !player.eyes) return;
            if (immediate || Time.time >= nextTrace) { Trace(player); nextTrace = Time.time + .2f; }
            bool softened = session.Shell && session.Shell.ReducedMotion;
            float targetGain = (FloorOccluded ? .14f : Occluded ? .38f : 1) * (softened ? .8f : 1);
            float targetCutoff = FloorOccluded ? 650 : Occluded ? 1200 : 9000;
            bool snap = immediate || !initialized;
            Gain = snap ? targetGain : Mathf.MoveTowards(Gain, targetGain, Time.deltaTime * 3);
            filter.cutoffFrequency = snap ? targetCutoff : Mathf.MoveTowards(filter.cutoffFrequency, targetCutoff, Time.deltaTime * 18000);
            initialized = true; InAudibleRange = false;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var voice = voices[i];
                if (!voice.source) { voices.RemoveAt(i); continue; }
                bool inRange = Vector3.Distance(player.eyes.transform.position, voice.source.transform.position) < voice.source.maxDistance;
                InAudibleRange |= inRange;
                // Linear rolloff reaches zero; also forbid smoothed-gain range leaks.
                voice.source.volume = inRange ? voice.gain * Gain : 0;
            }
        }
        // Physical audibility for existing event captions, never a proximity notice.
        // Master mute is deliberately excluded: captions remain an accessibility
        // alternative when the player chooses silence. The shell owns subtitle settings.
        public bool IsAudible(AudioSource source)
        {
            var session = GameSession.Current;
            if (!source || !source.isActiveAndEnabled || !isActiveAndEnabled || !session || !session.InputAllowed ||
                !session.player || !session.player.eyes || AudioListener.pause) return false;
            float distance = Vector3.Distance(session.player.eyes.transform.position, source.transform.position);
            float attenuation = Mathf.InverseLerp(source.maxDistance, source.minDistance, distance);
            return source.volume * attenuation > .012f;
        }
        void LateUpdate()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) return;
            Refresh(false);
        }
        void OnEnable() { nextTrace = 0; initialized = false; }
        void OnDisable() { foreach (var voice in voices) if (voice.source) voice.source.Stop(); }
    }
}
