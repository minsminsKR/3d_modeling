using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;

namespace HappyToy.V2
{
    /// <summary>Scene-independent, keyboard-accessible UI. All measurements use a letterboxed safe area.</summary>
    [DisallowMultipleComponent]
    public sealed class GameShellView : MonoBehaviour
    {
        GameShell shell;
        GameSession session;
        UIDocument document;
        PanelSettings settings;
        VisualElement root, stage, stamina, crosshair, noticePanel, noticeAccent, focusPanel, captionPanel;
        Label objective, objectiveCount, notice, focus, meter, status, noise, caption, volume, sensitivity, fieldOfView;
        Button reducedMotionButton, subtitlesButton, contrastButton, textSizeButton;
        GameShell.Page shown = (GameShell.Page)(-1);
        int journalStep = -1, journalExploration = -1, journalPage;
        bool shownContrast, shownLargeText, settingsLabelsDirty;
        bool shownReloading;
        string shownReloadError;
        readonly VisualElement[] objectiveSegments = new VisualElement[7];

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
            var label = Text(text, x, y, width, height, shell.LargeText ? 20 : 17);
            label.style.color = Muted;
            return label;
        }

        void Rebuild()
        {
            if (root == null || !shell || !session) return;
            string previousFocus = (root.focusController?.focusedElement as VisualElement)?.name;
            bool samePage = shown == shell.Screen;
            if (!samePage) journalPage = 0;
            shown = shell.Screen;
            shownContrast = shell.HighContrast;
            shownLargeText = shell.LargeText;
            shownReloading = shell.IsReloading;
            shownReloadError = shell.ReloadError;
            journalStep = session.StoryStep;
            journalExploration = session.ExplorationCount;
            root.Clear();
            objective = objectiveCount = notice = focus = meter = status = noise = caption = volume = sensitivity = fieldOfView = null;
            reducedMotionButton = subtitlesButton = contrastButton = textSizeButton = null;
            stamina = crosshair = noticePanel = noticeAccent = focusPanel = captionPanel = null;
            root.style.color = Paper;
            root.style.backgroundColor = shown == GameShell.Page.Playing ? Color.clear : new Color(.013f, .025f, .021f, .97f);
            root.pickingMode = shown == GameShell.Page.Playing ? PickingMode.Ignore : PickingMode.Position;
            stage = new VisualElement { name = "safe-area", pickingMode = PickingMode.Ignore };
            stage.style.width = 1600;
            stage.style.height = 900;
            stage.style.flexShrink = 0;
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
            BuildFrame();
            switch (shown)
            {
                case GameShell.Page.Title: BuildTitle(); break;
                case GameShell.Page.Pause: BuildPause(); break;
                case GameShell.Page.Journal: BuildJournal(); break;
                case GameShell.Page.Settings: BuildSettings(); break;
                case GameShell.Page.Result: BuildResult(); break;
            }
            if (shell.IsReloading)
            {
                Small("새 탐색을 준비하고 있습니다…", 150, 793, 1300, 35).style.color = Gold;
                stage.Query<Button>().ForEach(button => button.SetEnabled(false));
            }
            else if (!string.IsNullOrWhiteSpace(shell.ReloadError))
                Small(shell.ReloadError, 150, 786, 1300, 48).style.color = Rust;
            Small("HAPPY TOY  /  LAST ATTENDANCE", 150, 837, 580);
            var navigation = Small("Tab / 방향키  선택      Enter  확인      Esc  뒤로", 820, 837, 630);
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
            Small("폐교 조사 기록  /  마지막 출석", 150, 78, 1050);
            var paused = Small(shown == GameShell.Page.Title ? "아직 끝나지 않은 하교 시간" : "시간이 멈춰 있습니다", 1060, 78, 390);
            paused.style.unityTextAlign = TextAnchor.UpperRight;
            string title = shown == GameShell.Page.Title ? "마지막 출석" : shown == GameShell.Page.Pause ? "숨을 고르다" :
                shown == GameShell.Page.Journal ? "조사 기록" : shown == GameShell.Page.Settings ? "환경 설정" :
                session.Escaped ? "마지막 아이가 하교했습니다" : "발소리가 멈췄습니다";
            Text(title, 150, 145, 1300, 82, shown == GameShell.Page.Title ? 66 : 48).style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        void BuildTitle()
        {
            Text("지워진 이름 하나.\n돌아오지 않은 아이 한 명.", 153, 262, 640, 110, shell.LargeText ? 32 : 28);
            Small("흩어진 기록을 모아 마지막 하교를 완성하세요.", 153, 374, 640, 55);
            Button("begin", "학교에 들어가기  [Enter]", 451, shell.Begin, primary: true);
            Button("settings", "환경 설정 · 접근성", 519, shell.Settings);
            Button("quit", "종료", 587, () => Application.Quit());
            Small("이야기 진행은 현재 탐색 중에만 유지됩니다.\n갑작스러운 소리와 추격 장면이 포함되어 있습니다.", 153, 677, 650, 70);
            BuildAttendanceCard(895, 245, false);
        }
        void BuildAttendanceCard(float x, float y, bool progress)
        {
            var card = Panel(x, y, 540, 510, Surface);
            Outline(card, Edge);
            Panel(x + 26, y + 27, 3, 70, Gold);
            Small("SCHOOL ARCHIVE  /  출석 확인", x + 46, y + 28, 450);
            Text(progress ? $"복원한 기록  {session.StoryStep} / 4" : "출석 확인서", x + 46, y + 61, 460, 48, 30);
            for (int i = 0; i < 4; i++)
            {
                float lineY = y + 136 + i * 43;
                Small($"0{i + 1}", x + 30, lineY, 46);
                Panel(x + 88, lineY + 9, 255 - i * 24, 9, new Color(.16f, .22f, .18f));
                Small(progress && session.StoryStep > i ? "확인" : "미확인", x + 419, lineY, 95).style.color = progress && session.StoryStep > i ? Gold : Muted;
                Panel(x + 27, lineY + 33, 486, 1, Edge);
            }
            Panel(x + 26, y + 333, 487, 1, Edge);
            Text("WASD  이동       마우스  시선\nShift  달리기     C / Ctrl  낮은 자세\nE  조사·문·은신\nF  손전등          Q  폭죽 던지기\nJ  조사 기록      Esc  일시정지", x + 30, y + 345, 485, 155, shell.LargeText ? 23 : 21);
        }
        void BuildPause()
        {
            Small("현재 목표", 153, 249, 640);
            Text(session.Objective, 153, 286, 650, 80, ReadingSize);
            Button("resume", "계속하기  [Esc]", 387, shell.Resume, primary: true);
            Button("journal", "조사 기록  [J]", 455, shell.Journal);
            Button("settings", "환경 설정 · 접근성", 523, shell.Settings);
            Button("restart", "처음부터 다시 시작", 591, () => shell.Restart(true));
            Button("title", "시작 화면으로 (진행 초기화)", 659, () => shell.Restart(false));
            BuildAttendanceCard(895, 245, true);
            Small("다시 시작하거나 시작 화면으로 가면 이번 탐색 기록이 사라집니다.", 153, 762, 1270);
        }
        void BuildJournal()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt((4 + session.ExplorationCount) / 6f));
            journalPage = Mathf.Clamp(journalPage, 0, pages - 1);
            Small($"복원 {session.RecordsRecovered} / {session.TotalRecords}     주변 기록 {session.ExplorationCount}개     ·     발견한 내용만 보관합니다", 153, 218, 1290);
            for (int slot = 0; slot < 6; slot++)
            {
                int index = journalPage * 6 + slot;
                float x = 150 + slot % 2 * 665, y = 264 + slot / 2 * 158;
                string entry = index < 4 ? session.JournalEntry(index) : session.ExplorationEntry(index - 4);
                string placeholder = index < 4 ? "아직 발견하지 못했습니다.\n학교 안에서 단서를 찾아 조사하세요." : "아직 비어 있습니다.\n게시물을 조사하면 이곳에 남습니다.";
                var card = Panel(x, y, 635, 148, Surface);
                Outline(card, entry == null ? Edge : new Color(.39f, .43f, .30f));
                Panel(x, y, 3, 148, entry == null ? Edge : Gold);
                Small(index < 4 ? $"필수 기록  0{index + 1}" : $"주변 기록  {index - 3:00}", x + 20, y + 11, 460, 25).style.color = entry == null ? Muted : Gold;
                var label = Text(entry ?? placeholder, x + 20, y + 43, 596, 103, shell.LargeText ? 22 : 20);
                label.name = index < 4 ? "record-" + index : "inspection-" + (index - 4);
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
            volume = Text("", 174, 295, 580, 38, ReadingSize);
            Button("volume-down", "− 5%", 343, () => shell.AdjustSettings(-.05f, 0), 174, 270);
            Button("volume-up", "+ 5%", 343, () => shell.AdjustSettings(.05f, 0), 466, 290);
            sensitivity = Text("", 174, 425, 580, 38, ReadingSize);
            Button("mouse-down", "감도 낮추기", 473, () => shell.AdjustSettings(0, -.009f), 174, 270);
            Button("mouse-up", "감도 높이기", 473, () => shell.AdjustSettings(0, .009f), 466, 290);
            fieldOfView = Text("", 174, 555, 580, 38, ReadingSize);
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
            Small(session.Escaped ? "조사 종료  /  하교 확인" : "조사 중단  /  기록 미완료", 153, 247, 1170);
            string message = session.Escaped ? "지워졌던 이름을 다시 적었다.\n복도 너머에서 마지막 문이 닫힌다." : "이곳에는 아직 돌아오지 못한 기록이 있습니다.\n다시 들어가 마지막 이름을 찾아주세요.";
            Text(message, 153, 294, 1210, 103, shell.LargeText ? 30 : 27);
            Panel(895, 437, 540, 305, Surface);
            Small(session.Escaped ? "마지막 출석 확인" : "다음 탐색을 위한 기록", 920, 460, 490);
            Text($"복원한 기록  {session.RecordsRecovered} / {session.TotalRecords}\n탐색 시간  {Mathf.FloorToInt(session.ElapsedPlayTime / 60)}분 {Mathf.FloorToInt(session.ElapsedPlayTime % 60):00}초", 920, 505, 490, 85, ReadingSize);
            string hint = session.Escaped ? "모든 이름이 제자리로 돌아왔습니다." : !string.IsNullOrWhiteSpace(session.DefeatHint) ? session.DefeatHint : "문을 닫아 시선을 끊고 걸으며 숨을 회복하세요.";
            Text(hint, 920, 603, 490, 126, shell.LargeText ? 23 : 20).style.color = session.Escaped ? Gold : Muted;
            Button("restart", session.Escaped ? "다시 탐색하기" : "다시 시작", 447, () => shell.Restart(true), primary: true);
            Button("title", "시작 화면으로", 515, () => shell.Restart(false));
            Button("quit", "종료", 583, () => Application.Quit());
            if (!session.Escaped) Small("다시 시작하면 이번 탐색 기록은 초기화됩니다.", 153, 680, 675, 70);
        }

