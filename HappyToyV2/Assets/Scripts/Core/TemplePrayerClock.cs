using System;

namespace HappyToy.V2
{
    // The owner supplies simulation time only while the player can act.
    public sealed class TemplePrayerClock
    {
        readonly float duration;
        public float Elapsed { get; private set; }
        public bool Complete => Elapsed >= duration;

        public TemplePrayerClock(float duration, float elapsed = 0)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0 || elapsed > duration)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            this.duration = duration;
            Elapsed = elapsed;
        }

        public bool Advance(float delta, bool held)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (held || Complete) return false;
            Elapsed = Math.Min(duration, Elapsed + delta);
            return Complete;
        }
    }
}
