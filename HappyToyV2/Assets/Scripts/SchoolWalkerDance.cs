using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Additive, reproducible poses on the original service-generated rig.
    // Each pose is held under steady red light; tiny upper-body tremors never
    // produce a walk cycle. The legs and arms change only during dark pulses.
    public sealed class SchoolWalkerDance
    {
        readonly Transform visual,hips,chest,head,leftArm,rightArm,leftForearm,rightForearm,leftHand,rightHand;
        readonly Transform leftThigh,rightThigh,leftKnee,rightKnee;
        public SchoolWalkerDance(Transform visual)
        {
            this.visual=visual;
            var bones=visual.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name)=>bones.SingleOrDefault(b=>b.name==name)??throw new InvalidOperationException("School anomaly joint missing: "+name);
            hips=Bone("bone_0");chest=Bone("bone_3");head=Bone("bone_5");
            rightArm=Bone("bone_7");rightForearm=Bone("bone_8");leftArm=Bone("bone_17");leftForearm=Bone("bone_18");
            rightHand=Bone("bone_9");leftHand=Bone("bone_19");
            rightThigh=Bone("bone_26");rightKnee=Bone("bone_27");leftThigh=Bone("bone_30");leftKnee=Bone("bone_31");
        }
        public void Apply(double seconds)
        {
            double phase=SchoolWalkerFlicker.Pose(seconds);
            int current=(int)Math.Floor(phase),next=current+1;float blend=(float)(phase-current);
            float Wave(int index,float seed)=>Mathf.Sin((index%10000)*seed+.8f);
            float Value(float seed)=>Mathf.Lerp(Wave(current,seed),Wave(next,seed),blend);
            float side=Value(1.71f),curl=Value(2.39f),tilt=Value(1.13f),leg=Value(2.83f);
            float tremor=Mathf.Sin((float)(seconds%100)*33.7f)+.45f*Mathf.Sin((float)(seconds%100)*47.3f);
            hips.localRotation*=Quaternion.Euler(5+7*tilt,14*side,8+11*tilt);
            chest.localRotation*=Quaternion.Euler(8+10*curl,-15*side,-15-9*tilt+1.1f*tremor);
            head.localRotation*=Quaternion.Euler(-10+9*curl,9*side,19+8*tilt+1.7f*tremor);
            Aim(rightArm,rightForearm,new Vector3(.8f,.55f+.25f*side,.18f+.25f*curl));
            Aim(leftArm,leftForearm,new Vector3(-.8f,.35f-.25f*side,-.2f+.2f*tilt));
            Aim(rightForearm,rightHand,new Vector3(.15f,1,.25f+.35f*curl));
            Aim(leftForearm,leftHand,new Vector3(-.2f,.7f,-.35f+.35f*tilt));
            // Controlled asymmetric bends, applied atop the same neutral bind
            // pose. No skeleton, skin or model geometry is replaced.
            rightThigh.localRotation*=Quaternion.Euler(8+12*leg,3*side,4*tilt);
            rightKnee.localRotation*=Quaternion.Euler(-10-12*Mathf.Max(0,leg),0,0);
            leftThigh.localRotation*=Quaternion.Euler(5-11*leg,-3*side,-4*tilt);
            leftKnee.localRotation*=Quaternion.Euler(-8-12*Mathf.Max(0,-leg),0,0);
        }
        void Aim(Transform joint,Transform end,Vector3 direction)
            =>joint.rotation=Quaternion.FromToRotation(end.position-joint.position,visual.TransformDirection(direction))*joint.rotation;
    }
}
