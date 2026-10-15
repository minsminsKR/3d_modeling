using System;

namespace HappyToy.V2
{
    // Callers supply elapsed gameplay time, including the item wheel's slow time.
    public sealed class TalismanBurnClock
    {
        public const float Duration = 10;
        double elapsed;
        bool burning;
        public bool Burning => burning;
        public float Remaining => burning ? (float)Math.Max(0, Duration - elapsed) : 0;
        public float BurnedFraction => (float)Math.Min(1, elapsed / Duration);

        public bool Begin()
        {
            if (burning) return false;
            elapsed = 0;
            burning = true;
            return true;
        }

        public bool Tick(float seconds)
        {
            if (!burning || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return false;
            elapsed = Math.Min(Duration, elapsed + seconds);
            if (elapsed < Duration) return false;
            burning = false;
            return true;
        }

        public void Reset() { elapsed = 0; burning = false; }
    }
}
