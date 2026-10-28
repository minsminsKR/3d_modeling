using System;

namespace HappyToy.V2
{
    // Four reveals per second on average, with uneven intervals. Elapsed time
    // remains the complete checkpoint state; dark boundaries drive pose and step.
    public static class SchoolWalkerFlicker
    {
        public const double CycleSeconds=1.0;
        static readonly double[] starts={0,.20,.49,.77};
        static readonly double[] ends={.09,.29,.58,.86};
        const double BoundaryTolerance=1e-10;

        static void Validate(double elapsed)
        {if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentException("Finite nonnegative flicker time required");}
        static void Split(double elapsed,out long cycles,out double phase)
        {
            Validate(elapsed);cycles=(long)Math.Floor((elapsed+BoundaryTolerance)/CycleSeconds);
            phase=Math.Max(0,elapsed-cycles*CycleSeconds);
        }
        public static bool Dark(double elapsed)
        {
            Split(elapsed,out _,out double phase);
            for(int i=0;i<starts.Length;i++)
                if(phase+BoundaryTolerance>=starts[i]&&phase+BoundaryTolerance<ends[i])return false;
            return true;
        }
        // Count dark-onset boundaries, including an exact boundary.
        public static long FlashIndex(double elapsed)
        {
            Split(elapsed,out long cycles,out double phase);long count=cycles*starts.Length;
            for(int i=0;i<ends.Length;i++)if(phase+BoundaryTolerance>=ends[i])count++;else break;
            return count;
        }
        // Legacy pedestrian pose uses a fractional transition only while dark;
        // the creature itself selects a discrete pose with FlashIndex.
        public static double Pose(double elapsed)
        {
            Split(elapsed,out long cycles,out double phase);double pose=cycles*starts.Length;
            for(int i=0;i<starts.Length;i++)
            {
                if(phase<ends[i])break;
                double next=i+1<starts.Length?starts[i+1]:CycleSeconds;
                pose+=Math.Min(1,(phase-ends[i])/(next-ends[i]));
                if(phase<next)break;
            }
            return pose;
        }
        public static double MovingSeconds(double start,double duration)
        {
            Validate(start);Validate(duration);return Accumulated(start+duration)-Accumulated(start);
        }
        static double Accumulated(double elapsed)
        {
            Split(elapsed,out long cycles,out double phase);double darkPerCycle=CycleSeconds,result=0;
            for(int i=0;i<starts.Length;i++)
            {
                darkPerCycle-=ends[i]-starts[i];
                double next=i+1<starts.Length?starts[i+1]:CycleSeconds;
                result+=Math.Max(0,Math.Min(phase,next)-ends[i]);
            }
            return cycles*darkPerCycle+result;
        }
    }
}
