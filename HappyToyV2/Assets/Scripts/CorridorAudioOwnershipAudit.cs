using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Explicit diagnostic only. Production loops play normally; the listener tap
    // observes native buffers without generating samples or changing their contents.
    public sealed class CorridorAudioOwnershipAudit : MonoBehaviour
    {
        string output;
        float started, initialVolume;
        bool finished, savedPreferences, restoredPreferences;
        GameSession session;
        RoomAmbience ambience;
        RoomAmbience.Voice[] voices;
        RouteAudioCapture capture;
        CorridorAudioPhaseProbe probe;
        readonly Dictionary<AudioSource, bool> otherMutes = new Dictionary<AudioSource, bool>();
        readonly List<string> errors = new List<string>();
        readonly List<CorridorAudioPhaseProbe.Phase> phases = new List<CorridorAudioPhaseProbe.Phase>();
        readonly string[] floatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        readonly string[] optionKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };
        float[] oldFloats; int[] oldOptions; bool[] hadFloats, hadOptions;
        readonly Report report = new Report();
        [Serializable] sealed class ClipEvidence
        {
            public string cue, importedName, ownedName;
            public int samples, frequency, channels;
            public float peak, listenerDistance, gain, cutoff;
            public bool ownedCopy, loadedPcm, spatialLoop, occluded;
        }
        [Serializable] sealed class Report
        {
            public string status, failure, unity, device, profileDirectory;
            public string scope = "Controlled current corridor assay through public CreateCorridor/Begin. Player and actors do not move naturally: player poses are teleported near each real ambience emitter, monster behaviours and NavMesh agents are disabled. Non-ambience sources are explicitly muted throughout spatial-loop isolation. The three production RoomAmbience sources/owned imported clips play normally; no synthetic cue, forced AudioRenderer or manual clip playback. Phase statistics observe natural pre-device listener DSP and full WAV is retained. This is loop audibility, actual master/source mute, pause, disable/re-enable and owned cleanup evidence; not chapter survival, full game mix, human/device listening, fear or hardware performance certification.";
            public bool importedOwnership, pauseFrozen, disableStopped, enableResumed, ownedCleanup, importedResourcesSurvive;
            public int isolatedOtherSources;
            public ClipEvidence[] clips;
            public CorridorAudioPhaseProbe.Phase[] phases;
            public string[] errors;
            public RouteAudioCapture.Report audio;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v4-corridor-audio-output");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("Explicit corridor recorded-loop ownership assay").AddComponent<CorridorAudioOwnershipAudit>().output = Path.GetFullPath(args[i + 1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update()
        {
            if (finished) return;
            if (voices != null) IsolateOtherSources();
            if (started > 0 && Time.realtimeSinceStartup - started > 60) Finish("Native corridor audio assay timed out");
        }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup; Application.runInBackground = true; Directory.CreateDirectory(output);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished)
            {
                object next = null; bool moved = false; string failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { failure = error.ToString(); }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved)
                {
                    stack.Pop(); if (stack.Count == 0) { Finish(null); yield break; }
                    continue;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        IEnumerator Run()
        {
            yield return null; yield return null;
            session = GameSession.Current;
            Require(session && session.player && session.Shell, "Native session/player unavailable");
            SavePreferences(); initialVolume = session.Shell.Volume;
            report.profileDirectory = Path.Combine(output, "isolated-profile", Guid.NewGuid().ToString("N"));
            session.ConfigureRecordDirectory(report.profileDirectory);
            session.CreateCorridor(211); session.Shell.Begin();
            Require(session.CorridorMode && session.InputAllowed, "Public current corridor preparation failed");
            session.Shell.AdjustSettings(.8f - session.Shell.Volume, 0);
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component is StalkerBrain || component is NavMeshStartup || component is StoryDirector ||
                    component is AnnexEncounter || component is UncatAnnexEvent || component is V1HwacatEvent ||
                    component is LanternMaskEncounter || component is WeepingAngelEncounter || component is LovelyDollGuide)
                { component.StopAllCoroutines(); component.enabled = false; }
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) agent.enabled = false;
            session.player.enabled = false;
            session.player.GetComponent<CharacterController>().enabled = false;
            ambience = session.GetComponent<RoomAmbience>();
            Require(ambience, "Production RoomAmbience missing");
            yield return null; yield return null;
            voices = ambience.Voices.ToArray(); Require(voices.Length == 3, "Expected three real corridor ambience sources");
            IsolateOtherSources();
            var listener = session.player.eyes.GetComponent<AudioListener>();
            Require(listener && listener.enabled, "Production player listener missing");
            probe = listener.gameObject.AddComponent<CorridorAudioPhaseProbe>();
            capture = RouteAudioCapture.Attach(Path.Combine(output, "listener-audio"));
            var clipEvidence = new List<ClipEvidence>();
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i]; var imported = ExternalAudio.Shared("ambience-corridor", i);
                Require(imported && voice.source && voice.filter && voice.ownedClip, "Missing production recorded loop/source");
                var evidence = new ClipEvidence { cue = "ambience-corridor-" + i, importedName = imported.name,
                    ownedName = voice.ownedClip.name, samples = voice.ownedClip.samples, frequency = voice.ownedClip.frequency,
                    channels = voice.ownedClip.channels, ownedCopy = voice.ownedClip != imported && voice.source.clip == voice.ownedClip,
                    loadedPcm = voice.ownedClip.loadState == AudioDataLoadState.Loaded && voice.ownedClip.loadType == AudioClipLoadType.DecompressOnLoad,
                    spatialLoop = voice.source.loop && voice.source.spatialBlend == 1 && !voice.source.ignoreListenerPause && !voice.source.ignoreListenerVolume };
                var samples = new float[voice.ownedClip.samples * voice.ownedClip.channels];
                Require(voice.ownedClip.GetData(samples, 0), "Imported loop PCM could not be decoded");
                foreach (float value in samples)
                { Require(StealthRules.Finite(value), "Imported loop contains nonfinite samples"); evidence.peak = Mathf.Max(evidence.peak, Mathf.Abs(value)); }
                Require(evidence.ownedCopy && evidence.loadedPcm && evidence.spatialLoop && evidence.samples == 1080000 &&
                    evidence.frequency == 48000 && evidence.channels == 1 && evidence.peak > .01f && evidence.peak < .5f,
                    "Current field-recorded loop import/ownership contract failed: " + evidence.cue);
                SelectVoice(i); PlaceListenerNear(voice.source);
                yield return Measure("near-" + evidence.cue, true);
                evidence.listenerDistance = Vector3.Distance(listener.transform.position, voice.source.transform.position);
                evidence.gain = voice.source.volume; evidence.cutoff = voice.filter.cutoffFrequency; evidence.occluded = voice.occluded;
                Require(evidence.listenerDistance < 1.2f && evidence.gain > 0, "Nearby production loop never reached its real listener");
                clipEvidence.Add(evidence);
            }
            report.clips = clipEvidence.ToArray(); report.importedOwnership = true;
            SelectVoice(0); PlaceListenerNear(voices[0].source);
            yield return Measure("master-positive-before", true);
            session.Shell.AdjustSettings(-1, 0);
            Require(session.Shell.Volume == 0 && AudioListener.volume == 0, "Public master-volume adjustment did not mute");
            yield return Measure("master-zero", false);
            session.Shell.AdjustSettings(.8f, 0);
            yield return Measure("master-restored", true);
            voices[0].source.mute = true;
            yield return Measure("all-room-sources-muted", false);
            voices[0].source.mute = false;
            yield return Measure("room-source-unmuted", true);
            session.Shell.Pause(); int updates = ambience.MixUpdates; float gain = voices[0].source.volume;
            yield return Measure("shell-paused", false);
            report.pauseFrozen = AudioListener.pause && ambience.MixUpdates == updates && voices[0].source.volume == gain;
            Require(report.pauseFrozen, "Pause advanced the real ambience mix");
            session.Shell.Resume(); yield return Measure("shell-resumed", true);
            ambience.enabled = false;
            report.disableStopped = voices.All(voice => !voice.source.isPlaying);
            Require(report.disableStopped, "Disabling ambience left a production loop playing");
            yield return Measure("ambience-component-disabled", false);
            ambience.enabled = true; yield return Measure("ambience-component-enabled", true);
            report.enableResumed = voices.All(voice => voice.source.isPlaying);
            Require(report.enableResumed, "Re-enabling ambience did not resume its three owned sources");
            var owned = voices.Select(voice => voice.ownedClip).ToArray();
            var emitters = voices.Select(voice => voice.source.gameObject).ToArray();
            var shared = Enumerable.Range(0, 3).Select(index => ExternalAudio.Shared("ambience-corridor", index)).ToArray();
            Destroy(ambience); yield return null; yield return null;
            report.ownedCleanup = !ambience && owned.All(clip => !clip) && emitters.All(emitter => !emitter);
            report.importedResourcesSurvive = Enumerable.Range(0, 3).All(index => shared[index] &&
                ExternalAudio.Shared("ambience-corridor", index) == shared[index] && shared[index].loadState == AudioDataLoadState.Loaded);
            Require(report.ownedCleanup && report.importedResourcesSurvive, "Ambience teardown leaked owned objects or destroyed shared imported Resources");
            yield return Measure("ambience-component-destroyed", false);
        }
        void SelectVoice(int index)
        { for (int i = 0; i < voices.Length; i++) voices[i].source.mute = i != index; }
        void PlaceListenerNear(AudioSource source)
        {
            var player = session.player;
            Vector3 eyeOffset = player.eyes.transform.position - player.transform.position;
            player.transform.position = source.transform.position + Vector3.right * .65f - eyeOffset;
            Physics.SyncTransforms();
        }
        void IsolateOtherSources()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (voices.Any(voice => voice.source == source)) continue;
                if (!otherMutes.ContainsKey(source)) otherMutes.Add(source, source.mute);
                source.mute = true;
            }
        }
        IEnumerator Measure(string name, bool audible)
        {
            // Effects and buffers settle before the measured interval. The tap still
            // records the transition in the complete route WAV rather than discarding it.
            yield return new WaitForSecondsRealtime(.4f);
            probe.Begin(name, audible);
            yield return new WaitForSecondsRealtime(.55f);
            var phase = probe.End(); phases.Add(phase);
            Require(phase.callbacks >= 6 && phase.samples >= 10000 && phase.channels == 2 && phase.nonfinite == 0 && phase.clipped == 0,
                "Native DSP phase missing/invalid: " + name);
            Require(audible ? phase.peak > .00001 && phase.rms > .000001 : phase.peak < .00001,
                audible ? "Production loop inaudible: " + name : "Native PCM leaked during silence control: " + name);
        }
        void SavePreferences()
        {
            oldFloats = new float[floatKeys.Length]; hadFloats = new bool[floatKeys.Length];
            oldOptions = new int[optionKeys.Length]; hadOptions = new bool[optionKeys.Length];
            for (int i = 0; i < floatKeys.Length; i++) { hadFloats[i] = PlayerPrefs.HasKey(floatKeys[i]); oldFloats[i] = PlayerPrefs.GetFloat(floatKeys[i]); }
            for (int i = 0; i < optionKeys.Length; i++) { hadOptions[i] = PlayerPrefs.HasKey(optionKeys[i]); oldOptions[i] = PlayerPrefs.GetInt(optionKeys[i]); }
            savedPreferences = true;
        }
        void RestorePreferences()
        {
            if (!savedPreferences || restoredPreferences) return;
            if (session && session.Shell)
            {
                session.Shell.AdjustSettings(initialVolume - session.Shell.Volume, 0);
                session.Shell.SendMessage("SaveSettings", SendMessageOptions.DontRequireReceiver);
            }
            for (int i = 0; i < floatKeys.Length; i++) { if (hadFloats[i]) PlayerPrefs.SetFloat(floatKeys[i], oldFloats[i]); else PlayerPrefs.DeleteKey(floatKeys[i]); }
            for (int i = 0; i < optionKeys.Length; i++) { if (hadOptions[i]) PlayerPrefs.SetInt(optionKeys[i], oldOptions[i]); else PlayerPrefs.DeleteKey(optionKeys[i]); }
            PlayerPrefs.Save(); restoredPreferences = true;
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            try
            {
                if (probe) probe.End();
                report.audio = capture ? capture.Complete() : null;
                if (failure == null && (report.audio == null || !report.audio.nonSilent || report.audio.truncated ||
                    report.audio.nonfiniteSamples != 0 || !string.IsNullOrEmpty(report.audio.writerError))) failure = "Complete native WAV evidence unavailable";
                report.failure = failure ?? ""; report.phases = phases.ToArray(); report.errors = errors.ToArray();
                report.isolatedOtherSources = otherMutes.Count; report.unity = Application.unityVersion; report.device = SystemInfo.graphicsDeviceName;
                report.status = string.IsNullOrEmpty(report.failure) && errors.Count == 0 && report.importedOwnership && report.pauseFrozen &&
                    report.disableStopped && report.enableResumed && report.ownedCleanup && report.importedResourcesSurvive ? "PASS" : "FAIL";
                foreach (var pair in otherMutes) if (pair.Key) pair.Key.mute = pair.Value;
                RestorePreferences();
                File.WriteAllText(Path.Combine(output, "corridor-audio-ownership.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_CORRIDOR_AUDIO_OWNERSHIP_" + report.status);
                Application.Quit(report.status == "PASS" ? 0 : 2);
            }
            catch (Exception error) { Debug.LogError("Corridor audio report failed: " + error); RestorePreferences(); Application.Quit(2); }
        }
        void OnDestroy() { RestorePreferences(); }
    }

    public sealed class CorridorAudioPhaseProbe : MonoBehaviour
    {
        readonly object gate = new object();
        Phase phase;
        double squares;
        [Serializable] public sealed class Phase
        {
            public string name;
            public bool expectedAudible;
            public int callbacks, channels, nonfinite, clipped;
            public long samples;
            public double peak, rms;
        }
        public void Begin(string name, bool audible)
        { lock (gate) { phase = new Phase { name = name, expectedAudible = audible }; squares = 0; } }
        public Phase End()
        {
            lock (gate)
            {
                var result = phase; phase = null;
                if (result != null) result.rms = result.samples > 0 ? Math.Sqrt(squares / result.samples) : 0;
                return result;
            }
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                if (phase == null) return;
                phase.callbacks++; phase.channels = channels;
                foreach (float value in data)
                {
                    phase.samples++;
                    if (float.IsNaN(value) || float.IsInfinity(value)) { phase.nonfinite++; continue; }
                    double amplitude = Math.Abs(value); phase.peak = Math.Max(phase.peak, amplitude); squares += value * (double)value;
                    if (amplitude >= 1) phase.clipped++;
                }
            }
        }
    }
}
