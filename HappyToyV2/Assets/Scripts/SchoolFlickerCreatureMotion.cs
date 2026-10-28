using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // One deterministic held pose per dark onset. No pose interpolation or clock.
    public sealed class SchoolFlickerCreatureMotion
    {
        readonly Transform[] joints;
        readonly Quaternion[] rest;
        static readonly float[] ranges={14,14,18,48,65,24,34,85,110,24,34,85,110,14,32,14,32};
        // Measured on the actual 3.9m rig against the 4.4m by 3.25m hall.
        // Each seed is a different abrupt contortion; no interpolation occurs.
        static readonly int[] fittedSeeds={3,16,36,47,55,59,66,70,72,74,75,83,86,102,105,106};
        public SchoolFlickerCreatureMotion(Transform visual)
        {
            var all=visual.GetComponentsInChildren<Transform>(true);
            string[] names={"bone_0","bone_1","bone_3","bone_4","bone_5","bone_6","bone_7","bone_8","bone_9","bone_25","bone_26","bone_27","bone_28","bone_44","bone_45","bone_48","bone_49"};
            joints=names.Select(name=>all.SingleOrDefault(t=>t.name==name)??throw new InvalidOperationException("Creature joint missing: "+name)).ToArray();
            rest=joints.Select(t=>t.localRotation).ToArray();
        }
        static float Signed(long pose,int joint,int axis)
        {
            unchecked{uint x=(uint)pose*747796405u+(uint)joint*2891336453u+(uint)axis*277803737u+1013904223u;
                x^=x>>16;x*=2246822519u;x^=x>>13;x*=3266489917u;x^=x>>16;return (x%20001)/10000f-1f;}
        }
        public void Apply(double seconds)
        {
            ApplyPoseIndex(fittedSeeds[SchoolWalkerFlicker.FlashIndex(seconds)%fittedSeeds.Length]);
        }
        public void ApplyPoseIndex(long pose)
        {
            for(int i=0;i<joints.Length;i++)
            {
                // Large discontinuous contortions on the actual rig; each
                // pose is held unchanged until the next light-off boundary.
                // Restrict the trunk and shoulder swing enough to fit the
                // narrow school while retaining extreme head/wrist/elbow snaps.
                float range=ranges[i];
                joints[i].localRotation=rest[i]*Quaternion.Euler(Signed(pose,i,0)*range,Signed(pose,i,1)*range,Signed(pose,i,2)*range);
            }
        }
    }
}
