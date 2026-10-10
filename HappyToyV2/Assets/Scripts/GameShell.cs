using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    /// <summary>Owns navigation, the gameplay input gate and local accessibility preferences.</summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class GameShell : MonoBehaviour
    {
        public enum Page { Title, Playing, Pause, Journal, Settings, Result, Records, ChapterTransition }
        public Page Screen { get; private set; } = Page.Title;
        public Font ShellFont => font;
        public float Volume => volume;
        public float Sensitivity => sensitivity;
        public float FieldOfView => fieldOfView;
        public bool ReducedMotion { get; private set; }
        public bool Subtitles { get; private set; } = true;
        public bool HighContrast { get; private set; }
        public bool LargeText { get; private set; }
        public bool NoticeVisible => noticeTime > 0 && !string.IsNullOrWhiteSpace(lastNotice);
        public string Caption { get; private set; } = string.Empty;
        public bool CaptionVisible => Subtitles && captionTime > 0 && !string.IsNullOrWhiteSpace(Caption);
        public event Action SettingsChanged;
        public bool IsReloading { get; private set; }
        public int GameplayEntryFrame { get; private set; } = -1;
        public string ReloadError { get; private set; } = string.Empty;

        Page returnPage = Page.Title;
        GameSession session;
        Font font;
        static readonly SceneRestartGate restartGate = new SceneRestartGate();
        static bool corridorRestart;
        static bool chapterRestart;
        static bool chapterFromOffering;
        static string checkpointReloadError;
        string pendingSavedToken;
        string pendingChapterToken;
        bool requestChapterOnReload;
        bool requestCorridorOnReload;
        float volume = .8f, sensitivity = .09f, fieldOfView = 72f;
        float noticeTime, captionTime;
        int lastNoticeRevision = -1, captionPriority;
        string lastNotice;
        bool preferencesDirty, auditMode, ownsFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStartupRequest() { restartGate.Cancel(); checkpointReloadError=null; chapterFromOffering=false; }

        void Awake()
        {
            // Desktop window switching keeps the current run alive. Input devices
            // still reset on focus loss, so typing in another app cannot move us.
            Application.runInBackground = true;
            session = GetComponent<GameSession>();
            LoadSettings();
            Set(Page.Title);
        }

        void Start()
        {
            // A bundled font can be supplied without changing scenes; OS fallbacks cover Windows/macOS/Linux.
            font = Resources.Load<Font>("Fonts/Korean");
            if (!font)
            {
                ownsFont = true;
                font = Font.CreateDynamicFontFromOSFont(new[] {
                    "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial"
                }, 24);
            }
            ApplySettings();
            var args = Environment.GetCommandLineArgs();
            auditMode = args.Contains("-v3-graphics-output") || args.Any(a => a.StartsWith("-v2-", StringComparison.Ordinal) && a.EndsWith("-output", StringComparison.Ordinal));
            // Existing standalone audits enter through Begin; the flow audit retains the title.
            bool autoStartAudit = auditMode && !args.Contains("-v2-flow-output") && !args.Contains("-v2-chapter-output") &&
                !args.Contains("-v2-school-play-output") && !args.Contains("-v2-corridor-play-output") && !args.Contains("-v2-cabinet-peek-output") && !args.Contains("-v2-altar-output") && !args.Contains("-v2-school-reveal-output") && !args.Contains("-v2-school-stair-output") && !args.Contains("-v2-school-campus-output") && !args.Contains("-v2-threat-visual-output") && !args.Contains("-v2-menu-face-output") && !args.Contains("-v3-graphics-output");
            // A requested return to Title takes precedence over audit auto-start.
            if (restartGate.TryConsume(gameObject.scene.path, out bool playAfterLoad))
            { if (playAfterLoad) { if (chapterRestart) BeginChapter(); else if (corridorRestart) BeginCorridor(); else Begin(); } else Set(Page.Title); }
            else if (autoStartAudit) Begin();
            else Set(Page.Title);
            if (!GetComponent<GameShellView>()) gameObject.AddComponent<GameShellView>();
            if(!string.IsNullOrEmpty(checkpointReloadError)) { ReloadError=checkpointReloadError; checkpointReloadError=null; }
        }

        void Set(Page page)
        {
            if (IsReloading) return;
            if (page == Page.Playing && (!session || session.Finished)) return;
            if (page == Page.Playing && Screen != Page.Playing) GameplayEntryFrame = Time.frameCount;
            Screen = page;
            ReloadError = string.Empty;
            ApplyScreenState();
        }
        void ApplyScreenState()
        {
            bool playing = Screen == Page.Playing && !IsReloading;
            Time.timeScale = playing ? 1 : 0;
            AudioListener.pause = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void Begin() { if (Screen == Page.Title) Set(Page.Playing); }
        public void BeginChapter()
        {
            if(Screen!=Page.Title || IsReloading) return;
            try
            {
                if(session.ChapterMode || session.CorridorMode)
                { requestChapterOnReload=true; requestCorridorOnReload=false; Restart(true); return; }
                bool storyEntry=chapterFromOffering; chapterFromOffering=false;
                session.CreateChapter();
                if(!storyEntry && session.ChapterSuspension.HasRun && session.ChapterSuspension.Writable &&
                    !session.ChapterSuspension.Consume(session.ChapterSuspension.Snapshot.token))
                { ReloadError=session.ChapterSuspension.Status; return; }
                if(session.ChapterMode) Set(Page.Playing);
            }
            catch(Exception error)
            {
                ReloadError="학교를 준비하지 못했습니다. 다시 시작해 주세요.";
                Debug.LogException(error,this);
            }
        }
        public void BeginSchoolFromOffering()
        {
            if(IsReloading || Screen!=Page.Result || !session.CorridorMode || !session.Escaped || session.Corridor.Layout.Version<3) return;
            Set(Page.ChapterTransition);
            StartCoroutine(SchoolAfterOffering());
        }
        IEnumerator SchoolAfterOffering()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if(Screen!=Page.ChapterTransition || IsReloading) yield break;
            chapterFromOffering=true; requestChapterOnReload=true; requestCorridorOnReload=false;
            Restart(true);
            if(!IsReloading)
            {
                string error=ReloadError;
                chapterFromOffering=false; requestChapterOnReload=false; requestCorridorOnReload=false;
                chapterRestart=false; corridorRestart=false;
                Set(Page.Result); ReloadError=error;
            }
        }
        public void BeginCorridor()
        {
            if (Screen != Page.Title || IsReloading) return;
            try
            {
                if(session.CorridorMode || session.ChapterMode)
                { requestCorridorOnReload=true; requestChapterOnReload=false; Restart(true); return; }
                session.CreateCorridor(unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode()));
                if(session.Suspension.HasRun && session.Suspension.Writable && !session.Suspension.Consume(session.Suspension.Snapshot.token))
                { ReloadError=session.Suspension.Status; return; }
                if (session.CorridorMode) Set(Page.Playing);
            }
            catch (Exception error)
            {
                ReloadError = "회랑을 준비하지 못했습니다. 다시 시작해 주세요.";
                Debug.LogException(error, this);
            }
        }
        public void SuspendCorridor()
        {
            if(Screen!=Page.Pause || IsReloading || !session.CorridorMode) return;
            try
            {
                var checkpoint=session.CaptureCheckpoint();
                if(!session.Suspension.Save(checkpoint))
                { ReloadError=session.Suspension.Status; return; }
                pendingSavedToken=checkpoint.token;
                Restart(false);
            }
            catch(InvalidOperationException error) { ReloadError=error.Message; }
            catch(ArgumentException) { ReloadError="현재 탐색 상태를 안전하게 저장할 수 없습니다. 탐색을 계속한 뒤 다시 시도하세요."; }
        }
        public void SuspendChapter()
        {
            if(Screen!=Page.Pause || IsReloading || !session.ChapterMode) return;
            try
            {
                var checkpoint=session.CaptureChapterCheckpoint();
                if(!session.ChapterSuspension.Save(checkpoint)) { ReloadError=session.ChapterSuspension.Status; return; }
                pendingChapterToken=checkpoint.token; Restart(false);
            }
            catch(InvalidOperationException error) { ReloadError=error.Message; }
            catch(ArgumentException) { ReloadError="현재 학교 상태를 안전하게 저장할 수 없습니다. 탐색을 계속한 뒤 다시 시도하세요."; }
        }
        public void ContinueChapter()
        {
            if(Screen!=Page.Title || IsReloading || !session.ChapterSuspension.HasRun || !session.ChapterSuspension.Writable) return;
            var data=session.ChapterSuspension.Snapshot;
            StartCoroutine(ContinueChapterAfterNavigation(data));
        }
        IEnumerator ContinueChapterAfterNavigation(ChapterCheckpoint data)
        {
            IsReloading=true; ApplyScreenState(); Exception failure=null;
            try
            {
                session.CreateChapterForCheckpoint(data); session.Chapter.PrepareCheckpointNavigation(data);
            }
            catch(Exception error) when(error is ArgumentException || error is InvalidOperationException) { failure=error; }
            if(failure==null)
            {
                yield return null; yield return null;
                try
                {
                    session.ApplyChapterCheckpoint(data);
                    if(!session.ChapterSuspension.Consume(data.token)) throw new InvalidOperationException(session.ChapterSuspension.Status);
                }
                catch(Exception error) when(error is ArgumentException || error is InvalidOperationException) { failure=error; }
            }
            IsReloading=false;
            if(failure==null) Set(Page.Pause);
            else
            {
                Debug.LogWarning("HappyToy school restore failed: "+failure.Message,this);
                ReloadError="학교 탐색을 복원하지 못했습니다. 중단 기록을 보존했습니다.";
                if(session.ChapterMode) { checkpointReloadError=ReloadError; Restart(false); }
                else ApplyScreenState();
            }
        }
        public void ContinueCorridor()
        {
            if(Screen!=Page.Title || IsReloading || !session.Suspension.HasRun || !session.Suspension.Writable) return;
            var data=session.Suspension.Snapshot;
            try
            {
                session.CreateCorridorForCheckpoint(data); session.ApplyCheckpoint(data);
                if(!session.Suspension.Consume(data.token)) throw new InvalidOperationException(session.Suspension.Status);
                // Give the player control of when restored danger starts moving.
                Set(Page.Pause);
            }
            catch(Exception error) when(error is ArgumentException || error is InvalidOperationException)
            {
                ReloadError="중단한 탐색을 복원하지 못했습니다. 기록을 보존했습니다.";
                if(session.CorridorMode) { checkpointReloadError=ReloadError; Restart(false); }
            }
        }
        public void Pause() { if (Screen == Page.Playing) Set(Page.Pause); }
        public void Resume()
        {
            if(Screen!=Page.Pause) return;
            // If saving succeeded but scene reload failed, continuing this live
            // run must consume its checkpoint too, rather than leaving a rewind.
            if(pendingSavedToken!=null && !session.Suspension.Consume(pendingSavedToken) && !session.Suspension.Conflict)
            { ReloadError=session.Suspension.Status; return; }
            if(pendingChapterToken!=null && !session.ChapterSuspension.Consume(pendingChapterToken) && !session.ChapterSuspension.Conflict)
            { ReloadError=session.ChapterSuspension.Status; return; }
            pendingSavedToken=pendingChapterToken=null; Set(Page.Playing);
        }
        public void Journal()
        {
            if (Screen != Page.Playing && Screen != Page.Pause) return;
            returnPage = Screen;
            Set(Page.Journal);
        }
        public void Settings()
        {
            // Prevent repeated clicks from replacing the return destination with Settings itself.
            if (Screen == Page.Settings || Screen == Page.Journal || Screen==Page.Records) return;
            returnPage = Screen;
            Set(Page.Settings);
        }
        public void Back()
        {
            if (Screen != Page.Settings && Screen != Page.Journal && Screen!=Page.Records) return;
            if (Screen == Page.Settings) SaveSettings();
            Set(returnPage);
        }
        public void ChapterHistory()
        {
            if(IsReloading || Screen!=Page.Title && Screen!=Page.Result) return;
            returnPage=Screen; Set(Page.Records);
        }
        public void ShowResult()
        {
            captionTime = 0;
            Set(Page.Result);
        }
        public void Restart(bool play)
        {
            if (IsReloading || restartGate.Pending) return;
            var scene = gameObject.scene;
            bool loadable = !string.IsNullOrWhiteSpace(scene.path) &&
                (scene.buildIndex >= 0 ? Application.CanStreamedLevelBeLoaded(scene.buildIndex) : Application.CanStreamedLevelBeLoaded(scene.path));
            if (!loadable)
            {
                ReloadError = "현재 장면을 다시 열 수 없습니다. 저장된 씬과 빌드 장면 목록을 확인하세요. 탐색 기록은 유지됩니다.";
                return;
            }
            try
            {
                SaveSettings();
                chapterRestart = requestChapterOnReload || !requestCorridorOnReload && session && session.ChapterMode;
                corridorRestart = requestCorridorOnReload || !chapterRestart && session && session.CorridorMode;
                if (!restartGate.TryRequest(scene.path, play)) return;
                IsReloading = true; ReloadError = string.Empty; ApplyScreenState();
                if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
                else SceneManager.LoadScene(scene.path);
            }
            catch (Exception error)
            {
                restartGate.Cancel(); IsReloading = false;
                ReloadError = "다시 시작하지 못했습니다. 탐색 기록은 유지됩니다. 잠시 후 다시 시도하세요.";
                ApplyScreenState();
                Debug.LogWarning("HappyToy restart failed: " + error.Message, this);
            }
        }

        public void AdjustSettings(float sound, float mouse)
        {
            volume = Mathf.Clamp01(volume + sound);
            sensitivity = Mathf.Clamp(sensitivity + mouse, .03f, .18f);
            ChangedSettings();
        }
        public void AdjustFieldOfView(float amount)
        {
            fieldOfView = Mathf.Clamp(fieldOfView + amount, 60f, 100f);
            ChangedSettings();
        }
        public void ToggleReducedMotion() { ReducedMotion = !ReducedMotion; ChangedSettings(); }
        public void ToggleSubtitles() { Subtitles = !Subtitles; ChangedSettings(); }
        public void ToggleHighContrast() { HighContrast = !HighContrast; ChangedSettings(); }
        public void ToggleLargeText() { LargeText = !LargeText; ChangedSettings(); }
        public void RestoreDefaultSettings()
        {
            volume = .8f; sensitivity = .09f; fieldOfView = 72f;
            ReducedMotion = HighContrast = LargeText = false;
            Subtitles = true;
            ChangedSettings();
        }
        void ChangedSettings()
        {
            preferencesDirty = true;
            ApplySettings();
            SettingsChanged?.Invoke();
        }
        void LoadSettings()
        {
            volume = ReadPreference("v2.volume", .8f, 0f, 1f);
            sensitivity = ReadPreference("v2.sensitivity", .09f, .03f, .18f);
            fieldOfView = ReadPreference("v2.fov", 72f, 60f, 100f);
            ReducedMotion = PlayerPrefs.GetInt("v2.reducedMotion", 0) != 0;
            Subtitles = PlayerPrefs.GetInt("v2.subtitles", 1) != 0;
            HighContrast = PlayerPrefs.GetInt("v2.highContrast", 0) != 0;
            LargeText = PlayerPrefs.GetInt("v2.largeText", 0) != 0;
        }
        static float ReadPreference(string key, float fallback, float min, float max)
        {
            float value = PlayerPrefs.GetFloat(key, fallback);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
        void ApplySettings()
        {
            AudioListener.volume = DiagnosticAudioSilence.FilterVolume(volume);
            if (!session || !session.player) return;
            session.player.sensitivity = sensitivity;
            if (session.player.eyes) session.player.eyes.fieldOfView = fieldOfView;
        }
        void SaveSettings()
        {
            if (!preferencesDirty) return;
            PlayerPrefs.SetFloat("v2.volume", volume);
            PlayerPrefs.SetFloat("v2.sensitivity", sensitivity);
            PlayerPrefs.SetFloat("v2.fov", fieldOfView);
            PlayerPrefs.SetInt("v2.reducedMotion", ReducedMotion ? 1 : 0);
            PlayerPrefs.SetInt("v2.subtitles", Subtitles ? 1 : 0);
            PlayerPrefs.SetInt("v2.highContrast", HighContrast ? 1 : 0);
            PlayerPrefs.SetInt("v2.largeText", LargeText ? 1 : 0);
            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        /// <summary>Call for a sound actually emitted near the player, never to reveal an unseen actor.</summary>
        public void ShowCaption(string text, float duration = 2.5f, int priority = 0)
        {
            // Corridor threat sounds speak for themselves; omit instructional approach warnings.
            if (session.CorridorMode && priority >= 2) return;
            if (Screen != Page.Playing || string.IsNullOrWhiteSpace(text)) return;
            if (captionTime > 0 && priority < captionPriority) return;
            captionPriority = priority;
            Caption = text;
            captionTime = Mathf.Clamp(duration, .5f, 12f);
        }

        void Update()
        {
            if (!session || IsReloading) return;
            if (lastNotice != session.Notice || lastNoticeRevision != session.NoticeRevision)
            {
                lastNotice = session.Notice;
                lastNoticeRevision = session.NoticeRevision;
                noticeTime = string.IsNullOrWhiteSpace(lastNotice) ? 0 : Mathf.Clamp(4f + lastNotice.Length * .12f, 7f, 16f);
            }
            if (Screen == Page.Playing)
            {
                noticeTime = Mathf.Max(0, noticeTime - Time.deltaTime);
                captionTime = Mathf.Max(0, captionTime - Time.deltaTime);
            }
            if (!PlayerControls.AcceptsInput) return;
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            // UI Toolkit alone owns submit; Enter must activate the focused button exactly once.
            if (keys != null && keys.escapeKey.wasPressedThisFrame || pad != null &&
                (pad.startButton.wasPressedThisFrame || Screen != Page.Playing && pad.buttonEast.wasPressedThisFrame))
            {
                if (Screen == Page.Playing) Pause();
                else if (Screen == Page.Pause) Resume();
                else if (Screen == Page.Journal || Screen == Page.Settings || Screen==Page.Records) Back();
            }
            else if (keys != null && keys.jKey.wasPressedThisFrame || pad != null && pad.selectButton.wasPressedThisFrame)
            {
                if (Screen == Page.Playing || Screen == Page.Pause) Journal();
                else if (Screen == Page.Journal) Back();
            }
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) ApplyScreenState();
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        // OS window/minimize notifications do not own the explicit Esc menu.
        void OnApplicationPause(bool paused) { if (!paused) ApplyScreenState(); }
        void OnApplicationQuit() { SaveSettings(); }
        void OnDestroy()
        {
            SaveSettings();
            // A departing scene must not unpause a newly created shell.
            if (!GameSession.Current || GameSession.Current.Shell == this)
            {
                Time.timeScale = 1;
                AudioListener.pause = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (ownsFont && font) Destroy(font);
        }
    }
}
