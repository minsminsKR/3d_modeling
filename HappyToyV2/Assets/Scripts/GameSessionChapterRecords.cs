using System;
using System.Globalization;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class GameSession
    {
        public ChapterRecords ChapterRecords { get; private set; }
        public bool ChapterRecordSaved { get; private set; }
        public string ChapterRecordSaveMessage { get; private set; }="";
        ChapterRunProgress chapterProgress;
        ChapterRunReport chapterResult;
        Vector3 previousTravelPosition;
        int previousMovementUpdate;
        bool previousTravelHidden;
        public ChapterRunReport ChapterResult => chapterResult?.Copy();
        void BeginChapterMetrics()
        {
            chapterProgress=ChapterRunProgress.New(); ResetTravelObserver();
        }
        void ResetTravelObserver()
        {
            if(!player) return;
            previousTravelPosition=player.transform.position; previousMovementUpdate=player.MovementUpdates; previousTravelHidden=player.Hidden;
        }
        public ChapterRunProgress CaptureChapterMetrics() => chapterProgress?.Copy();
        void RestoreChapterMetrics(ChapterRunProgress data)
        {
            data?.Validate(); chapterProgress=data?.Copy()??ChapterRunProgress.New(false); ResetTravelObserver();
        }
        public void NoteChapterAction(ChapterAction action)
        {
            if(!ChapterMode || !InputAllowed || chapterProgress==null) return;
            switch(action)
            {
                case ChapterAction.FirecrackerThrown: chapterProgress.throws=Math.Min(1000000000,chapterProgress.throws+1); break;
                case ChapterAction.HidingEntered: chapterProgress.hides=Math.Min(1000000000,chapterProgress.hides+1); break;
                case ChapterAction.DoorClosed: chapterProgress.closedDoors=Math.Min(1000000000,chapterProgress.closedDoors+1); break;
            }
        }
        void LateUpdate() { ObserveChapterTravel(); }
        void ObserveChapterTravel()
        {
            if(!player || chapterProgress==null) return;
            float distance=Vector3.Distance(player.transform.position,previousTravelPosition);
            // Measure real controller movement, never keyboard intent. Hiding,
            // restore and scene/test teleports cannot manufacture travel distance.
            if(InputAllowed && !player.Hidden && !previousTravelHidden && player.MovementUpdates!=previousMovementUpdate &&
                distance<=Mathf.Max(.2f,player.runSpeed*Time.unscaledDeltaTime*2+.1f))
                chapterProgress.distance=Mathf.Min(1000000000,chapterProgress.distance+distance);
            ResetTravelObserver();
        }
        void PrepareChapterResult(bool escaped)
        {
            ObserveChapterTravel(); var metrics=chapterProgress??ChapterRunProgress.New(false);
            string source=DefeatSource??""; if(source.Length>80) source=source.Substring(0,80);
            chapterResult=new ChapterRunReport { token=metrics.token,endedUtc=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture),
                escaped=escaped,recovered=Chapter.Recovered,seconds=ElapsedPlayTime,defeatSource=source,
                firecrackersRemaining=player&&player.Firecrackers?player.Firecrackers.Count:0,staminaRemaining=player?player.Stamina:0,
                throws=metrics.throws,hides=metrics.hides,closedDoors=metrics.closedDoors,distance=metrics.distance,actionsComplete=metrics.complete };
        }
        public bool RetryChapterRecordSave()
        {
            if(!Finished || !ChapterMode || !ChapterRecords.PersistenceEnabled || ChapterRecordSaved || chapterResult==null) return false;
            ChapterRecordSaved=ChapterRecords.Record(chapterResult);
            ChapterRecordSaveMessage=ChapterRecords.Status; return ChapterRecordSaved;
        }
    }
}
