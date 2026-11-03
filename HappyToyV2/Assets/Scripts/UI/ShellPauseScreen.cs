using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Pause presentation owns its controls and receives availability and actions.</summary>
    internal sealed class ShellPauseScreen
    {
        internal struct State
        {
            public string SuspensionMessage;
            public bool CorridorMode, ChapterMode, CanSuspendCorridor, CanSuspendChapter, ShowSuspensionMessage;
            public static State Read(GameSession session, GameShell shell) => new State {
                CorridorMode = session.CorridorMode, ChapterMode = session.ChapterMode,
                CanSuspendCorridor = session.CorridorMode && session.Suspension.Writable && string.IsNullOrEmpty(session.Corridor.SuspendBlockReason),
                CanSuspendChapter = session.ChapterMode && session.ChapterSuspension.Writable && string.IsNullOrEmpty(session.Chapter.SuspendBlockReason),
                ShowSuspensionMessage = string.IsNullOrEmpty(shell.ReloadError),
                SuspensionMessage = session.ChapterMode && !session.ChapterSuspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.ChapterSuspension.Status) ? "현재 학교 저장을 사용할 수 없습니다." : session.ChapterSuspension.Status) :
                    session.ChapterMode && !string.IsNullOrEmpty(session.Chapter.SuspendBlockReason) ? session.Chapter.SuspendBlockReason :
                    session.CorridorMode && !session.Suspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.Suspension.Status) ? "현재 회랑 저장을 사용할 수 없습니다." : session.Suspension.Status) :
                    session.CorridorMode && !string.IsNullOrEmpty(session.Corridor.SuspendBlockReason) ? session.Corridor.SuspendBlockReason : null };
        }
        internal sealed class Actions
        {
            public Action Resume, Journal, Settings, Restart, Title, SuspendCorridor, SuspendChapter;
        }
        public void Build(ShellLayout layout, State state, Actions actions)
        {
            var accent = layout.HighContrast ? new Color(1f,.55f,.47f) : new Color(.73f,.27f,.24f);
            var heading=layout.Text("일시 정지", 175, 149, 620, 82, layout.LargeText ? 49 : 44);
            heading.style.color=layout.HighContrast?Color.white:new Color(.88f,.87f,.83f);

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
            if(state.ShowSuspensionMessage&&!string.IsNullOrWhiteSpace(state.SuspensionMessage))
            {
                var notice=layout.Small(state.SuspensionMessage, 176, 759, 700, 58);
                notice.style.color=layout.HighContrast?Color.white:new Color(.72f,.72f,.69f);
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
            marker.style.position=Position.Absolute;marker.style.left=0;marker.style.top=14;
            marker.style.width=layout.HighContrast?5:3;marker.style.height=28;marker.style.backgroundColor=accent;
            button.Add(marker);
            bool hovered=false,focused=false;
            void Refresh()
            {
                bool active=hovered||focused;
                button.style.color=active?Color.white:new Color(.68f,.69f,.67f);
                button.style.backgroundColor=Color.clear;
                marker.style.opacity=active?1:0;
                layout.Outline(button,Color.clear,0);
            }
            button.RegisterCallback<FocusInEvent>(_=>{focused=true;Refresh();});
            button.RegisterCallback<FocusOutEvent>(_=>{focused=false;Refresh();});
            button.RegisterCallback<PointerEnterEvent>(_=>{hovered=true;Refresh();});
            button.RegisterCallback<PointerLeaveEvent>(_=>{hovered=false;Refresh();});
            Refresh();return button;
        }

    }
}
