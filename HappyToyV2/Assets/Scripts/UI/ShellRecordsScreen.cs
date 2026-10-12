using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>History receives a copied profile snapshot and owns only its current page.</summary>
    internal sealed class ShellRecordsScreen
    {
        int schoolRecordPage;
        public void ResetPage() => schoolRecordPage = 0;
        public void Build(ShellLayout layout, ChapterRecords.Summary summary, string status, Action back, Action rebuild)
        {
            string best=summary.escapes>0?"최단 탈출 "+ShellLayout.RunTime(summary.bestEscapeSeconds):"아직 학교 탈출 기록이 없습니다.";
            layout.Small($"학교 탐색 {summary.attempts}회 · 탈출 {summary.escapes}회 · 최고 기억 {summary.bestRecovered}/5\n{best}",153,247,1290,62).name="school-history-summary";
            layout.Small(string.IsNullOrEmpty(status)?"최근 저장된 여덟 번의 학교 탐색을 보관합니다. 회랑 탐색 기록은 별도입니다.":status.Replace("\n"," "),153,313,1290,30);
            int pages=Mathf.Max(1,Mathf.CeilToInt(summary.recent.Length/4f)); schoolRecordPage=Mathf.Clamp(schoolRecordPage,0,pages-1);
            if(summary.recent.Length==0) layout.Text("아직 완료된 학교 탐색이 없습니다.\n학교에 들어가면 탈출 또는 포획 결과가 이곳에 남습니다.",174,384,1220,110,layout.ReadingSize).name="school-history-empty";
            for(int slot=0;slot<4;slot++)
            {
                int index=schoolRecordPage*4+slot; if(index>=summary.recent.Length) break;
                var run=summary.recent[index]; float y=344+slot*108; var card=layout.Panel(150,y,1300,99,layout.CardSurface); layout.Outline(card,layout.Edge);
                string source=string.IsNullOrEmpty(run.defeatSource)?"기록 미완료":run.defeatSource;
                if(source.Length>32) source=source.Substring(0,32)+"…";
                layout.Text($"{(run.escaped?"탈출 성공":"포획 · "+source)}    기억 {run.recovered}/5    {ShellLayout.RunTime(run.seconds)}",174,y+12,1240,40,layout.LargeText?24:22).name="school-history-run-"+index;
                layout.Small($"폭죽 {run.throws}회 · 은신 {run.hides}회 · 문 닫기 {run.closedDoors}회 · 이동 {run.distance:0.0} m · 남은 폭죽 {run.firecrackersRemaining}개"+
                    (run.actionsComplete?"":"  (이어하기 이후 행동)"),174,y+54,1240,36);
            }
            layout.Button("back","돌아가기  [Esc]",775,back,primary:true);
            if(pages>1)
            {
                layout.Button("school-records-previous","이전",775,()=>{schoolRecordPage--; rebuild();},895,165).SetEnabled(schoolRecordPage>0);
                var count=layout.Text($"{schoolRecordPage+1} / {pages}",1080,787,165,40,22); count.style.unityTextAlign=TextAnchor.UpperCenter;
                layout.Button("school-records-next","다음",775,()=>{schoolRecordPage++; rebuild();},1285,165).SetEnabled(schoolRecordPage<pages-1);
            }
        }

    }
}
