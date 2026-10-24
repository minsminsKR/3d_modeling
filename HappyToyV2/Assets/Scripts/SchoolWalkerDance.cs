using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Authored additive choreography for the existing 34-bone rig. The locomotion
    // sampler restores each base pose before these offsets, so they never accumulate.
    public sealed class SchoolWalkerDance
    {
        readonly Transform visual,hips,chest,head,leftArm,rightArm,leftForearm,rightForearm,leftHand,rightHand;
        public SchoolWalkerDance(Transform visual)
        {
            this.visual=visual;
            var bones=visual.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name)=>bones.SingleOrDefault(b=>b.name==name)??throw new InvalidOperationException("School dance joint missing: "+name);
            hips=Bone("bone_0");chest=Bone("bone_3");head=Bone("bone_5");
            rightArm=Bone("bone_7");rightForearm=Bone("bone_8");leftArm=Bone("bone_17");leftForearm=Bone("bone_18");
            rightHand=Bone("bone_9");leftHand=Bone("bone_19");
        }
        public void Apply(float seconds)
        {
            float beat=seconds*Mathf.PI*2*1.65f,wave=Mathf.Sin(beat),counter=Mathf.Cos(beat);
            hips.localPosition+=new Vector3(.055f*wave,.025f*(1-Mathf.Cos(beat*2)),0);
            hips.localRotation*=Quaternion.Euler(0,14*wave,9*wave);
            chest.localRotation*=Quaternion.Euler(7*counter,-20*wave,-11*wave);
            head.localRotation*=Quaternion.Euler(-7*counter,10*wave,7*counter);
            // Generic service bones do not share a humanoid local twist axis.
            // Aim the actual limb segments in model space so both hands visibly
            // rise above the shoulders, rather than merely rolling the wrists.
            Aim(rightArm,rightForearm,new Vector3(.65f,.75f+.4f*wave,.3f*counter));
            Aim(leftArm,leftForearm,new Vector3(-.65f,.75f-.4f*wave,-.3f*counter));
            Aim(rightForearm,rightHand,new Vector3(.18f,1,.45f*wave));
            Aim(leftForearm,leftHand,new Vector3(-.18f,1,-.45f*wave));
        }
        void Aim(Transform joint,Transform end,Vector3 direction)
            =>joint.rotation=Quaternion.FromToRotation(end.position-joint.position,visual.TransformDirection(direction))*joint.rotation;
    }
}
