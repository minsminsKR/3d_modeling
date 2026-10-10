using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // This assay isolates the new owned voices after actual earned perception.
    // The separate HorrorFeedbackAudit retains the normal combined production mix.
    public sealed class PursuitFeedbackAudit : MonoBehaviour
    {
        string output; float started; bool finished;
        GameSession session; PlayerMotor player; StalkerBrain actor;
        DetectionFeedback detection; DetectionPursuitAudio voice;
        RouteAudioCapture capture; CorridorAudioPhaseProbe tap;
        readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
        readonly Dictionary<AudioSource, bool> mutes = new Dictionary<AudioSource, bool>();
        readonly List<CorridorAudioPhaseProbe.Phase> phases = new List<CorridorAudioPhaseProbe.Phase>();
        readonly List<string> errors = new List<string>();
        float originalVolume; bool originalSoft, settingsSaved;
        Func<float> originalHidingSample; bool hidingSampleSaved;
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status, failure, unity, graphics;
            public string scope = "Controlled supported school Cyclops/player poses with actor movement speeds zero. Genuine opaque-cover no-sight control uses a legal active Chase restore without DetectionFeedback.Signal; subsequent cover removal earns actual production recognition/Chase. Only the new impact/music sources remain unmuted during isolation. Natural listener DSP is captured after current-process device mute verification. Pause, source/master mute, comfort, hidden presentation suppression, disengagement, lifecycle and imported clip ownership are production. Hiding suppression uses controlled public Hide state at the existing supported floor with an empty local marker, not authored cabinet geometry/survival. The separate HorrorFeedbackAudit verifies normal combined production audio; this isolated assay is not certification of human fear or natural survival.";
            public Check[] checks; public string[] errors; public CorridorAudioPhaseProbe.Phase[] phases;
            public RouteAudioCapture.Report audio;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v7-pursuit-output");
            if (index < 0 || index + 1 >= args.Length) return;
            new GameObject("Quiet owned pursuit sound audit").AddComponent<PursuitFeedbackAudit>().output = Path.GetFullPath(args[index + 1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update()
        {
            if (finished) return;
            if (voice) foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (source == voice.ImpactSource || source == voice.PursuitSource) continue;
                if (!mutes.ContainsKey(source)) mutes.Add(source, source.mute); source.mute = true;
            }
            if (started > 0 && Time.realtimeSinceStartup - started > 100) Finish("Bounded pursuit audio fixture timed out");
        }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup; Application.runInBackground = true; Directory.CreateDirectory(output);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished)
            {
                bool moved = false; object next = null; string failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { failure = error.ToString(); }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved) { stack.Pop(); if (stack.Count == 0) { Finish(null); yield break; } continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        void CheckValue(string name, bool passed)
        { checks[name] = passed; if (!passed) throw new InvalidOperationException(name); }
        IEnumerator Await(Func<bool> condition, float seconds, string why)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup >= end) throw new InvalidOperationException(why); yield return null; }
        }
        IEnumerator Measure(string name, bool audible, float settle = .2f)
        {
            yield return new WaitForSecondsRealtime(settle); tap.Begin(name, audible);
            yield return new WaitForSecondsRealtime(.55f); var phase = tap.End(); phases.Add(phase);
            CheckValue(name + " valid natural PCM", phase.callbacks >= 6 && phase.samples >= 10000 && phase.nonfinite == 0 && phase.clipped == 0);
            CheckValue(name + " audibility", audible ? phase.peak > .00001 && phase.rms > .000001 : phase.peak < .00001);
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output); yield return null; yield return null;
            session = GameSession.Current;
            CheckValue("actual session", session && session.player && session.Shell);
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile", Guid.NewGuid().ToString("N")));
            session.Shell.Begin(); player = session.player; detection = player.GetComponent<DetectionFeedback>();
            voice = player.GetComponent<DetectionPursuitAudio>();
            CheckValue("perception owns new voices", detection && voice);
            originalVolume = session.Shell.Volume; originalSoft = session.Shell.ReducedMotion; settingsSaved = true;
            session.Shell.AdjustSettings(.8f - session.Shell.Volume, 0);
            if (session.Shell.ReducedMotion) session.Shell.ToggleReducedMotion();
            var director = FindFirstObjectByType<StoryDirector>();
            CheckValue("actual authored school actor", director && director.stalker);
            actor = director.stalker;
            foreach (var item in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (item is StoryDirector || item is AnnexEncounter || item is UncatAnnexEvent || item is V1HwacatEvent ||
                    item is LanternMaskEncounter || item is WeepingAngelEncounter || item is LovelyDollGuide || item is V1CyclopseIntro)
                { item.StopAllCoroutines(); item.enabled = false; }
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include)) brain.gameObject.SetActive(false);
            foreach (var startup in actor.GetComponents<NavMeshStartup>()) { startup.StopAllCoroutines(); startup.enabled = false; }
            CheckValue("supported actual school floor", NavMesh.SamplePosition(new Vector3(-6.5f, 0, 0), out var origin, 1.5f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(origin.position + Vector3.right * 6, out _, 1, NavMesh.AllAreas));
            NavMesh.SamplePosition(origin.position + Vector3.right * 6, out var feet, 1, NavMesh.AllAreas);
            player.enabled = false; player.GetComponent<CharacterController>().enabled = false;
            player.transform.SetPositionAndRotation(feet.position + Vector3.up * .02f, Quaternion.Euler(0, -90, 0));
            player.flashlight.enabled = true; player.eyes.transform.localRotation = Quaternion.identity;
            var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = false; actor.enabled = false;
            actor.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(Vector3.right)); actor.player = player;
            actor.patrolSpeed = actor.chaseSpeed = 0; actor.state = StalkerBrain.State.Patrol;
            actor.gameObject.SetActive(true); agent.enabled = true;
            CheckValue("native school actor binding", agent.Warp(origin.position)); Physics.SyncTransforms();
            var importedImpacts = new[] { ExternalAudio.Shared("detection-impact", 0), ExternalAudio.Shared("detection-impact", 1) };
            var importedLoop = ExternalAudio.Shared("pursuit-loop");
            CheckValue("owned nonspatial clips", voice.OwnedImpactClips.Length == 2 && voice.OwnedImpactClips.All(clip => clip && !importedImpacts.Contains(clip)) &&
                voice.OwnedPursuitClip && voice.OwnedPursuitClip != importedLoop && voice.PursuitSource.clip == voice.OwnedPursuitClip &&
                voice.PursuitSource.loop && voice.PursuitSource.spatialBlend == 0 && voice.ImpactSource.spatialBlend == 0 &&
                !voice.PursuitSource.ignoreListenerPause && !voice.PursuitSource.ignoreListenerVolume && voice.PursuitSource.panStereo == 0);
            capture = RouteAudioCapture.Attach(Path.Combine(output, "listener-audio")); tap = player.eyes.gameObject.AddComponent<CorridorAudioPhaseProbe>();
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Explicit opaque no-recognition control";
            cover.transform.position = Vector3.Lerp(actor.transform.position, player.transform.position, .5f) + Vector3.up * 1.2f;
            cover.transform.localScale = new Vector3(.4f, 2.5f, 3); Physics.SyncTransforms();
            CheckValue("opaque control prevents actual sight", !actor.CanSeePlayer());
            var hiddenChase = actor.CaptureProgress(); hiddenChase.active = true; hiddenChase.state = StalkerBrain.State.Chase;
            hiddenChase.lastKnown = player.transform.position; hiddenChase.memory = 6; hiddenChase.awareness = 1;
            actor.RestoreChapterProgress(hiddenChase, FindObjectsByType<Interactable>(FindObjectsInactive.Include)); actor.enabled = true;
            yield return Measure("unwitnessed active chase", false);
            CheckValue("unwitnessed chase never creates music", actor.state == StalkerBrain.State.Chase && detection.CuesPlayed == 0 &&
                voice.ImpactsPlayed == 0 && voice.LoopsStarted == 0 && voice.LoopGain == 0);
            var calm = actor.CaptureProgress(); calm.state = StalkerBrain.State.Patrol; calm.memory = calm.awareness = 0;
            actor.RestoreChapterProgress(calm, FindObjectsByType<Interactable>(FindObjectsInactive.Include));
            cover.SetActive(false); Destroy(cover); Physics.SyncTransforms();
            yield return Await(() => voice.ImpactsPlayed == 1, 3, "Actual clear sight did not admit recognition impact");
            CheckValue("actual sight earns one impact", actor.CanSeePlayer() && actor.state == StalkerBrain.State.Chase && detection.CuesPlayed == 1);
            int impactCount = voice.ImpactsPlayed; voice.OnRecognition();
            CheckValue("same recognition cannot duplicate impact", voice.ImpactsPlayed == impactCount);
            yield return Measure("earned impact and rising pursuit", true, .05f);
            yield return Await(() => voice.LoopGain >= DetectionPursuitAudio.PursuitMaximumGain * .95f, 3, "Genuine sustained pursuit failed to ramp music");
            yield return Measure("sustained owned pursuit", true);
            session.Shell.Pause(); int cursor = voice.PursuitSource.timeSamples; float envelope = voice.Envelope;
            yield return Measure("paused owned pursuit", false);
            CheckValue("pause freezes envelope and native cursor", voice.Envelope == envelope && voice.PursuitSource.timeSamples == cursor);
            session.Shell.Resume(); yield return Measure("resumed owned pursuit", true);
            voice.ImpactSource.mute = voice.PursuitSource.mute = true; yield return Measure("source-muted owned pursuit", false);
            voice.ImpactSource.mute = voice.PursuitSource.mute = false;
            session.Shell.AdjustSettings(-1, 0); yield return Measure("master-muted owned pursuit", false);
            session.Shell.AdjustSettings(.8f, 0); yield return Measure("master-restored owned pursuit", true);
            session.Shell.ToggleReducedMotion(); yield return Measure("comfort owned pursuit", true);
            CheckValue("comfort bounds steady subjective music", voice.LoopGain <= DetectionPursuitAudio.SoftPursuitMaximumGain + .0001f &&
                voice.PursuitSource.pitch == 1 && voice.PursuitSource.panStereo == 0);
            session.Shell.ToggleReducedMotion();
            var hideMarker = new GameObject("Controlled local protected-hide presentation marker").AddComponent<Interactable>();
            hideMarker.kind = Interactable.Kind.HidingPlace; var beforeHide = player.transform.position;
            int impactsBeforeHide = voice.ImpactsPlayed, cuesBeforeHide = detection.CuesPlayed;
            bool originalChaseEnded = false;
            originalHidingSample = player.HidingRandomSample; hidingSampleSaved = true; player.HidingRandomSample = () => 0;
            try
            {
                player.Hide(hideMarker, beforeHide, beforeHide);
                CheckValue("actual protected hiding presentation", player.Hidden && player.HidingProtected);
                // A witnessed successful entry calls StalkerBrain.BeginSearch.
                // It ends the old pursuit episode; emerging into genuine sight
                // should earn one new recognition rather than suppressing it.
                originalChaseEnded = actor.state != StalkerBrain.State.Chase;
                if (player.HidingOutcome == CabinetHidingOutcome.Survived)
                    CheckValue("successful witnessed hiding ends original chase", actor.state == StalkerBrain.State.Search);
                yield return Await(() => voice.LoopGain == 0 && !voice.PursuitSource.isPlaying, 4, "Protected hiding did not fade witnessed music");
                yield return Measure("protected hidden owned pursuit", false);
                CheckValue("hidden state cannot earn another impact", voice.ImpactsPlayed == impactsBeforeHide && detection.CuesPlayed == cuesBeforeHide);
                originalChaseEnded |= actor.state != StalkerBrain.State.Chase;
                player.LeaveHiding(); CheckValue("public presentation fixture exits", !player.Hidden);
            }
            finally { player.HidingRandomSample = originalHidingSample; hidingSampleSaved = false; Destroy(hideMarker.gameObject); }
            int expectedImpacts = impactsBeforeHide + (originalChaseEnded ? 1 : 0);
            int expectedCues = cuesBeforeHide + (originalChaseEnded ? 1 : 0);
            yield return Await(() => actor.CanSeePlayer() && actor.state == StalkerBrain.State.Chase &&
                voice.ImpactsPlayed == expectedImpacts && detection.CuesPlayed == expectedCues &&
                voice.LoopGain >= DetectionPursuitAudio.PursuitMaximumGain * .95f, 3,
                "Actual sight did not earn exactly the recognition required by the observed pursuit episode");
            CheckValue(originalChaseEnded ? "fresh actual sight earns one new episode impact" : "continuous witnessed chase keeps its original impact",
                voice.ImpactsPlayed == expectedImpacts && detection.CuesPlayed == expectedCues);
            yield return Measure("visible-again owned pursuit", true);
            actor.enabled = false;
            yield return Await(() => voice.LoopGain == 0 && !voice.PursuitSource.isPlaying, 4, "Lost witnessed pursuit did not fade music to silence");
            yield return Measure("disengaged owned pursuit", false);
            voice.enabled = false; yield return Measure("disabled owned pursuit", false);
            CheckValue("disable clears owned presentation", !voice.ImpactSource.isPlaying && !voice.PursuitSource.isPlaying && voice.Envelope == 0);
            var ownedImpacts = voice.OwnedImpactClips.ToArray(); var ownedLoop = voice.OwnedPursuitClip;
            var emitter = voice.PursuitSource.gameObject; Destroy(voice); yield return null; yield return null;
            CheckValue("owned cleanup preserves imported audio", ownedImpacts.All(clip => !clip) && !ownedLoop && !emitter &&
                importedImpacts.All(clip => clip) && importedLoop && ExternalAudio.Shared("pursuit-loop") == importedLoop);
        }
        void Restore()
        {
            foreach (var pair in mutes) if (pair.Key) pair.Key.mute = pair.Value;
            if (player && hidingSampleSaved) { player.HidingRandomSample = originalHidingSample; hidingSampleSaved = false; }
            if (session && session.Shell && settingsSaved)
            {
                session.Shell.AdjustSettings(originalVolume - session.Shell.Volume, 0);
                if (session.Shell.ReducedMotion != originalSoft) session.Shell.ToggleReducedMotion();
                settingsSaved = false;
            }
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            try
            {
                var audio = capture ? capture.Complete() : null;
                if (failure == null && (audio == null || !audio.nonSilent || audio.truncated || audio.nonfiniteSamples != 0 || !string.IsNullOrEmpty(audio.writerError)))
                    failure = "Natural isolated pursuit PCM retention incomplete";
                var report = new Report { status = failure == null && errors.Count == 0 && checks.Count > 0 && checks.Values.All(value => value) ? "PASS" : "FAIL",
                    failure = failure ?? "", unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                    checks = checks.Select(pair => new Check { name = pair.Key, passed = pair.Value }).ToArray(), errors = errors.ToArray(), phases = phases.ToArray(), audio = audio };
                Restore(); File.WriteAllText(Path.Combine(output, "pursuit-feedback-audio.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_PURSUIT_FEEDBACK_AUDIO_" + report.status); Application.Quit(report.status == "PASS" ? 0 : 2);
            }
            catch (Exception error) { Restore(); Debug.LogError("Pursuit report failed: " + error); Application.Quit(2); }
        }
        void OnDestroy() => Restore();
    }
}
