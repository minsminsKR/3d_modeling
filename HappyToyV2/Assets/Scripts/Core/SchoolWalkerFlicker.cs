using System;

namespace HappyToy.V2
{
    // A restorable timeline: long red holds alternate with irregular dark
    // pulses. Pose and displacement advance only during the same dark pulses.
    public static class SchoolWalkerFlicker
    {
        // Every full red hold is exactly three simulation seconds; the brief
        // irregular dark pulses conceal each pose/position snap.
        public const double LitSeconds=3.0,CycleSeconds=40.18;
        static readonly double[] starts={3.00,6.09,9.16,12.25,15.34,18.43,21.51,24.60,
            27.68,30.76,33.84,36.93,40.04};
        static readonly double[] ends={3.09,6.16,9.25,12.34,15.43,18.51,21.60,24.68,
            27.76,30.84,33.93,37.04,40.18};

        public static bool Dark(double elapsed)
        {
            double phase=Phase(elapsed);
            for(int i=0;i<starts.Length;i++)if(phase>=starts[i]&&phase<ends[i])return true;
            return false;
        }
        // Count dark-onset boundaries, including one exactly at elapsed.
        // The same count drives both the instantaneous pose and route jump.
        public static long FlashIndex(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentException("Finite nonnegative flicker time required");
            long cycles=(long)Math.Floor(elapsed/CycleSeconds);double phase=Phase(elapsed);
            long count=cycles*starts.Length;for(int i=0;i<starts.Length;i++)if(phase>=starts[i])count++;else break;
            return count;
        }
        // The integer part selects an asymmetrical fixed pose. Its fractional
        // part changes behind the flash, so each lit frame holds the new pose.
        public static double Pose(double elapsed)
        {
            double cycles=Math.Floor(Math.Max(0,elapsed)/CycleSeconds),phase=Phase(elapsed);
            double pose=cycles*starts.Length;
            for(int i=0;i<starts.Length;i++)
            {
                if(phase<starts[i])break;
                pose+=Math.Min(1,(phase-starts[i])/(ends[i]-starts[i]));
                if(phase<ends[i])break;
            }
            return pose;
        }
        public static double MovingSeconds(double start,double duration)
        {
            if(double.IsNaN(start)||double.IsInfinity(start)||start<0||double.IsNaN(duration)||double.IsInfinity(duration)||duration<0)
                throw new ArgumentException("Finite nonnegative flicker interval required");
            return Accumulated(start+duration)-Accumulated(start);
        }
        static double Accumulated(double elapsed)
        {
            double cycles=Math.Floor(elapsed/CycleSeconds),phase=Phase(elapsed),perCycle=0,result=0;
            for(int i=0;i<starts.Length;i++)
            {perCycle+=ends[i]-starts[i];result+=Math.Max(0,Math.Min(phase,ends[i])-starts[i]);}
            return cycles*perCycle+result;
        }
        static double Phase(double elapsed)=>Math.Max(0,elapsed)%CycleSeconds;
    }
}
