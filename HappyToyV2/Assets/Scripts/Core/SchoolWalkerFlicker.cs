using System;

namespace HappyToy.V2
{
    // A restorable timeline: brief irregular red reveals alternate with longer
    // darkness. Pose and displacement change at each dark onset, so the next
    // reveal shows the completed step rather than the movement itself.
    public static class SchoolWalkerFlicker
    {
        public const double CycleSeconds=7.3;
        static readonly double[] starts={.12,.75,1.21,1.75,2.21,2.89,3.48,
            4.02,4.55,5.19,5.80,6.37,7.02};
        static readonly double[] ends={.64,1.12,1.65,2.12,2.78,3.38,3.91,
            4.45,5.10,5.70,6.27,6.91,7.30};

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
