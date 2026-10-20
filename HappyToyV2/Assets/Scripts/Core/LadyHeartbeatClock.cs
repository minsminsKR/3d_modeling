using System;

namespace HappyToy.V2
{
    // Player perception runs in real time, independent of Lady's frozen pose.
    // The caller supplies pause state and measured distance; no scene dependency.
    public sealed class LadyHeartbeatClock
    {
        public const float CalmBpm=58,MaximumBpm=168;
        float age,phase=.75f,closeness;
        public float Bpm {get;private set;}=CalmBpm;
        public float Gain {get;private set;}
        public int Beats {get;private set;}
        public bool Stopped {get;private set;}
        public bool Tick(float seconds,float distance,bool playing)
        {
            if(Stopped||!playing||!StealthRules.Finite(seconds)||seconds<=0)return false;
            if(!StealthRules.Finite(distance)||distance<0)distance=32;
            float near=Math.Max(0,Math.Min(1,(32-distance)/30));
            near=near*near*(3-2*near);
            float blend=(float)(1-Math.Exp(-seconds*2.4));
            closeness+=(near-closeness)*blend;Bpm=CalmBpm+(MaximumBpm-CalmBpm)*closeness;
            age+=seconds;Gain=(.06f+.28f*closeness)*Math.Min(1,age/1.8f);
            phase+=seconds*Bpm/60;
            if(phase<1)return false;
            phase%=1;Beats++;return true; // Never burst missed beats after a stalled frame.
        }
        public void Stop(){Stopped=true;Gain=0;}
    }
}
