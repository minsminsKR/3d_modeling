using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Journal presentation and paging; discovered entry providers are read-only.</summary>
    internal sealed class ShellJournalScreen
    {
        int journalPage;
        public void ResetPage() => journalPage = 0;
        internal struct State
        {
            public bool CorridorMode;
            public int Required, Total, RecordsRecovered, TotalRecords, ExplorationCount;
            public Func<int, string> JournalEntry, ExplorationEntry;
            public static State Read(GameSession session) => new State {
                CorridorMode = session.CorridorMode,
                Required = session.CorridorMode ? 0 : session.JournalCount,
                Total = session.CorridorMode ? Mathf.Max(session.TotalRecords, session.ExplorationCount) : session.JournalCount + session.ExplorationCount,
                RecordsRecovered = session.RecordsRecovered, TotalRecords = session.TotalRecords,
                ExplorationCount = session.ExplorationCount, JournalEntry = session.JournalEntry, ExplorationEntry = session.ExplorationEntry };
        }
        public void Build(ShellLayout layout, State state, Action back, Action rebuild)
        {
            int required = state.Required;
            int total = state.Total;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / 6f));
            journalPage = Mathf.Clamp(journalPage, 0, pages - 1);
            layout.Small(state.CorridorMode?$"회수한 기억 {state.RecordsRecovered} / {state.TotalRecords}     ·     발견한 순서대로 보관합니다":$"복원 {state.RecordsRecovered} / {state.TotalRecords}     주변 기록 {state.ExplorationCount}개     ·     발견한 내용만 보관합니다", 153, 218, 1290).name = "journal-progress";
            for (int slot = 0; slot < 6; slot++)
            {
                int index = journalPage * 6 + slot;
                if(state.CorridorMode && index>=total) break;
                float x = 150 + slot % 2 * 665, y = 264 + slot / 2 * 158;
                string entry = index < required ? state.JournalEntry(index) : state.ExplorationEntry(index - required);
                string placeholder = state.CorridorMode?"아직 회수하지 못했습니다.\n갈림길에서 기억이 울리는 소리를 들으세요.":index < required ? "아직 발견하지 못했습니다.\n학교 안에서 단서를 찾아 조사하세요." : "아직 비어 있습니다.\n게시물을 조사하면 이곳에 남습니다.";
                var card = layout.Panel(x, y, 635, 148, layout.CardSurface);
                layout.Outline(card, entry == null ? layout.Edge : new Color(.39f, .43f, .30f));
                layout.Panel(x, y, 3, 148, entry == null ? layout.Edge : layout.Gold);
                layout.Small(state.CorridorMode?$"회랑의 기억  0{index+1}":index < required ? $"필수 기록  0{index + 1}" : $"주변 기록  {index - required+1:00}", x + 20, y + 11, 460, 25).style.color = entry == null ? layout.Muted : layout.Gold;
                var label = layout.Text(entry ?? placeholder, x + 20, y + 43, 596, 103, layout.LargeText ? 22 : 20);
                label.name = state.CorridorMode?"corridor-memory-"+index:index < required ? "record-" + index : "inspection-" + (index - required);
                if (entry == null) label.style.color = layout.Muted;
            }
            layout.Button("back", "돌아가기  [J / Esc]", 747, back, primary: true);
            if (pages > 1)
            {
                var previous = layout.Button("journal-previous", "이전", 747, () => { journalPage--; rebuild(); }, 895, 165);
                previous.SetEnabled(journalPage > 0);
                var count = layout.Text($"{journalPage + 1} / {pages}", 1080, 759, 165, 40, 22);
                count.style.unityTextAlign = TextAnchor.UpperCenter;
                var next = layout.Button("journal-next", "다음", 747, () => { journalPage++; rebuild(); }, 1285, 165);
                next.SetEnabled(journalPage < pages - 1);
            }
        }
    }
}
