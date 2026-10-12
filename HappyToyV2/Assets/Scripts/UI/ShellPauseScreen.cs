using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Pause actions and attendance card share a progress snapshot, never view internals.</summary>
    internal sealed class ShellPauseScreen
    {
        Label controlInstructions;
        public void UpdateControls(string text) { if (controlInstructions != null) controlInstructions.text = text; }
        internal struct State
        {
            public string Objective, SuspensionMessage;
            public bool CorridorMode, ChapterMode, CanSuspendCorridor, CanSuspendChapter, ShowSuspensionMessage;
            public int CorridorVersion, RecordsRecovered, StoryStep;
            public static State Read(GameSession session, GameShell shell) => new State {
                Objective = session.Objective, CorridorMode = session.CorridorMode, ChapterMode = session.ChapterMode,
                CorridorVersion = session.CorridorMode ? session.Corridor.Layout.Version : 0,
                RecordsRecovered = session.RecordsRecovered, StoryStep = session.StoryStep,
                CanSuspendCorridor = session.CorridorMode && session.Suspension.Writable && string.IsNullOrEmpty(session.Corridor.SuspendBlockReason),
                CanSuspendChapter = session.ChapterMode && session.ChapterSuspension.Writable && string.IsNullOrEmpty(session.Chapter.SuspendBlockReason),
                ShowSuspensionMessage = string.IsNullOrEmpty(shell.ReloadError),
                SuspensionMessage = session.ChapterMode && !session.ChapterSuspension.Writable ? session.ChapterSuspension.Status :
                    session.ChapterMode && !string.IsNullOrEmpty(session.Chapter.SuspendBlockReason) ? session.Chapter.SuspendBlockReason :
                    !session.Suspension.Writable && !string.IsNullOrEmpty(session.Suspension.Status) ? session.Suspension.Status :
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
            layout.Small("현재 목표", 153, 249, 640);
            layout.Text(state.Objective, 153, 286, 650, 80, layout.ReadingSize);
            layout.Button("resume", "계속하기  [Esc]", 387, actions.Resume, primary: true);
            layout.Button("journal", "조사 기록  [J]", 455, actions.Journal);
            layout.Button("settings", "환경 설정 · 접근성", 523, actions.Settings);
            layout.Button("restart", state.CorridorMode?"새 회랑에서 다시 시작":"처음부터 다시 시작", 591, () => actions.Restart());
            layout.Button("title", "시작 화면으로 (진행 초기화)", 659, () => actions.Title());
            if(state.CorridorMode)
            {
                var suspend=layout.Button("suspend-run", "탐색 저장 후 시작 화면으로", 727, actions.SuspendCorridor);
                suspend.SetEnabled(state.CanSuspendCorridor);
            }
            if(state.ChapterMode)
            {
                var suspend=layout.Button("suspend-chapter", "학교 저장 후 시작 화면으로", 727, actions.SuspendChapter);
                suspend.SetEnabled(state.CanSuspendChapter);
            }
            BuildAttendanceCard(layout, state, controlText, 895, 245);
            if (state.ShowSuspensionMessage) layout.Small(state.SuspensionMessage, 153, 787, 1270, 40);
        }
        void BuildAttendanceCard(ShellLayout layout, State state, string controlText, float x, float y)
        {
            var card = layout.Panel(x, y, 540, 535, layout.CardSurface);
            layout.Outline(card, layout.Edge);
            layout.Panel(x + 26, y + 27, 3, 70, layout.Gold);
            layout.Small(state.CorridorMode?(state.CorridorVersion>=3 ? "CORRIDOR  /  붉은 교실에 기억을 돌려놓으세요" : "CORRIDOR  /  돌아갈 문을 기억하세요"):"SCHOOL ARCHIVE  /  출석 확인", x + 46, y + 28, 450);
            layout.Text(state.CorridorMode || state.ChapterMode ? $"회수한 기억  {state.RecordsRecovered} / 5" : $"주요 단계  {state.StoryStep} / 4", x + 46, y + 61, 460, 48, 30);
            bool corridorCard = state.CorridorMode || state.ChapterMode;
            for (int i = 0; i < (corridorCard ? 5 : 4); i++)
            {
                float lineY = y + 136 + i * (corridorCard ? 35 : 43);
                layout.Small($"0{i + 1}", x + 30, lineY, 46);
                layout.Panel(x + 88, lineY + 9, 255 - i * 24, 9, new Color(.16f, .22f, .18f));
                bool complete = (corridorCard ? state.RecordsRecovered : state.StoryStep) > i;
                layout.Small(complete ? "회수" : "미확인", x + 419, lineY, 95).style.color = complete ? layout.Gold : layout.Muted;
                layout.Panel(x + 27, lineY + 33, 486, 1, layout.Edge);
            }
            layout.Panel(x + 26, y + 311, 487, 1, layout.Edge);
            controlInstructions = layout.Text(controlText, x + 30, y + 323, 485, 200, layout.LargeText ? 23 : 21);
        }
    }
}
