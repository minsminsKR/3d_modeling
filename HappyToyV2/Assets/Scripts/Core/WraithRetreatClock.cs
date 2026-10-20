using System;

namespace HappyToy.V2
{
    // A respite uses gameplay time supplied by the owner, including pause/traps.
    public sealed class WraithRetreatClock
    {
        public const float Duration = 24;
        double remaining;
        public float Remaining => (float)remaining;
        public bool Active => remaining > 0;
        public void Begin() { remaining = Duration; }
        public void Tick(float seconds)
        {
            if (seconds > 0 && !float.IsNaN(seconds) && !float.IsInfinity(seconds))
                remaining = Math.Max(0, remaining - seconds);
        }
        public void Restore(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds > Duration)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            remaining = seconds;
        }
        public void Reset() { remaining = 0; }
    }
}
