using System;

namespace HappyToy.V2
{
    // A single restraint consumes five scaled gameplay seconds. Callers own pause.
    public sealed class TrapHoldClock
    {
        public const float Duration = 5;
        double remaining;
        public float Remaining => (float)remaining;
        public bool Held => remaining > 0;
        public bool Begin()
        {
            if (Held) return false;
            remaining = Duration; return true;
        }
        public bool Tick(float seconds)
        {
            if (!Held || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            remaining = Math.Max(0, remaining - seconds);
            return !Held;
        }
        public void Reset() { remaining = 0; }
    }
}
