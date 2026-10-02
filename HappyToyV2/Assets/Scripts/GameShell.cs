using System;
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
        public enum Page { Title, Playing, Pause, Journal, Settings, Result }
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

        Page returnPage = Page.Title;
        GameSession session;
        Font font;
        static bool beginAfterLoad;
        float volume = .8f, sensitivity = .09f, fieldOfView = 72f;
        float noticeTime, captionTime;
        int lastNoticeRevision = -1, captionPriority;
        string lastNotice;
        bool preferencesDirty, auditMode, ownsFont;

        void Awake()
        {
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
            auditMode = args.Any(a => a.StartsWith("-v2-", StringComparison.Ordinal) && a.EndsWith("-output", StringComparison.Ordinal));
            // Existing standalone audits enter through Begin; the flow audit retains the title.
            bool autoStartAudit = auditMode && !args.Contains("-v2-flow-output");
            if (beginAfterLoad || autoStartAudit) { beginAfterLoad = false; Begin(); }
            else Set(Page.Title);
            if (!GetComponent<GameShellView>()) gameObject.AddComponent<GameShellView>();
        }

        void Set(Page page)
        {
            if (page == Page.Playing && (!session || session.Finished)) return;
            Screen = page;
            bool playing = page == Page.Playing;
            Time.timeScale = playing ? 1 : 0;
            AudioListener.pause = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void Begin() { if (Screen == Page.Title) Set(Page.Playing); }
        public void Pause() { if (Screen == Page.Playing) Set(Page.Pause); }
        public void Resume() { if (Screen == Page.Pause) Set(Page.Playing); }
        public void Journal()
        {
            if (Screen != Page.Playing && Screen != Page.Pause) return;
            returnPage = Screen;
            Set(Page.Journal);
        }
        public void Settings()
        {
            // Prevent repeated clicks from replacing the return destination with Settings itself.
            if (Screen == Page.Settings || Screen == Page.Journal) return;
            returnPage = Screen;
            Set(Page.Settings);
        }
        public void Back()
        {
            if (Screen != Page.Settings && Screen != Page.Journal) return;
            if (Screen == Page.Settings) SaveSettings();
            Set(returnPage);
        }
        public void ShowResult()
        {
            captionTime = 0;
            Set(Page.Result);
        }
        public void Restart(bool play)
        {
            SaveSettings();
            beginAfterLoad = play;
            var scene = SceneManager.GetActiveScene();
            // Scene paths also support an authored scene opened directly in the editor.
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else if (!string.IsNullOrEmpty(scene.path)) SceneManager.LoadScene(scene.path);
            else Debug.LogWarning("HappyToy: save the active scene before restarting.", this);
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
            AudioListener.volume = volume;
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
            if (Screen != Page.Playing || string.IsNullOrWhiteSpace(text)) return;
            if (captionTime > 0 && priority < captionPriority) return;
            captionPriority = priority;
            Caption = text;
            captionTime = Mathf.Clamp(duration, .5f, 12f);
        }

        void Update()
        {
            if (!session) return;
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
            var keys = Keyboard.current;
            if (keys == null) return;
            // UI Toolkit alone owns submit; Enter must activate the focused button exactly once.
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (Screen == Page.Playing) Pause();
                else if (Screen == Page.Pause) Resume();
                else if (Screen == Page.Journal || Screen == Page.Settings) Back();
            }
            else if (keys.jKey.wasPressedThisFrame)
            {
                if (Screen == Page.Playing || Screen == Page.Pause) Journal();
                else if (Screen == Page.Journal) Back();
            }
        }
        void OnApplicationFocus(bool focused) { if (!focused && !auditMode) Pause(); }
        void OnApplicationPause(bool paused) { if (paused && !auditMode) Pause(); }
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
