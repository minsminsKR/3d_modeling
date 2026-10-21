using System;

namespace HappyToy.V2
{
    /// <summary>Slow failing-ballast variation; the clock belongs to simulation time.</summary>
    public sealed class SchoolFluorescentClock
    {
        public double Elapsed { get; private set; }
        readonly double phase;
        public SchoolFluorescentClock(double seed) { phase=seed; }
        public float Advance(float delta,bool playing,bool reducedMotion)
        {
            if(playing&&!float.IsNaN(delta)&&!float.IsInfinity(delta)&&delta>0)Elapsed+=delta;
            if(reducedMotion)return 1;
            // Bounded irregular drift and an occasional soft voltage sag; never a strobe.
            double drift=.965+.025*Math.Sin(Elapsed*.87+phase)+.010*Math.Sin(Elapsed*2.13+phase*1.73);
            double sag=Math.Pow(Math.Max(0,Math.Sin(Elapsed*.29+phase*.83)),18)*.15;
            return (float)(drift-sag);
        }
    }
}
