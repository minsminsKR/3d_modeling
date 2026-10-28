using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Adds discrete high-frequency convulsions on top of the calibrated pose.
    // The owner reapplies the base pose first, so there is no accumulated drift.
    public sealed class SchoolFlickerCreatureTremor
    {
        readonly Transform[] joints;
        readonly Transform leftFoot,rightFoot,headRoot,headTip;
        readonly Quaternion leftFootRest,rightFootRest;
        readonly Vector3 headRootRestPosition,headTipRestPosition;
        static readonly float[] Degrees={.6f,1.2f,1.6f,2.4f,2.8f,2.2f,2.5f,2.8f,2.9f,2.2f,2.5f,2.8f,2.9f,1.5f,1.8f,1.5f,1.8f};
        const double StepsPerSecond=14;
        public SchoolFlickerCreatureTremor(Transform visual)
        {
            if(!visual)throw new ArgumentNullException(nameof(visual));
            var all=visual.GetComponentsInChildren<Transform>(true);
            Transform Joint(string name)=>all.SingleOrDefault(t=>t.name==name)??throw new InvalidOperationException("Creature tremor joint missing: "+name);
            string[] names={"bone_0","bone_1","bone_3","bone_4","bone_5","bone_6","bone_7","bone_8","bone_9","bone_25","bone_26","bone_27","bone_28","bone_44","bone_45","bone_48","bone_49"};
            joints=names.Select(Joint).ToArray();leftFoot=Joint("bone_46");rightFoot=Joint("bone_50");
            headRoot=Joint("bone_4");headTip=Joint("bone_5");
            leftFootRest=leftFoot.localRotation;rightFootRest=rightFoot.localRotation;
            headRootRestPosition=headRoot.localPosition;headTipRestPosition=headTip.localPosition;
        }
        static float Signed(long step,int joint,int axis)
        {
            unchecked
            {
                uint x=(uint)step*747796405u+(uint)joint*2891336453u+(uint)axis*277803737u+0x682bc21du;
                x^=x>>16;x*=2246822519u;x^=x>>13;x*=3266489917u;x^=x>>16;
                return (x%20001)/10000f-1f;
            }
        }
        struct Anchor
        {
            readonly Transform joint;readonly Vector3 position;readonly Quaternion rotation;
            public Anchor(Transform joint){this.joint=joint;position=joint.position;rotation=joint.rotation;}
            public void Restore(){joint.SetPositionAndRotation(position,rotation);}
        }
        // Input time is the encounter simulation time, which does not advance
        // during pause or talisman time stop. The unlit pose remains untouched.
        public void Apply(double seconds,bool inverted)
        {
            // ApplyState resets ankle positions, but neither ankle rotations
            // nor head positions. Restore those on every sample, even in darkness.
            leftFoot.localRotation=leftFootRest;rightFoot.localRotation=rightFootRest;
            headRoot.localPosition=headRootRestPosition;headTip.localPosition=headTipRestPosition;
            if(SchoolWalkerFlicker.Dark(seconds))return;
            var a=new Anchor(inverted?headRoot:leftFoot);
            var b=new Anchor(inverted?headTip:rightFoot);
            long step=(long)Math.Floor(Math.Max(0,seconds)*StepsPerSecond);
            for(int i=0;i<joints.Length;i++)
            {
                // Keep the previous restrained unease without making the lit
                // silhouette read as continuously moving between flash poses.
                float amount=Degrees[i]*.25f;
                joints[i].localRotation*=Quaternion.Euler(Signed(step,i,0)*amount,Signed(step,i,1)*amount,Signed(step,i,2)*amount);
            }
            // Restore both contact joints after deforming their parents. For an
            // inverted body, keep both head bones anchored; otherwise pin soles.
            a.Restore();b.Restore();
        }
    }
}
