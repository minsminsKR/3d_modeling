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
            public bool CorridorMode, ChapterMode, TempleMode, CanSuspendCorridor, CanSuspendChapter, CanSuspendTemple, ShowSuspensionMessage;
            public static State Read(GameSession session, GameShell shell) => new State {
                CorridorMode = session.CorridorMode, ChapterMode = session.ChapterMode,
                TempleMode = session.PrologueMode, CanSuspendTemple = session.PrologueMode && !session.Prologue.ApproachActive && session.TempleSuspension.Writable,
                CanSuspendCorridor = session.CorridorMode && session.Suspension.Writable && string.IsNullOrEmpty(session.Corridor.SuspendBlockReason),
                CanSuspendChapter = session.ChapterMode && session.ChapterSuspension.Writable && string.IsNullOrEmpty(session.Chapter.SuspendBlockReason),
                ShowSuspensionMessage = string.IsNullOrEmpty(shell.ReloadError),
                SuspensionMessage = session.PrologueMode && session.Prologue.ApproachActive ? "사찰에 도착한 뒤 중단 저장할 수 있습니다." :
                    session.PrologueMode && !session.TempleSuspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.TempleSuspension.Status) ? "현재 사찰 저장을 사용할 수 없습니다." : session.TempleSuspension.Status) :
                    session.ChapterMode && !session.ChapterSuspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.ChapterSuspension.Status) ? "현재 학교 저장을 사용할 수 없습니다." : session.ChapterSuspension.Status) :
                    session.ChapterMode && !string.IsNullOrEmpty(session.Chapter.SuspendBlockReason) ? session.Chapter.SuspendBlockReason :
                    session.CorridorMode && !session.Suspension.Writable ?
                    (string.IsNullOrWhiteSpace(session.Suspension.Status) ? "현재 회랑 저장을 사용할 수 없습니다." : session.Suspension.Status) :
                    session.CorridorMode && !string.IsNullOrEmpty(session.Corridor.SuspendBlockReason) ? session.Corridor.SuspendBlockReason : null };
        }
        internal sealed class Actions
        {
            public Action Resume, Journal, Settings, Restart, Title, SuspendCorridor, SuspendChapter, SuspendTemple;
        }
        public void Build(ShellLayout layout, State state, Actions actions, bool reducedMotion)
        {
            var face=Resources.Load<Font>("Fonts/PauseMyeongjo");
            var heading=layout.Text("일시 정지", 175, 149, 620, 90, layout.LargeText ? 52 : 47);
            if(face)heading.style.unityFont=face;
            heading.style.letterSpacing=1.5f;
            heading.style.color=layout.HighContrast?Color.white:new Color(.88f,.87f,.83f);

            float y=278;
            PauseButton(layout,"resume","계속하기",y,actions.Resume,face,reducedMotion);y+=65;
            if (!state.TempleMode) { PauseButton(layout,"journal","조사 기록",y,actions.Journal,face,reducedMotion);y+=65; }
            PauseButton(layout,"settings","환경 설정 · 접근성",y,actions.Settings,face,reducedMotion);y+=65;
            if(state.CorridorMode)
            {
                var suspend=PauseButton(layout,"suspend-run","탐색 저장 후 나가기",y,actions.SuspendCorridor,face,reducedMotion);
                suspend.SetEnabled(state.CanSuspendCorridor);
                if(!state.CanSuspendCorridor)suspend.style.opacity=.38f;
                y+=65;
            }
            if(state.ChapterMode)
            {
                var suspend=PauseButton(layout,"suspend-chapter","학교 저장 후 나가기",y,actions.SuspendChapter,face,reducedMotion);
                suspend.SetEnabled(state.CanSuspendChapter);
                if(!state.CanSuspendChapter)suspend.style.opacity=.38f;
                y+=65;
            }
            if(state.TempleMode)
            {
                var suspend = PauseButton(layout,"suspend-temple","사찰 저장 후 나가기",y,actions.SuspendTemple,face,reducedMotion);
                suspend.SetEnabled(state.CanSuspendTemple); if(!state.CanSuspendTemple)suspend.style.opacity=.38f;
                y+=65;
            }
            PauseButton(layout,"restart",state.CorridorMode?"새 회랑에서 다시 시작":"처음부터 다시 시작",y,actions.Restart,face,reducedMotion);y+=65;
            PauseButton(layout,"title","시작 화면으로 (진행 초기화)",y,actions.Title,face,reducedMotion);
            if(state.ShowSuspensionMessage&&!string.IsNullOrWhiteSpace(state.SuspensionMessage))
            {
                var notice=layout.Small(state.SuspensionMessage, 176, 759, 700, 58);
                notice.style.color=layout.HighContrast?Color.white:new Color(.72f,.72f,.69f);
            }
        }

        static Button PauseButton(ShellLayout layout,string id,string text,float y,Action action,Font face,bool reducedMotion)
            => ShellMenuButton.Create(layout,id,text,174,y,530,56,action,face,()=>reducedMotion);
    }
}
