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
        public void Build(ShellLayout layout, State state, Actions actions, bool reducedMotion)
        {
            var accent = layout.HighContrast ? new Color(1f,.55f,.47f) : new Color(.73f,.27f,.24f);
            var face=Resources.Load<Font>("Fonts/PauseMyeongjo");
            var heading=layout.Text("일시 정지", 175, 149, 620, 90, layout.LargeText ? 52 : 47);
            if(face)heading.style.unityFont=face;
            heading.style.letterSpacing=1.5f;
            heading.style.color=layout.HighContrast?Color.white:new Color(.88f,.87f,.83f);

            float y=278;
            PauseButton(layout,"resume","계속하기",y,actions.Resume,accent,face,reducedMotion);y+=65;
            PauseButton(layout,"journal","조사 기록",y,actions.Journal,accent,face,reducedMotion);y+=65;
            PauseButton(layout,"settings","환경 설정 · 접근성",y,actions.Settings,accent,face,reducedMotion);y+=65;
            if(state.CorridorMode)
            {
                var suspend=PauseButton(layout,"suspend-run","탐색 저장 후 나가기",y,actions.SuspendCorridor,accent,face,reducedMotion);
                suspend.SetEnabled(state.CanSuspendCorridor);
                if(!state.CanSuspendCorridor)suspend.style.opacity=.38f;
                y+=65;
            }
            if(state.ChapterMode)
            {
                var suspend=PauseButton(layout,"suspend-chapter","학교 저장 후 나가기",y,actions.SuspendChapter,accent,face,reducedMotion);
                suspend.SetEnabled(state.CanSuspendChapter);
                if(!state.CanSuspendChapter)suspend.style.opacity=.38f;
                y+=65;
            }
            PauseButton(layout,"restart",state.CorridorMode?"새 회랑에서 다시 시작":"처음부터 다시 시작",y,actions.Restart,accent,face,reducedMotion);y+=65;
            PauseButton(layout,"title","시작 화면으로 (진행 초기화)",y,actions.Title,accent,face,reducedMotion);
            if(state.ShowSuspensionMessage&&!string.IsNullOrWhiteSpace(state.SuspensionMessage))
            {
                var notice=layout.Small(state.SuspensionMessage, 176, 759, 700, 58);
                notice.style.color=layout.HighContrast?Color.white:new Color(.72f,.72f,.69f);
            }
        }

        static Button PauseButton(ShellLayout layout,string id,string text,float y,Action action,Color accent,Font face,bool reducedMotion)
        {
            var button=new Button(action){name=id,text=text,tooltip=text};
            layout.Place(button, 174, y, 530, 56);
            if(face)button.style.unityFont=face;
            button.style.fontSize=layout.LargeText?29:26;
            button.style.letterSpacing=.55f;
            button.style.unityTextAlign=TextAnchor.MiddleLeft;
            button.style.paddingLeft=25;button.style.paddingRight=40;
            button.style.paddingTop=button.style.paddingBottom=0;
            button.style.borderTopLeftRadius=button.style.borderTopRightRadius=0;
            button.style.borderBottomLeftRadius=button.style.borderBottomRightRadius=0;
            var marker=new VisualElement{name=id+"-selection",pickingMode=PickingMode.Ignore};
            marker.style.position=Position.Absolute;marker.style.left=0;marker.style.top=14;
            marker.style.width=layout.HighContrast?5:3;marker.style.height=28;marker.style.backgroundColor=accent;
            button.Add(marker);
            var trace=new VisualElement{name=id+"-trace",pickingMode=PickingMode.Ignore};
            trace.style.position=Position.Absolute;trace.style.left=25;trace.style.top=49;
            trace.style.height=2;trace.style.backgroundColor=accent;button.Add(trace);
            button.style.backgroundColor=Color.clear;layout.Outline(button,Color.clear,0);
            float amount=0,startAmount=0,target=0;double began=0;
            IVisualElementScheduledItem animation=null;
            void Paint(float value)
            {
                amount=value;
                button.style.color=Color.Lerp(new Color(.68f,.69f,.67f),Color.white,value);
                button.style.paddingLeft=25+6*value;
                marker.style.opacity=value;trace.style.opacity=.72f*value;trace.style.width=46*value;
            }
            void Animate()
            {
                float t=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-began)/.18));
                Paint(Mathf.Lerp(startAmount,target,Mathf.SmoothStep(0,1,t)));
                if(t>=1)animation.Pause();
            }
            void Select(bool selected)
            {
                target=selected?1:0;
                if(reducedMotion){Paint(target);return;}
                startAmount=amount;began=Time.realtimeSinceStartupAsDouble;animation.Resume();
            }
            animation=button.schedule.Execute(Animate).Every(16);animation.Pause();Paint(0);
            button.RegisterCallback<FocusInEvent>(_=>Select(true));
            button.RegisterCallback<FocusOutEvent>(_=>Select(false));
            button.RegisterCallback<PointerEnterEvent>(_=>{if(button.enabledInHierarchy)button.Focus();});
            return button;
        }

    }
}
