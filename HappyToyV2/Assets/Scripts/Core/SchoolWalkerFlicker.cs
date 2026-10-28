using System;

namespace HappyToy.V2
{
    // Four fixed red reveals per simulation second. A short lit frame is
    // followed by darkness; the actor snaps once at the dark boundary.
    // Elapsed simulation time is the only state, so older checkpoints remain valid.
    public static class SchoolWalkerFlicker
    {
        public const double CycleSeconds=.25;
        public const double LitSeconds=.09;
        const double BoundaryTolerance=1e-10;

        public static bool Dark(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentException("Finite nonnegative flicker time required");
            double phase=Phase(elapsed);
            return phase+BoundaryTolerance>=LitSeconds;
        }
        // Count dark-onset boundaries, including one exactly at elapsed.
        // This shared index drives both the instantaneous pose and route step.
        public static long FlashIndex(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentException("Finite nonnegative flicker time required");
            long cycles=(long)Math.Floor((elapsed+BoundaryTolerance)/CycleSeconds);
            double phase=elapsed-cycles*CycleSeconds;
            return cycles+(phase+BoundaryTolerance>=LitSeconds?1:0);
        }
        // The integer part selects an asymmetrical fixed pose. During darkness
        // the fractional part evolves; the next reveal holds the new pose.
        public static double Pose(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentException("Finite nonnegative flicker time required");
            long cycles=(long)Math.Floor((elapsed+BoundaryTolerance)/CycleSeconds);
            double phase=elapsed-cycles*CycleSeconds;
            return cycles+Math.Max(0,Math.Min(1,(phase-LitSeconds)/(CycleSeconds-LitSeconds)));
        }
        public static double MovingSeconds(double start,double duration)
        {
            if(double.IsNaN(start)||double.IsInfinity(start)||start<0||double.IsNaN(duration)||double.IsInfinity(duration)||duration<0)
                throw new ArgumentException("Finite nonnegative flicker interval required");
            return Accumulated(start+duration)-Accumulated(start);
        }
        static double Accumulated(double elapsed)
        {
            double cycles=Math.Floor(elapsed/CycleSeconds),phase=elapsed-cycles*CycleSeconds;
            return cycles*(CycleSeconds-LitSeconds)+Math.Max(0,phase-LitSeconds);
        }
        static double Phase(double elapsed)
        {
            double cycles=Math.Floor((elapsed+BoundaryTolerance)/CycleSeconds);
            return Math.Max(0,elapsed-cycles*CycleSeconds);
        }
    }
}
