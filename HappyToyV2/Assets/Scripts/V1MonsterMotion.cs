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
        UncatLimbContacts limbContacts;StalkerFootsteps footsteps;
        public bool LimbContactRigAvailable => limbContacts!=null;
        public float LimbContactGap { get; private set; }
        public float AttackPoseWeight=>attackPose==null?0:attackPose.Weight;
        public bool AttackRigAvailable=>attackPose!=null&&attackPose.Available;
        public Vector3 AttackHand=>attackPose==null?Vector3.zero:attackPose.HandPosition;
        float groundCorrectionLimit=.35f;
        public float GroundGap { get; private set; }
        public string CurrentClip => current;
        public float ClipTime => animationPlayer&&current!=null?animationPlayer[current].time:0;
        public bool RefinedVisual { get; private set; }
        Material[] refinedMaterials;
        void Awake()
        {
            QualitySettings.skinWeights=SkinWeights.Unlimited;brain=GetComponent<StalkerBrain>();agent=GetComponent<NavMeshAgent>();
            // Preserve the original clip pose while mounting its actual rendered
            // sole on the floor. Uncat's authored chase dips .441 m below patrol0;
            // the old fixed .35 m limit left skin below the floor. Bound correction
            // by the existing agent's half-height, without changing its capsule.
            if(agent&&agent.height>0&&!float.IsNaN(agent.height)&&!float.IsInfinity(agent.height))
                groundCorrectionLimit=Mathf.Max(.35f,agent.height*.5f);
            RefinedVisual=EnemyVisualRefinement.TryApply(this,out var refinedModel,out var refinedAnimation,out refinedMaterials);
            model=refinedModel;animationPlayer=refinedAnimation;
            rest=model.localRotation;restPosition=model.localPosition;skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            sampledMesh=new Mesh();attackPose=new V1AttackPose(model);
            if(EnemySoundProfile.Identify(transform)==EnemySoundKind.Uncat)
                limbContacts=new UncatLimbContacts(skins);
        }
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
                float sole=float.MaxValue,limbSole=float.MaxValue;
                for(int index=0;index<skins.Length;index++)
                {
                    var skin=skins[index];var contactMask=limbContacts?.ForSkin(index);
                    // FBX hierarchy has ~95x scale; compensate it before applying TransformPoint.
                    skin.BakeMesh(sampledMesh,true);sampledMesh.GetVertices(vertices);
                    for(int vertex=0;vertex<vertices.Count;vertex++)
                    {
                        float y=skin.transform.TransformPoint(vertices[vertex]).y;sole=Mathf.Min(sole,y);
                        if(contactMask!=null&&contactMask[vertex])limbSole=Mathf.Min(limbSole,y);
                    }
                }
                if(sole!=float.MaxValue)
                {
                    float correction=Mathf.Clamp(floor.point.y-sole,-groundCorrectionLimit,groundCorrectionLimit);
                    model.position+=Vector3.up*correction;GroundGap=sole+correction-floor.point.y;
                    if(limbSole!=float.MaxValue)
                    {
                        LimbContactGap=limbSole+correction-floor.point.y;
                        if(!footsteps)footsteps=GetComponent<StalkerFootsteps>();
                        // Observe this frame's actual skin AFTER mounting. The
                        // feet/hands may be raised while a tail/body is grounded.
                        if(footsteps)footsteps.EmitAtRenderedLimbContact(LimbContactGap);
                    }
                }
            }
        }
        void OnDestroy()
        {
            if(sampledMesh)Destroy(sampledMesh);
            if(refinedMaterials!=null)foreach(var material in refinedMaterials)if(material)Destroy(material);
        }
        void OnDisable()
        {
            attackPose?.Reset();
            if(limbContacts!=null)
            {
                if(!footsteps)footsteps=GetComponent<StalkerFootsteps>();
                if(footsteps)footsteps.ClearRenderedContactDebt();
            }
        }
    }
}
