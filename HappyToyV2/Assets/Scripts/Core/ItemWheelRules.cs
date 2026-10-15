using System;

namespace HappyToy.V2
{
    public static class ItemWheelRules
    {
        public const float SlowTimeScale = .1f;
        public const float StickDeadZone = .25f;

        // x points right, y points up. Slot zero is at twelve o'clock.
        public static int Select(double x, double y, int slots, int previous)
        {
            if (slots < 1 || previous < 0 || previous >= slots)
                throw new ArgumentOutOfRangeException(nameof(slots));
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) ||
                x * x + y * y < StickDeadZone * StickDeadZone)
                return previous;
            double angle = Math.Atan2(x, y);
            if (angle < 0) angle += Math.PI * 2;
            return (int)Math.Floor(angle * slots / (Math.PI * 2) + .5) % slots;
        }
    }
}
