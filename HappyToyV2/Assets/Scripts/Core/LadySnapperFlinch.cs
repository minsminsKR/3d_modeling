using System;

namespace HappyToy.V2
{
    // One impact grants half a scaled gameplay second, never stacked time.
    public sealed class LadySnapperFlinch
    {
        public const float Duration = .5f, FrontRange = 2.5f;
        double remaining;
        public float Remaining => (float)remaining;
        public bool Held => remaining > 0;
        public static bool InFront(float distance, float height, float facingDot) =>
            distance >= 0 && distance <= FrontRange && Math.Abs(height) <= 2.4f && facingDot >= 0;
        public void Begin() { remaining = Duration; }
        public void Tick(float seconds, bool playing, bool timeStopped)
        {
            if (!playing || timeStopped || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            remaining = Math.Max(0, remaining - seconds);
        }
    }
}
