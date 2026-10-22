using System;
using Unity.AI.Navigation;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>One parked scissor gate and its guide rails; no lock/door gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolRetractableGate : MonoBehaviour
    {
        public Transform Visual {get;private set;}
        public Transform PadlockAnchor {get;private set;}
        public BoxCollider FoldedBody {get;private set;}
        public Bounds FoldedBounds {get;private set;}
        public float ClearWidth=>FoldedBounds.min.z-.22f;

        public static SchoolRetractableGate Build(SchoolLoopWorld owner,Transform section,bool physical)
        {
            var prefab=Resources.Load<GameObject>("SchoolRetractableGate/retractable-gate-visual");
            if(!prefab)throw new InvalidOperationException("Preserved generated school retractable gate is missing");
            var gate=new GameObject("Open folded school retractable gate").AddComponent<SchoolRetractableGate>();
            gate.transform.SetParent(section,false);gate.transform.localPosition=new Vector3(-4.6f,0,0);
            gate.Visual=Instantiate(prefab,gate.transform,false).transform;
            var source=GraphicsPropLibrary.LocalBounds(gate.Visual);
            gate.Visual.localRotation=Quaternion.Euler(0,-90,0);
            // Park against the far jamb. The leading post/padlock faces the open aisle.
            gate.Visual.localPosition=new Vector3(0,.015f,4.39f-source.size.x*.5f);
            gate.PadlockAnchor=gate.Visual.Find("Single padlock storage eye");
            if(!gate.PadlockAnchor)throw new InvalidOperationException("The gate's single hanging-padlock anchor is missing");
            gate.FoldedBounds=GraphicsPropLibrary.LocalBounds(gate.transform);
            if(gate.ClearWidth<3||gate.FoldedBounds.size.x>.65f)
                throw new InvalidOperationException("The parked gate intrudes into the school entry route");
            if(physical)
            {
                var body=new GameObject("Folded retractable gate physical envelope");body.layer=8;
                body.transform.SetParent(gate.transform,false);
                gate.FoldedBody=body.AddComponent<BoxCollider>();gate.FoldedBody.center=gate.FoldedBounds.center;
                gate.FoldedBody.size=gate.FoldedBounds.size+new Vector3(.025f,0,.025f);
                var navigation=body.AddComponent<NavMeshModifier>();navigation.overrideArea=true;navigation.area=1;
            }
            var iron=owner.HorrorSurfaces.Rust;
            owner.Box(gate.transform,"Retractable gate upper guide channel",new Vector3(0,2.63f,2.2f),new Vector3(.095f,.095f,4.4f),iron);
            // A flush visual guide leaves the physical floor continuous for walking.
            owner.Box(gate.transform,"Retractable gate flush floor guide",new Vector3(0,.006f,2.2f),new Vector3(.045f,.012f,4.18f),iron);
            owner.Box(gate.transform,"Retractable gate receiving jamb",new Vector3(0,1.28f,.16f),new Vector3(.075f,2.56f,.075f),iron,physical);
            owner.Box(gate.transform,"Retractable gate opposite locking receiver",new Vector3(.047f,1.18f,.16f),new Vector3(.025f,.12f,.08f),iron);
            return gate;
        }
    }
}
