using System;

namespace HappyToy.V2
{
    // Pure, deterministic presentation cues derived from the saved flash index.
    public static class SchoolFlickerDread
    {
        // A missed step and one long lunge interrupt the regular strobe gait.
        // Eight flashes still travel 3.6 metres, matching the old average.
        static readonly float[] strides={.45f,0,.45f,1.05f,.15f,.30f,.60f,.60f};
        static readonly float[] light={.96f,.43f,.82f,.30f,1f,.52f,.72f,.38f};
        public static float Stride(long flash)
        {
            if(flash<1)throw new ArgumentOutOfRangeException(nameof(flash));
            long phase=flash-1;
            return strides[(int)((phase%strides.Length+(phase/strides.Length%strides.Length)*3)%strides.Length)];
        }
        public static float Light(long flash,int fixture)
        {
            if(flash<0||fixture<0)throw new ArgumentOutOfRangeException();
            return light[(int)((flash%light.Length+(flash/light.Length%light.Length)*3+fixture*3L)%light.Length)];
        }
        public static float Nearness(float distance)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)throw new ArgumentOutOfRangeException(nameof(distance));
            return Math.Max(0,Math.Min(1,(20-distance)/16));
        }
    }
}
