using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns real observer evidence and the lifetime of a committed third capture.
    // The pure memory owns counts; monsters retain navigation, warning and impact.
    internal sealed class PlayerCabinetMemory
    {
        sealed class Witness { public Behaviour owner; public Func<bool> aggressive; }
        readonly List<Witness> witnesses=new List<Witness>();
        readonly CabinetTauntMemory memory=new CabinetTauntMemory();
        public bool ForcedCapture {get;private set;}
        public int CurrentCount {get;private set;}
        public bool HasWitness=>witnesses.Count>0;
        public bool Remembers(Behaviour actor,Vector3 entrance) => actor &&
            Vector3.Distance(actor.transform.position,entrance)<=CabinetTauntMemory.CloseDistance &&
            memory.Remembers(Identity(actor.transform),entrance.x,entrance.y,entrance.z);
        public void Prepare(PlayerMotor player,Vector3 entrance,StalkerBrain[] stalkers,LanternMaskEncounter[] masks,WeepingAngelEncounter[] angels)
        {
            witnesses.Clear();ForcedCapture=false;CurrentCount=0;
            var p=player.transform.position;memory.ForgetDistant(p.x,p.y,p.z);
            foreach(var actor in stalkers)if(actor.CanWitnessHiding(player,entrance))Add(actor,()=>actor.CabinetAggressive);
            foreach(var actor in masks)if(actor.CanWitnessHiding(player,entrance))Add(actor,()=>actor.CabinetAggressive);
            foreach(var actor in angels)if(actor.CanWitnessHiding(player,entrance))Add(actor,()=>actor.CabinetAggressive);
        }
        void Add(Behaviour owner,Func<bool> aggressive)=>witnesses.Add(new Witness {owner=owner,aggressive=aggressive});
        public void Record(int entry,Interactable cabinet,Vector3 entrance)
        {
            string key=string.IsNullOrEmpty(cabinet.stableId)?Identity(cabinet.transform):cabinet.stableId;
            foreach(var witness in witnesses)
            {
                var actor=witness.owner;
                if(!EnemyNavigation.SameFloor(actor.transform.position,entrance.y))continue;
                string observer=Identity(actor.transform);
                ForcedCapture|=memory.Observe(entry,key,observer,entrance.x,entrance.y,entrance.z,Vector3.Distance(actor.transform.position,entrance));
                CurrentCount=Math.Max(CurrentCount,memory.Count(key,observer));
            }
        }
        public bool BlocksExit()
        {
            if(!ForcedCapture)return false;
            foreach(var w in witnesses)if(w.owner && w.owner.isActiveAndEnabled && w.aggressive())return true;
            return false;
        }
        // Stable in regenerated scenes and across JSON restore; never an InstanceID.
        static string Identity(Transform transform)
        {
            string path="";
            for(var at=transform;at;at=at.parent)path="/"+at.name+"["+at.GetSiblingIndex()+"]"+path;
            return path;
        }
        public CabinetTauntMemory.Visit[] Capture(Vector3 position)
        {memory.ForgetDistant(position.x,position.y,position.z);return memory.Capture();}
        public void ForgetDistant(Vector3 position)=>memory.ForgetDistant(position.x,position.y,position.z);
        public void Restore(CabinetTauntMemory.Visit[] snapshot,int entry)
        {memory.Restore(snapshot,entry);witnesses.Clear();ForcedCapture=false;CurrentCount=0;}
    }
}
