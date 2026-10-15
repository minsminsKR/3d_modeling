using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    /// <summary>Scene-independent, keyboard-accessible UI. All measurements use a letterboxed safe area.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed partial class GameShellView : MonoBehaviour
    {
        GameShell shell;
        GameSession session;
        UIDocument document;
        PanelSettings settings;
        VisualElement threatVeil;
        VisualElement menuFaceBackground;
        VisualElement transitionVeil;
        Texture2D menuFaceTexture;
        CinematicTitleScene titleScene;
        public bool TitleUsesBaeksugwi => titleScene && titleScene.UsesBaeksugwi;
        VisualElement root, stage;
        Label navigationHelp;
        ShellLayout layout;
        ShellPauseScreen pauseScreen;
        ShellSettingsScreen settingsScreen;
        ShellHudView hud;
        readonly ShellJournalScreen journalScreen = new ShellJournalScreen();
        readonly ShellRecordsScreen recordsScreen = new ShellRecordsScreen();
        bool sendingGamepadNavigation;
        bool sendingKeyboardSubmit;
        Vector2 previousMenuDirection;
        float nextMenuRepeat;
        bool GamepadMenuIntent => Gamepad.current != null && (PlayerControls.UsingGamepad || Gamepad.current.buttonSouth.isPressed ||
            Gamepad.current.dpad.ReadValue().sqrMagnitude > .1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > .25f);
        bool KeyboardSubmitIntent => Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed);
        string NavigationHelp => PlayerControls.UsingGamepad ? "방향 패드  선택     아래 버튼  확인     오른쪽 버튼  뒤로" : "Tab / 방향키  선택      Enter  확인      Esc  뒤로";
        string ControlInstructions => PlayerControls.UsingGamepad ? "왼쪽 스틱  이동 / 누르며 달리기\n오른쪽 스틱  시선\n아래 버튼  조사·문·은신\n오른쪽 버튼  낮은 자세 / 위 버튼  빛\nLT 조준 / RT 콩알탄 (추격 전 유인)\nSelect 기록 / Start 일시정지" : "WASD  이동       마우스  시선\nShift  달리기     C / Ctrl  낮은 자세\nE  조사·문·은신·배터리·촛불\nF  손전등          Q  콩알탄 (추격 전 유인)\n우클릭 누르기  콩알탄 첫 충돌 조준\nJ  조사 기록      Esc  일시정지";
        string ControlsFooter => PlayerControls.UsingGamepad ? "오른쪽  낮은 자세    위  빛    RT  콩알탄    Start  메뉴" : "C  낮은 자세    F  빛    Q  콩알탄    J  기록    Esc  메뉴";
        GameShell.Page shown = (GameShell.Page)(-1);
        int journalStep = -1, journalExploration = -1;
        bool shownContrast, shownLargeText, shownGamepad, settingsLabelsDirty;
        bool shownReloading;
        string shownReloadError;

        public RenderTexture CaptureTarget { get; private set; }
        public VisualElement Root => root;
        public const string MenuFaceResourcePath = "Menu/cyclopse-menace-v1";
        public Texture2D MenuFaceTexture => menuFaceTexture;
        public bool MenuFaceVisible => menuFaceTexture && menuFaceBackground != null &&
            menuFaceBackground.style.display.value != DisplayStyle.None;
        Color Paper => shell && shell.HighContrast ? Color.white : new Color(.91f, .89f, .81f);
        Color Gold => layout.Gold;
        Color Edge => layout.Edge;
        Color Rust => layout.Rust;
        Color CardSurface => layout.CardSurface;
        void Start()
        {
            shell = GetComponent<GameShell>();
            session = GetComponent<GameSession>();
            if (!shell || !session) { enabled = false; return; }
            EnsurePanelSettings();
            if (Environment.GetCommandLineArgs().Contains("-v2-flow-output") && !CaptureTarget) SetCaptureSize(1600, 900);
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            root = document.rootVisualElement;
            root.name = "game-shell";
            // Own gamepad event production explicitly, including offscreen panels.
            // Filter the native provider's duplicate events before controls receive them.
            root.RegisterCallback<NavigationMoveEvent>(e => { if (!sendingGamepadNavigation && GamepadMenuIntent) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e =>
            {
                if (!sendingGamepadNavigation && !sendingKeyboardSubmit && (GamepadMenuIntent || KeyboardSubmitIntent))
                    e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(e => { if (!sendingGamepadNavigation && GamepadMenuIntent) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.overflow = Overflow.Hidden;
            if (shell.ShellFont) root.style.unityFont = shell.ShellFont;
            root.style.fontSize = 22;
            shell.SettingsChanged += OnSettingsChanged;
            Rebuild();
        }

        void OnSettingsChanged() { settingsLabelsDirty = true; }

        void EnsurePanelSettings()
        {
            if (settings) return;
            var template = Resources.Load<PanelSettings>("GamePanel");
            settings = template ? Instantiate(template) : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "HappyToy runtime UI panel";
            // Expand keeps every button within the screen at 4:3, 16:9 and ultrawide aspect ratios.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
            settings.sortingOrder = 100;
        }

        public void SetCaptureSize(int width, int height)
        {
            EnsurePanelSettings();
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;
            if (CaptureTarget && CaptureTarget.width == width && CaptureTarget.height == height) return;
            settings.targetTexture = null;
            if (CaptureTarget) { CaptureTarget.Release(); Destroy(CaptureTarget); }
            CaptureTarget = new RenderTexture(width, height, 24) { graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, name = "HappyToy display RGB UI capture" };
            CaptureTarget.Create();
            settings.targetTexture = CaptureTarget;
        }

        public void RestoreDisplayOutput()
        {
            EnsurePanelSettings(); settings.targetTexture = null; settings.clearColor = false;
        }

        // Kept as the title adapter's small construction surface; the layout owns styling.
        VisualElement Place(VisualElement element, float x, float y, float width, float height) => layout.Place(element, x, y, width, height);
        Label Text(string text, float x, float y, float width, float height, int size = 22) => layout.Text(text, x, y, width, height, size);
        VisualElement Panel(float x, float y, float width, float height, Color color) => layout.Panel(x, y, width, height, color);
        void Outline(VisualElement element, Color color, float width = 1) => layout.Outline(element, color, width);
        Label Small(string text, float x, float y, float width, float height = 28) => layout.Small(text, x, y, width, height);

        void BuildMenuFaceBackground()
        {
            // Resources owns this texture. Rebuilding a menu must never destroy it.
            if (!menuFaceTexture) menuFaceTexture = Resources.Load<Texture2D>(MenuFaceResourcePath);
            bool visible = menuFaceTexture && shown != GameShell.Page.Playing && shown != GameShell.Page.Pause && shown != GameShell.Page.ChapterTransition;
            menuFaceBackground = new VisualElement { name = "menu-face-background", pickingMode = PickingMode.Ignore, focusable = false };
            menuFaceBackground.style.position = Position.Absolute;
            menuFaceBackground.style.left = menuFaceBackground.style.top = menuFaceBackground.style.right = menuFaceBackground.style.bottom = 0;
            menuFaceBackground.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!titleScene) { titleScene = gameObject.AddComponent<CinematicTitleScene>(); titleScene.Initialize(shell); }
            titleScene.SetVisible(shown == GameShell.Page.Title);
            menuFaceBackground.style.backgroundImage = shown == GameShell.Page.Title
                ? new StyleBackground(Background.FromRenderTexture(titleScene.Image)) : new StyleBackground(menuFaceTexture);
            // Fill the viewport independently of the safe area, preserving the face's proportions.
            menuFaceBackground.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            menuFaceBackground.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            menuFaceBackground.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            menuFaceBackground.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            VisualElement scrim = shown == GameShell.Page.Title
                ? new CinematicMenuScrim(shell.HighContrast)
                : new VisualElement { pickingMode = PickingMode.Ignore, focusable = false };
            scrim.name = "menu-face-scrim";
            scrim.style.position = Position.Absolute;
            scrim.style.left = scrim.style.top = scrim.style.right = scrim.style.bottom = 0;
            if (shown != GameShell.Page.Title) scrim.style.backgroundColor = new Color(0, 0, 0, shell.HighContrast ? .40f : .28f);
            menuFaceBackground.Add(scrim);
            root.Add(menuFaceBackground);
        }

        void Rebuild()
        {
            if (root == null || !shell || !session) return;
            string previousFocus = (root.focusController?.focusedElement as VisualElement)?.name;
            bool samePage = shown == shell.Screen;
            if (!samePage) { journalScreen.ResetPage(); recordsScreen.ResetPage(); }
            shown = shell.Screen;
            shownContrast = shell.HighContrast;
            shownLargeText = shell.LargeText;
            shownGamepad = PlayerControls.UsingGamepad;
            shownReloading = shell.IsReloading;
            shownReloadError = shell.ReloadError;
            journalStep = session.RecordsRecovered;
            journalExploration = session.ExplorationCount;
            root.Clear(); threatVeil = null;
            settingsScreen = null; pauseScreen = null; hud = null; navigationHelp = null;
            root.style.color = Paper;
            root.style.backgroundColor = shown == GameShell.Page.Playing ? Color.clear :
                shown == GameShell.Page.Pause ? Color.black : new Color(.013f, .025f, .021f, .97f);
            root.pickingMode = shown == GameShell.Page.Playing ? PickingMode.Ignore : PickingMode.Position;
            BuildMenuFaceBackground();
            stage = new VisualElement { name = "safe-area", pickingMode = PickingMode.Ignore };
            stage.style.width = 1600;
            stage.style.height = 900;
            stage.style.flexShrink = 0;
            layout = new ShellLayout(stage, shell.HighContrast, shell.LargeText, MenuFaceVisible);
            if (shown == GameShell.Page.Playing)
            {
                threatVeil = new VisualElement { name = "witnessed-threat-veil", pickingMode = PickingMode.Ignore };
                threatVeil.style.backgroundSize = new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent));
                foreach (var effect in new[] { threatVeil })
                {
                    effect.style.position = Position.Absolute; effect.style.left = effect.style.top = effect.style.right = effect.style.bottom = 0;
                    effect.style.display = DisplayStyle.None; root.Add(effect);
                }
            }
            root.Add(stage);
            transitionVeil=new VisualElement{name="start-transition-black",pickingMode=PickingMode.Ignore};
            transitionVeil.style.position=Position.Absolute;
            transitionVeil.style.left=transitionVeil.style.top=transitionVeil.style.right=transitionVeil.style.bottom=0;
            transitionVeil.style.backgroundColor=Color.black;
            transitionVeil.style.display=DisplayStyle.None;root.Add(transitionVeil);
            if (shown == GameShell.Page.Playing)
            {
                BuildHud();
                // Scripted restarts can also originate in play; never hide a recovery error.
                if (shell.IsReloading && !shell.CinematicLoading || !string.IsNullOrWhiteSpace(shell.ReloadError))
                {
                    Panel(355, 414, 890, 96, CardSurface);
                    Text(shell.IsReloading ? "새 탐색을 준비하고 있습니다…" : shell.ReloadError,
                        379, 428, 842, 70, shell.LargeText ? 24 : 21).style.color = shell.IsReloading ? Gold : Rust;
                }
                return;
            }
            if(shown==GameShell.Page.ChapterTransition)
            {
                root.style.backgroundColor=new Color(.008f,.006f,.008f,1);
                Text("기억을 제단에 돌려놓았다",400,323,800,60,26).style.unityTextAlign=TextAnchor.MiddleCenter;
                Text("폐교의 기억",400,411,800,76,shell.LargeText?46:40).style.unityTextAlign=TextAnchor.MiddleCenter;
                Small("문 너머에서, 수업을 마치지 못한 아이들이 기다린다.",400,519,800,60).style.unityTextAlign=TextAnchor.MiddleCenter;
                return;
            }
            BuildFrame();
            switch (shown)
            {
                case GameShell.Page.Title: BuildTitle(); break;
                case GameShell.Page.Pause: BuildPause(); break;
                case GameShell.Page.Journal: BuildJournal(); break;
                case GameShell.Page.Settings: BuildSettings(); break;
                case GameShell.Page.Result: BuildResult(); break;
                case GameShell.Page.Records: BuildSchoolRecords(); break;
            }
            if (shell.IsReloading)
            {
                if(!shell.CinematicLoading)Small("새 탐색을 준비하고 있습니다…", 150, 793, 1300, 35).style.color = Gold;
                stage.Query<Button>().ForEach(button => button.SetEnabled(false));
            }
            else if (!string.IsNullOrWhiteSpace(shell.ReloadError))
                Small(shell.ReloadError, 150, 786, 1300, 48).style.color = Rust;
            Small("HAPPY TOY  /  FORGOTTEN CORRIDOR", 150, 837, 580);
            var navigation = navigationHelp = Small(NavigationHelp, 820, 837, 630);
            navigation.style.unityTextAlign = TextAnchor.UpperRight;
            string initialId = samePage && !string.IsNullOrEmpty(previousFocus) ? previousFocus :
                shown == GameShell.Page.Title ? "begin" : shown == GameShell.Page.Pause ? "resume" : shown == GameShell.Page.Result ? "restart" : "back";
            var initial = stage.Q<Button>(initialId) ?? stage.Q<Button>("back");
            if (initial != null && !initial.enabledSelf) initial = stage.Q<Button>("back");
            if (initial != null)
            {
                initial.Focus();
                // New elements enter the panel on the next layout pass. Focus only this still-current tree.
                var currentStage = stage;
                root.schedule.Execute(() => { if (stage == currentStage && initial.panel != null) initial.Focus(); });
            }
            UpdateSettingsLabels();
        }

        void BuildFrame()
        {
            if (shown == GameShell.Page.Title) return;
            Panel(105, 80, 3, 705, Gold);
            Panel(150, 115, 1300, 1, Edge);
            Small(session.ChapterMode || shown==GameShell.Page.Records ? "폐교의 기억  /  마지막 출석" : session.CorridorMode || shown==GameShell.Page.Title ? "잊힌 회랑  /  소리를 따라 남은 기억을 찾다" : "폐교 조사 기록  /  마지막 출석", 150, 78, 1050);
            var paused = Small(shown == GameShell.Page.Title ? "기억을 돌려놓을 제단을 찾으세요" : "시간이 멈춰 있습니다", 1060, 78, 390);
            paused.style.unityTextAlign = TextAnchor.UpperRight;
            string title = shown == GameShell.Page.Title ? "잊힌 회랑" : shown == GameShell.Page.Pause ? "숨을 고르다" :
                shown == GameShell.Page.Journal ? "조사 기록" : shown == GameShell.Page.Settings ? "환경 설정" :
                shown==GameShell.Page.Records ? "학교 탐색 기록" :
                session.Escaped ? session.CorridorMode ? "회랑의 문이 열렸습니다" : "마지막 아이가 하교했습니다" : "발소리가 멈췄습니다";
            Text(title, 150, 145, 1300, shown == GameShell.Page.Title ? 110 : 82, shown == GameShell.Page.Title ? 66 : 48).style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        void BuildTitle() => BuildCinematicTitle();
        void BuildPause()
        {
            pauseScreen = new ShellPauseScreen();
            pauseScreen.Build(layout, ShellPauseScreen.State.Read(session, shell), new ShellPauseScreen.Actions {
                Resume = shell.Resume, Journal = shell.Journal, Settings = shell.Settings,
                Restart = () => shell.Restart(true), Title = () => shell.Restart(false),
                SuspendCorridor = shell.SuspendCorridor, SuspendChapter = shell.SuspendChapter }, ControlInstructions);
        }
        void BuildJournal() => journalScreen.Build(layout, ShellJournalScreen.State.Read(session), shell.Back, Rebuild);
        void BuildSettings()
        {
            settingsScreen = new ShellSettingsScreen();
            settingsScreen.Build(layout, new ShellSettingsScreen.Actions {
                Adjust = shell.AdjustSettings, AdjustFieldOfView = shell.AdjustFieldOfView,
                ToggleReducedMotion = shell.ToggleReducedMotion, ToggleSubtitles = shell.ToggleSubtitles,
                ToggleHighContrast = shell.ToggleHighContrast, ToggleLargeText = shell.ToggleLargeText,
                Back = shell.Back, Defaults = shell.RestoreDefaultSettings }, ShellSettingsScreen.State.Read(shell));
            settingsLabelsDirty = false;
        }
        void BuildResult() => new ShellResultScreen().Build(layout, ShellResultScreen.State.Read(session), new ShellResultScreen.Actions {
            Restart = () => shell.Restart(true), Title = () => shell.Restart(false), History = shell.ChapterHistory,
            RetryRecord = () => { session.RetryRecordSave(); Rebuild(); },
            RetryChapterRecord = () => { session.RetryChapterRecordSave(); Rebuild(); } });
        void BuildSchoolRecords() => recordsScreen.Build(layout, session.ChapterRecords.Snapshot, session.ChapterRecords.Status, shell.Back, Rebuild);
        void BuildHud()
        {
            hud = new ShellHudView(layout, threatVeil);
            hud.Build(session.TotalRecords, ControlsFooter);
            UpdateHud();
        }
        void UpdateSettingsLabels()
        {
            if (settingsScreen == null) return;
            settingsScreen.Refresh(ShellSettingsScreen.State.Read(shell));
            settingsLabelsDirty = false;
        }
        void UpdateHud() => hud?.Update(ShellHudView.State.Read(session, shell), shownGamepad);
        void LateUpdate()
        {
            if (root == null || !shell || !session) return;
            bool changedRecords = journalStep != session.RecordsRecovered || journalExploration != session.ExplorationCount;
            if (shownGamepad != PlayerControls.UsingGamepad)
            {
                shownGamepad = PlayerControls.UsingGamepad;
                if (navigationHelp != null) navigationHelp.text = NavigationHelp;
                pauseScreen?.UpdateControls(ControlInstructions);
                hud?.UpdateControls(ControlsFooter);
            }
            if (shown != shell.Screen || shownContrast != shell.HighContrast || shownLargeText != shell.LargeText ||
                shownReloading != shell.IsReloading || shownReloadError != shell.ReloadError || changedRecords && shown != GameShell.Page.Playing)
                Rebuild();
            if (shown == GameShell.Page.Settings && settingsLabelsDirty) UpdateSettingsLabels();
            if (shown == GameShell.Page.Playing) UpdateHud();
            stage.style.opacity=shown==GameShell.Page.Title?shell.TransitionMenuOpacity:1-shell.TransitionBlack;
            var scrim=menuFaceBackground?.Q<VisualElement>("menu-face-scrim");
            if(scrim!=null)scrim.style.opacity=shown==GameShell.Page.Title?shell.TransitionMenuOpacity:1;
            if(transitionVeil!=null)
            {
                transitionVeil.style.display=shell.TransitionBlack>0?DisplayStyle.Flex:DisplayStyle.None;
                transitionVeil.style.opacity=shell.TransitionBlack;
            }
            UpdateKeyboardSubmit();
            UpdateGamepadMenu();
        }
        void UpdateKeyboardSubmit()
        {
            var keyboard=Keyboard.current;
            if(shell.Screen==GameShell.Page.Playing || shell.IsReloading || keyboard==null ||
                !(keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) return;
            var target=root.panel?.focusController.focusedElement as VisualElement;
            if(target==null || target.panel!=root.panel) target=stage.Q<Button>();
            if(target==null) return;
            // Runtime panels rendered to textures receive no native keyboard
            // submit. The same focused-button path also owns native Enter so a
            // press cannot arrive twice through the platform event provider.
            sendingKeyboardSubmit=true;
            try { using(var submit=NavigationSubmitEvent.GetPooled()) target.SendEvent(submit); }
            finally { sendingKeyboardSubmit=false; }
        }
        void UpdateGamepadMenu()
        {
            var pad = Gamepad.current;
            if (shown == GameShell.Page.Playing || shell.IsReloading || pad == null || !PlayerControls.UsingGamepad)
            { previousMenuDirection = Vector2.zero; return; }
            var direction = pad.dpad.ReadValue();
            if (direction.sqrMagnitude < .25f) direction = pad.leftStick.ReadValue();
            if (direction.sqrMagnitude < .25f) direction = Vector2.zero;
            else direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x),0) : new Vector2(0,Mathf.Sign(direction.y));
            var target = root.panel?.focusController.focusedElement as VisualElement;
            if (target == null || target.panel != root.panel) target = stage.Q<Button>();
            if (target == null) return;
            sendingGamepadNavigation = true;
            try
            {
                if (direction != Vector2.zero && (direction != previousMenuDirection || Time.unscaledTime >= nextMenuRepeat))
                {
                    using (var e = NavigationMoveEvent.GetPooled(direction)) target.SendEvent(e);
                    nextMenuRepeat = Time.unscaledTime + (direction != previousMenuDirection ? .45f : .12f);
                }
                // Resolve focus again after movement; one press submits exactly once.
                target = root.panel.focusController.focusedElement as VisualElement ?? target;
                if (pad.buttonSouth.wasPressedThisFrame && shell.GameplayEntryFrame != Time.frameCount)
                    using (var e = NavigationSubmitEvent.GetPooled()) target.SendEvent(e);
            }
            finally { sendingGamepadNavigation = false; previousMenuDirection = direction; }
        }
        void OnDestroy()
        {
            if (titleScene) Destroy(titleScene);
            if (shell) shell.SettingsChanged -= OnSettingsChanged;
            if (settings) settings.targetTexture = null;
            if (CaptureTarget) { CaptureTarget.Release(); Destroy(CaptureTarget); }
            if (document) Destroy(document);
            if (settings) Destroy(settings);
        }
    }
}
