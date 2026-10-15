using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Completed-run presentation receives copied values and explicit recovery/navigation commands.</summary>
    internal sealed class ShellResultScreen
    {
        internal struct State
        {
            public bool ChapterMode, CorridorMode, Escaped, RetryRecord, RetryChapterRecord;
            public int CorridorVersion, RecordsRecovered, TotalRecords;
            public float ElapsedPlayTime;
            public string DefeatHint, ChapterObjective, RecordSaveMessage, ChapterRecordSaveMessage;
            public ChapterRunReport ChapterResult;
            public static State Read(GameSession session) => new State {
                ChapterMode = session.ChapterMode, CorridorMode = session.CorridorMode, Escaped = session.Escaped,
                CorridorVersion = session.CorridorMode ? session.Corridor.Layout.Version : 0,
                RecordsRecovered = session.RecordsRecovered, TotalRecords = session.TotalRecords, ElapsedPlayTime = session.ElapsedPlayTime,
                DefeatHint = session.DefeatHint, ChapterObjective = session.ChapterMode ? session.Chapter.Objective : string.Empty,
                ChapterResult = session.ChapterMode ? session.ChapterResult : null,
                RecordSaveMessage = session.RecordSaveMessage, ChapterRecordSaveMessage = session.ChapterRecordSaveMessage,
                RetryRecord = session.CorridorMode && !session.RecordSaved && session.Records.Writable && !string.IsNullOrWhiteSpace(session.RecordSaveMessage),
                RetryChapterRecord = session.ChapterMode && !session.ChapterRecordSaved && session.ChapterRecords.Writable && !string.IsNullOrEmpty(session.ChapterRecordSaveMessage) };
        }
        internal sealed class Actions
        {
            public Action Restart, Title, History, RetryRecord, RetryChapterRecord;
        }
        public void Build(ShellLayout layout, State state, Actions actions)
        {
            if(state.ChapterMode) { BuildSchoolResult(layout, state, actions); return; }
            layout.Small(state.CorridorMode?(state.Escaped?"회랑 탐색 종료  /  탈출 성공":"회랑 탐색 중단  /  기억 미완료"):state.Escaped ? "조사 종료  /  하교 확인" : "조사 중단  /  기록 미완료", 153, 247, 1170);
            string message = state.Escaped ? "지워졌던 이름을 다시 적었다.\n복도 너머에서 마지막 문이 닫힌다." : "이곳에는 아직 돌아오지 못한 기록이 있습니다.\n다시 들어가 마지막 이름을 찾아주세요.";
            if (state.CorridorMode) message = state.Escaped ? (state.CorridorVersion>=3 ? "기억을 제단에 돌려놓았다.\n문 너머로 폐교의 기억이 이어진다." : "다섯 기억이 모이자 봉인이 풀렸다.\n처음 들어온 문 너머로 돌아왔다.") : "돌아갈 길을 잃었다.\n들었던 발소리를 기억하고 다음 회랑에 들어가세요.";
            layout.Text(message, 153, 294, 1210, 103, layout.LargeText ? 30 : 27);
            layout.Panel(895, 437, 540, 391, layout.CardSurface);
            layout.Small(state.CorridorMode?"이번 회랑 탐색":state.Escaped ? "마지막 출석 확인" : "다음 탐색을 위한 기록", 920, 460, 490);
            layout.Text($"복원한 기록  {state.RecordsRecovered} / {state.TotalRecords}\n탐색 시간  {Mathf.FloorToInt(state.ElapsedPlayTime / 60)}분 {Mathf.FloorToInt(state.ElapsedPlayTime % 60):00}초", 920, 505, 490, 85, layout.ReadingSize);
            string hint = state.Escaped ? "모든 이름이 제자리로 돌아왔습니다." : !string.IsNullOrWhiteSpace(state.DefeatHint) ? state.DefeatHint : "문을 닫아 시선을 끊고 걸으며 숨을 회복하세요.";
            layout.Text(hint, 920, 603, 490, 211, layout.LargeText ? 23 : 20).style.color = state.Escaped ? layout.Gold : layout.Muted;
            layout.Button("restart", state.Escaped ? "다시 탐색하기" : "다시 시작", 447, () => actions.Restart(), primary: true);
            layout.Button("title", "시작 화면으로", 515, () => actions.Title());
            layout.Button("quit", "종료", 583, () => Application.Quit());
            bool retryRecord = state.RetryRecord;
            if (retryRecord) layout.Button("retry-record-save", "탐색 기록 저장 다시 시도", 651, () => { actions.RetryRecord(); });
            if (!state.Escaped) layout.Small(state.CorridorMode ? "다시 시작하면 현재 회랑은 새로 생성됩니다." : "다시 시작하면 이번 탐색 기록은 초기화됩니다.",
                153, retryRecord ? 715 : 680, 675, retryRecord ? 55 : 70);
            if (!string.IsNullOrWhiteSpace(state.RecordSaveMessage))
                layout.Small(state.RecordSaveMessage, 153, retryRecord ? 770 : 752, 675, retryRecord ? 60 : 80).name = "record-save-status";
        }
        void BuildSchoolResult(ShellLayout layout, State state, Actions actions)
        {
            var result=state.ChapterResult;
            layout.Small(state.Escaped?"학교 조사 종료  /  하교 확인":"학교 조사 중단  /  다섯 기억의 기록",153,247,675);
            layout.Text(state.Escaped?"다섯 이름이 돌아왔다.\n학교 밖의 공기를 다시 마신다.":"기억은 아직 학교 안에 남아 있다.\n들었던 소리를 기억하고 다시 들어가세요.",153,294,650,109,layout.LargeText?30:27);
            layout.Panel(895,245,540,583,layout.CardSurface);
            layout.Small("이번 학교 탐색",920,269,490);
            layout.Text($"기억 {result.recovered} / 5\n탐색 시간  {ShellLayout.RunTime(result.seconds)}\n남은 콩알탄 {result.firecrackersRemaining}발 · 호흡 {Mathf.RoundToInt(result.staminaRemaining*100)}%\n이동 거리 {result.distance:0.0} m\n콩알탄 {result.throws}회 · 은신 {result.hides}회 · 문 닫기 {result.closedDoors}회",920,320,490,176,layout.LargeText?23:21).name="school-result-details";
            layout.Text(state.Escaped?"결과: 학교 탈출 성공":"마지막 기척: "+(string.IsNullOrEmpty(result.defeatSource)?"기록되지 않음":result.defeatSource),920,498,490,54,layout.ReadingSize).name="school-result-source";
            layout.Small(result.actionsComplete?"행동 기록은 이번 탐색 전체를 포함합니다.\n일시정지와 중단 시간은 탐색 시간에서 제외됩니다.":"이전 저장에는 행동 기록이 없어 이어하기 이후 행동만 표시합니다. 탐색 시간과 기억은 전체 진행입니다.",920,559,490,77).name="school-result-scope";
            string advice=state.Escaped?"모든 기억을 회수하고 1층 출입문으로 돌아왔습니다. 다른 탐색 기록은 최근 기록에서 확인할 수 있습니다.":
                state.ChapterObjective+"\n"+state.DefeatHint;
            layout.Text(advice,920,646,490,172,layout.LargeText?23:20).style.color=state.Escaped?layout.Gold:layout.Muted;
            layout.Button("restart",state.Escaped?"학교 다시 탐색하기":"학교 처음부터 다시 시작",447,()=>actions.Restart(),primary:true);
            layout.Button("title","시작 화면으로",515,()=>actions.Title()); layout.Button("quit","종료",583,()=>Application.Quit());
            layout.Button("school-records","최근 학교 탐색 기록",651,actions.History);
            bool retry=state.RetryChapterRecord;
            if(retry) layout.Button("retry-school-record-save","학교 결과 저장 다시 시도",710,()=>{actions.RetryChapterRecord();});
            string message=state.ChapterRecordSaveMessage;
            if(string.IsNullOrEmpty(message)) message=state.Escaped?"다음 탐색에서는 새로운 기록에 도전할 수 있습니다.":"다시 시작은 첫 기억부터 진행합니다. 완료된 탐색 기록은 남습니다.";
            layout.Small(message,153,retry?770:723,675,retry?62:98).name="school-record-save-status";
        }
    }
}
