using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // AI owners retain state and gate their clocks. This component owns native
    // navigation, animation and audio snapshots, including harmless reveal actors.
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class EnemyTimeStop : MonoBehaviour
    {
        sealed class Frozen
        {
            public Transform root;
            public Behaviour owner;
            public NavMeshAgent agent;
            public Vector3 point;
            public Quaternion facing;
            public bool stopped, rotation, ownerEnabled;
            public double capturedAt;
            public readonly List<(Animation animation, bool enabled)> animations = new List<(Animation, bool)>();
            public readonly List<(Animator animator, bool enabled)> animators = new List<(Animator, bool)>();
            public readonly List<(Transform part, Vector3 position, Quaternion rotation, Vector3 scale)> pose =
                new List<(Transform, Vector3, Quaternion, Vector3)>();
        }

        readonly Dictionary<Transform, Frozen> frozen = new Dictionary<Transform, Frozen>();
        readonly HashSet<AudioSource> pausedSounds = new HashSet<AudioSource>();
        GameSession session;
        Volume volume;
        VolumeProfile profile;
        bool active;
        public int FrozenActorCount => frozen.Count;
        public static bool Active => GameSession.Current && GameSession.Current.player &&
            GameSession.Current.player.Rituals && GameSession.Current.player.Rituals.Burning;

        // Unity drives nested iterators independently. Flatten them here so a
        // reveal cannot cross a phase/activation boundary during the time stop.
        public static IEnumerator RunClock(IEnumerator routine)
        {
            var pending = new Stack<IEnumerator>();
            pending.Push(routine);
            try
            {
                while (pending.Count > 0)
                {
                    if (Active) { yield return null; continue; }
                    var current = pending.Peek();
                    if (!current.MoveNext())
                    {
                        (pending.Pop() as System.IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) pending.Push(nested);
                    else yield return current.Current;
                }
            }
            finally
            {
                while (pending.Count > 0) (pending.Pop() as System.IDisposable)?.Dispose();
            }
        }

        public void Begin()
        {
            if (active) return;
            session = GameSession.Current;
            if (!session) return;
            active = true;
            var root = new GameObject("Talisman monochrome world");
            root.transform.SetParent(transform, false);
            volume = root.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 1000;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            profile.Add<ColorAdjustments>(true).saturation.Override(-100);
            CaptureActors();
            Shader.SetGlobalFloat("_RitualMonochrome", 1);
        }

        bool InRun(Component actor) => actor && session && actor.gameObject.scene == session.gameObject.scene;
        void CaptureActors()
        {
            // Disabled brains/agents remain visible during authored reveals.
            // FindObjectsByType includes disabled components on active objects.
            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (!InRun(agent)) continue;
                Behaviour owner = (Behaviour)agent.GetComponent<StalkerBrain>() ??
                    (Behaviour)agent.GetComponent<LanternMaskEncounter>() ?? agent.GetComponent<WeepingAngelEncounter>();
                if (owner) Capture(agent.transform, owner, agent);
            }
            foreach (var reveal in Object.FindObjectsByType<V1HwacatEvent>(FindObjectsSortMode.None))
            {
                if (!InRun(reveal)) continue;
                if (reveal.normal && reveal.normal.activeInHierarchy) Capture(reveal.normal.transform, reveal, null);
                CaptureRevealVoice(reveal);
            }
            foreach (var reveal in Object.FindObjectsByType<AnnexEncounter>(FindObjectsSortMode.None))
            {
                if (!InRun(reveal)) continue;
                foreach (var source in reveal.GetComponents<AudioSource>()) Pause(source);
                CaptureRevealVoice(reveal);
            }
            foreach (var reveal in Object.FindObjectsByType<V1CyclopseIntro>(FindObjectsSortMode.None))
                if (InRun(reveal)) CaptureRevealVoice(reveal);
            foreach (var reveal in Object.FindObjectsByType<UncatAnnexEvent>(FindObjectsSortMode.None))
                if (InRun(reveal)) CaptureRevealVoice(reveal);
        }
        void CaptureRevealVoice(Component owner)
        {
            var voice = owner.GetComponent<EncounterRevealAudio>();
            if (voice) Pause(voice.Source);
        }
        void Capture(Transform root, Behaviour owner, NavMeshAgent agent)
        {
            if (!root || !root.gameObject.activeInHierarchy) return;
            if (!frozen.TryGetValue(root, out var state))
            {
                state = new Frozen { root = root, owner = owner, agent = agent,
                    point = root.position, facing = root.rotation, capturedAt = Time.timeAsDouble,
                    ownerEnabled = owner.enabled, stopped = EnemyNavigation.Ready(agent) && agent.isStopped,
                    rotation = agent && agent.updateRotation };
                foreach (var animation in root.GetComponentsInChildren<Animation>(true))
                    state.animations.Add((animation, animation.enabled));
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    state.animators.Add((animator, animator.enabled));
                foreach (var part in root.GetComponentsInChildren<Transform>(true))
                    if (part != root) state.pose.Add((part, part.localPosition, part.localRotation, part.localScale));
                frozen.Add(root, state);
                Hold(state);
            }
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true)) Pause(source);
        }
        void Pause(AudioSource source)
        {
            if (!source || pausedSounds.Contains(source) || !source.isPlaying) return;
            source.Pause(); pausedSounds.Add(source);
        }
        static void Hold(Frozen state)
        {
            if (!state.root || !state.root.gameObject.activeInHierarchy) return;
            if (EnemyNavigation.Ready(state.agent))
            {
                state.agent.isStopped = true; state.agent.updateRotation = false;
                state.agent.velocity = Vector3.zero; state.agent.nextPosition = state.point;
            }
            state.root.SetPositionAndRotation(state.point, state.facing);
            // Native Animation crossfades continue blending with clip.speed=0.
            // Disabling the component freezes that blend as well as clip time.
            foreach (var entry in state.animations) if (entry.animation) entry.animation.enabled = false;
            foreach (var entry in state.animators) if (entry.animator) entry.animator.enabled = false;
            foreach (var entry in state.pose)
                if (entry.part)
                {
                    entry.part.SetLocalPositionAndRotation(entry.position, entry.rotation);
                    entry.part.localScale = entry.scale;
                }
        }
        void LateUpdate()
        {
            if (!active) return;
            if (!session || session != GameSession.Current || session.Finished) { End(); return; }
            CaptureActors();
            foreach (var state in frozen.Values) Hold(state);
        }
        public void End()
        {
            if (!active) return;
            active = false;
            foreach (var state in frozen.Values)
            {
                foreach (var entry in state.animations) if (entry.animation) entry.animation.enabled = entry.enabled;
                foreach (var entry in state.animators) if (entry.animator) entry.animator.enabled = entry.enabled;
                if (!state.root) continue;
                float seconds = (float)(Time.timeAsDouble - state.capturedAt);
                var brain = state.root.GetComponent<StalkerBrain>();
                if (brain) brain.DelayTimeStopDeadlines(seconds);
                var passage = state.root.GetComponent<StalkerDoorTraversal>();
                if (passage) passage.DelayTimeStopDeadline(seconds);
                var traffic = state.root.GetComponent<EnemyPassageTraffic>();
                if (traffic) traffic.DelayTimeStopDeadline(seconds);
                var baby = state.root.GetComponent<CorridorBabyBehaviour>();
                if (baby) baby.DelayTimeStopDeadlines(seconds);
                var maskAudio = state.root.GetComponent<CorridorMaskAudio>();
                if (maskAudio) maskAudio.DelayTimeStopDeadline(seconds);
                if (!state.agent) continue;
                state.agent.updateRotation = state.rotation;
                // A new cinematic/teardown owner must retain its locomotion lock.
                if (EnemyNavigation.Ready(state.agent) && state.owner &&
                    state.root.gameObject.activeInHierarchy && state.owner.enabled == state.ownerEnabled)
                    state.agent.isStopped = EnemyTrapRestraint.IsHeld(state.agent) || state.stopped;
            }
            foreach (var source in pausedSounds)
                if (source && source.gameObject.activeInHierarchy)
                {
                    if (session && !session.Finished) source.UnPause();
                    else source.Stop();
                }
            frozen.Clear(); pausedSounds.Clear();
            Shader.SetGlobalFloat("_RitualMonochrome", 0);
            if (volume) { volume.weight = 0; Destroy(volume.gameObject); }
            if (profile) Destroy(profile);
            volume = null; profile = null; session = null;
        }
        void OnDisable() { End(); }
        void OnDestroy() { End(); }
    }
}

