using System;

namespace HappyToy.V2
{
    /// <summary>Completed work never moves backwards or reports completion early.</summary>
    public sealed class LoadingProgress
    {
        public float Fraction { get; private set; }
        public bool Completed { get; private set; }
        public int Percent => Completed ? 100 : Math.Min(99, (int)Math.Floor(Fraction * 100d + .0001));
        public void Report(float fraction)
        {
            if (Completed || float.IsNaN(fraction) || float.IsInfinity(fraction)) return;
            Fraction = Math.Max(Fraction, Math.Max(0, Math.Min(.99f, fraction)));
        }
        public void Complete() { Fraction = 1; Completed = true; }
    }
}
