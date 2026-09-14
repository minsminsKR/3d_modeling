using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace HappyToy.V2
{
    public sealed class V1MonsterMotion:MonoBehaviour
    {
        public Animation animationPlayer;
        public Transform model;
        StalkerBrain brain;NavMeshAgent agent;
        Quaternion rest;string current;
        Vector3 restPosition;SkinnedMeshRenderer[] skins;Mesh sampledMesh;
        readonly List<Vector3> vertices=new List<Vector3>();
        V1AttackPose attackPose;
        public float AttackPoseWeight=>attackPose==null?0:attackPose.Weight;
        public bool AttackRigAvailable=>attackPose!=null&&attackPose.Available;
        public Vector3 AttackHand=>attackPose==null?Vector3.zero:attackPose.HandPosition;
        public float GroundGap { get; private set; }
        public string CurrentClip => current;
        public float ClipTime => animationPlayer&&current!=null?animationPlayer[current].time:0;
        void Awake(){QualitySettings.skinWeights=SkinWeights.Unlimited;brain=GetComponent<StalkerBrain>();agent=GetComponent<NavMeshAgent>();rest=model.localRotation;restPosition=model.localPosition;skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();sampledMesh=new Mesh();attackPose=new V1AttackPose(model);}
        void Update()
        {
            if(!animationPlayer||GameSession.Current.Finished)return;
            if(Time.timeScale==0)return;
            attackPose.Reset();
            var next=brain.state==StalkerBrain.State.Chase?"chase":"patrol";
            if(current!=next){animationPlayer.CrossFade(next,.2f);current=next;}
            bool striking=brain.AttackActive;
            animationPlayer[current].speed=striking?0:Mathf.Clamp(agent.velocity.magnitude/(next=="chase"?3.5f:1.45f),0,1.5f);
        }
        void LateUpdate()
        {
            if(Time.timeScale==0)return;
            // Keep V1 locomotion, with a skeletal anticipation/reach/recovery layered over it.
            float bend=brain.AttackWindup>0?-6*brain.AttackWindup:8*brain.AttackRecovery;
            model.localRotation=Quaternion.Euler(bend,0,0)*rest;
            model.localPosition=restPosition;
            attackPose.Apply(brain,transform);
            if(Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                float sole=float.MaxValue;
                foreach(var skin in skins)
                {
                    // FBX hierarchy has ~95x scale; compensate it before applying TransformPoint.
                    skin.BakeMesh(sampledMesh,true);sampledMesh.GetVertices(vertices);
                    foreach(var vertex in vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertex).y);
                }
                if(sole!=float.MaxValue){float correction=Mathf.Clamp(floor.point.y-sole,-.35f,.35f);model.position+=Vector3.up*correction;GroundGap=sole+correction-floor.point.y;}
            }
        }
        void OnDestroy(){if(sampledMesh)Destroy(sampledMesh);}
        void OnDisable(){attackPose?.Reset();}
    }
}
