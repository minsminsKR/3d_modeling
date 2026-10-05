using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in preserved-annex UI audit. Main corridor entry has separate flow evidence.
    public sealed class FlowAudit : MonoBehaviour
    {
        string output;
        Keyboard keys;
        int renderedFrames, captureAttempts;
        float deadline;
        bool preferencesCaptured;
        InputSettings.BackgroundBehavior oldBackgroundBehavior;
        readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
        readonly HashSet<string> existingPreferences = new HashSet<string>();
        readonly List<string> errors = new List<string>();
        static readonly string[] floatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        static readonly string[] intKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };

        [Serializable]
        class Result
        {
            public string scope = "Preserved authored annex via explicit Begin(); excludes the new main corridor title entry.";
            public bool titleFrozen, started, journalPaused, journalGated, journalFrozen, resumed,
                pauseOpened, pauseJournalReturns, pauseSettingsReturns, resultOpened, restartClean,
                pointerSettings, pointerVolume, pointerSensitivity, pointerFieldOfView, pointerToggles,
                settingsDefaults, settingsPersisted, compactLayout, inspectionStored,
                inspectionDeduplicated, inspectionDisplayed, journalPaging, inspectionReset,
                restartRequestsDeduplicated, restartResourcesClean, restartTitleDestination;
            public int discoveredInspections, expectedInspections, journalPagesVisited, uiFramesRendered, captureAttempts;
            public string[] errors;
        }

        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }
        void Update()
        {
            if (deadline <= 0 || Time.realtimeSinceStartup <= deadline) return;
            deadline = 0;
            File.WriteAllText(Path.Combine(output, "flow-timeout.txt"), "Flow audit timed out. No passing result was produced.");
            RestorePreferences();
            Application.Quit(2);
        }
        void CapturePreferences()
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
            preferencesCaptured = true;
        }
        void RestorePreferences()
        {
            if (!preferencesCaptured) return;
            // Flush any dirty settings first. Otherwise GameShell.OnApplicationQuit could
            // overwrite the restored preferences after a timeout inside Settings.
            if (GameSession.Current && GameSession.Current.Shell && GameSession.Current.Shell.Screen == GameShell.Page.Settings)
                GameSession.Current.Shell.Back();
            preferencesCaptured = false;
            foreach (var key in floatKeys)
                if (existingPreferences.Contains(key)) PlayerPrefs.SetFloat(key, savedFloats[key]); else PlayerPrefs.DeleteKey(key);
            foreach (var key in intKeys)
                if (existingPreferences.Contains(key)) PlayerPrefs.SetInt(key, savedInts[key]); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        void OnDestroy()
        {
            RestorePreferences();
            if (keys != null)
            {
                if (keys.added) InputSystem.RemoveDevice(keys);
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
            }
        }
        bool LayoutFits()
        {
            var view = GameSession.Current.GetComponent<GameShellView>();
            var bounds = view.Root.worldBound;
            bool fits = bounds.width > 0 && bounds.height > 0;
            view.Root.Query<Button>().ForEach(button => fits &= Fits(bounds, button));
            view.Root.Query<Label>().ForEach(label => fits &= Fits(bounds, label));
            return fits;
        }
        static bool Fits(Rect bounds, VisualElement element)
        {
            if (element.resolvedStyle.display == DisplayStyle.None) return true;
            var r = element.worldBound;
            // A one-pixel tolerance absorbs pixel snapping; it cannot hide a clipped row.
            return !float.IsNaN(r.x) && !float.IsNaN(r.y) && r.xMin >= bounds.xMin - 1 &&
                r.yMin >= bounds.yMin - 1 && r.xMax <= bounds.xMax + 1 && r.yMax <= bounds.yMax + 1;
        }
        IEnumerator Click(string id)
        {
            var view = GameSession.Current.GetComponent<GameShellView>();
            var button = view.Root.Q<Button>(id);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("Missing or disabled UI button: " + id);
            var position = button.worldBound.center;
            var target = view.Root.panel.Pick(position);
            if (target == null || (target != button && !button.Contains(target)))
                throw new InvalidOperationException("Button is obscured at pointer position: " + id);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = position, button = 0, clickCount = 1 })) target.SendEvent(down);
            yield return new WaitForSecondsRealtime(.04f);
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 0, clickCount = 1 })) target.SendEvent(up);
            yield return new WaitForSecondsRealtime(.15f);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v2-flow-output");
            if (i < 0 || i + 1 >= args.Length) return;
            Application.runInBackground = true;
            var go = new GameObject("Flow audit"); DontDestroyOnLoad(go); go.AddComponent<FlowAudit>().output = args[i + 1];
        }
        IEnumerator Key(Key key)
        {
            InputSystem.QueueStateEvent(keys, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return new WaitForSecondsRealtime(.08f);
        }
        IEnumerator Capture(string name)
        {
            captureAttempts++;
            yield return new WaitForSecondsRealtime(.15f); yield return new WaitForEndOfFrame();
            var target = GameSession.Current.GetComponent<GameShellView>().CaptureTarget;
            if (!target) throw new InvalidOperationException("Flow audit requires a rendered player and CaptureTarget.");
            var previous = RenderTexture.active; RenderTexture.active = target;
            var frame = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); frame.Apply(); RenderTexture.active = previous;
            bool visible = false;
            for (int y = 0; y < frame.height && !visible; y += 24)
                for (int x = 0; x < frame.width && !visible; x += 24) visible = frame.GetPixel(x, y).maxColorComponent > .05f;
            if (visible) renderedFrames++;
            File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); Destroy(frame);
        }
        IEnumerator CheckJournal(Result result, GameSession session, int expected)
        {
            var seen = new HashSet<int>();
            bool contentMatches = true, navigationMatches = true;
            for (int page = 0; page <= expected + 1; page++)
            {
                var root = session.GetComponent<GameShellView>().Root;
                root.Query<Label>().ForEach(label =>
                {
                    if (label.name == null || !label.name.StartsWith("inspection-")) return;
                    if (!int.TryParse(label.name.Substring("inspection-".Length), out var index)) { contentMatches = false; return; }
                    if (index >= expected) return; // Empty last-page slots may have placeholder labels.
                    contentMatches &= label.text == session.ExplorationEntry(index);
                    seen.Add(index);
                });
                result.journalPagesVisited++;
                var previous = root.Q<Button>("journal-previous");
                if (page == 0 && previous != null) navigationMatches &= !previous.enabledInHierarchy;
                var next = root.Q<Button>("journal-next");
                if (next == null || !next.enabledInHierarchy) break;
                if (page == expected + 1) { navigationMatches = false; break; }
                yield return Click("journal-next");
                yield return Capture("journal-page-" + (page + 2));
            }
            result.inspectionDisplayed = contentMatches && seen.Count == expected;
            for (int page = result.journalPagesVisited - 1; page > 0; page--) yield return Click("journal-previous");
            var firstRoot = session.GetComponent<GameShellView>().Root;
            navigationMatches &= firstRoot.Q<Label>("record-0")?.text == session.JournalEntry(0);
            var firstPrevious = firstRoot.Q<Button>("journal-previous");
            navigationMatches &= firstPrevious == null || !firstPrevious.enabledInHierarchy;
            result.journalPaging = navigationMatches && (expected <= 2 || result.journalPagesVisited > 1);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keys = InputSystem.AddDevice<Keyboard>(); deadline = Time.realtimeSinceStartup + 90; CapturePreferences();
            yield return new WaitForSecondsRealtime(1);
            var session = GameSession.Current; var shell = session.Shell; var player = session.player; var result = new Result();
            yield return Key(UnityEngine.InputSystem.Key.W);
            result.titleFrozen = shell.Screen == GameShell.Page.Title && Time.timeScale == 0 && player.MovementUpdates == 0 && !session.Collect("register");
            yield return Capture("title");
            yield return Click("settings"); result.pointerSettings = shell.Screen == GameShell.Page.Settings;
            yield return Click("defaults");
            result.settingsDefaults = Mathf.Approximately(shell.Volume, .8f) && Mathf.Approximately(shell.Sensitivity, .09f) &&
                Mathf.Approximately(shell.FieldOfView, 72) && !shell.ReducedMotion && shell.Subtitles && !shell.HighContrast && !shell.LargeText;
            float originalVolume = shell.Volume;
            yield return Click("volume-up"); result.pointerVolume = Mathf.Abs(shell.Volume - originalVolume) > .04f;
            float originalSensitivity = shell.Sensitivity;
            yield return Click("mouse-up"); result.pointerSensitivity = Mathf.Abs(shell.Sensitivity - originalSensitivity) > .008f;
            float originalFov = shell.FieldOfView;
            yield return Click("fov-up"); result.pointerFieldOfView = shell.FieldOfView > originalFov;
            yield return Click("reduced-motion"); yield return Click("subtitles"); yield return Click("high-contrast"); yield return Click("large-text");
            result.pointerToggles = shell.ReducedMotion && !shell.Subtitles && shell.HighContrast && shell.LargeText;
            float changedVolume = shell.Volume, changedSensitivity = shell.Sensitivity, changedFov = shell.FieldOfView;
            yield return Capture("settings");
            var view = session.GetComponent<GameShellView>();
            view.SetCaptureSize(1280, 720); yield return Capture("settings-720p"); result.compactLayout = LayoutFits();
            view.SetCaptureSize(1024, 768); yield return Capture("settings-4x3"); result.compactLayout &= LayoutFits();
            view.SetCaptureSize(1600, 900); yield return null; yield return Click("back");
            // This downstream audit inspects original annex records and actors.
            // The title's primary entry now starts the separate seeded corridor.
            shell.Begin(); yield return null;
            result.started = session.InputAllowed && Time.timeScale == 1;
            if (!result.started)
            {
                errors.Add("Explicit preserved annex did not begin; downstream checks were not run."); yield return Capture("start-input-failed");
                Save(result); RestorePreferences(); Application.Quit(1); yield break;
            }
            var notes = FindObjectsByType<Interactable>(FindObjectsSortMode.None)
                .Where(item => item.kind == Interactable.Kind.Inspect && !string.IsNullOrWhiteSpace(item.inspectionText)).OrderBy(item => item.name).ToArray();
            var uniqueNotes = notes.GroupBy(item => string.IsNullOrEmpty(item.stableId) ? item.name : item.stableId).Select(group => group.First()).ToArray();
            result.discoveredInspections = notes.Length; result.expectedInspections = uniqueNotes.Length;
            foreach (var note in notes) note.Use(player);
            result.inspectionStored = uniqueNotes.Length > 0 && session.ExplorationCount == uniqueNotes.Length && session.StoryStep == 0;
            foreach (var note in notes) note.Use(player);
            result.inspectionDeduplicated = session.ExplorationCount == uniqueNotes.Length;
            session.Collect("register"); yield return Key(UnityEngine.InputSystem.Key.J);
            result.journalPaused = shell.Screen == GameShell.Page.Journal && Time.timeScale == 0 && AudioListener.pause;
            result.journalGated = session.JournalEntry(0) != null && session.JournalEntry(1) == null;
            var position = player.transform.position; int movements = player.MovementUpdates; float stamina = player.Stamina;
            yield return Key(UnityEngine.InputSystem.Key.W);
            result.journalFrozen = player.transform.position == position && player.MovementUpdates == movements && player.Stamina == stamina;
            yield return CheckJournal(result, session, uniqueNotes.Length); yield return Capture("journal");
            view.SetCaptureSize(1280, 720); yield return Capture("journal-720p"); result.compactLayout &= LayoutFits();
            view.SetCaptureSize(1024, 768); yield return Capture("journal-4x3"); result.compactLayout &= LayoutFits();
            view.SetCaptureSize(1600, 900);
            yield return Key(UnityEngine.InputSystem.Key.Escape); result.resumed = session.InputAllowed && !AudioListener.pause;
            yield return Key(UnityEngine.InputSystem.Key.Escape); result.pauseOpened = shell.Screen == GameShell.Page.Pause; yield return Capture("pause");
            yield return Click("journal"); yield return Key(UnityEngine.InputSystem.Key.Escape);
            result.pauseJournalReturns = shell.Screen == GameShell.Page.Pause && Time.timeScale == 0;
            yield return Click("settings"); yield return Key(UnityEngine.InputSystem.Key.Escape);
            result.pauseSettingsReturns = shell.Screen == GameShell.Page.Pause && Time.timeScale == 0;
            shell.Resume(); player.TrySetCrouching(true); player.ApplyCurse(10);
            session.Finish(false); result.resultOpened = shell.Screen == GameShell.Page.Result && Time.timeScale == 0; yield return Capture("result");
            shell.Restart(true);
            bool pending = shell.IsReloading && !session.InputAllowed;
            shell.Restart(false); // Same-frame stale callback must not replace the first intent.
            yield return null; yield return new WaitForSecondsRealtime(.5f);
            var current = GameSession.Current;
            result.restartClean = current != session && current.InputAllowed && current.StoryStep == 0 && !current.Finished && !current.player.Hidden;
            result.inspectionReset = current.ExplorationCount == 0;
            result.restartRequestsDeduplicated = pending && current.InputAllowed && !current.Shell.IsReloading;
            result.restartResourcesClean = !current.player.Crouching && current.player.SlowRemaining == 0 &&
                current.player.Stamina > .95f && current.player.Firecrackers.Count == 2 && current.player.FootstepNoiseRadius == 0;
            var restarted = current.Shell;
            result.settingsPersisted = Mathf.Approximately(restarted.Volume, changedVolume) && Mathf.Approximately(restarted.Sensitivity, changedSensitivity) &&
                Mathf.Approximately(current.player.sensitivity, changedSensitivity) && Mathf.Approximately(restarted.FieldOfView, changedFov) &&
                Mathf.Approximately(current.player.eyes.fieldOfView, changedFov) &&
                restarted.ReducedMotion && !restarted.Subtitles && restarted.HighContrast && restarted.LargeText;
            restarted.Restart(false); restarted.Restart(true);
            yield return null; yield return new WaitForSecondsRealtime(.5f);
            result.restartTitleDestination = GameSession.Current != current && GameSession.Current.Shell.Screen == GameShell.Page.Title &&
                !GameSession.Current.InputAllowed && Time.timeScale == 0 && AudioListener.pause && UnityEngine.Cursor.visible;
            Save(result); RestorePreferences(); deadline = 0;
            // Every boolean is a required assertion, including new assertions added in future.
            bool passed = typeof(Result).GetFields().Where(field => field.FieldType == typeof(bool)).All(field => (bool)field.GetValue(result));
            Application.Quit(passed && errors.Count == 0 && captureAttempts >= 9 && renderedFrames == captureAttempts ? 0 : 1);
        }
        void Save(Result result)
        {
            result.uiFramesRendered = renderedFrames; result.captureAttempts = captureAttempts; result.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "flow.json"), JsonUtility.ToJson(result, true));
        }
    }
}
