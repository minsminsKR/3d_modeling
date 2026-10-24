using System;
using UnityEngine;

namespace HappyToy.V2
{
    [Serializable] public sealed class SchoolClockProgress
    {
        public int hours;
        public void Validate()=>SchoolProgressClock.Validate(hours);
    }
    [Serializable] public sealed class SchoolLoopProgress
    {
        public int traversals;
        // Optional: old v3 saves counted both seam directions and cannot imply successful hours.
        public SchoolClockProgress clock;
        // Optional for older observation-school saves with no walking civilian.
        public SchoolWalkerProgress walker;
        public void Validate()
        {
            if(traversals<0||traversals>1000000)throw new ArgumentException("Invalid school loop progress");
            clock?.Validate();
            walker?.Validate(traversals);
        }
    }
    public sealed partial class ChapterCheckpoint
    {
        public SchoolLoopProgress loop;
        void ValidateSchoolLoop()
        {
            if(version!=1||simulationVersion!=SchoolLoopPlan.Version||!Guid.TryParseExact(token,"N",out _)||
                scene!="Assets/Annex/SchoolAnnex.unity"||!CorridorCheckpoint.Number(seconds,0,1000000000)||recovered!=0||
                player==null||loop==null||lightingVersion!=1||lighting==null||lighting.version!=2||doors==null||doors.Length!=0||supplies==null||supplies.Length!=0)
                throw new ArgumentException("Invalid observation school checkpoint");
            loop.Validate();player.ValidateChapter();lighting.Validate();traps?.Validate(Point);ritualItems?.Validate();runProgress?.Validate();
            var local=player.position-SchoolLoopWorld.Origin;
            if(!SchoolLoopPlan.Walkable(local.x,local.z)||Mathf.Abs(local.y)>.20f)
                throw new ArgumentException("School loop checkpoint is outside the actual route");
        }
    }
    public sealed partial class MemoryChapter
    {
        ChapterCheckpoint CaptureLoopCheckpoint()
        {
            var data=new ChapterCheckpoint{simulationVersion=SchoolLoopPlan.Version,token=Guid.NewGuid().ToString("N"),scene=gameObject.scene.path,
                seconds=session.ElapsedPlayTime,player=session.player.CaptureProgress(),loop=new SchoolLoopProgress{traversals=Loop.Traversals,clock=new SchoolClockProgress{hours=Loop.ClockHours},walker=SchoolWalkerProgress.Capture(Loop.Walker.CaptureState())},
                lightingVersion=1,lighting=Lighting.Capture(),traps=TrapWorld.Capture(session.player),ritualItems=RitualWorld.Capture(session.player),
                doors=Array.Empty<ChapterCheckpoint.Door>(),supplies=Array.Empty<ChapterCheckpoint.Supply>()};
            data.Validate();return data;
        }
        void ValidateLoopCheckpoint(ChapterCheckpoint data)
        {
            data.Validate();
            if(!Ready||!Loop||session.Finished||session.InputAllowed||LayoutVersion!=data.simulationVersion)
                throw new InvalidOperationException("Prepare a fresh paused observation school");
            Lighting.ValidateRestore(data.lighting);TrapWorld.ValidateRestore(data.traps,session.player);RitualWorld.ValidateRestore(data.ritualItems,session.player);
            if(data.loop.walker!=null)Loop.Walker.ValidateRestoreState(data.loop.walker.ToState());
            if(!session.player.CanRestoreChapterProgress(data.player))throw new ArgumentException("Saved school loop player is obstructed");
        }
        void RestoreLoopCheckpoint(ChapterCheckpoint data)
        {
            ValidateLoopCheckpoint(data);session.player.RestoreChapterProgress(data.player);Loop.RestoreTraversal(data.loop.traversals);Loop.RestoreClock(data.loop.clock?.hours??0);
            if(data.loop.walker!=null)Loop.Walker.RestoreState(data.loop.walker.ToState());
            Lighting.Restore(data.lighting);TrapWorld.Restore(data.traps,session.player);RitualWorld.Restore(data.ritualItems,session.player);
            Physics.SyncTransforms();
        }
    }
}
