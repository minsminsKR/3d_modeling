using System;

namespace HappyToy.V2
{
    // Simulation-time event deadlines; no camera, engine, sound or scene dependency.
    public sealed class SchoolAnomalyEventClock
    {
        public const double RevealSeconds=2.2,BabyWaitSeconds=60,BlackoutInterval=1,DarknessSeconds=5;
        public const int HallLights=9;
        [Serializable] public struct State {public bool Triggered,Pursuing;public double Elapsed;}
        State state;
        public State Capture()=>state;
        public bool Triggered=>state.Triggered;
        public bool Pursuing=>state.Pursuing;
        public double Elapsed=>state.Elapsed;
        public int Extinguished=>state.Triggered?Math.Min(HallLights,(int)(state.Elapsed/BlackoutInterval)):0;
        public bool DarknessExpired=>state.Triggered&&state.Elapsed>=HallLights*BlackoutInterval+DarknessSeconds;
        public void Reset(){state=default;}
        public void Restore(State value,SchoolAnomalyKind kind){Validate(value,kind);state=value;}
        public static void Validate(State value,SchoolAnomalyKind kind)
        {
            if(double.IsNaN(value.Elapsed)||double.IsInfinity(value.Elapsed)||value.Elapsed<0||value.Elapsed>1000000||
                !value.Triggered&&(value.Elapsed!=0||value.Pursuing)||
                value.Triggered&&kind!=SchoolAnomalyKind.Baby&&kind!=SchoolAnomalyKind.Blackout||
                value.Pursuing&&(kind!=SchoolAnomalyKind.Baby||value.Elapsed<RevealSeconds))
                throw new ArgumentException("Invalid school anomaly event clock");
        }
        public void Step(double seconds,bool advancing,bool atStudySign,bool closeToBaby,SchoolAnomalyKind kind)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!advancing||kind!=SchoolAnomalyKind.Baby&&kind!=SchoolAnomalyKind.Blackout)return;
            if(!state.Triggered&&!atStudySign)return;
            state.Triggered=true;state.Elapsed=Math.Min(1000000,state.Elapsed+seconds);
            if(kind==SchoolAnomalyKind.Baby&&state.Elapsed>=RevealSeconds&&(closeToBaby||state.Elapsed>=BabyWaitSeconds))state.Pursuing=true;
        }
    }
}
