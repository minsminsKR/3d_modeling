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
    public sealed class GameShellView : MonoBehaviour
    {
        GameShell shell;
        GameSession session;
        UIDocument document;
        PanelSettings settings;
        VisualElement threatVeil;
        VisualElement root, stage, stamina, crosshair, focusPanel, itemFeedbackPanel, memoryStrip;
        Label focus, meter, status, noise, caption, itemFeedback, volume, sensitivity, fieldOfView;
        Label navigationHelp, controlInstructions, controlsFooter, batteryMeter;
        bool sendingGamepadNavigation;
        bool sendingKeyboardSubmit;
        Vector2 previousMenuDirection;
        float nextMenuRepeat;
        bool GamepadMenuIntent => Gamepad.current != null && (PlayerControls.UsingGamepad || Gamepad.current.buttonSouth.isPressed ||
            Gamepad.current.dpad.ReadValue().sqrMagnitude > .1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > .25f);
        bool KeyboardSubmitIntent => Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed);
        string NavigationHelp => PlayerControls.UsingGamepad ? "방향 패드  선택     아래 버튼  확인     오른쪽 버튼  뒤로" : "Tab / 방향키  선택      Enter  확인      Esc  뒤로";
        string ControlInstructions => PlayerControls.UsingGamepad ? "왼쪽 스틱  이동 / 누르며 달리기\n오른쪽 스틱  시선\n아래 버튼  조사·문·은신\n오른쪽 버튼  낮은 자세 / 위 버튼  빛\nLT 조준 / RT 폭죽 (추격 전 유인)\nSelect 기록 / Start 일시정지" : "WASD  이동       마우스  시선\nShift  달리기     C / Ctrl  낮은 자세\nE  조사·문·은신·배터리·촛불\nF  손전등          Q  폭죽 (추격 전 유인)\n우클릭 누르기  폭죽 첫 충돌 조준\nJ  조사 기록      Esc  일시정지";
        string ControlsFooter => PlayerControls.UsingGamepad ? "오른쪽  낮은 자세    위  빛    RT  폭죽    Start  메뉴" : "C  낮은 자세    F  빛    Q  폭죽    J  기록    Esc  메뉴";
        Button reducedMotionButton, subtitlesButton, contrastButton, textSizeButton;
        GameShell.Page shown = (GameShell.Page)(-1);
        int journalStep = -1, journalExploration = -1, journalPage;
        int schoolRecordPage;
        bool shownContrast, shownLargeText, shownGamepad, settingsLabelsDirty;
        bool shownReloading;
        string shownReloadError;
        readonly MemoryToken[] memoryTokens = new MemoryToken[7];

        sealed class MemoryToken : VisualElement
        {
            readonly bool highContrast;
            bool recovered;
            public MemoryToken(bool contrast)
            {
                highContrast = contrast; pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                SetRecovered(false);
            }
            public void SetRecovered(bool found)
            {
                bool changed = recovered != found; recovered = found;
                style.opacity = found ? 1 : highContrast ? .48f : .26f;
                EnableInClassList("recovered", found);
                if (changed) MarkDirtyRepaint();
            }
            void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                float sx = contentRect.width / 20, sy = contentRect.height / 26;
                Vector2 At(float x, float y) => new Vector2(x * sx, y * sy);
                void Page()
                {
                    painter.BeginPath(); painter.MoveTo(At(2, 2)); painter.LineTo(At(12, 2));
                    painter.LineTo(At(18, 8)); painter.LineTo(At(18, 24)); painter.LineTo(At(2, 24)); painter.ClosePath();
                }
                // Small folded attendance leaves remain readable against both
                // dark walls and a bright torch, without an enclosing HUD panel.
                Page(); painter.lineWidth = 3.4f * sx; painter.strokeColor = new Color(0, 0, 0, .8f); painter.Stroke();
                Page(); painter.fillColor = new Color(.93f, .90f, .80f, recovered ? .24f : .035f); painter.Fill();
                painter.lineWidth = 1.25f * sx; painter.strokeColor = new Color(.94f, .92f, .84f); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(At(12, 2)); painter.LineTo(At(12, 8)); painter.LineTo(At(18, 8));
                painter.MoveTo(At(6, 13)); painter.LineTo(At(14, 13));
                painter.MoveTo(At(6, 17)); painter.LineTo(At(13, 17));
                painter.MoveTo(At(6, 21)); painter.LineTo(At(10, 21)); painter.Stroke();
            }
        }

        public RenderTexture CaptureTarget { get; private set; }
        public VisualElement Root => root;
        Color Paper => shell && shell.HighContrast ? Color.white : new Color(.91f, .89f, .81f);
        Color Muted => shell && shell.HighContrast ? new Color(.83f, .86f, .83f) : new Color(.65f, .71f, .68f);
        Color Gold => shell && shell.HighContrast ? new Color(1f, .86f, .48f) : new Color(.84f, .72f, .47f);
        Color Surface => shell && shell.HighContrast ? new Color(.015f, .02f, .018f) : new Color(.048f, .075f, .065f);
        Color Edge => new Color(.22f, .30f, .25f);
        Color Rust => shell && shell.HighContrast ? new Color(1f, .66f, .48f) : new Color(.91f, .55f, .39f);
        int ReadingSize => shell && shell.LargeText ? 26 : 22;

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
            CaptureTarget = new RenderTexture(width, height, 24) { graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB, name = "HappyToy UI capture" };
            CaptureTarget.Create();
            settings.targetTexture = CaptureTarget;
        }

        VisualElement Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
            element.style.marginLeft = element.style.marginRight = 0;
            element.style.marginTop = element.style.marginBottom = 0;
            stage.Add(element);
            return element;
        }
        Label Text(string text, float x, float y, float width, float height, int size = 22)
        {
            var label = new Label(text);
            Place(label, x, y, width, height);
            label.style.fontSize = size;
            label.style.color = Paper;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = label.style.paddingRight = 0;
            label.style.paddingTop = label.style.paddingBottom = 0;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
        VisualElement Panel(float x, float y, float width, float height, Color color)
        {
            var panel = Place(new VisualElement(), x, y, width, height);
            panel.style.backgroundColor = color;
            panel.pickingMode = PickingMode.Ignore;
            return panel;
        }
        void Outline(VisualElement element, Color color, float width = 1)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = width;
            element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopColor = element.style.borderBottomColor = color;
        }
        Button Button(string id, string text, float y, Action action, float x = 150, float width = 440, bool primary = false)
        {
            var button = new Button(action) { name = id, text = text, tooltip = text };
            Place(button, x, y, width, 54);
            button.style.fontSize = shell.LargeText ? 24 : 21;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 20;
            button.style.paddingRight = 12;
            // Default theme vertical padding can clip the bundled Korean font
            // at 720p. The fixed-height button keeps its full line box available.
            button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.color = primary ? new Color(.04f, .06f, .05f) : Paper;
            button.style.backgroundColor = primary ? Gold : Surface;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 2;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 2;
            Outline(button, primary ? Gold : Edge);
            bool hovered = false, focused = false;
            Action refresh = () =>
            {
                bool active = hovered || focused;
                button.style.backgroundColor = active ? new Color(.19f, .25f, .20f) : primary ? Gold : Surface;
                button.style.color = active ? Color.white : primary ? new Color(.04f, .06f, .05f) : Paper;
                Outline(button, active ? Gold : primary ? Gold : Edge, focused ? 3 : 1);
            };
            button.RegisterCallback<FocusInEvent>(_ => { focused = true; refresh(); });
            button.RegisterCallback<FocusOutEvent>(_ => { focused = false; refresh(); });
            button.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; refresh(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; refresh(); });
            return button;
        }
        Label Small(string text, float x, float y, float width, float height = 28)
        {
            // The bundled Korean font has a taller line box than Latin defaults.
            // Preserve the large-text size and reserve the measured single-line space.
            var label = Text(text, x, y, width, shell.LargeText ? Mathf.Max(height, 32) : height, shell.LargeText ? 20 : 17);
            label.style.color = Muted;
            return label;
        }

        void Rebuild()
        {
            if (root == null || !shell || !session) return;
            string previousFocus = (root.focusController?.focusedElement as VisualElement)?.name;
            bool samePage = shown == shell.Screen;
            if (!samePage) journalPage = 0;
            if(!samePage) schoolRecordPage=0;
            shown = shell.Screen;
            shownContrast = shell.HighContrast;
            shownLargeText = shell.LargeText;
            shownGamepad = PlayerControls.UsingGamepad;
            shownReloading = shell.IsReloading;
            shownReloadError = shell.ReloadError;
            journalStep = session.RecordsRecovered;
            journalExploration = session.ExplorationCount;
            root.Clear(); threatVeil = null;
            focus = meter = status = noise = caption = itemFeedback = volume = sensitivity = fieldOfView = null;
            reducedMotionButton = subtitlesButton = contrastButton = textSizeButton = null;
            stamina = crosshair = focusPanel = itemFeedbackPanel = memoryStrip = null;
            Array.Clear(memoryTokens, 0, memoryTokens.Length);
            root.style.color = Paper;
            root.style.backgroundColor = shown == GameShell.Page.Playing ? Color.clear : new Color(.013f, .025f, .021f, .97f);
            root.pickingMode = shown == GameShell.Page.Playing ? PickingMode.Ignore : PickingMode.Position;
            stage = new VisualElement { name = "safe-area", pickingMode = PickingMode.Ignore };
            stage.style.width = 1600;
            stage.style.height = 900;
            stage.style.flexShrink = 0;
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
            if (shown == GameShell.Page.Playing)
            {
                BuildHud();
                // Scripted restarts can also originate in play; never hide a recovery error.
                if (shell.IsReloading || !string.IsNullOrWhiteSpace(shell.ReloadError))
                {
                    Panel(355, 414, 890, 96, Surface);
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
                Small("새 탐색을 준비하고 있습니다…", 150, 793, 1300, 35).style.color = Gold;
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
        void BuildTitle()
        {
            Text("매번 달라지는 회랑.\n다섯 기억을 붉은 교실의 제단에 바치세요.", 153, 262, 680, 110, shell.LargeText ? 32 : 28);
            bool protectedRecords=!session.ChapterRecords.Writable && !string.IsNullOrEmpty(session.ChapterRecords.Status);
            bool protectedSchoolSlot=!session.ChapterSuspension.Writable && !string.IsNullOrEmpty(session.ChapterSuspension.Status);
            bool protectedCorridorSlot=!session.Suspension.Writable && !string.IsNullOrEmpty(session.Suspension.Status);
            bool protectedSlot=protectedSchoolSlot || protectedCorridorSlot;
            string introduction=protectedRecords?"학교 기록 저장이 중지됐습니다.\n최근 학교 기록에서 상태를 확인하세요.":"발소리를 듣고, 문으로 시선을 끊고, 어둠 속에 숨으세요.\n폭죽은 추격이 시작되기 전에 소리로 유인할 수 있습니다.";
            if(protectedSlot)
            {
                string mode=protectedSchoolSlot && protectedCorridorSlot?"회랑·폐교":protectedSchoolSlot?"폐교":"회랑";
                introduction=mode+" 중단 기록을 읽을 수 없어 원문을 보존했습니다.\n"+
                    (protectedRecords?"학교 결과 저장도 중지됐습니다. 최근 학교 기록을 확인하세요.":"새 탐색은 가능합니다. 해당 중단 저장은 보호 중입니다.");
            }
            var titleNotice=Small(introduction,153,374,680,65);
            titleNotice.name=protectedRecords?"school-profile-notice":protectedSlot?"checkpoint-status":"title-introduction";
            if(protectedSlot || protectedRecords) titleNotice.style.color=Rust;
            bool saved=session.Suspension.HasRun, schoolSaved=session.ChapterSuspension.HasRun;
            float next=451;
            Button("begin", saved?"새 회랑 탐색  [Enter]":"회랑에 들어가기  [Enter]", next, shell.BeginCorridor, primary:true); next+=60;
            if(saved) { Button("continue-run", "회랑 이어하기 · 기억 "+session.Suspension.Snapshot.recovered.Count(value=>value)+"/5", next, shell.ContinueCorridor).SetEnabled(session.Suspension.Writable); next+=60; }
            Button("begin-school", schoolSaved?"폐교의 기억 · 처음부터":"폐교의 기억 · 세 층의 학교", next, shell.BeginChapter); next+=60;
            if(schoolSaved)
            {
                var data=session.ChapterSuspension.Snapshot;
                Button("continue-chapter", $"폐교 이어하기 · 기억 {data.recovered}/5", next, shell.ContinueChapter).SetEnabled(session.ChapterSuspension.Writable); next+=60;
            }
            Button("settings", "환경 설정 · 접근성", next, shell.Settings); next+=60;
            Button("quit", "종료", next, () => Application.Quit()); next+=64;
            BuildTitleRecords(895,245);
        }
        void BuildTitleRecords(float x,float y)
        {
            var card=Panel(x,y,540,583,Surface); Outline(card,Edge);
            Small("회랑 / 폐교 · 별도로 보관하는 탐색 기록",x+30,y+26,480,36);
            var corridor=session.Records.Snapshot;
            string corridorBest=corridor.escapes>0?"최단 탈출  "+RunTime(corridor.bestEscapeSeconds):"아직 회랑 탈출 기록이 없습니다.";
            Text($"회랑 탐색 {corridor.attempts}회 · 탈출 {corridor.escapes}회\n최고 기억 {corridor.bestRecovered}/5\n{corridorBest}",x+30,y+70,480,106,shell.LargeText?23:21).name="corridor-record-summary";
            Panel(x+26,y+177,487,1,Edge);
            var school=session.ChapterRecords.Snapshot;
            string schoolBest=school.escapes>0?"최단 탈출  "+RunTime(school.bestEscapeSeconds):"아직 학교 탈출 기록이 없습니다.";
            Text($"학교 탐색 {school.attempts}회 · 탈출 {school.escapes}회\n최고 기억 {school.bestRecovered}/5\n{schoolBest}",x+30,y+190,480,106,shell.LargeText?23:21).name="school-record-summary";
            Button("school-records","최근 학교 탐색 기록",y+301,shell.ChapterHistory,x+30,480);
            Panel(x+26,y+365,487,1,Edge);
            controlInstructions=Text(ControlInstructions,x+30,y+377,485,200,shell.LargeText?23:21);
        }
        void BuildAttendanceCard(float x, float y, bool progress)
        {
            var card = Panel(x, y, 540, 535, Surface);
            Outline(card, Edge);
            Panel(x + 26, y + 27, 3, 70, Gold);
            Small(session.CorridorMode?(session.Corridor.Layout.Version>=3 ? "CORRIDOR  /  붉은 교실에 기억을 돌려놓으세요" : "CORRIDOR  /  돌아갈 문을 기억하세요"):"SCHOOL ARCHIVE  /  출석 확인", x + 46, y + 28, 450);
            Text(progress ? session.CorridorMode || session.ChapterMode ? $"회수한 기억  {session.RecordsRecovered} / 5" : $"주요 단계  {session.StoryStep} / 4" : "학교 탐색 기록", x + 46, y + 61, 460, 48, 30);
            if(!progress)
            {
                var school=session.ChapterRecords.Snapshot;
                string best=school.escapes>0?"최단 탈출  "+RunTime(school.bestEscapeSeconds):"아직 학교 탈출 기록이 없습니다.";
                Text($"학교 탐색 {school.attempts}회 · 탈출 {school.escapes}회\n최고 기억 {school.bestRecovered}/5\n{best}",x+30,y+132,480,110,shell.LargeText?23:21).name="school-record-summary";
                Button("school-records","최근 학교 탐색 기록",y+249,shell.ChapterHistory,x+30,480);
                Panel(x+26,y+311,487,1,Edge);
                controlInstructions=Text(ControlInstructions,x+30,y+323,485,200,shell.LargeText?23:21);
                return;
            }
            bool corridorCard = session.CorridorMode || session.ChapterMode || !progress;
            for (int i = 0; i < (corridorCard ? 5 : 4); i++)
            {
                float lineY = y + 136 + i * (corridorCard ? 35 : 43);
                Small($"0{i + 1}", x + 30, lineY, 46);
                Panel(x + 88, lineY + 9, 255 - i * 24, 9, new Color(.16f, .22f, .18f));
                bool complete = progress && (corridorCard ? session.RecordsRecovered : session.StoryStep) > i;
                Small(complete ? "회수" : "미확인", x + 419, lineY, 95).style.color = complete ? Gold : Muted;
                Panel(x + 27, lineY + 33, 486, 1, Edge);
            }
            Panel(x + 26, y + 311, 487, 1, Edge);
            controlInstructions = Text(ControlInstructions, x + 30, y + 323, 485, 200, shell.LargeText ? 23 : 21);
        }
        void BuildPause()
        {
            Small("현재 목표", 153, 249, 640);
            Text(session.Objective, 153, 286, 650, 80, ReadingSize);
            Button("resume", "계속하기  [Esc]", 387, shell.Resume, primary: true);
            Button("journal", "조사 기록  [J]", 455, shell.Journal);
            Button("settings", "환경 설정 · 접근성", 523, shell.Settings);
            Button("restart", session.CorridorMode?"새 회랑에서 다시 시작":"처음부터 다시 시작", 591, () => shell.Restart(true));
            Button("title", "시작 화면으로 (진행 초기화)", 659, () => shell.Restart(false));
            if(session.CorridorMode)
            {
                var suspend=Button("suspend-run", "탐색 저장 후 시작 화면으로", 727, shell.SuspendCorridor);
                suspend.SetEnabled(session.Suspension.Writable&&string.IsNullOrEmpty(session.Corridor.SuspendBlockReason));
            }
            if(session.ChapterMode)
            {
                var suspend=Button("suspend-chapter", "학교 저장 후 시작 화면으로", 727, shell.SuspendChapter);
                suspend.SetEnabled(session.ChapterSuspension.Writable&&string.IsNullOrEmpty(session.Chapter.SuspendBlockReason));
            }
            BuildAttendanceCard(895, 245, true);
            if(string.IsNullOrEmpty(shell.ReloadError)) Small(session.ChapterMode && !session.ChapterSuspension.Writable?session.ChapterSuspension.Status:
                session.ChapterMode && !string.IsNullOrEmpty(session.Chapter.SuspendBlockReason)?session.Chapter.SuspendBlockReason:
                !session.Suspension.Writable&&!string.IsNullOrEmpty(session.Suspension.Status)?session.Suspension.Status:
                session.CorridorMode && !string.IsNullOrEmpty(session.Corridor.SuspendBlockReason)?session.Corridor.SuspendBlockReason:
                session.ChapterMode ? "학교 저장으로 나가면 기억과 현재 상태를 이어갑니다. 처음부터 시작하면 저장된 학교 탐색을 초기화합니다." :
                "중단 저장으로 나가면 같은 회랑을 이어갈 수 있습니다. 다시 시작은 현재 탐색을 초기화합니다.", 153, 787, 1270,40);
        }
        void BuildJournal()
        {
            int required=session.CorridorMode?0:session.JournalCount;
            int total=session.CorridorMode?Mathf.Max(CorridorRun.Required,session.ExplorationCount):required+session.ExplorationCount;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / 6f));
            journalPage = Mathf.Clamp(journalPage, 0, pages - 1);
            Small(session.CorridorMode?$"회수한 기억 {session.RecordsRecovered} / 5     ·     발견한 순서대로 보관합니다":$"복원 {session.RecordsRecovered} / {session.TotalRecords}     주변 기록 {session.ExplorationCount}개     ·     발견한 내용만 보관합니다", 153, 218, 1290);
            for (int slot = 0; slot < 6; slot++)
            {
                int index = journalPage * 6 + slot;
                if(session.CorridorMode && index>=total) break;
                float x = 150 + slot % 2 * 665, y = 264 + slot / 2 * 158;
                string entry = index < required ? session.JournalEntry(index) : session.ExplorationEntry(index - required);
                string placeholder = session.CorridorMode?"아직 회수하지 못했습니다.\n갈림길에서 기억이 울리는 소리를 들으세요.":index < required ? "아직 발견하지 못했습니다.\n학교 안에서 단서를 찾아 조사하세요." : "아직 비어 있습니다.\n게시물을 조사하면 이곳에 남습니다.";
                var card = Panel(x, y, 635, 148, Surface);
                Outline(card, entry == null ? Edge : new Color(.39f, .43f, .30f));
                Panel(x, y, 3, 148, entry == null ? Edge : Gold);
                Small(session.CorridorMode?$"회랑의 기억  0{index+1}":index < required ? $"필수 기록  0{index + 1}" : $"주변 기록  {index - required+1:00}", x + 20, y + 11, 460, 25).style.color = entry == null ? Muted : Gold;
                var label = Text(entry ?? placeholder, x + 20, y + 43, 596, 103, shell.LargeText ? 22 : 20);
                label.name = session.CorridorMode?"corridor-memory-"+index:index < required ? "record-" + index : "inspection-" + (index - required);
                if (entry == null) label.style.color = Muted;
            }
            Button("back", "돌아가기  [J / Esc]", 747, shell.Back, primary: true);
            if (pages > 1)
            {
                var previous = Button("journal-previous", "이전", 747, () => { journalPage--; Rebuild(); }, 895, 165);
                previous.SetEnabled(journalPage > 0);
                var count = Text($"{journalPage + 1} / {pages}", 1080, 759, 165, 40, 22);
                count.style.unityTextAlign = TextAnchor.UpperCenter;
                var next = Button("journal-next", "다음", 747, () => { journalPage++; Rebuild(); }, 1285, 165);
                next.SetEnabled(journalPage < pages - 1);
            }
        }
        void BuildSettings()
        {
            Small("변경 사항은 즉시 적용되며, 돌아가면 이 PC에 저장됩니다.", 153, 229, 1290);
            Panel(150, 278, 630, 426, Surface);
            Panel(810, 278, 640, 426, Surface);
            volume = Text("", 174, 295, 580, 42, ReadingSize);
            Button("volume-down", "− 5%", 343, () => shell.AdjustSettings(-.05f, 0), 174, 270);
            Button("volume-up", "+ 5%", 343, () => shell.AdjustSettings(.05f, 0), 466, 290);
            sensitivity = Text("", 174, 425, 580, 42, ReadingSize);
            Button("mouse-down", "감도 낮추기", 473, () => shell.AdjustSettings(0, -.009f), 174, 270);
            Button("mouse-up", "감도 높이기", 473, () => shell.AdjustSettings(0, .009f), 466, 290);
            fieldOfView = Text("", 174, 555, 580, 42, ReadingSize);
            Button("fov-down", "− 5°", 603, () => shell.AdjustFieldOfView(-5), 174, 270);
            Button("fov-up", "+ 5°", 603, () => shell.AdjustFieldOfView(5), 466, 290);
            Small("움직임 · 깜빡임 · 소리 안내 · 가독성", 836, 295, 590);
            reducedMotionButton = Button("reduced-motion", "", 341, shell.ToggleReducedMotion, 836, 588);
            subtitlesButton = Button("subtitles", "", 417, shell.ToggleSubtitles, 836, 588);
            contrastButton = Button("high-contrast", "", 493, shell.ToggleHighContrast, 836, 588);
            textSizeButton = Button("large-text", "", 569, shell.ToggleLargeText, 836, 588);
            Small("소리 자막은 들리는 효과음을 글로 안내합니다.", 836, 650, 588, 42);
            Button("back", "저장하고 돌아가기  [Esc]", 753, shell.Back, primary: true);
            Button("defaults", "기본값으로 되돌리기", 753, shell.RestoreDefaultSettings, 1070, 380);
        }
        void BuildResult()
        {
            if(session.ChapterMode) { BuildSchoolResult(); return; }
            Small(session.CorridorMode?(session.Escaped?"회랑 탐색 종료  /  탈출 성공":"회랑 탐색 중단  /  기억 미완료"):session.Escaped ? "조사 종료  /  하교 확인" : "조사 중단  /  기록 미완료", 153, 247, 1170);
            string message = session.Escaped ? "지워졌던 이름을 다시 적었다.\n복도 너머에서 마지막 문이 닫힌다." : "이곳에는 아직 돌아오지 못한 기록이 있습니다.\n다시 들어가 마지막 이름을 찾아주세요.";
            if (session.CorridorMode) message = session.Escaped ? (session.Corridor.Layout.Version>=3 ? "기억을 제단에 돌려놓았다.\n문 너머로 폐교의 기억이 이어진다." : "다섯 기억이 모이자 봉인이 풀렸다.\n처음 들어온 문 너머로 돌아왔다.") : "돌아갈 길을 잃었다.\n들었던 발소리를 기억하고 다음 회랑에 들어가세요.";
            Text(message, 153, 294, 1210, 103, shell.LargeText ? 30 : 27);
            Panel(895, 437, 540, 391, Surface);
            Small(session.CorridorMode?"이번 회랑 탐색":session.Escaped ? "마지막 출석 확인" : "다음 탐색을 위한 기록", 920, 460, 490);
            Text($"복원한 기록  {session.RecordsRecovered} / {session.TotalRecords}\n탐색 시간  {Mathf.FloorToInt(session.ElapsedPlayTime / 60)}분 {Mathf.FloorToInt(session.ElapsedPlayTime % 60):00}초", 920, 505, 490, 85, ReadingSize);
            string hint = session.Escaped ? "모든 이름이 제자리로 돌아왔습니다." : !string.IsNullOrWhiteSpace(session.DefeatHint) ? session.DefeatHint : "문을 닫아 시선을 끊고 걸으며 숨을 회복하세요.";
            Text(hint, 920, 603, 490, 211, shell.LargeText ? 23 : 20).style.color = session.Escaped ? Gold : Muted;
            Button("restart", session.Escaped ? "다시 탐색하기" : "다시 시작", 447, () => shell.Restart(true), primary: true);
            Button("title", "시작 화면으로", 515, () => shell.Restart(false));
            Button("quit", "종료", 583, () => Application.Quit());
            bool retryRecord = session.CorridorMode && !session.RecordSaved && session.Records.Writable && !string.IsNullOrWhiteSpace(session.RecordSaveMessage);
            if (retryRecord) Button("retry-record-save", "탐색 기록 저장 다시 시도", 651, () => { session.RetryRecordSave(); Rebuild(); });
            if (!session.Escaped) Small(session.CorridorMode ? "다시 시작하면 현재 회랑은 새로 생성됩니다." : "다시 시작하면 이번 탐색 기록은 초기화됩니다.",
                153, retryRecord ? 715 : 680, 675, retryRecord ? 55 : 70);
            if (!string.IsNullOrWhiteSpace(session.RecordSaveMessage))
                Small(session.RecordSaveMessage, 153, retryRecord ? 770 : 752, 675, retryRecord ? 60 : 80).name = "record-save-status";
        }
        static string RunTime(float seconds) => $"{Mathf.FloorToInt(seconds/60)}분 {Mathf.FloorToInt(seconds%60):00}초";
        void BuildSchoolResult()
        {
            var result=session.ChapterResult;
            Small(session.Escaped?"학교 조사 종료  /  하교 확인":"학교 조사 중단  /  다섯 기억의 기록",153,247,675);
            Text(session.Escaped?"다섯 이름이 돌아왔다.\n학교 밖의 공기를 다시 마신다.":"기억은 아직 학교 안에 남아 있다.\n들었던 소리를 기억하고 다시 들어가세요.",153,294,650,109,shell.LargeText?30:27);
            Panel(895,245,540,583,Surface);
            Small("이번 학교 탐색",920,269,490);
            Text($"기억 {result.recovered} / 5\n탐색 시간  {RunTime(result.seconds)}\n남은 폭죽 {result.firecrackersRemaining}개 · 호흡 {Mathf.RoundToInt(result.staminaRemaining*100)}%\n이동 거리 {result.distance:0.0} m\n폭죽 {result.throws}회 · 은신 {result.hides}회 · 문 닫기 {result.closedDoors}회",920,320,490,176,shell.LargeText?23:21).name="school-result-details";
            Text(session.Escaped?"결과: 학교 탈출 성공":"마지막 기척: "+(string.IsNullOrEmpty(result.defeatSource)?"기록되지 않음":result.defeatSource),920,498,490,54,ReadingSize).name="school-result-source";
            Small(result.actionsComplete?"행동 기록은 이번 탐색 전체를 포함합니다.\n일시정지와 중단 시간은 탐색 시간에서 제외됩니다.":"이전 저장에는 행동 기록이 없어 이어하기 이후 행동만 표시합니다. 탐색 시간과 기억은 전체 진행입니다.",920,559,490,77).name="school-result-scope";
            string advice=session.Escaped?"모든 기억을 회수하고 1층 출입문으로 돌아왔습니다. 다른 탐색 기록은 최근 기록에서 확인할 수 있습니다.":
                session.Chapter.Objective+"\n"+session.DefeatHint;
            Text(advice,920,646,490,172,shell.LargeText?23:20).style.color=session.Escaped?Gold:Muted;
            Button("restart",session.Escaped?"학교 다시 탐색하기":"학교 처음부터 다시 시작",447,()=>shell.Restart(true),primary:true);
            Button("title","시작 화면으로",515,()=>shell.Restart(false)); Button("quit","종료",583,()=>Application.Quit());
            Button("school-records","최근 학교 탐색 기록",651,shell.ChapterHistory);
            bool retry=!session.ChapterRecordSaved && session.ChapterRecords.Writable && !string.IsNullOrEmpty(session.ChapterRecordSaveMessage);
            if(retry) Button("retry-school-record-save","학교 결과 저장 다시 시도",710,()=>{session.RetryChapterRecordSave(); Rebuild();});
            string message=session.ChapterRecordSaveMessage;
            if(string.IsNullOrEmpty(message)) message=session.Escaped?"다음 탐색에서는 새로운 기록에 도전할 수 있습니다.":"다시 시작은 첫 기억부터 진행합니다. 완료된 탐색 기록은 남습니다.";
            Small(message,153,retry?770:723,675,retry?62:98).name="school-record-save-status";
        }
        void BuildSchoolRecords()
        {
            var summary=session.ChapterRecords.Snapshot;
            string best=summary.escapes>0?"최단 탈출 "+RunTime(summary.bestEscapeSeconds):"아직 학교 탈출 기록이 없습니다.";
            Small($"학교 탐색 {summary.attempts}회 · 탈출 {summary.escapes}회 · 최고 기억 {summary.bestRecovered}/5\n{best}",153,247,1290,62).name="school-history-summary";
            Small(string.IsNullOrEmpty(session.ChapterRecords.Status)?"최근 저장된 여덟 번의 학교 탐색을 보관합니다. 회랑 탐색 기록은 별도입니다.":session.ChapterRecords.Status.Replace("\n"," "),153,313,1290,30);
            int pages=Mathf.Max(1,Mathf.CeilToInt(summary.recent.Length/4f)); schoolRecordPage=Mathf.Clamp(schoolRecordPage,0,pages-1);
            if(summary.recent.Length==0) Text("아직 완료된 학교 탐색이 없습니다.\n학교에 들어가면 탈출 또는 포획 결과가 이곳에 남습니다.",174,384,1220,110,ReadingSize).name="school-history-empty";
            for(int slot=0;slot<4;slot++)
            {
                int index=schoolRecordPage*4+slot; if(index>=summary.recent.Length) break;
                var run=summary.recent[index]; float y=344+slot*108; var card=Panel(150,y,1300,99,Surface); Outline(card,Edge);
                string source=string.IsNullOrEmpty(run.defeatSource)?"기록 미완료":run.defeatSource;
                if(source.Length>32) source=source.Substring(0,32)+"…";
                Text($"{(run.escaped?"탈출 성공":"포획 · "+source)}    기억 {run.recovered}/5    {RunTime(run.seconds)}",174,y+12,1240,40,shell.LargeText?24:22).name="school-history-run-"+index;
                Small($"폭죽 {run.throws}회 · 은신 {run.hides}회 · 문 닫기 {run.closedDoors}회 · 이동 {run.distance:0.0} m · 남은 폭죽 {run.firecrackersRemaining}개"+
                    (run.actionsComplete?"":"  (이어하기 이후 행동)"),174,y+54,1240,36);
            }
            Button("back","돌아가기  [Esc]",775,shell.Back,primary:true);
            if(pages>1)
            {
                Button("school-records-previous","이전",775,()=>{schoolRecordPage--; Rebuild();},895,165).SetEnabled(schoolRecordPage>0);
                var count=Text($"{schoolRecordPage+1} / {pages}",1080,787,165,40,22); count.style.unityTextAlign=TextAnchor.UpperCenter;
                Button("school-records-next","다음",775,()=>{schoolRecordPage++; Rebuild();},1285,165).SetEnabled(schoolRecordPage<pages-1);
            }
        }

        void BuildHud()
        {
            int tokenCount = Mathf.Clamp(session.TotalRecords, 1, memoryTokens.Length);
            float tokenWidth = shell.LargeText ? 24 : 20, tokenHeight = tokenWidth * 1.3f, pitch = tokenWidth + 10;
            memoryStrip = Panel(42, 36, pitch * tokenCount - 10, tokenHeight, Color.clear);
            memoryStrip.name = "memory-progress";
            for (int i = 0; i < tokenCount; i++)
            {
                var token = new MemoryToken(shell.HighContrast) { name = "memory-token-" + i };
                token.style.position = Position.Absolute; token.style.left = i * pitch; token.style.top = 0;
                token.style.width = tokenWidth; token.style.height = tokenHeight;
                memoryStrip.Add(token); memoryTokens[i] = token;
            }
            crosshair = Panel(797.5f, 447.5f, 5, 5, Color.white);
            crosshair.name = "crosshair";
            // Preserve the white dot against bright flashlight-lit surfaces.
            Outline(crosshair, new Color(0, 0, 0, .85f), shell.HighContrast ? 1 : .75f);
            UpdateCrosshair(false);
            caption = Text("", 440, 688, 720, 54, shell.LargeText ? 22 : 19);
            caption.name = "sound-caption";
            caption.style.unityTextAlign = TextAnchor.UpperCenter;
            caption.style.color = Gold;
            focusPanel = Panel(460, 753, 680, 54, new Color(.017f, .026f, .022f, .95f));
            Outline(focusPanel, Edge);
            focus = Text("", 481, 764, 638, 42, shell.LargeText ? 27 : 23);
            focus.style.unityTextAlign = TextAnchor.UpperCenter;
            Small("호흡", 47, 781, 170);
            Panel(47, 817, 265, 6, Edge);
            stamina = Panel(47, 817, 265, 6, Gold);
            meter = Text("", 47, 837, 860, 36, shell.LargeText ? 23 : 19);
            batteryMeter = Text("", 47, 740, 365, 40, shell.LargeText ? 23 : 19);
            batteryMeter.name="flashlight-battery-meter";
            status = Text("", 753, 189, 802, 44, shell.LargeText ? 24 : 20);
            status.name = "player-status";
            status.style.unityTextAlign = TextAnchor.UpperRight;
            status.style.color = Gold;
            noise = Text("", 753, 230, 802, 34, shell.LargeText ? 22 : 19);
            noise.style.unityTextAlign = TextAnchor.UpperRight;
            noise.style.color = Muted;
            // Fit the message instead of covering the upper-right view with an empty box.
            // This sits above the lower-left inventory and leaves the aiming path clear.
            itemFeedbackPanel = Panel(47, 672, 365, 42, new Color(.015f, .027f, .022f, shell.HighContrast ? .95f : .58f));
            itemFeedbackPanel.name = "item-action-feedback-panel";
            itemFeedbackPanel.style.width = itemFeedbackPanel.style.height = StyleKeyword.Auto;
            itemFeedbackPanel.style.maxWidth = 365;
            itemFeedbackPanel.style.paddingLeft = itemFeedbackPanel.style.paddingRight = 10;
            itemFeedbackPanel.style.paddingTop = itemFeedbackPanel.style.paddingBottom = 5;
            itemFeedback = new Label { name = "item-action-feedback", pickingMode = PickingMode.Ignore };
            itemFeedbackPanel.Add(itemFeedback);
            itemFeedback.style.fontSize = shell.LargeText ? 21 : 18;
            itemFeedback.style.whiteSpace = WhiteSpace.Normal;
            itemFeedback.style.maxWidth = 345;
            itemFeedback.style.marginLeft = itemFeedback.style.marginRight = 0;
            itemFeedback.style.marginTop = itemFeedback.style.marginBottom = 0;
            itemFeedback.style.paddingLeft = itemFeedback.style.paddingRight = 0;
            itemFeedback.style.paddingTop = itemFeedback.style.paddingBottom = 0;
            itemFeedback.style.unityTextAlign = TextAnchor.MiddleLeft;
            itemFeedback.style.color = Gold;
            controlsFooter = Small(ControlsFooter, 942, 840, 610);
            controlsFooter.style.unityTextAlign = TextAnchor.UpperRight;
            UpdateHud();
        }
        void UpdateSettingsLabels()
        {
            if (volume == null) return;
            settingsLabelsDirty = false;
            volume.text = $"전체 음량    {Mathf.RoundToInt(shell.Volume * 100)}%";
            sensitivity.text = $"마우스 감도    {shell.Sensitivity / .09f:0.00}×";
            fieldOfView.text = $"시야각    {Mathf.RoundToInt(shell.FieldOfView)}°";
            reducedMotionButton.text = "움직임·깜빡임 완화    " + (shell.ReducedMotion ? "켜짐" : "꺼짐");
            subtitlesButton.text = "소리 자막    " + (shell.Subtitles ? "켜짐" : "꺼짐");
            contrastButton.text = "고대비 UI    " + (shell.HighContrast ? "켜짐" : "꺼짐");
            textSizeButton.text = "큰 글씨    " + (shell.LargeText ? "켜짐" : "꺼짐");
            reducedMotionButton.tooltip = reducedMotionButton.text;
            subtitlesButton.tooltip = subtitlesButton.text;
            contrastButton.tooltip = contrastButton.text;
            textSizeButton.tooltip = textSizeButton.text;
        }
        static void Visible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        void UpdateCrosshair(bool focused)
        {
            float diameter = focused ? 7 : 5;
            float radius = diameter * .5f;
            crosshair.style.width = crosshair.style.height = diameter;
            crosshair.style.left = 800 - radius;
            crosshair.style.top = 450 - radius;
            // UI Toolkit can snap the layout beyond the authored diameter.
            // Unresolved (NaN) dimensions leave the nominal radius intact.
            var resolved = crosshair.resolvedStyle;
            if (resolved.width > diameter) diameter = resolved.width;
            if (resolved.height > diameter) diameter = resolved.height;
            radius = diameter * .5f;
            crosshair.style.borderTopLeftRadius = crosshair.style.borderTopRightRadius = radius;
            crosshair.style.borderBottomLeftRadius = crosshair.style.borderBottomRightRadius = radius;
        }
        void UpdateHud()
        {
            if (!session.player || memoryStrip == null) return;
            var player = session.player;
            for (int i = 0; i < memoryTokens.Length; i++)
                memoryTokens[i]?.SetRecovered(session.RecordsRecovered > i);
            caption.text = shell.Caption;
            Visible(caption, shell.CaptionVisible);
            stamina.style.width = 265 * Mathf.Clamp01(player.Stamina);
            stamina.style.backgroundColor = player.SprintExhausted ? Rust : Gold;
            var charge=player.FlashlightSystem;
            batteryMeter.text=$"손전등 {Mathf.CeilToInt(charge.Fraction*100)}% · {(charge.Depleted?"배터리 필요":charge.Lit?"켜짐":"꺼짐")}";
            batteryMeter.style.color=charge.Fraction<.2f?Rust:Gold;
            int count = player.Firecrackers != null ? player.Firecrackers.Count : 0;
            meter.text = $"숨 {Mathf.RoundToInt(player.Stamina * 100)}%    ·    폭죽 {count}개";
            if (player.SprintExhausted) meter.text += player.Stamina < .25f ? "    걸으며 숨을 회복하세요" : shownGamepad ? "    스틱 버튼을 놓으면 다시 달릴 수 있습니다" : "    Shift를 놓으면 다시 달릴 수 있습니다";
            bool hasFocus = player.Hidden || player.Focus;
            string interact = shownGamepad ? "[아래 버튼]  " : "[E]  ";
            focus.text = player.Hidden ? interact + "숨은 곳에서 나오기" : player.Focus ? interact + player.Focus.DisplayLabel : string.Empty;
            Visible(focus, hasFocus);
            Visible(focusPanel, hasFocus);
            UpdateCrosshair(player.Focus);
            status.text = player.Hidden ? (player.HidingThreatCueActive ? "문 앞에서 공격 준비 · [E] 지금 나오세요" : "캐비닛 안 · 발소리를 듣고 움직이세요") : player.SlowRemaining > 0 ? $"이동 속도 감소  ·  {Mathf.CeilToInt(player.SlowRemaining)}초" : player.ActualSpeed <= .12f ? (player.Crouching ? "낮은 자세 · 멈춰서 숨을 고릅니다" : "멈춰서 숨을 고릅니다") : player.Crouching ? "낮은 자세 · 천천히 조용하게 이동합니다" : player.Running ? "달리는 중 · 발소리가 멀리 퍼집니다" : "걷는 중 · 물에서는 발소리가 더 멀리 퍼집니다";
            status.style.color = player.HidingThreatCueActive ? Rust : Gold;
            var inventory = player.Firecrackers;
            bool showAim = inventory && inventory.Aim && inventory.Aim.Visible;
            bool showItemFeedback = inventory && (inventory.FeedbackVisible || showAim);
            itemFeedback.text = showAim ? inventory.Aim.Hint : showItemFeedback ? inventory.ActionFeedback : string.Empty;
            Visible(itemFeedback, showItemFeedback); Visible(itemFeedbackPanel, showItemFeedback);
            itemFeedbackPanel.style.opacity = showAim ? 1 : inventory ? Mathf.Clamp01(inventory.FeedbackRemaining / .25f) : 0;
            string sound = player.FootstepNoiseRadius <= 0 ? "없음" : player.FootstepNoiseRadius <= 3.5f ? "작음" : player.FootstepNoiseRadius <= 7 ? "보통" : "큼";
            noise.text = "내 발소리  " + sound;
            var detection = player.GetComponent<DetectionFeedback>();
            bool threatened = detection && detection.PeripheralStrength > .001f;
            Visible(threatVeil, threatened);
            if (threatened)
            {
                threatVeil.style.backgroundImage = detection.PeripheralTexture;
                threatVeil.style.opacity = detection.PeripheralStrength;
            }
            noise.style.color = player.FootstepNoiseRadius > 7 ? Rust : Muted;
        }
        void LateUpdate()
        {
            if (root == null || !shell || !session) return;
            bool changedRecords = journalStep != session.RecordsRecovered || journalExploration != session.ExplorationCount;
            if (shownGamepad != PlayerControls.UsingGamepad)
            {
                shownGamepad = PlayerControls.UsingGamepad;
                if (navigationHelp != null) navigationHelp.text = NavigationHelp;
                if (controlInstructions != null) controlInstructions.text = ControlInstructions;
                if (controlsFooter != null) controlsFooter.text = ControlsFooter;
            }
            if (shown != shell.Screen || shownContrast != shell.HighContrast || shownLargeText != shell.LargeText ||
                shownReloading != shell.IsReloading || shownReloadError != shell.ReloadError || changedRecords && shown != GameShell.Page.Playing)
                Rebuild();
            if (shown == GameShell.Page.Settings && settingsLabelsDirty) UpdateSettingsLabels();
            if (shown == GameShell.Page.Playing) UpdateHud();
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
            if (shell) shell.SettingsChanged -= OnSettingsChanged;
            if (settings) settings.targetTexture = null;
            if (CaptureTarget) { CaptureTarget.Release(); Destroy(CaptureTarget); }
            if (document) Destroy(document);
            if (settings) Destroy(settings);
        }
    }
}
