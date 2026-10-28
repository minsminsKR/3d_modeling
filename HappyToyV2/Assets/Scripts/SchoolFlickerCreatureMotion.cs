using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Poses the actual 52-joint AI rig from the passage clock; no autonomous time.
    public sealed class SchoolFlickerCreatureMotion
    {
        readonly Transform[] joints;
        readonly Quaternion[] rest;
        public SchoolFlickerCreatureMotion(Transform visual)
        {
            var all=visual.GetComponentsInChildren<Transform>(true);
            string[] names={"bone_0","bone_3","bone_5","bone_7","bone_8","bone_26","bone_27","bone_44","bone_45","bone_48","bone_49"};
            joints=names.Select(name=>all.SingleOrDefault(t=>t.name==name)??throw new InvalidOperationException("Creature joint missing: "+name)).ToArray();
            rest=joints.Select(t=>t.localRotation).ToArray();
        }
        public void Apply(double seconds)
        {
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=rest[i];
            double phase=SchoolWalkerFlicker.Pose(seconds);int index=(int)Math.Floor(phase);float blend=(float)(phase-index);
            float Wave(float k)=>Mathf.Lerp(Mathf.Sin(index*k+.8f),Mathf.Sin((index+1)*k+.8f),blend);
            float side=Wave(1.71f),curl=Wave(2.39f),leg=Wave(2.83f);
            float tremor=Mathf.Sin((float)(seconds%100)*33.7f)+.45f*Mathf.Sin((float)(seconds%100)*47.3f);
            joints[0].localRotation*=Quaternion.Euler(4+6*curl,9*side,5*side);
            joints[1].localRotation*=Quaternion.Euler(5+8*curl,-8*side,8*side+tremor);
            joints[2].localRotation*=Quaternion.Euler(-5+7*curl,6*side,8*side+1.5f*tremor);
            joints[3].localRotation*=Quaternion.Euler(9*curl,7*side,13*side);
            joints[4].localRotation*=Quaternion.Euler(16+12*curl,0,0);
            joints[5].localRotation*=Quaternion.Euler(-9*curl,-7*side,-13*side);
            joints[6].localRotation*=Quaternion.Euler(16-12*curl,0,0);
            joints[7].localRotation*=Quaternion.Euler(8*leg,0,0);
            joints[8].localRotation*=Quaternion.Euler(-8*Mathf.Max(0,leg),0,0);
            joints[9].localRotation*=Quaternion.Euler(-8*leg,0,0);
            joints[10].localRotation*=Quaternion.Euler(-8*Mathf.Max(0,-leg),0,0);
        }
    }
}
