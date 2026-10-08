using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // The authored school has its own schema and slot; corridor saves stay separate.
    [Serializable]
    public sealed partial class ChapterCheckpoint
    {
        public int version = 1, simulationVersion = 1, recovered;
        public string token, scene;
        public int lightingVersion;
        public LightExplorationCheckpoint lighting;
        public float seconds;
        public PlayerMotor.Progress player;
        public ChapterRunProgress runProgress;
        // Identity is explicit, so inactive actors cannot swap when enumeration changes.
        public StalkerBrain.Progress cyclopse, portraitActor, nurseryActor;
        public WeepingAngelEncounter.ChapterProgress mannequin;
        public LanternMaskEncounter.ChapterProgress mask;
        public bool portraitCompleted, portraitWitnessed, nurseryReleased;
        public Door[] doors;
        public Supply[] supplies;
        [Serializable] public sealed class Door
        { public string id; public bool open, hasSecondary; public Vector3 leaf, secondary; }
        [Serializable] public sealed class Supply
        { public string id; public bool available; }
        public static bool Point(Vector3 p) => CorridorCheckpoint.Number(p.x,-100,100) &&
            CorridorCheckpoint.Number(p.z,-100,100) && CorridorCheckpoint.Number(p.y,-6,8);
        public static bool Rotation(Quaternion q) => CorridorCheckpoint.Number(q.x,-1,1) &&
            CorridorCheckpoint.Number(q.y,-1,1) && CorridorCheckpoint.Number(q.z,-1,1) &&
            CorridorCheckpoint.Number(q.w,-1,1) && Mathf.Abs(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w-1)<.01f;
        static bool Identity(string id) => !string.IsNullOrEmpty(id) && id.Length<=40;
        public void Validate()
        {
            if(version!=1 || simulationVersion<1 || simulationVersion>SchoolCampusLayout.Version || !Guid.TryParseExact(token,"N",out _) ||
                scene!="Assets/Annex/SchoolAnnex.unity" || !CorridorCheckpoint.Number(seconds,0,1000000000) ||
                recovered<0 || recovered>5 || player==null || cyclopse==null || portraitActor==null || nurseryActor==null ||
                mannequin==null || mask==null || doors==null || doors.Length>128 || supplies==null || supplies.Length>128)
                throw new ArgumentException("Invalid school checkpoint schema");
            // Legacy marker 0 means this run predates batteries/candles. New captures always write 1.
            if(lightingVersion<0 || lightingVersion>1 || lightingVersion==1 && lighting==null ||
                lightingVersion==0 && lighting!=null && !lighting.LegacyEmpty)
                throw new ArgumentException("Invalid lighting checkpoint version");
            if(lightingVersion==1) lighting.Validate();
            player.ValidateChapter(); cyclopse.ValidateChapter(); portraitActor.ValidateChapter(); nurseryActor.ValidateChapter();
            runProgress?.Validate();
            mannequin.Validate(); mask.Validate();
            if(cyclopse.active!=(recovered>=1) || mannequin.active!=(recovered>=2) || mask.active!=(recovered>=3) ||
                portraitCompleted && recovered<3 || portraitWitnessed && !portraitCompleted ||
                recovered>=4 && (!portraitCompleted || !portraitWitnessed) ||
                portraitActor.active!=portraitCompleted || nurseryReleased && recovered<4 ||
                recovered>=5 && !nurseryReleased || nurseryActor.active!=nurseryReleased)
                throw new ArgumentException("School progression and actors disagree");
            var ids=new HashSet<string>();
            foreach(var door in doors)
                if(door==null || !Identity(door.id) || !ids.Add(door.id) || !CorridorCheckpoint.Vector(door.leaf) ||
                    !CorridorCheckpoint.Vector(door.secondary)) throw new ArgumentException("Invalid school doors");
            ids.Clear();
            foreach(var supply in supplies)
                if(supply==null || !Identity(supply.id) || !ids.Add(supply.id)) throw new ArgumentException("Invalid school supplies");
        }
    }
}
