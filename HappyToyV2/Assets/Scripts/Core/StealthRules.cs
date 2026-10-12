using System;

namespace HappyToy.V2
{
    // Shared tuning and measured awareness. Pure C#: the checks execute this exact logic.
    public static class StealthRules
    {
        public const float AwarenessDecayPerSecond = .55f;
        public const float MaximumAwarenessStep = .25f;

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float FootstepRadius(bool crouching, bool running, bool wet)
        {
            // A crouched movement state always wins over a stale sprint input.
            if (crouching) return wet ? 3.5f : 1.75f;
            if (running) return wet ? 14 : 10;
            return wet ? 7 : 4;
        }

        public static float SightRange(bool crouching, bool lightOn, bool chasing, float baselineRange)
        {
            if (!Finite(baselineRange) || baselineRange <= 0) return 0;
            // Quiet posture helps avoid recognition, never deletes an established chase.
            if (chasing) return baselineRange;
            return baselineRange * (lightOn ? 1 : .7f) * (crouching ? .6f : 1);
        }

        public static float AcquisitionSeconds(bool crouching, bool lightOn, bool running, float distance)
        {
            if (!Finite(distance) || distance < 0) return float.PositiveInfinity;
            float seconds = (.45f + Math.Min(distance, 20) * .06f) *
                (lightOn ? 1 : 1.4f) * (crouching ? 1.5f : running ? .7f : 1);
            return Math.Max(.3f, Math.Min(2.8f, seconds));
        }

        public sealed class Awareness
        {
            public float Value { get; private set; }
            public bool Acquired => Value >= 1;

            // Value is accumulated evidence, rather than elapsed time divided by the
            // latest threshold. Changing stance/light cannot retroactively recognize a player.
            public bool Tick(bool visible, float acquisitionSeconds, float deltaTime)
            {
                if (!Finite(deltaTime) || deltaTime <= 0) return Acquired;
                if (visible && (!Finite(acquisitionSeconds) || acquisitionSeconds <= 0)) return Acquired;
                // A hitch cannot turn one visible frame into an instant acquisition.
                float step = Math.Min(deltaTime, MaximumAwarenessStep);
                float delta = visible ? step / Math.Max(.3f, acquisitionSeconds) : -step * AwarenessDecayPerSecond;
                Value = Math.Max(0, Math.Min(1, Value + delta));
                return Acquired;
            }

            public void Reset() { Value = 0; }
            public void Restore(float value)
            {
                if (!Finite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
                Value = value;
            }
        }
    }
}
