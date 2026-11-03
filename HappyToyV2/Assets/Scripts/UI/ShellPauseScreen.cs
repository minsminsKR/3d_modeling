using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Pause presentation owns its controls and receives a progress snapshot and actions.</summary>
    internal sealed class ShellPauseScreen
    {
        Label controlInstructions;
        public void UpdateControls(string text) { if (controlInstructions != null) controlInstructions.text = text; }
        internal struct State
        {
            public string Objective, SuspensionMessage, SchoolClock;
            public bool CorridorMode, ChapterMode, SchoolLoopMode, CanSuspendCorridor, CanSuspendChapter, ShowSuspensionMessage;
            public int CorridorVersion, RecordsRecovered, TotalRecords, StoryStep;
            public static State Read(GameSession session, GameShell shell) => new State {
                Objective = session.Objective, CorridorMode = session.CorridorMode, ChapterMode = session.ChapterMode,
                SchoolLoopMode = session.ChapterMode && session.Chapter.Loop,
                SchoolClock = session.ChapterMode && session.Chapter.Loop ? SchoolProgressClock.Format(session.Chapter.Loop.ClockHours) : null,
                CorridorVersion = session.CorridorMode ? session.Corridor.Layout.Version : 0,
                RecordsRecovered = session.RecordsRecovered, TotalRecords = session.TotalRecords, StoryStep = session.StoryStep,
                CanSuspendCorridor = session.CorridorMode && session.Suspension.Writable && string.IsNullOrEmpty(session.Corridor.SuspendBlockReason),
                CanSuspendChapter = session.ChapterMode && session.ChapterSuspension.Writable && string.IsNullOrEmpty(session.Chapter.SuspendBlockReason),
                ShowSuspensionMessage = string.IsNullOrEmpty(shell.ReloadError),
                SuspensionMessage = session.ChapterMode && !session.ChapterSuspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.ChapterSuspension.Status) ? "현재 학교 저장을 사용할 수 없습니다." : session.ChapterSuspension.Status) :
                    session.ChapterMode && !string.IsNullOrEmpty(session.Chapter.SuspendBlockReason) ? session.Chapter.SuspendBlockReason :
                    session.CorridorMode && !session.Suspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.Suspension.Status) ? "현재 회랑 저장을 사용할 수 없습니다." : session.Suspension.Status) :
                    session.CorridorMode && !string.IsNullOrEmpty(session.Corridor.SuspendBlockReason) ? session.Corridor.SuspendBlockReason :
                    session.ChapterMode ? "학교 저장으로 나가면 기억과 현재 상태를 이어갑니다. 처음부터 시작하면 저장된 학교 탐색을 초기화합니다." :
                    "중단 저장으로 나가면 같은 회랑을 이어갈 수 있습니다. 다시 시작은 현재 탐색을 초기화합니다." };
        }
        internal sealed class Actions
        {
            public Action Resume, Journal, Settings, Restart, Title, SuspendCorridor, SuspendChapter;
        }
        public void Build(ShellLayout layout, State state, Actions actions, string controlText)
        {
            var accent = layout.HighContrast ? new Color(1f,.50f,.43f) : new Color(.82f,.31f,.27f);
            var muted = layout.HighContrast ? new Color(.90f,.90f,.88f) : new Color(.70f,.73f,.72f);
            layout.Panel(153, 126, 4, 63, accent);
            var eyebrow=layout.Small("PAUSED  /  잠시 멈춤", 177, 124, 470, 32);
            eyebrow.style.color=muted;
            var heading=layout.Text("일시 정지", 174, 161, 620, 85, layout.LargeText ? 53 : 48);
            heading.style.unityFontStyleAndWeight=FontStyle.Bold;
            layout.Panel(176, 251, 520, 1, new Color(.70f,.72f,.69f,.30f));

            float y=278;
            PauseButton(layout,"resume","계속하기",y,actions.Resume,accent);y+=65;
            PauseButton(layout,"journal","조사 기록",y,actions.Journal,accent);y+=65;
            PauseButton(layout,"settings","환경 설정 · 접근성",y,actions.Settings,accent);y+=65;
            if(state.CorridorMode)
            {
                var suspend=PauseButton(layout,"suspend-run","탐색 저장 후 나가기",y,actions.SuspendCorridor,accent);
                suspend.SetEnabled(state.CanSuspendCorridor);
                if(!state.CanSuspendCorridor)suspend.style.opacity=.38f;
                y+=65;
            }
            if(state.ChapterMode)
            {
                var suspend=PauseButton(layout,"suspend-chapter","학교 저장 후 나가기",y,actions.SuspendChapter,accent);
                suspend.SetEnabled(state.CanSuspendChapter);
                if(!state.CanSuspendChapter)suspend.style.opacity=.38f;
                y+=65;
            }
            PauseButton(layout,"restart",state.CorridorMode?"새 회랑에서 다시 시작":"처음부터 다시 시작",y,actions.Restart,accent);y+=65;
            PauseButton(layout,"title","시작 화면으로 (진행 초기화)",y,actions.Title,accent);
            BuildStatus(layout,state,controlText,accent,muted);
            if(state.ShowSuspensionMessage)
            {
                var notice=layout.Small(state.SuspensionMessage, 177, 760, 1250, 58);
                notice.style.color=muted;
            }
        }

        static Button PauseButton(ShellLayout layout,string id,string text,float y,Action action,Color accent)
        {
            var button=new Button(action){name=id,text=text,tooltip=text};
            layout.Place(button, 174, y, 530, 56);
            button.style.fontSize=layout.LargeText?27:24;
            button.style.unityTextAlign=TextAnchor.MiddleLeft;
            button.style.paddingLeft=25;button.style.paddingRight=40;
            button.style.paddingTop=button.style.paddingBottom=0;
            button.style.borderTopLeftRadius=button.style.borderTopRightRadius=0;
            button.style.borderBottomLeftRadius=button.style.borderBottomRightRadius=0;
            var marker=new VisualElement{name=id+"-selection",pickingMode=PickingMode.Ignore};
            marker.style.position=Position.Absolute;marker.style.left=0;marker.style.top=9;
            marker.style.width=4;marker.style.height=38;marker.style.backgroundColor=accent;
            button.Add(marker);
            bool hovered=false,focused=false;
            void Refresh()
            {
                bool active=hovered||focused;
                button.style.color=active?Color.white:new Color(.78f,.80f,.79f);
                button.style.backgroundColor=active?new Color(.22f,.19f,.18f,.88f):Color.clear;
                marker.style.opacity=active?1:0;
                layout.Outline(button,active?new Color(accent.r,accent.g,accent.b,.75f):Color.clear,focused?2:1);
            }
            button.RegisterCallback<FocusInEvent>(_=>{focused=true;Refresh();});
            button.RegisterCallback<FocusOutEvent>(_=>{focused=false;Refresh();});
            button.RegisterCallback<PointerEnterEvent>(_=>{hovered=true;Refresh();});
            button.RegisterCallback<PointerLeaveEvent>(_=>{hovered=false;Refresh();});
            Refresh();return button;
        }

        void BuildStatus(ShellLayout layout,State state,string controlText,Color accent,Color muted)
        {
            const float x=900,y=245;
            var panel=layout.Panel(x,y,510,515,new Color(.025f,.031f,.031f,layout.HighContrast?.94f:.76f));
            layout.Outline(panel,new Color(.72f,.72f,.68f,.25f));
            layout.Panel(x+28,y+28,40,2,accent);
            var title=layout.Small("현재 목표",x+28,y+48,440,32);title.style.color=muted;
            layout.Text(state.Objective,x+28,y+87,454,125,layout.LargeText?23:21).name="pause-objective";
            layout.Panel(x+28,y+222,454,1,new Color(.72f,.72f,.68f,.24f));
            bool memories=state.CorridorMode||state.ChapterMode;
            int total=state.CorridorMode?state.TotalRecords:state.ChapterMode?5:4;
            int current=memories?state.RecordsRecovered:state.StoryStep;
            string progressText=state.SchoolLoopMode?$"진행 시각  {state.SchoolClock}":
                memories?$"회수한 기억  {current} / {total}":$"주요 단계  {current} / {total}";
            var progress=layout.Text(progressText,x+28,y+233,454,40,layout.LargeText?25:22);
            progress.name="pause-progress";
            var controls=layout.Small("조작 안내",x+28,y+279,450,32);controls.style.color=muted;
            controlInstructions=layout.Text(controlText,x+28,y+315,454,190,layout.LargeText?21:19);
        }
    }
}
