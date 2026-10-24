using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Samples the authored in-place walk using real travelled metres. It owns
    // neither movement nor an automatic animator clock, so pause holds exactly.
    [DisallowMultipleComponent]
    public sealed class SchoolWalkerMotion : MonoBehaviour
    {
        Transform[] bones;
        Vector3[] contactPosition,walkPosition;
        Quaternion[] contactRotation,walkRotation;
        Vector3[] contactScale,walkScale;
        GameObject visual;
        AnimationClip walk,idle;
        float stride;
        public float Clock {get;private set;}
        public float IdleClock {get;private set;}
        public float IdleBlend {get;private set;}
        public AnimationClip WalkClip=>walk;
        public AnimationClip IdleClip=>idle;
        public Transform Visual=>visual?visual.transform:null;
        public void Prepare(GameObject model,AnimationClip walking,AnimationClip resting,float metresPerCycle)
        {
            if(!model||!walking||walking.length<=.05f||float.IsNaN(metresPerCycle)||float.IsInfinity(metresPerCycle)||metresPerCycle<=.1f)
                throw new ArgumentException("A rigged school walker needs its authored in-place walk and stride");
            visual=model;walk=walking;idle=resting;stride=metresPerCycle;
            foreach(var automatic in visual.GetComponentsInChildren<Animation>(true))automatic.enabled=false;
            foreach(var automatic in visual.GetComponentsInChildren<Animator>(true))automatic.enabled=false;
            if(idle&&idle.length>.05f)idle.SampleAnimation(visual,0);else walk.SampleAnimation(visual,0);
            bones=visual.GetComponentsInChildren<Transform>(true);
            contactPosition=bones.Select(b=>b.localPosition).ToArray();contactRotation=bones.Select(b=>b.localRotation).ToArray();
            contactScale=bones.Select(b=>b.localScale).ToArray();
            walkPosition=new Vector3[bones.Length];walkRotation=new Quaternion[bones.Length];walkScale=new Vector3[bones.Length];
        }
        public void Apply(SchoolWalkerRoute.State state)
        {
            if(!visual||!walk)return;
            Clock=(float)(state.Distance/stride*walk.length);IdleClock=(float)state.IdleElapsed;IdleBlend=state.IdleBlend;
            for(int i=0;i<bones.Length;i++)
            {bones[i].localPosition=contactPosition[i];bones[i].localRotation=contactRotation[i];bones[i].localScale=contactScale[i];}
            walk.SampleAnimation(visual,Mathf.Repeat(Clock,walk.length));
            if(IdleBlend<=0)return;
            if(idle&&idle.length>.05f)
            {
                for(int i=0;i<bones.Length;i++)
                {
                    walkPosition[i]=bones[i].localPosition;walkRotation[i]=bones[i].localRotation;walkScale[i]=bones[i].localScale;
                    bones[i].localPosition=contactPosition[i];bones[i].localRotation=contactRotation[i];bones[i].localScale=contactScale[i];
                }
                idle.SampleAnimation(visual,Mathf.Repeat(IdleClock,idle.length));
                for(int i=0;i<bones.Length;i++)
                {
                    bones[i].localPosition=Vector3.Lerp(walkPosition[i],bones[i].localPosition,IdleBlend);
                    bones[i].localRotation=Quaternion.Slerp(walkRotation[i],bones[i].localRotation,IdleBlend);
                    bones[i].localScale=Vector3.Lerp(walkScale[i],bones[i].localScale,IdleBlend);
                }
            }
            else for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(bones[i].localPosition,contactPosition[i],IdleBlend);
                bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,contactRotation[i],IdleBlend);
                bones[i].localScale=Vector3.Lerp(bones[i].localScale,contactScale[i],IdleBlend);
            }
        }
    }
}
