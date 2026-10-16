using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    public static class TrapWorld
    {
        public static void Populate(Transform parent, PlayerMotor player, Interactable[] sources)
        {
            player.Traps.ConfigureWorld(parent);
            RitualWorld.Populate(parent,player);
            Physics.SyncTransforms();
            int number = 0;
            for (int i=1;i<sources.Length;i+=3)
            {
                Vector3 point = default; bool found = false;
                foreach (var offset in SupplyOffsets())
                {
                    var desired = sources[i].transform.position+offset;
                    // Use the supply's actual floor, including the school's basement.
                    desired.y = sources[i].transform.position.y-.85f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(desired,out var nav,1.25f,UnityEngine.AI.NavMesh.AllAreas) &&
                        Mathf.Abs(nav.position.y-sources[i].transform.position.y) < 1.5f && TrapPlacement.Ground(nav.position,out point,true)) { found = true; break; }
                }
                if (!found) continue;
                var root = new GameObject("Iron trap supply "+number); root.layer = 9;
                root.transform.SetParent(parent,false); root.transform.position = point+Vector3.up*.002f;
                var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0,.07f,0); collider.size = new Vector3(.29f,.14f,.27f);
                var item = root.AddComponent<Interactable>(); item.kind = Interactable.Kind.TrapSupply;
                item.stableId = "trap-supply-"+number++; item.label = "덫 줍기";
                TrapVisuals.Attach(root.transform,.45f).SetClosed(.85f);
            }
        }
        static IEnumerable<Vector3> SupplyOffsets()
        {
            foreach(float radius in new[]{.8f,1.3f,1.8f})
                for(int direction=0;direction<8;direction++)
                    yield return Quaternion.Euler(0,direction*45,0)*Vector3.forward*radius;
        }
        public static bool Holding => UnityEngine.Object.FindObjectsByType<EnemyTrapRestraint>(FindObjectsSortMode.None)
            .Any(x=>x.Held && GameSession.Current && x.gameObject.scene==GameSession.Current.gameObject.scene);
        public static TrapExplorationCheckpoint Capture(PlayerMotor player)
        {
            var root = player.Traps.WorldRoot;
            return new TrapExplorationCheckpoint {
                stock = player.Traps.Count, cooldown = player.Traps.Cooldown,
                supplies = root.GetComponentsInChildren<Interactable>(true).Where(x=>x.kind==Interactable.Kind.TrapSupply)
                    .OrderBy(x=>x.stableId,StringComparer.Ordinal).Select(x=>new TrapExplorationCheckpoint.Supply{id=x.stableId,available=x.gameObject.activeSelf}).ToArray(),
                armed = root.GetComponentsInChildren<PlacedMonsterTrap>().Where(x=>x.Armed)
                    .Select(x=>new TrapExplorationCheckpoint.Placed{point=x.transform.position-Vector3.up*.006f,rotation=x.transform.rotation}).ToArray() };
        }
        public static void ValidateRestore(TrapExplorationCheckpoint data, PlayerMotor player)
        {
            if (data == null) return;
            data.Validate();
            var ids = player.Traps.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(x=>x.kind==Interactable.Kind.TrapSupply)
                .Select(x=>x.stableId).OrderBy(x=>x,StringComparer.Ordinal);
            if (!ids.SequenceEqual(data.supplies.Select(x=>x.id))) throw new ArgumentException("Trap supply geometry mismatch");
            foreach (var trap in data.armed)
                if (!TrapPlacement.Ground(trap.point,out var floor,true) || Vector3.Distance(floor,trap.point) > .08f)
                    throw new ArgumentException("Unsupported saved trap footprint");
        }
        public static void Restore(TrapExplorationCheckpoint data, PlayerMotor player)
        {
            if (data == null) return; // Optional field: legacy saves keep their new starting trap.
            var root = player.Traps.WorldRoot; player.Traps.Restore(data.stock,data.cooldown);
            var supplies = data.supplies.ToDictionary(x=>x.id,StringComparer.Ordinal);
            foreach (var item in root.GetComponentsInChildren<Interactable>(true).Where(x=>x.kind==Interactable.Kind.TrapSupply))
                item.gameObject.SetActive(supplies[item.stableId].available);
            foreach (var trap in root.GetComponentsInChildren<PlacedMonsterTrap>()) UnityEngine.Object.Destroy(trap.gameObject);
            foreach (var trap in data.armed) PlacedMonsterTrap.Create(root,trap.point,trap.rotation,false);
        }
    }
    [Serializable]
    public sealed class TrapExplorationCheckpoint
    {
        public int stock;
        public float cooldown;
        public Supply[] supplies;
        public Placed[] armed;
        [Serializable] public sealed class Supply { public string id; public bool available; }
        [Serializable] public sealed class Placed { public Vector3 point; public Quaternion rotation; }
        public void Validate(Func<Vector3,bool> pointRule = null)
        {
            if (stock<0 || stock>MonsterTrapInventory.Capacity || !CorridorCheckpoint.Number(cooldown,0,.5f) ||
                supplies==null || supplies.Length>64 || armed==null || armed.Length>64) throw new ArgumentException("Invalid trap checkpoint");
            var ids = new HashSet<string>(); string prior = "";
            foreach (var supply in supplies)
            {
                if (supply==null || string.IsNullOrEmpty(supply.id) || !supply.id.StartsWith("trap-supply-") || supply.id.Length>40 ||
                    !ids.Add(supply.id) || string.CompareOrdinal(prior,supply.id)>0) throw new ArgumentException("Invalid trap supply state");
                prior = supply.id;
            }
            foreach (var trap in armed)
                if (trap==null || !CorridorCheckpoint.Vector(trap.point) || pointRule!=null && !pointRule(trap.point) || !ChapterCheckpoint.Rotation(trap.rotation))
                    throw new ArgumentException("Invalid saved trap pose");
        }
    }
}
