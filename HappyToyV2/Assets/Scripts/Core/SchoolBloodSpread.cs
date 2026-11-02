using System;

namespace HappyToy.V2
{
    // Simulation time controls the spread; no extra save field is necessary.
    public static class SchoolBloodSpread
    {
        public const float FullSeconds=30f;
        public static float Progress(float elapsed)
        {
            if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0)throw new ArgumentOutOfRangeException(nameof(elapsed));
            return Math.Min(1,elapsed/FullSeconds);
        }
    }
}
