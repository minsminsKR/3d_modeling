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
        CyclopseStumblePose introPose;
        float introFall, introKneel, introBrace, introRoll, introYawSign;
        public bool IntroPoseActive { get; private set; }
        public bool IntroPoseRigAvailable => introPose != null && introPose.Available;
        public float IntroBodyPitch => IntroPoseActive ? 86 * introFall : 0;
        public float IntroBodyYaw => IntroPoseActive ? 78 * introFall * introYawSign : 0;
        public Vector3 IntroHeadPosition => introPose != null ? introPose.HeadPosition : transform.position + Vector3.up * PresentationHeight * .8f;
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
        public float PresentationHeight { get; private set; }
        public float PresentationScale { get; private set; } = 1;
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
            var patrol=animationPlayer.GetClip("patrol");
            if(patrol)patrol.SampleAnimation(animationPlayer.gameObject,0);
            PresentationScale=MonsterPresentationScale.Enlarge(model,
                MonsterPresentationScale.TargetHeight(EnemySoundProfile.Identify(transform)),
                transform.position.y-(agent?agent.baseOffset:0));
            PresentationHeight=MonsterPresentationScale.Bounds(model).size.y;
            groundCorrectionLimit*=PresentationScale;
            rest=model.localRotation;restPosition=model.localPosition;skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
            sampledMesh=new Mesh();attackPose=new V1AttackPose(model);
            if(EnemySoundProfile.Identify(transform)==EnemySoundKind.Cyclopse)
                introPose=new CyclopseStumblePose(model);
            if(EnemySoundProfile.Identify(transform)==EnemySoundKind.Uncat)
                limbContacts=new UncatLimbContacts(skins);
        }
        void Update()
        {
            if(!animationPlayer||GameSession.Current.Finished)return;
            if(Time.timeScale==0)return;
            attackPose.Reset();
            introPose?.Reset();
            var next=brain.state==StalkerBrain.State.Chase?"chase":"patrol";
            bool cryingIdle = brain.CorridorBaby && brain.state != StalkerBrain.State.Chase &&
                agent.velocity.sqrMagnitude < .0036f && animationPlayer.GetClip("cry");
            if (cryingIdle) { next = "cry"; animationPlayer["cry"].wrapMode = WrapMode.Loop; }
            if(current!=next){animationPlayer.CrossFade(next,.2f);current=next;}
            bool striking=brain.AttackActive;
            animationPlayer[current].speed = striking || IntroPoseActive ? 0 : cryingIdle ? .72f :
                Mathf.Clamp(agent.velocity.magnitude/(next=="chase"?3.5f:1.45f),0,1.5f);
        }
        void LateUpdate()
        {
            if(Time.timeScale==0)return;
            // Keep V1 locomotion, with a skeletal anticipation/reach/recovery layered over it.
            float bend=brain.AttackWindup>0?-6*brain.AttackWindup:8*brain.AttackRecovery;
            model.localRotation=IntroPoseActive?
                Quaternion.AngleAxis(IntroBodyYaw,Vector3.up)*Quaternion.Euler(IntroBodyPitch,0,introRoll)*rest:
                Quaternion.Euler(bend,0,0)*rest;
            model.localPosition=restPosition;
            if(IntroPoseActive)
            {
                // Fall diagonally into the visible main hall. Yaw precedes pitch,
                // so both the body and its planted hands share the same axes.
                // The route actor and navigation capsule remain at the junction.
                var yaw=Quaternion.AngleAxis(IntroBodyYaw,Vector3.up);
                var forward=yaw*transform.forward;var right=yaw*transform.right;
                model.position+=forward*(.16f*introFall);
                introPose?.Apply(forward,right,introFall,introKneel,introBrace);
            }
            else attackPose.Apply(brain,transform);
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
                    // A prone body pivots from its authored feet, so its lowest
                    // rendered vertex can move much farther than a walking sole.
                    // Mount that actual posed skin, rather than moving the agent
                    // or keeping the fallen monster floating above the corridor.
                    float limit=IntroPoseActive?Mathf.Max(groundCorrectionLimit,PresentationHeight):groundCorrectionLimit;
                    float correction=Mathf.Clamp(floor.point.y-sole,-limit,limit);
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
            ClearIntroPose();
            if(limbContacts!=null)
            {
                if(!footsteps)footsteps=GetComponent<StalkerFootsteps>();
                if(footsteps)footsteps.ClearRenderedContactDebt();
            }
        }
        public void SetIntroPose(float fallWeight,float kneelWeight,float braceWeight,float rollDegrees=0)
        {
            if(!IntroPoseRigAvailable)return;
            if(!IntroPoseActive)
            {
                var session=GameSession.Current;
                var eye=session&&session.player&&session.player.eyes?session.player.eyes.transform.position:
                    transform.position-transform.right;
                var towardEye=eye-transform.position;towardEye.y=0;
                introYawSign=Vector3.Dot(transform.right,towardEye)>=0?1:-1;
            }
            IntroPoseActive=true;
            introFall=Mathf.Clamp01(fallWeight);introKneel=Mathf.Clamp01(kneelWeight);
            introBrace=Mathf.Clamp01(braceWeight);introRoll=Mathf.Clamp(rollDegrees,-8,8);
        }
        public void ClearIntroPose()
        {
            introPose?.Reset();IntroPoseActive=false;
            introFall=introKneel=introBrace=introRoll=introYawSign=0;
            if(model){model.localRotation=rest;model.localPosition=restPosition;}
        }

        // A short additive event pose on the original Mixamo hierarchy. Joint
        // lengths, bind poses and the imported locomotion clips remain intact.
        sealed class CyclopseStumblePose
        {
            readonly Transform head,rightArm,rightForearm,rightHand,leftArm,leftForearm,leftHand;
            readonly Transform rightThigh,rightShin,rightFoot,leftThigh,leftShin,leftFoot;
            readonly Transform[] joints;
            readonly Quaternion[] before;
            bool applied;
            public bool Available=>head&&rightArm&&rightForearm&&rightHand&&leftArm&&leftForearm&&leftHand&&
                rightThigh&&rightShin&&rightFoot&&leftThigh&&leftShin&&leftFoot;
            public Vector3 HeadPosition=>head?head.position:Vector3.zero;
            public CyclopseStumblePose(Transform model)
            {
                var bones=model.GetComponentsInChildren<Transform>();
                Transform Find(string suffix)=>System.Array.Find(bones,bone=>bone.name.EndsWith(suffix,System.StringComparison.Ordinal));
                head=Find("Head");rightArm=Find("RightArm");rightForearm=Find("RightForeArm");rightHand=Find("RightHand");
                leftArm=Find("LeftArm");leftForearm=Find("LeftForeArm");leftHand=Find("LeftHand");
                rightThigh=Find("RightUpLeg");rightShin=Find("RightLeg");rightFoot=Find("RightFoot");
                leftThigh=Find("LeftUpLeg");leftShin=Find("LeftLeg");leftFoot=Find("LeftFoot");
                var valid=new List<Transform>();
                foreach(var joint in new[]{rightArm,rightForearm,leftArm,leftForearm,rightThigh,rightShin,leftThigh,leftShin})
                    if(joint)valid.Add(joint);
                joints=valid.ToArray();before=new Quaternion[joints.Length];
            }
            public void Reset()
            {
                if(!applied)return;
                for(int i=0;i<joints.Length;i++)if(joints[i])joints[i].localRotation=before[i];
                applied=false;
            }
            static void Aim(Transform joint,Transform child,Vector3 direction,float weight,float maxAngle)
            {
                if(!joint||!child||weight<=0||direction.sqrMagnitude<.001f)return;
                var target=Quaternion.FromToRotation(child.position-joint.position,direction)*joint.rotation;
                joint.rotation=Quaternion.Slerp(joint.rotation,Quaternion.RotateTowards(joint.rotation,target,maxAngle),weight);
            }
            public void Apply(Vector3 forward,Vector3 right,float fall,float kneel,float brace)
            {
                if(!Available)return;
                for(int i=0;i<joints.Length;i++)before[i]=joints[i].localRotation;
                applied=true;
                var up=Vector3.up;
                // Cyclopse's upper-arm weights extend into its broad chest. Keep
                // shoulders compact and bend the forearms for the floor brace.
                Aim(rightArm,rightForearm,forward*.65f+right*.25f-up*.45f,brace,18);
                Aim(leftArm,leftForearm,forward*.65f-right*.25f-up*.45f,brace,18);
                Aim(rightForearm,rightHand,forward*.35f-up*.9f,brace,38);
                Aim(leftForearm,leftHand,forward*.35f-up*.9f,brace,38);
                float knee=Mathf.Clamp01(kneel+fall*.24f);
                Aim(rightThigh,rightShin,forward*.55f-up*.55f,knee,28);
                Aim(leftThigh,leftShin,forward*.4f-up*.65f,knee*.8f,24);
                Aim(rightShin,rightFoot,-forward*.5f-up*.35f,knee,58);
                Aim(leftShin,leftFoot,-forward*.55f-up*.3f,knee*.8f,50);
            }
        }
    }
}
