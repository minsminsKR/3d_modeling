using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Assembly boundary. Each collaborator disposes only its own runtime art.
    [DisallowMultipleComponent]
    public sealed class CorridorEntrancePresentation : MonoBehaviour
    {
        Transform assembly;
        public Transform TempleGateRoot {get;private set;}
        public CorridorEntranceCavern Cavern {get;private set;}
        public CorridorEntranceRitualDressing Dressing {get;private set;}
        public CorridorEntranceVapour Vapour {get;private set;}
        public int RockInstances=>Cavern?Cavern.RockInstances:0;
        public IReadOnlyList<Light> Lanterns=>Dressing.Lanterns;

        public static CorridorEntrancePresentation Attach(CorridorEntranceChamber chamber)
        {
            if(!chamber||!chamber.VisualRoot||!chamber.Gate)throw new ArgumentException("Prepared entrance chamber required");
            if(chamber.TryGetComponent<CorridorEntrancePresentation>(out var existing))return existing;
            var result=chamber.gameObject.AddComponent<CorridorEntrancePresentation>();
            result.assembly=CorridorEntranceModels.Group(chamber.VisualRoot,"Owned sealed sanctuary visual assembly",Vector3.zero);
            result.Cavern=CorridorEntranceCavern.Attach(chamber,result.assembly);
            result.TempleGateRoot=CorridorEntranceModels.Attach("temple-gate",result.assembly,"Monumental sealed Korean temple gate",
                chamber.VisualRoot.InverseTransformPoint(chamber.GatePosition));
            chamber.ConfigureGateBody(new Vector3(0,3.9f,1.70f),new Vector3(9.2f,7.8f,.55f));
            chamber.ConfigureGateGeometry(result.TempleGateRoot);
            result.Dressing=CorridorEntranceRitualDressing.Attach(result.assembly);
            result.Vapour=CorridorEntranceVapour.Attach(result.assembly,result.Dressing.IncenseSources);
            return result;
        }
        void OnDestroy(){if(assembly)Destroy(assembly.gameObject);}
    }
}
