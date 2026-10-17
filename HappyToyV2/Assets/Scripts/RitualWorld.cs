using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    public static class RitualWorld
    {
        static bool Pickup(Interactable item) => item.kind == Interactable.Kind.IncenseSupply || item.kind == Interactable.Kind.TalismanFragment;
        public static void Populate(Transform root,PlayerMotor player)
        {
            player.Rituals.ConfigureWorld(root);
            Physics.SyncTransforms();
            var memories = root.GetComponentsInChildren<Interactable>(true).Where(x=>x.kind==Interactable.Kind.CorridorMemory || x.kind==Interactable.Kind.ChapterMemory)
                .OrderBy(x=>x.stableId,StringComparer.Ordinal).ToArray();
            for (int i=0;i<memories.Length;i++)
            {
                var anchor = memories[i].transform;
                Add(root,anchor.position+anchor.right*.28f,Interactable.Kind.TalismanFragment,"ritual-fragment-"+i,"부적의 조각 줍기", "fragment_"+(i%5));
                if (i==0 || i==memories.Length-1)
                    Add(root,anchor.position-anchor.right*.28f,Interactable.Kind.IncenseSupply,"ritual-incense-"+i,"향 줍기","incense");
            }
            var corridor=root.GetComponentInParent<CorridorRun>();
            if(corridor)PopulateFurniture(corridor);
        }
        static void PopulateFurniture(CorridorRun corridor)
        {
            int index=0;
            foreach(var drawer in corridor.Drawers.Where(x=>!x.ContainedPickup&&x.StableId.StartsWith("drawer-supply-",StringComparison.Ordinal)).OrderBy(x=>x.StableId,StringComparer.Ordinal))
            {
                var support=drawer.PickupSupport;
                var at=new Vector3(support.bounds.center.x,support.bounds.max.y,support.bounds.center.z);
                bool incense=index++%2==0;
                drawer.AddPickup(Add(drawer.GetComponentInParent<CorridorFurniturePlacement>().transform,at,incense?Interactable.Kind.IncenseSupply:Interactable.Kind.TalismanFragment,
                    "ritual-drawer-"+drawer.StableId,incense?"향 줍기":"부적의 조각 줍기",incense?"incense":"fragment_"+(index%5),support));
            }
            index=0;
            foreach(var shelf in corridor.Furnishings.Where(x=>x.PropKey=="archive-shelf").OrderBy(x=>x.StableId,StringComparer.Ordinal))
            {
                bool incense=index++%2==0;
                // Lower left bay, alongside the finite battery pack, clear of
                // the books on the right and supported by the same real board.
                Add(shelf.transform,shelf.transform.TransformPoint(new Vector3(-.12f,.504f,-.09f)),
                    incense?Interactable.Kind.IncenseSupply:Interactable.Kind.TalismanFragment,"ritual-shelf-"+index,
                    incense?"향 줍기":"부적의 조각 줍기",incense?"incense":"fragment_"+(index%5));
            }
        }
        static Interactable Add(Transform parent,Vector3 position,Interactable.Kind kind,string id,string label,string model,Collider support=null)
        {
            var root = new GameObject(label) { layer=9 }; root.transform.SetParent(parent,false); root.transform.position=position;
            var item = root.AddComponent<Interactable>(); item.kind=kind; item.stableId=id; item.label=label;
            var box = root.AddComponent<BoxCollider>();
            var visual = PlayerItemVisuals.Attach(model,root.transform);
            if (kind==Interactable.Kind.IncenseSupply) visual.localScale=Vector3.one*.7f;
            if(support)RitualPickupPlacement.PlaceOnSupport(root.transform,visual,box,position,support);
            else RitualPickupPlacement.Place(root.transform,visual,box,position);
            foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=9;
            return item;
        }
        public static RitualCheckpoint Capture(PlayerMotor player)
        {
            return new RitualCheckpoint { version=2,incense=player.Rituals.Incense,fragments=player.Rituals.Fragments,talismans=player.Rituals.Talismans,
                selected=(int)player.Items.Selected, smoke=player.Rituals.Smoke ? player.Rituals.Smoke.Remaining : 0,
                supplies=player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup).OrderBy(x=>x.stableId,StringComparer.Ordinal)
                    .Select(x=>new RitualCheckpoint.Supply { id=x.stableId,available=x.gameObject.activeSelf }).ToArray() };
        }
        public static void ValidateRestore(RitualCheckpoint data,PlayerMotor player)
        {
            if (data==null || data.LegacyEmpty) return; data.Validate();
            var expected=player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup);
            if(data.version==1)expected=expected.Where(x=>x.stableId.StartsWith("ritual-fragment-",StringComparison.Ordinal)||x.stableId.StartsWith("ritual-incense-",StringComparison.Ordinal));
            if (!expected.Select(x=>x.stableId).OrderBy(x=>x,StringComparer.Ordinal)
                .SequenceEqual(data.supplies.Select(x=>x.id))) throw new ArgumentException("Ritual item geometry mismatch");
        }
        public static void Restore(RitualCheckpoint data,PlayerMotor player)
        {
            if (data==null || data.LegacyEmpty) return;
            player.Rituals.Restore(data.incense,data.fragments,data.talismans); player.Items.Select((PlayerItem)data.selected);
            var states=data.supplies.ToDictionary(x=>x.id,StringComparer.Ordinal);
            foreach (var item in player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup))
                item.gameObject.SetActive(!states.TryGetValue(item.stableId,out var state)||state.available);
            if (data.smoke>0) player.Rituals.RestoreSmoke(data.smoke);
        }
    }
    [Serializable]
    public sealed class RitualCheckpoint
    {
        public int version,incense,fragments,talismans,selected;
        public float smoke;
        public Supply[] supplies;
        public bool LegacyEmpty => version==0 && incense==0 && fragments==0 && talismans==0 && selected==0 && smoke==0 &&
            (supplies==null || supplies.Length==0);
        [Serializable] public sealed class Supply { public string id; public bool available; }
        public void Validate()
        {
            if (LegacyEmpty) return;
            TalismanStockRules.Validate(fragments,talismans);
            if ((version!=1&&version!=2) || incense<0 || selected<0 || selected>4 || !CorridorCheckpoint.Number(smoke,0,IncenseGuidance.Duration) || supplies==null || supplies.Length>32)
                throw new ArgumentException("Invalid ritual item stock");
            string prior="";
            foreach (var item in supplies)
            {
                if (item==null || string.IsNullOrEmpty(item.id) || item.id.Length>48 || !item.id.StartsWith("ritual-") || string.CompareOrdinal(prior,item.id)>=0)
                    throw new ArgumentException("Invalid ritual pickup state");
                prior=item.id;
            }
        }
    }
}
