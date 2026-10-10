using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class CorridorBabyProbe : MonoBehaviour
    {
        string output; float started; bool finished;
        GameSession session; StalkerBrain brain; CorridorBabyBehaviour baby;
        RouteAudioCapture capture; CorridorAudioPhaseProbe tap;
        readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
        readonly List<CorridorAudioPhaseProbe.Phase> phases = new List<CorridorAudioPhaseProbe.Phase>();
        readonly List<string> errors = new List<string>();
        readonly Dictionary<AudioSource, bool> mutes = new Dictionary<AudioSource, bool>();
        readonly string[] floatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        readonly string[] intKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };
        float[] floats; int[] ints; bool[] hadFloats, hadInts; bool preferencesSaved, restored;
        float oldVolume, oldPatrol, oldChase;
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status, failure, unity, device;
            public string scope = "Controlled native corridor Baby fixture. Public CreateCorridor prepares the actual actor/model/resources; its release is explicitly forced for this diagnostic. Initial wait, admitted small-sound navigation, actual sight/chase loss and slow patrol use production brain/agent. Footstep and loud-noise callbacks are controlled stimuli using production physical-range/NavMesh guards, not claims of keyboard footsteps or natural firecracker throwing. Other enemies are disabled; player positions are controlled teleports. Actor speeds are set to zero only after state/movement proof for isolated crying PCM/pause/mute/cabinet/cleanup controls. Other sources are muted during isolation. Cabinet RNG is explicitly fixed to the successful outcome for that acoustic fixture. Listener DSP is natural, pre-device and retained as a WAV; no synthetic samples or forced AudioRenderer. This does not certify natural survival, full mix, human/device listening, fear or hardware performance.";
            public Check[] checks; public string[] errors;
            public CorridorAudioPhaseProbe.Phase[] phases;
            public RouteAudioCapture.Report audio;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v5-baby-output");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("Explicit Baby state and recorded-cry diagnostic").AddComponent<CorridorBabyProbe>().output = Path.GetFullPath(args[i + 1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string value, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(value); }
        void Update()
        {
            if (finished) return;
            if (baby) foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (source == baby.CrySource) continue;
                if (!mutes.ContainsKey(source)) mutes.Add(source, source.mute);
                source.mute = true;
            }
            if (started > 0 && Time.realtimeSinceStartup - started > 80) Finish("Bounded Baby fixture timed out");
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
                if (!moved) { stack.Pop(); if (stack.Count == 0) { Finish(null); yield break; } continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        void CheckValue(string name, bool passed)
        { checks[name] = passed; if (!passed) throw new InvalidOperationException(name); }
        IEnumerator Await(Func<bool> condition, float seconds, string message)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup >= limit) throw new InvalidOperationException(message); yield return null; }
        }
        void PlacePlayer(Vector3 point)
        { session.player.transform.position = point + Vector3.up * .03f; Physics.SyncTransforms(); }
        Vector3 Nearby(Vector3 origin, float radius, bool clear)
        {
            for (int i = 0; i < 24; i++)
            {
                Vector3 offset = Quaternion.Euler(0, i * 15, 0) * Vector3.forward * radius;
                if (!NavMesh.SamplePosition(origin + offset, out var hit, .65f, NavMesh.AllAreas) ||
                    Vector3.Distance(origin, hit.position) < radius * .6f || !EnemyNavigation.SameFloor(hit.position, origin.y)) continue;
                if (clear && (!EnemyNavigation.ClearSight(origin + Vector3.up * 1.2f, hit.position + Vector3.up * 1.2f) ||
                    EnemyNavigation.SoundTransmission(hit.position + Vector3.up * .5f, origin + Vector3.up, brain.transform, session.player.transform) < .99f)) continue;
                var route = new NavMeshPath();
                if (EnemyNavigation.TryRoute(brain.GetComponent<NavMeshAgent>(), hit.position, brain.HomeFloorY, route, radius * 3)) return hit.position;
            }
            throw new InvalidOperationException("No supported controlled Baby fixture pose");
        }
        Vector3 Remote()
        {
            var route = new NavMeshPath();
            for (int cell = 0; cell < CorridorLayout.Count; cell++)
            {
                if (!NavMesh.SamplePosition(session.Corridor.CellPosition(cell), out var hit, 1.5f, NavMesh.AllAreas) ||
                    Vector3.Distance(hit.position, brain.transform.position) < 18) continue;
                if (EnemyNavigation.TryRoute(brain.GetComponent<NavMeshAgent>(), hit.position, brain.HomeFloorY, route, 60)) return hit.position;
            }
            throw new InvalidOperationException("No remote supported player pose");
        }
        IEnumerator Measure(string name, bool audible)
        {
            yield return new WaitForSecondsRealtime(.4f); tap.Begin(name, audible);
            yield return new WaitForSecondsRealtime(.65f); var phase = tap.End(); phases.Add(phase);
            CheckValue(name + " native callbacks", phase.callbacks >= 6 && phase.samples >= 10000 && phase.channels == 2 && phase.nonfinite == 0 && phase.clipped == 0);
            CheckValue(name + " native PCM", audible ? phase.peak > .00001 && phase.rms > .000001 : phase.peak < .00001);
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);
            yield return null; yield return null; session = GameSession.Current;
            CheckValue("real session", session && session.player && session.Shell);
            SavePreferences(); oldVolume = session.Shell.Volume;
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile", Guid.NewGuid().ToString("N")));
            session.CreateCorridor(211); session.Shell.Begin(); session.Shell.AdjustSettings(.8f - session.Shell.Volume, 0);
            brain = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include).Single(actor => actor.CorridorBaby);
            baby = brain.CorridorBaby; var agent = brain.GetComponent<NavMeshAgent>(); oldPatrol = brain.patrolSpeed; oldChase = brain.chaseSpeed;
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (component is StalkerBrain && component != brain || component is NavMeshStartup || component is StoryDirector ||
                    component is AnnexEncounter || component is LanternMaskEncounter || component is WeepingAngelEncounter ||
                    component is UncatAnnexEvent || component is V1HwacatEvent || component is LovelyDollGuide)
                { component.StopAllCoroutines(); component.enabled = false; }
            foreach (var other in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include)) if (other != agent) other.enabled = false;
            session.player.enabled = false; session.player.GetComponent<CharacterController>().enabled = false;
            CheckValue("supported Baby release", NavMesh.SamplePosition(brain.transform.position, out var spawn, 1, agent.areaMask));
            brain.transform.position = spawn.position; brain.gameObject.SetActive(true); agent.enabled = true;
            CheckValue("real Baby agent", agent.Warp(spawn.position)); brain.enabled = true;
            Vector3 near = Nearby(brain.transform.position, 3.4f, true); PlacePlayer(near);
            brain.transform.rotation = Quaternion.LookRotation(brain.transform.position - session.player.transform.position);
            capture = RouteAudioCapture.Attach(Path.Combine(output, "listener-audio"));
            tap = session.player.eyes.gameObject.AddComponent<CorridorAudioPhaseProbe>();
            Vector3 waitingAt = brain.transform.position;
            yield return Measure("initial recorded crying", true);
            CheckValue("first appearance stays and cries", baby.WaitingForSound && brain.state == StalkerBrain.State.Patrol &&
                Vector3.Distance(waitingAt, brain.transform.position) < .02f && agent.isStopped && baby.CrySource.isPlaying);
            var imported = ExternalAudio.Shared("enemy-baby-cry");
            CheckValue("owned spatial imported cry", imported && baby.OwnedCry != imported && baby.CrySource.clip == baby.OwnedCry &&
                baby.CrySource.loop && baby.CrySource.spatialBlend == 1 && !baby.CrySource.ignoreListenerPause && !baby.CrySource.ignoreListenerVolume);
            Vector3 small = Nearby(brain.transform.position, 2.2f, true); PlacePlayer(Remote());
            int heard = brain.FootstepNoisesAccepted;
            typeof(StalkerBrain).GetMethod("HearFootstep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(brain, new object[] { small, 4f });
            CheckValue("small callback investigates heard position", brain.FootstepNoisesAccepted == heard + 1 &&
                brain.state == StalkerBrain.State.Investigate && baby.Phase == CorridorBabyMemory.Phase.InvestigatingCry &&
                Vector3.Distance(brain.InvestigationPoint, small) < 1.5f && !baby.HasChased);
            yield return Await(() => brain.InvestigationArrived, 14, "Small heard position not reached by native slow agent");
            CheckValue("small noise caused real travel", Vector3.Distance(waitingAt, brain.transform.position) > .7f && baby.CrySource.isPlaying);
            yield return Await(() => baby.WaitingForSound, 5, "First investigation did not settle to waiting cry");
            Vector3 seenAt = Nearby(brain.transform.position, 3.5f, true); PlacePlayer(seenAt);
            brain.transform.rotation = Quaternion.LookRotation(session.player.transform.position - brain.transform.position);
            Physics.SyncTransforms(); yield return Await(() => brain.state == StalkerBrain.State.Chase, 2, "Actual sight did not start Baby chase");
            CheckValue("real sight remembers pursuit", brain.CanSeePlayer() && baby.HasChased && baby.Phase == CorridorBabyMemory.Phase.Chasing);
            PlacePlayer(Remote()); yield return Await(() => baby.Phase == CorridorBabyMemory.Phase.WanderingCry, 9, "Lost pursuit did not enter wandering cry");
            Vector3 wanderAt = brain.transform.position;
            yield return Await(() => Vector3.Distance(wanderAt, brain.transform.position) > .3f, 7, "Post-chase Baby did not wander");
            CheckValue("wander is slow and crying", agent.speed <= CorridorBabyBehaviour.CalmSpeed + .001f && baby.CrySource.isPlaying);
            PlacePlayer(Remote()); var playerAt = session.player.transform.position; int largeBefore = brain.NoisesAccepted;
            Vector3 loud = Nearby(brain.transform.position, 1.6f, false);
            CheckValue("loud callback admitted without sight", !brain.CanSeePlayer() && brain.HearNoise(loud, 3));
            CheckValue("heard large report targets player", brain.NoisesAccepted == largeBefore + 1 && brain.state == StalkerBrain.State.Chase &&
                baby.Phase == CorridorBabyMemory.Phase.Chasing && Vector3.Distance(brain.LastKnownPosition, playerAt) < 1.5f);
            var saved = brain.CaptureProgress(); saved.Validate();
            CheckValue("baby checkpoint retains chase history", saved.babyVersion == 1 && saved.baby != null && saved.baby.hasChased && saved.baby.phase == CorridorBabyMemory.Phase.Chasing);
            // Audio-only controls below explicitly immobilize this admitted actor.
            brain.patrolSpeed = brain.chaseSpeed = 0; agent.speed = 0; PlacePlayer(Nearby(brain.transform.position, 3.4f, true));
            yield return Measure("post-chase recorded crying", true);
            session.Shell.AdjustSettings(-1, 0); yield return Measure("master muted crying", false);
            session.Shell.AdjustSettings(.8f, 0); yield return Measure("master restored crying", true);
            baby.CrySource.mute = true; yield return Measure("source muted crying", false);
            baby.CrySource.mute = false; yield return Measure("source restored crying", true);
            session.Shell.Pause(); int pausedSample = baby.CrySource.timeSamples; var pausedAt = brain.transform.position;
            yield return Measure("paused crying", false);
            CheckValue("pause freezes position and native clip cursor", AudioListener.pause && brain.transform.position == pausedAt && baby.CrySource.timeSamples == pausedSample);
            session.Shell.Resume(); yield return Measure("resumed crying", true);
            CheckValue("stationary cry admitted to candle danger", session.Corridor.Lighting.HeardMovement(brain));
            var cabinet = FindObjectsByType<Interactable>(FindObjectsInactive.Include).First(item => item.gameObject.activeInHierarchy &&
                item.kind == Interactable.Kind.HidingPlace && item.gameObject.scene == session.gameObject.scene);
            Vector3 outward = cabinet.outside.position - cabinet.inside.position; outward.y = 0;
            CheckValue("supported cabinet cry fixture", NavMesh.SamplePosition(cabinet.outside.position + outward.normalized * 3,
                out var nearCabinet, 1.5f, agent.areaMask));
            CheckValue("real agent cabinet fixture warp", agent.Warp(nearCabinet.position)); Physics.SyncTransforms(); PlacePlayer(cabinet.outside.position);
            session.player.HidingRandomSample = () => 0f; session.player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position);
            CheckValue("actual cabinet acoustic fixture", session.player.Hidden);
            yield return Measure("cabinet recorded crying", true); yield return new WaitForSecondsRealtime(1.4f);
            CheckValue("stationary cry refreshes danger inside cabinet", session.Corridor.Lighting.HeardMovement(brain));
            session.player.LeaveHiding(); session.player.HidingRandomSample = null;
            baby.enabled = false; yield return Measure("disabled crying", false);
            baby.enabled = true; yield return Measure("enabled crying", true);
            session.Finish(false); yield return Measure("finished crying", false);
            CheckValue("finish stops cry", !baby.CrySource.isPlaying);
            var owned = baby.OwnedCry; var emitter = baby.CrySource.gameObject;
            Destroy(baby); yield return null; yield return null;
            CheckValue("owned cry cleanup preserves imported resource", !owned && !emitter && imported && ExternalAudio.Shared("enemy-baby-cry") == imported);
        }
        void SavePreferences()
        {
            floats = new float[floatKeys.Length]; ints = new int[intKeys.Length]; hadFloats = new bool[floatKeys.Length]; hadInts = new bool[intKeys.Length];
            for (int i = 0; i < floatKeys.Length; i++) { hadFloats[i] = PlayerPrefs.HasKey(floatKeys[i]); floats[i] = PlayerPrefs.GetFloat(floatKeys[i]); }
            for (int i = 0; i < intKeys.Length; i++) { hadInts[i] = PlayerPrefs.HasKey(intKeys[i]); ints[i] = PlayerPrefs.GetInt(intKeys[i]); }
            preferencesSaved = true;
        }
        void Restore()
        {
            if (restored) return; restored = true;
            if (brain) { brain.patrolSpeed = oldPatrol; brain.chaseSpeed = oldChase; }
            if (session && session.player) session.player.HidingRandomSample = null;
            foreach (var pair in mutes) if (pair.Key) pair.Key.mute = pair.Value;
            if (!preferencesSaved) return;
            if (session && session.Shell) { session.Shell.AdjustSettings(oldVolume - session.Shell.Volume, 0); session.Shell.SendMessage("SaveSettings", SendMessageOptions.DontRequireReceiver); }
            for (int i = 0; i < floatKeys.Length; i++) { if (hadFloats[i]) PlayerPrefs.SetFloat(floatKeys[i], floats[i]); else PlayerPrefs.DeleteKey(floatKeys[i]); }
            for (int i = 0; i < intKeys.Length; i++) { if (hadInts[i]) PlayerPrefs.SetInt(intKeys[i], ints[i]); else PlayerPrefs.DeleteKey(intKeys[i]); }
            PlayerPrefs.Save();
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            try
            {
                if (tap) tap.End(); var audio = capture ? capture.Complete() : null;
                if (failure == null && (audio == null || !audio.nonSilent || audio.truncated || audio.nonfiniteSamples > 0 ||
                    !string.IsNullOrEmpty(audio.writerError))) failure = "Natural Baby PCM retention incomplete";
                var report = new Report { status = failure == null && errors.Count == 0 && checks.Count > 0 && checks.Values.All(value => value) ? "PASS" : "FAIL",
                    failure = failure ?? "", unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
                    checks = checks.Select(pair => new Check { name = pair.Key, passed = pair.Value }).ToArray(), phases = phases.ToArray(), errors = errors.ToArray(), audio = audio };
                Restore(); File.WriteAllText(Path.Combine(output, "baby-behaviour.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_BABY_BEHAVIOUR_" + report.status); Application.Quit(report.status == "PASS" ? 0 : 2);
            }
            catch (Exception error) { Restore(); Debug.LogError("Baby report failed: " + error); Application.Quit(2); }
        }
        void OnDestroy() { Restore(); }
    }
}
