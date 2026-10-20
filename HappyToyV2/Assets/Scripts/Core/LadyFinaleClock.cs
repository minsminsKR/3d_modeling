using System;

namespace HappyToy.V2
{
    // Progression only: no session, scene, navigation or audio dependencies.
    public sealed class LadyFinaleClock
    {
        public const float SilenceSeconds=.9f;
        public int Phase {get;private set;}
        public float Remaining {get;private set;}
        public bool Active=>Phase!=0;
        public bool Hunting=>Phase==2;
        public bool Begin(int recovered,int required)
        {
            if(Active||required<=0||recovered<required)return false;
            Phase=1;Remaining=SilenceSeconds;return true;
        }
        public bool Tick(float seconds,bool playing,bool timeStopped)
        {
            if(Phase!=1||!playing||timeStopped||!StealthRules.Finite(seconds)||seconds<=0)return false;
            Remaining=Math.Max(0,Remaining-seconds);
            if(Remaining>0)return false;
            Phase=2;return true;
        }
        public void Restore(int phase,float remaining)
        {
            if(phase<0||phase>2||!StealthRules.Finite(remaining)||remaining<0||remaining>SilenceSeconds||
                (phase!=1&&remaining!=0))throw new ArgumentException("Invalid Lady finale clock");
            Phase=phase;Remaining=remaining;
        }
    }
}
