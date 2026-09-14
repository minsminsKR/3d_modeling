using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Authored additive reach on the original V1 skeleton. Never edits the imported clips/FBXs.
    public sealed class V1AttackPose
    {
        readonly Transform arm,forearm,hand,leftArm,leftForearm,spine;
        readonly Transform[] joints;
        Quaternion[] before;
        readonly bool broadBody;
        public float Weight {get;private set;}
        public bool Available=>arm&&forearm&&hand;
        public Vector3 HandPosition=>hand?hand.position:Vector3.zero;
        public V1AttackPose(Transform model)
        {
            var bones=model.GetComponentsInChildren<Transform>();
            broadBody=model.parent&&model.parent.name.Contains("Cyclopse");
            Transform Find(string suffix)=>bones.FirstOrDefault(t=>t.name.EndsWith(suffix,System.StringComparison.Ordinal));
            arm=Find("RightArm");forearm=Find("RightForeArm");hand=Find("RightHand");
            leftArm=Find("LeftArm");leftForearm=Find("LeftForeArm");spine=Find("Spine1");
            joints=new[]{spine,arm,forearm,leftArm}.Where(t=>t).ToArray();before=new Quaternion[joints.Length];
        }
        bool applied;
        public void Reset()
        {
            if(!applied)return;for(int i=0;i<joints.Length;i++)joints[i].localRotation=before[i];applied=false;Weight=0;
        }
        static void Aim(Transform bone,Transform child,Vector3 direction,float weight,float maxAngle)
        {
            if(!bone||!child)return;
            var rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation;
            bone.rotation=Quaternion.Slerp(bone.rotation,Quaternion.RotateTowards(bone.rotation,rotation,maxAngle),weight);
        }
        public void Apply(StalkerBrain brain,Transform actor)
        {
            if(!Available||!brain.AttackActive)return;
            for(int i=0;i<joints.Length;i++)before[i]=joints[i].localRotation;applied=true;
            float windup=brain.AttackWindup,recovery=brain.AttackRecovery;
            Weight=recovery>0?Mathf.SmoothStep(0,1,recovery):Mathf.SmoothStep(0,1,windup/.45f);
            // Pull back early; reach during the final 0.18s so the strike pose meets the hit time.
            float reach=recovery>0?1:Mathf.SmoothStep(0,1,(windup-.76f)/.24f);
            var forward=actor.forward;var right=actor.right;var up=Vector3.up;
            // Cyclopse's shoulder weights extend into its broad torso: use a compact reach,
            // not a humanoid overhead windup that tears the original silhouette.
            if(spine&&!broadBody)spine.rotation=Quaternion.AngleAxis(Mathf.Lerp(-6,6,reach)*Weight,up)*spine.rotation;
            Aim(arm,forearm,Vector3.Lerp(right*.65f+up*.6f-forward*.5f,forward+right*.12f-up*.08f,reach),Weight,broadBody?18:50);
            Aim(forearm,hand,Vector3.Lerp(up*.8f+forward*.35f,forward-up*.1f,reach),Weight,broadBody?30:65);
            Aim(leftArm,leftForearm,-right*.35f+forward*.35f-up*.6f,Weight*.55f,broadBody?10:25);
        }
    }
}
