using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // The authored action contains separate head/neck/shoulder/elbow/wrist/knee
    // channels. Sampling never accumulates transforms or advances during a hold.
    [DefaultExecutionOrder(100),DisallowMultipleComponent]
    public sealed class LadyMotion : MonoBehaviour
    {
        AnimationClip clip;GameObject visual;Transform[] bones;Quaternion[] neutral;Vector3[] neutralPosition;
        Func<bool> canAdvance;Func<bool> comfort;
        public float Clock {get;private set;}
        public Transform Head {get;private set;}
        public Transform Visual=>visual?visual.transform:null;
        public void Prepare(GameObject model,Func<bool> playing,Func<bool> reducedMotion)
        {
            visual=model;canAdvance=playing;comfort=reducedMotion;
            clip=Resources.Load<AnimationClip>("Lady/chase");
            if(!clip)throw new InvalidOperationException("Lady chase action is missing");
            clip.SampleAnimation(visual,0);
            bones=visual.GetComponentsInChildren<Transform>(true);neutral=bones.Select(bone=>bone.localRotation).ToArray();
            neutralPosition=bones.Select(bone=>bone.localPosition).ToArray();
            Head=bones.Single(bone=>bone.name=="Head");
        }
        public void RestoreClock(float time){Clock=Mathf.Max(0,time);Sample();}
        void LateUpdate()
        {
            if(!visual||!clip||!canAdvance()||EnemyTimeStop.Active)return;
            Clock+=Time.deltaTime;Sample();
        }
        void Sample()
        {
            if(!clip||!visual)return;
            clip.SampleAnimation(visual,Mathf.Repeat(Clock,clip.length));
            if(comfort!=null&&comfort())for(int i=0;i<bones.Length;i++)
            {
                bones[i].localRotation=Quaternion.Slerp(neutral[i],bones[i].localRotation,.18f);
                // The authored support offset follows the same reduced-motion
                // strength as the contortions that caused the foot lift.
                bones[i].localPosition=Vector3.Lerp(neutralPosition[i],bones[i].localPosition,.18f);
            }
        }
    }
}
