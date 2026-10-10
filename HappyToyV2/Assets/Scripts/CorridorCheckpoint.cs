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
        public int furnitureVersion;
        public Drawer[] drawers;
        public float seconds;
        public bool[] recovered, supplies;
        public PlayerMotor.Progress player;
        public StalkerBrain.Progress[] threats;
        public int threatVersion;
        public LanternMaskEncounter.ChapterProgress mask;
        public Door[] doors;
        [Serializable] public sealed class Door { public string id; public bool open; public Vector3 leaf; }
        [Serializable] public sealed class Drawer { public string id; public bool open; public float travel; }
        public static bool Number(float value, float min, float max) => StealthRules.Finite(value) && value >= min && value <= max;
        public static bool Point(Vector3 p) => Number(p.x,193,268) && Number(p.z,193,268) && Number(p.y,-.15f,.85f);
        public static bool Vector(Vector3 p) => Number(p.x,-10000,10000) && Number(p.y,-10000,10000) && Number(p.z,-10000,10000);
        public void Validate()
        {
            if (version != 1 || simulationVersion < 1 || simulationVersion > 3 || !Guid.TryParseExact(token,"N",out _) || !Number(seconds,0,1000000000) ||
                recovered == null || recovered.Length != 5 || supplies == null || supplies.Length != 8 ||
                doors == null || doors.Length > 162 || threats == null || player == null ||
                threatVersion < 0 || threatVersion > 1 || threats.Length != (threatVersion == 0 ? 4 : 3) ||
                (threatVersion == 1 && mask == null) || (threatVersion == 0 && mask != null && !mask.LegacyEmpty))
                throw new ArgumentException("Invalid checkpoint schema");
            // Legacy marker 0 means this run predates batteries/candles. New captures always write 1.
            if(lightingVersion<0 || lightingVersion>1 || lightingVersion==1 && lighting==null ||
                lightingVersion==0 && lighting!=null && !lighting.LegacyEmpty)
                throw new ArgumentException("Invalid lighting checkpoint version");
            if(lightingVersion==1) lighting.Validate();
            // Marker zero is an older save with no furniture. Finite pickup
            // flags still apply unchanged; new trays simply start closed.
            if(furnitureVersion<0 || furnitureVersion>2 || furnitureVersion==0 && drawers!=null && drawers.Length!=0 ||
                furnitureVersion==1 && (drawers==null || drawers.Length!=4) ||
                furnitureVersion==2 && (drawers==null || drawers.Length!=12))
                throw new ArgumentException("Invalid furniture checkpoint version");
            if(furnitureVersion>=1)
            {
                var drawerIds=new System.Collections.Generic.HashSet<string>();
                foreach(var drawer in drawers)
                    if(drawer==null || !ValidDrawerId(drawer.id, furnitureVersion) ||
                        !drawerIds.Add(drawer.id) || !Number(drawer.travel,0,.30f)) throw new ArgumentException("Invalid drawer checkpoint");
            }
            player.Validate();
            foreach(var threat in threats) { if(threat == null) throw new ArgumentException("Missing threat"); threat.Validate(); }
            if(threatVersion==1) mask.ValidateCorridor();
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var door in doors)
                if(door == null || string.IsNullOrEmpty(door.id) || door.id.Length > 40 || !ids.Add(door.id) ||
                    !Number(door.leaf.x,0,2.8f) || !Number(door.leaf.y,1.17f,1.19f) || !Number(door.leaf.z,-.01f,.01f))
                    throw new ArgumentException("Invalid door checkpoint");
        }
        static bool ValidDrawerId(string id, int furnitureVersion)
        {
            if (furnitureVersion == 1) return id=="drawer-supply-0" || id=="drawer-supply-2" || id=="drawer-supply-4" || id=="drawer-supply-6";
            if (string.IsNullOrEmpty(id)) return false;
            if (id.StartsWith("drawer-supply-")) return int.TryParse(id.Substring(14),out int supply) && supply>=0 && supply<8 && id=="drawer-supply-"+supply;
            return id.StartsWith("drawer-atmosphere-") && int.TryParse(id.Substring(18),out int cell) && cell>=0 && cell<CorridorLayout.Count && id=="drawer-atmosphere-"+cell;
        }
    }
}
