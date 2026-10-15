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
            var memories = root.GetComponentsInChildren<Interactable>(true).Where(x=>x.kind==Interactable.Kind.CorridorMemory || x.kind==Interactable.Kind.ChapterMemory)
                .OrderBy(x=>x.stableId,StringComparer.Ordinal).ToArray();
            for (int i=0;i<memories.Length;i++)
            {
                var anchor = memories[i].transform;
                Add(root,anchor.position+anchor.right*.28f,Interactable.Kind.TalismanFragment,"ritual-fragment-"+i,"부적의 조각 줍기", "fragment_"+(i%5));
                if (i==0 || i==memories.Length-1)
                    Add(root,anchor.position-anchor.right*.28f,Interactable.Kind.IncenseSupply,"ritual-incense-"+i,"향 줍기","incense");
            }
        }
        static void Add(Transform parent,Vector3 position,Interactable.Kind kind,string id,string label,string model)
        {
            var root = new GameObject(label) { layer=9 }; root.transform.SetParent(parent,false); root.transform.position=position;
            var item = root.AddComponent<Interactable>(); item.kind=kind; item.stableId=id; item.label=label;
            var box = root.AddComponent<BoxCollider>(); box.size=new Vector3(.16f,.05f,.12f);
            var visual = PlayerItemVisuals.Attach(model,root.transform);
            visual.localRotation=Quaternion.Euler(80,15,0);
            if (kind==Interactable.Kind.IncenseSupply) visual.localScale=Vector3.one*.7f;
        }
        public static RitualCheckpoint Capture(PlayerMotor player)
        {
            return new RitualCheckpoint { version=1,incense=player.Rituals.Incense,fragments=player.Rituals.Fragments,talismans=player.Rituals.Talismans,
                selected=(int)player.Items.Selected, smoke=player.Rituals.Smoke ? player.Rituals.Smoke.Remaining : 0,
                supplies=player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup).OrderBy(x=>x.stableId,StringComparer.Ordinal)
                    .Select(x=>new RitualCheckpoint.Supply { id=x.stableId,available=x.gameObject.activeSelf }).ToArray() };
        }
        public static void ValidateRestore(RitualCheckpoint data,PlayerMotor player)
        {
            if (data==null || data.LegacyEmpty) return; data.Validate();
            if (!player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup).Select(x=>x.stableId).OrderBy(x=>x,StringComparer.Ordinal)
                .SequenceEqual(data.supplies.Select(x=>x.id))) throw new ArgumentException("Ritual item geometry mismatch");
        }
        public static void Restore(RitualCheckpoint data,PlayerMotor player)
        {
            if (data==null || data.LegacyEmpty) return;
            player.Rituals.Restore(data.incense,data.fragments,data.talismans); player.Items.Select((PlayerItem)data.selected);
            var states=data.supplies.ToDictionary(x=>x.id,StringComparer.Ordinal);
            foreach (var item in player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>(true).Where(Pickup)) item.gameObject.SetActive(states[item.stableId].available);
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
            if (version!=1 || incense<0 || selected<0 || selected>4 || !CorridorCheckpoint.Number(smoke,0,IncenseGuidance.Duration) || supplies==null || supplies.Length>32)
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
