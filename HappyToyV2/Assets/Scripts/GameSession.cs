using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }
        public PlayerMotor player;
        public int requiredNames = 4;
        public bool requireAnnexRecords;
        static readonly string[] annexIds = { "music-roster", "archive-record", "nursery-tag" };
        static readonly string[] annexObjectives = {
            "별관 북쪽 음악실에서 합창 명단을 조사하세요.",
            "별관 남쪽 자료실에서 폐쇄 기록을 조사하세요.",
            "별관 서쪽 내려가는 계단으로 지하에 가서 물에 젖은 이름표를 조사하세요."
        };
        public bool AnnexRecordsComplete => !requireAnnexRecords || System.Array.TrueForAll(annexIds, inspected.Contains);
        public event System.Action<string> RecordInspected;
        public bool Finished { get; private set; }
        public bool Escaped { get; private set; }
        public int StoryStep => names.Count;
        public bool InputAllowed => Shell && Shell.Screen==GameShell.Page.Playing && !Finished;
        public GameShell Shell { get; private set; }
        public string Objective
        {
            get
            {
                if(StoryStep==3 && requireAnnexRecords)
                    for(int i=0;i<annexIds.Length;i++)if(!inspected.Contains(annexIds[i]))return annexObjectives[i];
                if(StoryStep==2 && requireAnnexRecords)return "별관 북쪽 계단으로 2층 붉은 액자실에 올라가 보건 기록을 찾으세요.";
                return objectives[Mathf.Min(StoryStep,4)];
            }
        }
        public string CurrentObjectiveId
        {
            get
            {
                if(StoryStep>=4)return "exit";
                if(StoryStep==3&&requireAnnexRecords)
                    foreach(var id in annexIds)if(!inspected.Contains(id))return id;
                return sequence[StoryStep];
            }
        }
        public string Notice => notice;
        public string JournalEntry(int index)=>index>=0&&index<StoryStep?clues[index]:null;
        public string ExplorationEntry(int index)=>index>=0&&index<exploration.Count?exploration[index]:null;
        public int ExplorationCount=>exploration.Count;
        readonly List<string> exploration=new List<string>();
        readonly HashSet<string> inspected=new HashSet<string>();
        public void Inspect(string id,string text)
        {
            if(!InputAllowed||string.IsNullOrWhiteSpace(text))return;
            if(inspected.Add(id)){exploration.Add(text);RecordInspected?.Invoke(id);}
            Notify(text);
        }
        public event System.Action<int> StoryChanged;
        readonly string[] sequence = { "register", "ribbon", "record", "restore" };
        readonly string[] objectives = {
            "교실 출석부에서 지워진 이름을 확인하세요.",
            "화장실에 남겨진 리본을 찾으세요.",
            "보건실 기록에서 마지막 하교 시각을 확인하세요.",
            "교실 준비함에 리본과 기록을 돌려놓으세요.",
            "현관의 출석함에 마지막 이름을 돌려놓으세요."
        };
        readonly string[] clues = {
            "출석부 — 윤서의 이름만 칼로 긁어 지웠다. 비고란에는 ‘화장실 확인’이라고 적혀 있다.",
            "젖은 리본 — 매듭에 보건실 표찰이 걸려 있다. 종이 울린 뒤, 복도 끝에 검은 형체가 서 있다.",
            "보건 기록 — 18:10, 보호자 인계 없음. 하교 완료 도장은 그보다 한 시간 먼저 찍혔다.",
            "준비함 — 리본과 기록을 돌려놓자 빈 자리에서 의자가 한 번 끌렸다. 이제 이름을 지우지 말아야 한다."
        };
        readonly HashSet<string> names = new HashSet<string>();
        string notice = "마지막 출석 — 지워진 아이의 하교 기록을 복원하세요.";
        void Awake() { Current = this; Application.targetFrameRate=120; Shell=gameObject.AddComponent<GameShell>();gameObject.AddComponent<RoomAmbience>(); }
        public bool Collect(string id)
        {
            if(!InputAllowed)return false;
            if (names.Contains(id)) return false;
            if(id=="restore" && StoryStep==3 && !AnnexRecordsComplete)
            {notice="이름을 복원할 증거가 부족합니다. "+Objective;return false;}
            if (StoryStep >= sequence.Length || id != sequence[StoryStep])
            { notice = Objective; return false; }
            names.Add(id);notice=clues[StoryStep-1];StoryChanged?.Invoke(StoryStep);return true;
        }
        public void TryEscape()
        {
            if(!InputAllowed)return;
            if (names.Count < requiredNames) { notice = objectives[StoryStep]; return; }
            Finish(true);
        }
        public void Notify(string text){if(!Finished)notice=text;}
        public void Finish(bool escaped)
        {
            if (Finished) return;
            Finished = true; Escaped = escaped; Time.timeScale = 0;
            Shell.ShowResult();
        }
    }
}