        void BuildHud()
        {
            var objectivePanel = Panel(42, 32, 688, 143, new Color(.015f, .027f, .022f, shell.HighContrast ? 1 : .93f));
            Outline(objectivePanel, Edge);
            Panel(42, 32, 3, 143, Gold);
            Small("현재 목표", 65, 47, 220);
            objectiveCount = Small("", 525, 47, 180);
            objectiveCount.style.unityTextAlign = TextAnchor.UpperRight;
            objective = Text(session.Objective, 65, 82, 641, 74, ReadingSize);
            int segmentCount = Mathf.Clamp(session.TotalRecords, 1, objectiveSegments.Length);
            float segmentWidth = 638f / segmentCount;
            for (int i = 0; i < segmentCount; i++) objectiveSegments[i] = Panel(66 + segmentWidth * i, 163, segmentWidth - 9, 3, Edge);
            crosshair = Panel(797, 447, 6, 6, Paper);
            crosshair.name = "crosshair";
            noticePanel = Panel(355, 618, 890, 129, Surface);
            Outline(noticePanel, Edge);
            noticeAccent = Panel(355, 618, 3, 129, Gold);
            noticeAccent.name = "notice-accent";
            notice = Text("", 379, 632, 840, 104, shell.LargeText ? 26 : 23);
            captionPanel = Panel(355, 526, 890, 77, new Color(.01f, .015f, .012f, .93f));
            caption = Text("", 379, 536, 842, 62, shell.LargeText ? 24 : 21);
            caption.style.unityTextAlign = TextAnchor.UpperCenter;
            caption.style.color = Gold;
            focusPanel = Panel(460, 753, 680, 54, new Color(.017f, .026f, .022f, .95f));
            Outline(focusPanel, Edge);
            focus = Text("", 481, 764, 638, 37, shell.LargeText ? 27 : 23);
            focus.style.unityTextAlign = TextAnchor.UpperCenter;
            Small("호흡", 47, 781, 170);
            Panel(47, 817, 265, 6, Edge);
            stamina = Panel(47, 817, 265, 6, Gold);
            meter = Text("", 47, 837, 860, 32, shell.LargeText ? 23 : 19);
            status = Text("", 753, 189, 802, 44, shell.LargeText ? 24 : 20);
            status.style.unityTextAlign = TextAnchor.UpperRight;
            status.style.color = Gold;
            noise = Text("", 753, 230, 802, 34, shell.LargeText ? 22 : 19);
            noise.style.unityTextAlign = TextAnchor.UpperRight;
            noise.style.color = Muted;
            Small("C  낮은 자세    F  빛    Q  폭죽    J  기록    Esc  메뉴", 942, 840, 610).style.unityTextAlign = TextAnchor.UpperRight;
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
        void UpdateHud()
        {
            if (!session.player || objective == null) return;
            var player = session.player;
            objective.text = session.Objective;
            objectiveCount.text = $"기록 {session.RecordsRecovered} / {session.TotalRecords}";
            for (int i = 0; i < objectiveSegments.Length; i++)
                if (objectiveSegments[i] != null) objectiveSegments[i].style.backgroundColor = session.RecordsRecovered > i ? Gold : Edge;
            notice.text = session.Notice;
            Visible(notice, shell.NoticeVisible);
            Visible(noticePanel, shell.NoticeVisible);
            Visible(noticeAccent, shell.NoticeVisible);
            caption.text = shell.Caption;
            Visible(caption, shell.CaptionVisible);
            Visible(captionPanel, shell.CaptionVisible);
            stamina.style.width = 265 * Mathf.Clamp01(player.Stamina);
            stamina.style.backgroundColor = player.SprintExhausted ? Rust : Gold;
            int count = player.Firecrackers != null ? player.Firecrackers.Count : 0;
            meter.text = $"숨 {Mathf.RoundToInt(player.Stamina * 100)}%    ·    폭죽 {count}개";
            if (player.SprintExhausted) meter.text += player.Stamina < .25f ? "    걸으며 숨을 회복하세요" : "    Shift를 놓으면 다시 달릴 수 있습니다";
            bool hasFocus = player.Hidden || player.Focus;
            focus.text = player.Hidden ? "[E]  숨은 곳에서 나오기" : player.Focus ? "[E]  " + player.Focus.DisplayLabel : string.Empty;
            Visible(focus, hasFocus);
            Visible(focusPanel, hasFocus);
            crosshair.style.backgroundColor = player.Focus ? Gold : Paper;
            crosshair.style.width = crosshair.style.height = player.Focus ? 9 : 6;
            crosshair.style.left = player.Focus ? 795.5f : 797;
            crosshair.style.top = player.Focus ? 445.5f : 447;
            status.text = player.Hidden ? "은신 중 · 들키지 않도록 기다리세요" : player.SlowRemaining > 0 ? $"이동 속도 감소  ·  {Mathf.CeilToInt(player.SlowRemaining)}초" : player.Crouching ? "낮은 자세 · 천천히 조용하게 이동합니다" : player.Running ? "달리는 중 · 발소리가 멀리 퍼집니다" : "걷는 중 · 물에서는 발소리가 더 멀리 퍼집니다";
            string sound = player.FootstepNoiseRadius <= 0 ? "없음" : player.FootstepNoiseRadius <= 3.5f ? "작음" : player.FootstepNoiseRadius <= 7 ? "보통" : "큼";
            noise.text = "내 발소리  " + sound + (player.Hidden ? "" : player.flashlight && player.flashlight.enabled ? "    ·    손전등 켜짐" : "    ·    손전등 꺼짐");
            noise.style.color = player.FootstepNoiseRadius > 7 ? Rust : Muted;
        }
        void LateUpdate()
        {
            if (root == null || !shell || !session) return;
            bool changedRecords = journalStep != session.StoryStep || journalExploration != session.ExplorationCount;
            if (shown != shell.Screen || shownContrast != shell.HighContrast || shownLargeText != shell.LargeText ||
                shownReloading != shell.IsReloading || shownReloadError != shell.ReloadError || changedRecords && shown != GameShell.Page.Playing)
                Rebuild();
            if (shown == GameShell.Page.Settings && settingsLabelsDirty) UpdateSettingsLabels();
            if (shown == GameShell.Page.Playing) UpdateHud();
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
