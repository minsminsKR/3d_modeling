using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Controlled event interruption cases with direct collection/inspection setup.
    // This is not a walking or survival playthrough.
    public sealed class EventTransitionAudit : MonoBehaviour
    {
        static bool installed;
        string output;
        float deadline;
        bool preferenceCaptured, originalReducedMotion;
        readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
        readonly HashSet<string> existingPreferences = new HashSet<string>();
        static readonly string[] floatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        static readonly string[] intKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };
        readonly List<string> errors = new List<string>();
        [Serializable]
        class Result
        {
            public string scope = "Controlled interruption, comfort-setting and restart cases; not a survival playthrough";
            public bool reachedRoar, pauseFrozen, annexPrerequisites, restorationAccepted,
                resolvedWithoutReactivation, cancellationIdempotent, uncatCancelledOnRestore,
                firstRestartClean, revealStarted, secondRestartClean,
                uncatComfortStable, uncatPauseFrozen, uncatCancelledOnFinish,
                thirdRestartClean, uncatCancelledOnDisable;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-v2-transitions-output");
            if (i < 0 || i + 1 >= args.Length || installed) return;
            installed = true; Application.runInBackground = true;
            var go = new GameObject("Event transition audit"); DontDestroyOnLoad(go);
            go.AddComponent<EventTransitionAudit>().output = args[i + 1];
        }
        void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }
        void Update()
        {
            if (deadline <= 0 || Time.realtimeSinceStartup <= deadline) return;
            deadline = 0;
            File.WriteAllText(Path.Combine(output, "transitions-timeout.txt"), "Event transition audit timed out. No passing result was produced.");
            RestorePreference(); Application.Quit(2);
        }
        void CapturePreference(GameShell shell)
        {
            foreach (var key in floatKeys)
            {
                if (PlayerPrefs.HasKey(key)) existingPreferences.Add(key);
                savedFloats[key] = PlayerPrefs.GetFloat(key);
            }
            foreach (var key in intKeys)
            {
                if (PlayerPrefs.HasKey(key)) existingPreferences.Add(key);
                savedInts[key] = PlayerPrefs.GetInt(key);
            }
            originalReducedMotion = shell.ReducedMotion;
            preferenceCaptured = true;
        }
        void RestorePreference()
        {
            if (!preferenceCaptured) return;
            var session = GameSession.Current;
            if (session && session.Shell)
            {
                var shell = session.Shell;
                if (shell.ReducedMotion != originalReducedMotion) shell.ToggleReducedMotion();
                // SaveSettings writes every preference, so flush first and then restore
                // all pre-audit keys, including keys which were previously absent.
                if (shell.Screen != GameShell.Page.Settings) shell.Settings();
                if (shell.Screen == GameShell.Page.Settings) shell.Back();
            }
            foreach (var key in floatKeys)
                if (existingPreferences.Contains(key)) PlayerPrefs.SetFloat(key, savedFloats[key]); else PlayerPrefs.DeleteKey(key);
            foreach (var key in intKeys)
                if (existingPreferences.Contains(key)) PlayerPrefs.SetInt(key, savedInts[key]); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); preferenceCaptured = false;
        }
        bool Clean(Vector3 chairPosition, Quaternion paintingRotation)
        {
            var session = GameSession.Current;
            var story = FindAnyObjectByType<StoryDirector>();
            var reveal = FindAnyObjectByType<V1HwacatEvent>();
            var uncat = FindAnyObjectByType<UncatAnnexEvent>();
            return session && story && reveal && uncat && session.InputAllowed && session.StoryStep == 0 &&
                !session.Finished && !session.player.Hidden && session.ExplorationCount == 0 &&
                !story.stalker.gameObject.activeSelf && story.RestorationChairCues == 0 &&
                Vector3.Distance(story.emptyChair.localPosition, chairPosition) < .001f &&
                reveal.Phase == "idle" && !reveal.normal.activeSelf && !reveal.angry.gameObject.activeSelf &&
                Quaternion.Angle(reveal.painting.rotation, paintingRotation) < .1f &&
                !uncat.Triggered && !uncat.Released && !uncat.Cancelled && !uncat.monster.gameObject.activeSelf &&
                session.GetComponent<RoomAmbience>().Voices.Count == 3 &&
                FindObjectsByType<StoryDirector>().Length == 1;
        }
        void InspectRecord(GameSession session, string id)
        {
            var record = FindObjectsByType<Interactable>()
                .FirstOrDefault(item => item.kind == Interactable.Kind.Inspect && item.stableId == id);
            if (!record) throw new InvalidOperationException("Missing authored inspection: " + id);
            record.Use(session.player);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartup + 60;
            yield return new WaitForSecondsRealtime(1);
            var result = new Result(); var session = GameSession.Current;
            var story = FindAnyObjectByType<StoryDirector>();
            var chairPosition = story.emptyChair.localPosition;
            var paintingRotation = FindAnyObjectByType<V1HwacatEvent>().painting.rotation;
            session.Collect("register"); session.Collect("ribbon");
            // The actor now walks a real occluded corner path before the roar.
            float until = Time.realtimeSinceStartup + 22; V1CyclopseIntro intro = null;
            while (Time.realtimeSinceStartup < until)
            {
                intro = story.GetComponent<V1CyclopseIntro>();
                if (intro && intro.Phase == "roar") break;
                yield return null;
            }
            result.reachedRoar = intro && intro.Phase == "roar";
            var position = story.stalker.transform.position;
            session.Shell.Pause(); yield return new WaitForSecondsRealtime(.4f);
            result.pauseFrozen = result.reachedRoar && intro.Phase == "roar" &&
                Vector3.Distance(position, story.stalker.transform.position) < .001f && AudioListener.pause;
            session.Shell.Resume(); session.Collect("record");
            if (session.requireAnnexRecords)
                foreach (var id in new[] { "music-roster", "archive-record", "nursery-tag" }) InspectRecord(session, id);
            result.annexPrerequisites = session.AnnexRecordsComplete;
            result.restorationAccepted = session.Collect("restore") && session.StoryStep == 4;
            yield return new WaitForSecondsRealtime(1.6f);
            var reveal = FindAnyObjectByType<V1HwacatEvent>();
            var uncat = FindAnyObjectByType<UncatAnnexEvent>();
            result.resolvedWithoutReactivation = intro && intro.Phase == "resolved" &&
                !story.stalker.gameObject.activeSelf && reveal.Phase == "resolved" &&
                !reveal.normal.activeSelf && !reveal.angry.gameObject.activeSelf;
            result.uncatCancelledOnRestore = uncat && uncat.Cancelled && !uncat.monster.gameObject.activeSelf;
            if (intro)
            {
                intro.Cancel(); intro.Cancel(); yield return intro.Play(story.stalker);
                result.cancellationIdempotent = intro.Phase == "resolved" && !story.stalker.gameObject.activeSelf;
            }
            session.Shell.Restart(true); yield return null; yield return new WaitForSecondsRealtime(.8f);
            result.firstRestartClean = GameSession.Current != session && Clean(chairPosition, paintingRotation);
            session = GameSession.Current;
            session.Collect("register"); session.Collect("ribbon"); session.Collect("record");
            yield return new WaitForSecondsRealtime(.2f);
            result.revealStarted = FindAnyObjectByType<V1HwacatEvent>().Phase == "paintingDrop";
            session.Shell.Restart(true); yield return null; yield return new WaitForSecondsRealtime(.8f);
            result.secondRestartClean = GameSession.Current != session && Clean(chairPosition, paintingRotation);

            // Exercise the authored Uncat warning without travel or survival assumptions.
            session = GameSession.Current; uncat = FindAnyObjectByType<UncatAnnexEvent>();
            float intensity = uncat.corridorLight.intensity;
            CapturePreference(session.Shell);
            if (!session.Shell.ReducedMotion) session.Shell.ToggleReducedMotion();
            InspectRecord(session, "archive-record");
            bool stable = uncat.Triggered && !uncat.Cancelled;
            until = Time.realtimeSinceStartup + .35f;
            while (Time.realtimeSinceStartup < until)
            {
                stable &= Mathf.Approximately(uncat.corridorLight.intensity, intensity);
                yield return null;
            }
            result.uncatComfortStable = stable && !uncat.Released;
            position = uncat.monster.transform.position;
            session.Shell.Pause(); yield return new WaitForSecondsRealtime(.3f);
            result.uncatPauseFrozen = !uncat.Released && !uncat.Cancelled && AudioListener.pause &&
                Vector3.Distance(position, uncat.monster.transform.position) < .001f &&
                Mathf.Approximately(uncat.corridorLight.intensity, intensity);
            session.Shell.Resume(); session.Finish(false); yield return null;
            result.uncatCancelledOnFinish = uncat.Cancelled && !uncat.monster.gameObject.activeSelf &&
                Mathf.Approximately(uncat.corridorLight.intensity, intensity);
            RestorePreference();
            session.Shell.Restart(true); yield return null; yield return new WaitForSecondsRealtime(.8f);
            result.thirdRestartClean = GameSession.Current != session && Clean(chairPosition, paintingRotation);
            session = GameSession.Current; uncat = FindAnyObjectByType<UncatAnnexEvent>();
            intensity = uncat.corridorLight.intensity;
            InspectRecord(session, "archive-record"); yield return new WaitForSecondsRealtime(.15f);
            bool warningStarted = uncat.Triggered && !uncat.Cancelled && !uncat.Released;
            uncat.enabled = false; yield return null;
            result.uncatCancelledOnDisable = warningStarted && uncat.Cancelled &&
                !uncat.monster.gameObject.activeSelf && Mathf.Approximately(uncat.corridorLight.intensity, intensity);

            result.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "transitions.json"), JsonUtility.ToJson(result, true));
            deadline = 0; Application.logMessageReceived -= Log;
            bool passed = typeof(Result).GetFields().Where(field => field.FieldType == typeof(bool))
                .All(field => (bool)field.GetValue(result));
            Application.Quit(passed && errors.Count == 0 ? 0 : 1);
        }
        void OnDestroy() { RestorePreference(); Application.logMessageReceived -= Log; }
    }
}
