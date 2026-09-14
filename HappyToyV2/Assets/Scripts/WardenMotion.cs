using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Articulated Blender parts: distance-driven gait plus a strike matching the actual hit window.
    public sealed class WardenMotion : MonoBehaviour
    {
        readonly Dictionary<string,Transform> joints=new Dictionary<string,Transform>();
        readonly Dictionary<string,Quaternion> rest=new Dictionary<string,Quaternion>();
        NavMeshAgent agent;StalkerBrain brain;Vector3 previous;float phase;
        Transform visualRoot;Vector3 visualRest;
        readonly List<Renderer> shoes=new List<Renderer>();
        public float SoleGap { get; private set; }
        public int BoundJoints => joints.Count;
        public float GaitDistance { get; private set; }
        public float PoseMagnitude { get; private set; }
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();brain=GetComponent<StalkerBrain>();
            foreach(var t in GetComponentsInChildren<Transform>())
                if(t.name=="WardenBody"||t.name=="WardenHead"||t.name=="LegL"||t.name=="LegR"||
                   t.name=="ArmL"||t.name=="ArmR"||t.name=="ForearmL"||t.name=="ForearmR")
                {joints[t.name]=t;rest[t.name]=t.localRotation;}
            if(joints.TryGetValue("WardenBody",out var body)){visualRoot=body.parent;visualRest=visualRoot.localPosition;}
            foreach(var renderer in GetComponentsInChildren<Renderer>())
                if(renderer.name.Replace('_',' ').StartsWith("Worn school shoe"))shoes.Add(renderer);
        }
        void OnEnable(){previous=transform.position;}
        void Bend(string name,float degrees,Vector3 axis)
        {
            if(!joints.TryGetValue(name,out var joint))return;
            var localAxis=joint.parent.InverseTransformDirection(axis);
            joint.localRotation=Quaternion.AngleAxis(degrees,localAxis)*rest[name];
            PoseMagnitude+=Mathf.Abs(degrees);
        }
        void LateUpdate()
        {
            if(!brain||!agent||Time.timeScale==0)return;
            float distance=Vector3.Distance(previous,transform.position);previous=transform.position;
            if(distance<1){phase+=distance*Mathf.PI/.85f;GaitDistance+=distance;}
            float blend=Mathf.Clamp01(agent.velocity.magnitude/1.45f);
            float gait=Mathf.Sin(phase)*blend;
            float windup=brain.AttackWindup;
            float strike=brain.AttackRecovery;
            float reach=windup>0?Mathf.Lerp(0,65,windup):strike>0?Mathf.Lerp(0,-85,Mathf.Clamp01(strike*2)):0;
            // Lower the complete visual as the rigid legs swing, keeping the supporting sole near the floor.
            if(visualRoot)visualRoot.localPosition=visualRest-Vector3.up*(.94f*(1-Mathf.Cos(gait*22*Mathf.Deg2Rad)));
            PoseMagnitude=0;
            Bend("WardenBody",3+Mathf.Abs(gait)*3+Mathf.Max(0,-reach)*.12f,transform.right);
            Bend("LegL",gait*22,transform.right);Bend("LegR",-gait*22,transform.right);
            Bend("ArmL",-gait*17+reach,transform.right);Bend("ArmR",gait*17+reach*.85f,transform.right);
            Bend("ForearmL",-10-windup*38,transform.right);Bend("ForearmR",-6-windup*30,transform.right);
            Bend("WardenHead",Mathf.Sin(Time.time*1.3f)*3+(brain.state==StalkerBrain.State.Chase?12:0),transform.forward);
            // NavMesh sits slightly above physical floor; align the lowest sole to the actual floor.
            if(visualRoot&&shoes.Count>0&&Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                float sole=float.MaxValue;foreach(var shoe in shoes)sole=Mathf.Min(sole,shoe.bounds.min.y);
                float correction=Mathf.Clamp(floor.point.y-sole,-.2f,.2f);
                visualRoot.position+=Vector3.up*correction;SoleGap=sole+correction-floor.point.y;
            }
        }
    }
}
