using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Simulation snapshot, separate from aggregate finished-run records.
    // File storage and the user-facing suspend flow are layered above this codec.
    [Serializable]
    public sealed class CorridorCheckpoint
    {
        public int version = 1, simulationVersion = 1, seed;
        public string token;
        public int lightingVersion;
        public LightExplorationCheckpoint lighting;
        public float seconds;
        public bool[] recovered, supplies;
        public PlayerMotor.Progress player;
        public StalkerBrain.Progress[] threats;
        public Door[] doors;
        [Serializable] public sealed class Door { public string id; public bool open; public Vector3 leaf; }
        public static bool Number(float value, float min, float max) => StealthRules.Finite(value) && value >= min && value <= max;
        public static bool Point(Vector3 p) => Number(p.x,197,251) && Number(p.z,197,251) && Number(p.y,-.15f,.85f);
        public static bool Vector(Vector3 p) => Number(p.x,-10000,10000) && Number(p.y,-10000,10000) && Number(p.z,-10000,10000);
        public void Validate()
        {
            if (version != 1 || simulationVersion != 1 || !Guid.TryParseExact(token,"N",out _) || !Number(seconds,0,1000000000) ||
                recovered == null || recovered.Length != 5 || supplies == null || supplies.Length != 8 ||
                doors == null || doors.Length > 162 || threats == null || threats.Length != 4 || player == null)
                throw new ArgumentException("Invalid checkpoint schema");
            // Legacy marker 0 means this run predates batteries/candles. New captures always write 1.
            if(lightingVersion<0 || lightingVersion>1 || lightingVersion==1 && lighting==null ||
                lightingVersion==0 && lighting!=null && !lighting.LegacyEmpty)
                throw new ArgumentException("Invalid lighting checkpoint version");
            if(lightingVersion==1) lighting.Validate();
            player.Validate();
            foreach(var threat in threats) { if(threat == null) throw new ArgumentException("Missing threat"); threat.Validate(); }
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var door in doors)
                if(door == null || string.IsNullOrEmpty(door.id) || door.id.Length > 40 || !ids.Add(door.id) ||
                    !Number(door.leaf.x,0,2.8f) || !Number(door.leaf.y,1.17f,1.19f) || !Number(door.leaf.z,-.01f,.01f))
                    throw new ArgumentException("Invalid door checkpoint");
        }
    }
}
